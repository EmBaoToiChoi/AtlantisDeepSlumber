using Unity.Netcode;
using UnityEngine;

public class LeoLightningProjectile : NetworkBehaviour
{
    [Tooltip("Thời gian tồn tại của hiệu ứng sấm sét trước khi tự hủy")]
    public float lifetime = 3f;
    
    [Tooltip("Sát thương gây ra bởi tia sét")]
    public float damage = 40f;

    [HideInInspector]
    public LeoPlayer owner;

    private System.Collections.Generic.HashSet<Transform> hitEnemyRoots = new System.Collections.Generic.HashSet<Transform>();
    private bool isHit = false;

    private void Awake()
    {
        gameObject.tag = "Set"; // Bắt buộc tag "Set" để giải đố đá nguyên tố hệ Lôi trong game
    }

    private void Start()
    {
        // Đảm bảo có Collider để va chạm hoạt động
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            SphereCollider sphere = gameObject.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = 1.5f; // Kích thước vùng va chạm sét đánh
            Debug.LogWarning($"[LeoLightningProjectile] Không tìm thấy Collider. Đã tự động thêm SphereCollider mặc định.");
        }

        // Đảm bảo có Rigidbody để nhận biết va chạm tĩnh
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.constraints = RigidbodyConstraints.FreezeAll;
        }

        Debug.Log($"[LeoLightningProjectile] AOE Sét khởi tạo tại: {transform.position}, Tag: {gameObject.tag}");

        if (NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer)
        {
            Destroy(gameObject, lifetime);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Chỉ xử lý va chạm trên Server hoặc chế độ Standalone/Offline
        bool isServerOrStandalone = NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;
        if (!isServerOrStandalone) return;
        if (isHit) return;

        // Bỏ qua va chạm với bất kỳ đối tượng Player nào
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

            Debug.Log($"[LeoLightningProjectile] Sét đánh trúng Enemy: {other.name}, Gây sát thương: {damage}");
            
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
        }
    }
}
