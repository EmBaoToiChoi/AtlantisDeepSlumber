using Unity.Netcode;
using UnityEngine;

public class LeoLightningProjectile : NetworkBehaviour
{
    public float speed = 20f;
    public float lifetime = 5f;
    public float damage = 40f;
    
    [HideInInspector]
    public LeoPlayer owner;

    private System.Collections.Generic.HashSet<Transform> hitEnemyRoots = new System.Collections.Generic.HashSet<Transform>();

    private void Start()
    {
        gameObject.tag = "Set"; // Force tag "Set" for elemental rock puzzles
        
        Debug.Log($"[LeoLightningProjectile] Đạn sét được khởi tạo tại: {transform.position}, góc xoay: {transform.rotation.eulerAngles}, Tag: {gameObject.tag}");

        if (NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer)
        {
            Destroy(gameObject, lifetime);
        }
    }

    private void Update()
    {
        transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        bool isServerOrStandalone = NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;
        if (!isServerOrStandalone) return;

        // Bỏ qua va chạm với Player
        if (other.CompareTag("Player") || 
            other.gameObject.layer == LayerMask.NameToLayer("Player") ||
            other.GetComponentInParent<ArthurPlayer>() != null ||
            other.GetComponentInParent<ElenaPlayer>() != null ||
            other.GetComponentInParent<LeoPlayer>() != null ||
            other.GetComponentInParent<MayaPlayer>() != null ||
            other.GetComponentInParent<SimplePlayerTest>() != null)
        {
            return;
        }

        // Kiểm tra xem đối tượng va chạm có phải là Enemy hay không
        bool isEnemy = other.CompareTag("Enemy") || 
                       other.gameObject.layer == LayerMask.NameToLayer("Enemy") ||
                       other.name.ToLower().Contains("enemy") ||
                       other.GetComponentInParent<Enemy1_DapBua>() != null ||
                       other.GetComponentInParent<Enemy2_Zombie>() != null ||
                       other.GetComponentInParent<Enemy3_Buaa>() != null ||
                       other.GetComponentInParent<Enemy4_Bongtoi>() != null ||
                       other.GetComponentInParent<Enemy5_PhuThuy>() != null;

        if (isEnemy)
        {
            Transform enemyRoot = other.transform.root;
            if (hitEnemyRoots.Contains(enemyRoot))
            {
                return;
            }
            hitEnemyRoots.Add(enemyRoot);

            Debug.Log($"[LeoLightningProjectile] Đạn sét va chạm trúng Enemy: {other.name}, Gây sát thương: {damage}");
            
            var e1 = other.GetComponentInParent<Enemy1_DapBua>();
            if (e1 != null) { e1.TakeDamage(damage); }
            else
            {
                var e2 = other.GetComponentInParent<Enemy2_Zombie>();
                if (e2 != null) { e2.TakeDamage(damage); }
                else
                {
                    var e3 = other.GetComponentInParent<Enemy3_Buaa>();
                    if (e3 != null) { e3.TakeDamage(damage); }
                    else
                    {
                        var e4 = other.GetComponentInParent<Enemy4_Bongtoi>();
                        if (e4 != null) { e4.TakeDamage(damage); }
                        else
                        {
                            var e5 = other.GetComponentInParent<Enemy5_PhuThuy>();
                            if (e5 != null) { e5.TakeDamage(damage); }
                        }
                    }
                }
            }

            DespawnOrDestroy();
        }
        else if (!other.isTrigger)
        {
            Debug.Log($"[LeoLightningProjectile] Đạn sét va chạm trúng chướng ngại vật: {other.name}, tự hủy.");
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
