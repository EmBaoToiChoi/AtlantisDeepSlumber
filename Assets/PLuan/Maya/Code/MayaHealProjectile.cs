using Unity.Netcode;
using UnityEngine;

public class MayaHealProjectile : NetworkBehaviour
{
    public float speed = 30f;
    public float lifetime = 4f;
    public float healPercentage = 0.375f; // 37.5% of Maya's max health (+50%)
    public float mayaMaxHealth = 100f;   // Maya's max health, passed at spawn
    public float duration = 5f;          // HoT duration

    [HideInInspector]
    public MayaPlayer owner;

    [HideInInspector]
    public Transform localTargetTransform; // Used in Standalone mode

    // Network variable to replicate target to all clients in Multiplayer mode
    public NetworkVariable<NetworkObjectReference> targetNetObjRef = new NetworkVariable<NetworkObjectReference>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private void Start()
    {
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer)
        {
            Destroy(gameObject, lifetime);
        }
    }

    private void Update()
    {
        Transform currentTarget = null;
        if (NetworkManager.Singleton == null)
        {
            currentTarget = localTargetTransform;
        }
        else if (targetNetObjRef.Value.TryGet(out NetworkObject targetNetObj))
        {
            currentTarget = targetNetObj.transform;
        }

        if (currentTarget != null)
        {
            // Point towards player's torso/chest area (approx 1m up from base pivot)
            Vector3 targetCenter = currentTarget.position + Vector3.up * 1f;
            Vector3 dir = (targetCenter - transform.position).normalized;
            if (dir != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(dir);
            }
        }

        transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        bool isServerOrStandalone = NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;
        if (!isServerOrStandalone) return;

        // Try to hit a player
        IPlayerHUDTarget targetPlayer = other.GetComponentInParent<IPlayerHUDTarget>();
        if (targetPlayer != null && targetPlayer.CurrentHealth > 0)
        {
            if (owner != null && targetPlayer.gameObject == owner.gameObject)
            {
                // Ignore self collision
                return;
            }

            // Calculate heal per second (e.g. 25% of Maya's Max Health / 5 seconds)
            float totalHeal = mayaMaxHealth * healPercentage;
            float healPerSecond = totalHeal / duration;

            // Apply Heal Over Time effect
            var hostObj = other.transform.root.gameObject;
            var hot = hostObj.GetComponent<MayaHealOverTimeEffect>();
            if (hot == null)
            {
                hot = hostObj.AddComponent<MayaHealOverTimeEffect>();
            }
            hot.Initialize(duration, healPerSecond);

            Debug.Log($"[MayaHealProjectile] Hit player {hostObj.name}. Applied HoT of {healPerSecond}/sec for {duration}s.");
            DespawnOrDestroy();
        }
        else if (!other.isTrigger && !other.CompareTag("Enemy") && other.gameObject.layer != LayerMask.NameToLayer("Enemy"))
        {
            // Hit obstacle (wall, ground, etc.)
            DespawnOrDestroy();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider != null)
        {
            OnTriggerEnter(collision.collider);
        }
    }

    private void DespawnOrDestroy()
    {
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
