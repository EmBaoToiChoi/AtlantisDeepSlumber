using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Gắn script này vào GameObject "CompleteUI" (Cái hiển thị chữ "PUZZLE 4 COMPLETED").
/// Script sẽ tự động chạy hiệu ứng Animation (Fade in, văng bự ra rồi thu lại xíu)
/// mỗi khi GameObject này được SetActive(true).
/// </summary>
public class MinigameCompleteUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Text chữ PUZZLE COMPLETED. Nếu để trống sẽ tự tìm trong object này.")]
    public TMP_Text completeText;

    [Header("Animation Settings")]
    public float animDuration = 1f;
    
    // Gradient màu chữ (tùy chọn chớp tắt bóng bẩy)
    public Color startColor = new Color(1f, 1f, 1f, 0f); // Trong suốt
    public Color endColor = new Color(1f, 0.8f, 0.2f, 1f); // Vàng gold chói

    private Vector3 originalScale;

    private void Awake()
    {
        // Tự động tìm TMP_Text nếu chưa gán
        if (completeText == null)
            completeText = GetComponentInChildren<TMP_Text>(true);

        originalScale = transform.localScale;
    }

    private void OnEnable()
    {
        // Reset lại trạng thái ban đầu trước khi diễn hoạt
        transform.localScale = Vector3.zero;

        if (completeText != null)
            completeText.color = startColor;

        // Bắt đầu chạy hiệu ứng
        StartCoroutine(PlayCompleteAnimation());
    }

    private IEnumerator PlayCompleteAnimation()
    {
        float timer = 0f;

        while (timer < animDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / animDuration;

            // 1. Hiệu ứng Scale (Ease Out Back - Nảy ra to rồi co lại xíu)
            // Công thức nội suy tạo độ nảy (overshoot)
            float scaleProgress = EaseOutBack(progress);
            transform.localScale = originalScale * scaleProgress;

            // 2. Hiệu ứng Fade Color
            if (completeText != null)
            {
                completeText.color = Color.Lerp(startColor, endColor, progress);
            }

            yield return null;
        }

        // Đảm bảo kết quả chuẩn ở cuối cùng
        transform.localScale = originalScale;
        if (completeText != null)
            completeText.color = endColor;

        // Bạn có thể thêm hiệu ứng nhấp nháy chữ sau khi hiện xong ở đây (tùy ý)
        StartCoroutine(GlowEffect());
    }

    // Hiệu ứng chữ chớp nháy nhẹ liên tục sau khi hiện xong
    private IEnumerator GlowEffect()
    {
        if (completeText == null) yield break;

        Color glowColor = new Color(1f, 1f, 0.8f, 1f); // Vàng sáng trắng

        while (true)
        {
            float pingPong = Mathf.PingPong(Time.time * 2f, 1f);
            completeText.color = Color.Lerp(endColor, glowColor, pingPong);
            yield return null;
        }
    }

    // Hàm toán học tạo hiệu ứng nảy (Bouncing/Elastic)
    private float EaseOutBack(float t)
    {
        float c1 = 1.70158f;
        float c3 = c1 + 1f;

        t = t - 1f;
        return 1f + c3 * Mathf.Pow(t, 3) + c1 * Mathf.Pow(t, 2);
    }
}
