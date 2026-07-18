using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Script gây sát thương va chạm khi người chơi chạm vào đá triệu hồi (Earth Blast).
/// Gây 20 sát thương và có cooldown để tránh gây chết người chơi tức thì.
/// </summary>
public class EarthBlastDamageZone : MonoBehaviour
{
    public float damage = 20f;
    public float damageCooldown = 1.0f;
    private Dictionary<Transform, float> lastDamageTimes = new Dictionary<Transform, float>();

    private void OnTriggerStay(Collider other)
    {
        ProcessDamage(other.gameObject);
    }

    private void OnCollisionStay(Collision collision)
    {
        ProcessDamage(collision.gameObject);
    }

    private void ProcessDamage(GameObject target)
    {
        var player = target.GetComponentInParent<IPlayerHUDTarget>() ?? target.GetComponentInChildren<IPlayerHUDTarget>();
        if (player != null && player.CurrentHealth > 0)
        {
            Transform playerTrans = player.transform;
            if (!lastDamageTimes.TryGetValue(playerTrans, out float lastTime) || Time.time - lastTime >= damageCooldown)
            {
                lastDamageTimes[playerTrans] = Time.time;
                EnemyDamageHelper.DealDamage(playerTrans, damage, Vector3.zero);
                Debug.Log($"[EarthBlastDamageZone] Gây {damage} sát thương va chạm cho {playerTrans.name}");
            }
        }
    }
}
