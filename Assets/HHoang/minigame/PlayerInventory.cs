using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class PlayerInteraction : NetworkBehaviour
{
    [Header("Cấu hình")]
    public Transform holdPoint;
    public LayerMask interactableLayer;
    
    // ----------------------------------------------------
    // CÁC BIẾN LIÊN KẾT VỚI TRẠM TƯƠNG TÁC (InteractBox / PillarStation)
    // ----------------------------------------------------
    public InteractBox currentInteractBox = null;
    public PillarStation currentPillarStation = null;
    
    // Đồng bộ trạng thái bê đồ qua mạng
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

    // Điều khiển hoạt ảnh giơ tay khi trạng thái bê ngọc thay đổi
    private void OnCarryingCoreChanged(bool oldVal, bool newVal)
    {
        Animator anim = GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.SetBool("isCarryingLog", newVal);
            if (newVal) anim.SetTrigger("isPickingUpLog");
            else anim.SetTrigger("isDroppingLog");
        }
    }

    void Update()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsOwner) return;

        if (Keyboard.current != null)
        {
            // ====== PHÍM F: NHẶT NGỌC HOẶC ĐẶT VÀO TRẠM ======
            if (Keyboard.current.fKey.wasPressedThisFrame)
            {
                if (!isCarryingCore.Value)
                {
                    TryPickupCrystal(); // Nếu tay không -> Nhặt ngọc
                }
                else
                {
                    // Đang bưng ngọc: Kiểm tra xem có đứng gần trạm nào không để đặt vào
                    if (currentInteractBox != null && !currentInteractBox.isCrystalLocked.Value)
                    {
                        currentInteractBox.TrySnapCrystal();
                    }
                    else if (currentPillarStation != null)
                    {
                        currentPillarStation.TryInteract(this, heldCoreNetworkId.Value);
                    }
                }
            }
            // ====== PHÍM G: THẢ NGỌC ======
            else if (Keyboard.current.gKey.wasPressedThisFrame && isCarryingCore.Value)
            {
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

    // ----------------------------------------------------
    // HÀM HỖ TRỢ CHO INTERACT BOX GỌI (Ép người chơi bỏ ngọc khỏi tay)
    // ----------------------------------------------------
    public void ForceDropFromStation()
    {
        if (!IsServer) return;
        heldCoreNetworkId.Value = ulong.MaxValue;
        isCarryingCore.Value = false;
    }
}