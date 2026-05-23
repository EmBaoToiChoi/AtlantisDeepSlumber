using UnityEngine;
using Unity.Netcode;

public class CrystalCore : NetworkBehaviour
{
    private NetworkVariable<ulong> currentHolderId = new NetworkVariable<ulong>(0, 
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public bool IsBeingHeld => currentHolderId.Value != 0;

    // Hàm gọi khi nhấn E để nhặt
    public void RequestPickup(ulong playerId)
    {
        RequestPickupServerRpc(playerId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPickupServerRpc(ulong playerId)
    {
        currentHolderId.Value = playerId;
    }

    // Hàm gọi khi thả lõi
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