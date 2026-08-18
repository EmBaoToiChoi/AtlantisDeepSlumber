using UnityEngine;
using System.Collections.Generic;

public class EarthSpikesDamageZone : MonoBehaviour
{
    [Header("Damage Settings")]
    public float damageAmount = 5f;
    [Tooltip("Bán kính vùng đâm gai cơ sở (sẽ tự động nhân theo Scale của Transform)")]
    public float baseDamageRadius = 3.2f;
    [Tooltip("Độ cao tối đa của gai nhô lên (sẽ tự động nhân theo Scale Y)")]
    public float baseMaxSpikeHeight = 2.5f;
    [Tooltip("Thời gian chờ đòn gồng/vòng sáng vàng trước khi gai đá nhô lên hoàn toàn khỏi mặt đất (1.85s)")]
    public float spikeEmergenceDelay = 1.85f;
    [Tooltip("Thời gian duy trì đâm gai cắm trên mặt đất trước khi rút xuống")]
    public float spikeActiveDuration = 1.6f;
    [Tooltip("Thời gian sống tối đa của VFX hiệu ứng")]
    public float vfxLifespan = 4.2f;

    public float EffectiveDamageRadius => baseDamageRadius * transform.lossyScale.x;
    public float EffectiveMaxSpikeHeight => baseMaxSpikeHeight * transform.lossyScale.y;

    private Dictionary<Transform, float> playerDamageTimers = new Dictionary<Transform, float>();
    private float elapsedTime = 0f;
    private SphereCollider triggerCollider;
    private bool hasTriggeredEmergenceShake = false;

    private void Start()
    {
        Destroy(gameObject, vfxLifespan);

        // Tự động gắn Trigger Collider với bán kính chuẩn cơ sở (Scale sẽ tự động nhân lên)
        triggerCollider = gameObject.AddComponent<SphereCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.radius = baseDamageRadius;
        triggerCollider.center = new Vector3(0f, 0.5f, 0f);
    }

    private void Update()
    {
        elapsedTime += Time.deltaTime;

        // Rung chấn động mặt đất khi gai đá bắt đầu nhô lên
        if (!hasTriggeredEmergenceShake && elapsedTime >= spikeEmergenceDelay)
        {
            hasTriggeredEmergenceShake = true;
            CameraShakeHelper.ShakeAtPosition(transform.position, 0.6f, 1.2f, 40f);
        }

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
        float scaledRadius = EffectiveDamageRadius;
        Collider[] hits = Physics.OverlapSphere(centerPos + Vector3.up * (0.5f * transform.lossyScale.y), scaledRadius);

        foreach (var col in hits)
        {
            TryProcessPlayerDamage(col);
        }
    }

    private void TryProcessPlayerDamage(Collider col)
    {
        if (!IsSpikeActive()) return;
        if (col == null) return;

        Transform playerRoot = GetPlayerRoot(col);
        if (playerRoot == null) return;

        Vector3 centerPos = transform.position;
        float scaledRadius = EffectiveDamageRadius;
        float scaledMaxHeight = EffectiveMaxSpikeHeight;

        // 1. Kiểm tra độ cao (đã nhân theo Scale Y)
        float heightDiff = playerRoot.position.y - centerPos.y;
        if (heightDiff < -1.0f || heightDiff > scaledMaxHeight) return;

        // 2. Kiểm tra khoảng cách phẳng X-Z (đã nhân theo Scale X)
        Vector2 playerFlatPos = new Vector2(playerRoot.position.x, playerRoot.position.z);
        Vector2 centerFlatPos = new Vector2(centerPos.x, centerPos.z);
        if (Vector2.Distance(playerFlatPos, centerFlatPos) > scaledRadius) return;

        if (!playerDamageTimers.ContainsKey(playerRoot))
        {
            playerDamageTimers[playerRoot] = 0f;
        }

        playerDamageTimers[playerRoot] -= Time.deltaTime;

        // Gây sát thương -5 HP lập tức khi bước/chạy vào gai (hoặc tiếp tục đứng trên bãi gai sau 0.5s)
        if (playerDamageTimers[playerRoot] <= 0f)
        {
            playerDamageTimers[playerRoot] = 0.5f;
            EnemyDamageHelper.DealDamage(playerRoot, damageAmount, Vector3.up * 3.5f + (playerRoot.position - centerPos).normalized * 2.0f);
            Debug.Log($"[EarthSpikesDamageZone] Player '{playerRoot.name}' dính bãi gai (Scale {transform.lossyScale.x}x)! Trừ -{damageAmount} HP!");
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
        float scaledRadius = EffectiveDamageRadius;
        float scaledMaxHeight = EffectiveMaxSpikeHeight;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * (0.5f * transform.lossyScale.y), scaledRadius);
        Gizmos.DrawWireCube(transform.position + Vector3.up * (scaledMaxHeight * 0.5f), new Vector3(scaledRadius * 2f, scaledMaxHeight, scaledRadius * 2f));
    }
}
