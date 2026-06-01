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

    // Trong file PlayerInteraction.cs (vốn tên là PlayerInventory.cs trong upload của bạn)
    void Update()
    {
        if (!IsOwner) return;

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (currentHeldCore == null) TryPickupCore();
            else
            {
                // SỬA: Truyền 'this' thay vì GetComponent<PlayerMovement>()
                if (currentStation != null && currentStation.TryInteract(this)) { /* Tương tác thành công */ }
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
                RequestPickupServerRpc(core.NetworkObject.NetworkObjectId);
                break;
            }
        }
    }

    [ServerRpc]
    private void RequestPickupServerRpc(ulong networkObjectId, ServerRpcParams rpcParams = default)
    {
        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out var netObj))
        {
            var core = netObj.GetComponent<CrystalCore>();
            currentHeldCore = core;
            isCarryingCore.Value = true;
            core.RequestPickup(OwnerClientId);
            
            // Đồng bộ cho riêng Client sở hữu
            AssignHeldCoreClientRpc(networkObjectId, new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { rpcParams.Receive.SenderClientId } } });
        }
    }

    [ClientRpc]
    private void AssignHeldCoreClientRpc(ulong networkObjectId, ClientRpcParams rpcParams = default)
    {
        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out var netObj))
            currentHeldCore = netObj.GetComponent<CrystalCore>();
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
    private void ClearHeldCoreClientRpc() => currentHeldCore = null;

    [ServerRpc(RequireOwnership = false)]
    public void SetCarryingCoreServerRpc(bool state) => isCarryingCore.Value = state;
}