using UnityEngine;
using System.Collections;

public class CameraShakeTimeline : MonoBehaviour
{
    [Header("Camera Settings")]
    [Tooltip("Kéo Camera cậu muốn rung vào đây")]
    public Transform cameraTransform; 

    [Header("Shake Settings")]
    public float shakeDuration = 0.5f;   
    public float shakeMagnitude = 0.2f;  

    private bool isShaking = false;

    // Timeline sẽ gọi hàm này
    public void TriggerShake()
    {
        if (!isShaking && cameraTransform != null)
        {
            StartCoroutine(Shake());
        }
    }

    private IEnumerator Shake()
    {
        isShaking = true;
        // Lưu lại vị trí local ban đầu của camera
        Vector3 originalPos = cameraTransform.localPosition;
        float elapsed = 0.0f;

        while (elapsed < shakeDuration)
        {
            float x = Random.Range(-1f, 1f) * shakeMagnitude;
            float y = Random.Range(-1f, 1f) * shakeMagnitude;

            cameraTransform.localPosition = new Vector3(originalPos.x + x, originalPos.y + y, originalPos.z);

            elapsed += Time.deltaTime;
            yield return null; 
        }

        cameraTransform.localPosition = originalPos;
        isShaking = false;
    }
}