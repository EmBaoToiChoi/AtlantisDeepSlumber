using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using Unity.Netcode;

public class LocalCutsceneVideoPlayer : MonoBehaviour
{
    [Header("Video Configuration")]
    [Tooltip("Kéo thả file video CutsceneMoDau.mp4 vào đây")]
    public VideoClip videoClip;

    [Header("UI Styling")]
    [Tooltip("Màu nền phía sau video (mặc định là đen để che game load)")]
    public Color backgroundColor = Color.black;

    private VideoPlayer videoPlayer;
    private Canvas cutsceneCanvas;
    private RawImage videoRawImage;
    private RenderTexture videoRenderTexture;
    private GameObject skipButtonObj;
    
    private bool isPlaying = false;
    private bool playerDisabled = false;
    private GameObject detectedPlayer = null;

    private void Start()
    {
        if (videoClip == null)
        {
            Debug.LogError("[LocalCutsceneVideoPlayer] Chưa gán VideoClip! Vui lòng kéo thả file video vào ô Video Clip trên Inspector.");
            return;
        }

        StartCutscene();
    }

    private void StartCutscene()
    {
        isPlaying = true;
        playerDisabled = false;
        detectedPlayer = null;

        // 1. Tạo Canvas hiển thị video full-screen
        GameObject canvasGo = new GameObject("CutsceneCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cutsceneCanvas = canvasGo.GetComponent<Canvas>();
        cutsceneCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        cutsceneCanvas.sortingOrder = 99999; // Đảm bảo đè lên toàn bộ UI khác (kể cả loading screen hay game HUD)
        
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // 2. Tạo hình nền đen che map đang load bên dưới
        GameObject bgGo = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bgGo.transform.SetParent(canvasGo.transform, false);
        Image bgImage = bgGo.GetComponent<Image>();
        bgImage.color = backgroundColor;
        RectTransform bgRect = bgGo.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        // 3. Tạo RawImage để hứng hình ảnh từ Video
        GameObject rawImageGo = new GameObject("VideoRawImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        rawImageGo.transform.SetParent(canvasGo.transform, false);
        videoRawImage = rawImageGo.GetComponent<RawImage>();
        RectTransform videoRect = rawImageGo.GetComponent<RectTransform>();
        videoRect.anchorMin = Vector2.zero;
        videoRect.anchorMax = Vector2.one;
        videoRect.sizeDelta = Vector2.zero;

        // 4. Tạo RenderTexture động trùng khớp với độ phân giải màn hình hiện tại
        videoRenderTexture = new RenderTexture(Screen.width, Screen.height, 24);
        videoRawImage.texture = videoRenderTexture;

        // 5. Cấu hình VideoPlayer
        videoPlayer = gameObject.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.source = VideoSource.VideoClip;
        videoPlayer.clip = videoClip;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = videoRenderTexture;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct; // Phát âm thanh trực tiếp ra loa hệ thống

        // Đăng ký sự kiện khi chạy hết video
        videoPlayer.loopPointReached += OnVideoFinished;

        // 6. Thiết kế nút SKIP góc dưới cùng bên phải
        CreateSkipButton(canvasGo.transform);

        // Bắt đầu chạy video
        videoPlayer.Play();
        Debug.Log("[LocalCutsceneVideoPlayer] Đang chạy video cutscene mở đầu...");

        // Khóa chuột và làm mờ con trỏ trong quá trình xem cutscene
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void CreateSkipButton(Transform parent)
    {
        // Container cho nút Skip
        skipButtonObj = new GameObject("SkipButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        skipButtonObj.transform.SetParent(parent, false);

        RectTransform btnRect = skipButtonObj.GetComponent<RectTransform>();
        // Neo góc dưới bên phải (Bottom-Right)
        btnRect.anchorMin = new Vector2(1, 0);
        btnRect.anchorMax = new Vector2(1, 0);
        btnRect.pivot = new Vector2(1, 0);
        btnRect.sizeDelta = new Vector2(160, 50);
        btnRect.anchoredPosition = new Vector2(-80, 60);

        // Thiết kế background nút (Màu tối bán trong suốt - Glassmorphic style)
        Image btnImage = skipButtonObj.GetComponent<Image>();
        btnImage.color = new Color(0.1f, 0.1f, 0.1f, 0.7f);

        // Thêm text hiển thị chữ "SKIP (ESC)"
        GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textGo.transform.SetParent(skipButtonObj.transform, false);
        
        Text txt = textGo.GetComponent<Text>();
        txt.text = "SKIP (ESC)";
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 20;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;

        RectTransform txtRect = textGo.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.sizeDelta = Vector2.zero;

        // Hook sự kiện click chuột vào nút Skip
        Button btn = skipButtonObj.GetComponent<Button>();
        btn.onClick.AddListener(SkipCutscene);
    }

    private void Update()
    {
        if (isPlaying)
        {
            // Liên tục tìm và khóa di chuyển của người chơi cục bộ khi họ spawn ra
            DisableLocalPlayer();

            // Liên tục ẩn UI HUD của game trong lúc đang xem cutscene
            HideHUD();

            // Lắng nghe phím ESC để bỏ qua cutscene
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Debug.Log("[LocalCutsceneVideoPlayer] Nhấn ESC để bỏ qua cutscene.");
                SkipCutscene();
            }
        }
    }

    private void DisableLocalPlayer()
    {
        if (playerDisabled && detectedPlayer != null) return;

        // 1. Tìm qua NetworkManager (Chế độ Multiplayer)
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            var localClient = NetworkManager.Singleton.LocalClient;
            if (localClient != null && localClient.PlayerObject != null)
            {
                detectedPlayer = localClient.PlayerObject.gameObject;
                SetPlayerScriptsEnabled(detectedPlayer, false);
                playerDisabled = true;
                return;
            }
        }

        // 2. Tìm qua Tag "Player" (Chế độ offline/standalone hoặc khi object chưa kịp gán vào NetworkManager)
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject p in players)
        {
            var netObj = p.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsOwner)
            {
                detectedPlayer = p;
                SetPlayerScriptsEnabled(detectedPlayer, false);
                playerDisabled = true;
                return;
            }
            else if (netObj == null)
            {
                // Standalone test
                detectedPlayer = p;
                SetPlayerScriptsEnabled(detectedPlayer, false);
                playerDisabled = true;
                return;
            }
        }
    }

    private void SetPlayerScriptsEnabled(GameObject playerGo, bool enabled)
    {
        if (playerGo == null) return;

        // Tắt/Bật tất cả các class nhân vật có khả năng là của người chơi
        var leo = playerGo.GetComponent<LeoPlayer>() ?? playerGo.GetComponentInParent<LeoPlayer>();
        if (leo != null) leo.enabled = enabled;

        var arthur = playerGo.GetComponent<ArthurPlayer>() ?? playerGo.GetComponentInParent<ArthurPlayer>();
        if (arthur != null) arthur.enabled = enabled;

        var elena = playerGo.GetComponent<ElenaPlayer>() ?? playerGo.GetComponentInParent<ElenaPlayer>();
        if (elena != null) elena.enabled = enabled;

        var maya = playerGo.GetComponent<MayaPlayer>() ?? playerGo.GetComponentInParent<MayaPlayer>();
        if (maya != null) maya.enabled = enabled;

        // Triệt tiêu quán tính di chuyển để người chơi không tự động trượt đi
        if (!enabled)
        {
            Rigidbody rb = playerGo.GetComponent<Rigidbody>() ?? playerGo.GetComponentInParent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
        
        Debug.Log($"[LocalCutsceneVideoPlayer] Đã {(enabled ? "kích hoạt lại" : "tạm khóa")} di chuyển cho người chơi: {playerGo.name}");
    }

    private void HideHUD()
    {
        // Tìm và ẩn HUD canvas/UI Toolkit trong game để không hiển thị đè lên video
#if UNITY_2023_1_OR_NEWER
        PlayerHUDController hud = FindAnyObjectByType<PlayerHUDController>();
#else
        PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
#endif
        if (hud != null && hud.gameObject.activeSelf)
        {
            hud.gameObject.SetActive(false);
            Debug.Log("[LocalCutsceneVideoPlayer] Đã tạm ẩn HUD của người chơi.");
        }
    }

    private void ShowHUD()
    {
#if UNITY_2023_1_OR_NEWER
        PlayerHUDController hud = FindAnyObjectByType<PlayerHUDController>();
#else
        PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
#endif
        if (hud != null && !hud.gameObject.activeSelf)
        {
            hud.gameObject.SetActive(true);
            Debug.Log("[LocalCutsceneVideoPlayer] Đã khôi phục hiển thị HUD.");
        }
    }

    private void OnVideoFinished(VideoPlayer source)
    {
        Debug.Log("[LocalCutsceneVideoPlayer] Video đã chạy hết.");
        EndCutscene();
    }

    public void SkipCutscene()
    {
        Debug.Log("[LocalCutsceneVideoPlayer] Người chơi click bỏ qua cutscene.");
        EndCutscene();
    }

    private void EndCutscene()
    {
        if (!isPlaying) return;
        isPlaying = false;

        // Dừng và giải phóng video
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoFinished;
            videoPlayer.Stop();
        }

        // Kích hoạt lại điều khiển cho nhân vật
        if (detectedPlayer != null)
        {
            SetPlayerScriptsEnabled(detectedPlayer, true);
        }
        else
        {
            // Dự phòng: cố gắng tìm lại và bật nếu lúc xem video chưa spawn kịp
            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            foreach (GameObject p in players)
            {
                var netObj = p.GetComponent<NetworkObject>();
                if (netObj == null || netObj.IsOwner)
                {
                    SetPlayerScriptsEnabled(p, true);
                }
            }
        }

        // Hiện lại HUD game
        ShowHUD();

        // Tự động thu hồi con trỏ chuột theo cơ chế gốc của game
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Hủy Canvas và RenderTexture để giải phóng RAM/VRAM
        if (cutsceneCanvas != null)
        {
            Destroy(cutsceneCanvas.gameObject);
        }

        if (videoRenderTexture != null)
        {
            videoRenderTexture.Release();
            Destroy(videoRenderTexture);
        }

        Debug.Log("[LocalCutsceneVideoPlayer] Đã kết thúc Cutscene. Người chơi bắt đầu chơi.");
        
        // Hủy chính Script/GameObject quản lý để giải phóng tài nguyên hoàn toàn
        Destroy(gameObject);
    }
}
