using UnityEngine;

public static class EnemyDamageHelper
{
    public static void DealDamage(Transform playerTransform, float damage, Vector3 knockbackForce)
    {
        if (playerTransform == null) return;

        var simple = playerTransform.GetComponentInParent<SimplePlayerTest>();
        if (simple != null)
        {
            simple.TakeDamage(damage);
            simple.ApplyKnockback(knockbackForce);
            return;
        }

        var leo = playerTransform.GetComponentInParent<LeoPlayer>();
        if (leo != null)
        {
            leo.TakeDamage(damage);
            leo.ApplyKnockback(knockbackForce);
            return;
        }

        var elena = playerTransform.GetComponentInParent<ElenaPlayer>();
        if (elena != null)
        {
            elena.TakeDamage(damage);
            elena.ApplyKnockback(knockbackForce);
            return;
        }

        var maya = playerTransform.GetComponentInParent<MayaPlayer>();
        if (maya != null)
        {
            maya.TakeDamage(damage);
            maya.ApplyKnockback(knockbackForce);
            return;
        }
    }
}
