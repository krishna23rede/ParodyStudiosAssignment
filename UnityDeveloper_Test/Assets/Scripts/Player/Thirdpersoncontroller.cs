using UnityEngine;

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
 
    [Header("Fall-Death")]
    public float maxFallTime = 3f;
 
    private Rigidbody rb;
    private Animator anim;
    private Camera    mainCam;
    private Vector3 gravityDir = Vector3.down;
 
    private Vector3 pendingGravityDir = Vector3.down;
 
    private float gravitySpeed = 0f;
 
    private bool  isGrounded;
    private float airTimer = 0f;

    private Vector3 hitPoint;
    private bool hasHit;    
    private Vector3 currentDir = Vector3.zero;

    private static readonly int GroundHash = Animator.StringToHash("IsGrounded");
    private static readonly int MoveHash = Animator.StringToHash("IsMoving");
 
    void Awake()
    {
        rb       = GetComponent<Rigidbody>();
        anim     = GetComponentInChildren<Animator>();
        mainCam  = Camera.main;
 
        rb.useGravity     = false;
        rb.freezeRotation = true;
        rb.interpolation  = RigidbodyInterpolation.Interpolate;
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
        HandleMovement();
        ApplyGravityVelocity();
    }

    void CheckGround()
    {
        isGrounded = Physics.CheckSphere(groundCheck.position, groundRadius, groundMask);
        if (isGrounded && gravitySpeed < 0f)
            gravitySpeed = 0f;
    }
    void HandleJump()
    {
        if (isGrounded && Input.GetKeyDown(KeyCode.Space))
        {
            gravitySpeed = jumpForce;
        }
    }
    void HandleGravityInput()
    {
        if (Input.GetKeyDown(KeyCode.Return))
        {
            hologram?.Hide();
            currentDir = Vector3.zero;
            return;
        }

        Vector3 up = transform.up;
        Vector3 forward = Vector3.ProjectOnPlane(mainCam.transform.forward, up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(mainCam.transform.right, up).normalized;

        if (Input.GetKeyDown(KeyCode.UpArrow))
            currentDir  = forward;
        else if (Input.GetKeyDown(KeyCode.DownArrow))
            currentDir  = -forward;
        else if (Input.GetKeyDown(KeyCode.RightArrow))
            currentDir  = right;
        else if (Input.GetKeyDown(KeyCode.LeftArrow))
            currentDir  = -right;

    }
    void HandleMovement()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
 
        Vector3 playerUp   = transform.up;
        Vector3 camForward = Vector3.ProjectOnPlane(mainCam.transform.forward, playerUp).normalized;
        Vector3 camRight   = Vector3.ProjectOnPlane(mainCam.transform.right,   playerUp).normalized;
 
        Vector3 moveDir = (camForward * v + camRight * h).normalized;
 
        Vector3 currentVel    = rb.velocity;
        float   gravComponent = Vector3.Dot(currentVel, gravityDir);   // signed fall speed
        Vector3 lateralVel    = moveDir * moveSpeed;
 
        rb.velocity = lateralVel + gravityDir * gravComponent;
 
        // Rotate to face move direction while keeping aligned to gravity
        if (moveDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir, playerUp);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRot,
                             rotationSpeed * Time.fixedDeltaTime));
        }
    }

    void UpdateGravityPreview()
    {
        if (currentDir == Vector3.zero) return;

        Vector3 offset = new Vector3(0f, 1f, 0f); 
        Vector3 origin = transform.position + offset;

        if (Physics.Raycast(origin, currentDir, out RaycastHit hit, rayDistance, groundMask))
        {
            pendingGravityDir = -hit.normal;
            hasHit = true;
            hitPoint = hit.point;

            hologram?.ShowPreview(pendingGravityDir, hitPoint);
            Debug.DrawRay(origin, currentDir * rayDistance, Color.green);
        }
        else
        {
            hasHit = false;
            hologram?.Hide();
            Debug.DrawRay(origin, currentDir * rayDistance, Color.red);
        }
    }

    void ApplyGravityVelocity()
    {
        if (!isGrounded)
            gravitySpeed -= gravityStrength * Time.fixedDeltaTime;
 
        // Replace just the gravity-axis component of velocity
        Vector3 lateralVel = rb.velocity - gravityDir * Vector3.Dot(rb.velocity, gravityDir);
        rb.velocity  = lateralVel + gravityDir * (-gravitySpeed);
    }
 
    void TrackFallTime()
    {
        if (!isGrounded)
        {
            airTimer += Time.deltaTime;
            if (airTimer >= maxFallTime)
                GameManager.Instance.TriggerGameOver(GameOverReason.Fell);
        }
        else
        {
            airTimer = 0f;
        }
    }
    void DriveAnimations()
    {
        float speed = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")).magnitude;
 
        if(anim != null)
        {
            anim.SetBool(GroundHash, isGrounded);
            anim.SetBool(MoveHash, speed > 0.1f);            
        }


        if(hologram != null && hologram.isActive())
        {
            hologram.UpdateAnimation(speed, isGrounded);
        }
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