using Unity.Netcode;
using UnityEngine;

public class ElenaIceProjectile : NetworkBehaviour
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
    public ElenaPlayer owner;

    private System.Collections.Generic.HashSet<Transform> hitEnemyRoots = new System.Collections.Generic.HashSet<Transform>();
    private bool isHit = false;

    private void Start()
    {
        gameObject.tag = "Bang"; // Force tag "Bang" for elemental rock puzzles
        
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

        Debug.Log($"[ElenaIceProjectile] Đạn băng được khởi tạo tại: {transform.position}, Tag: {gameObject.tag}");

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

        if (isEnemy)
        {
            Transform enemyRoot = other.transform.root;
            if (hitEnemyRoots.Contains(enemyRoot))
            {
                return;
            }
            hitEnemyRoots.Add(enemyRoot);

            Debug.Log($"[ElenaIceProjectile] Đạn băng va chạm trúng Enemy: {other.name}, Gây sát thương: {damage}");
            
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
            Debug.Log($"[ElenaIceProjectile] Đạn băng va chạm trúng chướng ngại vật: {other.name}, nổ.");
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
}
