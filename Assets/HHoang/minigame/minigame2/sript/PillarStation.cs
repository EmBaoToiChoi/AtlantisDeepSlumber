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
        // Bỏ qua điều kiện isOccupied.Value khi kiểm tra để đảm bảo Server quyết định
        if (player == null || player.currentHeldCore == null) return;
        
        // Nếu trụ đã occupied, hãy kiểm tra lại trên Server một lần nữa trước khi từ chối
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