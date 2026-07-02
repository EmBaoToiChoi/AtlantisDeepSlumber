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

    // Kích hoạt từ Trigger
    public void StartCutscene()
    {
        if (playOnlyOnce && hasPlayed) return;
        StartCutsceneServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void StartCutsceneServerRpc()
    {
        if (isPlaying || (playOnlyOnce && hasPlayed)) return;
        isPlaying = true;
        hasPlayed = true; 
        
        // --- LOGIC TELEPORT MỚI CHUẨN NETCODE ---
        int spotIndex = 0;
        // Quét danh sách 4 máy Client đang kết nối
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject != null && spotIndex < playerSpots.Count)
            {
                // Tạo thông báo GỬI RIÊNG cho từng máy Client
                ClientRpcParams clientRpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { client.ClientId }
                    }
                };

                // Ép máy Client đó tự dịch chuyển con nhân vật của chính nó vào đúng Spot
                TeleportLocalPlayerClientRpc(spotIndex, clientRpcParams);
                spotIndex++;
            }
        }

        PlayCutsceneClientRpc();
        StartCoroutine(WaitAndFinish((float)videoPlayer.length));
    }

    // Hàm này CHỈ chạy trên đúng cái máy Client được chỉ định
    [ClientRpc]
    private void TeleportLocalPlayerClientRpc(int spotIndex, ClientRpcParams clientRpcParams = default)
    {
        var localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject;
        if (localPlayer != null && spotIndex < playerSpots.Count)
        {
            // TẮT TẠM THỜI MỌI THỨ CẢN TRỞ DỊCH CHUYỂN
            var charCtrl = localPlayer.GetComponent<CharacterController>();
            if (charCtrl != null) charCtrl.enabled = false;

            var navAgent = localPlayer.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (navAgent != null) navAgent.enabled = false;

            // Dịch chuyển đến cái bục (Spot) tương ứng
            localPlayer.transform.position = playerSpots[spotIndex].position;
            localPlayer.transform.rotation = playerSpots[spotIndex].rotation;

            // BẬT LẠI VẬT LÝ
            if (charCtrl != null) charCtrl.enabled = true;
            if (navAgent != null) navAgent.enabled = true;
            
            Debug.Log($"[VideoCutscene] Client {NetworkManager.Singleton.LocalClientId} tự dịch chuyển thành công vào vị trí Spot {spotIndex}");
        }
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
        
        TogglePlayerMovementClientRpc(true);
        isPlaying = false;
    }

    [ClientRpc]
    private void TogglePlayerMovementClientRpc(bool enable)
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
        if (!IsSpawned) return; 

        if (other.CompareTag("Player"))
        {
            StartCutscene();
        }
    }
}