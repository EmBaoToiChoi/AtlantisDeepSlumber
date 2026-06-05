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
    public void EnableLeftHitbox()
    {
        EnsurePlayerReference();
        if (player != null)
        {
            player.EnableLeftHitbox();
        }
    }

    /// <summary>
    /// Event receiver to disable the Left hand/weapon hitbox.
    /// </summary>
    public void DisableLeftHitbox()
    {
        EnsurePlayerReference();
        if (player != null)
        {
            player.DisableLeftHitbox();
        }
    }

    /// <summary>
    /// Event receiver to enable the Right hand/weapon hitbox.
    /// </summary>
    public void EnableRightHitbox()
    {
        EnsurePlayerReference();
        if (player != null)
        {
            player.EnableRightHitbox();
        }
    }

    /// <summary>
    /// Event receiver to disable the Right hand/weapon hitbox.
    /// </summary>
    public void DisableRightHitbox()
    {
        EnsurePlayerReference();
        if (player != null)
        {
            player.DisableRightHitbox();
        }
    }

    /// <summary>
    /// Event receiver to enable BOTH left and right hitboxes (used for Punch 3 combo).
    /// </summary>
    public void EnableBothHitboxes()
    {
        EnsurePlayerReference();
        if (player != null)
        {
            player.EnableBothHitboxes();
        }
    }

    /// <summary>
    /// Event receiver to disable BOTH left and right hitboxes (used for Punch 3 combo).
    /// </summary>
    public void DisableBothHitboxes()
    {
        EnsurePlayerReference();
        if (player != null)
        {
            player.DisableBothHitboxes();
        }
    }

    // ------------------------------------------------------------------
    //  Punch-specific alias event functions for intuitive selection
    // ------------------------------------------------------------------

    public void EnableLeftPunch()
    {
        EnableLeftHitbox();
    }

    public void DisableLeftPunch()
    {
        DisableLeftHitbox();
    }

    public void EnableRightPunch()
    {
        EnableRightHitbox();
    }

    public void DisableRightPunch()
    {
        DisableRightHitbox();
    }

    public void EnableComboPunch()
    {
        EnableBothHitboxes();
    }

    public void DisableComboPunch()
    {
        DisableBothHitboxes();
    }

    // ------------------------------------------------------------------
    //  Slash-specific alias event functions for intuitive selection
    // ------------------------------------------------------------------

    public void EnableSingleSlash()
    {
        EnsurePlayerReference();
        if (player != null)
        {
            player.EnableRightWeaponHitbox();
        }
    }

    public void DisableSingleSlash()
    {
        EnsurePlayerReference();
        if (player != null)
        {
            player.DisableRightWeaponHitbox();
        }
    }

    public void EnableDoubleSlash()
    {
        EnsurePlayerReference();
        if (player != null)
        {
            player.EnableBothWeaponHitboxes();
        }
    }

    public void DisableDoubleSlash()
    {
        EnsurePlayerReference();
        if (player != null)
        {
            player.DisableBothWeaponHitboxes();
        }
    }

    /// <summary>
    /// Event receiver to end Root Motion after a slash finishes.
    /// </summary>
    public void OnSlashEnd()
    {
        EnsurePlayerReference();
        if (player != null)
        {
            player.OnSlashEnd();
        }
    }

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
    public void OnPunchEnd()
    {
        EnsurePlayerReference();
        if (player != null)
        {
            player.OnPunchEnd();
            Debug.Log("[RollAnimationEventHelper] OnPunchEnd forwarded.");
        }
    }

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
    public void OnAttackEnd()
    {
        EnsurePlayerReference();
        if (player != null)
        {
            player.OnAttackEnd();
            Debug.Log("[RollAnimationEventHelper] OnAttackEnd forwarded.");
        }
    }

    // ------------------------------------------------------------------
    //  Weapon Hitbox Aliases (cả 2 kiếm cùng lúc)
    // ------------------------------------------------------------------

    /// <summary>Bật hitbox CẢ 2 kiếm - dùng cho Slash bình thường.</summary>
    public void EnableBothWeaponHitboxes()
    {
        EnsurePlayerReference();
        if (player != null) player.EnableBothWeaponHitboxes();
    }

    /// <summary>Tắt hitbox CẢ 2 kiếm.</summary>
    public void DisableBothWeaponHitboxes()
    {
        EnsurePlayerReference();
        if (player != null) player.DisableBothWeaponHitboxes();
    }

    /// <summary>Bật hitbox kiếm trái - dùng khi chỉ chém bằng tay trái.</summary>
    public void EnableLeftWeaponHitbox()
    {
        EnsurePlayerReference();
        if (player != null) player.EnableLeftWeaponHitbox();
    }

    /// <summary>Tắt hitbox kiếm trái.</summary>
    public void DisableLeftWeaponHitbox()
    {
        EnsurePlayerReference();
        if (player != null) player.DisableLeftWeaponHitbox();
    }

    /// <summary>Bật hitbox kiếm phải - dùng khi chỉ chém bằng tay phải.</summary>
    public void EnableRightWeaponHitbox()
    {
        EnsurePlayerReference();
        if (player != null) player.EnableRightWeaponHitbox();
    }

    /// <summary>Tắt hitbox kiếm phải.</summary>
    public void DisableRightWeaponHitbox()
    {
        EnsurePlayerReference();
        if (player != null) player.DisableRightWeaponHitbox();
    }
}
