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

    void Update()
    {
        // Server tính toán vị trí của lõi theo người chơi đầu tiên trong danh sách
        if (IsServer && holders.Count > 0)
        {
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(holders[0], out var client))
            {
                var playerMovement = client.PlayerObject.GetComponent<PlayerMovement>();
                if (playerMovement != null && playerMovement.holdPoint != null)
                {
                    transform.position = playerMovement.holdPoint.position;
                    transform.rotation = playerMovement.holdPoint.rotation;
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
        if (holders.Contains(playerId)) holders.Remove(playerId);
    }
}