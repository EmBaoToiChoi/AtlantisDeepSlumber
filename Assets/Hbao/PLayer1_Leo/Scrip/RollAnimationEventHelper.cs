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
        }
    }

    // ------------------------------------------------------------------
    //  Public Animation Event Receivers (Selectable in Unity Editor dropdown)
    // ------------------------------------------------------------------
    
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
}
