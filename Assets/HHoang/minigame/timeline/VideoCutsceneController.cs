using UnityEngine;
using UnityEngine.Video;
using Unity.Netcode;
using System.Collections.Generic;

public class VideoCutsceneController : NetworkBehaviour
{
    [Header("Video Settings")]
    public VideoPlayer videoPlayer;
    public GameObject videoUI; // Kéo cái Canvas chứa Raw Image vào biến này để code bật/tắt nó
    public GameObject blackScreenUI; // [ĐÃ THÊM] Kéo 1 cái Panel đen xì che full màn hình vào đây
    public GameObject objectToHide; // Object tắt khi chạy phim

    [Header("Teleport & Control")]
    public Transform safeZone; // Kéo 1 điểm an toàn (trên trời/dưới đất) vào đây
    public List<Transform> playerSpots = new List<Transform>();
    public List<string> scriptNamesToDisable = new List<string>();

    [Header("Security")]
    public bool playOnlyOnce = true;
    private bool hasPlayed = false;

    [Header("Status")]
    public bool isPlaying = false;
    
    // [ĐÃ THÊM] Cờ để Dedicated Server chờ Client xem xong báo cáo
    private bool serverReceivedFinishSignal = false;

    // Kích hoạt từ Trigger hoặc UI
    public void StartCutscene()
    {
        if (playOnlyOnce && hasPlayed) return;
        
        // Cẩn thận: Nếu là Client gọi, đẩy lên ServerRpc. Nếu là Server, chạy thẳng.
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
        serverReceivedFinishSignal = false; // Reset cờ
        
        // [SỬA]: Gọi RPC chuẩn bị (Bật màn đen, khóa nhân vật) trước
        PrepareCutsceneClientRpc();

        // [SỬA]: Không truyền float duration nữa, Coroutine tự chờ tín hiệu mạng
        StartCoroutine(WaitAndTeleportAndFinish());
    }

    private System.Collections.IEnumerator WaitAndTeleportAndFinish()
    {
        // [SỬA] Chờ 1 giây để màn hình đen phủ kín và video kịp nạp vào RAM
        yield return new WaitForSeconds(1f);

        int count = Mathf.Min(NetworkManager.Singleton.ConnectedClientsList.Count, playerSpots.Count);
        ulong[] targetClientIds = new ulong[count];
        for (int i = 0; i < count; i++)
        {
            targetClientIds[i] = NetworkManager.Singleton.ConnectedClientsList[i].ClientId;
        }

        // [LẦN 1] Đưa tất cả lên Khu Vực An Toàn
        TeleportToSafeZoneClientRpc(targetClientIds);

        // Ra lệnh chiếu phim
        PlayCutsceneClientRpc();

        // [SỬA QUAN TRỌNG] Server dừng lại ở đây, chờ máy Client xem xong bắn tín hiệu lên
        yield return new WaitUntil(() => serverReceivedFinishSignal);

        // [LẦN 2] Phim xong: Dừng video ngay lập tức (vẫn giữ màn hình đen)
        StopVideoClientRpc();
        
        // Đưa tất cả ra vị trí chiến đấu thực sự (playerSpots)
        TeleportAllPlayersClientRpc(targetClientIds);

        // [SỬA] Chờ 1 giây để che giấu cảnh nhân vật rơi rớt/bay lơ lửng do vật lý
        yield return new WaitForSeconds(1f);

        // Phát lệnh kết thúc phim (Rút màn hình đen ra)
        FinishCutsceneClientRpc();
    }

    // --- CÁC HÀM CLIENT RPC MỚI ĐƯỢC CHIA NHỎ ĐỂ ĐỒNG BỘ ---

    [ClientRpc]
    private void PrepareCutsceneClientRpc()
    {
        TogglePlayerMovement(false); // Khóa nhân vật ngay
        if (blackScreenUI != null) blackScreenUI.SetActive(true); // Kéo rèm đen
        if (objectToHide != null) objectToHide.SetActive(false);
        if (videoPlayer != null) videoPlayer.Prepare(); 
    }

    [ClientRpc]
    private void PlayCutsceneClientRpc()
    {
        if (videoUI != null) videoUI.SetActive(true); 

        if (videoPlayer != null)
        {
            videoPlayer.Play();
            // Gắn event: Khi máy này xem xong thì gọi hàm báo cáo
            videoPlayer.loopPointReached += OnClientVideoFinished;
        }
        // [ĐÃ XÓA AudioListener.pause = true; ở đây để video có tiếng]
    }

    // [ĐÃ THÊM] Hàm callback khi Client xem xong video
    private void OnClientVideoFinished(VideoPlayer vp)
    {
        if (videoPlayer != null) videoPlayer.loopPointReached -= OnClientVideoFinished; // Gỡ event an toàn
        ReportFinishToServerRpc();
    }

    // [ĐÃ THÊM] Client bắn tín hiệu lên báo Server biết mình đã xem xong
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
            videoPlayer.loopPointReached -= OnClientVideoFinished; // Gỡ an toàn phòng hờ
        }
        if (videoUI != null) videoUI.SetActive(false);
        // Vẫn giữ BlackScreen bật để che cảnh teleport về
    }

    [ClientRpc]
    private void FinishCutsceneClientRpc()
    {
        if (objectToHide != null) objectToHide.SetActive(true);
        if (blackScreenUI != null) blackScreenUI.SetActive(false); // Xong xuôi thì cất rèm đen đi
        
        TogglePlayerMovement(true);
        isPlaying = false;
    }

    // =========================================================================
    // CÁC HÀM TELEPORT VÀ VẬT LÝ DƯỚI ĐÂY GIỮ NGUYÊN 100% NHƯ CODE GỐC CỦA ÔNG
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
                // Dùng Coroutine để ép dịch chuyển an toàn cho máy yếu
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
                // Dùng Coroutine để ép dịch chuyển an toàn cho máy yếu
                StartCoroutine(ForceTeleportRoutine(localPlayer.gameObject, playerSpots[mySpotIndex].position, playerSpots[mySpotIndex].rotation));
                Debug.Log($"[VideoCutscene] Client {localClientId} tự dịch chuyển ngầm vào Spot {mySpotIndex}");
            }
        }
    }

    // Hàm chuyên dụng để trị máy yếu không chịu dịch chuyển
    private System.Collections.IEnumerator ForceTeleportRoutine(GameObject playerObj, Vector3 targetPos, Quaternion targetRot)
    {
        var charCtrl = playerObj.GetComponent<CharacterController>();
        var navAgent = playerObj.GetComponent<UnityEngine.AI.NavMeshAgent>();

        // 1. Tắt điều khiển
        if (charCtrl != null) charCtrl.enabled = false;
        if (navAgent != null) navAgent.enabled = false;

        // 2. Chờ 1 khung hình để máy yếu kịp nghỉ
        yield return new WaitForEndOfFrame(); 

        // 3. Đặt tọa độ mới
        playerObj.transform.position = targetPos;
        playerObj.transform.rotation = targetRot;
        
        // 4. ÉP BUỘC vật lý Unity cập nhật ngay lập tức (Rất quan trọng)
        Physics.SyncTransforms(); 

        // 5. Chờ thêm 1 khung hình nữa cho chắc cốp
        yield return new WaitForEndOfFrame(); 

        // 6. Bật lại điều khiển
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
        // QUAN TRỌNG NHẤT: Chỉ Server mới được bắt sự kiện va chạm này
        if (!IsSpawned || !IsServer) return; 

        if (other.CompareTag("Player"))
        {
            StartCutsceneServer();
        }
    }
}