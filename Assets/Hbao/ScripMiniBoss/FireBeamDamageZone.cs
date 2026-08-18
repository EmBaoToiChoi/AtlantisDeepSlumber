using UnityEngine;
using System.Collections.Generic;

public class FireBeamDamageZone : MonoBehaviour
{
    private float damage = 5f;
    private float knockback = 12f;
    private float hitCooldown = 0.4f;
    private float scanRadius = 2.8f;
    private MonoBehaviour bossOwner;

    // Lưu trữ thời gian trúng đòn cuối cùng của mỗi player để giãn cách sát thương
    private Dictionary<Transform, float> lastHitTimes = new Dictionary<Transform, float>();

    private void Awake()
    {
        // Tự động gắn CapsuleCollider Trigger dọc theo cột lửa để bắt mọi va chạm vật lý
        var col = GetComponent<Collider>();
        if (col == null)
        {
            var cap = gameObject.AddComponent<CapsuleCollider>();
            cap.isTrigger = true;
            cap.radius = scanRadius;
            cap.height = 35f;
            cap.center = new Vector3(0f, 0f, 15f);
            cap.direction = 2; // Hướng dọc theo trục Z của tia
        }
    }

    public void Initialize(MonoBehaviour owner, float dmg, float kb)
    {
        bossOwner = owner;
        damage = dmg;
        knockback = kb;
        lastHitTimes.Clear();
    }

    private void Update()
    {
        ScanAndDamageNearbyPlayers();
    }

    private void ScanAndDamageNearbyPlayers()
    {
        Vector3 origin = transform.position;
        Vector3 forward = transform.forward;

        // Quét tất cả collider trong bán kính quanh chân cột lửa
        Collider[] hits = Physics.OverlapSphere(origin, scanRadius * 1.5f);
        foreach (var hit in hits)
        {
            if (hit != null) TryDealDamage(hit);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryDealDamage(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryDealDamage(other);
    }

    private void TryDealDamage(Collider other)
    {
        if (other == null) return;
        Transform root = other.transform.root;
        if (root == null || root.CompareTag("Enemy") || (bossOwner != null && root == bossOwner.transform)) return;

        // Nhận diện Player chính xác qua interface IPlayerHUDTarget hoặc tag Player
        var player = other.GetComponentInParent<IPlayerHUDTarget>() ?? other.GetComponentInChildren<IPlayerHUDTarget>() ?? other.GetComponent<IPlayerHUDTarget>();
        Transform playerRoot = player != null ? player.transform : (root.CompareTag("Player") ? root : null);

        if (playerRoot != null)
        {
            // Kiểm tra thời gian giãn cách (cooldown) để tránh trừ máu liên tục mỗi frame
            if (lastHitTimes.TryGetValue(playerRoot, out float lastTime))
            {
                if (Time.time - lastTime < hitCooldown)
                {
                    return;
                }
            }

            lastHitTimes[playerRoot] = Time.time;

            // Tính toán lực đẩy lùi
            Vector3 knockbackDir = (playerRoot.position - transform.position).normalized;
            knockbackDir.y = 0.3f;
            Vector3 force = knockbackDir * knockback;

            // Trực tiếp trừ máu đồng bộ
            EnemyDamageHelper.DealDamage(playerRoot, damage, force);
            Debug.Log($"[FireBeamDamageZone] Cột lửa chưởng trúng player: {playerRoot.name}, Sát thương: -{damage} HP");
        }
    }
}
