using UnityEngine;
using Unity.Netcode;

public class SpellBall : NetworkBehaviour
{
    [Header("Spell Ball Properties")]
    public float speed = 14f;
    public float damage = 10f;
    public float knockback = 3f;
    public float lifeTimer = 5f;

    [HideInInspector]
    public GameObject caster; // Tham chiếu đến quái vật bắn đạn để tránh va chạm vào bản thân

    private bool IsNetworkActive => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    private bool isHit = false;
    private float spawnTime;
    private Vector3 spawnPosition;

    private void Awake()
    {
        spawnTime = Time.time;
        spawnPosition = transform.position;

        if (string.IsNullOrEmpty(gameObject.tag) || gameObject.tag == "Untagged")
        {
            gameObject.tag = "Lua";
        }
    }

    private void Start()
    {
        if (spawnTime <= 0f)
        {
            spawnTime = Time.time;
            spawnPosition = transform.position;
        }

        // Tự động đảm bảo có Collider dạng Trigger để bắt va chạm
        Collider rootCol = GetComponent<Collider>();
        if (rootCol == null)
        {
            rootCol = gameObject.AddComponent<SphereCollider>();
        }
        rootCol.isTrigger = true;

        // Đảm bảo tất cả collider con cũng là trigger
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        foreach (var c in colliders)
        {
            c.isTrigger = true;
        }

        // Tự động đảm bảo có Rigidbody ở chế độ Kinematic để nạp va chạm vật lý
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.useGravity = false;
        rb.isKinematic = true;
    }

    private void Update()
    {
        // Nếu kẻ thi triển (caster) đã bị tiêu diệt / xoá khỏi màn chơi, tự động tiêu huỷ quả cầu lửa
        if (caster == null || !caster.activeInHierarchy)
        {
            DespawnOrDestroy();
            return;
        }

        // Di chuyển đạn về phía trước liên tục theo hướng transform.forward (bay song song mặt đất)
        if (!isHit)
        {
            float moveSpeed = speed > 0f ? speed : 14f;
            transform.position += transform.forward * moveSpeed * Time.deltaTime;
        }

        // Chỉ Server hoặc Standalone quản lý thời gian sống của viên đạn
        if (IsNetworkActive && !IsServer) return;

        lifeTimer -= Time.deltaTime;
        if (lifeTimer <= 0)
        {
            DespawnOrDestroy();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isHit) return;
        if (other == null) return;

        // Bỏ qua va chạm với kẻ thi triển (Caster / Enemy5) và các bộ phận thuộc caster
        if (caster != null)
        {
            if (other.transform.root == caster.transform.root || other.transform.IsChildOf(caster.transform) || caster.transform.IsChildOf(other.transform.root))
            {
                return;
            }
        }

        // Bỏ qua va chạm với quái vật khác / kẻ thi triển
        if (other.CompareTag("Enemy") || 
            other.gameObject.layer == LayerMask.NameToLayer("Enemy") ||
            other.GetComponentInParent<Enemy5_PhuThuy>() != null ||
            (other.GetComponentInParent<NetworkBehaviour>() != null && (other.name.ToLower().Contains("enemy") || other.name.ToLower().Contains("nguoi chim"))))
        {
            return;
        }

        bool isServerOrStandalone = !IsNetworkActive || IsServer;

        // Kiểm tra xem đối tượng va chạm có phải là Player hay không
        bool isPlayer = other.CompareTag("Player") ||
                        other.gameObject.layer == LayerMask.NameToLayer("Player") ||
                        other.GetComponentInParent<IPlayerHUDTarget>() != null ||
                        other.GetComponentInChildren<IPlayerHUDTarget>() != null;

        // Nếu KHÔNG PHẢI là Player: Bỏ qua va chạm nếu vừa mới sinh ra (dưới 0.25 giây) hoặc ở gần vị trí vừa sinh ra (< 1.2m)
        // để tránh việc đạn bị nổ/kẹt ngay lập tức do va chạm gậy (Staff), tay, hoặc mặt đất
        if (!isPlayer)
        {
            if (Time.time - spawnTime < 0.25f || Vector3.Distance(transform.position, spawnPosition) < 1.2f)
            {
                return;
            }
        }

        if (isPlayer)
        {
            if (isServerOrStandalone)
            {
                EnemyDamageHelper.DealDamage(other.transform, damage, transform.forward * knockback);
            }
            isHit = true;
            DespawnOrDestroy();
        }
        else if (!other.isTrigger)
        {
            // Va chạm với chướng ngại vật / tường trong game
            isHit = true;
            DespawnOrDestroy();
        }
    }

    private void DespawnOrDestroy()
    {
        if (IsNetworkActive && IsServer && GetComponent<NetworkObject>() != null && GetComponent<NetworkObject>().IsSpawned)
        {
            GetComponent<NetworkObject>().Despawn();
        }
        else if (!IsNetworkActive)
        {
            Destroy(gameObject);
        }
    }
}