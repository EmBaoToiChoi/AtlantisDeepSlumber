using UnityEngine;

public class CustomCameraShake : MonoBehaviour
{
    private Vector3 originalPosition;
    private float shakeDurationRemaining = 0f;
    private float shakeMagnitude = 0f;
    private bool isContinuous = false;

    [Header("Default Settings")]
    public float defaultDuration = 0.5f;

    void OnEnable()
    {
        // Lưu lại vị trí đứng yên ban đầu của Cam3
        originalPosition = transform.localPosition;
    }

    // Kiểu 1: Rung một khoảng thời gian rồi tự tắt (Gọi tại Keyframe đơn lẻ)
    public void ShakeWithMagnitude(float magnitude)
    {
        shakeMagnitude = magnitude;
        shakeDurationRemaining = defaultDuration;
        isContinuous = false;
    }

    // Kiểu 2: Bắt đầu đoạn rung (Gọi tại Keyframe A)
    public void StartContinuousShake(float magnitude)
    {
        shakeMagnitude = magnitude;
        isContinuous = true;
    }

    // Kiểu 2: Dừng đoạn rung (Gọi tại Keyframe B)
    public void StopShake()
    {
        isContinuous = false;
        shakeDurationRemaining = 0f;
        transform.localPosition = originalPosition;
    }

    // LateUpdate chạy sau cùng ở mỗi frame, đồng bộ hoàn hảo với Unity Recorder
    void LateUpdate()
    {
        if (isContinuous || shakeDurationRemaining > 0)
        {
            // Tạo độ lệch ngẫu nhiên dựa trên độ mạnh magnitude
            float x = Random.Range(-1f, 1f) * shakeMagnitude;
            float y = Random.Range(-1f, 1f) * shakeMagnitude;

            // Áp vị trí mới cho camera
            transform.localPosition = originalPosition + new Vector3(x, y, 0);

            if (!isContinuous)
            {
                // Dùng unscaledDeltaTime để thời gian trừ lùi chính xác tuyệt đối khi Recorder khóa FPS
                shakeDurationRemaining -= Time.unscaledDeltaTime;
                if (shakeDurationRemaining <= 0)
                {
                    transform.localPosition = originalPosition;
                }
            }
        }
    }
}