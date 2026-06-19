using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class PlayerInteraction : NetworkBehaviour
{
    [Header("Cấu hình")]
    public Transform holdPoint;
    public LayerMask interactableLayer;
    
    // Các biến liên kết Trạm
    public InteractBox currentInteractBox = null;
    public PillarStation currentPillarStation = null;
    
    public NetworkVariable<ulong> heldCoreNetworkId = new NetworkVariable<ulong>(ulong.MaxValue);
    public NetworkVariable<bool> isCarryingCore = new NetworkVariable<bool>(false);

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        isCarryingCore.OnValueChanged += OnCarryingCoreChanged;
        if (isCarryingCore.Value) OnCarryingCoreChanged(false, true);
    }

    public override void OnNetworkDespawn()
    {
        isCarryingCore.OnValueChanged -= OnCarryingCoreChanged;
        base.OnNetworkDespawn();
    }

    // --- HÀM TÌM ANIMATOR CHUẨN XÁC NHẤT ---
    private Animator GetPlayerAnimator()
    {
        Animator anim = GetComponent<Animator>();
        if (anim == null) anim = GetComponentInChildren<Animator>();
        return anim;
    }

    // Đồng bộ lại hoạt ảnh khi mạng cập nhật (Tránh bị kẹt)
    private void OnCarryingCoreChanged(bool oldVal, bool newVal)
    {
        Animator anim = GetPlayerAnimator();
        if (anim != null)
        {
            // Reset trigger cũ để chống kẹt hoạt ảnh
            anim.ResetTrigger("isPickingUpLog");
            anim.ResetTrigger("isDroppingLog");

            anim.SetBool("isCarryingLog", newVal);
            if (newVal) anim.SetTrigger("isPickingUpLog");
            else anim.SetTrigger("isDroppingLog");
        }

        // Tạm khóa script bê gỗ để nó không đánh nhau với viên ngọc
        var logCarrier = GetComponent("PlayerLogCarrier") as MonoBehaviour;
        if (logCarrier != null) logCarrier.enabled = !newVal; 
    }

    void Update()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsOwner) return;

        if (Keyboard.current != null)
        {
            // ====== PHÍM F: NHẶT / ĐẶT NGỌC ======
            if (Keyboard.current.fKey.wasPressedThisFrame)
            {
                if (!isCarryingCore.Value)
                {
                    TryPickupCrystal(); // Tay không -> Đi tìm nhặt
                }
                else
                {
                    if (currentInteractBox != null && !currentInteractBox.isCrystalLocked.Value)
                        currentInteractBox.TrySnapCrystal();
                    else if (currentPillarStation != null)
                        currentPillarStation.TryInteract(this, heldCoreNetworkId.Value);
                }
            }
            // ====== PHÍM G: THẢ NGỌC ======
            else if (Keyboard.current.gKey.wasPressedThisFrame && isCarryingCore.Value)
            {
                // 1. Ép chạy hoạt ảnh THẢ đồ ngay lập tức để siêu mượt
                Animator anim = GetPlayerAnimator();
                if (anim != null)
                {
                    anim.SetBool("isCarryingLog", false);
                    anim.SetTrigger("isDroppingLog");
                }

                // 2. Gửi lệnh lên mạng thả vật lý
                RequestDropServerRpc();
            }
        }
    }

    private void TryPickupCrystal()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, 2f, interactableLayer);
        foreach (var hit in hitColliders)
        {
            if (hit.TryGetComponent<CrystalCore>(out var core))
            {
                Animator anim = GetPlayerAnimator();
                if (anim != null)
                {
                    // FIX: Xóa sạch trigger cũ để tránh kẹt lệnh
                    anim.ResetTrigger("isPickingUpLog");
                    anim.ResetTrigger("isDroppingLog");
                    
                    // Bật dáng bê đồ
                    anim.SetBool("isCarryingLog", true);
                    
                    // KÍCH HOẠT TRIGGER CÚI NHẶT
                    anim.SetTrigger("isPickingUpLog");
                    
                    // THÊM DÒNG NÀY: Ép animator chuyển sang trạng thái nhặt ngay lập tức
                    // "PickUp" phải là tên chính xác của State trong Animator của bạn
                    // Nếu bạn không biết tên state, hãy thử bỏ dòng này hoặc kiểm tra tên trong Unity
                    // anim.Play("PickUp", 0, 0f); 
                }

                RequestPickupServerRpc(core.NetworkObject.NetworkObjectId);
                break;
            }
        }
    }

    [ServerRpc]
    private void RequestPickupServerRpc(ulong networkObjectId)
    {
        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out var netObj))
        {
            var core = netObj.GetComponent<CrystalCore>();
            if (core != null && !isCarryingCore.Value)
            {
                core.PerformPickup(OwnerClientId);
                heldCoreNetworkId.Value = networkObjectId;
                isCarryingCore.Value = true;
            }
        }
    }

    [ServerRpc]
    private void RequestDropServerRpc()
    {
        if (heldCoreNetworkId.Value != ulong.MaxValue && 
            NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(heldCoreNetworkId.Value, out var netObj))
        {
            var core = netObj.GetComponent<CrystalCore>();
            if (core != null)
            {
                core.PerformDrop();
            }
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