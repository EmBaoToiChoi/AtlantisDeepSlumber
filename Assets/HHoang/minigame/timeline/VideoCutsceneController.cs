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
        yield return new WaitForSeconds(1f);

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
        if (blackScreenUI != null) blackScreenUI.SetActive(true); 
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
        if (blackScreenUI != null) blackScreenUI.SetActive(false); 
        
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
    // [ĐÃ THAY ĐỔI] LOGIC TRIGGER NHIỀU NGƯỜI CHƠI
    // =========================================================================

    private void OnTriggerEnter(Collider other)
    {
        if (!IsSpawned || !IsServer) return; 
        if (isPlaying || (playOnlyOnce && hasPlayed)) return; // Nếu đang phát hoặc đã phát rồi thì bỏ qua

        if (other.CompareTag("Player"))
        {
            // Lấy NetworkObject của người chơi để lưu ClientId
            var netObj = other.GetComponentInParent<NetworkObject>();
            if (netObj != null && netObj.IsPlayerObject)
            {
                playersInZone.Add(netObj.OwnerClientId); // Thêm vào danh sách
                CheckCutsceneCondition();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsSpawned || !IsServer) return;

        if (other.CompareTag("Player"))
        {
            var netObj = other.GetComponentInParent<NetworkObject>();
            if (netObj != null && netObj.IsPlayerObject)
            {
                playersInZone.Remove(netObj.OwnerClientId); // Xóa khỏi danh sách khi đi ra ngoài
            }
        }
    }

    private void CheckCutsceneCondition()
    {
        // Lấy tổng số người chơi đang kết nối trong phòng
        int totalPlayersInRoom = NetworkManager.Singleton.ConnectedClientsList.Count;
        
        // Tính toán số người cần thiết (Tổng - 1, nhưng ít nhất phải là 1)
        int requiredPlayers = Mathf.Max(1, totalPlayersInRoom - 1);

        // Đề phòng trường hợp có người ngắt kết nối (Disconnect) khi đang đứng trong zone
        // Xóa những ClientId không còn tồn tại trong phòng ra khỏi danh sách playersInZone
        playersInZone.RemoveWhere(id => !NetworkManager.Singleton.ConnectedClients.ContainsKey(id));

        Debug.Log($"[VideoCutscene] Số người trong vùng: {playersInZone.Count} / Cần thiết: {requiredPlayers} (Tổng user: {totalPlayersInRoom})");

        // Nếu số người đứng trong zone đủ yêu cầu => Phát Cutscene!
        if (playersInZone.Count >= requiredPlayers)
        {
            playersInZone.Clear(); // Dọn dẹp danh sách
            StartCutsceneServer();
        }
    }
}