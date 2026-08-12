using UnityEngine;
using System.Collections.Generic;

public class EarthSpikesDamageZone : MonoBehaviour
{
    [Header("Damage Settings")]
    public float damageAmount = 5f;
    [Tooltip("Bán kính vùng đâm gai chuẩn xác theo mô hình VFX_Earth_Area_01 (mặc định 3.2m)")]
    public float damageRadius = 3.2f;
    [Tooltip("Độ cao tối đa của gai nhô lên (người chơi nhảy cao hơn 2.5m sẽ né được gai)")]
    public float maxSpikeHeight = 2.5f;
    [Tooltip("Thời gian chờ đòn gồng trước khi gai nhô lên khỏi mặt đất (0.35s)")]
    public float spikeEmergenceDelay = 0.35f;
    [Tooltip("Thời gian duy trì đâm gai cắm trên mặt đất trước khi rút xuống")]
    public float spikeActiveDuration = 1.8f;
    [Tooltip("Thời gian sống tối đa của VFX hiệu ứng")]
    public float vfxLifespan = 3.5f;

    private Dictionary<Transform, float> playerDamageTimers = new Dictionary<Transform, float>();
    private float elapsedTime = 0f;
    private SphereCollider triggerCollider;

    private void Start()
    {
        Destroy(gameObject, vfxLifespan);

        // Tự động gắn Trigger Collider để phát hiện Player chạy từ bên ngoài vào bãi gai lập tức
        triggerCollider = gameObject.AddComponent<SphereCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.radius = damageRadius;
        triggerCollider.center = new Vector3(0f, 0.5f, 0f);
    }

    private void Update()
    {
        elapsedTime += Time.deltaTime;

        // Quét liên tục tất cả Player trong bán kính khi gai đang nhô lên
        if (IsSpikeActive())
        {
            ScanAndDamagePlayersInRadius();
        }
    }

    private bool IsSpikeActive()
    {
        return elapsedTime >= spikeEmergenceDelay && elapsedTime <= (spikeEmergenceDelay + spikeActiveDuration);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsSpikeActive())
        {
            TryProcessPlayerDamage(other);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (IsSpikeActive())
        {
            TryProcessPlayerDamage(other);
        }
    }

    private void ScanAndDamagePlayersInRadius()
    {
        Vector3 centerPos = transform.position;
        Collider[] hits = Physics.OverlapSphere(centerPos + Vector3.up * 0.5f, damageRadius);

        foreach (var col in hits)
        {
            TryProcessPlayerDamage(col);
        }
    }

    private void TryProcessPlayerDamage(Collider col)
    {
        if (col == null) return;

        Transform playerRoot = GetPlayerRoot(col);
        if (playerRoot == null) return;

        Vector3 centerPos = transform.position;

        // 1. Kiểm tra độ cao: Nếu Player nhảy cao hơn độ cao gai nhô (2.5m) -> Không bị dính
        float heightDiff = playerRoot.position.y - centerPos.y;
        if (heightDiff < -1.0f || heightDiff > maxSpikeHeight) return;

        // 2. Kiểm tra khoảng cách phẳng (X-Z)
        Vector2 playerFlatPos = new Vector2(playerRoot.position.x, playerRoot.position.z);
        Vector2 centerFlatPos = new Vector2(centerPos.x, centerPos.z);
        if (Vector2.Distance(playerFlatPos, centerFlatPos) > damageRadius) return;

        if (!playerDamageTimers.ContainsKey(playerRoot))
        {
            playerDamageTimers[playerRoot] = 0f;
        }

        playerDamageTimers[playerRoot] -= Time.deltaTime;

        // Gây sát thương -5 HP lập tức khi bước/chạy vào gai (hoặc tiếp tục đứng trên bãi gai sau 0.5s)
        if (playerDamageTimers[playerRoot] <= 0f)
        {
            playerDamageTimers[playerRoot] = 0.5f; // Cooldown 0.5s nếu tiếp tục đứng trên bãi gai
            EnemyDamageHelper.DealDamage(playerRoot, damageAmount, Vector3.up * 3.0f + (playerRoot.position - centerPos).normalized * 1.5f);
            Debug.Log($"[EarthSpikesDamageZone] Player '{playerRoot.name}' chạy vào bãi gai! Trừ -{damageAmount} HP!");
        }
    }

    private Transform GetPlayerRoot(Collider col)
    {
        if (col == null) return null;

        if (col.CompareTag("Player")) return col.transform.root;

        var leo = col.GetComponentInParent<LeoPlayer>();
        if (leo != null) return leo.transform;

        var arthur = col.GetComponentInParent<ArthurPlayer>();
        if (arthur != null) return arthur.transform;

        var elena = col.GetComponentInParent<ElenaPlayer>();
        if (elena != null) return elena.transform;

        var maya = col.GetComponentInParent<MayaPlayer>();
        if (maya != null) return maya.transform;

        var simple = col.GetComponentInParent<SimplePlayerTest>();
        if (simple != null) return simple.transform;

        var skel = col.GetComponentInParent<Skeleton>();
        if (skel != null) return skel.transform;

        if (col.transform.root != null && col.transform.root.CompareTag("Player"))
            return col.transform.root;

        return null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0f, 0.8f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, damageRadius);
        Gizmos.DrawWireCube(transform.position + Vector3.up * (maxSpikeHeight * 0.5f), new Vector3(damageRadius * 2f, maxSpikeHeight, damageRadius * 2f));
    }
}
