using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PlayerDeathEffectManager : MonoBehaviour
{
    private static PlayerDeathEffectManager _instance;
    public static PlayerDeathEffectManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("PlayerDeathEffectManager");
                _instance = go.AddComponent<PlayerDeathEffectManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    private Canvas canvas;
    private RectTransform topEyelid;
    private RectTransform bottomEyelid;
    private Image centerFade;

    private bool isPlaying = false;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        CreateUI();
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        ResetDeathEffect();
    }

    private void CreateUI()
    {
        // 1. Create Canvas
        GameObject canvasGo = new GameObject("DeathEffectCanvas");
        canvasGo.transform.SetParent(transform);
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasGo.AddComponent<GraphicRaycaster>();

        // 2. Create Top Eyelid
        GameObject topGo = new GameObject("TopEyelid", typeof(RectTransform));
        topGo.transform.SetParent(canvasGo.transform, false);
        topEyelid = topGo.GetComponent<RectTransform>();
        topEyelid.anchorMin = new Vector2(0f, 1f); // Top Left
        topEyelid.anchorMax = new Vector2(1f, 1f); // Top Right
        topEyelid.pivot = new Vector2(0.5f, 1f);   // Pivot at top
        topEyelid.anchoredPosition = Vector3.zero;
        topEyelid.sizeDelta = new Vector2(0f, 0f); // Height starts at 0

        Image topImage = topGo.AddComponent<Image>();
        topImage.color = Color.black;

        // 3. Create Bottom Eyelid
        GameObject bottomGo = new GameObject("BottomEyelid", typeof(RectTransform));
        bottomGo.transform.SetParent(canvasGo.transform, false);
        bottomEyelid = bottomGo.GetComponent<RectTransform>();
        bottomEyelid.anchorMin = new Vector2(0f, 0f); // Bottom Left
        bottomEyelid.anchorMax = new Vector2(1f, 0f); // Bottom Right
        bottomEyelid.pivot = new Vector2(0.5f, 0f);   // Pivot at bottom
        bottomEyelid.anchoredPosition = Vector3.zero;
        bottomEyelid.sizeDelta = new Vector2(0f, 0f); // Height starts at 0

        Image bottomImage = bottomGo.AddComponent<Image>();
        bottomImage.color = Color.black;

        // 4. Create Center Fade Overlay (for overall blur/darkening)
        GameObject fadeGo = new GameObject("CenterFade", typeof(RectTransform));
        fadeGo.transform.SetParent(canvasGo.transform, false);
        RectTransform fadeRect = fadeGo.GetComponent<RectTransform>();
        fadeRect.anchorMin = Vector2.zero;
        fadeRect.anchorMax = Vector2.one;
        fadeRect.anchoredPosition = Vector2.zero;
        fadeRect.sizeDelta = Vector2.zero;

        centerFade = fadeGo.AddComponent<Image>();
        centerFade.color = new Color(0f, 0f, 0f, 0f); // Starts transparent
    }

    public void PlayDeathEffect(float duration = 3.0f)
    {
        if (isPlaying) return;
        StartCoroutine(EyelidsDeathCoroutine(duration));
    }

    public void ResetDeathEffect()
    {
        StopAllCoroutines();
        isPlaying = false;
        if (topEyelid != null) topEyelid.sizeDelta = new Vector2(0f, 0f);
        if (bottomEyelid != null) bottomEyelid.sizeDelta = new Vector2(0f, 0f);
        if (centerFade != null) centerFade.color = new Color(0f, 0f, 0f, 0f);
    }

    private IEnumerator EyelidsDeathCoroutine(float duration)
    {
        isPlaying = true;
        float elapsed = 0f;

        float screenHeight = 1080f; // scaler's reference height
        if (canvas != null)
        {
            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null) screenHeight = scaler.referenceResolution.y;
        }
        float targetHeight = screenHeight / 2f + 50f; // Overlap slightly to avoid gaps in center

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float percent = Mathf.Clamp01(elapsed / duration);

            // Easing function for smooth organic eyelids closing (ease-in-out)
            float t = percent * percent * (3f - 2f * percent);

            // Animate eyelids height
            float currentHeight = targetHeight * t;
            if (topEyelid != null) topEyelid.sizeDelta = new Vector2(0f, currentHeight);
            if (bottomEyelid != null) bottomEyelid.sizeDelta = new Vector2(0f, currentHeight);

            // Animate overall fade
            if (centerFade != null) centerFade.color = new Color(0f, 0f, 0f, percent * 0.9f);

            yield return null;
        }

        // Set final values
        if (topEyelid != null) topEyelid.sizeDelta = new Vector2(0f, targetHeight);
        if (bottomEyelid != null) bottomEyelid.sizeDelta = new Vector2(0f, targetHeight);
        if (centerFade != null) centerFade.color = new Color(0f, 0f, 0f, 1.0f); // Fully black
    }
}
