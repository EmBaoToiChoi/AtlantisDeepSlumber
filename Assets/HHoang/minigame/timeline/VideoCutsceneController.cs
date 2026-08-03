using UnityEngine;
using UnityEngine.Video;
using Unity.Netcode;
using System.Collections.Generic;

public class VideoCutsceneController : NetworkBehaviour
{
    [Header("Video Settings")]
    public VideoPlayer videoPlayer;
    public GameObject videoUI; 
    public GameObject blackScreenUI; 
    public GameObject objectToHide; 

    [Header("Teleport & Control")]
    public Transform safeZone; 
    public List<Transform> playerSpots = new List<Transform>();
    public List<string> scriptNamesToDisable = new List<string>();

    [Header("Security")]
    public bool playOnlyOnce = true;
    private bool hasPlayed = false;

    [Header("Status")]
    public bool isPlaying = false;
    
    private bool serverReceivedFinishSignal = false;
    private bool hasRequestedSkip = false;

    // [ĐÃ THÊM] Danh sách lưu trữ ID của các người chơi đang đứng trong vùng Trigger
    private HashSet<ulong> playersInZone = new HashSet<ulong>();

    private void Update()
    {
        if (!IsSpawned) return;

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

    public void StartCutscene()
    {
        if (playOnlyOnce && hasPlayed) return;
        
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
        if (isPlaying || (playOnlyOnce && hasPlayed)) return;
        isPlaying = true;
        hasPlayed = true; 
        serverReceivedFinishSignal = false; 
        
        PrepareCutsceneClientRpc();
        StartCoroutine(WaitAndTeleportAndFinish());
    }

    private System.Collections.IEnumerator WaitAndTeleportAndFinish()
    {
        yield return new WaitForSeconds(1f); // Đợi 1 giây để màn hình đen từ từ hiện lên hết

        int count = Mathf.Min(NetworkManager.Singleton.ConnectedClientsList.Count, playerSpots.Count);
        ulong[] targetClientIds = new ulong[count];
        for (int i = 0; i < count; i++)
        {
            targetClientIds[i] = NetworkManager.Singleton.ConnectedClientsList[i].ClientId;
        }

        TeleportToSafeZoneClientRpc(targetClientIds);
        PlayCutsceneClientRpc();

        yield return new WaitUntil(() => serverReceivedFinishSignal);

        StopVideoClientRpc();
        TeleportAllPlayersClientRpc(targetClientIds);

        yield return new WaitForSeconds(1f);
        FinishCutsceneClientRpc();
    }

    [ClientRpc]
    private void PrepareCutsceneClientRpc()
    {
        TogglePlayerMovement(false); 
        
        // [ĐÃ SỬA TẠI ĐÂY] Làm mờ dần màn hình thành đen đặc (alpha từ 0 -> 1) trong 1 giây
        if (blackScreenUI != null) 
        {
            StartCoroutine(FadeCanvasGroup(blackScreenUI, 0f, 1f, 1f, false));
        }

        if (objectToHide != null) objectToHide.SetActive(false);
        if (videoPlayer != null) videoPlayer.Prepare(); 
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
        if (objectToHide != null) objectToHide.SetActive(true);
        
        // [ĐÃ SỬA TẠI ĐÂY] Làm sáng dần màn hình (alpha từ 1 -> 0) trong 1 giây, sau đó tắt hẳn object
        if (blackScreenUI != null) 
        {
            StartCoroutine(FadeCanvasGroup(blackScreenUI, 1f, 0f, 1f, true));
        }
        
        TogglePlayerMovement(true);
        isPlaying = false;
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

    private System.Collections.IEnumerator ForceTeleportRoutine(GameObject playerObj, Vector3 targetPos, Quaternion targetRot)
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
        var localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject;
        
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

    // =========================================================================
    // LOGIC TRIGGER NHIỀU NGƯỜI CHƠI (LƯU TRẠNG THÁI)
    // =========================================================================

    private void OnTriggerEnter(Collider other)
    {
        if (!IsSpawned || !IsServer) return; 
        if (isPlaying || (playOnlyOnce && hasPlayed)) return;

        if (other.CompareTag("Player"))
        {
            var netObj = other.GetComponentInParent<NetworkObject>();
            if (netObj != null && netObj.IsPlayerObject)
            {
                // Thêm người chơi vào danh sách. (HashSet sẽ tự động bỏ qua nếu đã có sẵn)
                playersInZone.Add(netObj.OwnerClientId); 
                CheckCutsceneCondition();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // KHÔNG LÀM GÌ CẢ Ở ĐÂY NỮA
        // Việc bỏ trống hàm này giúp hệ thống "nhớ" những ai đã từng chạm vào Trigger
    }

    private void CheckCutsceneCondition()
    {
        int totalPlayersInRoom = NetworkManager.Singleton.ConnectedClientsList.Count;
        int requiredPlayers = Mathf.Max(1, totalPlayersInRoom - 1);

        // Vẫn giữ nguyên logic xóa những người đã Disconnect ra khỏi danh sách
        playersInZone.RemoveWhere(id => !NetworkManager.Singleton.ConnectedClients.ContainsKey(id));

        Debug.Log($"[VideoCutscene] Số người đã check-in: {playersInZone.Count} / Cần thiết: {requiredPlayers} (Tổng user: {totalPlayersInRoom})");

        if (playersInZone.Count >= requiredPlayers)
        {
            playersInZone.Clear(); 
            StartCutsceneServer();
        }
    }

    // =========================================================================
    // [ĐÃ THÊM] HIỆU ỨNG MỜ DẦN (FADE IN / FADE OUT) CHO MÀN HÌNH ĐEN
    // =========================================================================
    private System.Collections.IEnumerator FadeCanvasGroup(GameObject targetObj, float startAlpha, float endAlpha, float duration, bool disableAfter)
    {
        if (targetObj == null) yield break;

        // Tự động tìm hoặc gắn CanvasGroup vào UI để có thể chỉnh độ trong suốt
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
}