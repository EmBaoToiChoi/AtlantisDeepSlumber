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
                // SỬA Ở ĐÂY: Truyền OwnerClientId của người chơi thay vì NetworkObjectId của ngọc
                core.RequestPickup(NetworkManager.LocalClientId); 
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

    public void DropCore() { if (IsOwner) DropCoreServerRpc(); }

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