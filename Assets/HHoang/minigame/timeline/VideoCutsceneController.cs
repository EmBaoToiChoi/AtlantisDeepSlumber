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
    [Tooltip("Nếu tích vào đây, phim sẽ chỉ chạy đúng 1 lần duy nhất")]
    public bool playOnlyOnce = true;
    private bool hasPlayed = false;

    [Header("Status")]
    public bool isPlaying = false;

    // Gọi hàm này từ trigger hoặc sự kiện khác
    public void StartCutscene()
    {
        if (playOnlyOnce && hasPlayed) return;
        if (IsServer) StartCutsceneServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void StartCutsceneServerRpc()
    {
        if (isPlaying || (playOnlyOnce && hasPlayed)) return;
        isPlaying = true;
        hasPlayed = true; 
        
        // 1. Teleport player (xếp hàng từng người)
        var players = PlayerHUDManager.ActivePlayers;
        int i = 0;
        foreach (var p in players)
        {
            if (p != null && i < playerSpots.Count)
            {
                MonoBehaviour pMono = p as MonoBehaviour;
                if (pMono != null)
                {
                    // Tắt CharacterController nếu có để dịch chuyển không bị kẹt
                    var charCtrl = pMono.GetComponent<CharacterController>();
                    if (charCtrl != null) charCtrl.enabled = false;
                    
                    pMono.transform.position = playerSpots[i].position;
                    pMono.transform.rotation = playerSpots[i].rotation;
                    
                    if (charCtrl != null) charCtrl.enabled = true;
                    i++;
                }
            }
        }

        PlayCutsceneClientRpc();
        StartCoroutine(WaitAndFinish((float)videoPlayer.length));
    }

    [ClientRpc]
    private void PlayCutsceneClientRpc()
    {
        if (objectToHide != null) objectToHide.SetActive(false);
        
        if (videoPlayer != null)
        {
            // ÉP VIDEO VÀO CAMERA CHÍNH CỦA NGƯỜI CHƠI
            if (Camera.main != null)
            {
                videoPlayer.renderMode = VideoRenderMode.CameraNearPlane;
                videoPlayer.targetCamera = Camera.main;
            }
            videoPlayer.Play();
        }

        TogglePlayerMovement(false);
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
        
        TogglePlayerMovement(true);
        isPlaying = false;
    }

    private void TogglePlayerMovement(bool enable)
    {
        // Tìm player bằng Tag "Player"
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        
        if (playerObj != null)
        {
            MonoBehaviour[] allScripts = playerObj.GetComponents<MonoBehaviour>();
            foreach (var script in allScripts)
            {
                if (script != null && scriptNamesToDisable.Contains(script.GetType().Name))
                {
                    script.enabled = enable;
                    Debug.Log($"[VideoCutscene] Đã {(enable ? "BẬT" : "TẮT")} script: {script.GetType().Name}");
                }
            }
        }
        else
        {
            Debug.LogError("[VideoCutscene] Không tìm thấy Player có Tag 'Player'!");
        }
    }
}