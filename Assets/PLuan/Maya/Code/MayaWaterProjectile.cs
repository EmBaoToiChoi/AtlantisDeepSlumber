using Unity.Netcode;
using UnityEngine;

public class MayaWaterProjectile : NetworkBehaviour
{
    public float speed = 20f;
    public float lifetime = 5f;
    public float damage = 40f;
    public float lifetimeAfterHit = 2f; // Thời gian chờ hủy đạn sau khi nổ để chờ hiệu ứng chạy xong
    
    [Header("Visual Effects References")]
    public GameObject castGFX;
    public GameObject hitGFX;
    public GameObject flyingGFX;

    [HideInInspector]
    public MayaPlayer owner;

    private System.Collections.Generic.HashSet<Transform> hitEnemyRoots = new System.Collections.Generic.HashSet<Transform>();
    private bool isHit = false;
    private Vector3 spawnPosition;

    private void Awake()
    {
        gameObject.tag = "Nuoc"; // Force tag "Nuoc" for elemental rock puzzles
    }

    private void Start()
    {
        spawnPosition = transform.position;
        // Đảm bảo đạn và tất cả con ở Layer Default (0) để chắc chắn va chạm được với đá
        SetLayerRecursive(gameObject, 0);

        // Đảm bảo có Collider để va chạm hoạt động
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            SphereCollider sphere = gameObject.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = 0.5f;
            Debug.LogWarning($"[MayaWaterProjectile] Không tìm thấy Collider. Đã tự động thêm SphereCollider mặc định.");
        }
        else
        {
            col.isTrigger = true;
        }

        // Đảm bảo có Rigidbody để nhận biết va chạm với các vật thể tĩnh (static obstacles)
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.constraints = RigidbodyConstraints.FreezeAll;
            Debug.Log("[MayaWaterProjectile] Đã tự động thêm Rigidbody ở chế độ Kinematic để bắt va chạm tĩnh.");
        }

        // Tự động tìm kiếm các bộ phận GFX nếu chưa gán trong Inspector
        if (castGFX == null) castGFX = FindChildWithNamePart("cast");
        if (hitGFX == null) hitGFX = FindChildWithNamePart("hit");
        if (flyingGFX == null)
        {
            foreach (Transform child in transform)
            {
                if (child.gameObject != castGFX && child.gameObject != hitGFX)
                {
                    flyingGFX = child.gameObject;
                    break;
                }
            }
        }

        // Khởi tạo trạng thái ban đầu của các GFX
        if (castGFX != null) castGFX.SetActive(true);
        if (flyingGFX != null) flyingGFX.SetActive(true);
        if (hitGFX != null) hitGFX.SetActive(false);

        Debug.Log($"[MayaWaterProjectile] Đạn nước được khởi tạo tại: {transform.position}, Tag: {gameObject.tag}");

        if (NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer)
        {
            Destroy(gameObject, lifetime);
        }
    }

    private void Update()
    {
        if (!isHit)
        {
            transform.Translate(Vector3.forward * speed * Time.deltaTime);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Kiểm tra xem có chạm vào đá nguyên tố không (xử lý trên cả Client và Server để bảo đảm tin cậy)
        ElementalRockPuzzle rock = other.GetComponentInParent<ElementalRockPuzzle>();
        if (rock != null)
        {
            rock.NotifyElementHit(gameObject);
            
            // Chạy visual nổ cục bộ và tắt đạn
            if (NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer)
            {
                HandleHitImpact();
            }
            else
            {
                ApplyHitVisuals();
            }
            return;
        }

        // Chỉ xử lý va chạm trên Server hoặc chế độ Standalone
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

        // Bypass non-enemy collisions if they are too close to the spawn point to prevent self/ground detonation
        if (!isEnemy && Vector3.Distance(transform.position, spawnPosition) < 1.5f)
        {
            return;
        }

        if (isEnemy)
        {
            Transform enemyRoot = other.transform.root;
            if (hitEnemyRoots.Contains(enemyRoot))
            {
                return;
            }
            hitEnemyRoots.Add(enemyRoot);

            Debug.Log($"[MayaWaterProjectile] Đạn nước va chạm trúng Enemy: {other.name}, Gây sát thương: {damage}");
            
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

            HandleHitImpact();
        }
        else if (!other.isTrigger)
        {
            Debug.Log($"[MayaWaterProjectile] Đạn nước va chạm trúng chướng ngại vật: {other.name}, nổ.");
            HandleHitImpact();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider != null)
        {
            OnTriggerEnter(collision.collider);
        }
    }

    private void HandleHitImpact()
    {
        // Chạy visual nổ cục bộ
        ApplyHitVisuals();

        // Đồng bộ visual nổ sang các Client khác qua mạng
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsServer)
        {
            TriggerHitVisualsClientRpc();
        }

        // Thiết lập thời gian hủy đạn tự động
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer)
        {
            CancelInvoke(nameof(DespawnOrDestroy));
            Invoke(nameof(DespawnOrDestroy), lifetimeAfterHit);
        }
    }

    [ClientRpc]
    private void TriggerHitVisualsClientRpc()
    {
        if (!IsServer)
        {
            ApplyHitVisuals();
        }
    }

    private void ApplyHitVisuals()
    {
        if (isHit) return;
        isHit = true;
        speed = 0f;

        // Tắt bộ phận cast và thân đạn bay
        if (castGFX != null) castGFX.SetActive(false);
        if (flyingGFX != null) flyingGFX.SetActive(false);

        // Bật visual nổ
        if (hitGFX != null) hitGFX.SetActive(true);

        // Vô hiệu hóa Collider để không kích hoạt va chạm thêm nữa
        if (TryGetComponent<Collider>(out var col)) col.enabled = false;
        
        // Ngắt vận tốc vật lý nếu có
        if (TryGetComponent<Rigidbody>(out var rb))
        {
            rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
    }

    /// <summary>
    /// Được gọi từ Animation Event tại keyframe cuối của hoạt ảnh Hit/Nổ.
    /// </summary>
    public void OnHitAnimationEnd()
    {
        Debug.Log($"[{gameObject.name}] Nhận sự kiện kết thúc Animation Event -> Despawn đạn.");
        DespawnOrDestroy();
    }

    private void DespawnOrDestroy()
    {
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            if (IsServer)
            {
                NetworkObject.Despawn(true);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private GameObject FindChildWithNamePart(string part)
    {
        foreach (Transform child in transform)
        {
            if (child.name.ToLower().Contains(part.ToLower()))
            {
                return child.gameObject;
            }
        }
        return null;
    }

    private void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
        {
            SetLayerRecursive(child.gameObject, layer);
        }
    }
}
