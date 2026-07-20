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
                    int playerIndex = 0;
                    foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
                    {
                        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
                        {
                            NetworkObject playerObj = client.PlayerObject;
                            if (playerObj != null)
                            {
                                Vector3 spawnPos = teleportTarget != null ? teleportTarget.position : playerObj.transform.position;

                                // Thêm offset nhỏ để 4 người chơi không bị teleport dính chặt vào cùng 1 điểm gây lỗi vật lý văng ra ngoài
                                Vector3 offset = Vector3.zero;
                                float dist = 0.25f; // Khoảng cách nhỏ gọn lại (0.75m từ tâm)
                                if (playerIndex == 0) offset = new Vector3(dist, 0, dist);
                                else if (playerIndex == 1) offset = new Vector3(-dist, 0, dist);
                                else if (playerIndex == 2) offset = new Vector3(dist, 0, -dist);
                                else if (playerIndex == 3) offset = new Vector3(-dist, 0, -dist);
                                spawnPos += offset;
                                playerIndex++;

                                // Tắt CharacterController trên server trước khi dịch chuyển để đồng bộ chuẩn xác
                                CharacterController cc = playerObj.GetComponent<CharacterController>();
                                if (cc != null) cc.enabled = false;

                                // Gửi ClientRpc TRƯỚC để client chuẩn bị nhận vị trí mới
                                p4Manager.TeleportPlayerToCenterClientRpc(playerObj.NetworkObjectId, spawnPos);

                                // Dùng NetworkTransform.Teleport() nếu có để tránh bị override
                                Unity.Netcode.Components.NetworkTransform netTransform = playerObj.GetComponent<Unity.Netcode.Components.NetworkTransform>();
                                if (netTransform != null)
                                {
                                    try 
                                    {
                                        // Nếu dùng ClientNetworkTransform, Server gọi Teleport lên client sẽ ném Exception làm crash vòng lặp. Cần try-catch.
                                        netTransform.Teleport(spawnPos, playerObj.transform.rotation, playerObj.transform.localScale);
                                    }
                                    catch (System.Exception e)
                                    {
                                        Debug.LogWarning($"[Puzzle4Teleport] Không thể gọi Teleport trực tiếp cho Client {clientId} (client tự quản lý vị trí qua ClientRpc): {e.Message}");
                                    }
                                }
                                else
                                {
                                    playerObj.transform.position = spawnPos;
                                }

                                Rigidbody rb = playerObj.GetComponent<Rigidbody>();
                                if (rb != null)
                                {
                                    rb.linearVelocity = Vector3.zero;
                                    rb.angularVelocity = Vector3.zero;
                                    rb.Sleep();
                                }

                                // Bật lại CharacterController
                                if (cc != null) cc.enabled = true;
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
