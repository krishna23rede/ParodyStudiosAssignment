using UnityEngine;

public class CollectibleCube : MonoBehaviour
{
    [Header("Visual Feedback")]
    public float spinSpeed = 90f;
    public float bobHeight = 0.2f;
    public float bobSpeed  = 2f;
 
    private Vector3 startPos;
 
    void Start()
    {
        startPos = transform.position;
    }
 
    void Update()
    {
        // Spin + bob so the cubes are easy to spot
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
        float bob = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = startPos + Vector3.up * bob;
    }
 
    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
 
        GameManager.Instance?.RegisterCubeCollected();
        Destroy(gameObject);
    }
}
 