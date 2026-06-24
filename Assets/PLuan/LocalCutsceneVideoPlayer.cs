using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
public class LocalCutsceneVideoPlayer : NetworkBehaviour
{
    [Header("Video Configuration")]
    [Tooltip("Kéo thả file video CutsceneMoDau.mp4 vào đây")]
    public VideoClip videoClip;

    [Header("Waiting Settings")]
    [Tooltip("Thời gian chờ tối đa (giây) trước khi tự động chạy video (đề phòng có người chơi bị kẹt không load được map)")]
    public float maxWaitTimeout = 20f;

    [Header("UI Styling")]
    [Tooltip("Màu nền phía sau video (mặc định là đen để che game load)")]
    public Color backgroundColor = Color.black;

    // --- Biến đồng bộ mạng ---
    private readonly NetworkVariable<int> readyClientsCount = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<bool> cutsceneStarted = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<bool> cutsceneFinished = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private VideoPlayer videoPlayer;
    private Canvas cutsceneCanvas;
    private RawImage videoRawImage;
    private RenderTexture videoRenderTexture;
    
    private GameObject waitingOverlayObj;
    private Text waitingText;
    private GameObject skipButtonObj;

    private bool isCutscenePlaying = false;
    private bool playerDisabled = false;
    private GameObject detectedPlayer = null;
    private bool isCutsceneEnded = false;
    private PlayerHUDController cachedHud = null;
    
    // Quản lý đếm thời gian
    private float serverWaitTimer = 0f;
    private float videoDuration = 0f;
    private float videoTimer = 0f;
    
    private readonly HashSet<ulong> serverReadyClients = new HashSet<ulong>();

    private void Awake()
    {
        // Tạm dừng thời gian game ngay lập tức khi load map
        Time.timeScale = 0f;
        
        // Khởi tạo màn hình chờ và ẩn HUD game ngay lập tức
        CreateWaitingOverlay();
        SetHUDVisible(false);
    }

    public override void OnNetworkSpawn()
    {
        // Đăng ký nhận sự kiện thay đổi trạng thái từ server
        readyClientsCount.OnValueChanged += OnReadyClientsCountChanged;
        cutsceneStarted.OnValueChanged += OnCutsceneStartedChanged;
        cutsceneFinished.OnValueChanged += OnCutsceneFinishedChanged;

        // Cập nhật text chờ ban đầu
        UpdateWaitingText(readyClientsCount.Value, GetTargetPlayersCount());

        // Kiểm tra trạng thái hiện tại phòng trường hợp client kết nối trễ
        if (cutsceneFinished.Value)
        {
            EndCutscene();
        }
        else if (cutsceneStarted.Value)
        {
            StartVideoPlayback();
        }
        else
        {
            // Báo cho Server biết Client này đã load xong scene và sẵn sàng
            NotifyClientReadyServerRpc(NetworkManager.Singleton.LocalClientId);
        }

        if (IsServer)
        {
            serverWaitTimer = 0f;
            serverReadyClients.Clear();
            
            // Tính toán thời gian video trên Server (Độc lập với card màn hình VPS)
            if (videoClip != null)
            {
                videoDuration = (float)videoClip.length;
            }
            else
            {
                videoDuration = 10f; // Hồi phòng khi không có clip
            }
            videoTimer = 0f;
        }
    }

    public override void OnNetworkDespawn()
    {
        readyClientsCount.OnValueChanged -= OnReadyClientsCountChanged;
        cutsceneStarted.OnValueChanged -= OnCutsceneStartedChanged;
        cutsceneFinished.OnValueChanged -= OnCutsceneFinishedChanged;
    }

    private void Update()
    {
        // --- LOGIC DÀNH RIÊNG CHO SERVER ---
        if (IsServer)
        {
            // 1. Quản lý đếm ngược thời gian chờ lúc load map
            if (!cutsceneStarted.Value)
            {
                serverWaitTimer += Time.unscaledDeltaTime;
                int target = GetTargetPlayersCount();

                if (readyClientsCount.Value >= target || serverWaitTimer >= maxWaitTimeout)
                {
                    Debug.Log($"[LocalCutsceneVideoPlayer] Đủ điều kiện bắt đầu. Sẵn sàng: {readyClientsCount.Value}/{target}. Timeout: {serverWaitTimer >= maxWaitTimeout}");
                    cutsceneStarted.Value = true;
                }
            }
            // 2. Quản lý tự động đếm giây hết video (Tránh lỗi VPS headless không chạy được VideoPlayer.loopPointReached)
            else if (!cutsceneFinished.Value)
            {
                videoTimer += Time.unscaledDeltaTime;
                if (videoTimer >= videoDuration)
                {
                    Debug.Log("[LocalCutsceneVideoPlayer] [SERVER] Hết thời lượng video. Đang tự động kết thúc cutscene...");
                    cutsceneFinished.Value = true;
                }
            }
        }

        // --- LOGIC DÀNH CHO CẢ CLIENT & SERVER ---
        if (!cutsceneFinished.Value)
        {
            // Liên tục tìm và khóa di chuyển của người chơi cục bộ khi họ vừa spawn
            DisableLocalPlayer();

            // Đảm bảo HUD luôn ẩn trong suốt quá trình chờ và phát video
            SetHUDVisible(false);

            // CHỈ CHỦ PHÒNG (ROOM HOST) MỚI ĐƯỢC BẤM ESC ĐỂ SKIP
            if (IsLocalRoomHost() && isCutscenePlaying && Input.GetKeyDown(KeyCode.Escape))
            {
                Debug.Log("[LocalCutsceneVideoPlayer] Chủ phòng nhấn ESC để bỏ qua cutscene.");
                SkipCutscene();
            }
        }
    }

    // --- QUẢN LÝ EVENT MẠNG ---

    private void OnReadyClientsCountChanged(int prev, int current)
    {
        UpdateWaitingText(current, GetTargetPlayersCount());
    }

    private void OnCutsceneStartedChanged(bool prev, bool started)
    {
        if (started && !isCutscenePlaying)
        {
            StartVideoPlayback();
        }
    }

    private void OnCutsceneFinishedChanged(bool prev, bool finished)
    {
        if (finished)
        {
            EndCutscene();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void NotifyClientReadyServerRpc(ulong clientId)
    {
        if (!IsServer) return;

        if (serverReadyClients.Add(clientId))
        {
            readyClientsCount.Value = serverReadyClients.Count;
            Debug.Log($"[LocalCutsceneVideoPlayer] Client {clientId} đã báo sẵn sàng. Tiến trình: {readyClientsCount.Value}/{GetTargetPlayersCount()}");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSkipCutsceneServerRpc()
    {
        if (!IsServer) return;
        
        Debug.Log("[LocalCutsceneVideoPlayer] [SERVER] Nhận yêu cầu Skip từ chủ phòng. Đang kết thúc cutscene cho toàn bộ người chơi...");
        cutsceneFinished.Value = true;
    }

    // --- LOGIC PLAY VIDEO & HÌNH ẢNH ---

    private void CreateWaitingOverlay()
    {
        // 1. Tạo Canvas Overlay đè lên toàn bộ game
        GameObject canvasGo = new GameObject("CutsceneCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cutsceneCanvas = canvasGo.GetComponent<Canvas>();
        cutsceneCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        cutsceneCanvas.sortingOrder = 99999;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // 2. Nền đen che game
        GameObject bgGo = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bgGo.transform.SetParent(canvasGo.transform, false);
        Image bgImage = bgGo.GetComponent<Image>();
        bgImage.color = backgroundColor;
        RectTransform bgRect = bgGo.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        // 3. RawImage để hiển thị video (Lúc đầu ẩn đi để hiện chữ chờ)
        GameObject rawImageGo = new GameObject("VideoRawImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        rawImageGo.transform.SetParent(canvasGo.transform, false);
        videoRawImage = rawImageGo.GetComponent<RawImage>();
        videoRawImage.gameObject.SetActive(false);
        RectTransform videoRect = rawImageGo.GetComponent<RectTransform>();
        videoRect.anchorMin = Vector2.zero;
        videoRect.anchorMax = Vector2.one;
        videoRect.sizeDelta = Vector2.zero;

        // 4. Chữ hiển thị đang chờ người chơi
        waitingOverlayObj = new GameObject("WaitingOverlayText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        waitingOverlayObj.transform.SetParent(canvasGo.transform, false);
        waitingText = waitingOverlayObj.GetComponent<Text>();
        waitingText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        waitingText.fontSize = 32;
        waitingText.color = Color.white;
        waitingText.alignment = TextAnchor.MiddleCenter;

        RectTransform textRect = waitingOverlayObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
    }

    private void StartVideoPlayback()
    {
        isCutscenePlaying = true;
        
        // Ẩn chữ chờ và hiển thị khung hình Video RawImage
        if (waitingOverlayObj != null) waitingOverlayObj.SetActive(false);
        if (videoRawImage != null) videoRawImage.gameObject.SetActive(true);

        // Tạo RenderTexture khớp độ phân giải màn hình
        videoRenderTexture = new RenderTexture(Screen.width, Screen.height, 24);
        videoRawImage.texture = videoRenderTexture;

        // Khởi tạo VideoPlayer
        videoPlayer = gameObject.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.source = VideoSource.VideoClip;
        videoPlayer.clip = videoClip;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = videoRenderTexture;
        // Bắt buộc sử dụng thời gian thực để video chạy bình thường kể cả khi Time.timeScale = 0
        videoPlayer.timeUpdateMode = VideoTimeUpdateMode.DSPTime;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;

        // CHỈ CHỦ PHÒNG (ROOM HOST) MỚI HIỂN THỊ NÚT SKIP
        if (IsLocalRoomHost())
        {
            CreateSkipButton(cutsceneCanvas.transform);
        }

        videoPlayer.Play();
        Debug.Log("[LocalCutsceneVideoPlayer] Video cutscene đã bắt đầu phát.");

        // Hiện con trỏ chuột cho chủ phòng bấm Skip nếu cần
        if (IsLocalRoomHost())
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void CreateSkipButton(Transform parent)
    {
        skipButtonObj = new GameObject("SkipButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        skipButtonObj.transform.SetParent(parent, false);

        RectTransform btnRect = skipButtonObj.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(1, 0);
        btnRect.anchorMax = new Vector2(1, 0);
        btnRect.pivot = new Vector2(1, 0);
        btnRect.sizeDelta = new Vector2(160, 50);
        btnRect.anchoredPosition = new Vector2(-80, 60);

        Image btnImage = skipButtonObj.GetComponent<Image>();
        btnImage.color = new Color(0.12f, 0.12f, 0.12f, 0.75f);

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

        Button btn = skipButtonObj.GetComponent<Button>();
        btn.onClick.AddListener(SkipCutscene);
    }

    private void UpdateWaitingText(int current, int target)
    {
        if (waitingText != null)
        {
            waitingText.text = $"WAITING FOR EXPLORERS ({current}/{target})...";
        }
    }

    private int GetTargetPlayersCount()
    {
        // Lấy số lượng người chơi thực tế trong phòng chờ đã được NetworkBootstrap ghi nhận
        if (NetworkBootstrap.ActivePlayerNames != null && NetworkBootstrap.ActivePlayerNames.Count > 0)
        {
            return NetworkBootstrap.ActivePlayerNames.Count;
        }

        // Chế độ Offline/Editor Quick Test
        return 1;
    }

    private bool IsLocalRoomHost()
    {
        // 1. Chạy thử trong Unity Editor (Host mode gốc)
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer && !Application.isBatchMode)
        {
            return true;
        }

        // 2. Chạy qua Dedicated Server trên VPS (đọc cấu hình từ PlayerPrefs)
        return PlayerPrefs.GetInt("IsRoomHost", 0) == 1;
    }

    // --- ĐIỀU KHIỂN INPUT NGƯỜI CHƠI ---

    private void DisableLocalPlayer()
    {
        if (playerDisabled && detectedPlayer != null) return;

        // Tìm kiếm player cục bộ
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

        var leo = playerGo.GetComponent<LeoPlayer>() ?? playerGo.GetComponentInParent<LeoPlayer>();
        if (leo != null) leo.enabled = enabled;

        var arthur = playerGo.GetComponent<ArthurPlayer>() ?? playerGo.GetComponentInParent<ArthurPlayer>();
        if (arthur != null) arthur.enabled = enabled;

        var elena = playerGo.GetComponent<ElenaPlayer>() ?? playerGo.GetComponentInParent<ElenaPlayer>();
        if (elena != null) elena.enabled = enabled;

        var maya = playerGo.GetComponent<MayaPlayer>() ?? playerGo.GetComponentInParent<MayaPlayer>();
        if (maya != null) maya.enabled = enabled;

        if (!enabled)
        {
            Rigidbody rb = playerGo.GetComponent<Rigidbody>() ?? playerGo.GetComponentInParent<Rigidbody>();
            if (rb != null)
            {
#if UNITY_6000_0_OR_NEWER || UNITY_2023_1_OR_NEWER
                rb.linearVelocity = Vector3.zero;
#else
                rb.velocity = Vector3.zero;
#endif
                rb.angularVelocity = Vector3.zero;
            }
        }
    }

    private void SetHUDVisible(bool visible)
    {
#if UNITY_2023_1_OR_NEWER
        PlayerHUDController[] huds = FindObjectsByType<PlayerHUDController>(FindObjectsSortMode.None);
#else
        PlayerHUDController[] huds = FindObjectsOfType<PlayerHUDController>();
#endif
        foreach (var hud in huds)
        {
            var uiDoc = hud.GetComponent<UIDocument>();
            if (uiDoc != null)
            {
                if (uiDoc.enabled != visible)
                {
                    uiDoc.enabled = visible;
                    Debug.Log($"[LocalCutsceneVideoPlayer] Đã {(visible ? "hiển thị" : "tạm ẩn")} UIDocument của: {hud.name}");
                }
            }
        }
    }

    // --- KẾT THÚC CUTSCENE ---

    public void SkipCutscene()
    {
        if (!IsLocalRoomHost()) return;
        
        Debug.Log("[LocalCutsceneVideoPlayer] Yêu cầu bỏ qua cutscene gửi lên Server...");
        RequestSkipCutsceneServerRpc();
    }

    private void EndCutscene()
    {
        if (isCutsceneEnded) return;
        isCutsceneEnded = true;

        isCutscenePlaying = false;

        // Dừng video phát
        if (videoPlayer != null)
        {
            videoPlayer.Stop();
        }

        // RESUME GAME (Kích hoạt lại tốc độ trò chơi bình thường)
        Time.timeScale = 1f;
        Debug.Log("[LocalCutsceneVideoPlayer] Trò chơi đã được kích hoạt lại (Time.timeScale = 1).");

        // Kích hoạt lại script điều khiển của người chơi cho tất cả đối tượng hợp lệ
        if (detectedPlayer != null)
        {
            SetPlayerScriptsEnabled(detectedPlayer, true);
        }

        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject p in players)
        {
            var netObj = p.GetComponent<NetworkObject>();
            if (netObj == null || netObj.IsOwner)
            {
                SetPlayerScriptsEnabled(p, true);
            }
        }

        // Khôi phục hiển thị HUD
        SetHUDVisible(true);

        // Khóa con trỏ chuột lại cho gameplay
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Giải phóng và dọn dẹp các tài nguyên giao diện
        if (cutsceneCanvas != null)
        {
            Destroy(cutsceneCanvas.gameObject);
        }

        if (videoRenderTexture != null)
        {
            videoRenderTexture.Release();
            Destroy(videoRenderTexture);
        }

        // Nếu là Server, giải phóng Network Object này
        if (IsServer)
        {
            GetComponent<NetworkObject>().Despawn(true);
        }
    }

    private void OnDestroy()
    {
        // Dự phòng dọn dẹp và khôi phục an toàn nếu đối tượng bị xóa đột ngột từ Server trước khi chạy hết
        if (!cutsceneFinished.Value)
        {
            Time.timeScale = 1f;
            if (detectedPlayer != null)
            {
                SetPlayerScriptsEnabled(detectedPlayer, true);
            }
            else
            {
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

            SetHUDVisible(true);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (cutsceneCanvas != null)
        {
            Destroy(cutsceneCanvas.gameObject);
        }

        if (videoRenderTexture != null)
        {
            videoRenderTexture.Release();
            Destroy(videoRenderTexture);
        }
    }
}
