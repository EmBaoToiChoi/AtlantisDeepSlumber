using UnityEngine;
using Unity.Netcode;

public class PillarStation : NetworkBehaviour
{
    public int stationIndex;
    public AscensionManager manager; // Kéo AscensionController vào đây
    public Transform snapPosition;
    // Thay dòng cũ bằng dòng này
    public NetworkVariable<bool> isOccupied = new NetworkVariable<bool>(false);

    // ĐÃ BỎ: isPlayerInZone, OnTriggerEnter, OnTriggerExit
    // Vì giờ đây PlayerMovement tự biết nó đang đứng ở trạm nào thông qua InteractBox

    // Gọi hàm này từ InteractBox/PlayerMovement khi nhấn E
    public bool TryInteract(PlayerMovement player)
    {
        // Đọc giá trị .Value của NetworkVariable
        if (isOccupied.Value) return false; 

        if (player.currentHeldCore != null)
        {
            // 1. Gửi lệnh lên Server để "hút" tinh thể (vẫn giữ nguyên)
            RequestSnapServerRpc(player.currentHeldCore.NetworkObject.NetworkObjectId, stationIndex);
            
            // 2. Tắt liên kết với player
            player.currentHeldCore.RequestDrop(player.OwnerClientId);
            player.currentHeldCore = null;
            player.SetCarryingCoreServerRpc(false);
            
            // KHÔNG GÁN isOccupied = true TẠI ĐÂY NỮA
            // Vì hành động này phải do Server thực hiện để đồng bộ cho tất cả mọi người
            
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
                
                // CẬP NHẬT GIÁ TRỊ QUA .Value
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