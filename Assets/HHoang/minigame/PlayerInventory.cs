using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class PlayerInteraction : NetworkBehaviour
{
    [Header("Cấu hình")]
    public Transform holdPoint;
    public LayerMask interactableLayer;
    public PillarStation currentStation = null;
    public CrystalCore currentHeldCore = null;

    public NetworkVariable<bool> isCarryingCore = new NetworkVariable<bool>(false);

    void Update()
    {
        // 1. Chỉ Client sở hữu mới xử lý Input
        if (!IsOwner) return;

        // 2. Chỉ kiểm tra Input nếu không phải đang chạy Server Headless
        // Hoặc kiểm tra null Keyboard.current trước khi dùng
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (currentHeldCore == null) 
            {
                TryPickupCore();
            }
            else
            {
                if (currentStation != null && currentStation.TryInteract(this)) { }
                else { DropCore(); }
            }
        }
    }

    private void TryPickupCore()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, 2f, interactableLayer);
        foreach (var hit in hitColliders)
        {
            if (hit.TryGetComponent<CrystalCore>(out var core) && !core.isSnapped.Value)
            {
                // GỬI NETWORKOBJECTID LÊN SERVER THAY VÌ GỌI HÀM CỦA CORE
                RequestPickupServerRpc(core.NetworkObject.NetworkObjectId); 
                break;
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPickupServerRpc(ulong networkObjectId, ServerRpcParams rpcParams = default)
    {
        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out var netObj))
        {
            var core = netObj.GetComponent<CrystalCore>();
            if (core != null && !core.isSnapped.Value)
            {
                // Server thực hiện chuyển quyền sở hữu
                core.GetComponent<NetworkObject>().ChangeOwnership(rpcParams.Receive.SenderClientId);
                
                // Cập nhật trạng thái người chơi
                this.currentHeldCore = core; // Lưu ý: cái này chỉ lưu trên Server
                this.isCarryingCore.Value = true;
                
                // Gọi ClientRpc để Client biết nó đang cầm cái gì
                AssignHeldCoreClientRpc(networkObjectId, rpcParams.Receive.SenderClientId);
            }
        }
    }

    // Trong PlayerInteraction.cs
    public void ForceDropFromStation()
    {
        if (!IsServer) return; // Chỉ Server gọi hàm này
        
        if (currentHeldCore != null)
        {
            // 1. Thu hồi quyền sở hữu từ Client về Server
            currentHeldCore.GetComponent<NetworkObject>().RemoveOwnership();
            
            // 2. Gửi lệnh thông báo Client xóa core
            ulong ownerId = NetworkManager.Singleton.ConnectedClients.ContainsKey(OwnerClientId) ? OwnerClientId : 0;
            ClearHeldCoreClientRpc(new ClientRpcParams { 
                Send = new ClientRpcSendParams { TargetClientIds = new[] { ownerId } } 
            });
            
            // 3. Reset cục bộ (Chỉ Server được set biến này)
            currentHeldCore = null;
            isCarryingCore.Value = false;
        }
    }

    public void DropCore() 
    { 
        if (IsOwner) DropCoreServerRpc(); 
    }
    
    private void InternalDrop() 
    {
        currentHeldCore = null;
        // Đã xóa dòng isCarryingCore.Value = false; ở đây để tránh lỗi Netcode
    }

    [ServerRpc(RequireOwnership = false)]
    private void DropCoreServerRpc(ServerRpcParams rpcParams = default)
    {
        if (currentHeldCore != null)
        {
            // Gửi lệnh thả tới ngọc
            currentHeldCore.RequestDrop(rpcParams.Receive.SenderClientId);
            
            // Reset cục bộ trên Server (Server có quyền set biến này)
            currentHeldCore = null;
            isCarryingCore.Value = false;
            
            // Thông báo cho Client xóa UI/Tham chiếu
            ClearHeldCoreClientRpc(new ClientRpcParams { 
                Send = new ClientRpcSendParams { TargetClientIds = new[] { rpcParams.Receive.SenderClientId } } 
            });
        }
    }

    // Sửa lại hàm này
    [ClientRpc]
    private void AssignHeldCoreClientRpc(ulong networkObjectId, ulong targetClientId)
    {
        // Kiểm tra xem máy này có phải là máy của người chơi vừa nhặt không
        if (NetworkManager.Singleton.LocalClientId == targetClientId)
        {
            if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out var netObj))
            {
                currentHeldCore = netObj.GetComponent<CrystalCore>();
            }
        }
    }

    [ClientRpc]
    private void ClearHeldCoreClientRpc(ClientRpcParams rpcParams = default) 
    { 
        currentHeldCore = null; 
        
        // ---- QUAN TRỌNG NHẤT LÀ CHỖ NÀY ----
        // MÌNH ĐÃ XÓA DÒNG isCarryingCore.Value = false; ĐI RỒI!
        // Vì ClientRpc chạy trên máy người chơi, mà người chơi thì không được tự ý sửa biến NetworkVariable.
        // Server đã sửa ở hàm DropCoreServerRpc phía trên rồi, nó sẽ tự đồng bộ về Client.
    }
}