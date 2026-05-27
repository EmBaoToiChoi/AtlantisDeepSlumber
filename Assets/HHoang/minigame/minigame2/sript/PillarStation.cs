using UnityEngine;
using Unity.Netcode;

public class PillarStation : NetworkBehaviour
{
    public int stationIndex;
    public AscensionManager manager;
    public Transform snapPosition;

    // SỬA: Phải dùng NetworkVariable để đồng bộ mạng
    public NetworkVariable<bool> isOccupied = new NetworkVariable<bool>(false);

    // Gọi hàm này từ InteractBox/PlayerMovement khi nhấn E
    public bool TryInteract(PlayerMovement player)
    {
        Debug.Log($"Trụ {stationIndex} báo Occupied: {isOccupied.Value}");
        Debug.Log($"Player đang cầm: {player.currentHeldCore}"); // XEM NÓ CÓ BỊ NULL KHÔNG
        if (isOccupied.Value) return false; 

        if (player.currentHeldCore != null)
        {
            RequestSnapServerRpc(player.currentHeldCore.NetworkObject.NetworkObjectId, stationIndex);
            
            player.currentHeldCore.RequestDrop(player.OwnerClientId);
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
                // SỬA: Cập nhật .Value trên Server, nó sẽ tự gửi xuống Client
                isOccupied.Value = true; 
            }
        }
    }

    // Thêm vào PillarStation.cs
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) // Đảm bảo Player có tag là "Player"
        {
            var player = other.GetComponent<PlayerMovement>();
            if (player != null)
            {
                player.currentStation = this; // Gán trạm hiện tại cho người chơi
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            var player = other.GetComponent<PlayerMovement>();
            if (player != null && player.currentStation == this)
            {
                player.currentStation = null; // Rời khỏi vùng thì set về null
            }
        }
    }
}