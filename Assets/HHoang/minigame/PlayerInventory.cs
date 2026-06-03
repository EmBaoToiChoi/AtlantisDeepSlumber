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
                // Gửi ID của chính người chơi lên để Server biết ai đang nhặt
                core.RequestPickup(NetworkManager.Singleton.LocalClientId); 
                break;
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPickupServerRpc(ulong networkObjectId, ServerRpcParams rpcParams = default)
    {
        // Server tự kiểm tra logic mà không cần đụng tới Keyboard
        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out var netObj))
        {
            var core = netObj.GetComponent<CrystalCore>();
            if (core != null && !core.isSnapped.Value)
            {
                currentHeldCore = core;
                isCarryingCore.Value = true;
                core.RequestPickup(rpcParams.Receive.SenderClientId);
                
                AssignHeldCoreClientRpc(networkObjectId, new ClientRpcParams { 
                    Send = new ClientRpcSendParams { TargetClientIds = new[] { rpcParams.Receive.SenderClientId } } 
                });
            }
        }
    }

    public void DropCore() 
    { 
        if (IsOwner) DropCoreServerRpc(); 
    }
    private void InternalDrop() 
    {
        currentHeldCore = null;
        isCarryingCore.Value = false;
    }

    [ServerRpc(RequireOwnership = false)]
    private void DropCoreServerRpc(ServerRpcParams rpcParams = default)
    {
        if (currentHeldCore != null)
        {
            // Gửi lệnh thả tới ngọc
            currentHeldCore.RequestDrop(rpcParams.Receive.SenderClientId);
            
            // Reset cục bộ trên Server
            currentHeldCore = null;
            isCarryingCore.Value = false;
            
            // Thông báo cho Client xóa UI/Tham chiếu
            ClearHeldCoreClientRpc(new ClientRpcParams { 
                Send = new ClientRpcSendParams { TargetClientIds = new[] { rpcParams.Receive.SenderClientId } } 
            });
        }
    }

    [ClientRpc]
    private void AssignHeldCoreClientRpc(ulong networkObjectId, ClientRpcParams rpcParams = default)
    {
        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out var netObj))
            currentHeldCore = netObj.GetComponent<CrystalCore>();
    }

    [ClientRpc]
    private void ClearHeldCoreClientRpc(ClientRpcParams rpcParams = default) 
    { 
        currentHeldCore = null; 
        isCarryingCore.Value = false;
    }
}