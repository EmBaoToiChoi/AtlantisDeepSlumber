using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Script gây sát thương va chạm khi người chơi chạm vào bất kỳ phần nào của đá triệu hồi (Earth Blast).
/// Trừ 5 HP và có cooldown để tránh gây chết người chơi tức thì.
/// </summary>
public class EarthBlastDamageZone : MonoBehaviour
{
    public float damage = 5f;
    public float damageCooldown = 1.0f;
    private Dictionary<Transform, float> lastDamageTimes = new Dictionary<Transform, float>();

    private void OnTriggerEnter(Collider other)
    {
        ProcessDamage(other.gameObject);
    }

    private void OnTriggerStay(Collider other)
    {
        ProcessDamage(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        ProcessDamage(collision.gameObject);
    }

    private void OnCollisionStay(Collision collision)
    {
        ProcessDamage(collision.gameObject);
    }

    private void ProcessDamage(GameObject target)
    {
        if (target == null) return;

        Transform playerRoot = GetPlayerRoot(target.transform);
        if (playerRoot != null)
        {
            if (!lastDamageTimes.TryGetValue(playerRoot, out float lastTime) || Time.time - lastTime >= damageCooldown)
            {
                lastDamageTimes[playerRoot] = Time.time;
                EnemyDamageHelper.DealDamage(playerRoot, damage, Vector3.zero);
                Debug.Log($"[EarthBlastDamageZone] Player {playerRoot.name} chạm vào khối đá triệu hồi -> Trừ {damage} HP!");
            }
        }
    }

    private Transform GetPlayerRoot(Transform t)
    {
        if (t == null) return null;

        Transform root = t.root;
        if (root.CompareTag("Player") || t.CompareTag("Player")) return root;

        var leo = t.GetComponentInParent<LeoPlayer>();
        if (leo != null) return leo.transform;

        var arthur = t.GetComponentInParent<ArthurPlayer>();
        if (arthur != null) return arthur.transform;

        var elena = t.GetComponentInParent<ElenaPlayer>() ?? (MonoBehaviour)t.GetComponentInParent<ElenaArcher>();
        if (elena != null) return elena.transform;

        var maya = t.GetComponentInParent<MayaPlayer>() ?? (MonoBehaviour)t.GetComponentInParent<MayaSupport>();
        if (maya != null) return maya.transform;

        var cc = t.GetComponentInParent<CharacterController>();
        if (cc != null) return cc.transform;

        var p = t.GetComponentInParent<IPlayerHUDTarget>() ?? t.GetComponentInChildren<IPlayerHUDTarget>();
        if (p != null && p is MonoBehaviour mono) return mono.transform;

        return null;
    }

    /// <summary>
    /// Hàm tiện ích tự động gắn Kinematic Rigidbody ở Root và đồng bộ hóa các Collider có sẵn trong Prefab.
    /// Không tự động sinh Collider mới để đảm bảo kích hoạt 100% theo thiết kế thủ công của Prefab.
    /// </summary>
    public static void SetupRockColliders(GameObject blastInstance, float earthBlastRadius, float earthBlastScale, float damageAmount = 5f)
    {
        if (blastInstance == null) return;

        // 1. Gắn Rigidbody Kinematic ở Root để Unity Physics luôn phát tín hiệu Trigger 100%
        var rb = blastInstance.GetComponent<Rigidbody>();
        if (rb == null) rb = blastInstance.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // 2. Gắn EarthBlastDamageZone vào Root (Sát thương 5 HP)
        var rootDmg = blastInstance.GetComponent<EarthBlastDamageZone>();
        if (rootDmg == null) rootDmg = blastInstance.AddComponent<EarthBlastDamageZone>();
        rootDmg.damage = damageAmount;

        // 3. Tìm tất cả các Collider đã được thiết kế sẵn trong Prefab, bật isTrigger và gắn EarthBlastDamageZone
        Collider[] existingColliders = blastInstance.GetComponentsInChildren<Collider>(true);
        foreach (var col in existingColliders)
        {
            if (col != null)
            {
                // Bật isTrigger theo yêu cầu
                col.isTrigger = true;

                // Gắn thêm component gây sát thương cho từng Collider có sẵn
                var childDmg = col.gameObject.GetComponent<EarthBlastDamageZone>();
                if (childDmg == null) childDmg = col.gameObject.AddComponent<EarthBlastDamageZone>();
                childDmg.damage = damageAmount;
            }
        }
    }
}
