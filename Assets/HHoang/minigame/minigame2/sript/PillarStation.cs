using UnityEngine;
using Unity.Netcode;

public class PillarStation : NetworkBehaviour
{
    public int stationIndex;
    public AscensionManager manager;
    public Transform snapPosition;
    public NetworkVariable<bool> isOccupied = new NetworkVariable<bool>(false);

    public void TryInteract(PlayerInteraction player, ulong heldCoreId)
    {
        if (player == null || heldCoreId == ulong.MaxValue) return;
        RequestSnapServerRpc(heldCoreId, stationIndex);
    }
    
    [ServerRpc(RequireOwnership = false)]
    void RequestSnapServerRpc(ulong crystalNetId, int index, ServerRpcParams rpcParams = default)
    {
        if (isOccupied.Value) return;

        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(crystalNetId, out var netObj)) return;

        var crystal = netObj.GetComponent<CrystalCore>();
        
        // Xác thực: Chỉ người đang sở hữu ngọc mới được đặt
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(rpcParams.Receive.SenderClientId, out var client))
        {
            var pInt = client.PlayerObject.GetComponent<PlayerInteraction>();
            
            if (pInt.heldCoreNetworkId.Value == crystalNetId)
            {
                // THAY VÌ TELEPORT, ta gọi hàm báo viên ngọc bắt đầu bay từ từ vào trụ
                crystal.StartSnappingToStation(snapPosition);
                
                // Giải phóng ngọc khỏi tay người chơi
                pInt.ForceDropFromStation();
                
                if (manager != null)
                {
                    manager.SnapCrystalToPillar(crystal, index);
                    isOccupied.Value = true;
                }
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