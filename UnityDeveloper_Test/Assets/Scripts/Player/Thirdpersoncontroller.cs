using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class Thirdpersoncontroller : MonoBehaviour
{
    #region Inspector Fields

    [Header("Movement")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    public GravityHologram hologram;

    [Header("Jump")]
    public float jumpForce = 6f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundRadius = 0.25f;
    public LayerMask groundMask;

    [Header("Custom Gravity")]
    public float gravityStrength = 20f;
    public float rayDistance = 5f;

    [Header("Gravity Switch")]
    public float switchDuration = 0.45f;  
    public AnimationCurve switchCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Fall-Death")]
    public float maxFallTime = 3f;

    public ThirdPersonCamera cameraController;

    #endregion

    #region Private Fields
    private Rigidbody rb;
    private Animator anim;
    private Camera mainCam;

    // Current gravity direction
    public Vector3 gravityDir = Vector3.down;

    // Gravity direction selected for switching
    private Vector3 pendingGravityDir = Vector3.down;

    // Vertical gravity velocity
    private float gravityVelocity  = 0f;

    // Prevent movement during gravity switch
    private bool isSwitching = false;

    // Used for upside-down jump switch
    private bool isSwitchingUpsideDown = false;

    // Ground check state
    private bool isGrounded;

    // Timer used for fall death
    private float airTimer = 0f;

    // Current direction used for gravity preview
    private Vector3 currentDir = Vector3.zero;

    // Raycast hit info
    private Vector3 hitPoint;
    private bool hasHit;

    // Prevent repeated jump spam
    private float lastJumpTime = -999f;

    // Animator hashes
    private static readonly int GroundHash = Animator.StringToHash("IsGrounded");
    private static readonly int MoveHash   = Animator.StringToHash("IsMoving");

    #endregion

    void Awake()
    {
        rb      = GetComponent<Rigidbody>();
        anim    = GetComponentInChildren<Animator>();
        mainCam = Camera.main;

         // Disable Unity gravity because we use custom gravity
        rb.useGravity = false;

        // Prevent physics rotation
        rb.freezeRotation = true;

        // Smooth rigidbody movement
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // Better collision handling
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    void Update()
    {
        HandleJump();
        HandleGravityInput();
        HandleFallTime();
        HandleAnimations();
    }

    void FixedUpdate()
    {
        CheckGround();

        // Disable movement while switching gravity
        if (!isSwitching)
            HandleMovement();

        ApplyGravityVelocity();
        
        UpdateGravityPreview();
    }

    void CheckGround()
    {
        isGrounded = Physics.CheckSphere(groundCheck.position, groundRadius, groundMask);

        // Reset gravity velocity when grounded
        if (isGrounded && gravityVelocity > 0f)
        {
            gravityVelocity = 0f;
            if(isSwitchingUpsideDown) isSwitchingUpsideDown = false;
        }
            
    }

    void HandleJump()
    {
        if (isSwitching || isSwitchingUpsideDown) return;
 
        if (Input.GetKeyDown(KeyCode.Space))
        {
            float timeSinceLast = Time.time - lastJumpTime;
 
            // If player jumps in air -> invert gravity
            if (!isGrounded)
            {
                lastJumpTime = -999f; 
                isSwitchingUpsideDown = true;
                StartCoroutine(SmoothGravitySwitch(-gravityDir));
                return;
            }

            // Normal grounded jump
            lastJumpTime = Time.time;
            if (isGrounded)
                gravityVelocity = -jumpForce;
        }
    }

    IEnumerator SmoothGravitySwitch(Vector3 targetGravDir)
    {
        isSwitching = true;

        // Small boost during switch
        gravityVelocity = -jumpForce;

        Vector3 fromGrav = gravityDir;
        Quaternion fromRot = rb.rotation;

        // New up direction after switch
        Vector3 targetUp = -targetGravDir;

        // Preserve forward direction
        Vector3 currentFwd = transform.forward;

        // Project forward onto new surface
        Vector3 projectedFwd = Vector3.ProjectOnPlane(currentFwd, targetUp).normalized;

        // Fallback if forward becomes invalid
        if (projectedFwd.sqrMagnitude < 0.001f)
        {
            projectedFwd = Vector3.ProjectOnPlane(transform.right, targetUp).normalized;
        }

        // Target rotation
        Quaternion toRot =  Quaternion.LookRotation(projectedFwd, targetUp);

        // Prevent huge velocity spikes
        float savedSpeed = gravityVelocity;
        gravityVelocity = Mathf.Max(savedSpeed, 1.5f);

        rb.velocity = Vector3.zero;

        float elapsed = 0f;

        while (elapsed < switchDuration)
        {
            elapsed += Time.deltaTime;

            float t = switchCurve.Evaluate(Mathf.Clamp01(elapsed / switchDuration));

            // Smooth gravity blend
            gravityDir = Vector3.Slerp(fromGrav, targetGravDir, t).normalized;

            // Smooth player rotation
            rb.MoveRotation( Quaternion.Slerp(fromRot, toRot, t));

            // Reduce velocity during switch
            rb.velocity = Vector3.Lerp(rb.velocity, Vector3.zero, t);

            yield return null;
        }

        // Final values
        gravityDir = targetGravDir;

        rb.MoveRotation(toRot);

        rb.velocity = Vector3.zero;

        gravityVelocity  = 0f;

        isSwitching = false;
    }

    void HandleGravityInput()
    {
        // Confirm gravity switch
        if (Input.GetKeyDown(KeyCode.Return))
        {
            if (hasHit && !isSwitching) StartCoroutine(SmoothGravitySwitch(pendingGravityDir));

            hologram?.Hide();
            currentDir = Vector3.zero;
            return;
        }

        // Camera directions aligned to player surface
        Vector3 up      = transform.up;
        Vector3 forward = Vector3.ProjectOnPlane(mainCam.transform.forward, up).normalized;
        Vector3 right   = Vector3.ProjectOnPlane(mainCam.transform.right,   up).normalized;

        // Select gravity preview direction
        if      (Input.GetKeyDown(KeyCode.UpArrow))    currentDir =  forward;
        else if (Input.GetKeyDown(KeyCode.DownArrow))  currentDir = -forward;
        else if (Input.GetKeyDown(KeyCode.RightArrow)) currentDir =  right;
        else if (Input.GetKeyDown(KeyCode.LeftArrow))  currentDir = -right;
    }

    void UpdateGravityPreview()
    {
        if (currentDir == Vector3.zero) return;

        Vector3 origin = transform.position + transform.up * 1f;

        // Check nearby surface
        if (Physics.Raycast(origin, currentDir, out RaycastHit hit, rayDistance, groundMask))
        {
            pendingGravityDir = -hit.normal;
            hasHit   = true;
            hitPoint = hit.point;
            hologram?.ShowPreview(pendingGravityDir, hitPoint);
            Debug.DrawRay(origin, currentDir * rayDistance, Color.green);
        }
        else
        {
            hasHit = false;
            Debug.DrawRay(origin, currentDir * rayDistance, Color.red);
        }
    }

    void HandleMovement()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        // Player's local up direction
        Vector3 gravityUp = -gravityDir;

        // Camera forward adjusted to match player surface
        Vector3 camForward = cameraController.CameraForwardOnPlane;
        Vector3 camRight   = cameraController.CameraRightOnPlane;

        // Final movement direction
        Vector3 move = camForward * v + camRight * h;
        Vector3 moveDir = move.sqrMagnitude > 1f ? move.normalized : move;

        // Preserve existing gravity velocity
        float gravComponent = Vector3.Dot(rb.velocity, gravityDir);

        // Horizontal movement velocity
        Vector3 lateralVel = moveDir * moveSpeed;

        rb.velocity = lateralVel + gravityDir * gravComponent;

        // Rotate player toward movement direction
        if (moveDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot =
                Quaternion.LookRotation(
                    moveDir,
                    gravityUp
                );

            rb.MoveRotation(
                Quaternion.Slerp(
                    rb.rotation,
                    targetRot,
                    rotationSpeed * Time.fixedDeltaTime
                )
            );
        }
    }

    void ApplyGravityVelocity()
    {
        if (isSwitching) return;

        // Accelerate downward while airborne
        if (!isGrounded) gravityVelocity += gravityStrength * Time.fixedDeltaTime;

        // Keep horizontal movement while applying gravity
        Vector3 lateralVel = rb.velocity - gravityDir * Vector3.Dot(rb.velocity, gravityDir);
        rb.velocity = lateralVel + gravityDir * gravityVelocity;
    }

    void HandleFallTime()
    {
        if (!isGrounded)
        {
            airTimer += Time.deltaTime;
            if (airTimer >= maxFallTime) GameManager.Instance.TriggerGameOver(GameOverReason.Fell);
        }
        else airTimer = 0f;
    }

    void HandleAnimations()
    {
        float speed = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")).magnitude;

        anim?.SetBool(GroundHash, isGrounded);
        anim?.SetBool(MoveHash,   speed > 0.1f);

        // Update hologram animation
        if (hologram != null && hologram.isActive())
            hologram.UpdateAnimation(speed);
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (groundCheck != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundRadius);
        }
        if (hasHit)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(hitPoint, 0.2f);
        }
    }
#endif
}