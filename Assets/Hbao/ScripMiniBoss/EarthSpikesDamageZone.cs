using UnityEngine;
using System.Collections.Generic;

public class EarthSpikesDamageZone : MonoBehaviour
{
    [Header("Damage Settings")]
    public float damageAmount = 5f;
    [Tooltip("Bán kính vùng đâm gai chuẩn xác theo mô hình VFX_Earth_Area_01 (mặc định 2.6m)")]
    public float damageRadius = 2.6f;
    [Tooltip("Độ cao tối đa của gai nhô lên (người chơi nhảy cao hơn 2.2m sẽ né được gai)")]
    public float maxSpikeHeight = 2.2f;
    [Tooltip("Thời gian chờ đòn gồng trước khi gai nhô lên khỏi mặt đất (0.35s)")]
    public float spikeEmergenceDelay = 0.35f;
    [Tooltip("Thời gian duy trì đâm gai cắm trên mặt đất trước khi rút xuống")]
    public float spikeActiveDuration = 1.6f;
    [Tooltip("Thời gian sống tối đa của VFX hiệu ứng")]
    public float vfxLifespan = 3.5f;

    private Dictionary<Transform, float> playerDamageTimers = new Dictionary<Transform, float>();
    private float elapsedTime = 0f;

    private void Start()
    {
        Destroy(gameObject, vfxLifespan);
    }

    private void Update()
    {
        elapsedTime += Time.deltaTime;

        // Chỉ tính sát thương chuẩn xác từ lúc gai đá nhô lên (0.35s) đến khi gai lặn xuống (0.35s + 1.6s)
        if (elapsedTime >= spikeEmergenceDelay && elapsedTime <= (spikeEmergenceDelay + spikeActiveDuration))
        {
            CheckSpikeCollisionAndDamage();
        }
    }

    private void CheckSpikeCollisionAndDamage()
    {
        Vector3 centerPos = transform.position;
        Collider[] hits = Physics.OverlapSphere(centerPos + Vector3.up * 0.5f, damageRadius);

        List<Transform> currentFramePlayers = new List<Transform>();

        foreach (var col in hits)
        {
            if (col == null || col.isTrigger) continue;

            Transform playerRoot = GetPlayerRoot(col);
            if (playerRoot == null) continue;

            // 1. Kiểm tra độ cao: Nếu Player đang nhảy cao hơn độ cao gai nhô (2.2m) -> Né được đòn gai
            float heightDiff = playerRoot.position.y - centerPos.y;
            if (heightDiff < -0.8f || heightDiff > maxSpikeHeight) continue;

            // 2. Kiểm tra bán kính phẳng (X-Z): Phải nằm chuẩn trong vòng gai Spikes Outer (2.6m)
            Vector2 playerFlatPos = new Vector2(playerRoot.position.x, playerRoot.position.z);
            Vector2 centerFlatPos = new Vector2(centerPos.x, centerPos.z);
            if (Vector2.Distance(playerFlatPos, centerFlatPos) > damageRadius) continue;

            if (!currentFramePlayers.Contains(playerRoot))
            {
                currentFramePlayers.Add(playerRoot);
            }
        }

        foreach (var player in currentFramePlayers)
        {
            if (!playerDamageTimers.ContainsKey(player))
            {
                playerDamageTimers[player] = 0f;
            }

            playerDamageTimers[player] -= Time.deltaTime;

            // Đâm gai gây -5 HP lập tức khi trúng gai (và lặp lại mỗi 0.6s nếu vẫn tiếp tục đứng lỳ trên bãi gai)
            if (playerDamageTimers[player] <= 0f)
            {
                playerDamageTimers[player] = 0.6f;
                EnemyDamageHelper.DealDamage(player, damageAmount, Vector3.up * 3.5f + (player.position - centerPos).normalized * 1.5f);
                Debug.Log($"[EarthSpikesDamageZone] Gai đá đâm trúng '{player.name}'! Trừ -{damageAmount} HP!");
            }
        }
    }

    private Transform GetPlayerRoot(Collider col)
    {
        if (col.CompareTag("Player")) return col.transform.root;

        if (col.GetComponentInParent<LeoPlayer>() != null) return col.GetComponentInParent<LeoPlayer>().transform;
        if (col.GetComponentInParent<ArthurPlayer>() != null) return col.GetComponentInParent<ArthurPlayer>().transform;
        if (col.GetComponentInParent<ElenaPlayer>() != null) return col.GetComponentInParent<ElenaPlayer>().transform;
        if (col.GetComponentInParent<MayaPlayer>() != null) return col.GetComponentInParent<MayaPlayer>().transform;
        if (col.GetComponentInParent<SimplePlayerTest>() != null) return col.GetComponentInParent<SimplePlayerTest>().transform;
        if (col.GetComponentInParent<Skeleton>() != null) return col.GetComponentInParent<Skeleton>().transform;

        return null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0f, 0.8f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, damageRadius);
        Gizmos.DrawWireCube(transform.position + Vector3.up * (maxSpikeHeight * 0.5f), new Vector3(damageRadius * 2f, maxSpikeHeight, damageRadius * 2f));
    }
}
