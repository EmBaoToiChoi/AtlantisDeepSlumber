using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;

public class FireBeamDamageZone : MonoBehaviour
{
    private float damage = 35f;
    private float knockback = 12f;
    private float hitCooldown = 0.5f;
    private MonoBehaviour bossOwner;

    // Lưu trữ thời gian trúng đòn cuối cùng của mỗi player để giãn cách sát thương
    private Dictionary<Transform, float> lastHitTimes = new Dictionary<Transform, float>();

    public void Initialize(MonoBehaviour owner, float dmg, float kb)
    {
        bossOwner = owner;
        damage = dmg;
        knockback = kb;
        lastHitTimes.Clear();
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
        // Nhận diện Player chính xác qua interface IPlayerHUDTarget (bao gồm cả các collider con)
        var player = other.GetComponentInParent<IPlayerHUDTarget>() ?? other.GetComponentInChildren<IPlayerHUDTarget>();
        if (player != null)
        {
            Transform playerRoot = player.transform;

            // Đồng bộ mạng tối ưu: Mỗi client tự chịu trách nhiệm tính toán va chạm cho chính nhân vật của mình (IsOwner).
            // Điều này ngăn chặn việc nhân bản sát thương từ nhiều máy khác nhau gửi về Server.
            bool isLocal = player.isStandaloneMode;
            if (!isLocal)
            {
                var netObj = player.gameObject.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsOwner)
                {
                    isLocal = true;
                }
            }

            if (!isLocal) return;

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

            // Trực tiếp trừ máu đồng bộ qua mạng
            EnemyDamageHelper.DealDamage(playerRoot, damage, force);
            Debug.Log($"[FireBeamDamageZone] Tia lửa chưởng trúng local player: {playerRoot.name}, Sát thương: {damage}");
        }
    }
}
