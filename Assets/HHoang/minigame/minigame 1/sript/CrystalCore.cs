using UnityEngine;
using Unity.Netcode;

public class CrystalCore : NetworkBehaviour
{
    // NetworkVariable để đồng bộ ID người cầm lõi giữa các máy
    private NetworkVariable<ulong> currentHolderId = new NetworkVariable<ulong>(0, 
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public bool IsBeingHeld => currentHolderId.Value != 0;

    void Update()
    {
        // Chỉ server mới tính toán vị trí, sau đó NetworkTransform sẽ tự đồng bộ cho Client
        if (IsServer && IsBeingHeld)
        {
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(currentHolderId.Value, out var client))
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

    // Hàm gọi từ PlayerMovement (Chạy trên Client của người nhặt)
    public void RequestPickup(ulong playerId)
    {
        RequestPickupServerRpc(playerId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPickupServerRpc(ulong playerId)
    {
        currentHolderId.Value = playerId;
    }

    // Hàm gọi từ PlayerMovement (Chạy trên Client của người thả)
    public void RequestDrop()
    {
        RequestDropServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestDropServerRpc()
    {
        currentHolderId.Value = 0;
    }
}