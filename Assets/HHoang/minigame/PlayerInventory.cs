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
        // 1. Chỉ Client sở hữu mới có quyền gửi lệnh tương tác
        if (!IsOwner) return;

        // 2. Kiểm tra input an toàn
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (currentHeldCore == null) 
            {
                TryPickupCore();
            }
            else
            {
                // Gọi tới trạm hoặc drop
                if (currentStation != null && currentStation.TryInteract(this)) 
                { 
                    // Tương tác thành công xử lý bên trong trạm
                }
                else 
                { 
                    DropCore(); 
                }
            }
        }
    }

    private void TryPickupCore()
    {
        // Thực hiện Physics trên phía Client để dự đoán (Prediction)
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, 2f, interactableLayer);
        foreach (var hit in hitColliders)
        {
            if (hit.TryGetComponent<CrystalCore>(out var core) && !core.isSnapped.Value)
            {
                // Gửi ID lên server để Server xác nhận
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
                currentHeldCore = core;
                isCarryingCore.Value = true;
                core.RequestPickup(rpcParams.Receive.SenderClientId);
                
                // Gửi ClientRpc tới đúng người chơi đã request
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

    [ServerRpc(RequireOwnership = false)]
    private void DropCoreServerRpc()
    {
        if (currentHeldCore != null)
        {
            currentHeldCore.RequestDrop(OwnerClientId);
            currentHeldCore = null;
            isCarryingCore.Value = false;
            ClearHeldCoreClientRpc();
        }
    }

    [ClientRpc]
    private void AssignHeldCoreClientRpc(ulong networkObjectId, ClientRpcParams rpcParams = default)
    {
        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out var netObj))
            currentHeldCore = netObj.GetComponent<CrystalCore>();
    }

    [ClientRpc]
    private void ClearHeldCoreClientRpc() => currentHeldCore = null;
}