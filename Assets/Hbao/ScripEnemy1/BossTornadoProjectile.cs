using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quản lý đường đạn Lốc Xoáy (Tornado Wave) bảo vệ xung quanh Boss và di chuyển thẳng ra ngoài.
/// Khi chạm vào Player: Gây sát thương, hất tung lên cao, xoay vòng trên không rồi rơi xuống sàn té ngã và đứng dậy.
/// </summary>
public class BossTornadoProjectile : MonoBehaviour
{
    private Vector3 moveDirection;
    private float speed = 7.0f;
    private float lifetime = 5.0f;
    private float damage = 15.0f;
    private float liftHeight = 4.5f;
    private float trapDuration = 1.2f;
    private bool isServerAuthority = false;
    private HashSet<Transform> hitPlayers = new HashSet<Transform>();
    private float radius = 1.8f;

    public void Initialize(Vector3 dir, float moveSpeed, float life, float dmg, float height, float trapDur, bool isServer)
    {
        moveDirection = dir.normalized;
        speed = moveSpeed;
        lifetime = life;
        damage = dmg;
        liftHeight = height;
        trapDuration = trapDur;
        isServerAuthority = isServer;

        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        // Di chuyển thẳng về phía trước theo hướng đã định
        transform.position += moveDirection * speed * Time.deltaTime;

        // Chỉ Server / Host xử lý va chạm để đảm bảo đồng bộ hóa mạng
        if (isServerAuthority)
        {
            CheckPlayerCollision();
        }
    }

    private void CheckPlayerCollision()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position + Vector3.up * 1.0f, radius);
        foreach (var col in hits)
        {
            if (col == null) continue;
            Transform playerRoot = GetPlayerRoot(col.transform);
            if (playerRoot != null && !hitPlayers.Contains(playerRoot))
            {
                hitPlayers.Add(playerRoot);
                OnHitPlayer(playerRoot);
            }
        }
    }

    private void OnHitPlayer(Transform player)
    {
        Debug.Log($"[BossTornadoProjectile] Lốc xoáy trúng Player: {player.name} -> Gây {damage} HP và hất tung xoay vòng!");

        // 1. Gây sát thương (Server-authoritative)
        EnemyDamageHelper.DealDamage(player, damage, Vector3.zero);

        // 2. Kích hoạt hiệu ứng hất tung lên cao, xoay tít trên không, rơi xuống đất và ngã rồi đứng dậy
        var stun = player.GetComponentInParent<PlayerKickedStun>() ?? player.GetComponentInChildren<PlayerKickedStun>();
        if (stun != null)
        {
            stun.ApplyTornadoKnockup(liftHeight, trapDuration);
        }
    }

    private Transform GetPlayerRoot(Transform t)
    {
        if (t == null) return null;
        if (t.CompareTag("Player")) return t;
        if (t.root != null && t.root.CompareTag("Player")) return t.root;

        var p = t.GetComponentInParent<IPlayerHUDTarget>() ?? t.GetComponentInChildren<IPlayerHUDTarget>();
        if (p != null && p is MonoBehaviour mono) return mono.transform;

        return null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 1.0f, radius);
    }
}
