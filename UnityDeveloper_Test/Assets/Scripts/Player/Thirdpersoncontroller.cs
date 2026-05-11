using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Thirdpersoncontroller : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
 
    [Header("Jump")]
    public float jumpForce = 6f;
 
    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundRadius = 0.25f;
    public LayerMask groundMask;
 
    [Header("Custom Gravity")]
    public float gravityStrength = 20f;
    public float gravityTransitionSpeed = 8f;   // how fast player body re-aligns
 
    [Header("Fall-Death")]
    public float maxFallTime = 3f;
 
    private Rigidbody rb;
    private Animator  anim;
    private Camera    mainCam;
    private GravityHologram hologram;
    private Vector3 gravityDir = Vector3.down;
 
    private Vector3 pendingGravityDir = Vector3.down;
 
    private float gravitySpeed = 0f;
 
    private bool  isGrounded;
    private float airTimer = 0f;
 
    void Awake()
    {
        rb       = GetComponent<Rigidbody>();
        anim     = GetComponentInChildren<Animator>();
        hologram = GetComponent<GravityHologram>();
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
        Vector3 newPending = pendingGravityDir;
 
        if      (Input.GetKeyDown(KeyCode.UpArrow))    Debug.Log("Up arrow pressed");
        else if (Input.GetKeyDown(KeyCode.DownArrow))  Debug.Log("Down arrow pressed");
        else if (Input.GetKeyDown(KeyCode.LeftArrow))  Debug.Log("Left arrow pressed");
        else if (Input.GetKeyDown(KeyCode.RightArrow)) Debug.Log("Right arrow pressed");
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
 
        anim.SetFloat("Speed", speed, 0.1f, Time.deltaTime);
        anim.SetBool("IsGrounded", isGrounded);
        anim.SetBool("IsMoving", speed > 0.1f);
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundRadius);
        }
 
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, gravityDir * 2f);
 
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, pendingGravityDir * 2.5f);
    }
}