using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class CrystalCore : NetworkBehaviour
{
    // Danh sách các Client ID đang cùng khiêng lõi
    public NetworkList<ulong> holders;

    void Awake()
    {
        holders = new NetworkList<ulong>();
    }

    // Trả về hệ số tốc độ dựa trên số người khiêng
    public float GetMoveSpeedMultiplier()
    {
        // 2 người khiêng = 100% tốc độ (buff), 1 người khiêng = 60% tốc độ (bị chậm)
        return holders.Count >= 2 ? 1.0f : 0.6f;
    }

    // Trong CrystalCore.cs
    void Update()
    {
        if (IsServer && holders.Count > 0)
        {
            Vector3 targetPos = Vector3.zero;
            int activeHolders = 0;

            // Tính vị trí trung bình của các điểm giữ (holdPoints)
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
                // Lõi sẽ nằm ở giữa 2 người, hoặc theo người đầu tiên nếu chỉ có 1 người
                transform.position = targetPos / activeHolders;
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
        if (holders.Contains(playerId)) holders.Remove(playerId);
    }
}