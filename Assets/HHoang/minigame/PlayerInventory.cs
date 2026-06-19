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

    // --- BIẾN BỔ TRỢ ĐỂ TEST OFFLINE TRONG SCENE KHÔNG BỊ LỖI RPC ---
    private bool isCarryingCoreOffline = false;
    private CrystalCore localHeldCrystalOffline = null;

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

    // Kiểm tra xem nhân vật có đang ôm Ngọc hay không (Cả Online lẫn Offline)
    private bool IsCarryingCoreLocal()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            return isCarryingCore.Value;
        }
        return isCarryingCoreOffline;
    }

    private Animator GetPlayerAnimator()
    {
        Animator anim = GetComponent<Animator>();
        if (anim == null) anim = GetComponentInChildren<Animator>();
        return anim;
    }

    // Đồng bộ mạng thay đổi trạng thái
    private void OnCarryingCoreChanged(bool oldVal, bool newVal)
    {
        HandleCarryingCoreVisuals(newVal);
    }

    // --- HÀM XỬ LÝ ĐỒNG BỘ HOẠT ẢNH VÀ THÀNH PHẦN ---
    private void HandleCarryingCoreVisuals(bool newVal)
    {
        Animator anim = GetPlayerAnimator();
        if (anim != null)
        {
            SafeSetBool(anim, "Bung", newVal);
            SafeSetBool(anim, "IsCarrying", newVal);

            if (newVal)
            {
                SafeSetTrigger(anim, "BungTrigger");
                SafeSetTrigger(anim, "Bưng");
            }

            if (anim.layerCount > 1)
            {
                anim.SetLayerWeight(1, newVal ? 1f : 0f);
            }
        }

        // Tạm khóa script bê gỗ để nó không đánh nhau với viên ngọc
        var logCarrier = GetComponent("PlayerLogCarrier") as MonoBehaviour;
        if (logCarrier != null) logCarrier.enabled = !newVal; 
    }

    private void SafeSetBool(Animator anim, string paramName, bool value)
    {
        if (anim == null) return;
        foreach (var param in anim.parameters)
        {
            if (param.name == paramName) { anim.SetBool(paramName, value); return; }
        }
    }

    private void SafeSetTrigger(Animator anim, string paramName)
    {
        if (anim == null) return;
        foreach (var param in anim.parameters)
        {
            if (param.name == paramName) { anim.SetTrigger(paramName); return; }
        }
    }

    void Update()
    {
        // Nếu đang bật mạng mạng và mình không phải chủ sở hữu nhân vật thì không xử lý nút bấm
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsOwner) return;

        if (Keyboard.current != null)
        {
            bool isCarrying = IsCarryingCoreLocal();

            // ====== PHÍM F: NHẶT / ĐẶT NGỌC VÀO TRỤ ======
            if (Keyboard.current.fKey.wasPressedThisFrame)
            {
                Debug.Log($"[CrystalDebug] F key pressed. isCarrying (local) = {isCarrying}");
                if (!isCarrying)
                {
                    TryPickupCrystal(); // Tay không -> Đi tìm nhặt ngọc dưới đất
                }
                else
                {
                    // Đang ôm ngọc trên tay -> Thực hiện đặt vào Trạm Box hoặc Trụ cột
                    if (currentInteractBox != null && !currentInteractBox.isCrystalLocked.Value)
                    {
                        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                        {
                            // XỬ LÝ ĐẶT NGỌC OFFLINE KHI TEST SCENE
                            HandleOfflineSnapToBox();
                        }
                        else
                        {
                            currentInteractBox.TrySnapCrystal();
                        }
                    }
                    else if (currentPillarStation != null)
                    {
                        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                        {
                            // XỬ LÝ ĐẶT NGỌC VÀO PILLAR OFFLINE KHI TEST SCENE
                            HandleOfflineSnapToPillar();
                        }
                        else
                        {
                            currentPillarStation.TryInteract(this, heldCoreNetworkId.Value);
                        }
                    }
                }
            }
            // ====== PHÍM G: THẢ NGỌC XUỐNG ĐẤT ======
            else if (Keyboard.current.gKey.wasPressedThisFrame && isCarrying)
            {
                HandleDropAction();
            }
        }
    }

    private void TryPickupCrystal()
    {
        Debug.Log($"[CrystalDebug] TryPickupCrystal called. Player Pos: {transform.position}");

        var target = GetComponent<IPlayerHUDTarget>();
        if (target != null)
        {
            int weaponIdx = target.GetActiveWeaponIndex();
            Debug.Log($"[CrystalDebug] Player weapon index: {weaponIdx}");
            if (weaponIdx == 2)
            {
                Debug.LogWarning("[CrystalDebug] Blocked: Weapon index is 2 (Weapon in hand).");
                var hud = FindAnyObjectByType<PlayerHUDController>();
                if (hud != null) hud.ShowMissionAlert("Bạn phải cất vũ khí mới nhặt được ngọc!", 3.0f);
                return;
            }
        }

        var carrier = GetComponent<PlayerLogCarrier>();
        if (carrier != null && carrier.isCarrying)
        {
            Debug.LogWarning("[CrystalDebug] Blocked: Player is already carrying something.");
            var hud = FindAnyObjectByType<PlayerHUDController>();
            if (hud != null) hud.ShowMissionAlert("Bạn đang bưng một thanh gỗ rồi!", 3.0f);
            return;
        }

        // 1. Try to check using the interactableLayer mask
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, 3f, interactableLayer);
        CrystalCore core = null;
        foreach (var hit in hitColliders)
        {
            core = hit.GetComponent<CrystalCore>();
            if (core == null) core = hit.GetComponentInParent<CrystalCore>();
            if (core == null) core = hit.GetComponentInChildren<CrystalCore>();
            if (core != null)
            {
                Debug.Log($"[CrystalDebug] Found CrystalCore using mask: {core.name}");
                break;
            }
        }

        // 2. Fallback: Check all colliders within 3f (in case LayerMask is incorrectly set in inspector)
        if (core == null)
        {
            Collider[] fallbackColliders = Physics.OverlapSphere(transform.position, 3f);
            foreach (var hit in fallbackColliders)
            {
                core = hit.GetComponent<CrystalCore>();
                if (core == null) core = hit.GetComponentInParent<CrystalCore>();
                if (core == null) core = hit.GetComponentInChildren<CrystalCore>();
                if (core != null)
                {
                    Debug.Log($"[CrystalDebug] Found CrystalCore using fallback: {core.name}");
                    break;
                }
            }
        }

        if (core != null)
        {
            // 1. Chạy hoạt ảnh bưng bê ngay lập tức cho mượt mà
            HandleCarryingCoreVisuals(true);

            // 2. Phân tách xử lý Mạng và Offline để tránh crash báo lỗi RPC
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                Debug.LogWarning("[Scene Test] Đang nhặt ngọc Offline.");
                localHeldCrystalOffline = core;
                isCarryingCoreOffline = true;
                
                core.holderId.Value = 0; // Offline placeholder
                core.PerformPickup(0); // Gọi trực tiếp hàm xử lý Visual ôm đồ của ngọc
            }
            else
            {
                // Gửi lệnh lên máy chủ khi chạy chế độ mạng Online
                Debug.Log($"[CrystalDebug] Requesting ServerRpc for NetworkObjectId: {core.NetworkObject.NetworkObjectId}");
                RequestPickupServerRpc(core.NetworkObject.NetworkObjectId);
            }
        }
        else
        {
            Debug.LogWarning("[CrystalDebug] No CrystalCore component found within 3 meters!");
        }
    }

    private void HandleDropAction()
    {
        Debug.Log("[CrystalDebug] HandleDropAction called.");
        // Ép chạy hoạt ảnh thả đồ ngay lập tức
        Animator anim = GetPlayerAnimator();
        if (anim != null)
        {
            SafeSetBool(anim, "Bung", false);
            SafeSetBool(anim, "IsCarrying", false);
            SafeSetTrigger(anim, "isDroppingLog");
        }

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            Debug.LogWarning("[Scene Test] Đang thả ngọc Offline.");
            if (localHeldCrystalOffline != null)
            {
                localHeldCrystalOffline.PerformDrop();
                localHeldCrystalOffline = null;
            }
            isCarryingCoreOffline = false;
            HandleCarryingCoreVisuals(false);
        }
        else
        {
            Debug.Log("[CrystalDebug] Requesting Drop ServerRpc.");
            RequestDropServerRpc();
        }
    }

    private void HandleOfflineSnapToBox()
    {
        if (localHeldCrystalOffline != null && currentInteractBox != null)
        {
            CrystalCore coreToSnap = localHeldCrystalOffline;
            
            // Xóa trạng thái đang cầm trên tay
            localHeldCrystalOffline = null;
            isCarryingCoreOffline = false;
            HandleCarryingCoreVisuals(false);

            // Đưa ngọc thẳng vào bệ và khóa lại
            coreToSnap.StartSnappingToStation(currentInteractBox.crystalSnapPoint);
            currentInteractBox.isCrystalLocked.Value = true; 
        }
    }

    private void HandleOfflineSnapToPillar()
    {
        if (localHeldCrystalOffline != null && currentPillarStation != null)
        {
            CrystalCore coreToSnap = localHeldCrystalOffline;

            localHeldCrystalOffline = null;
            isCarryingCoreOffline = false;
            HandleCarryingCoreVisuals(false);

            // Gọi logic tương tác hoặc khóa vào cột Pillar tùy thuộc cấu trúc cột của bạn
            currentPillarStation.TryInteract(this, 0); 
            coreToSnap.LockToStation();
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
                core.holderId.Value = OwnerClientId;
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
                core.holderId.Value = ulong.MaxValue;
            }
            heldCoreNetworkId.Value = ulong.MaxValue;
            isCarryingCore.Value = false;
        }
    }

    public void ForceDropFromStation()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            if (!IsServer) return;
            heldCoreNetworkId.Value = ulong.MaxValue;
            isCarryingCore.Value = false;
        }
        else
        {
            localHeldCrystalOffline = null;
            isCarryingCoreOffline = false;
            HandleCarryingCoreVisuals(false);
        }
    }
}