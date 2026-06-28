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
    // ĐÃ XÓA: public PillarStation currentPillarStation = null;
    
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
                    // Đang ôm ngọc trên tay -> Thực hiện đặt vào Trạm Box
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
                    // ĐÃ XÓA: Khúc code xử lý đặt ngọc vào currentPillarStation ở đây
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

        // 2. Fallback: Check all colliders within 3f
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
            // Bắt đầu hoạt ảnh nhặt và set pending giống CollectibleItemDrop
            PlayPickupAnimation();
            SetPendingPickItem(core.gameObject);
            StartCoroutine(CollectCrystalSequence(core));
        }
        else
        {
            Debug.LogWarning("[CrystalDebug] No CrystalCore component found within 3 meters!");
        }
    }

    private void PlayPickupAnimation()
    {
        var target = GetComponent<IPlayerHUDTarget>();
        if (target is LeoPlayer leo) leo.PlayAnimation("Pick", 0.1f);
        else if (target is ArthurPlayer arthur) arthur.PlayAnimation("Idle_Pick", 0.1f);
        else if (target is ElenaPlayer elena) elena.PlayAnimation("Idle_Pick", 0.1f);
        else if (target is MayaPlayer maya) maya.PlayAnimation("Idle_Pick", 0.1f);
        else
        {
            Animator anim = GetPlayerAnimator();
            if (anim != null)
            {
                SafeSetTrigger(anim, "BungTrigger");
                SafeSetTrigger(anim, "Bưng");
            }
        }
    }

    private void SetPendingPickItem(GameObject item)
    {
        var target = GetComponent<IPlayerHUDTarget>();
        if (target is LeoPlayer leo) leo.pendingPickItem = item;
        else if (target is ArthurPlayer arthur) arthur.pendingPickItem = item;
        else if (target is ElenaPlayer elena) elena.pendingPickItem = item;
        else if (target is MayaPlayer maya) maya.pendingPickItem = item;
        else if (target is SimplePlayerTest spt) spt.pendingPickItem = item;
    }

    private GameObject GetPendingPickItem()
    {
        var target = GetComponent<IPlayerHUDTarget>();
        if (target is LeoPlayer leo) return leo.pendingPickItem;
        if (target is ArthurPlayer arthur) return arthur.pendingPickItem;
        if (target is ElenaPlayer elena) return elena.pendingPickItem;
        if (target is MayaPlayer maya) return maya.pendingPickItem;
        if (target is SimplePlayerTest spt) return spt.pendingPickItem;
        return null;
    }

    private System.Collections.IEnumerator CollectCrystalSequence(CrystalCore core)
    {
        yield return new WaitForSeconds(1.0f);

        if (core != null && GetPendingPickItem() == core.gameObject)
        {
            SetPendingPickItem(null);

            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                // Offline
                string itemName = "Ngoc" + core.crystalID;
                bool added = AddCrystalToInventory(itemName);
                if (added)
                {
                    Destroy(core.gameObject);
                }
            }
            else
            {
                // Online: Gửi yêu cầu nhặt lên server để xác thực tránh tranh chấp
                Debug.Log($"[CrystalDebug] Requesting ServerRpc to pick up crystal: {core.NetworkObject.NetworkObjectId}");
                RequestPickupCrystalServerRpc(core.NetworkObject.NetworkObjectId);
            }
        }
    }

    private bool AddCrystalToInventory(string itemName)
    {
        var leoComp = GetComponent<LeoPlayer>();
        if (leoComp != null) return leoComp.TryAddItem(itemName, false);
        
        var arthurComp = GetComponent<ArthurPlayer>();
        if (arthurComp != null) return arthurComp.TryAddItem(itemName, false);
        
        var elenaComp = GetComponent<ElenaPlayer>();
        if (elenaComp != null) return elenaComp.TryAddItem(itemName);
        
        var mayaComp = GetComponent<MayaPlayer>();
        if (mayaComp != null) return mayaComp.TryAddItem(itemName);
        
        var sptComp = GetComponent<SimplePlayerTest>();
        if (sptComp != null) return sptComp.TryAddItem(itemName);

        return false;
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

    // ĐÃ XÓA: Hàm HandleOfflineSnapToPillar() 

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

    public void RequestDropItem(string itemName)
    {
        if (itemName == "Ngoc1" || itemName == "Ngoc2")
        {
            int crystalID = (itemName == "Ngoc1") ? 1 : 2;

            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                SpawnCrystalLocal(crystalID);
            }
            else
            {
                RequestDropCrystalServerRpc(crystalID);
            }
        }
    }

    private void SpawnCrystalLocal(int crystalID)
    {
        GameObject prefab = Resources.Load<GameObject>($"Crystal_Prefab_{crystalID}");
        if (prefab == null) prefab = Resources.Load<GameObject>("Crystal_Default");

        if (prefab != null)
        {
            Vector3 spawnPos = transform.position + Vector3.up * 0.5f + transform.forward * 0.6f;
            GameObject crystal = Instantiate(prefab, spawnPos, Quaternion.identity);
            
            var core = crystal.GetComponent<CrystalCore>();
            if (core != null)
            {
                core.crystalID = crystalID;
                core.holderId.Value = ulong.MaxValue;
            }

            var rb = crystal.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.AddForce(transform.forward * 2f, ForceMode.Impulse);
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestDropCrystalServerRpc(int crystalID)
    {
        GameObject prefab = Resources.Load<GameObject>($"Crystal_Prefab_{crystalID}");
        if (prefab == null) prefab = Resources.Load<GameObject>("Crystal_Default");

        if (prefab != null)
        {
            Vector3 spawnPos = transform.position + Vector3.up * 0.5f + transform.forward * 0.6f;
            GameObject crystal = Instantiate(prefab, spawnPos, Quaternion.identity);
            
            var core = crystal.GetComponent<CrystalCore>();
            if (core != null)
            {
                core.crystalID = crystalID;
                core.holderId.Value = ulong.MaxValue;
            }

            var netObj = crystal.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn();
            }

            var rb = crystal.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.AddForce(transform.forward * 2f, ForceMode.Impulse);
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPickupCrystalServerRpc(ulong networkObjectId, ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out var netObj))
        {
            var core = netObj.GetComponent<CrystalCore>();
            // Kiểm tra bảo vệ: ngọc chưa bị snap và chưa có ai nhặt
            if (core != null && !core.isSnapped.Value && core.holderId.Value == ulong.MaxValue)
            {
                // Đánh dấu đã được nhặt bởi sender
                core.holderId.Value = senderClientId;

                // Gửi lệnh ClientRpc cho client tương ứng để thêm ngọc vào túi đồ
                AddCrystalToInventoryClientRpc(senderClientId, "Ngoc" + core.crystalID);

                // Despawn viên ngọc trên mạng
                if (netObj.IsSpawned)
                {
                    netObj.Despawn();
                }
            }
        }
    }

    [ClientRpc]
    private void AddCrystalToInventoryClientRpc(ulong targetClientId, string itemName)
    {
        if (NetworkManager.Singleton.LocalClientId == targetClientId)
        {
            AddCrystalToInventory(itemName);
        }
    }
}