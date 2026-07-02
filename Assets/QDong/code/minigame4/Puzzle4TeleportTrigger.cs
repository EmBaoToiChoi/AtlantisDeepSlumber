using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Playables;

public class Puzzle4TeleportTrigger : NetworkBehaviour
{
    [Header("Dependencies")]
    [Tooltip("Kéo Puzzle4TrapTrigger vào đây để tắt đường sau khi teleport")]
    public Puzzle4TrapTrigger trapTrigger;
    
    [Tooltip("Kéo Object chứa Video hoặc Cutscene vào đây để bật lên")]
    public GameObject videoObject;

    [Tooltip("Thời gian phát video (thời gian player bị khóa)")]
    public float videoDuration = 5f;

    [Tooltip("Kéo PlayableDirector (chứa Timeline video) vào đây (Tùy chọn)")]
    public PlayableDirector timelineDirector;

    [Header("Teleport Settings")]
    [Tooltip("Điểm trung tâm đĩa để dịch chuyển 4 người chơi tới")]
    public Transform teleportTarget;
    
    [Tooltip("Số lượng người chơi cần đi qua box để kích hoạt")]
    public int requiredPlayers = 4;
    
    private HashSet<ulong> playersTouched = new HashSet<ulong>();
    public bool activated = false;

    private int GetRequiredPlayersCount()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            // Nếu số người kết nối ít hơn requiredPlayers thì chỉ cần số người hiện tại
            return Mathf.Min(requiredPlayers, NetworkManager.Singleton.ConnectedClientsIds.Count);
        }
        return 1; // Chế độ chơi đơn
    }

    private void OnTriggerEnter(Collider other)
    {
        if(!IsServer)
            return;

        if (activated)
            return;

        if(other.CompareTag("Player"))
        {
            NetworkObject netObj = other.GetComponentInParent<NetworkObject>();
            if (netObj != null)
            {
                activated = true;
                Debug.Log($"[Puzzle4Teleport] Người chơi {netObj.OwnerClientId} đã chạm. KÍCH HOẠT NGAY LẬP TỨC!");

                TriggerTeleportAndTimelineClientRpc();
                
                // Kích hoạt bẫy/tắt đường đi
                if (trapTrigger != null)
                {
                    trapTrigger.ActivateTrapFromTeleport();
                }

                // Gọi bắt đầu minigame ngay lập tức để bật UI và kích hoạt minigame
                Puzzle4Manager p4Manager = FindAnyObjectByType<Puzzle4Manager>();
                if (p4Manager != null)
                {
                    Debug.Log("[Puzzle4Teleport] Bắt đầu minigame NGAY LẬP TỨC!");
                    p4Manager.StartMinigameFromTeleport();
                }
                else
                {
                    Debug.LogError("[Puzzle4Teleport] LỖI: Không tìm thấy Puzzle4Manager trên Server!");
                }
            }
        }
    }

    private IEnumerator ServerStartMinigameCoroutine()
    {
        float duration = 3f;
        if (timelineDirector != null)
        {
            duration = (float)timelineDirector.duration;
            if (duration > 30f) duration = 30f;
        }
        
        yield return new WaitForSeconds(duration);

        Puzzle4Manager p4Manager = FindAnyObjectByType<Puzzle4Manager>();
        if (p4Manager != null)
        {
            Debug.Log("[Puzzle4Teleport-Server] Đã hết thời gian chờ Timeline, gọi StartMinigameFromTeleport...");
            p4Manager.StartMinigameFromTeleport();
        }
        else
        {
            Debug.LogError("[Puzzle4Teleport-Server] LỖI: Không tìm thấy Puzzle4Manager trên Server!");
        }
    }

    [ClientRpc]
    void TriggerTeleportAndTimelineClientRpc()
    {
        // 1. Teleport local player to the target
        if (teleportTarget != null)
        {
            var allMonos = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var mono in allMonos)
            {
                if (mono is IPlayerHUDTarget player && player.IsOwner)
                {
                    player.transform.position = teleportTarget.position;
                    
                    Rigidbody rb = player.gameObject.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }
                }
            }
            Debug.Log("[Puzzle4Teleport] Đã teleport người chơi tới trung tâm đĩa.");
        }

        // 2. Lock players and Play Timeline
        StartCoroutine(PlayTimelineAndLockCoroutine());
    }

    private IEnumerator PlayTimelineAndLockCoroutine()
    {
        // Khoá di chuyển của local player (bằng cách freeze Rigidbody)
        List<Rigidbody> lockedRbs = new List<Rigidbody>();
        var allMonos = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        
        foreach (var mono in allMonos)
        {
            if (mono is IPlayerHUDTarget player && player.IsOwner)
            {
                Rigidbody rb = player.gameObject.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.constraints = RigidbodyConstraints.FreezeAll;
                    lockedRbs.Add(rb);
                }
            }
        }

        // Tính toán thời gian khóa
        float duration = videoDuration;
        if (timelineDirector != null)
        {
            timelineDirector.Play();
            duration = (float)timelineDirector.duration;
            if (duration > 30f) duration = 30f;
            Debug.Log("[Puzzle4Teleport] Đang chạy Timeline video...");
        }
        else if (videoObject != null)
        {
            videoObject.SetActive(true);
            Debug.Log("[Puzzle4Teleport] Đang bật Video Object...");
        }

        // Chờ thời gian video chạy xong
        yield return new WaitForSeconds(duration);

        // Tắt video object
        if (videoObject != null)
        {
            videoObject.SetActive(false);
            Debug.Log("[Puzzle4Teleport] Đã tắt Video Object...");
        }

        // Mở khoá di chuyển
        foreach (var rb in lockedRbs)
        {
            if (rb != null)
            {
                // Trả về trạng thái chỉ freeze rotation (hoặc tuỳ thuộc vào setup gốc của game)
                rb.constraints = RigidbodyConstraints.FreezeRotation;
            }
        }
        
        Debug.Log("[Puzzle4Teleport] Đã mở khoá di chuyển cho player trên Client.");
    }
}
