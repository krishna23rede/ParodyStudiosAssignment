using UnityEngine;

public class GravityHologram : MonoBehaviour
{
    [Header("References")]
 
    [Tooltip("How far from the player to offset the hologram preview")]
    public float previewOffset = 1.5f;
 
    private SkinnedMeshRenderer[] parts;

    private Animator anim;

    private static readonly int GroundHash = Animator.StringToHash("IsGrounded");
    private static readonly int MoveHash = Animator.StringToHash("IsMoving");
 
    void Awake()
    {
        parts = GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true);
        anim  = GetComponentInChildren<Animator>();
        gameObject.SetActive(false);
    }
    public void UpdateAnimation(float speedHash, bool groundHash)
    {
        anim.SetBool(GroundHash, groundHash);
        anim.SetBool(MoveHash, speedHash > 0.1f);
    }
    public void ShowPreview(Vector3 proposedGravityDir, Vector3 hitPoint)
    { 
        gameObject.SetActive(true);
 
        // gameObject.transform.position = transform.position + proposedGravityDir * previewOffset;
        gameObject.transform.position = hitPoint;
 
        gameObject.transform.rotation = Quaternion.FromToRotation(Vector3.up, -proposedGravityDir);
    }
 
    public void Hide()
    {
        gameObject?.SetActive(false);
    }

    public bool isActive()
    {
        return gameObject.activeSelf;
    }
}
 