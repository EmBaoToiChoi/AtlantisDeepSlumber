using UnityEngine;

public static class EnemyDamageHelper
{
    public static void DealDamage(Transform playerTransform, float damage, Vector3 knockbackForce)
    {
        if (playerTransform == null) return;

        var skeleton = playerTransform.GetComponentInParent<Skeleton>();
        if (skeleton != null)
        {
            skeleton.TakeDamage(damage);
            skeleton.ApplyKnockback(knockbackForce);
            return;
        }

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

        var arthur = playerTransform.GetComponentInParent<ArthurPlayer>();
        if (arthur != null)
        {
            arthur.TakeDamage(damage);
            arthur.ApplyKnockback(knockbackForce);
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

    public static void DealKickDamageWithStun(Transform playerTransform, float damage, Vector3 knockbackForce, float stunDuration)
    {
        if (playerTransform == null) return;

        // Trừ máu trước (không áp dụng đẩy lùi qua DealDamage thường để tránh đẩy lực 2 lần)
        DealDamage(playerTransform, damage, Vector3.zero);

        // Áp dụng hiệu ứng ngã / khóa di chuyển
        var stun = playerTransform.GetComponentInParent<PlayerKickedStun>() ?? playerTransform.GetComponentInChildren<PlayerKickedStun>();
        if (stun != null)
        {
            stun.ApplyKickedStun(stunDuration, knockbackForce);
        }
        else
        {
            // Dự phòng (fallback) nếu chưa gắn script PlayerKickedStun: chỉ đẩy lùi thông thường
            var skeleton = playerTransform.GetComponentInParent<Skeleton>();
            if (skeleton != null) { skeleton.ApplyKnockback(knockbackForce); return; }
            var simple = playerTransform.GetComponentInParent<SimplePlayerTest>();
            if (simple != null) { simple.ApplyKnockback(knockbackForce); return; }
            var leo = playerTransform.GetComponentInParent<LeoPlayer>();
            if (leo != null) { leo.ApplyKnockback(knockbackForce); return; }
            var arthur = playerTransform.GetComponentInParent<ArthurPlayer>();
            if (arthur != null) { arthur.ApplyKnockback(knockbackForce); return; }
            var elena = playerTransform.GetComponentInParent<ElenaPlayer>();
            if (elena != null) { elena.ApplyKnockback(knockbackForce); return; }
            var maya = playerTransform.GetComponentInParent<MayaPlayer>();
            if (maya != null) { maya.ApplyKnockback(knockbackForce); return; }
        }
    }
}
