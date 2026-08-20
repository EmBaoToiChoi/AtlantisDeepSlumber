using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.UIElements;
using Unity.Netcode;

// Giải quyết tranh chấp namespace giữa UI cũ (UGUI) và UI Toolkit mới (UIElements)
using Image = UnityEngine.UI.Image;
using Button = UnityEngine.UI.Button;
using Cursor = UnityEngine.Cursor;

[RequireComponent(typeof(NetworkObject))]
public class LocalCutsceneVideoPlayer : NetworkBehaviour
{
    [Header("Video Configuration")]
    [Tooltip("Kéo thả file video CutsceneMoDau.mp4 vào đây")]
    public VideoClip videoClip;

    [Tooltip("CHỈ DÙNG CHO VPS/SERVER ẢO: Nhập chính xác thời lượng video (giây). Ví dụ 4 phút = 240.")]
    public float vpsVideoDuration = 183f;

    [Header("Waiting Settings")]
    [Tooltip("Thời gian chờ tối đa (giây) trước khi tự động chạy video (đề phòng có người chơi bị kẹt không load được map)")]
    public float maxWaitTimeout = 20f;

    [Tooltip("Kéo thả file video BackgroundLoading.mp4 vào đây để phát nền trong lúc chờ người chơi")]
    public VideoClip waitingVideoClip;

    [Header("Cutscene Background Music Settings")]
    [Tooltip("Âm lượng nhạc nền MainMenu khi bắt đầu chiếu Cutscene (0.25 = 25% làm nền nhẹ)")]
    [Range(0.0f, 1.0f)]
    public float cutsceneBgmVolume = 0.25f;

    [Tooltip("Thời gian (giây) nhạc nền tiếp tục phát sau khi Cutscene bắt đầu trước khi tự động tắt dần")]
    public float cutsceneBgmDuration = 55.0f;

    [Header("UI Styling")]
    [Tooltip("Màu nền phía sau video (mặc định là đen để che game load)")]
    public Color backgroundColor = Color.black;

    [Header("Objects to Hide After Cutscene")]
    [Tooltip("Kéo thả GameObject muốn ẩn sau khi xem xong Cutscene (nếu có thì ẩn, không có thì bỏ qua)")]
    public GameObject objectToHide;

    [Tooltip("Danh sách GameObject muốn ẩn sau khi xem xong Cutscene (nếu cần ẩn nhiều object)")]
    public List<GameObject> objectsToHide = new List<GameObject>();

    // --- Biến đồng bộ mạng ---
    private readonly NetworkVariable<int> readyClientsCount = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<bool> cutsceneStarted = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<bool> cutsceneFinished = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private VideoPlayer videoPlayer;
    private Canvas cutsceneCanvas;
    private RawImage videoRawImage;
    private RenderTexture videoRenderTexture;
    
    // --- Biến quản lý màn hình chờ người chơi ---
    private VideoPlayer waitingVideoPlayer;
    private RenderTexture waitingVideoRenderTexture;
    private RawImage waitingVideoRawImage;
    private GameObject waitingOverlayObj;
    private Text waitingText;
    private Text waitingSubText;
    private GameObject skipButtonObj;

    private bool isCutscenePlaying = false;
    private bool playerDisabled = false;
    private GameObject detectedPlayer = null;
    private bool isCutsceneEnded = false;
    private PlayerHUDController cachedHud = null;
    private Coroutine cutsceneBgmCoroutine = null;
    
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
            
            // Dùng số thời lượng cấu hình tay cho an toàn trên Server/VPS
            videoDuration = vpsVideoDuration;
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
                    Debug.Log($"[LocalCutsceneVideoPlayer] Đủ điều kiện bắt đầu cutscene. Sẵn sàng: {readyClientsCount.Value}/{target}. Timeout: {serverWaitTimer >= maxWaitTimeout}");
                    cutsceneStarted.Value = true;
                }
            }
            // 2. CHỈ đếm giờ nếu đang chạy trên VPS (isBatchMode = true)
            else if (!cutsceneFinished.Value && Application.isBatchMode)
            {
                // Giới hạn dt tối đa 0.1s mỗi frame để tránh giật lag làm trôi thời gian
                float dt = Time.unscaledDeltaTime;
                if (dt > 0.1f) dt = 0.1f;
                
                videoTimer += dt;
                if (videoTimer >= videoDuration)
                {
                    Debug.Log("[LocalCutsceneVideoPlayer] [SERVER VPS] Hết thời lượng video. Đang tự động kết thúc cutscene...");
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

        // 2. Nền đen dự phòng
        GameObject bgGo = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bgGo.transform.SetParent(canvasGo.transform, false);
        Image bgImage = bgGo.GetComponent<Image>();
        bgImage.color = backgroundColor;
        RectTransform bgRect = bgGo.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        // 3. Khởi tạo Video Nền lặp cho màn hình chờ (BackgroundLoading.mp4) nếu được cấu hình
#if UNITY_EDITOR
        if (waitingVideoClip == null)
        {
            waitingVideoClip = UnityEditor.AssetDatabase.LoadAssetAtPath<VideoClip>("Assets/PLuan/MainMenu/IMG/BackgroundLoading.mp4");
        }
#endif
        if (waitingVideoClip != null)
        {
            GameObject waitRawGo = new GameObject("WaitingVideoRawImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            waitRawGo.transform.SetParent(canvasGo.transform, false);
            waitingVideoRawImage = waitRawGo.GetComponent<RawImage>();
            RectTransform waitRawRect = waitRawGo.GetComponent<RectTransform>();
            waitRawRect.anchorMin = Vector2.zero;
            waitRawRect.anchorMax = Vector2.one;
            waitRawRect.sizeDelta = Vector2.zero;

            int rtW = Screen.width > 0 ? Screen.width : 1920;
            int rtH = Screen.height > 0 ? Screen.height : 1080;
            waitingVideoRenderTexture = new RenderTexture(rtW, rtH, 0);
            waitingVideoRenderTexture.name = "WaitingExplorers_RT";
            waitingVideoRawImage.texture = waitingVideoRenderTexture;

            waitingVideoPlayer = gameObject.AddComponent<VideoPlayer>();
            waitingVideoPlayer.playOnAwake = false;
            waitingVideoPlayer.source = VideoSource.VideoClip;
            waitingVideoPlayer.clip = waitingVideoClip;
            waitingVideoPlayer.isLooping = true; // Bật Loop cho video chờ người chơi
            waitingVideoPlayer.renderMode = VideoRenderMode.RenderTexture;
            waitingVideoPlayer.targetTexture = waitingVideoRenderTexture;
            waitingVideoPlayer.timeUpdateMode = VideoTimeUpdateMode.DSPTime;
            waitingVideoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            waitingVideoPlayer.Play();
        }

        // 4. Lớp Scrim / Vignette làm dịu video nền
        GameObject scrimGo = new GameObject("WaitingScrim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        scrimGo.transform.SetParent(canvasGo.transform, false);
        Image scrimImg = scrimGo.GetComponent<Image>();
        scrimImg.color = new Color(0.01f, 0.05f, 0.11f, 0.62f);
        RectTransform scrimRect = scrimGo.GetComponent<RectTransform>();
        scrimRect.anchorMin = Vector2.zero;
        scrimRect.anchorMax = Vector2.one;
        scrimRect.sizeDelta = Vector2.zero;

        // 5. RawImage để hiển thị video Cutscene chính (Lúc đầu ẩn đi)
        GameObject rawImageGo = new GameObject("VideoRawImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        rawImageGo.transform.SetParent(canvasGo.transform, false);
        videoRawImage = rawImageGo.GetComponent<RawImage>();
        videoRawImage.gameObject.SetActive(false);
        RectTransform videoRect = rawImageGo.GetComponent<RectTransform>();
        videoRect.anchorMin = Vector2.zero;
        videoRect.anchorMax = Vector2.one;
        videoRect.sizeDelta = Vector2.zero;

        // 6. Cụm Giao Diện Chờ Người Chơi Đáy Màn Hình (Không Khung Viền)
        waitingOverlayObj = new GameObject("WaitingOverlayContainer", typeof(RectTransform));
        waitingOverlayObj.transform.SetParent(canvasGo.transform, false);
        RectTransform cardRect = waitingOverlayObj.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0f);
        cardRect.anchorMax = new Vector2(0.5f, 0f);
        cardRect.pivot = new Vector2(0.5f, 0f);
        cardRect.anchoredPosition = new Vector2(0, 50);
        cardRect.sizeDelta = new Vector2(900, 100);

        // 6.1 Chữ chính chờ người chơi
        GameObject textGo = new GameObject("WaitingMainText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Shadow));
        textGo.transform.SetParent(waitingOverlayObj.transform, false);
        waitingText = textGo.GetComponent<Text>();
        waitingText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        waitingText.fontSize = 24;
        waitingText.fontStyle = FontStyle.Bold;
        waitingText.color = Color.white;
        waitingText.alignment = TextAnchor.MiddleCenter;
        waitingText.text = $"Đang chờ nhà thám hiểm ({readyClientsCount.Value}/4)";
        
        Shadow textShadow = textGo.GetComponent<Shadow>();
        textShadow.effectColor = new Color(0f, 0.9f, 1f, 0.75f);
        textShadow.effectDistance = new Vector2(0f, 0f);

        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0, 0.5f);
        textRect.anchorMax = new Vector2(1, 1);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        // 6.2 Chữ phụ / Gợi ý
        GameObject subGo = new GameObject("WaitingSubText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Shadow));
        subGo.transform.SetParent(waitingOverlayObj.transform, false);
        waitingSubText = subGo.GetComponent<Text>();
        waitingSubText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        waitingSubText.fontSize = 14;
        waitingSubText.color = new Color(0.78f, 0.94f, 1f, 0.9f);
        waitingSubText.alignment = TextAnchor.MiddleCenter;
        waitingSubText.text = "Đang đồng bộ hóa dữ liệu toàn bộ nhà thám hiểm...";

        Shadow subShadow = subGo.GetComponent<Shadow>();
        subShadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        subShadow.effectDistance = new Vector2(1f, -1f);

        RectTransform subRect = subGo.GetComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0, 0);
        subRect.anchorMax = new Vector2(1, 0.5f);
        subRect.offsetMin = Vector2.zero;
        subRect.offsetMax = Vector2.zero;
    }

    private void StartVideoPlayback()
    {
        isCutscenePlaying = true;
        
        // Ẩn và dọn dẹp các thành phần video & UI của màn hình chờ
        if (waitingVideoPlayer != null)
        {
            waitingVideoPlayer.Stop();
            Destroy(waitingVideoPlayer);
            waitingVideoPlayer = null;
        }
        if (waitingVideoRenderTexture != null)
        {
            waitingVideoRenderTexture.Release();
            Destroy(waitingVideoRenderTexture);
            waitingVideoRenderTexture = null;
        }
        if (waitingVideoRawImage != null)
        {
            Destroy(waitingVideoRawImage.gameObject);
            waitingVideoRawImage = null;
        }
        if (waitingOverlayObj != null)
        {
            Destroy(waitingOverlayObj);
            waitingOverlayObj = null;
        }

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

        // Đăng ký Event để biết khi nào video thực sự chiếu xong
        videoPlayer.loopPointReached += OnVideoCompleted;

        // CHỈ CHỦ PHÒNG (ROOM HOST) MỚI HIỂN THỊ NÚT SKIP
        if (IsLocalRoomHost())
        {
            CreateSkipButton(cutsceneCanvas.transform);
        }

        videoPlayer.Play();
        Debug.Log("[LocalCutsceneVideoPlayer] Video cutscene đã bắt đầu phát.");

        // Giảm nhỏ âm lượng BGM xuống 25% (20-30%) để làm nền cho Cutscene và bắt đầu đếm 55s trước khi tắt
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.FadeBGMToMultiplier(cutsceneBgmVolume, 1.5f);
            if (cutsceneBgmCoroutine != null) StopCoroutine(cutsceneBgmCoroutine);
            cutsceneBgmCoroutine = StartCoroutine(CutsceneBgmTimerCoroutine());
        }

        // Hiện con trỏ chuột cho chủ phòng bấm Skip nếu cần
        if (IsLocalRoomHost())
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private IEnumerator CutsceneBgmTimerCoroutine()
    {
        // Chờ đến trước mốc kết thúc 3 giây để fade out êm ái
        float waitTime = Mathf.Max(0f, cutsceneBgmDuration - 3.0f);
        float elapsed = 0f;
        while (elapsed < waitTime && isCutscenePlaying)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (isCutscenePlaying && AudioManager.Instance != null)
        {
            Debug.Log($"[LocalCutsceneVideoPlayer] Đạt mốc {cutsceneBgmDuration}s cutscene. Đang fade out và tắt nhạc nền...");
            AudioManager.Instance.FadeOutBGM(3.0f);
        }
        cutsceneBgmCoroutine = null;
    }

    // --- SỰ KIỆN KHI VIDEO PHÁT XONG (TỰ ĐỘNG) ---
    private void OnVideoCompleted(VideoPlayer vp)
    {
        // Gỡ event ra để tránh lỗi bộ nhớ
        vp.loopPointReached -= OnVideoCompleted;
        Debug.Log("[LocalCutsceneVideoPlayer] Video đã phát đến frame cuối cùng tự nhiên.");

        // Nếu máy này là Server (Host), ra lệnh kết thúc toàn bộ cho mọi người
        if (IsServer)
        {
            cutsceneFinished.Value = true;
        }
        else
        {
            // Nếu là Client xem xong, tự kết thúc ở máy mình
            EndCutscene();
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
        txt.text = "Bỏ qua (ESC)";
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
            waitingText.text = $"ĐANG CHỜ CÁC NHÀ THÁM HIỂM ({current}/4)...";
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

        // Dừng đếm giờ BGM Cutscene và khôi phục nhạc nền nhỏ cho gameplay
        if (cutsceneBgmCoroutine != null)
        {
            StopCoroutine(cutsceneBgmCoroutine);
            cutsceneBgmCoroutine = null;
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayAmbientBGM(cutsceneBgmVolume, 1.5f);
        }

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

        if (ZombieQuestTargetManager.Instance != null)
        {
            ZombieQuestTargetManager.Instance.StartQuest();
        }

        // Ẩn object sau khi kết thúc cutscene (nếu có)
        HideTargetObjects();

        // Khóa con trỏ chuột lại cho gameplay
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Giải phóng và dọn dẹp các tài nguyên giao diện
        if (waitingVideoPlayer != null)
        {
            waitingVideoPlayer.Stop();
            Destroy(waitingVideoPlayer);
            waitingVideoPlayer = null;
        }

        if (waitingVideoRenderTexture != null)
        {
            waitingVideoRenderTexture.Release();
            Destroy(waitingVideoRenderTexture);
            waitingVideoRenderTexture = null;
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

        // Nếu là Server, giải phóng Network Object này
        if (IsServer)
        {
            GetComponent<NetworkObject>().Despawn(true);
        }
    }

    private void HideTargetObjects()
    {
        if (objectToHide != null)
        {
            objectToHide.SetActive(false);
            Debug.Log($"[LocalCutsceneVideoPlayer] Đã ẩn object: {objectToHide.name}");
        }

        if (objectsToHide != null)
        {
            foreach (var obj in objectsToHide)
            {
                if (obj != null)
                {
                    obj.SetActive(false);
                    Debug.Log($"[LocalCutsceneVideoPlayer] Đã ẩn object: {obj.name}");
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (cutsceneBgmCoroutine != null)
        {
            StopCoroutine(cutsceneBgmCoroutine);
            cutsceneBgmCoroutine = null;
        }

        if (AudioManager.Instance != null && !cutsceneFinished.Value)
        {
            AudioManager.Instance.PlayAmbientBGM(cutsceneBgmVolume, 1.5f);
        }

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
            HideTargetObjects();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (waitingVideoPlayer != null)
        {
            waitingVideoPlayer.Stop();
            Destroy(waitingVideoPlayer);
            waitingVideoPlayer = null;
        }

        if (waitingVideoRenderTexture != null)
        {
            waitingVideoRenderTexture.Release();
            Destroy(waitingVideoRenderTexture);
            waitingVideoRenderTexture = null;
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