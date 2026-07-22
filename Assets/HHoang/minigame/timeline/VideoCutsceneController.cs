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
    
    // [ĐÃ THÊM] Cờ để chống spam nút ESC gửi nhiều tín hiệu làm lag mạng
    private bool hasRequestedSkip = false;

    private void Update()
    {
        if (!IsSpawned) return;

        // [ĐÃ THÊM] Nếu màn hình video đang hiển thị và chưa ai bấm skip
        if (videoUI != null && videoUI.activeSelf && !hasRequestedSkip)
        {
            // Bất kỳ ai nhấn ESC (hoặc ông có thể đổi sang phím Space/Enter tùy ý)
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                hasRequestedSkip = true; // Khóa lại, không cho bấm 2 lần
                Debug.Log("[VideoCutscene] Phát hiện phím ESC! Đang yêu cầu Skip cutscene cho cả phòng...");
                
                // Gỡ event chạy hết video để không báo cáo đúp
                if (videoPlayer != null)
                {
                    videoPlayer.loopPointReached -= OnClientVideoFinished;
                }

                // Gọi Server báo là xem xong rồi để Server ngắt phim của mọi người!
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

        // Server đứng hình ở đây chờ xem phim xong HOẶC có người bấm ESC
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
        
        hasRequestedSkip = false; // [ĐÃ THÊM] Reset lại cờ skip mỗi lần bắt đầu xem phim

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

    private void OnTriggerEnter(Collider other)
    {
        if (!IsSpawned || !IsServer) return; 

        if (other.CompareTag("Player"))
        {
            StartCutsceneServer();
        }
    }
}