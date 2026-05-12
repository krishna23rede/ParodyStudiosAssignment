using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    #region Inspector fields

    [Header("Target")]
    public Transform target;
 
    public Transform playerBody;
 
    [Header("Spring Arm")]
    [Range(0.5f, 30f)]
    public float armLength = 5f;
    public Vector3 pivotOffset = new Vector3(0f, 1.5f, 0f);
 
    [Range(1f, 30f)]
    public float positionSmoothSpeed = 10f;
 
    [Range(1f, 30f)]
    public float orientationSmoothSpeed = 8f;
 
    [Header("Mouse Input")]
    [Range(0.01f, 10f)]
    public float yawSensitivity = 2f;
 
    [Range(0.01f, 10f)]
    public float pitchSensitivity = 2f;
 
    public bool invertPitch = false;
 
    [Header("Pitch Limits")]
    [Range(0f, 89f)]
    public float pitchMin = -30f;   // negative = looking down
 
    [Range(0f, 89f)]
    public float pitchMax = 70f;    // positive = looking up
 
    [Header("Collision")]
    public LayerMask collisionLayers = ~0; // all layers by default
 
    [Range(0.05f, 1f)]
    public float collisionProbeRadius = 0.15f;
 
    [Range(0.1f, 2f)]
    public float minArmLength = 0.5f;
 
    [Range(1f, 30f)]
    public float armReturnSpeed = 10f;
 
    [Header("Debug")]
    public bool showDebugGizmos = true;

    #endregion
 
    #region Public Properties
 
    /// Camera forward direction adjusted to match the player surface.
    public Vector3 CameraForwardOnPlane { get; private set; }
 
    // Camera right direction adjusted to match the player surface.
    public Vector3 CameraRightOnPlane { get; private set; }

    #endregion
 
    #region Private State
    private float _yaw;
    private float _pitch;
 
    // Smoothed pivot position and orientation (gravity-aligned to player up).
    private Vector3 _smoothPivotPos;
    private Quaternion _smoothRigOrientation; // tracks player's gravity frame
 
    private float _currentArmLength;
 
    private Transform _self;
    #endregion
 
    private void Awake()
    {
        _self = transform;
 
        // Initialise smoothed values.
        if (target != null)
        {
            _smoothPivotPos = GetDesiredPivotWorldPos();
            _smoothRigOrientation = GetPlayerGravityOrientation();
        }
        else
        {
            _smoothPivotPos = _self.position;
            _smoothRigOrientation = Quaternion.identity;
        }
 
        _currentArmLength = armLength;
 
        _yaw = 0f;
        _pitch = 10f; // slight downward look feels natural as a default
    }
 
    private void LateUpdate()
    {
        if (target == null) return;
 
        GatherMouseInput();
        SmoothPivotPosition();
        SmoothRigOrientation();
        ApplyCameraTransform();
        UpdateMovementDirections();
    }
 
    // Mouse Input
    private void GatherMouseInput()
    {
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");
 
        // Apply sensitivity.
        _yaw   += mouseX * yawSensitivity;
        _pitch += mouseY * pitchSensitivity * (invertPitch ? 1f : -1f);
 
        // Keep yaw in [0, 360) to avoid float precision drift over long play sessions.
        _yaw = Mathf.Repeat(_yaw, 360f);
 
        // pitchMin is negative (look down), pitchMax is positive (look up).
        _pitch = Mathf.Clamp(_pitch, -pitchMax, -pitchMin);
    }
 
    // Smooth Pivot Position
    private void SmoothPivotPosition()
    {
        Vector3 desired = GetDesiredPivotWorldPos();
        _smoothPivotPos = Vector3.Lerp(_smoothPivotPos, desired,
                                        1f - Mathf.Exp(-positionSmoothSpeed * Time.deltaTime));
    }
 
    private Vector3 GetDesiredPivotWorldPos()
    {
        // Transform pivotOffset from player's local space to world space.
        return target.position + target.TransformDirection(pivotOffset);
    }
 
    // Smooth Rig Orientation (Gravity Frame)
    private void SmoothRigOrientation()
    {
        Quaternion desiredOrientation = GetPlayerGravityOrientation();
 
        _smoothRigOrientation = Quaternion.Slerp(
            _smoothRigOrientation,
            desiredOrientation,
            1f - Mathf.Exp(-orientationSmoothSpeed * Time.deltaTime));
    }
 
    private Quaternion GetPlayerGravityOrientation()
    {
        Transform bodyRef = playerBody != null ? playerBody : target;
 
        Vector3 playerUp      = bodyRef.up;
        Vector3 playerForward = bodyRef.forward;
 
        if (Vector3.Cross(playerForward, playerUp).sqrMagnitude < 0.001f)
        {
            // Fall back to any perpendicular direction.
            playerForward = bodyRef.right;
        }
 
        // We use the overload LookRotation(forward, up) where 'up' is our gravity up.
        return Quaternion.LookRotation(
            Vector3.ProjectOnPlane(playerForward, playerUp).normalized,
            playerUp);
    }
 
    //  Build and Apply Camera Transform
    private void ApplyCameraTransform()
    {
        // --- Build yaw rotation around the CURRENT gravity up ---
        Quaternion yawRotation = Quaternion.AngleAxis(_yaw, _smoothRigOrientation * Vector3.up);
 
        // Apply yaw on top of the gravity frame to get the intermediate orientation.
        Quaternion afterYaw = yawRotation * _smoothRigOrientation;

        // --- Build pitch rotation around the rig's local right ---
        Quaternion pitchRotation = Quaternion.AngleAxis(_pitch, afterYaw * Vector3.right);
 
        // Final rig rotation: gravity frame → yaw → pitch.
        Quaternion finalRotation = pitchRotation * afterYaw;
 
        // The arm extends along the rig's local -Z direction (standard "behind" axis).
        Vector3 armDirection = finalRotation * Vector3.back; // same as -Vector3.forward
 
        // Probe for collision along the arm and get the safe arm length.
        float safeLength = ProbeCollision(_smoothPivotPos, armDirection);
 
        // Smoothly retract or re-extend the arm.
        if (safeLength < _currentArmLength)
        {
            // Instant retraction on collision → prevents camera clipping through walls.
            _currentArmLength = safeLength;
        }
        else
        {
            // Gradual extension when obstacle clears → prevents jarring pop-back.
            _currentArmLength = Mathf.Lerp(_currentArmLength, safeLength,
                                            1f - Mathf.Exp(-armReturnSpeed * Time.deltaTime));
        }
 
        // Final camera world position.
        Vector3 finalPosition = _smoothPivotPos + armDirection * _currentArmLength;
 
        // Apply to this Transform (the Camera GameObject).
        _self.position = finalPosition;
        _self.rotation = finalRotation;
    }
 
    // Spring Arm Collision Probe
    private float ProbeCollision(Vector3 pivotWorldPos, Vector3 armDir)
    {
        // SphereCast from pivot along the arm direction.
        if (Physics.SphereCast(
                pivotWorldPos,                  // origin
                collisionProbeRadius,           // sphere radius
                armDir,                         // direction
                out RaycastHit hit,             // result
                armLength,                      // max distance
                collisionLayers,
                QueryTriggerInteraction.Ignore))
        {
            // Pull the camera slightly closer than the hit point so the sphere
            float safeDistance = Mathf.Max(hit.distance - collisionProbeRadius * 2f,
                                           minArmLength);
            return safeDistance;
        }
 
        return armLength;
    }
 
    // Update Movement Direction Helpers
    private void UpdateMovementDirections()
    {
        Transform bodyRef = playerBody != null ? playerBody : target;
        Vector3 playerUp = bodyRef.up;
 
        // Project camera forward onto the plane perpendicular to playerUp.
        Vector3 camForward = _self.forward;
        Vector3 projForward = Vector3.ProjectOnPlane(camForward, playerUp);
 
        if (projForward.sqrMagnitude > 0.001f)
        {
            CameraForwardOnPlane = projForward.normalized;
        }
        else
        {
            // Edge case: camera is pointing straight up or down along gravity.
            // Fall back to the player's own forward on their movement plane.
            CameraForwardOnPlane = Vector3.ProjectOnPlane(target.forward, playerUp).normalized;
        }
 
        // Right is the cross product of forward × playerUp (right-hand rule).
        // This keeps right consistent with the player's gravity frame.
        CameraRightOnPlane = Vector3.Cross(playerUp, CameraForwardOnPlane).normalized;
    }
 
    // Snap Camera Behind Player
    public void SnapBehindPlayer()
    {
        _yaw = 0f; 

        // Force the smoothed orientation to match immediately.
        _smoothRigOrientation = GetPlayerGravityOrientation();
        _smoothPivotPos = GetDesiredPivotWorldPos();
        _currentArmLength = armLength;
 
        // Apply immediately so there's no lerp pop on the next frame.
        ApplyCameraTransform();
    }
 
    // Resets pitch to the supplied value (degrees, clamped to configured limits).
    public void SetPitch(float degrees)
    {
        _pitch = Mathf.Clamp(degrees, -pitchMax, -pitchMin);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || target == null) return;
 
        // Pivot sphere.
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(GetDesiredPivotWorldPos(), 0.1f);
 
        // Arm line.
        if (Application.isPlaying)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(_smoothPivotPos, _self.position);
 
            Gizmos.color = Color.green;
            Gizmos.DrawRay(_self.position, CameraForwardOnPlane);
 
            Gizmos.color = Color.red;
            Gizmos.DrawRay(_self.position, CameraRightOnPlane);
        }
    }
#endif
}