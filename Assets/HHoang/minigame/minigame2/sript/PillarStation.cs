using UnityEngine;
using Unity.Netcode;

public class PillarStation : NetworkBehaviour
{
    public int stationIndex;
    public AscensionManager manager;
    public Transform snapPosition;
    public GameObject pillarEffect;
    public NetworkVariable<bool> isOccupied = new NetworkVariable<bool>(false);

    void Start() 
    {
        if (pillarEffect != null) pillarEffect.SetActive(false); // Mặc định tắt
    }
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
            // TỐI ƯU 1: Tránh lỗi NullReferenceException nếu player đột ngột disconnect
            if (client.PlayerObject == null) return; 

            var pInt = client.PlayerObject.GetComponent<PlayerInteraction>();
            
            // Nên check thêm pInt != null cho chắc cú
            if (pInt != null && pInt.heldCoreNetworkId.Value == crystalNetId)
            {
                crystal.StartSnappingToStation(snapPosition);
                pInt.ForceDropFromStation();
                
                if (manager != null)
                {
                    manager.SnapCrystalToPillar(crystal, index);
                    isOccupied.Value = true;
                    SetEffectStateClientRpc(true);
                }
            }
        }
    }

    [ClientRpc]
    public void SetEffectStateClientRpc(bool state)
    {
        if (pillarEffect != null) 
        {
            pillarEffect.SetActive(state);
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