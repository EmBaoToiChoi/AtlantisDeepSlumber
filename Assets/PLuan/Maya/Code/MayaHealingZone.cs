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

        // Fallback visual: If no custom VFX prefab was instantiated as a child
        if (transform.childCount == 0)
        {
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            
            // Remove collider so it doesn't block player movement or rays
            var col = cylinder.GetComponent<Collider>();
            if (col != null)
            {
                Destroy(col);
            }

            cylinder.transform.SetParent(transform);
            cylinder.transform.localPosition = new Vector3(0f, 0.05f, 0f); // Slightly above ground
            cylinder.transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);
            cylinder.transform.localRotation = Quaternion.identity;

            var renderer = cylinder.GetComponent<Renderer>();
            if (renderer != null)
            {
                // Sprites/Default is available on BiRP/URP, supports simple unlit transparent color tinting
                Shader transparentShader = Shader.Find("Sprites/Default");
                if (transparentShader != null)
                {
                    Material mat = new Material(transparentShader);
                    mat.color = new Color(0.2f, 0.8f, 0.3f, 0.25f); // Transparent green glow
                    renderer.material = mat;
                }
            }
        }
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
