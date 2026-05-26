using UnityEngine;
using Unity.Netcode;

public class PillarStation : NetworkBehaviour
{
    public int stationIndex;
    public AscensionManager manager; // Kéo AscensionController vào đây
    public Transform snapPosition;

    // ĐÃ BỎ: isPlayerInZone, OnTriggerEnter, OnTriggerExit
    // Vì giờ đây PlayerMovement tự biết nó đang đứng ở trạm nào thông qua InteractBox

    // Gọi hàm này từ InteractBox/PlayerMovement khi nhấn E
    public bool TryInteract(PlayerMovement player)
    {
        // Chỉ cần player cầm bóng là thực hiện cắm trụ luôn (InteractBox đã lọc điều kiện đứng trong vùng rồi)
        if (player.currentHeldCore != null)
        {
            // 1. Gửi lệnh lên Server để "hút" tinh thể
            RequestSnapServerRpc(player.currentHeldCore.NetworkObject.NetworkObjectId, stationIndex);
            
            // 2. Tắt liên kết với player
            player.currentHeldCore.RequestDrop(player.OwnerClientId);
            player.currentHeldCore = null;
            player.SetCarryingCoreServerRpc(false);
            
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
            }
        }
    }
}