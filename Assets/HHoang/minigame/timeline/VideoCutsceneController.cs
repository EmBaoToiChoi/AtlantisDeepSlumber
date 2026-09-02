using UnityEngine;
using UnityEngine.Video;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

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

    [Header("Activate / Show Object After Cutscene")]
    [Tooltip("Kéo thả GameObject muốn hiển thị / kích hoạt (SetActive(true)) sau khi chạy hết video cutscene (nếu có thì bật, không có thì bỏ qua)")]
    public GameObject objectToActivateAfterVideo;
    [Tooltip("Danh sách các GameObject muốn hiển thị / kích hoạt sau khi chạy hết video cutscene (nếu cần bật nhiều object)")]
    public List<GameObject> objectsToActivateAfterVideo = new List<GameObject>();

    [Header("Teleport & Control")]
    public Transform safeZone; 
    public List<Transform> playerSpots = new List<Transform>();
    public List<string> scriptNamesToDisable = new List<string>();
    [Tooltip("Bật tùy chọn này để VideoCutsceneController KHÔNG thực hiện bất kỳ lượt Teleport nào (để script bên ngoài như Minigame 4 tự quản lý teleport)")]
    public bool disableTeleport = false;

    [Header("Final Ending & Credits (UI Toolkit)")]
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

    [Header("Security")]
    public bool playOnlyOnce = true;
    public bool hasPlayed = false;
    public bool HasPlayed => hasPlayed;

    [Header("Status")]
    public bool isPlaying = false;
    
    private bool serverReceivedFinishSignal = false;
    private bool hasRequestedSkip = false;

    // Danh sách lưu trữ ID của các người chơi đang đứng trong vùng Trigger
    private HashSet<ulong> playersInZone = new HashSet<ulong>();

    private void Update()
    {
        if (!IsSpawned) return;

        // Bỏ qua cutscene video thông thường bằng phím ESC
        if (videoUI != null && videoUI.activeSelf && !hasRequestedSkip)
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
        ActivateObjectsAfterVideo();
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
                ActivateObjectsAfterVideo();
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
        yield return new WaitForSeconds(1f);

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

        // NẾU LÀ CUTSCENE CUỐI CÙNG -> CHUYỂN SANG LUỒNG ENDING & CREDITS (UI TOOLKIT)
        if (isFinalEndingCutscene)
        {
            Debug.Log("[VideoCutsceneController] Kích hoạt Chuỗi Kết Thúc Ending & Credits (UI Toolkit) cho toàn bộ phòng!");
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
        ActivateObjectsAfterVideo();

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
        ActivateObjectsAfterVideo();

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
    // LUỒNG ENDING & CREDITS QUA UI TOOLKIT
    // =========================================================================

    [ClientRpc]
    private void StartEndingSequenceClientRpc()
    {
        SaveManager.MarkCutscenePlayed(GetCutsceneId());
        hasPlayed = true;
        DisableTriggerCollider();

        TogglePlayerMovement(false);
        SetLocalPlayerCameraFollow(false);

        if (objectToHide != null) objectToHide.SetActive(false);
        HideObjectsAfterVideo();
        ActivateObjectsAfterVideo();

        EndingCreditsUI endingUI = GetComponent<EndingCreditsUI>();
        if (endingUI == null) endingUI = gameObject.AddComponent<EndingCreditsUI>();

        endingUI.epilogueQuote = epilogueQuote;
        endingUI.epilogueDuration = epilogueDuration;
        endingUI.creditScrollSpeed = creditScrollSpeed;
        endingUI.creditLoopCount = creditLoopCount;
        endingUI.endingMusic = endingMusic;

        endingUI.PlayEndingSequence();
    }

    // =========================================================================
    // PREVIEW TRONG EDITOR & TEST PLAY MODE
    // =========================================================================
    public void PreviewCreditsInEditor()
    {
        EndingCreditsUI endingUI = GetComponent<EndingCreditsUI>();
        if (endingUI == null) endingUI = gameObject.AddComponent<EndingCreditsUI>();
        
        endingUI.epilogueQuote = epilogueQuote;
        endingUI.epilogueDuration = epilogueDuration;
        endingUI.creditScrollSpeed = creditScrollSpeed;
        endingUI.creditLoopCount = creditLoopCount;
        endingUI.endingMusic = endingMusic;

        endingUI.PreviewCreditsInEditor();
    }

    public void PreviewEpilogueInEditor()
    {
        EndingCreditsUI endingUI = GetComponent<EndingCreditsUI>();
        if (endingUI == null) endingUI = gameObject.AddComponent<EndingCreditsUI>();
        
        endingUI.epilogueQuote = epilogueQuote;
        endingUI.epilogueDuration = epilogueDuration;

        endingUI.PreviewEpilogueInEditor();
    }

    public void ClearPreviewInEditor()
    {
        EndingCreditsUI endingUI = GetComponent<EndingCreditsUI>();
        if (endingUI != null) endingUI.ClearPreviewInEditor();
    }

    public void TestEndingInPlayMode()
    {
        if (Application.isPlaying)
        {
            EndingCreditsUI endingUI = GetComponent<EndingCreditsUI>();
            if (endingUI == null) endingUI = gameObject.AddComponent<EndingCreditsUI>();

            endingUI.epilogueQuote = epilogueQuote;
            endingUI.epilogueDuration = epilogueDuration;
            endingUI.creditScrollSpeed = creditScrollSpeed;
            endingUI.creditLoopCount = creditLoopCount;
            endingUI.endingMusic = endingMusic;

            endingUI.PlayEndingSequence();
        }
        else
        {
            Debug.LogWarning("[VideoCutsceneController] Vui lòng nhấn nút Play game ở trên trước khi bấm chạy thử hoạt cảnh!");
        }
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

    private void ActivateObjectsAfterVideo()
    {
        ActivateSingleObject(objectToActivateAfterVideo);

        if (objectsToActivateAfterVideo != null)
        {
            foreach (var obj in objectsToActivateAfterVideo)
            {
                ActivateSingleObject(obj);
            }
        }
    }

    private void ActivateSingleObject(GameObject obj)
    {
        if (obj == null) return;
        obj.SetActive(true);
        Debug.Log($"[VideoCutsceneController] Đã kích hoạt (active) object sau video: {obj.name}");

        // Nếu đối tượng được kích hoạt là Boss, tự động đánh thức AI của Boss ngay lập tức
        var bossAI = obj.GetComponent<BossAI>() ?? obj.GetComponentInChildren<BossAI>();
        if (bossAI != null) bossAI.ActivateBoss();

        var miniBossAI = obj.GetComponent<MiniBossAI>() ?? obj.GetComponentInChildren<MiniBossAI>();
        if (miniBossAI != null) miniBossAI.ActivateBoss();

        var finalBossAI = obj.GetComponent<FinalBossAI>() ?? obj.GetComponentInChildren<FinalBossAI>();
        if (finalBossAI != null) finalBossAI.ActivateBoss();
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