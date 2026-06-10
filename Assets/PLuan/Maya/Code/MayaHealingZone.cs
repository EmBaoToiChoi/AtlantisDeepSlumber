using Unity.Netcode;
using UnityEngine;

public class MayaHealingZone : MonoBehaviour
{
    public float radius = 5f;
    public float duration = 5f;
    public float healAmount = 50f;

    private float timer = 0f;
    private bool hasHealed = false;

    private void Start()
    {
        Debug.Log($"[MayaHealingZone] Healing Zone spawned at {transform.position} with radius {radius} for {duration} seconds. Total Heal: {healAmount}");
    }

    private void Update()
    {
        bool isAuthoritative = NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;
        if (!isAuthoritative) return;

        timer += Time.deltaTime;

        if (timer >= duration && !hasHealed)
        {
            hasHealed = true;
            ApplyHealing();
            Destroy(gameObject);
        }
    }

    private void ApplyHealing()
    {
        System.Collections.Generic.HashSet<IPlayerHUDTarget> healedPlayers = new System.Collections.Generic.HashSet<IPlayerHUDTarget>();
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, radius);
        foreach (var col in hitColliders)
        {
            if (col == null) continue;

            // Search for player scripts via the shared interface
            IPlayerHUDTarget player = col.GetComponentInParent<IPlayerHUDTarget>();
            if (player != null && player.CurrentHealth > 0 && !healedPlayers.Contains(player))
            {
                healedPlayers.Add(player);
                player.Heal(healAmount);
                Debug.Log($"[MayaHealingZone] Healed player {col.transform.root.name} for {healAmount} HP at the end of the duration.");
            }
        }
    }
}
