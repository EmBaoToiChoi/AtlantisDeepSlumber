using UnityEngine;

public class PlayerLogCarrier : MonoBehaviour
{
    [Tooltip("Trạng thái người chơi có đang bưng đồ hay không")]
    public bool isCarrying = false;

    private GameObject carriedLogInstance;
    private GameObject carriedCrystalInstance; // Lưu Visual của Tinh Thể[cite: 4]

    [Tooltip("Transform điểm gắn đồ tùy chọn. Nếu bỏ trống sẽ gắn vào root player.")]
    public Transform carryTargetTransform;

    // --- CƠ CHẾ ÔM NGỌC/TINH THỂ (MỚI THÊM) ---[cite: 4]
    public void CarryCrystal(int crystalID, bool showVisualCrystal = true)
    {
        isCarrying = true;
        TogglePlayerWeapons(false); // Ẩn vũ khí[cite: 4]
        PlayCarryAnimation(true);   // Kích hoạt animation bưng[cite: 4]

        if (!showVisualCrystal) return;

        // Tìm kiếm Prefab tinh thể tương ứng trong thư mục Resources[cite: 4]
        GameObject crystalPrefab = Resources.Load<GameObject>($"Crystal_Prefab_{crystalID}");
        if (crystalPrefab == null)
        {
            crystalPrefab = Resources.Load<GameObject>("Crystal_Default");
        }
        if (crystalPrefab == null)
        {
            crystalPrefab = Resources.Load<GameObject>("crystal");
        }
        if (crystalPrefab == null)
        {
            crystalPrefab = Resources.Load<GameObject>("BlueCrystal");
        }
        if (crystalPrefab == null)
        {
            crystalPrefab = Resources.Load<GameObject>("GreenCrystal");
        }
        if (crystalPrefab == null)
        {
            crystalPrefab = Resources.Load<GameObject>("OrangeCrystal");
        }
        if (crystalPrefab == null)
        {
            crystalPrefab = Resources.Load<GameObject>("RedCrystal");
        }

        if (crystalPrefab != null)
        {
            carriedCrystalInstance = Instantiate(crystalPrefab);
            carriedCrystalInstance.SetActive(true);
            
            var netObj = carriedCrystalInstance.GetComponent<Unity.Netcode.NetworkObject>();
            if (netObj != null) DestroyImmediate(netObj);

            foreach (var col in carriedCrystalInstance.GetComponentsInChildren<Collider>()) DestroyImmediate(col);
            foreach (var rb in carriedCrystalInstance.GetComponentsInChildren<Rigidbody>()) DestroyImmediate(rb);
        }
        else
        {
            // Tạo tạm một khối cầu nếu chưa setup Prefab trong Resources[cite: 4]
            carriedCrystalInstance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            carriedCrystalInstance.SetActive(true);
            Destroy(carriedCrystalInstance.GetComponent<Collider>());

            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = Color.cyan;
            carriedCrystalInstance.GetComponent<Renderer>().material = mat;
        }

        Transform parentTransform = carryTargetTransform != null ? carryTargetTransform : transform;
        carriedCrystalInstance.transform.SetParent(parentTransform, false);

        if (carryTargetTransform != null)
        {
            carriedCrystalInstance.transform.localPosition = Vector3.zero;
            carriedCrystalInstance.transform.localRotation = Quaternion.identity;
        }
        else
        {
            carriedCrystalInstance.transform.localPosition = new Vector3(0f, 1.0f, 0.4f); // Ngang ngực
            carriedCrystalInstance.transform.localRotation = Quaternion.identity;
        }
    }

    public void DropCrystal()
    {
        isCarrying = false;
        if (carriedCrystalInstance != null)
        {
            Destroy(carriedCrystalInstance);
            carriedCrystalInstance = null;
        }
        TogglePlayerWeapons(true); // Trả lại vũ khí[cite: 4]
        PlayCarryAnimation(false);  // Tắt animation bưng[cite: 4]
    }

    // --- CƠ CHẾ BƯNG GỖ NGUYÊN BẢN ---[cite: 4]
    public void CarryLog(bool showVisualLog = true)
    {
        isCarrying = true;
        TogglePlayerWeapons(false);
        PlayCarryAnimation(true);
        if (!showVisualLog) return;
        
        GameObject logPrefab = Resources.Load<GameObject>("firewood_single");
        if (logPrefab == null) logPrefab = Resources.Load<GameObject>("WoodLog");

        if (logPrefab != null)
        {
            carriedLogInstance = Instantiate(logPrefab);
            carriedLogInstance.SetActive(true);
            
            var netObj = carriedLogInstance.GetComponent<Unity.Netcode.NetworkObject>();
            if (netObj != null) DestroyImmediate(netObj);

            foreach (var col in carriedLogInstance.GetComponentsInChildren<Collider>()) DestroyImmediate(col);
            foreach (var rb in carriedLogInstance.GetComponentsInChildren<Rigidbody>()) DestroyImmediate(rb);
        }
        else
        {
            carriedLogInstance = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            carriedLogInstance.SetActive(true);
            Destroy(carriedLogInstance.GetComponent<Collider>());

            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.45f, 0.28f, 0.12f);
            carriedLogInstance.GetComponent<Renderer>().material = mat;
        }
        
        Transform parentTransform = carryTargetTransform != null ? carryTargetTransform : transform;
        carriedLogInstance.transform.SetParent(parentTransform, false);

        Vector3 targetWorldScale = logPrefab != null ? logPrefab.transform.localScale : new Vector3(0.18f, 0.45f, 0.18f);
        Vector3 parentLossyScale = parentTransform.lossyScale;
        carriedLogInstance.transform.localScale = new Vector3(
            targetWorldScale.x / (parentLossyScale.x != 0 ? parentLossyScale.x : 1f),
            targetWorldScale.y / (parentLossyScale.y != 0 ? parentLossyScale.y : 1f),
            targetWorldScale.z / (parentLossyScale.z != 0 ? parentLossyScale.z : 1f)
        );

        if (carryTargetTransform != null)
        {
            carriedLogInstance.transform.localPosition = Vector3.zero;
            carriedLogInstance.transform.localRotation = Quaternion.identity;
        }
        else
        {
            carriedLogInstance.transform.localPosition = new Vector3(0f, 0.95f, 0.42f);
            carriedLogInstance.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        }
    }

    public void DropLog()
    {
        isCarrying = false;
        if (carriedLogInstance != null)
        {
            Destroy(carriedLogInstance);
            carriedLogInstance = null;
        }
        TogglePlayerWeapons(true);
        PlayCarryAnimation(false);
    }

    public void TogglePlayerWeapons(bool active)
    {
        var arthur = GetComponent<ArthurPlayer>();
        if (arthur != null)
        {
            if (!active)
            {
                if (arthur.leftHandWeapon != null) arthur.leftHandWeapon.SetActive(false);
                if (arthur.rightHandWeapon != null) arthur.rightHandWeapon.SetActive(false);
            }
            else arthur.SyncWeaponVisuals(arthur.GetActiveWeaponIndex());
        }
        var leo = GetComponent<LeoPlayer>();
        if (leo != null)
        {
            if (!active)
            {
                if (leo.leftHandSword != null) leo.leftHandSword.SetActive(false);
                if (leo.rightHandSword != null) leo.rightHandSword.SetActive(false);
            }
            else leo.SyncWeaponVisuals(leo.GetActiveWeaponIndex());
        }
        var elena = GetComponent<ElenaPlayer>();
        if (elena != null)
        {
            if (!active)
            {
                if (elena.weaponInHandVisual != null) elena.weaponInHandVisual.SetActive(false);
            }
            else
            {
                int activeIdx = elena.GetActiveWeaponIndex();
                if (elena.weaponInHandVisual != null) elena.weaponInHandVisual.SetActive(activeIdx == 2);
                if (elena.weaponOnBackVisual != null) elena.weaponOnBackVisual.SetActive(activeIdx == 1);
            }
        }
        var maya = GetComponent<MayaPlayer>();
        if (maya != null)
        {
            if (!active)
            {
                if (maya.weaponInHandVisual != null) maya.weaponInHandVisual.SetActive(false);
            }
            else
            {
                int activeIdx = maya.GetActiveWeaponIndex();
                if (maya.weaponInHandVisual != null) maya.weaponInHandVisual.SetActive(activeIdx == 2);
                if (maya.weaponOnBackVisual != null) maya.weaponOnBackVisual.SetActive(activeIdx == 1);
            }
        }
    }

    private void PlayCarryAnimation(bool carrying)
    {
        Animator anim = GetComponentInChildren<Animator>();
        if (anim == null) return;

        SafeSetBool(anim, "Bung", carrying);
        SafeSetBool(anim, "IsCarrying", carrying);

        if (carrying)
        {
            SafeSetTrigger(anim, "BungTrigger");
            SafeSetTrigger(anim, "Bưng");
        }

        if (anim.layerCount > 1)
        {
            anim.SetLayerWeight(1, carrying ? 1f : 0f);
        }
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

    private void Update()
    {
        if (isCarrying && carriedLogInstance == null && carriedCrystalInstance == null)
        {
            isCarrying = false;
            TogglePlayerWeapons(true);
            PlayCarryAnimation(false);
            return;
        }

        if (!isCarrying) return;

        IPlayerHUDTarget player = GetComponent<IPlayerHUDTarget>();
        if (player == null) return;

        if (!(player.IsStandaloneMode || player.IsOwner)) return;

        if (Input.GetKeyDown(KeyCode.G))
        {
            if (carriedLogInstance != null && !IsNearBridgeRepairTrigger())
            {
                player.RequestDropWoodLog();
            }
        }
    }

    private bool IsNearBridgeRepairTrigger()
    {
        Collider[] cols = Physics.OverlapSphere(transform.position, 5f);
        foreach (var col in cols)
        {
            var repairTrigger = col.GetComponent<BridgeRepairTrigger>();
            if (repairTrigger != null)
            {
                var bridge = repairTrigger.bridgeController;
                if (bridge == null) bridge = FindAnyObjectByType<BridgeCollapseTrigger>();
                if (bridge != null && bridge.IsBridgeCollapsed() && !bridge.IsBridgeRepaired())
                {
                    if (Vector3.Distance(transform.position, col.bounds.ClosestPoint(transform.position)) <= bridge.repairInteractRadius) return true;
                }
            }
        }
        return false;
    }

    private void OnDestroy()
    {
        if (carriedLogInstance != null) Destroy(carriedLogInstance);
        if (carriedCrystalInstance != null) Destroy(carriedCrystalInstance); // Giải phóng bộ nhớ Visual Tinh thể[cite: 4]
    }
}