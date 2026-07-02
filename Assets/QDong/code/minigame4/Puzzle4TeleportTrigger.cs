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
    
    [Tooltip("Kéo PlayableDirector (chứa Timeline video) vào đây")]
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
                ulong clientId = netObj.OwnerClientId;
                if (!playersTouched.Contains(clientId))
                {
                    playersTouched.Add(clientId);
                    int currentRequired = GetRequiredPlayersCount();
                    Debug.Log($"[Puzzle4Teleport] Người chơi {clientId} đã đi qua. ({playersTouched.Count}/{currentRequired})");

                    if (playersTouched.Count >= currentRequired)
                    {
                        activated = true;
                        TriggerTeleportAndTimelineClientRpc();
                        
                        // Kích hoạt bẫy/tắt đường đi
                        if (trapTrigger != null)
                        {
                            trapTrigger.ActivateTrapFromTeleport();
                        }
                    }
                }
            }
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

        // Bật timeline
        if (timelineDirector != null)
        {
            timelineDirector.Play();
            Debug.Log("[Puzzle4Teleport] Đang chạy Timeline video...");
            
            float duration = (float)timelineDirector.duration;
            if (duration > 30f) duration = 30f;
            
            // Chờ cho timeline chạy xong (tối đa 30s)
            yield return new WaitForSeconds(duration);
        }
        else
        {
            // Fallback nếu không có timeline
            yield return new WaitForSeconds(3f); 
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
        
        Debug.Log("[Puzzle4Teleport] Đã mở khoá di chuyển cho player.");

        if (IsServer)
        {
            Puzzle4Manager p4Manager = FindAnyObjectByType<Puzzle4Manager>();
            if (p4Manager != null)
            {
                p4Manager.StartMinigameFromTeleport();
            }
        }
    }
}
