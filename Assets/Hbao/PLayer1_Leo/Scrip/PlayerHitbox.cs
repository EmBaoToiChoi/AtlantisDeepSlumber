using UnityEngine;

/// <summary>
/// Helper script to be attached to the Left and Right hitbox GameObjects of the player.
/// Detects collisions with enemies and reports them to the LeoPlayer script in parent.
/// </summary>
[RequireComponent(typeof(Collider))]
public class PlayerHitbox : MonoBehaviour
{
    private LeoPlayer player;
    private Collider hitboxCollider;

    private void Start()
    {
        player = GetComponentInParent<LeoPlayer>();
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
        if (player != null && hitboxCollider != null && hitboxCollider.enabled)
        {
            player.OnHitboxCollision(other);
        }
    }
}
