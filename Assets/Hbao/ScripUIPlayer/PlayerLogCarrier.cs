using UnityEngine;

public class PlayerLogCarrier : MonoBehaviour
{
    [Tooltip("Trạng thái người chơi có đang bưng gỗ hay không")]
    public bool isCarrying = false;

    private GameObject carriedLogInstance;

    [Tooltip("Transform điểm gắn gỗ tùy chọn (nếu có, gỗ sẽ tự động gắn vào đây). Nếu bỏ trống sẽ gắn vào root player.")]
    public Transform carryTargetTransform;

    public void CarryLog(bool showVisualLog = true)
    {
        isCarrying = true;
        
        // Hide weapons
        TogglePlayerWeapons(false);
        
        if (!showVisualLog)
        {
            // Kích hoạt trạng thái Animator bưng gỗ
            PlayCarryAnimation(true);
            return;
        }
        
        // Cố gắng load prefab gỗ thật từ Resources
        GameObject logPrefab = Resources.Load<GameObject>("firewood_single");
        if (logPrefab == null)
        {
            logPrefab = Resources.Load<GameObject>("WoodLog");
        }

        if (logPrefab != null)
        {
            carriedLogInstance = Instantiate(logPrefab);
            carriedLogInstance.SetActive(true);
            
            // Dọn dẹp NetworkObject ngay lập tức để tránh lỗi re-parenting của Netcode
            var netObj = carriedLogInstance.GetComponent<Unity.Netcode.NetworkObject>();
            if (netObj != null)
            {
                DestroyImmediate(netObj);
            }

            // Dọn dẹp các thành quan trọng khác không cần thiết trên tệp gỗ thật ngay lập tức
            var colliders = carriedLogInstance.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                if (col != null) DestroyImmediate(col);
            }
            
            var rbs = carriedLogInstance.GetComponentsInChildren<Rigidbody>();
            foreach (var rb in rbs)
            {
                if (rb != null) DestroyImmediate(rb);
            }
            
            var comps = carriedLogInstance.GetComponentsInChildren<MonoBehaviour>();
            foreach (var comp in comps)
            {
                if (comp != null && comp != this) DestroyImmediate(comp);
            }
        }
        else
        {
            // Tạo cylinder làm giả gỗ nếu không tìm thấy prefab
            carriedLogInstance = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            carriedLogInstance.SetActive(true);
            Destroy(carriedLogInstance.GetComponent<Collider>());

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            
            if (shader != null)
            {
                Material mat = new Material(shader);
                mat.color = new Color(0.45f, 0.28f, 0.12f);
                carriedLogInstance.GetComponent<Renderer>().material = mat;
            }
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
            
            Debug.Log($"[PlayerLogCarrier] CarryLog: Attached log to custom carryTargetTransform '{carryTargetTransform.name}' with compensated scale: {carriedLogInstance.transform.localScale}");
        }
        else
        {
            // Vị trí ngang bụng/ngực và nằm ngang nối từ tay trái qua tay phải
            carriedLogInstance.transform.localPosition = new Vector3(0f, 0.95f, 0.42f);
            carriedLogInstance.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

            Debug.Log($"[PlayerLogCarrier] CarryLog: Attached cosmetic log to root player transform. compensated localScale={carriedLogInstance.transform.localScale}");
        }

        // Kích hoạt trạng thái Animator bưng gỗ
        PlayCarryAnimation(true);
    }

    public void DropLog()
    {
        isCarrying = false;
        if (carriedLogInstance != null)
        {
            Destroy(carriedLogInstance);
            carriedLogInstance = null;
        }
        
        // Restore weapons
        TogglePlayerWeapons(true);

        // Reset trạng thái Animator về bình thường
        PlayCarryAnimation(false);
    }

    private void TogglePlayerWeapons(bool active)
    {
        // Arthur
        var arthur = GetComponent<ArthurPlayer>();
        if (arthur != null)
        {
            if (!active)
            {
                if (arthur.leftHandWeapon != null) arthur.leftHandWeapon.SetActive(false);
                if (arthur.rightHandWeapon != null) arthur.rightHandWeapon.SetActive(false);
            }
            else
            {
                arthur.SyncWeaponVisuals(arthur.GetActiveWeaponIndex());
            }
        }
        // Leo
        var leo = GetComponent<LeoPlayer>();
        if (leo != null)
        {
            if (!active)
            {
                if (leo.leftHandSword != null) leo.leftHandSword.SetActive(false);
                if (leo.rightHandSword != null) leo.rightHandSword.SetActive(false);
            }
            else
            {
                leo.SyncWeaponVisuals(leo.GetActiveWeaponIndex());
            }
        }
        // Elena
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
        // Maya
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

        // Set các biến Animator để người dùng dễ dàng cấu hình Blend Tree / States cho dáng Bưng gỗ:
        // - "Bung" (bool)
        // - "IsCarrying" (bool)
        // - "BungTrigger" (trigger)
        // - "Bưng" (trigger)
        SafeSetBool(anim, "Bung", carrying);
        SafeSetBool(anim, "IsCarrying", carrying);

        if (carrying)
        {
            SafeSetTrigger(anim, "BungTrigger");
            SafeSetTrigger(anim, "Bưng");
        }
    }

    private void SafeSetBool(Animator anim, string paramName, bool value)
    {
        if (anim == null) return;
        foreach (var param in anim.parameters)
        {
            if (param.name == paramName)
            {
                anim.SetBool(paramName, value);
                return;
            }
        }
    }

    private void SafeSetTrigger(Animator anim, string paramName)
    {
        if (anim == null) return;
        foreach (var param in anim.parameters)
        {
            if (param.name == paramName)
            {
                anim.SetTrigger(paramName);
                return;
            }
        }
    }

    private Transform FindHandBone(Transform current)
    {
        // 1. Arthur
        var arthur = GetComponent<ArthurPlayer>();
        if (arthur != null)
        {
            if (arthur.rightHandWeapon != null) return arthur.rightHandWeapon.transform.parent;
            if (arthur.leftHandWeapon != null) return arthur.leftHandWeapon.transform.parent;
        }

        // 2. Leo
        var leo = GetComponent<LeoPlayer>();
        if (leo != null)
        {
            if (leo.rightHandSword != null) return leo.rightHandSword.transform.parent;
            if (leo.leftHandSword != null) return leo.leftHandSword.transform.parent;
        }

        // 3. Elena
        var elena = GetComponent<ElenaPlayer>();
        if (elena != null)
        {
            if (elena.weaponInHandVisual != null) return elena.weaponInHandVisual.transform.parent;
        }

        // 4. Maya
        var maya = GetComponent<MayaPlayer>();
        if (maya != null)
        {
            if (maya.weaponInHandVisual != null) return maya.weaponInHandVisual.transform.parent;
        }

        // Fallback: search hierarchy for hand keywords
        return FindHandBoneFallback(current);
    }

    private Transform FindHandBoneFallback(Transform current)
    {
        string nameLower = current.name.ToLower();
        if (nameLower.Contains("hand") || nameLower.Contains("wrist") || nameLower.Contains("palm"))
        {
            if (nameLower.Contains("right") || nameLower.Contains("_r") || nameLower.Contains("hand.r"))
            {
                return current;
            }
        }
        
        for (int i = 0; i < current.childCount; i++)
        {
            Transform found = FindHandBoneFallback(current.GetChild(i));
            if (found != null) return found;
        }
        
        return null;
    }

    private Transform FindCarryBone(Transform playerTransform)
    {
        // Thử tìm xương ngực/spine trước để khúc gỗ nằm ngang cân giữa 2 tay
        Transform chest = FindBoneByName(playerTransform, "chest");
        if (chest == null) chest = FindBoneByName(playerTransform, "spine_02");
        if (chest == null) chest = FindBoneByName(playerTransform, "spine02");
        if (chest == null) chest = FindBoneByName(playerTransform, "spine2");
        if (chest == null) chest = FindBoneByName(playerTransform, "upperchest");
        
        if (chest != null) return chest;

        // Nếu không có, thử xương spine1
        Transform spine = FindBoneByName(playerTransform, "spine");
        if (spine == null) spine = FindBoneByName(playerTransform, "spine_01");
        if (spine == null) spine = FindBoneByName(playerTransform, "spine01");
        
        if (spine != null) return spine;

        // Fallback về tay phải
        return FindHandBone(playerTransform);
    }

    private Transform FindBoneByName(Transform current, string targetName)
    {
        if (current.name.ToLower().Contains(targetName.ToLower()))
        {
            return current;
        }

        for (int i = 0; i < current.childCount; i++)
        {
            Transform found = FindBoneByName(current.GetChild(i), targetName);
            if (found != null) return found;
        }

        return null;
    }

    private void OnDestroy()
    {
        if (carriedLogInstance != null)
        {
            Destroy(carriedLogInstance);
        }
    }
}
