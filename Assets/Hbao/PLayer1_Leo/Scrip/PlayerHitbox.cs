using UnityEngine;

/// <summary>
/// Helper script to be attached to Left, Right, Weapon, and Axe hitbox GameObjects.
/// Detects collisions with enemies/trees and reports them to player scripts (LeoPlayer, ArthurPlayer).
/// </summary>
[RequireComponent(typeof(Collider))]
public class PlayerHitbox : MonoBehaviour
{
    private LeoPlayer leoPlayer;
    private ArthurPlayer arthurPlayer;
    private Collider hitboxCollider;

    private void Awake()
    {
        CachePlayerReferences();
        hitboxCollider = GetComponent<Collider>();
    }

    private void CachePlayerReferences()
    {
        if (leoPlayer == null) leoPlayer = GetComponentInParent<LeoPlayer>();
        if (arthurPlayer == null) arthurPlayer = GetComponentInParent<ArthurPlayer>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;
        CachePlayerReferences();

        if (leoPlayer != null) leoPlayer.OnHitboxCollision(other);
        else if (arthurPlayer != null) arthurPlayer.OnHitboxCollision(other);
    }
}
