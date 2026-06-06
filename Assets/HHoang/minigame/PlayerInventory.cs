using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class PlayerInteraction : NetworkBehaviour
{
    [Header("Cấu hình")]
    public Transform holdPoint;
    public LayerMask interactableLayer;
    
    public InteractBox currentInteractBox = null;
    public PillarStation currentPillarStation = null;
    
    // Dùng NetworkVariable để Server luôn biết chính xác ID viên ngọc người chơi đang giữ
    public NetworkVariable<ulong> heldCoreNetworkId = new NetworkVariable<ulong>(ulong.MaxValue);
    public NetworkVariable<bool> isCarryingCore = new NetworkVariable<bool>(false);

    // Biến local chỉ để Client hiển thị
    public CrystalCore currentHeldCore = null;

    void Update()
    {
        if (!IsOwner) return;

        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
        {
            if (!isCarryingCore.Value) 
            {
                TryPickupCore();
            }
            else
            {
                // Ưu tiên 1: Đứng ở trụ -> Nạp ngọc vào trụ
                if (currentPillarStation != null) 
                { 
                    currentPillarStation.TryInteract(this, heldCoreNetworkId.Value); 
                }
                // Ưu tiên 2: Đứng ở hộp InteractBox -> Tương tác với Mini-game 1
                else if (currentInteractBox != null) 
                { 
                    // Gọi hàm tương tác của Mini-game 1 tại đây
                    // Ví dụ: currentInteractBox.ProcessCrystal(this, heldCoreNetworkId.Value);
                    Debug.Log("Đang tương tác với Mini-game 1");
                }
                // Ưu tiên 3: Không đứng ở đâu cả -> Vứt ngọc
                else 
                { 
                    RequestDropServerRpc(); 
                }
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
            core.PerformPickup(rpcParams.Receive.SenderClientId);
            
            heldCoreNetworkId.Value = networkObjectId;
            isCarryingCore.Value = true;
        }
    }

    [ServerRpc]
    private void RequestDropServerRpc(ServerRpcParams rpcParams = default)
    {
        if (heldCoreNetworkId.Value != ulong.MaxValue && 
            NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(heldCoreNetworkId.Value, out var netObj))
        {
            var core = netObj.GetComponent<CrystalCore>();
            core.PerformDrop();
            
            heldCoreNetworkId.Value = ulong.MaxValue;
            isCarryingCore.Value = false;
        }
    }

    public void ForceDropFromStation()
    {
        if (!IsServer) return;
        heldCoreNetworkId.Value = ulong.MaxValue;
        isCarryingCore.Value = false;
    }
}