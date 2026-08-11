using Unity.Netcode;
using UnityEngine;

public class MayaHealingZone : MonoBehaviour
{
    public float radius = 5f;
    public float duration = 5f;
    public float healAmount = 50f;
    public GameObject healBurstVfxPrefab;
    public GameObject playerHealVfxPrefab;

    private float timer = 0f;
    private float tickTimer = 0f;
    private const float TICK_INTERVAL = 1.0f;
    private bool hasHealed = false;

    private void Start()
    {
        Debug.Log($"[MayaHealingZone] Healing Zone spawned at {transform.position} with radius {radius} for {duration} seconds. Total Heal: {healAmount}");
    }

    private void Update()
    {
        timer += Time.deltaTime;
        tickTimer += Time.deltaTime;

        // Hồi máu liên tục mỗi 1.0s cho bất kỳ ai (Player & Skeleton) đứng trong vùng VFX
        if (tickTimer >= TICK_INTERVAL && !hasHealed)
        {
            tickTimer -= TICK_INTERVAL;
            float perTickHeal = healAmount / Mathf.Max(1f, duration);
            ApplyContinuousHealing(perTickHeal);
        }

        if (timer >= duration && !hasHealed)
        {
            hasHealed = true;

            ApplyPlayerVisualEffects();

            bool isAuthoritative = NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;
            if (isAuthoritative && healAmount > 0f)
            {
                ApplyHealing();
            }

            if (healBurstVfxPrefab != null)
            {
                GameObject burst = Instantiate(healBurstVfxPrefab, transform.position, Quaternion.identity);
                burst.transform.localScale = Vector3.one * 1.2f;
                Destroy(burst, 3f);
            }

            Destroy(gameObject);
        }
    }

    private void ApplyContinuousHealing(float perTickHeal)
    {
        bool isAuthoritative = NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;
        System.Collections.Generic.HashSet<GameObject> processedObjects = new System.Collections.Generic.HashSet<GameObject>();

        Collider[] hitColliders = Physics.OverlapSphere(transform.position, Mathf.Max(radius, 4.0f));
        foreach (var col in hitColliders)
        {
            if (col == null) continue;
            GameObject rootObj = col.transform.root.gameObject;
            if (processedObjects.Contains(rootObj)) continue;

            IPlayerHUDTarget player = col.GetComponentInParent<IPlayerHUDTarget>();
            if (player != null && player.CurrentHealth > 0)
            {
                processedObjects.Add(rootObj);
                if (isAuthoritative && perTickHeal > 0f) player.Heal(perTickHeal);
                TriggerPlayerVfx(player);
                continue;
            }

            Skeleton skel = col.GetComponentInParent<Skeleton>();
            if (skel != null && !skel.IsDead)
            {
                processedObjects.Add(rootObj);
                if (isAuthoritative && perTickHeal > 0f) skel.Heal(perTickHeal);
                TriggerGenericVfx(skel.transform);
            }
        }

        MonoBehaviour[] allMonos = FindObjectsOfType<MonoBehaviour>();
        foreach (var mono in allMonos)
        {
            if (mono == null || processedObjects.Contains(mono.gameObject)) continue;

            if (mono is IPlayerHUDTarget player && player.CurrentHealth > 0)
            {
                if (Vector3.Distance(player.transform.position, transform.position) <= Mathf.Max(radius, 4.0f))
                {
                    processedObjects.Add(player.gameObject);
                    if (isAuthoritative && perTickHeal > 0f) player.Heal(perTickHeal);
                    TriggerPlayerVfx(player);
                }
            }
            else if (mono is Skeleton skel && !skel.IsDead)
            {
                if (Vector3.Distance(skel.transform.position, transform.position) <= Mathf.Max(radius, 4.0f))
                {
                    processedObjects.Add(skel.gameObject);
                    if (isAuthoritative && perTickHeal > 0f) skel.Heal(perTickHeal);
                    TriggerGenericVfx(skel.transform);
                }
            }
        }
    }

    private void TriggerGenericVfx(Transform target)
    {
        if (playerHealVfxPrefab == null || target == null) return;
        GameObject playerVfx = Instantiate(playerHealVfxPrefab, target.position + Vector3.up * 0.5f, Quaternion.identity, target);
        Destroy(playerVfx, 2f);
    }

    private void ApplyPlayerVisualEffects()
    {
        if (playerHealVfxPrefab == null) return;

        System.Collections.Generic.HashSet<IPlayerHUDTarget> healedPlayers = new System.Collections.Generic.HashSet<IPlayerHUDTarget>();
        
        // 1. Quét theo va chạm Collider
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, Mathf.Max(radius, 4.0f));
        foreach (var col in hitColliders)
        {
            if (col == null) continue;
            IPlayerHUDTarget player = col.GetComponentInParent<IPlayerHUDTarget>();
            if (player != null && player.CurrentHealth > 0 && !healedPlayers.Contains(player))
            {
                healedPlayers.Add(player);
                TriggerPlayerVfx(player);
            }
        }

        // 2. Quét dự phòng trực tiếp tất cả Player trong Scene để không sót bất kỳ người chơi nào
        MonoBehaviour[] allMonos = FindObjectsOfType<MonoBehaviour>();
        foreach (var mono in allMonos)
        {
            if (mono is IPlayerHUDTarget player && player.CurrentHealth > 0 && !healedPlayers.Contains(player))
            {
                if (Vector3.Distance(player.transform.position, transform.position) <= Mathf.Max(radius, 4.0f))
                {
                    healedPlayers.Add(player);
                    TriggerPlayerVfx(player);
                }
            }
        }
    }

    private void TriggerPlayerVfx(IPlayerHUDTarget player)
    {
        if (player == null || player.gameObject == null) return;

        // Nếu là 3D mesh item (như Healing Item prefab), tạo hiệu ứng 5 dấu + nhỏ xoay vòng & bay lên quanh người chơi
        if (playerHealVfxPrefab.GetComponentInChildren<ParticleSystem>() == null)
        {
            GameObject orbitObj = new GameObject("PlayerHealOrbitVfx");
            orbitObj.transform.SetParent(player.transform);
            orbitObj.transform.localPosition = Vector3.zero;
            orbitObj.transform.localRotation = Quaternion.identity;

            var orbit = orbitObj.AddComponent<PlayerHealOrbitVfx>();
            orbit.Initialize(playerHealVfxPrefab);
        }
        else
        {
            GameObject playerVfx = Instantiate(playerHealVfxPrefab, player.transform.position + Vector3.up * 0.5f, Quaternion.identity, player.transform);
            Destroy(playerVfx, 3f);
        }
    }

    private void ApplyHealing()
    {
        System.Collections.Generic.HashSet<GameObject> healedObjects = new System.Collections.Generic.HashSet<GameObject>();
        
        // 1. Quét theo va chạm Collider
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, Mathf.Max(radius, 4.0f));
        foreach (var col in hitColliders)
        {
            if (col == null) continue;
            GameObject rootObj = col.transform.root.gameObject;
            if (healedObjects.Contains(rootObj)) continue;

            IPlayerHUDTarget player = col.GetComponentInParent<IPlayerHUDTarget>();
            if (player != null && player.CurrentHealth > 0)
            {
                healedObjects.Add(rootObj);
                player.Heal(healAmount);
                Debug.Log($"[MayaHealingZone] Final Heal player {col.transform.root.name} for {healAmount} HP.");
                continue;
            }

            Skeleton skel = col.GetComponentInParent<Skeleton>();
            if (skel != null && !skel.IsDead)
            {
                healedObjects.Add(rootObj);
                skel.Heal(healAmount);
                Debug.Log($"[MayaHealingZone] Final Heal Skeleton for {healAmount} HP.");
            }
        }

        // 2. Quét dự phòng trực tiếp tất cả Player & Skeleton trong Scene
        MonoBehaviour[] allMonos = FindObjectsOfType<MonoBehaviour>();
        foreach (var mono in allMonos)
        {
            if (mono == null || healedObjects.Contains(mono.gameObject)) continue;

            if (mono is IPlayerHUDTarget player && player.CurrentHealth > 0)
            {
                if (Vector3.Distance(player.transform.position, transform.position) <= Mathf.Max(radius, 4.0f))
                {
                    healedObjects.Add(player.gameObject);
                    player.Heal(healAmount);
                    Debug.Log($"[MayaHealingZone Fallback] Final Heal player {player.DisplayName} for {healAmount} HP.");
                }
            }
            else if (mono is Skeleton skel && !skel.IsDead)
            {
                if (Vector3.Distance(skel.transform.position, transform.position) <= Mathf.Max(radius, 4.0f))
                {
                    healedObjects.Add(skel.gameObject);
                    skel.Heal(healAmount);
                    Debug.Log($"[MayaHealingZone Fallback] Final Heal Skeleton for {healAmount} HP.");
                }
            }
        }
    }
}
