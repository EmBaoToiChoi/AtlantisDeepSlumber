using UnityEngine;

/// <summary>
/// Helper script attached to the GameObject with the Animator component on Player 4 (Arthur).
/// Provides public methods for Animation Events that forward callbacks to the ArthurPlayer script.
/// Allows you to manually add and select events in the Unity Editor dropdown.
/// </summary>
public class ArthurAnimationEventHelper : MonoBehaviour
{
    private ArthurPlayer player;

    private void Start()
    {
        EnsurePlayerReference();
    }

    private void EnsurePlayerReference()
    {
        if (player == null)
        {
            player = GetComponentInParent<ArthurPlayer>();
            if (player == null)
            {
                player = GetComponentInChildren<ArthurPlayer>(true);
            }
            if (player == null)
            {
                player = transform.root.GetComponentInChildren<ArthurPlayer>(true);
            }
            if (player == null)
            {
                Debug.LogError("[ArthurAnimationEventHelper] LỖI: Không tìm thấy component ArthurPlayer ở các Object cha hoặc con!");
            }
            else
            {
                Debug.Log($"[ArthurAnimationEventHelper] Đã tìm thấy ArthurPlayer thành công: {player.name}");
            }
        }
    }

    // ==================================================================
    //  Skill R Event Receiver (MỚI TÍNH HỢP)
    // ==================================================================

    /// <summary>
    /// Được gọi từ Animation Event tại thời điểm hoạt ảnh gồng chiêu R hoàn tất.
    /// </summary>
    public void OnRSkillWeaponGlow()
    {
        EnsurePlayerReference();
        if (player != null)
        {
            player.OnRSkillWeaponGlow();
            Debug.Log("[ArthurAnimationEventHelper] Đã chuyển tiếp sự kiện OnRSkillWeaponGlow tới ArthurPlayer.");
        }
    }

    // ==================================================================
    //  Skill E Event Receiver (BẤT TỬ)
    // ==================================================================

    /// <summary>
    /// Được gọi từ Animation Event tại thời điểm hoạt ảnh gồng chiêu E hoàn tất.
    /// </summary>
    public void OnSkillEAnimEnd()
    {
        EnsurePlayerReference();
        if (player != null)
        {
            player.OnSkillEAnimEnd();
            Debug.Log("[ArthurAnimationEventHelper] Đã chuyển tiếp sự kiện OnSkillEAnimEnd tới ArthurPlayer.");
        }
    }

    // ==================================================================
    //  Skill Q Event Receiver (DẶM KHIÊN)
    // ==================================================================

    /// <summary>
    /// Được gọi từ Animation Event tại thời điểm hoạt ảnh dặm khiên (Skill Q) chạm đất.
    /// </summary>
    public void OnSkillQShieldSlam()
    {
        EnsurePlayerReference();
        if (player != null)
        {
            player.OnSkillQShieldSlam();
            Debug.Log("[ArthurAnimationEventHelper] Đã chuyển tiếp sự kiện OnSkillQShieldSlam tới ArthurPlayer.");
        }
    }

    // ------------------------------------------------------------------
    //  Public Animation Event Receivers (Selectable in Unity Editor dropdown)
    // ------------------------------------------------------------------

    public void OnRollEnd()
    {
        EnsurePlayerReference();
        if (player != null)
        {
            player.OnRollEnd();
            Debug.Log("[ArthurAnimationEventHelper] Forwarded OnRollEnd callback to ArthurPlayer.");
        }
    }

    public void EnableLeftHitbox() { EnsurePlayerReference(); if (player != null) player.EnableLeftHitbox(); }
    public void DisableLeftHitbox() { EnsurePlayerReference(); if (player != null) player.DisableLeftHitbox(); }
    public void EnableRightHitbox() { EnsurePlayerReference(); if (player != null) player.EnableRightHitbox(); }
    public void DisableRightHitbox() { EnsurePlayerReference(); if (player != null) player.DisableRightHitbox(); }
    public void EnableBothHitboxes() { EnsurePlayerReference(); if (player != null) player.EnableBothHitboxes(); }
    public void DisableBothHitboxes() { EnsurePlayerReference(); if (player != null) player.DisableBothHitboxes(); }

    // ------------------------------------------------------------------
    //  Punch-specific alias event functions for intuitive selection
    // ------------------------------------------------------------------

    public void EnableLeftPunch() { EnsurePlayerReference(); if (player != null) player.EnableLeftHitbox(); }
    public void DisableLeftPunch() { EnsurePlayerReference(); if (player != null) player.DisableLeftHitbox(); }
    public void EnableRightPunch() { EnsurePlayerReference(); if (player != null) player.EnableRightHitbox(); }
    public void DisableRightPunch() { EnsurePlayerReference(); if (player != null) player.DisableRightHitbox(); }
    public void EnableComboPunch() { EnsurePlayerReference(); if (player != null) player.EnableBothHitboxes(); }
    public void DisableComboPunch() { EnsurePlayerReference(); if (player != null) player.DisableBothHitboxes(); }

    // ------------------------------------------------------------------
    //  Slash-specific alias event functions for intuitive selection
    // ------------------------------------------------------------------

    public void EnableSingleSlash() { EnsurePlayerReference(); if (player != null) player.EnableLeftWeaponHitbox(); }
    public void DisableSingleSlash() { EnsurePlayerReference(); if (player != null) player.DisableLeftWeaponHitbox(); }
    public void EnableDoubleSlash() { EnsurePlayerReference(); if (player != null) player.EnableBothWeaponHitboxes(); }
    public void DisableDoubleSlash() { EnsurePlayerReference(); if (player != null) player.DisableBothWeaponHitboxes(); }

    public void OnSlashEnd() { EnsurePlayerReference(); if (player != null) player.DisableBothWeaponHitboxes(); }
    public void OnPunchEnd() { EnsurePlayerReference(); if (player != null) player.DisableBothHitboxes(); }
    public void Onpunchend() { EnsurePlayerReference(); if (player != null) player.DisableBothHitboxes(); }
    public void OnAttackEnd() { EnsurePlayerReference(); if (player != null) { player.DisableBothHitboxes(); player.DisableBothWeaponHitboxes(); } }

    public void OnPickItemEvent()
    {
        EnsurePlayerReference();
        if (player != null)
        {
            player.OnPickItemEvent();
        }
    }

    public void DrawLeftSword()
    {
        EnsurePlayerReference();
        if (player != null)
        {
            player.DrawLeftSword();
        }
    }

    public void DrawRightSword()
    {
        EnsurePlayerReference();
        if (player != null)
        {
            player.DrawRightSword();
        }
    }

    public void SheatheLeftSword()
    {
        EnsurePlayerReference();
        if (player != null)
        {
            player.SheatheLeftSword();
        }
    }

    public void SheatheRightSword()
    {
        EnsurePlayerReference();
        if (player != null)
        {
            player.SheatheRightSword();
        }
    }

    public void OnDrawLeftEnd()
    {
        EnsurePlayerReference();
        if (player != null)
        {
            player.OnDrawLeftEnd();
        }
    }

    public void OnSheatheLeftEnd()
    {
        EnsurePlayerReference();
        if (player != null)
        {
            player.OnSheatheLeftEnd();
        }
    }

    public void OnWeaponSwitchEnd()
    {
        EnsurePlayerReference();
        if (player != null)
        {
            player.OnWeaponSwitchEnd();
        }
    }

    public void OnDeathAnimationEnd()
    {
        EnsurePlayerReference();
        if (player != null) player.OnDeathAnimationEnd();
    }

    public void OnStandUpFinished()
    {
        PlayerKickedStun stun = GetComponentInParent<PlayerKickedStun>();
        if (stun == null) stun = GetComponentInChildren<PlayerKickedStun>(true);
        if (stun != null) stun.OnStandUpFinished();
    }
}