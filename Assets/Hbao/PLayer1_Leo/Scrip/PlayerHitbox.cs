using UnityEngine;

/// <summary>
/// Helper script to be attached to the Left and Right hitbox GameObjects of the player.
/// Detects collisions with enemies and reports them to the LeoPlayer or ArthurPlayer script in parent.
/// </summary>
[RequireComponent(typeof(Collider))]
public class PlayerHitbox : MonoBehaviour
{
    private LeoPlayer leoPlayer;
    private ArthurPlayer arthurPlayer;
    private Collider hitboxCollider;

    private void Start()
    {
        leoPlayer = GetComponentInParent<LeoPlayer>();
        arthurPlayer = GetComponentInParent<ArthurPlayer>();
        hitboxCollider = GetComponent<Collider>();
        
        // Ensure the collider is configured as a trigger
        if (hitboxCollider != null)
        {
            hitboxCollider.isTrigger = true;
            hitboxCollider.enabled = false; // Start disabled, will be enabled by animation events
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hitboxCollider != null && hitboxCollider.enabled)
        {
            if (leoPlayer != null)
            {
                leoPlayer.OnHitboxCollision(other);
            }
            else if (arthurPlayer != null)
            {
                arthurPlayer.OnHitboxCollision(other);
            }
        }
    }
}
