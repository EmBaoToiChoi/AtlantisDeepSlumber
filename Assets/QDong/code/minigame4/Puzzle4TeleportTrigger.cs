using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

public class Puzzle4TeleportTrigger : NetworkBehaviour
{
    [Header("Dependencies")]
    [Tooltip("Kéo Puzzle4TrapTrigger vào đây để tắt đường sau khi teleport")]
    public Puzzle4TrapTrigger trapTrigger;

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
        if (!IsServer)
            return;

        if (activated)
            return;

        // Chặn kích hoạt lại nếu minigame đã hoàn thành
        Puzzle4Manager p4Manager = FindAnyObjectByType<Puzzle4Manager>();
        if (p4Manager != null && p4Manager.puzzleCompleted.Value)
        {
            Debug.Log("[Puzzle4Teleport] Minigame đã hoàn thành, không kích hoạt lại.");
            return;
        }

        if (other.CompareTag("Player"))
        {
            NetworkObject netObj = other.GetComponentInParent<NetworkObject>();
            if (netObj != null)
            {
                activated = true;
                Debug.Log($"[Puzzle4Teleport] Người chơi {netObj.OwnerClientId} đã chạm. KÍCH HOẠT NGAY LẬP TỨC!");
                
                // Kích hoạt bẫy/tắt đường đi
                if (trapTrigger != null)
                {
                    trapTrigger.ActivateTrapFromTeleport();
                }

                // Teleport all players robustly
                if (p4Manager != null)
                {
                    int index = 0;
                    foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
                    {
                        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
                        {
                            NetworkObject playerObj = client.PlayerObject;
                            if (playerObj != null)
                            {
                                Vector3 offset = new Vector3(
                                    Mathf.Cos(index * Mathf.PI / 2) * 1.5f, 
                                    0.5f, 
                                    Mathf.Sin(index * Mathf.PI / 2) * 1.5f
                                );
                                Vector3 spawnPos = teleportTarget != null ? teleportTarget.position + offset : playerObj.transform.position;
                                
                                playerObj.transform.position = spawnPos;
                                Rigidbody rb = playerObj.GetComponent<Rigidbody>();
                                if (rb != null)
                                {
                                    rb.linearVelocity = Vector3.zero;
                                    rb.angularVelocity = Vector3.zero;
                                }
                                
                                p4Manager.TeleportPlayerToCenterClientRpc(playerObj.NetworkObjectId, spawnPos);
                                index++;
                            }
                        }
                    }

                    // Bắt đầu minigame ngay lập tức
                    Debug.Log("[Puzzle4Teleport] Bắt đầu minigame ngay lập tức!");
                    p4Manager.StartMinigameFromTeleport();
                }
                else
                {
                    Debug.LogError("[Puzzle4Teleport] LỖI: Không tìm thấy Puzzle4Manager trên Server!");
                }
            }
        }
    }

    /// <summary>
    /// Gọi từ Server sau khi minigame hoàn thành để hiện lại box nhưng KHÔNG cho kích hoạt.
    /// Chỉ bật Renderer (nhìn thấy được), Collider vẫn tắt.
    /// </summary>
    [ClientRpc]
    public void ReappearClientRpc()
    {
        gameObject.SetActive(true);

        // Chỉ bật Renderer để thấy được, KHÔNG bật Collider
        // ⇒ Người chơi không thể kích hoạt lại minigame sau khi hoàn thành
        Renderer rend = GetComponent<Renderer>();
        if (rend != null) rend.enabled = true;

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        Debug.Log("[Puzzle4Teleport] ReappearClientRpc: Box đã hiện lại (chỉ Renderer, Collider vẫn tắt)!");
    }
}
