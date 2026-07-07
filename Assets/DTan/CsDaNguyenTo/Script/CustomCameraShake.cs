using System.Collections;
using UnityEngine;

public class CustomCameraShake : MonoBehaviour
{
    private Vector3 originalPosition;
    private Coroutine shakeCoroutine;

    // Tạo sẵn biến mặc định để chỉnh trên Inspector nếu cần
    [Header("Default Settings")]
    public float defaultDuration = 0.5f; 
    public float defaultMagnitude = 0.2f;

    void OnEnable()
    {
        originalPosition = transform.localPosition;
    }

    // --- KIỂU 1: RUNG 1 PHÁT RỒI TỰ TẮT (Chỉ nhận 1 tham số là Độ mạnh) ---
    // Thời gian rung sẽ lấy từ defaultDuration
    public void ShakeWithMagnitude(float magnitude)
    {
        if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
        shakeCoroutine = StartCoroutine(DoShake(defaultDuration, magnitude));
    }

    // --- KIỂU 2: RUNG TỪ KEYFRAME A ĐẾN KEYFRAME B ---
    // Gọi tại Keyframe A (Truyền vào độ mạnh magnitude)
    public void StartContinuousShake(float magnitude)
    {
        if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
        shakeCoroutine = StartCoroutine(DoContinuousShake(magnitude));
    }

    // Gọi tại Keyframe B (Không cần truyền gì cả)
    public void StopShake()
    {
        if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
        transform.localPosition = originalPosition;
    }

    // --- CÁC HÀM XỬ LÝ LOGIC NGẦM ---
    private IEnumerator DoShake(float duration, float magnitude)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;
            transform.localPosition = new Vector3(originalPosition.x + x, originalPosition.y + y, originalPosition.z);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.localPosition = originalPosition;
    }

    private IEnumerator DoContinuousShake(float magnitude)
    {
        while (true)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;
            transform.localPosition = new Vector3(originalPosition.x + x, originalPosition.y + y, originalPosition.z);
            yield return null;
        }
    }
}