using UnityEngine;
using Unity.Netcode;

public class PillarStation : NetworkBehaviour
{
    public int stationIndex;
    public AscensionManager manager;
    public Transform snapPosition;
    public NetworkVariable<bool> isOccupied = new NetworkVariable<bool>(false);

    public bool TryInteract(PlayerInteraction player)
    {
        // Đảm bảo không bị null và trạm trống
        if (player == null || isOccupied.Value || player.currentHeldCore == null) return false;

        RequestSnapServerRpc(player.currentHeldCore.NetworkObject.NetworkObjectId, stationIndex);
        player.DropCore(); 
        return true;
    }

    [ServerRpc(RequireOwnership = false)]
    void RequestSnapServerRpc(ulong crystalNetId, int index)
    {
        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(crystalNetId, out var netObj))
        {
            var crystal = netObj.GetComponent<CrystalCore>();
            if (manager != null)
            {
                manager.SnapCrystalToPillar(crystal, index);
                isOccupied.Value = true;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<PlayerInteraction>(out var player)) player.currentStation = this;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<PlayerInteraction>(out var player) && player.currentStation == this)
            player.currentStation = null;
    }
}