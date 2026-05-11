using UnityEngine;

public class GravityHologram : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Root of the hologram hierarchy (parent of body, head, arm parts, etc.)")]
    public GameObject hologramRoot;
 
    [Tooltip("How far from the player to offset the hologram preview")]
    public float previewOffset = 1.5f;
 
    private SkinnedMeshRenderer[] parts;
 
    void Awake()
    {
        if (hologramRoot != null)
            parts = hologramRoot.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true);
 
        hologramRoot?.SetActive(false);
    }
 
    public void ShowPreview(Vector3 proposedGravityDir)
    {
        if (hologramRoot == null) return;
 
        hologramRoot.SetActive(true);
 
        hologramRoot.transform.position = transform.position + proposedGravityDir * previewOffset;
 
        hologramRoot.transform.rotation = Quaternion.FromToRotation(Vector3.up, -proposedGravityDir);
    }
 
    public void Hide()
    {
        hologramRoot?.SetActive(false);
    }
}
 