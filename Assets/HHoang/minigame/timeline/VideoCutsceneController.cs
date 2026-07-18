using UnityEngine;
using UnityEngine.Video;
using Unity.Netcode;
using System.Collections.Generic;

public class VideoCutsceneController : NetworkBehaviour
{
    [Header("Video Settings")]
    public VideoPlayer videoPlayer;
    public GameObject videoUI; // Kéo cái Canvas chứa Raw Image vào biến này để code bật/tắt nó
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
        
        // 1. Ra lệnh cho mọi Client bật Video lên xem trước
        PlayCutsceneClientRpc();

        // 2. Chạy Coroutine: Đợi video che màn hình rồi mới Teleport
        StartCoroutine(WaitAndTeleportAndFinish((float)videoPlayer.length));
    }

    private System.Collections.IEnumerator WaitAndTeleportAndFinish(float duration)
    {
        // CHỜ 0.5 GIÂY: Lúc này video đã hiện lên che khuất nhân vật
        yield return new WaitForSeconds(0.5f);

        int count = Mathf.Min(NetworkManager.Singleton.ConnectedClientsList.Count, playerSpots.Count);
        ulong[] targetClientIds = new ulong[count];
        for (int i = 0; i < count; i++)
        {
            targetClientIds[i] = NetworkManager.Singleton.ConnectedClientsList[i].ClientId;
        }

        // [LẦN 1] Đưa tất cả lên Khu Vực An Toàn (để xem phim mà không bị quái đánh)
        TeleportToSafeZoneClientRpc(targetClientIds);

        // Chờ nốt thời gian còn lại của Video
        float remainingTime = Mathf.Max(0f, duration - 0.5f);
        yield return new WaitForSeconds(remainingTime);

        // [LẦN 2] Phim xong, đưa tất cả ra vị trí chiến đấu thực sự (playerSpots)
        TeleportAllPlayersClientRpc(targetClientIds);

        // Phát lệnh kết thúc phim
        FinishCutsceneClientRpc();
    }

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

    // [THÊM HÀM NÀY VÀO]: Hàm chuyên dụng để trị máy yếu không chịu dịch chuyển
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

    [ClientRpc]
    private void PlayCutsceneClientRpc()
    {
        if (objectToHide != null) objectToHide.SetActive(false);
        
        if (videoUI != null) videoUI.SetActive(true); 

        if (videoPlayer != null)
        {
            videoPlayer.Play();
        }

        // Tạm dừng toàn bộ âm thanh môi trường/gameplay
        AudioListener.pause = true;

        TogglePlayerMovement(false);
    }

    [ClientRpc]
    private void FinishCutsceneClientRpc()
    {
        if (objectToHide != null) objectToHide.SetActive(true);
        
        if (videoUI != null) videoUI.SetActive(false); 

        if (videoPlayer != null)
        {
            videoPlayer.Stop();
        }
        
        // Bật lại âm thanh bình thường cho game
        AudioListener.pause = false;
        
        TogglePlayerMovement(true);
        isPlaying = false;
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