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
        Debug.Log($"[DEBUG] Đang gọi TryInteract tại trạm: {stationIndex}. Đang cầm ngọc: {(player.currentHeldCore != null ? "Có" : "Không")}");
        
        if (player == null || player.currentHeldCore == null) 
        {
            Debug.LogWarning("[DEBUG] TryInteract bị chặn: Thiếu player hoặc không cầm ngọc!");
            return;
        }
        
        RequestSnapServerRpc(player.currentHeldCore.NetworkObject.NetworkObjectId, stationIndex);
    }
    
    [ServerRpc(RequireOwnership = false)]
    void RequestSnapServerRpc(ulong crystalNetId, int index, ServerRpcParams rpcParams = default)
    {
        Debug.Log("[DEBUG] [PillarStation] Server đã nhận được yêu cầu đặt ngọc!");

        // 1. Kiểm tra trạng thái trụ
        if (isOccupied.Value) 
        { 
            Debug.LogWarning($"[DEBUG] [PillarStation] Server từ chối vì trụ {index} đang bị chiếm (isOccupied = true)!"); 
            return; 
        }

        // 2. Kiểm tra viên ngọc có tồn tại không
        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(crystalNetId, out var netObj))
        {
            Debug.LogError($"[DEBUG] [PillarStation] Server không tìm thấy NetworkObject với ID: {crystalNetId}");
            return;
        }

        var crystal = netObj.GetComponent<CrystalCore>();
        if (crystal == null)
        {
            Debug.LogError($"[DEBUG] [PillarStation] Đối tượng với ID {crystalNetId} không có script CrystalCore!");
            return;
        }

        // 3. Xóa ngọc khỏi tay nhân vật
        bool foundPlayer = false;
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(rpcParams.Receive.SenderClientId, out var client))
        {
            if (client.PlayerObject != null && client.PlayerObject.TryGetComponent<PlayerInteraction>(out var pInt))
            {
                Debug.Log("[DEBUG] [PillarStation] Đang gọi ForceDropFromStation cho Player...");
                pInt.ForceDropFromStation();
                foundPlayer = true;
            }
        }
        
        if (!foundPlayer)
        {
            Debug.LogWarning("[DEBUG] [PillarStation] Không tìm thấy PlayerInteraction để tước ngọc!");
        }

        // 4. Xử lý logic tại Manager
        if (manager != null)
        {
            Debug.Log($"[DEBUG] [PillarStation] Gọi manager.SnapCrystalToPillar cho trụ {index}");
            manager.SnapCrystalToPillar(crystal, index);
            isOccupied.Value = true;
            Debug.Log($"[DEBUG] [PillarStation] Đặt ngọc vào trụ {index} thành công!");
        }
        else
        {
            Debug.LogError("[DEBUG] [PillarStation] Biến 'manager' đang bị null! Chưa gán vào Inspector?");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[DEBUG] Đã chạm vào trụ: {gameObject.name}");
        if (other.TryGetComponent<PlayerInteraction>(out var player)) player.currentPillarStation = this;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<PlayerInteraction>(out var player) && player.currentPillarStation == this)
            player.currentPillarStation = null;
    }
}