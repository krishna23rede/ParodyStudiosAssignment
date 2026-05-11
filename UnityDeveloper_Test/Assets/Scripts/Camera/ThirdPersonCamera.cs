using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public Thirdpersoncontroller player;

    [Header("Distance Settings")]
    public float distance = 5f;
    public float height = 2f;

    [Header("Smoothing")]
    public float positionSmooth = 12f;
    public float rotationSmooth = 12f;

    [Header("Orbit")]
    public float mouseSensitivity = 3f;
    public float minPitch = -30f;
    public float maxPitch = 70f;

    float yaw;
    float pitch = 20f;

    void LateUpdate()
    {
        if (!target || !player) return;

        Vector3 up = -player.gravityDir;

        // ── input orbit ─────────────────────────────
        if (Input.GetMouseButton(1))
        {
            yaw   += Input.GetAxis("Mouse X") * mouseSensitivity;
            pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        // ── build gravity-aligned frame ─────────────
        Vector3 forward = Vector3.ProjectOnPlane(target.forward, up);
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.ProjectOnPlane(target.up, up);

        forward.Normalize();

        Quaternion gravityFrame = Quaternion.LookRotation(forward, up);

        // apply orbit inside gravity frame
        Quaternion orbit =
            Quaternion.AngleAxis(yaw, up) *
            Quaternion.AngleAxis(pitch, Vector3.right);

        Quaternion finalRotation = gravityFrame * orbit;

        // ── smooth position follow ─────────────────
        Vector3 desiredPosition =
            target.position + finalRotation * new Vector3(0, height, -distance);

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            positionSmooth * Time.deltaTime
        );

        // ── smooth rotation ────────────────────────
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            finalRotation,
            rotationSmooth * Time.deltaTime
        );
    }
}