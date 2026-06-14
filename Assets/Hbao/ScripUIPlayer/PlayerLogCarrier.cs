using UnityEngine;

public class PlayerLogCarrier : MonoBehaviour
{
    [Tooltip("Trạng thái người chơi có đang bưng gỗ hay không")]
    public bool isCarrying = false;

    private GameObject carriedLogInstance;

    public void CarryLog()
    {
        isCarrying = true;
        
        // Hide weapons
        TogglePlayerWeapons(false);

        // Tìm xương tay phải để gắn gỗ (hoặc tay trái nếu không thấy)
        Transform handBone = FindHandBone(transform);
        
        // Tạo visual mô phỏng khúc gỗ hình trụ nằm ngang
        carriedLogInstance = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(carriedLogInstance.GetComponent<Collider>()); // Loại bỏ Collider tránh ảnh hưởng va chạm của player
        
        if (handBone != null)
        {
            carriedLogInstance.transform.SetParent(handBone, false);
            // Điều chỉnh vị trí, góc xoay để khúc gỗ nằm cân giữa 2 tay bưng
            carriedLogInstance.transform.localPosition = new Vector3(-0.1f, 0.18f, 0f);
            carriedLogInstance.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        }
        else
        {
            // Fallback nếu không quét được xương tay (gắn trước ngực làm mốc)
            carriedLogInstance.transform.SetParent(transform, false);
            carriedLogInstance.transform.localPosition = new Vector3(0f, 1.15f, 0.45f);
            carriedLogInstance.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        }
        
        // Kích thước khúc gỗ
        carriedLogInstance.transform.localScale = new Vector3(0.18f, 0.45f, 0.18f);
        
        // Tô màu nâu gỗ
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            Material mat = new Material(shader);
            mat.color = new Color(0.45f, 0.28f, 0.12f);
            carriedLogInstance.GetComponent<Renderer>().material = mat;
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
        SafeSetBool(anim, "Bung", carrying);
        SafeSetBool(anim, "IsCarrying", carrying);

        if (carrying)
        {
            SafeSetTrigger(anim, "BungTrigger");
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
            Transform found = FindHandBone(current.GetChild(i));
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
