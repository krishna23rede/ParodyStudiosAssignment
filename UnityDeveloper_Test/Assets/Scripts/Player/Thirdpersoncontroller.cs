using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class Thirdpersoncontroller : MonoBehaviour
{
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

    [Header("Double-Tap Flip")]

    private Rigidbody rb;
    private Animator anim;
    private Camera mainCam;

    public Vector3 gravityDir = Vector3.down;  
    private Vector3 pendingGravityDir = Vector3.down;
    private float gravitySpeed = 0f;           

    private bool isSwitching = false; 

    private bool isGrounded;
    private float airTimer = 0f;

    private Vector3 currentDir = Vector3.zero;
    private Vector3 hitPoint;
    private bool hasHit;
    private float lastJumpTime = -999f;  // initialized far in the past

    private static readonly int GroundHash = Animator.StringToHash("IsGrounded");
    private static readonly int MoveHash   = Animator.StringToHash("IsMoving");

    // ───────────────────────────────────────────────────────────
    void Awake()
    {
        rb      = GetComponent<Rigidbody>();
        anim    = GetComponentInChildren<Animator>();
        mainCam = Camera.main;

        rb.useGravity             = false;
        rb.freezeRotation         = true;
        rb.interpolation          = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    void Update()
    {
        CheckGround();
        HandleJump();
        HandleGravityInput();
        TrackFallTime();
        DriveAnimations();
        UpdateGravityPreview();
    }

    void FixedUpdate()
    {
        if (!isSwitching) HandleMovement();
        ApplyGravityVelocity();
    }

    // ── ground ─────────────────────────────────────────────────
    void CheckGround()
    {
        isGrounded = Physics.CheckSphere(groundCheck.position, groundRadius, groundMask);
        if (isGrounded && gravitySpeed < 0f) gravitySpeed = 0f;
    }

    void HandleJump()
    {
        if (isSwitching) return;
 
        if (Input.GetKeyDown(KeyCode.Space))
        {
            float timeSinceLast = Time.time - lastJumpTime;
 
            if (!isGrounded)
            {
                lastJumpTime = -999f; 
                StartCoroutine(SmoothGravitySwitch(-gravityDir));
                return;
            }
 
            lastJumpTime = Time.time;
            if (isGrounded)
                gravitySpeed = jumpForce;
        }
    }
    IEnumerator SmoothGravitySwitch(Vector3 targetGravDir)
    {
        isSwitching = true;

        gravitySpeed = jumpForce;

        Vector3    fromGrav = gravityDir;
        Quaternion fromRot  = rb.rotation;

        Vector3 targetUp      = -targetGravDir;
        Vector3 currentFwd    = transform.forward;
        Vector3 projectedFwd  = Vector3.ProjectOnPlane(currentFwd, targetUp).normalized;
        if (projectedFwd.sqrMagnitude < 0.001f)
            projectedFwd = Vector3.ProjectOnPlane(transform.right, targetUp).normalized;

        Quaternion toRot = Quaternion.LookRotation(projectedFwd, targetUp);

        float savedSpeed = gravitySpeed;
        gravitySpeed = Mathf.Max(savedSpeed, 1.5f);  
        rb.velocity  = Vector3.zero;

        float elapsed = 0f;

        while (elapsed < switchDuration)
        {
            elapsed += Time.deltaTime;
            float t = switchCurve.Evaluate(Mathf.Clamp01(elapsed / switchDuration));

            gravityDir = Vector3.Slerp(fromGrav, targetGravDir, t).normalized;

            rb.MoveRotation(Quaternion.Slerp(fromRot, toRot, t));

            rb.velocity = Vector3.Lerp(rb.velocity, Vector3.zero, t);

            yield return null; 
        }

        gravityDir = targetGravDir;
        rb.MoveRotation(toRot);
        rb.velocity  = Vector3.zero;
        gravitySpeed = 0f;      

        isSwitching = false;
    }
    void HandleGravityInput()
    {
        // ENTER → confirm switch
        if (Input.GetKeyDown(KeyCode.Return))
        {
            if (hasHit && !isSwitching)
                StartCoroutine(SmoothGravitySwitch(pendingGravityDir));

            hologram?.Hide();
            currentDir = Vector3.zero;
            return;
        }

        Vector3 up      = transform.up;
        Vector3 forward = Vector3.ProjectOnPlane(mainCam.transform.forward, up).normalized;
        Vector3 right   = Vector3.ProjectOnPlane(mainCam.transform.right,   up).normalized;

        if      (Input.GetKeyDown(KeyCode.UpArrow))    currentDir =  forward;
        else if (Input.GetKeyDown(KeyCode.DownArrow))  currentDir = -forward;
        else if (Input.GetKeyDown(KeyCode.RightArrow)) currentDir =  right;
        else if (Input.GetKeyDown(KeyCode.LeftArrow))  currentDir = -right;
    }

    void UpdateGravityPreview()
    {
        if (currentDir == Vector3.zero) return;

        Vector3 origin = transform.position + transform.up * 1f;

        if (Physics.Raycast(origin, currentDir, out RaycastHit hit, rayDistance, groundMask))
        {
            // pendingGravityDir = -hit.normal;
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

        Vector3 gravityUp = -gravityDir;

        // build stable camera forward relative to gravity
        Vector3 camForward = Vector3.ProjectOnPlane(mainCam.transform.forward, gravityUp).normalized;
        Vector3 camRight   = Vector3.Cross(gravityUp, camForward).normalized;

        Vector3 moveDir = (camForward * v + camRight * h).normalized;

        float   gravComponent = Vector3.Dot(rb.velocity, gravityDir);
        Vector3 lateralVel    = moveDir * moveSpeed;

        rb.velocity = lateralVel + gravityDir * gravComponent;

        if (moveDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir, gravityUp);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRot,
                            rotationSpeed * Time.fixedDeltaTime));
        }
    }

    void ApplyGravityVelocity()
    {
        if (isSwitching) return;   // coroutine owns velocity during transition

        if (!isGrounded)
            gravitySpeed -= gravityStrength * Time.fixedDeltaTime;

        Vector3 lateralVel = rb.velocity - gravityDir * Vector3.Dot(rb.velocity, gravityDir);
        rb.velocity = lateralVel + gravityDir * (-gravitySpeed);
    }

    void TrackFallTime()
    {
        if (!isGrounded)
        {
            airTimer += Time.deltaTime;
            if (airTimer >= maxFallTime)
                GameManager.Instance.TriggerGameOver(GameOverReason.Fell);
        }
        else airTimer = 0f;
    }

    void DriveAnimations()
    {
        float speed = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")).magnitude;

        anim?.SetBool(GroundHash, isGrounded);
        anim?.SetBool(MoveHash,   speed > 0.1f);

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