using UnityEngine;
using Unity.Netcode;

public class PillarStation : NetworkBehaviour
{
    public int stationIndex;
    public AscensionManager manager; // Kéo AscensionController vào đây
    public Transform snapPosition;
    private bool isPlayerInZone = false;

    private void OnTriggerEnter(Collider other) { if (other.CompareTag("Player")) isPlayerInZone = true; }
    private void OnTriggerExit(Collider other) { if (other.CompareTag("Player")) isPlayerInZone = false; }

    // Gọi hàm này từ PlayerMovement khi nhấn E
    public bool TryInteract(PlayerMovement player)
    {
        if (isPlayerInZone && player.currentHeldCore != null)
        {
            // QUAN TRỌNG: Tắt sự liên kết của tinh thể với người chơi trước khi hút
            player.currentHeldCore.RequestDrop(player.OwnerClientId); 
            
            RequestSnapServerRpc(player.currentHeldCore.NetworkObject.NetworkObjectId, stationIndex);
            
            // Xóa tham chiếu trên người chơi
            player.currentHeldCore = null;
            player.SetCarryingCoreServerRpc(false);
            
            return true; 
        }
        return false;
    }

    [ServerRpc(RequireOwnership = false)]
    void RequestSnapServerRpc(ulong crystalNetId, int index)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(crystalNetId, out var networkObject))
        {
            CrystalCore crystal = networkObject.GetComponent<CrystalCore>();
            if (manager != null && crystal != null)
            {
                manager.SnapCrystalToPillar(crystal, index);
            }
        }
    }
}