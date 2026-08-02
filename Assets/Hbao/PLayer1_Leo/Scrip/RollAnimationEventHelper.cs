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
                player = transform.root.GetComponentInChildren<LeoPlayer>(true);
            }
            if (player == null)
            {
                Debug.LogError("[RollAnimationEventHelper] LỖI: Không tìm thấy component LeoPlayer ở các Object cha hoặc con!");
            }
            else
            {
                Debug.Log($"[RollAnimationEventHelper] Đã tìm thấy LeoPlayer thành công: {player.name}");
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
    public void EnableLeftHitbox() { 
        EnsurePlayerReference(); 
        Debug.Log("[RollAnimationEventHelper] Animation Event: EnableLeftHitbox");
        if (player != null) player.EnableLeftHitbox(); 
    }
    public void DisableLeftHitbox() { 
        EnsurePlayerReference(); 
        Debug.Log("[RollAnimationEventHelper] Animation Event: DisableLeftHitbox");
        if (player != null) player.DisableLeftHitbox(); 
    }
    public void EnableRightHitbox() { 
        EnsurePlayerReference(); 
        Debug.Log("[RollAnimationEventHelper] Animation Event: EnableRightHitbox");
        if (player != null) player.EnableRightHitbox(); 
    }
    public void DisableRightHitbox() { 
        EnsurePlayerReference(); 
        Debug.Log("[RollAnimationEventHelper] Animation Event: DisableRightHitbox");
        if (player != null) player.DisableRightHitbox(); 
    }
    public void EnableBothHitboxes() { 
        EnsurePlayerReference(); 
        Debug.Log("[RollAnimationEventHelper] Animation Event: EnableBothHitboxes");
        if (player != null) player.EnableBothHitboxes(); 
    }
    public void DisableBothHitboxes() { 
        EnsurePlayerReference(); 
        Debug.Log("[RollAnimationEventHelper] Animation Event: DisableBothHitboxes");
        if (player != null) player.DisableBothHitboxes(); 
    }

    // ------------------------------------------------------------------
    //  Punch-specific alias event functions for intuitive selection
    // ------------------------------------------------------------------

    public void EnableLeftPunch() { 
        EnsurePlayerReference(); 
        Debug.Log("[RollAnimationEventHelper] Animation Event: EnableLeftPunch");
        if (player != null) player.EnableLeftHitbox(); 
    }
    public void DisableLeftPunch() { 
        EnsurePlayerReference(); 
        Debug.Log("[RollAnimationEventHelper] Animation Event: DisableLeftPunch");
        if (player != null) player.DisableLeftHitbox(); 
    }
    public void EnableRightPunch() { 
        EnsurePlayerReference(); 
        Debug.Log("[RollAnimationEventHelper] Animation Event: EnableRightPunch");
        if (player != null) player.EnableRightHitbox(); 
    }
    public void DisableRightPunch() { 
        EnsurePlayerReference(); 
        Debug.Log("[RollAnimationEventHelper] Animation Event: DisableRightPunch");
        if (player != null) player.DisableRightHitbox(); 
    }
    public void EnableComboPunch() { 
        EnsurePlayerReference(); 
        Debug.Log("[RollAnimationEventHelper] Animation Event: EnableComboPunch");
        if (player != null) player.EnableBothHitboxes(); 
    }
    public void DisableComboPunch() { 
        EnsurePlayerReference(); 
        Debug.Log("[RollAnimationEventHelper] Animation Event: DisableComboPunch");
        if (player != null) player.DisableBothHitboxes(); 
    }

    // ------------------------------------------------------------------
    //  Slash-specific alias event functions for intuitive selection
    // ------------------------------------------------------------------

    public void EnableSingleSlash() { 
        EnsurePlayerReference(); 
        Debug.Log("[RollAnimationEventHelper] Animation Event: EnableSingleSlash");
        if (player != null) player.EnableLeftWeaponHitbox(); 
    }
    public void DisableSingleSlash() { 
        EnsurePlayerReference(); 
        Debug.Log("[RollAnimationEventHelper] Animation Event: DisableSingleSlash");
        if (player != null) player.DisableLeftWeaponHitbox(); 
    }
    public void EnableDoubleSlash() { 
        EnsurePlayerReference(); 
        Debug.Log("[RollAnimationEventHelper] Animation Event: EnableDoubleSlash");
        if (player != null) player.EnableBothWeaponHitboxes(); 
    }
    public void DisableDoubleSlash() { 
        EnsurePlayerReference(); 
        Debug.Log("[RollAnimationEventHelper] Animation Event: DisableDoubleSlash");
        if (player != null) player.DisableBothWeaponHitboxes(); 
    }

    public void OnSlashEnd() { 
        EnsurePlayerReference(); 
        Debug.Log("[RollAnimationEventHelper] Animation Event: OnSlashEnd");
        if (player != null) player.DisableBothWeaponHitboxes(); 
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
    public void OnPunchEnd() { EnsurePlayerReference(); if (player != null) player.DisableBothHitboxes(); }

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
    public void OnAttackEnd() { EnsurePlayerReference(); if (player != null) { player.DisableBothHitboxes(); player.DisableBothWeaponHitboxes(); } }

    // ------------------------------------------------------------------
    //  Weapon Hitbox Aliases (cả 2 kiếm cùng lúc)
    // ------------------------------------------------------------------

    /// <summary>Bật hitbox CẢ 2 kiếm - dùng cho Slash bình thường.</summary>
    public void EnableBothWeaponHitboxes() { EnsurePlayerReference(); if (player != null) player.EnableBothWeaponHitboxes(); }
    public void DisableBothWeaponHitboxes() { EnsurePlayerReference(); if (player != null) player.DisableBothWeaponHitboxes(); }
    public void EnableLeftWeaponHitbox() { EnsurePlayerReference(); if (player != null) player.EnableLeftWeaponHitbox(); }
    public void DisableLeftWeaponHitbox() { EnsurePlayerReference(); if (player != null) player.DisableLeftWeaponHitbox(); }
    public void EnableRightWeaponHitbox() { EnsurePlayerReference(); if (player != null) player.EnableRightWeaponHitbox(); }
    public void DisableRightWeaponHitbox() { EnsurePlayerReference(); if (player != null) player.DisableRightWeaponHitbox(); }

    // --- VFX Event Forwarders ---
    public void PlayLeftSlashVFX()
    {
        EnsurePlayerReference();
        if (player != null) { player.PlayLeftSlashVFX(); player.PerformRaycastSlashDamage(1.0f); }
    }

    public void PlayRightSlashVFX()
    {
        EnsurePlayerReference();
        if (player != null) { player.PlayRightSlashVFX(); player.PerformRaycastSlashDamage(1.0f); }
    }

    public void PlayDualSlashVFX()
    {
        EnsurePlayerReference();
        if (player != null) { player.PlayDualSlashVFX(); player.PerformRaycastSlashDamage(1.25f); }
    }

    public void PlayDualSlash1VFX()
    {
        EnsurePlayerReference();
        if (player != null) { player.PlayDualSlash1VFX(); player.PerformRaycastSlashDamage(1.2f); }
    }

    public void PlayDualSlash2VFX()
    {
        EnsurePlayerReference();
        if (player != null) { player.PlayDualSlash2VFX(); player.PerformRaycastSlashDamage(1.3f); }
    }

    public void OnShootRSkill()
    {
        EnsurePlayerReference();
        if (player != null) player.OnShootRSkill();
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
