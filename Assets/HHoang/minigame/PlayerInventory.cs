using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class PlayerInteraction : NetworkBehaviour
{
    [Header("Cấu hình")]
    public Transform holdPoint;
    public LayerMask interactableLayer;
    
    // TÁCH LÀM 2 BIẾN RIÊNG BIỆT CHO 2 MINI-GAME
    public InteractBox currentInteractBox = null;
    public PillarStation currentPillarStation = null;
    public CrystalCore currentHeldCore = null;

    public NetworkVariable<bool> isCarryingCore = new NetworkVariable<bool>(false);

    void Update()
    {
        if (!IsOwner) return;

        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
        {
            if (currentHeldCore == null) 
            {
                TryPickupCore();
            }
            else
            {
                // Ưu tiên 1: Đứng ở Pillar -> Nạp ngọc
                if (currentPillarStation != null) 
                { 
                    currentPillarStation.TryInteract(this); 
                }
                // Ưu tiên 2: Đứng ở hộp InteractBox -> Để yên cho trạm tự xử lý
                else if (currentInteractBox != null) 
                { 
                    // Do nothing
                }
                // Đứng ngoài đường -> Vứt ngọc
                else 
                { 
                    DropCore(); 
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

    public void DropCore()
    {
        if (currentHeldCore != null)
        {
            RequestDropServerRpc();
        }
    }

    // Hàm này cho phép Server tự tước quyền cầm ngọc của Player khi khóa ngọc vào bệ
    public void ForceDropFromStation()
    {
        currentHeldCore = null;
        isCarryingCore.Value = false;
        ClearHeldCoreClientRpc();
    }

    [ServerRpc]
    private void RequestPickupServerRpc(ulong networkObjectId, ServerRpcParams rpcParams = default)
    {
        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out var netObj))
        {
            var core = netObj.GetComponent<CrystalCore>();
            core.PerformPickup(rpcParams.Receive.SenderClientId);
            
            isCarryingCore.Value = true;
            
            // --- DÒNG SỬA LỖI Ở ĐÂY NÈ ---
            // Phải bắt Server (VPS) tự ghi nhớ cục ngọc, nếu không lúc Drop nó đéo biết vứt cái gì!
            currentHeldCore = core; 
            // ------------------------------

            AssignHeldCoreClientRpc(networkObjectId, rpcParams.Receive.SenderClientId);
        }
    }

    [ServerRpc]
    private void RequestDropServerRpc(ServerRpcParams rpcParams = default)
    {
        if (currentHeldCore != null)
        {
            currentHeldCore.PerformDrop();
            
            currentHeldCore = null;
            isCarryingCore.Value = false;
            
            ClearHeldCoreClientRpc(new ClientRpcParams { 
                Send = new ClientRpcSendParams { TargetClientIds = new[] { rpcParams.Receive.SenderClientId } } 
            });
        }
    }

    [ClientRpc]
    private void AssignHeldCoreClientRpc(ulong networkObjectId, ulong targetClientId)
    {
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
    }
}