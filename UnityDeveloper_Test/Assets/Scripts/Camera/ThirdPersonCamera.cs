using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    public Transform target;

    [Header("Camera Settings")]
    public Vector3 offset = new Vector3(0f, 3f, -5f);
    public float mouseSensitivity = 100f;
    public float smoothSpeed = 10f;

    private float yaw;
    private float pitch;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Mouse Input
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        yaw += mouseX;
        pitch -= mouseY;

        // Clamp vertical rotation
        pitch = Mathf.Clamp(pitch, -30f, 60f);

        // Rotation
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

        // Desired position
        Vector3 desiredPosition = target.position + rotation * offset;

        // Smooth movement
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

        // Look at player
        transform.LookAt(target.position + Vector3.up * 1.5f);
    }
}