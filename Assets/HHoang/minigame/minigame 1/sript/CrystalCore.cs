using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class CrystalCore : NetworkBehaviour
{
    public int crystalID;
    public NetworkList<ulong> holders;
    public NetworkVariable<bool> isSnapped = new NetworkVariable<bool>(false);

    void Awake() => holders = new NetworkList<ulong>();

    // Trả về hệ số tốc độ dựa trên số người khiêng
    public float GetMoveSpeedMultiplier()
    {
        // 2 người khiêng = 100% tốc độ (buff), 1 người khiêng = 60% tốc độ (bị chậm)
        return holders.Count >= 2 ? 1.0f : 0.6f;
    }

    // Trong CrystalCore.cs
    void Update()
    {
        if (isSnapped.Value) return;

        if (IsServer)
        {
            if (holders.Count > 0)
            {
                // Logic cũ: Khiêng vật phẩm
                Vector3 targetPos = Vector3.zero;
                int activeHolders = 0;

                foreach (ulong clientId in holders)
                {
                    if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
                    {
                        var playerMovement = client.PlayerObject.GetComponent<PlayerMovement>();
                        if (playerMovement != null && playerMovement.holdPoint != null)
                        {
                            targetPos += playerMovement.holdPoint.position;
                            activeHolders++;
                        }
                    }
                }

                if (activeHolders > 0)
                {
                    transform.position = targetPos / activeHolders;
                    GetComponent<Rigidbody>().isKinematic = true; // Giữ chặt khi đang khiêng
                }
            }
            else
            {
                // --- THÊM PHẦN NÀY ---
                // Nếu không còn ai khiêng, cho phép vật phẩm rơi xuống đất
                Rigidbody rb = GetComponent<Rigidbody>();
                if (rb != null && rb.isKinematic)
                {
                    rb.isKinematic = false; // Bật vật lý để rơi
                    rb.useGravity = true;
                }
            }
        }
    }

    public void RequestPickup(ulong playerId) => RequestPickupServerRpc(playerId);
    public void RequestDrop(ulong playerId) => RequestDropServerRpc(playerId);

    [ServerRpc(RequireOwnership = false)]
    private void RequestPickupServerRpc(ulong playerId)
    {
        if (!holders.Contains(playerId)) holders.Add(playerId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestDropServerRpc(ulong playerId)
    {
        if (holders.Contains(playerId)) 
        {
            holders.Remove(playerId);
            Debug.Log($"Player {playerId} đã thả Core thành công trên Server");
            
            // Ngay khi thả, cho phép rơi ngay lập tức thay vì đợi Update
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }
        }
    }
}