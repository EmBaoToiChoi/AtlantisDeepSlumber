using UnityEngine;
using Unity.Netcode;

public class PillarStation : NetworkBehaviour
{
    public int stationIndex;
    public AscensionManager manager;
    public Transform snapPosition;
    public NetworkVariable<bool> isOccupied = new NetworkVariable<bool>(false);

    public void TryInteract(PlayerInteraction player)
    {
        if (player == null || isOccupied.Value || player.currentHeldCore == null) return;
        
        // Gửi lệnh lên Server khóa ngọc, KHÔNG GỌI player.DropCore() ở đây nữa
        RequestSnapServerRpc(player.currentHeldCore.NetworkObject.NetworkObjectId, stationIndex);
    }

    [ServerRpc(RequireOwnership = false)]
    void RequestSnapServerRpc(ulong crystalNetId, int index, ServerRpcParams rpcParams = default)
    {
        if (isOccupied.Value) return;

        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(crystalNetId, out var netObj))
        {
            var crystal = netObj.GetComponent<CrystalCore>();

            // Xóa ngọc khỏi tay nhân vật một cách an toàn trên Server
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(rpcParams.Receive.SenderClientId, out var client))
            {
                if (client.PlayerObject != null && client.PlayerObject.TryGetComponent<PlayerInteraction>(out var pInt))
                {
                    pInt.ForceDropFromStation();
                }
            }

            if (manager != null)
            {
                manager.SnapCrystalToPillar(crystal, index);
                isOccupied.Value = true;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<PlayerInteraction>(out var player)) player.currentPillarStation = this;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<PlayerInteraction>(out var player) && player.currentPillarStation == this)
            player.currentPillarStation = null;
    }
}