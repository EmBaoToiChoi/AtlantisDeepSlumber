using UnityEngine;
using Unity.Netcode;

public class PillarStation : NetworkBehaviour
{
    public int stationIndex;
    public AscensionManager manager; // Kéo AscensionController vào đây
    public Transform snapPosition;
    public bool isOccupied = false;

    // ĐÃ BỎ: isPlayerInZone, OnTriggerEnter, OnTriggerExit
    // Vì giờ đây PlayerMovement tự biết nó đang đứng ở trạm nào thông qua InteractBox

    // Gọi hàm này từ InteractBox/PlayerMovement khi nhấn E
    public bool TryInteract(PlayerMovement player)
    {
        // Kiểm tra nếu trụ đã có đồ thì từ chối hành động
        if (isOccupied) return false;
        // Chỉ cần player cầm bóng là thực hiện cắm trụ luôn (InteractBox đã lọc điều kiện đứng trong vùng rồi)
        if (player.currentHeldCore != null)
        {
            // 1. Gửi lệnh lên Server để "hút" tinh thể
            RequestSnapServerRpc(player.currentHeldCore.NetworkObject.NetworkObjectId, stationIndex);
            
            // 2. Tắt liên kết với player
            player.currentHeldCore.RequestDrop(player.OwnerClientId);
            player.currentHeldCore = null;
            player.SetCarryingCoreServerRpc(false);

            isOccupied = true; // Đánh dấu là đã có đồ
            
            return true; // Báo cho PlayerMovement là đã cắm thành công
        }
        return false;
    }

    [ServerRpc(RequireOwnership = false)]
    void RequestSnapServerRpc(ulong crystalNetId, int index)
    {
        Debug.Log("Server đã nhận lệnh cắm trụ!"); // THÊM DÒNG NÀY ĐỂ CHECK
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(crystalNetId, out var networkObject))
        {
            CrystalCore crystal = networkObject.GetComponent<CrystalCore>();
            if (manager != null && crystal != null)
            {
                manager.SnapCrystalToPillar(crystal, index);
                isOccupied = true; // Đồng bộ trạng thái trên server
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