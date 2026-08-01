using System.Collections;
using UnityEngine;

namespace PLuan.Cutscenes
{
    /// <summary>
    /// Script tạo hiệu ứng tự tăng Scale mượt mà từ 0 đến kích thước ban đầu (Original Scale).
    /// Rất thích hợp dùng cho Cutscene, xuất hiện vật thể/quái vật/VFX hoặc UI.
    /// </summary>
    public class ScaleUpEffect : MonoBehaviour
    {
        public enum EaseType
        {
            Linear,
            SmoothStep,
            EaseOutBack, // Hiệu ứng nảy nhẹ (Pop up) tạo cảm giác cực sinh động
            EaseOutBounce // Hiệu ứng tưng tưng như quả bóng
        }

        [Header("--- Scale Settings ---")]
        [Tooltip("Tự động chạy hiệu ứng khi GameObject được Bật (Active)? (Rất hợp cho Timeline Activation Track)")]
        [SerializeField] private bool playOnEnable = true;

        [Tooltip("Thời gian tăng scale (giây)")]
        [SerializeField] private float duration = 1.0f;

        [Tooltip("Độ trễ trước khi bắt đầu tăng scale (giây)")]
        [SerializeField] private float delay = 0f;

        [Tooltip("Kiểu chuyển động Scale")]
        [SerializeField] private EaseType easeType = EaseType.EaseOutBack;

        private Vector3 targetScale;
        private Coroutine scaleCoroutine;

        private void Awake()
        {
            // Lưu lại Scale ban đầu của Object trong Inspector làm targetScale
            targetScale = transform.localScale;
        }

        private void OnEnable()
        {
            if (playOnEnable)
            {
                PlayScaleUp();
            }
        }

        /// <summary>
        /// Bắt đầu hiệu ứng tăng Scale từ 0 -> Target Scale
        /// </summary>
        public void PlayScaleUp()
        {
            if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
            scaleCoroutine = StartCoroutine(ScaleRoutine());
        }

        /// <summary>
        /// Bắt đầu hiệu ứng giảm Scale từ Target Scale -> 0
        /// </summary>
        public void PlayScaleDown()
        {
            if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
            scaleCoroutine = StartCoroutine(ScaleDownRoutine());
        }

        private IEnumerator ScaleRoutine()
        {
            // Đặt scale về 0 ngay lập tức
            transform.localScale = Vector3.zero;

            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float easedT = EvaluateEase(t);

                transform.localScale = Vector3.LerpUnclamped(Vector3.zero, targetScale, easedT);
                yield return null;
            }

            transform.localScale = targetScale;
        }

        private IEnumerator ScaleDownRoutine()
        {
            Vector3 startScale = transform.localScale;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
                yield return null;
            }

            transform.localScale = Vector3.zero;
        }

        private float EvaluateEase(float t)
        {
            switch (easeType)
            {
                case EaseType.SmoothStep:
                    return t * t * (3f - 2f * t);

                case EaseType.EaseOutBack:
                    // Tạo hiệu ứng phóng to quá đà một chút rồi giật nhẹ về đúng size (Pop Up)
                    float c1 = 1.70158f;
                    float c3 = c1 + 1f;
                    return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);

                case EaseType.EaseOutBounce:
                    float n1 = 7.5625f;
                    float d1 = 2.75f;
                    if (t < 1 / d1) return n1 * t * t;
                    else if (t < 2 / d1) return n1 * (t -= 1.5f / d1) * t + 0.75f;
                    else if (t < 2.5 / d1) return n1 * (t -= 2.25f / d1) * t + 0.9375f;
                    else return n1 * (t -= 2.625f / d1) * t + 0.984375f;

                case EaseType.Linear:
                default:
                    return t;
            }
        }
    }
}
