using UnityEngine;
using TMPro;

public class FPSCounter : MonoBehaviour
{
    public TextMeshProUGUI fpsText;

    void Update()
    {
        float fps = 1f / Time.unscaledDeltaTime;
        fpsText.text = "FPS: " + Mathf.Round(fps);
    }
}