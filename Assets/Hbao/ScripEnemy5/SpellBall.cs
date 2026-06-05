using UnityEngine;
using Unity.Netcode;

public class SpellBall : NetworkBehaviour
{
    public float damage = 20f;
    public float knockback = 5f;

    private float lifeTimer = 5f;

    private bool IsNetworkActive => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

    private void Update()
    {
        if (IsNetworkActive && !IsServer) return;

        lifeTimer -= Time.deltaTime;
        if (lifeTimer <= 0)
        {
            if (IsNetworkActive && IsServer)
                GetComponent<NetworkObject>().Despawn();
            else
                Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsNetworkActive && !IsServer) return;

        // Deal damage using helper
        EnemyDamageHelper.DealDamage(other.transform, damage, transform.forward * knockback);

        if (IsNetworkActive && IsServer)
        {
            if (GetComponent<NetworkObject>() != null && GetComponent<NetworkObject>().IsSpawned)
                GetComponent<NetworkObject>().Despawn();
        }
        else
        {
            Destroy(gameObject);
        }
    }
}