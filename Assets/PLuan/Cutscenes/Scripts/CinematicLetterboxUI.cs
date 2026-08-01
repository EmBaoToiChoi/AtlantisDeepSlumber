using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace PLuan.Cutscenes
{
    /// <summary>
    /// Hiệu ứng khung đen điện ảnh (Letterbox / Cinematic Black Bars) ở trên và dưới màn hình.
    /// Gán script này vào Canvas UI chứa 2 thanh RectTransform (TopBar và BottomBar).
    /// </summary>
    public class CinematicLetterboxUI : MonoBehaviour
    {
        [Header("--- UI Elements ---")]
        [SerializeField] private RectTransform topBar;
        [SerializeField] private RectTransform bottomBar;

        [Header("--- Settings ---")]
        [Tooltip("Chiều cao của thanh đen (pixel).")]
        [SerializeField] private float barHeight = 100f;

        [Tooltip("Tốc độ trượt thanh đen vào/ra.")]
        [SerializeField] private float animSpeed = 5f;

        private Coroutine activeCoroutine;

        private void Awake()
        {
            SetBarHeights(0f);
        }

        public void ShowLetterbox()
        {
            if (activeCoroutine != null) StopCoroutine(activeCoroutine);
            activeCoroutine = StartCoroutine(AnimateBars(barHeight));
        }

        public void HideLetterbox()
        {
            if (activeCoroutine != null) StopCoroutine(activeCoroutine);
            activeCoroutine = StartCoroutine(AnimateBars(0f));
        }

        private IEnumerator AnimateBars(float targetHeight)
        {
            if (topBar == null || bottomBar == null) yield break;

            float currentHeight = topBar.sizeDelta.y;

            while (Mathf.Abs(currentHeight - targetHeight) > 0.5f)
            {
                currentHeight = Mathf.Lerp(currentHeight, targetHeight, Time.deltaTime * animSpeed);
                SetBarHeights(currentHeight);
                yield return null;
            }

            SetBarHeights(targetHeight);
        }

        private void SetBarHeights(float height)
        {
            if (topBar != null)
            {
                topBar.sizeDelta = new Vector2(topBar.sizeDelta.x, height);
            }

            if (bottomBar != null)
            {
                bottomBar.sizeDelta = new Vector2(bottomBar.sizeDelta.x, height);
            }
        }
    }
}
