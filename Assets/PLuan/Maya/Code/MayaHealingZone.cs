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
    private bool hasHealed = false;

    private void Start()
    {
        Debug.Log($"[MayaHealingZone] Healing Zone spawned at {transform.position} with radius {radius} for {duration} seconds. Total Heal: {healAmount}");
    }

    private void Update()
    {
        timer += Time.deltaTime;

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
                Destroy(burst, 3f);
            }

            Destroy(gameObject);
        }
    }

    private void ApplyPlayerVisualEffects()
    {
        if (playerHealVfxPrefab == null) return;

        System.Collections.Generic.HashSet<IPlayerHUDTarget> healedPlayers = new System.Collections.Generic.HashSet<IPlayerHUDTarget>();
        
        // 1. Quét theo va chạm Collider
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, radius);
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
                if (Vector3.Distance(player.transform.position, transform.position) <= radius)
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
        System.Collections.Generic.HashSet<IPlayerHUDTarget> healedPlayers = new System.Collections.Generic.HashSet<IPlayerHUDTarget>();
        
        // 1. Quét theo va chạm Collider
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, radius);
        foreach (var col in hitColliders)
        {
            if (col == null) continue;
            IPlayerHUDTarget player = col.GetComponentInParent<IPlayerHUDTarget>();
            if (player != null && player.CurrentHealth > 0 && !healedPlayers.Contains(player))
            {
                healedPlayers.Add(player);
                player.Heal(healAmount);
                Debug.Log($"[MayaHealingZone] Healed player {col.transform.root.name} for {healAmount} HP at the end of the duration.");
            }
        }

        // 2. Quét dự phòng trực tiếp tất cả Player trong Scene
        MonoBehaviour[] allMonos = FindObjectsOfType<MonoBehaviour>();
        foreach (var mono in allMonos)
        {
            if (mono is IPlayerHUDTarget player && player.CurrentHealth > 0 && !healedPlayers.Contains(player))
            {
                if (Vector3.Distance(player.transform.position, transform.position) <= radius)
                {
                    healedPlayers.Add(player);
                    player.Heal(healAmount);
                    Debug.Log($"[MayaHealingZone Fallback] Healed player {player.DisplayName} for {healAmount} HP.");
                }
            }
        }
    }
}
