using UnityEngine;

/// <summary>
/// Helper script attached to the GameObject with the Animator component.
/// Provides public methods for Animation Events that forward callbacks to the LeoPlayer script.
/// Allows you to manually add and select events in the Unity Editor dropdown.
/// </summary>
public class RollAnimationEventHelper : MonoBehaviour
{
    private LeoPlayer player;

    private void Start()
    {
        EnsurePlayerReference();
    }

    private void EnsurePlayerReference()
    {
        if (player == null)
        {
            player = GetComponentInParent<LeoPlayer>();
            if (player == null)
            {
                player = GetComponentInChildren<LeoPlayer>(true);
            }
            if (player == null)
            {
                Debug.LogError("[RollAnimationEventHelper] LỖI: Không tìm thấy component LeoPlayer ở các Object cha hoặc con!");
            }
        }
    }

    // ------------------------------------------------------------------
    //  Public Animation Event Receivers (Selectable in Unity Editor dropdown)
    // ------------------------------------------------------------------
    
    /// <summary>
    /// Kích hoạt hiệu ứng particle chém kiếm từ Animation Event.
    /// comboStepIndex: 1 = Slash1, 2 = Slash2, 3 = Slash3
    /// </summary>
    public void TriggerSlashParticle(int comboStepIndex)
    {
        EnsurePlayerReference();
        if (player != null)
        {
            player.TriggerSlashParticle(comboStepIndex);
        }
    }

    /// <summary>
    /// Event receiver for end of roll animation.
    /// </summary>
    public void OnRollEnd()
    {
        EnsurePlayerReference();
        if (player != null)
        {
            player.OnRollEnd();
            Debug.Log("[RollAnimationEventHelper] Forwarded OnRollEnd callback to LeoPlayer.");
        }
    }

    /// <summary>
    /// Event receiver to enable the Left hand/weapon hitbox.
    /// </summary>
    public void EnableLeftHitbox() {}
    public void DisableLeftHitbox() {}
    public void EnableRightHitbox() {}
    public void DisableRightHitbox() {}
    public void EnableBothHitboxes() {}
    public void DisableBothHitboxes() {}

    // ------------------------------------------------------------------
    //  Punch-specific alias event functions for intuitive selection
    // ------------------------------------------------------------------

    public void EnableLeftPunch() {}
    public void DisableLeftPunch() {}
    public void EnableRightPunch() {}
    public void DisableRightPunch() {}
    public void EnableComboPunch() {}
    public void DisableComboPunch() {}

    // ------------------------------------------------------------------
    //  Slash-specific alias event functions for intuitive selection
    // ------------------------------------------------------------------

    public void EnableSingleSlash() {}
    public void DisableSingleSlash() {}
    public void EnableDoubleSlash() {}
    public void DisableDoubleSlash() {}

    public void OnSlashEnd() {}

    /// <summary>
    /// Event receiver to unlock player movement after a punch attack finishes.
    /// </summary>
    public void UnlockMovement()
    {
        EnsurePlayerReference();
        if (player != null)
        {
            player.UnlockMovement();
        }
    }

    /// <summary>
    /// Event receiver được gọi khi nhân vật cúi xuống nhặt đồ.
    /// </summary>
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
        if (player != null) player.DrawLeftSword();
    }

    public void DrawRightSword()
    {
        EnsurePlayerReference();
        if (player != null) player.DrawRightSword();
    }

    public void SheatheLeftSword()
    {
        EnsurePlayerReference();
        if (player != null) player.SheatheLeftSword();
    }

    public void SheatheRightSword()
    {
        EnsurePlayerReference();
        if (player != null) player.SheatheRightSword();
    }

    public void OnWeaponSwitchEnd()
    {
        EnsurePlayerReference();
        if (player != null) player.OnWeaponSwitchEnd();
    }

    public void OnDrawLeftEnd()
    {
        EnsurePlayerReference();
        if (player != null) player.OnDrawLeftEnd();
    }

    public void OnSheatheLeftEnd()
    {
        EnsurePlayerReference();
        if (player != null) player.OnSheatheLeftEnd();
    }

    // ------------------------------------------------------------------
    //  Punch End Events - Gọi ở FRAME CUỐI của animation đấm
    // ------------------------------------------------------------------

    /// <summary>
    /// Gọi ở frame CUỐI animation Punch1/Punch2/Punch3.
    /// Tắt tất cả hitbox tay và signal kết thúc nhịp đấm.
    /// </summary>
    public void OnPunchEnd() {}

    // ------------------------------------------------------------------
    //  Slash End Events - Gọi ở FRAME CUỐI của animation chém
    // ------------------------------------------------------------------

    /// <summary>
    /// Gọi ở frame CUỐI của animation Slash1/Slash2/Slash3.
    /// Tắt tất cả hitbox kiếm.
    /// (OnSlashEnd() đã có sẵn bên trên)
    /// </summary>

    // ------------------------------------------------------------------
    //  General Attack End - Dùng chung cho cả đấm lẫn chém
    // ------------------------------------------------------------------

    /// <summary>
    /// Gọi ở frame CUỐI của bất kỳ animation tấn công nào.
    /// Tắt TẤT CẢ hitbox (tay + kiếm).
    /// </summary>
    public void OnAttackEnd() {}

    // ------------------------------------------------------------------
    //  Weapon Hitbox Aliases (cả 2 kiếm cùng lúc)
    // ------------------------------------------------------------------

    /// <summary>Bật hitbox CẢ 2 kiếm - dùng cho Slash bình thường.</summary>
    public void EnableBothWeaponHitboxes() {}
    public void DisableBothWeaponHitboxes() {}
    public void EnableLeftWeaponHitbox() {}
    public void DisableLeftWeaponHitbox() {}
    public void EnableRightWeaponHitbox() {}
    public void DisableRightWeaponHitbox() {}

    // --- VFX Event Forwarders ---
    public void PlayLeftSlashVFX()
    {
        EnsurePlayerReference();
        if (player != null) player.PlayLeftSlashVFX();
    }

    public void PlayRightSlashVFX()
    {
        EnsurePlayerReference();
        if (player != null) player.PlayRightSlashVFX();
    }

    public void PlayDualSlashVFX()
    {
        EnsurePlayerReference();
        if (player != null) player.PlayDualSlashVFX();
    }

    public void PlayDualSlash1VFX()
    {
        EnsurePlayerReference();
        if (player != null) player.PlayDualSlash1VFX();
    }

    public void PlayDualSlash2VFX()
    {
        EnsurePlayerReference();
        if (player != null) player.PlayDualSlash2VFX();
    }

    public void OnShootRSkill()
    {
        EnsurePlayerReference();
        if (player != null) player.OnShootRSkill();
    }
}
