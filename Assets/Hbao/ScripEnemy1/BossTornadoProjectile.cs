using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Quản lý đường đạn Lốc Xoáy (Tornado Wave) bảo vệ xung quanh Boss và di chuyển thẳng ra ngoài.
/// Khi chạm vào Player: Gây sát thương, hất tung lên cao, xoay vòng trên không rồi rơi xuống sàn té ngã và đứng dậy.
/// Tương thích 100% cả Dedicated Server, Host lẫn Client (Double-sided Collision Detection).
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
    private float hitRadius = 2.5f;

    public void Initialize(Vector3 dir, float moveSpeed, float life, float dmg, float height, float trapDur, bool isServer)
    {
        moveDirection = dir.normalized;
        speed = moveSpeed;
        lifetime = life;
        damage = dmg;
        liftHeight = height;
        trapDuration = trapDur;
        isServerAuthority = isServer;

        // Tự động gắn SphereCollider Trigger để đón bắt va chạm vật lý của Unity
        SphereCollider col = GetComponent<SphereCollider>();
        if (col == null) col = gameObject.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = hitRadius;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        // Di chuyển thẳng về phía trước theo hướng đã định
        transform.position += moveDirection * speed * Time.deltaTime;

        // Quét va chạm liên tục mỗi khung hình để đảm bảo không bao giờ bị lọt người chơi
        CheckPlayerOverlap();
    }

    private void OnTriggerEnter(Collider other)
    {
        ProcessHit(other.gameObject);
    }

    private void OnTriggerStay(Collider other)
    {
        ProcessHit(other.gameObject);
    }

    private void CheckPlayerOverlap()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position + Vector3.up * 0.8f, hitRadius);
        foreach (var col in hits)
        {
            if (col != null)
            {
                ProcessHit(col.gameObject);
            }
        }
    }

    private void ProcessHit(GameObject target)
    {
        if (target == null) return;
        Transform playerRoot = GetPlayerRoot(target.transform);
        if (playerRoot == null) return;

        if (hitPlayers.Contains(playerRoot)) return;
        hitPlayers.Add(playerRoot);

        OnHitPlayer(playerRoot);
    }

    private void OnHitPlayer(Transform player)
    {
        Debug.Log($"[BossTornadoProjectile] Lốc xoáy TRÚNG Player: {player.name} -> Gây {damage} HP và hất tung xoay vòng!");

        // 1. Gây sát thương (nếu là Server hoặc Host)
        if (isServerAuthority || (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer))
        {
            EnemyDamageHelper.DealDamage(player, damage, Vector3.zero);
        }

        // 2. Kích hoạt hiệu ứng hất tung lên cao, xoay tít trên không, rơi xuống đất ngã và đứng dậy
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

        var leo = t.GetComponentInParent<LeoPlayer>();
        if (leo != null) return leo.transform;

        var arthur = t.GetComponentInParent<ArthurPlayer>();
        if (arthur != null) return arthur.transform;

        var elena = t.GetComponentInParent<ElenaPlayer>() ?? (MonoBehaviour)t.GetComponentInParent<ElenaArcher>();
        if (elena != null) return elena.transform;

        var maya = t.GetComponentInParent<MayaPlayer>() ?? (MonoBehaviour)t.GetComponentInParent<MayaSupport>();
        if (maya != null) return maya.transform;

        var stun = t.GetComponentInParent<PlayerKickedStun>() ?? t.GetComponentInChildren<PlayerKickedStun>();
        if (stun != null) return stun.transform;

        var p = t.GetComponentInParent<IPlayerHUDTarget>() ?? t.GetComponentInChildren<IPlayerHUDTarget>();
        if (p != null && p is MonoBehaviour mono) return mono.transform;

        return null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.8f, hitRadius);
    }
}
