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
                    Debug.Log("Đang tương tác với Mini-game 1");
                }
                // Ưu tiên 3: Không đứng ở đâu cả -> NÉM/THẢ NGỌC RA NGOÀI
                else 
                { 
                    // [VỊ TRÍ SỬA 1]: Đổi từ RequestDropServerRpc() sang gọi lệnh Ném
                    RequestThrowServerRpc(transform.forward); 
                }
            }
        }
    }

    private void TryPickupCore()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, 2f, interactableLayer);
        foreach (var hit in hitColliders)
        {
            // [VỊ TRÍ SỬA 2]: Thêm lệnh check ngọc Puzzle của Mini-game 3 trước
            if (hit.TryGetComponent<PuzzleCrystalCore>(out var puzzleCore) && !puzzleCore.isSnapped.Value)
            {
                RequestPickupPuzzleServerRpc(puzzleCore.NetworkObject.NetworkObjectId);
                break;
            }
            // Logic check ngọc thường cũ giữ nguyên
            else if (hit.TryGetComponent<CrystalCore>(out var core) && !core.isSnapped.Value)
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
            
            // ---> THÊM DÒNG NÀY VÀO: Chặn không cho nhặt nếu ngọc đã có người cầm
            if (core.holderId.Value != ulong.MaxValue) return;
            // <---

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

    // =========================================================
    // [VỊ TRÍ SỬA 3]: THÊM 2 HÀM SERVER RPC MỚI VÀO CUỐI FILE
    // =========================================================

    [ServerRpc]
    private void RequestPickupPuzzleServerRpc(ulong networkObjectId, ServerRpcParams rpcParams = default)
    {
        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out var netObj))
        {
            var core = netObj.GetComponent<PuzzleCrystalCore>();
            ulong senderId = rpcParams.Receive.SenderClientId;

            // ---> THÊM DÒNG NÀY VÀO: Chống lỗi bấm đúp phím F làm văng oan uổng
            if (core.holderId.Value != ulong.MaxValue) return; 
            // <---

            // Check luật 1 lần chạm
            if (core.CanPickup(senderId))
            {
                core.PerformPickup(senderId);
                heldCoreNetworkId.Value = networkObjectId;
                isCarryingCore.Value = true;
            }
            else
            {
                core.RepelPlayer(senderId); // Bị từ chối -> Đẩy lùi
            }
        }
    }

    [ServerRpc]
    private void RequestThrowServerRpc(Vector3 throwDirection, ServerRpcParams rpcParams = default)
    {
        if (heldCoreNetworkId.Value != ulong.MaxValue && 
            NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(heldCoreNetworkId.Value, out var netObj))
        {
            // Tự động phân loại: Nếu là ngọc Puzzle thì ném văng ra, nếu ngọc thường thì rớt xuống đất
            if (netObj.TryGetComponent<PuzzleCrystalCore>(out var puzzleCore))
            {
                puzzleCore.PerformThrow(throwDirection);
            }
            else if (netObj.TryGetComponent<CrystalCore>(out var core))
            {
                core.PerformDrop();
            }
            
            heldCoreNetworkId.Value = ulong.MaxValue;
            isCarryingCore.Value = false;
        }
    }
}