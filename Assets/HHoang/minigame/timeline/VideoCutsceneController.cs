using UnityEngine;
using UnityEngine.Video;
using Unity.Netcode;
using System.Collections.Generic;

public class VideoCutsceneController : NetworkBehaviour
{
    [Header("Video Settings")]
    public VideoPlayer videoPlayer;
    public GameObject objectToHide; // Object tắt khi chạy phim

    [Header("Teleport & Control")]
    public List<Transform> playerSpots = new List<Transform>();
    public List<string> scriptNamesToDisable = new List<string>();

    [Header("Security")]
    public bool playOnlyOnce = true;
    private bool hasPlayed = false;

    [Header("Status")]
    public bool isPlaying = false;

    // Hàm kích hoạt sự kiện
    public void StartCutscene()
    {
        if (playOnlyOnce && hasPlayed) return;
        
        // ĐÃ SỬA: Xóa if(IsServer) đi.
        // Cứ có người gọi là báo thẳng lên Server, Server sẽ tự xử lý cho cả 4 máy!
        StartCutsceneServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void StartCutsceneServerRpc()
    {
        if (isPlaying || (playOnlyOnce && hasPlayed)) return;
        isPlaying = true;
        hasPlayed = true; 
        
        // 1. Teleport tất cả player đang online
        var players = PlayerHUDManager.ActivePlayers;
        int i = 0;
        foreach (var p in players)
        {
            if (p != null && i < playerSpots.Count)
            {
                MonoBehaviour pMono = p as MonoBehaviour;
                if (pMono != null)
                {
                    var charCtrl = pMono.GetComponent<CharacterController>();
                    if (charCtrl != null) charCtrl.enabled = false;
                    
                    pMono.transform.position = playerSpots[i].position;
                    pMono.transform.rotation = playerSpots[i].rotation;
                    
                    if (charCtrl != null) charCtrl.enabled = true;
                    i++;
                }
            }
        }

        // 2. Gửi lệnh bật phim cho tất cả Client
        PlayCutsceneClientRpc();
        StartCoroutine(WaitAndFinish((float)videoPlayer.length));
    }

    [ClientRpc]
    private void PlayCutsceneClientRpc()
    {
        if (objectToHide != null) objectToHide.SetActive(false);
        
        if (videoPlayer != null)
        {
            videoPlayer.renderMode = VideoRenderMode.CameraNearPlane;
            videoPlayer.targetCamera = Camera.main;
            videoPlayer.Play();
        }

        // Tự khóa script di chuyển của chính Client này
        TogglePlayerMovementClientRpc(false);
    }

    private System.Collections.IEnumerator WaitAndFinish(float duration)
    {
        yield return new WaitForSeconds(duration);
        FinishCutsceneClientRpc();
    }

    [ClientRpc]
    private void FinishCutsceneClientRpc()
    {
        if (objectToHide != null) objectToHide.SetActive(true);
        if (videoPlayer != null)
        {
            videoPlayer.Stop();
            videoPlayer.targetCamera = null;
        }
        
        // Tự mở khóa script di chuyển của chính Client này
        TogglePlayerMovementClientRpc(true);
        isPlaying = false;
    }

    [ClientRpc]
    private void TogglePlayerMovementClientRpc(bool enable)
    {
        // Tìm nhân vật của chính người chơi đang ngồi ở máy này
        var localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject;
        
        if (localPlayer != null)
        {
            MonoBehaviour[] allScripts = localPlayer.GetComponents<MonoBehaviour>();
            foreach (var script in allScripts)
            {
                if (script != null && scriptNamesToDisable.Contains(script.GetType().Name))
                {
                    script.enabled = enable;
                    Debug.Log($"[VideoCutscene] Client {NetworkManager.Singleton.LocalClientId} đã {(enable ? "BẬT" : "TẮT")} script: {script.GetType().Name}");
                }
            }
        }
        else
        {
            Debug.LogError("[VideoCutscene] Không tìm thấy Local PlayerObject để khóa di chuyển!");
        }
    }

    // --- ĐÃ THÊM: HÀM NÀY GIÚP OBJECT TỰ TRỞ THÀNH BẪY ĐỘC LẬP ---
    private void OnTriggerEnter(Collider other)
    {
        // Bỏ qua nếu object chưa được load trên mạng
        if (!IsSpawned) return; 

        // Kiểm tra xem đối tượng dẫm vào có mang Tag "Player" không
        if (other.CompareTag("Player"))
        {
            Debug.Log($"[VideoCutscene] Phát hiện Player {other.name} dẫm bẫy! Kích hoạt video...");
            StartCutscene();
        }
    }
}