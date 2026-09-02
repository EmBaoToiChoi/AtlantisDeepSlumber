using UnityEngine;
using UnityEngine.Video;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class VideoCutsceneController : NetworkBehaviour
{
    [Header("Video Settings")]
    public VideoPlayer videoPlayer;
    public GameObject videoUI; 
    public GameObject blackScreenUI; 
    [Tooltip("UI hoặc Object tạm ẩn trong lúc phát video (sẽ tự động hiện lại sau khi video kết thúc, ví dụ: UIPlayer)")]
    public GameObject objectToHide; 

    [Header("Hide Object After Cutscene")]
    [Tooltip("Kéo thả GameObject muốn ẩn sau khi chạy hết video cutscene (nếu có thì ẩn, không có thì bỏ qua)")]
    public GameObject objectToHideAfterVideo;
    [Tooltip("Danh sách các GameObject muốn ẩn sau khi chạy hết video cutscene (nếu cần ẩn nhiều object)")]
    public List<GameObject> objectsToHideAfterVideo = new List<GameObject>();

    [Header("Teleport & Control")]
    public Transform safeZone; 
    public List<Transform> playerSpots = new List<Transform>();
    public List<string> scriptNamesToDisable = new List<string>();
    [Tooltip("Bật tùy chọn này để VideoCutsceneController KHÔNG thực hiện bất kỳ lượt Teleport nào (để script bên ngoài như Minigame 4 tự quản lý teleport)")]
    public bool disableTeleport = false;

    [Header("Final Ending & Credits")]
    [Tooltip("Bật tùy chọn này nếu đây là Cutscene Cuối Cùng của game để chạy Epilogue (màn hình đen) + Credits + Quay về Menu")]
    public bool isFinalEndingCutscene = false;

    [TextArea(2, 5)]
    [Tooltip("Dòng chữ lắng đọng hiển thị trên màn hình đen sau khi hết video")]
    public string epilogueQuote = "Vực sâu nuốt chửng ánh sáng...\nNhưng giấc ngủ ngàn năm của Atlantis mới chỉ vừa bắt đầu.";

    [Tooltip("Thời gian hiển thị dòng chữ epilogue (giây)")]
    public float epilogueDuration = 5f;

    [Tooltip("Tốc độ cuộn của bảng Credit")]
    public float creditScrollSpeed = 65f;

    [Tooltip("Số lượt chạy lặp lại Credit trước khi tự động về MainMenu")]
    [Range(1, 10)]
    public int creditLoopCount = 2;

    [Tooltip("Âm thanh / Nhạc nền phát trong lúc chạy Credit (tùy chọn)")]
    public AudioClip endingMusic;

    [Tooltip("UI Canvas Credit tùy chỉnh nếu bạn muốn tự kéo thả (nếu để trống, hệ thống sẽ TỰ ĐỘNG TẠO giao diện Ending đẹp mắt)")]
    public GameObject customEndingCanvas;

    [Header("Security")]
    public bool playOnlyOnce = true;
    public bool hasPlayed = false;
    public bool HasPlayed => hasPlayed;

    [Header("Status")]
    public bool isPlaying = false;
    
    private bool serverReceivedFinishSignal = false;
    private bool hasRequestedSkip = false;
    private bool isEndingSequenceActive = false;
    private bool isExitingToMenu = false;

    // Danh sách lưu trữ ID của các người chơi đang đứng trong vùng Trigger
    private HashSet<ulong> playersInZone = new HashSet<ulong>();

    private void Update()
    {
        if (!IsSpawned) return;

        // Bỏ qua cutscene video thông thường bằng phím ESC
        if (videoUI != null && videoUI.activeSelf && !hasRequestedSkip && !isEndingSequenceActive)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                hasRequestedSkip = true; 
                Debug.Log("[VideoCutscene] Phát hiện phím ESC! Đang yêu cầu Skip cutscene cho cả phòng...");
                
                if (videoPlayer != null)
                {
                    videoPlayer.loopPointReached -= OnClientVideoFinished;
                }

                ReportFinishToServerRpc();
            }
        }

        // Bỏ qua phần Ending Credits bằng phím ESC hoặc Space
        if (isEndingSequenceActive && !isExitingToMenu)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space))
            {
                Debug.Log("[VideoCutscene] Phát hiện ESC/Space trong lúc chạy Ending Credits -> Chuyển về Menu chính!");
                ReturnToMainMenu();
            }
        }
    }

    public string GetCutsceneId()
    {
        return gameObject.name;
    }

    public void DisableTriggerCollider()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
        var cols = GetComponentsInChildren<Collider>();
        foreach (var c in cols)
        {
            if (c != null && c.isTrigger) c.enabled = false;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SyncCutscenePlayedStateServerRpc(string cutsceneName)
    {
        hasPlayed = true;
        HideObjectsAfterVideo();
        DisableTriggerCollider();
        Debug.Log($"[VideoCutsceneController] [SERVER] Client đã đồng bộ Cutscene '{cutsceneName}' đã xem -> hasPlayed = true");
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (SaveManager.IsContinueMode && playOnlyOnce)
        {
            if (SaveManager.IsCutscenePlayed(GetCutsceneId()))
            {
                hasPlayed = true;
                Debug.Log($"[VideoCutsceneController] Tiếp Tục Chơi: Cutscene '{GetCutsceneId()}' đã xem rồi, tắt trigger và bỏ qua.");
                HideObjectsAfterVideo();
                DisableTriggerCollider();

                if (!IsServer)
                {
                    SyncCutscenePlayedStateServerRpc(GetCutsceneId());
                }
            }
        }
    }

    public void StartCutscene()
    {
        if (playOnlyOnce && (hasPlayed || (SaveManager.IsContinueMode && SaveManager.IsCutscenePlayed(GetCutsceneId()))))
        {
            Debug.Log($"[VideoCutsceneController] StartCutscene bị hủy vì Cutscene '{GetCutsceneId()}' đã xem.");
            DisableTriggerCollider();
            return;
        }
        
        if (IsServer) StartCutsceneServer();
        else StartCutsceneServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void StartCutsceneServerRpc()
    {
        StartCutsceneServer();
    }

    private void StartCutsceneServer()
    {
        Debug.Log($"[VideoCutsceneController] StartCutsceneServer được gọi. isPlaying: {isPlaying}, isFinalEnding: {isFinalEndingCutscene}");
        if (isPlaying || (playOnlyOnce && hasPlayed)) return;
        isPlaying = true;
        hasPlayed = true; 
        serverReceivedFinishSignal = false; 
        DisableTriggerCollider();

        SaveManager.MarkCutscenePlayed(GetCutsceneId());
        
        PrepareCutsceneClientRpc();
        StartCoroutine(WaitAndTeleportAndFinish());
    }

    private IEnumerator WaitAndTeleportAndFinish()
    {
        yield return new WaitForSeconds(1f); // Đợi 1 giây để màn hình đen từ từ hiện lên hết

        int count = Mathf.Min(NetworkManager.Singleton.ConnectedClientsList.Count, playerSpots.Count);
        ulong[] targetClientIds = new ulong[count];
        for (int i = 0; i < count; i++)
        {
            targetClientIds[i] = NetworkManager.Singleton.ConnectedClientsList[i].ClientId;
        }

        if (!disableTeleport && safeZone != null && !isFinalEndingCutscene)
        {
            TeleportToSafeZoneClientRpc(targetClientIds);
        }

        PlayCutsceneClientRpc();

        yield return new WaitUntil(() => serverReceivedFinishSignal);

        Debug.Log("[VideoCutsceneController] Đã nhận tín hiệu kết thúc video (serverReceivedFinishSignal = true).");
        StopVideoClientRpc();

        // NẾU LÀ CUTSCENE CUỐI CÙNG -> CHUYỂN SANG LUỒNG ENDING & CREDITS
        if (isFinalEndingCutscene)
        {
            Debug.Log("[VideoCutsceneController] Kích hoạt Chuỗi Kết Thúc Ending & Credits cho toàn bộ phòng!");
            StartEndingSequenceClientRpc();
            isPlaying = false;
            yield break;
        }

        if (!disableTeleport && playerSpots != null && playerSpots.Count > 0)
        {
            TeleportAllPlayersClientRpc(targetClientIds);
        }

        yield return new WaitForSeconds(1f);
        FinishCutsceneClientRpc();

        HideObjectsAfterVideo();

        isPlaying = false;
    }

    [ClientRpc]
    private void PrepareCutsceneClientRpc()
    {
        TogglePlayerMovement(false); 
        CameraShakeHelper.StopShake(); 
        SetLocalPlayerCameraFollow(false);
        
        if (blackScreenUI != null) 
        {
            StartCoroutine(FadeCanvasGroup(blackScreenUI, 0f, 1f, 0.6f, false));
        }

        if (objectToHide != null) objectToHide.SetActive(false);
        if (videoPlayer != null) videoPlayer.Prepare(); 

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetCutsceneActive(true, 0.4f);
        }
    }

    [ClientRpc]
    private void PlayCutsceneClientRpc()
    {
        if (videoUI != null) videoUI.SetActive(true); 
        hasRequestedSkip = false; 

        if (videoPlayer != null)
        {
            videoPlayer.Play();
            videoPlayer.loopPointReached += OnClientVideoFinished;
        }
    }

    private void OnClientVideoFinished(VideoPlayer vp)
    {
        if (videoPlayer != null) videoPlayer.loopPointReached -= OnClientVideoFinished; 
        ReportFinishToServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void ReportFinishToServerRpc()
    {
        serverReceivedFinishSignal = true; 
    }

    [ClientRpc]
    private void StopVideoClientRpc()
    {
        if (videoPlayer != null)
        {
            videoPlayer.Stop();
            videoPlayer.loopPointReached -= OnClientVideoFinished; 
        }
        if (videoUI != null) videoUI.SetActive(false);
    }

    [ClientRpc]
    private void FinishCutsceneClientRpc()
    {
        SaveManager.MarkCutscenePlayed(GetCutsceneId());
        hasPlayed = true;
        DisableTriggerCollider();

        if (objectToHide != null) objectToHide.SetActive(true);
        HideObjectsAfterVideo();

        if (blackScreenUI != null) 
        {
            StartCoroutine(FadeCanvasGroup(blackScreenUI, 1f, 0f, 1f, true));
        }
        
        SetLocalPlayerCameraFollow(true);
        TogglePlayerMovement(true);
        isPlaying = false;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetCutsceneActive(false, 1.2f);
        }
    }

    // =========================================================================
    // LUỒNG ENDING, MÀN HÌNH ĐEN, CREDITS & QUAY VỀ MAIN MENU
    // =========================================================================

    [ClientRpc]
    private void StartEndingSequenceClientRpc()
    {
        SaveManager.MarkCutscenePlayed(GetCutsceneId());
        hasPlayed = true;
        DisableTriggerCollider();

        isEndingSequenceActive = true;
        isExitingToMenu = false;

        TogglePlayerMovement(false);
        SetLocalPlayerCameraFollow(false);

        if (objectToHide != null) objectToHide.SetActive(false);
        HideObjectsAfterVideo();

        // Mở khóa chuột để người chơi có thể bấm nút Bỏ Qua
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        PlayerHUDController.isAnyUIOpen = true;

        StartCoroutine(EndingSequenceRoutine());
    }

    private IEnumerator EndingSequenceRoutine()
    {
        // 1. Đảm bảo màn hình đen hiển thị hoàn toàn
        if (blackScreenUI != null)
        {
            blackScreenUI.SetActive(true);
            CanvasGroup bgCg = blackScreenUI.GetComponent<CanvasGroup>();
            if (bgCg == null) bgCg = blackScreenUI.AddComponent<CanvasGroup>();
            bgCg.alpha = 1f;
        }

        // Tạo hoặc chuẩn bị Canvas Ending
        GameObject endingCanvasObj = customEndingCanvas;

        if (endingCanvasObj == null)
        {
            endingCanvasObj = BuildDynamicEndingUI();
        }
        else
        {
            endingCanvasObj.SetActive(true);
        }

        CanvasGroup canvasGroup = endingCanvasObj.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = endingCanvasObj.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;

        // Phát nhạc Ending nếu có
        AudioSource endingAudioSource = null;
        if (endingMusic != null)
        {
            endingAudioSource = endingCanvasObj.AddComponent<AudioSource>();
            endingAudioSource.clip = endingMusic;
            endingAudioSource.loop = true;
            endingAudioSource.volume = 0.8f;
            endingAudioSource.Play();
        }

        // Tìm các thành phần UI
        Transform epiloguePanel = endingCanvasObj.transform.Find("EpiloguePanel");
        Transform creditsPanel = endingCanvasObj.transform.Find("CreditsPanel");
        Transform skipBtnTransform = endingCanvasObj.transform.Find("SkipButton");
        RectTransform creditsContainer = creditsPanel != null ? creditsPanel.Find("CreditsContainer") as RectTransform : null;

        if (skipBtnTransform != null)
        {
            Button skipBtn = skipBtnTransform.GetComponent<Button>();
            if (skipBtn != null)
            {
                skipBtn.onClick.RemoveAllListeners();
                skipBtn.onClick.AddListener(() => ReturnToMainMenu());
            }
        }

        // ==========================================
        // GIAI ĐOẠN 1: HIỆN DÒNG CHỮ LẮNG ĐỌNG (EPILOGUE QUOTE)
        // ==========================================
        if (epiloguePanel != null)
        {
            epiloguePanel.gameObject.SetActive(true);
            if (creditsPanel != null) creditsPanel.gameObject.SetActive(false);

            CanvasGroup epiCg = epiloguePanel.GetComponent<CanvasGroup>();
            if (epiCg == null) epiCg = epiloguePanel.gameObject.AddComponent<CanvasGroup>();

            // Fade in dòng chữ trong 1.5s
            float t = 0;
            while (t < 1.5f && !isExitingToMenu)
            {
                t += Time.unscaledDeltaTime;
                epiCg.alpha = Mathf.Lerp(0f, 1f, t / 1.5f);
                yield return null;
            }
            epiCg.alpha = 1f;

            // Giữ hiển thị trong epilogueDuration giây
            float holdTime = 0;
            while (holdTime < epilogueDuration && !isExitingToMenu)
            {
                holdTime += Time.unscaledDeltaTime;
                yield return null;
            }

            // Fade out dòng chữ trong 1.2s
            t = 0;
            while (t < 1.2f && !isExitingToMenu)
            {
                t += Time.unscaledDeltaTime;
                epiCg.alpha = Mathf.Lerp(1f, 0f, t / 1.2f);
                yield return null;
            }
            epiCg.alpha = 0f;
            epiloguePanel.gameObject.SetActive(false);
        }

        if (isExitingToMenu) yield break;

        yield return new WaitForSecondsRealtime(0.5f);

        // ==========================================
        // GIAI ĐOẠN 2: CHẠY BẢNG CREDITS CUỘN
        // ==========================================
        if (creditsPanel != null && creditsContainer != null)
        {
            creditsPanel.gameObject.SetActive(true);

            float startY = -Screen.height - 200f;
            float endY = creditsContainer.rect.height + Screen.height + 200f;
            int loopsCompleted = 0;

            creditsContainer.anchoredPosition = new Vector2(creditsContainer.anchoredPosition.x, startY);

            while (loopsCompleted < creditLoopCount && !isExitingToMenu)
            {
                float currentY = creditsContainer.anchoredPosition.y;
                currentY += creditScrollSpeed * Time.unscaledDeltaTime * 1.5f;
                creditsContainer.anchoredPosition = new Vector2(creditsContainer.anchoredPosition.x, currentY);

                if (currentY >= endY)
                {
                    loopsCompleted++;
                    Debug.Log($"[VideoCutsceneController] Hoàn thành {loopsCompleted}/{creditLoopCount} lượt chạy Credit.");
                    creditsContainer.anchoredPosition = new Vector2(creditsContainer.anchoredPosition.x, startY);
                    yield return new WaitForSecondsRealtime(0.5f);
                }

                yield return null;
            }
        }
        else
        {
            // Nếu không có panel credit, chờ 5s rồi về Menu
            yield return new WaitForSecondsRealtime(5f);
        }

        if (!isExitingToMenu)
        {
            ReturnToMainMenu();
        }
    }

    public void ReturnToMainMenu()
    {
        if (isExitingToMenu) return;
        isExitingToMenu = true;
        Debug.Log("[VideoCutsceneController] Đang thực hiện quay về MainMenu...");

        StartCoroutine(QuitToMainMenuRoutine());
    }

    private IEnumerator QuitToMainMenuRoutine()
    {
        // Rời phòng online nếu có
        string roomId = PlayerPrefs.GetString("CurrentRoomID", "");
        if (!string.IsNullOrEmpty(roomId))
        {
            _ = AuthService.LeaveRoom(roomId);
        }

        // Tắt Netcode
        if (NetworkManager.Singleton != null)
        {
            Debug.Log("[VideoCutsceneController] Ngắt kết nối NetworkManager...");
            NetworkManager.Singleton.Shutdown();
        }

        yield return null;
        yield return null;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        PlayerHUDController.isAnyUIOpen = false;

        if (SceneLoader.Instance != null)
        {
            _ = SceneLoader.Instance.LoadSceneAsync("MainMenu", "CẢM ƠN BẠN ĐÃ TRẢI NGHIỆM TRÒ CHƠI...");
        }
        else
        {
            SceneManager.LoadScene("MainMenu");
        }
    }

    // =========================================================================
    // HÀM TẠO DYNAMIC UI CHO ENDING & CREDITS
    // =========================================================================
    private GameObject BuildDynamicEndingUI()
    {
        GameObject canvasObj = new GameObject("Ending_Credits_Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9998;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // 1. Background màu đen
        GameObject bgObj = new GameObject("DarkBackground");
        bgObj.transform.SetParent(canvasObj.transform, false);
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.02f, 0.03f, 0.05f, 0.98f);
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        // 2. Epilogue Panel
        GameObject epiObj = new GameObject("EpiloguePanel");
        epiObj.transform.SetParent(canvasObj.transform, false);
        RectTransform epiRect = epiObj.AddComponent<RectTransform>();
        epiRect.anchorMin = Vector2.zero;
        epiRect.anchorMax = Vector2.one;
        epiRect.sizeDelta = Vector2.zero;
        epiObj.AddComponent<CanvasGroup>();

        GameObject epiTextObj = new GameObject("EpilogueText");
        epiTextObj.transform.SetParent(epiObj.transform, false);
        TextMeshProUGUI epiTmp = epiTextObj.AddComponent<TextMeshProUGUI>();
        epiTmp.text = $"<i><color=#E2B755>\"{epilogueQuote}\"</color></i>";
        epiTmp.fontSize = 42;
        epiTmp.alignment = TextAlignmentOptions.Center;
        epiTmp.color = Color.white;
        epiTmp.lineSpacing = 20;
        RectTransform epiTextRect = epiTextObj.GetComponent<RectTransform>();
        epiTextRect.anchorMin = new Vector2(0.1f, 0.2f);
        epiTextRect.anchorMax = new Vector2(0.9f, 0.8f);
        epiTextRect.sizeDelta = Vector2.zero;

        // 3. Credits Panel
        GameObject creditsPanelObj = new GameObject("CreditsPanel");
        creditsPanelObj.transform.SetParent(canvasObj.transform, false);
        RectTransform credPanelRect = creditsPanelObj.AddComponent<RectTransform>();
        credPanelRect.anchorMin = Vector2.zero;
        credPanelRect.anchorMax = Vector2.one;
        credPanelRect.sizeDelta = Vector2.zero;

        GameObject containerObj = new GameObject("CreditsContainer");
        containerObj.transform.SetParent(creditsPanelObj.transform, false);
        RectTransform contRect = containerObj.AddComponent<RectTransform>();
        contRect.anchorMin = new Vector2(0.5f, 0.5f);
        contRect.anchorMax = new Vector2(0.5f, 0.5f);
        contRect.pivot = new Vector2(0.5f, 0.5f);
        contRect.sizeDelta = new Vector2(1200, 2400);

        TextMeshProUGUI credTmp = containerObj.AddComponent<TextMeshProUGUI>();
        credTmp.alignment = TextAlignmentOptions.Center;
        credTmp.fontSize = 28;
        credTmp.color = new Color(0.9f, 0.95f, 1f, 0.95f);
        credTmp.lineSpacing = 16;
        credTmp.richText = true;

        string creditContent = 
            "<size=64><b><color=#5DE6FF>ATLANTIS: DEEP SLUMBER</color></b></size>\n\n" +
            "<size=32><b><color=#E2B755>ĐỒ ÁN TỐT NGHIỆP</color></b></size>\n" +
            "<size=22><color=#AAAAAA>FPT POLYTECHNIC / FPT UNIVERSITY</color></size>\n\n" +
            "───────────────────────────\n\n" +
            "<size=36><b><color=#FFD166>NHÓM PHÁT TRIỂN</color></b></size>\n" +
            "<size=42><b><color=#FFFFFF>HIGH FIVE TIDES</color></b></size>\n\n\n" +
            "<size=32><b><color=#E2B755>GIẢNG VIÊN HƯỚNG DẪN</color></b></size>\n" +
            "<size=36><b>Cô Hồ Thị Hồng Nga</b></size>\n\n\n" +
            "<size=32><b><color=#E2B755>LẬP TRÌNH HỆ THỐNG & GAMEPLAY</color></b></size>\n" +
            "<size=34>Vũ Phạm Luân\nNguyễn Mạnh Hoài Bảo\nHồ Hữu Hoàng\nHuỳnh Nhật Đông</size>\n\n\n" +
            "<size=32><b><color=#E2B755>CUTSCENE & CINEMATICS</color></b></size>\n" +
            "<size=34>Hồ Hữu Hoàng\nNguyễn Nhật Tân\nVũ Phạm Luân</size>\n\n\n" +
            "<size=32><b><color=#E2B755>CỐT TRUYỆN & KỊCH BẢN</color></b></size>\n" +
            "<size=34>High Five Tides</size>\n\n\n" +
            "<size=32><b><color=#E2B755>TÀI NGUYÊN 3D, HIỆU ỨNG & MÔI TRƯỜNG</color></b></size>\n" +
            "<size=30>Unity Asset Store & Online Creators Community\n(Bản quyền thương mại & Sưu tầm)</size>\n\n\n" +
            "<size=32><b><color=#E2B755>ÂM THANH & NHẠC NỀN</color></b></size>\n" +
            "<size=30>Freesound Community & Royalty-Free Audio Artists</size>\n\n\n" +
            "───────────────────────────\n\n" +
            "<size=32><b><color=#E2B755>LỜI CẢM ƠN ĐẶC BIỆT</color></b></size>\n" +
            "<size=28>Xin chân thành gửi lời cảm ơn sâu sắc nhất tới:\n" +
            "Cô Hồ Thị Hồng Nga đã tận tình hướng dẫn và đồng hành.\n" +
            "Quý Thầy Cô bộ môn đã chỉ dạy và hỗ trợ nhóm trong suốt quá trình học tập.\n" +
            "Gia đình & Bạn bè đã luôn cổ vũ, động viên.\n\n" +
            "Và đặc biệt là <b>BẠN</b> — Người đã đồng hành cùng Atlantis đến những giây phút cuối cùng!</size>\n\n\n\n" +
            "<size=54><b><color=#5DE6FF>THANK YOU FOR PLAYING!</color></b></size>\n\n";

        credTmp.text = creditContent;

        // 4. Skip Button ở góc trên bên phải
        GameObject skipBtnObj = new GameObject("SkipButton");
        skipBtnObj.transform.SetParent(canvasObj.transform, false);
        RectTransform skipRect = skipBtnObj.AddComponent<RectTransform>();
        skipRect.anchorMin = new Vector2(1, 1);
        skipRect.anchorMax = new Vector2(1, 1);
        skipRect.pivot = new Vector2(1, 1);
        skipRect.anchoredPosition = new Vector2(-40, -40);
        skipRect.sizeDelta = new Vector2(220, 55);

        Image skipImg = skipBtnObj.AddComponent<Image>();
        skipImg.color = new Color(0.12f, 0.16f, 0.22f, 0.85f);

        Button skipBtnComp = skipBtnObj.AddComponent<Button>();
        ColorBlock colors = skipBtnComp.colors;
        colors.normalColor = new Color(0.12f, 0.16f, 0.22f, 0.85f);
        colors.highlightedColor = new Color(0.2f, 0.45f, 0.65f, 0.95f);
        colors.pressedColor = new Color(0.1f, 0.3f, 0.5f, 1f);
        skipBtnComp.colors = colors;

        GameObject skipTextObj = new GameObject("Text");
        skipTextObj.transform.SetParent(skipBtnObj.transform, false);
        TextMeshProUGUI skipTmp = skipTextObj.AddComponent<TextMeshProUGUI>();
        skipTmp.text = "<b>BỎ QUA [ESC]  ⏩</b>";
        skipTmp.fontSize = 20;
        skipTmp.alignment = TextAlignmentOptions.Center;
        skipTmp.color = new Color(0.9f, 0.95f, 1f, 1f);
        RectTransform skipTextRect = skipTextObj.GetComponent<RectTransform>();
        skipTextRect.anchorMin = Vector2.zero;
        skipTextRect.anchorMax = Vector2.one;
        skipTextRect.sizeDelta = Vector2.zero;

        // Ẩn lúc đầu
        creditsPanelObj.SetActive(false);
        epiObj.SetActive(false);

        return canvasObj;
    }

    private void HideObjectsAfterVideo()
    {
        if (objectToHideAfterVideo != null)
        {
            objectToHideAfterVideo.SetActive(false);
            Debug.Log($"[VideoCutsceneController] Đã ẩn object sau video: {objectToHideAfterVideo.name}");
        }

        if (objectsToHideAfterVideo != null)
        {
            foreach (var obj in objectsToHideAfterVideo)
            {
                if (obj != null)
                {
                    obj.SetActive(false);
                    Debug.Log($"[VideoCutsceneController] Đã ẩn object sau video: {obj.name}");
                }
            }
        }
    }

    // =========================================================================
    // CÁC HÀM TELEPORT VÀ VẬT LÝ
    // =========================================================================

    [ClientRpc]
    private void TeleportToSafeZoneClientRpc(ulong[] mappedClientIds)
    {
        var localClientId = NetworkManager.Singleton.LocalClientId;
        var localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject;

        if (localPlayer != null && safeZone != null)
        {
            bool isTarget = false;
            foreach (var id in mappedClientIds)
            {
                if (id == localClientId) { isTarget = true; break; }
            }

            if (isTarget)
            {
                StartCoroutine(ForceTeleportRoutine(localPlayer.gameObject, safeZone.position, safeZone.rotation));
            }
        }
    }

    [ClientRpc]
    private void TeleportAllPlayersClientRpc(ulong[] mappedClientIds)
    {
        var localClientId = NetworkManager.Singleton.LocalClientId;
        var localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject;

        if (localPlayer != null)
        {
            int mySpotIndex = -1;
            for (int i = 0; i < mappedClientIds.Length; i++)
            {
                if (mappedClientIds[i] == localClientId)
                {
                    mySpotIndex = i;
                    break;
                }
            }

            if (mySpotIndex >= 0 && mySpotIndex < playerSpots.Count)
            {
                StartCoroutine(ForceTeleportRoutine(localPlayer.gameObject, playerSpots[mySpotIndex].position, playerSpots[mySpotIndex].rotation));
            }
        }
    }

    private IEnumerator ForceTeleportRoutine(GameObject playerObj, Vector3 targetPos, Quaternion targetRot)
    {
        var charCtrl = playerObj.GetComponent<CharacterController>();
        var navAgent = playerObj.GetComponent<UnityEngine.AI.NavMeshAgent>();

        if (charCtrl != null) charCtrl.enabled = false;
        if (navAgent != null) navAgent.enabled = false;

        yield return new WaitForEndOfFrame(); 

        playerObj.transform.position = targetPos;
        playerObj.transform.rotation = targetRot;
        
        Physics.SyncTransforms(); 

        yield return new WaitForEndOfFrame(); 

        if (charCtrl != null) charCtrl.enabled = true;
        if (navAgent != null) navAgent.enabled = true;
    }

    private void TogglePlayerMovement(bool enable)
    {
        var localPlayer = (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null) 
            ? NetworkManager.Singleton.LocalClient.PlayerObject : null;
        
        if (localPlayer != null)
        {
            MonoBehaviour[] allScripts = localPlayer.GetComponents<MonoBehaviour>();
            foreach (var script in allScripts)
            {
                if (script != null && scriptNamesToDisable.Contains(script.GetType().Name))
                {
                    script.enabled = enable;
                }
            }
        }
    }

    private void SetLocalPlayerCameraFollow(bool enable)
    {
        var localPlayer = (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null) 
            ? NetworkManager.Singleton.LocalClient.PlayerObject : null;
        
        if (localPlayer != null)
        {
            MonoBehaviour[] allScripts = localPlayer.GetComponents<MonoBehaviour>();
            foreach (var script in allScripts)
            {
                if (script == null) continue;
                var field = script.GetType().GetField("enableCameraFollow");
                if (field != null)
                {
                    field.SetValue(script, enable);
                }
            }
        }
    }

    // =========================================================================
    // LOGIC TRIGGER NHIỀU NGƯỜI CHƠI (LƯU TRẠNG THÁI)
    // =========================================================================

    private void OnTriggerEnter(Collider other)
    {
        if (playOnlyOnce && (hasPlayed || (SaveManager.IsContinueMode && SaveManager.IsCutscenePlayed(GetCutsceneId()))))
        {
            DisableTriggerCollider();
            return;
        }

        if (!IsSpawned || !IsServer) return; 
        if (isPlaying || (playOnlyOnce && hasPlayed)) return;

        if (other.CompareTag("Player"))
        {
            var netObj = other.GetComponentInParent<NetworkObject>();
            if (netObj != null && netObj.IsPlayerObject)
            {
                playersInZone.Add(netObj.OwnerClientId); 
                CheckCutsceneCondition();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Giữ nguyên để nhớ danh sách đã chạm
    }

    private void CheckCutsceneCondition()
    {
        int totalPlayersInRoom = NetworkManager.Singleton.ConnectedClientsList.Count;
        int requiredPlayers = Mathf.Max(1, totalPlayersInRoom - 1);

        playersInZone.RemoveWhere(id => !NetworkManager.Singleton.ConnectedClients.ContainsKey(id));

        Debug.Log($"[VideoCutscene] Số người đã check-in: {playersInZone.Count} / Cần thiết: {requiredPlayers} (Tổng user: {totalPlayersInRoom})");

        if (playersInZone.Count >= requiredPlayers)
        {
            playersInZone.Clear(); 
            StartCutsceneServer();
        }
    }

    private IEnumerator FadeCanvasGroup(GameObject targetObj, float startAlpha, float endAlpha, float duration, bool disableAfter)
    {
        if (targetObj == null) yield break;

        CanvasGroup canvasGroup = targetObj.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = targetObj.AddComponent<CanvasGroup>();
        }

        if (!targetObj.activeSelf) targetObj.SetActive(true);

        float time = 0;
        while (time < duration)
        {
            time += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, time / duration);
            yield return null;
        }
        
        canvasGroup.alpha = endAlpha;

        if (disableAfter)
        {
            targetObj.SetActive(false);
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        if (isPlaying && AudioManager.Instance != null)
        {
            AudioManager.Instance.SetCutsceneActive(false, 1.0f);
        }
    }
}