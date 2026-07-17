using UnityEngine;

public class PlayerAnimationBridge : MonoBehaviour
{
    private ElenaPlayer mainPlayerScript;

    void Start()
    {
        // Tự động tìm script ElenaPlayer nằm trên Object cha (Player2)
        mainPlayerScript = GetComponentInParent<ElenaPlayer>();
        
        if (mainPlayerScript == null)
        {
            Debug.LogError($"[{gameObject.name}] Không tìm thấy script ElenaPlayer trên Object cha!");
        }
    }

    // Hàm này sẽ hứng Event từ Animator của Object con
    public void OnWeaponDrawGrab()
    {
        if (mainPlayerScript != null)
        {
            mainPlayerScript.OnWeaponDrawGrab(); // Bắn tiếp lên cho cha xử lý
        }
    }

    // Hàm này cũng thế
    public void OnWeaponSheathPlace()
    {
        if (mainPlayerScript != null)
        {
            mainPlayerScript.OnWeaponSheathPlace(); // Bắn tiếp lên cho cha xử lý
        }
    }

    // Forward sự kiện kết thúc nhào lộn
    public void OnRollEnd()
    {
        if (mainPlayerScript != null)
        {
            mainPlayerScript.OnRollEnd();
        }
    }

    // Forward sự kiện rút tên lên tay (Animation Event)
    public void OnDrawArrow()
    {
        if (mainPlayerScript != null)
        {
            mainPlayerScript.OnDrawArrow();
        }
    }

    public void OnShootRSkill()
    {
        if (mainPlayerScript != null)
        {
            mainPlayerScript.OnShootRSkill();
        }
    }

    public void OnPickItemEvent()
    {
        if (mainPlayerScript != null)
        {
            mainPlayerScript.OnPickItemEvent();
        }
    }

    public void OnDeathAnimationEnd()
    {
        if (mainPlayerScript != null) mainPlayerScript.OnDeathAnimationEnd();
    }

    public void OnStandUpFinished()
    {
        PlayerKickedStun stun = GetComponentInParent<PlayerKickedStun>();
        if (stun == null) stun = GetComponentInChildren<PlayerKickedStun>(true);
        if (stun != null) stun.OnStandUpFinished();
    }

    public void EnableLeftHitbox() { if (mainPlayerScript != null) mainPlayerScript.EnableLeftHitbox(); }
    public void DisableLeftHitbox() { if (mainPlayerScript != null) mainPlayerScript.DisableLeftHitbox(); }
    public void EnableRightHitbox() { if (mainPlayerScript != null) mainPlayerScript.EnableRightHitbox(); }
    public void DisableRightHitbox() { if (mainPlayerScript != null) mainPlayerScript.DisableRightHitbox(); }
    public void EnableBothHitboxes() { if (mainPlayerScript != null) mainPlayerScript.EnableBothHitboxes(); }
    public void DisableBothHitboxes() { if (mainPlayerScript != null) mainPlayerScript.DisableBothHitboxes(); }

    public void EnableLeftPunch() { if (mainPlayerScript != null) mainPlayerScript.EnableLeftHitbox(); }
    public void DisableLeftPunch() { if (mainPlayerScript != null) mainPlayerScript.DisableLeftHitbox(); }
    public void EnableRightPunch() { if (mainPlayerScript != null) mainPlayerScript.EnableRightHitbox(); }
    public void DisableRightPunch() { if (mainPlayerScript != null) mainPlayerScript.DisableRightHitbox(); }
    public void EnableComboPunch() { if (mainPlayerScript != null) mainPlayerScript.EnableBothHitboxes(); }
    public void DisableComboPunch() { if (mainPlayerScript != null) mainPlayerScript.DisableBothHitboxes(); }

    public void EnableSingleSlash() { if (mainPlayerScript != null) mainPlayerScript.EnableLeftWeaponHitbox(); }
    public void DisableSingleSlash() { if (mainPlayerScript != null) mainPlayerScript.DisableLeftWeaponHitbox(); }
    public void EnableDoubleSlash() { if (mainPlayerScript != null) mainPlayerScript.EnableBothWeaponHitboxes(); }
    public void DisableDoubleSlash() { if (mainPlayerScript != null) mainPlayerScript.DisableBothWeaponHitboxes(); }

    public void OnSlashEnd() { if (mainPlayerScript != null) mainPlayerScript.DisableBothWeaponHitboxes(); }
    public void OnPunchEnd() { if (mainPlayerScript != null) mainPlayerScript.DisableBothHitboxes(); }
    public void Onpunchend() { if (mainPlayerScript != null) mainPlayerScript.DisableBothHitboxes(); }
    public void OnAttackEnd() { if (mainPlayerScript != null) { mainPlayerScript.DisableBothHitboxes(); mainPlayerScript.DisableBothWeaponHitboxes(); } }

    public void EnableBothWeaponHitboxes() { if (mainPlayerScript != null) mainPlayerScript.EnableBothWeaponHitboxes(); }
    public void DisableBothWeaponHitboxes() { if (mainPlayerScript != null) mainPlayerScript.DisableBothWeaponHitboxes(); }
    public void EnableLeftWeaponHitbox() { if (mainPlayerScript != null) mainPlayerScript.EnableLeftWeaponHitbox(); }
    public void DisableLeftWeaponHitbox() { if (mainPlayerScript != null) mainPlayerScript.DisableLeftWeaponHitbox(); }
    public void EnableRightWeaponHitbox() { if (mainPlayerScript != null) mainPlayerScript.EnableRightWeaponHitbox(); }
    public void DisableRightWeaponHitbox() { if (mainPlayerScript != null) mainPlayerScript.DisableRightWeaponHitbox(); }
}