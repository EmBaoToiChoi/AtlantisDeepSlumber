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

    private PlayerHUDController localHud = null;
    private bool showedPickupPrompt = false;
    private bool showedCarryPrompt = false;

    private PlayerHUDController GetHUD()
    {
        if (localHud == null)
        {
            localHud = FindAnyObjectByType<PlayerHUDController>();
        }
        return localHud;
    }

    private void Awake()
    {
        if (holdPoint == null)
        {
            holdPoint = FindCarryBone(transform);
            if (holdPoint == null) holdPoint = transform;
        }
    }

    private Transform FindCarryBone(Transform playerTransform)
    {
        Transform chest = FindBoneByName(playerTransform, "chest");
        if (chest == null) chest = FindBoneByName(playerTransform, "spine_02");
        if (chest == null) chest = FindBoneByName(playerTransform, "spine02");
        if (chest == null) chest = FindBoneByName(playerTransform, "spine2");
        if (chest == null) chest = FindBoneByName(playerTransform, "upperchest");
        
        if (chest != null) return chest;

        Transform spine = FindBoneByName(playerTransform, "spine");
        if (spine == null) spine = FindBoneByName(playerTransform, "spine_01");
        if (spine == null) spine = FindBoneByName(playerTransform, "spine01");
        
        if (spine != null) return spine;
        return null;
    }

    private Transform FindBoneByName(Transform current, string targetName)
    {
        if (current.name.ToLower().Contains(targetName.ToLower())) return current;

        for (int i = 0; i < current.childCount; i++)
        {
            Transform found = FindBoneByName(current.GetChild(i), targetName);
            if (found != null) return found;
        }
        return null;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        isCarryingCore.OnValueChanged += OnCarryingCoreChanged;
        
        if (isCarryingCore.Value)
        {
            OnCarryingCoreChanged(false, true);
        }
    }

    public override void OnNetworkDespawn()
    {
        isCarryingCore.OnValueChanged -= OnCarryingCoreChanged;
        if (showedPickupPrompt || showedCarryPrompt)
        {
            var hud = GetHUD();
            if (hud != null) hud.ShowInteractionPrompt(false, "");
        }
        base.OnNetworkDespawn();
    }

    private void OnCarryingCoreChanged(bool oldVal, bool newVal)
    {
        // 1. Tìm Animator (Tự động nhận diện cho cả 4 nhân vật Arthur, Leo, Elena, Maya)
        Animator anim = GetComponentInChildren<Animator>();
        
        // 2. Đồng bộ biến IsCarrying sang Animator để chuyển trạng thái Blend Tree/Animation sang bê đồ
        if (anim != null)
        {
            anim.SetBool("IsCarrying", newVal);
        }

        // (ĐÃ XÓA 4 DÒNG TÌM NHÂN VẬT GÂY LỖI Ở ĐÂY)

        // 3. Quản lý việc bật tắt logic hiển thị model bên PlayerLogCarrier
        var carrier = GetComponent<PlayerLogCarrier>();
        if (carrier != null)
        {
            if (newVal) 
            {
                carrier.CarryLog(false); 
            }
            else 
            {
                carrier.DropLog();
            }
        }
    }

    private float GetPlayerHealth()
    {
        var target = GetComponent<IPlayerHUDTarget>();
        if (target != null) return target.CurrentHealth;
        return 100f;
    }

    private bool IsLocalPlayer()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) return IsOwner;
        if (PlayerHUDController.LocalPlayerTarget != null && PlayerHUDController.LocalPlayerTarget.gameObject == gameObject) return true;
        if (PlayerHUDController.LocalPlayerTarget == null) return CompareTag("Player");
        return false;
    }

    void Update()
    {
        if (!IsLocalPlayer()) return;

        var hud = GetHUD();

        // Tự động thả ngọc khi chết
        if (isCarryingCore.Value && GetPlayerHealth() <= 0)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                PerformThrowOffline(transform.forward);
            else
                RequestThrowServerRpc(transform.forward);
            
            if (hud != null) hud.ShowInteractionPrompt(false, "");
            return;
        }

        // Gọi hàm quản lý UI (Hàm này đã được thêm đầy đủ ở bên dưới)
        ManageInteractionUI(hud);

        if (Keyboard.current != null)
        {
            // ====== PHÍM F: NHẶT ĐỒ / ĐẶT NGỌC ======
            if (Keyboard.current.fKey.wasPressedThisFrame)
            {
                var carrier = GetComponent<PlayerLogCarrier>();
                bool isCarryingLog = (carrier != null && carrier.isCarrying);

                if (!isCarryingCore.Value && !isCarryingLog) 
                {
                    var target = GetComponent<IPlayerHUDTarget>();
                    if (target != null && target.GetActiveWeaponIndex() == 2)
                    {
                        if (hud != null) hud.ShowMissionAlert("Bạn phải cất vũ khí mới nhặt được đồ!", 3.0f);
                        return;
                    }
                    TryPickupInteractable();
                }
                else if (isCarryingCore.Value)
                {
                    if (currentPillarStation != null)
                    {
                        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                            PerformSnapPillarOffline(currentPillarStation);
                        else
                            currentPillarStation.TryInteract(this, heldCoreNetworkId.Value);
                    }
                    else if (currentInteractBox != null && (currentInteractBox.stationIndex == 2 || currentInteractBox.stationIndex == 3) && !currentInteractBox.isCrystalLocked.Value)
                    {
                        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                            PerformSnapInteractBoxOffline(currentInteractBox);
                        else
                            currentInteractBox.TrySnapCrystal();
                    }
                }
            }

            // ====== PHÍM G: THẢ ĐỒ XUỐNG ĐẤT ======
            if (Keyboard.current.gKey.wasPressedThisFrame)
            {
                if (isCarryingCore.Value)
                {
                    if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                        PerformThrowOffline(transform.forward);
                    else
                        RequestThrowServerRpc(transform.forward);
                }
                else 
                {
                    var carrier = GetComponent<PlayerLogCarrier>();
                    if (carrier != null && carrier.isCarrying)
                    {
                        SendMessage("RequestDropWoodLog", SendMessageOptions.DontRequireReceiver);
                    }
                }
            }
        }
    }

    // ĐÂY LÀ HÀM UI BỊ THIẾU MÀ MÌNH ĐÃ BỔ SUNG LẠI
    private void ManageInteractionUI(PlayerHUDController hud)
    {
        if (isCarryingCore.Value)
        {
            showedPickupPrompt = false;
            if (hud != null)
            {
                if (currentPillarStation != null || (currentInteractBox != null && (currentInteractBox.stationIndex == 2 || currentInteractBox.stationIndex == 3) && !currentInteractBox.isCrystalLocked.Value))
                {
                    hud.ShowInteractionPrompt(true, "Ấn [F] để đặt Ngọc");
                }
                else
                {
                    hud.ShowInteractionPrompt(true, "Ấn [G] để thả Ngọc");
                }
                showedCarryPrompt = true;
            }
        }
        else
        {
            if (showedCarryPrompt)
            {
                if (hud != null) hud.ShowInteractionPrompt(false, "");
                showedCarryPrompt = false;
            }

            bool nearCore = false;
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, 2.5f, interactableLayer);
            foreach (var hit in hitColliders)
            {
                if ((hit.TryGetComponent<PuzzleCrystalCore>(out var puzzleCore) && !puzzleCore.isSnapped.Value && (puzzleCore.holderId.Value == ulong.MaxValue || puzzleCore.holderId.Value == 9999)) ||
                    (hit.TryGetComponent<CrystalCore>(out var core) && !core.isSnapped.Value && (core.holderId.Value == ulong.MaxValue || core.holderId.Value == 9999)))
                {
                    nearCore = true;
                    break;
                }
            }

            if (nearCore)
            {
                if (!showedPickupPrompt)
                {
                    if (hud != null) hud.ShowInteractionPrompt(true, "Ấn [F] để nhặt Ngọc");
                    showedPickupPrompt = true;
                }
            }
            else
            {
                if (showedPickupPrompt)
                {
                    if (hud != null) hud.ShowInteractionPrompt(false, "");
                    showedPickupPrompt = false;
                }
            }
        }
    }

    private void TryPickupInteractable()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, 2f, interactableLayer);
        bool foundCore = false;

        foreach (var hit in hitColliders)
        {
            if (hit.TryGetComponent<PuzzleCrystalCore>(out var puzzleCore) && !puzzleCore.isSnapped.Value)
            {
                if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                    PerformPickupPuzzleOffline(puzzleCore);
                else
                    RequestPickupPuzzleServerRpc(puzzleCore.NetworkObject.NetworkObjectId);
                foundCore = true;
                break;
            }
            else if (hit.TryGetComponent<CrystalCore>(out var core) && !core.isSnapped.Value)
            {
                if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                    PerformPickupOffline(core);
                else
                    RequestPickupServerRpc(core.NetworkObject.NetworkObjectId);
                foundCore = true;
                break;
            }
        }

        if (!foundCore)
        {
            var carrier = GetComponent<PlayerLogCarrier>();
            if (carrier != null && !carrier.isCarrying)
            {
                carrier.SendMessage("TryPickupLog", SendMessageOptions.DontRequireReceiver); 
            }
        }
    }

    [ServerRpc]
    private void RequestPickupServerRpc(ulong networkObjectId, ServerRpcParams rpcParams = default)
    {
        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out var netObj))
        {
            var core = netObj.GetComponent<CrystalCore>();
            if (core.holderId.Value != ulong.MaxValue) return;

            ulong senderId = rpcParams.Receive.SenderClientId;
            if (NetworkManager.ConnectedClients.TryGetValue(senderId, out var client) && client.PlayerObject != null)
            {
                var target = client.PlayerObject.GetComponent<IPlayerHUDTarget>();
                if (target != null && target.GetActiveWeaponIndex() == 2) return;

                var carrier = client.PlayerObject.GetComponent<PlayerLogCarrier>();
                if (carrier != null && carrier.isCarrying) return;
                
                var pInt = client.PlayerObject.GetComponent<PlayerInteraction>();
                if (pInt != null && pInt.isCarryingCore.Value) return;
            }

            core.PerformPickup(senderId);
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

    [ServerRpc]
    private void RequestPickupPuzzleServerRpc(ulong networkObjectId, ServerRpcParams rpcParams = default)
    {
        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out var netObj))
        {
            var core = netObj.GetComponent<PuzzleCrystalCore>();
            ulong senderId = rpcParams.Receive.SenderClientId;

            if (core.holderId.Value != ulong.MaxValue) return; 

            if (NetworkManager.ConnectedClients.TryGetValue(senderId, out var client) && client.PlayerObject != null)
            {
                var target = client.PlayerObject.GetComponent<IPlayerHUDTarget>();
                if (target != null && target.GetActiveWeaponIndex() == 2) return;

                var carrier = client.PlayerObject.GetComponent<PlayerLogCarrier>();
                if (carrier != null && carrier.isCarrying) return;
                
                var pInt = client.PlayerObject.GetComponent<PlayerInteraction>();
                if (pInt != null && pInt.isCarryingCore.Value) return;
            }

            if (core.CanPickup(senderId))
            {
                core.PerformPickup(senderId);
                heldCoreNetworkId.Value = networkObjectId;
                isCarryingCore.Value = true;
            }
            else
            {
                core.RepelPlayer(senderId);
            }
        }
    }

    [ServerRpc]
    private void RequestThrowServerRpc(Vector3 throwDirection, ServerRpcParams rpcParams = default)
    {
        if (heldCoreNetworkId.Value != ulong.MaxValue && 
            NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(heldCoreNetworkId.Value, out var netObj))
        {
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

    private void SetCarryingCoreState(bool carrying)
    {
        isCarryingCore.Value = carrying;
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            OnCarryingCoreChanged(false, carrying);
        }
    }

    private void PerformPickupOffline(CrystalCore core)
    {
        core.holderId.Value = 9999;
        core.PerformPickup(9999);
        heldCoreNetworkId.Value = core.gameObject.GetInstanceID() > 0 ? (ulong)core.gameObject.GetInstanceID() : 99999;
        currentHeldCore = core;
        SetCarryingCoreState(true);
    }

    private void PerformPickupPuzzleOffline(PuzzleCrystalCore puzzleCore)
    {
        puzzleCore.holderId.Value = 9999;
        puzzleCore.PerformPickup(9999);
        heldCoreNetworkId.Value = puzzleCore.gameObject.GetInstanceID() > 0 ? (ulong)puzzleCore.gameObject.GetInstanceID() : 99999;
        currentHeldCore = null; 
        SetCarryingCoreState(true);
    }

    private void PerformThrowOffline(Vector3 throwDirection)
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, 5f, interactableLayer);
        foreach (var hit in hitColliders)
        {
            if (hit.TryGetComponent<PuzzleCrystalCore>(out var puzzleCore) && puzzleCore.holderId.Value == 9999)
            {
                puzzleCore.PerformThrow(throwDirection);
                break;
            }
            else if (hit.TryGetComponent<CrystalCore>(out var core) && core.holderId.Value == 9999)
            {
                core.PerformDrop();
                break;
            }
        }
        
        heldCoreNetworkId.Value = ulong.MaxValue;
        SetCarryingCoreState(false);
    }

    private void PerformSnapPillarOffline(PillarStation station)
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, 5f, interactableLayer);
        foreach (var hit in hitColliders)
        {
            if (hit.TryGetComponent<CrystalCore>(out var core) && core.holderId.Value == 9999)
            {
                core.StartSnappingToStation(station.snapPosition);
                station.isOccupied.Value = true;
                if (station.pillarEffect != null) station.pillarEffect.SetActive(true);
                break;
            }
        }
        
        heldCoreNetworkId.Value = ulong.MaxValue;
        SetCarryingCoreState(false);
    }

    private void PerformSnapInteractBoxOffline(InteractBox box)
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, 5f, interactableLayer);
        foreach (var hit in hitColliders)
        {
            if (hit.TryGetComponent<CrystalCore>(out var core) && core.holderId.Value == 9999)
            {
                core.LockToStation();
                core.transform.position = box.crystalSnapPoint.position;
                core.transform.rotation = box.crystalSnapPoint.rotation;
                
                var snapFollow = core.GetComponent<CrystalSnapFollow>();
                if (snapFollow != null) snapFollow.targetSnapPoint = box.crystalSnapPoint;
                
                box.isCrystalLocked.Value = true;
                break;
            }
        }
        
        heldCoreNetworkId.Value = ulong.MaxValue;
        SetCarryingCoreState(false);
    }
}