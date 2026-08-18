using UnityEngine;

public static class EnemyDamageHelper
{
    public static void DealDamage(Transform targetTransform, float damage, Vector3 knockbackForce)
    {
        if (targetTransform == null || damage <= 0f) return;

        // 1. Kiểm tra đối tượng là Player (hỗ trợ toàn bộ class: Leo, Arthur, Elena, Maya, SimplePlayerTest, Skeleton)
        var leo = targetTransform.GetComponentInParent<LeoPlayer>() ?? targetTransform.GetComponentInChildren<LeoPlayer>() ?? targetTransform.GetComponent<LeoPlayer>();
        if (leo != null)
        {
            leo.RequestTakeDamage(damage);
            leo.TakeDamage(damage);
            leo.ApplyKnockback(knockbackForce);
            Debug.Log($"[EnemyDamageHelper] Đã gây {damage} sát thương cho LeoPlayer '{leo.name}'");
            return;
        }

        var arthur = targetTransform.GetComponentInParent<ArthurPlayer>() ?? targetTransform.GetComponentInChildren<ArthurPlayer>() ?? targetTransform.GetComponent<ArthurPlayer>();
        if (arthur != null)
        {
            arthur.RequestTakeDamage(damage);
            arthur.TakeDamage(damage);
            arthur.ApplyKnockback(knockbackForce);
            Debug.Log($"[EnemyDamageHelper] Đã gây {damage} sát thương cho ArthurPlayer '{arthur.name}'");
            return;
        }

        var elena = targetTransform.GetComponentInParent<ElenaPlayer>() ?? targetTransform.GetComponentInChildren<ElenaPlayer>() ?? targetTransform.GetComponent<ElenaPlayer>();
        if (elena != null)
        {
            elena.RequestTakeDamage(damage);
            elena.TakeDamage(damage);
            elena.ApplyKnockback(knockbackForce);
            Debug.Log($"[EnemyDamageHelper] Đã gây {damage} sát thương cho ElenaPlayer '{elena.name}'");
            return;
        }

        var maya = targetTransform.GetComponentInParent<MayaPlayer>() ?? targetTransform.GetComponentInChildren<MayaPlayer>() ?? targetTransform.GetComponent<MayaPlayer>();
        if (maya != null)
        {
            maya.RequestTakeDamage(damage);
            maya.TakeDamage(damage);
            maya.ApplyKnockback(knockbackForce);
            Debug.Log($"[EnemyDamageHelper] Đã gây {damage} sát thương cho MayaPlayer '{maya.name}'");
            return;
        }

        var simple = targetTransform.GetComponentInParent<SimplePlayerTest>() ?? targetTransform.GetComponentInChildren<SimplePlayerTest>() ?? targetTransform.GetComponent<SimplePlayerTest>();
        if (simple != null)
        {
            simple.RequestTakeDamage(damage);
            simple.TakeDamage(damage);
            simple.ApplyKnockback(knockbackForce);
            Debug.Log($"[EnemyDamageHelper] Đã gây {damage} sát thương cho SimplePlayerTest '{simple.name}'");
            return;
        }

        var skeleton = targetTransform.GetComponentInParent<Skeleton>() ?? targetTransform.GetComponentInChildren<Skeleton>() ?? targetTransform.GetComponent<Skeleton>();
        if (skeleton != null)
        {
            skeleton.TakeDamage(damage);
            skeleton.ApplyKnockback(knockbackForce);
            Debug.Log($"[EnemyDamageHelper] Đã gây {damage} sát thương cho Skeleton '{skeleton.name}'");
            return;
        }

        // 2. Fallback trực tiếp qua interface IPlayerHUDTarget
        var hudTarget = targetTransform.GetComponentInParent<IPlayerHUDTarget>() ?? targetTransform.GetComponentInChildren<IPlayerHUDTarget>() ?? targetTransform.GetComponent<IPlayerHUDTarget>();
        if (hudTarget != null && hudTarget is MonoBehaviour mono)
        {
            mono.SendMessage("RequestTakeDamage", damage, SendMessageOptions.DontRequireReceiver);
            mono.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
            return;
        }

        // 2. Dự phòng an toàn: Kiểm tra đối tượng là Enemy/Boss (MiniBossAI, FinalBossAI, BossAI, etc.)
        var mb = targetTransform.GetComponentInParent<MiniBossAI>() ?? targetTransform.GetComponentInChildren<MiniBossAI>();
        if (mb != null)
        {
            mb.TakeDamage(damage);
            return;
        }

        var fb = targetTransform.GetComponentInParent<FinalBossAI>() ?? targetTransform.GetComponentInChildren<FinalBossAI>();
        if (fb != null)
        {
            fb.TakeDamage(damage);
            return;
        }

        var b = targetTransform.GetComponentInParent<BossAI>() ?? targetTransform.GetComponentInChildren<BossAI>();
        if (b != null)
        {
            b.TakeDamage(damage);
            return;
        }

        var e1 = targetTransform.GetComponentInParent<Enemy1_DapBua>() ?? targetTransform.GetComponentInChildren<Enemy1_DapBua>();
        if (e1 != null) { e1.TakeDamage(damage); return; }

        var e2 = targetTransform.GetComponentInParent<Enemy2_Zombie>() ?? targetTransform.GetComponentInChildren<Enemy2_Zombie>();
        if (e2 != null) { e2.TakeDamage(damage); return; }

        var e3 = targetTransform.GetComponentInParent<Enemy3_Buaa>() ?? targetTransform.GetComponentInChildren<Enemy3_Buaa>();
        if (e3 != null) { e3.TakeDamage(damage); return; }

        var e4 = targetTransform.GetComponentInParent<Enemy4_Bongtoi>() ?? targetTransform.GetComponentInChildren<Enemy4_Bongtoi>();
        if (e4 != null) { e4.TakeDamage(damage); return; }

        var e5 = targetTransform.GetComponentInParent<Enemy5_PhuThuy>() ?? targetTransform.GetComponentInChildren<Enemy5_PhuThuy>();
        if (e5 != null) { e5.TakeDamage(damage); return; }
    }

    public static void DealKickDamageWithStun(Transform playerTransform, float damage, Vector3 knockbackForce, float stunDuration)
    {
        if (playerTransform == null) return;

        // Trừ máu trước
        DealDamage(playerTransform, damage, Vector3.zero);

        // Áp dụng hiệu ứng ngã / khóa di chuyển
        var stun = playerTransform.GetComponentInParent<PlayerKickedStun>() ?? playerTransform.GetComponentInChildren<PlayerKickedStun>();
        if (stun != null)
        {
            stun.ApplyKickedStun(stunDuration, knockbackForce);
        }
        else
        {
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
