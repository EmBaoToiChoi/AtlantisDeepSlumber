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

    private void Awake()
    {
        if (string.IsNullOrEmpty(gameObject.tag) || gameObject.tag == "Untagged")
        {
            gameObject.tag = "Lua";
        }
    }

    private void Start()
    {
        spawnTime = Time.time;

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
        // Di chuyển đạn về phía trước liên tục theo hướng transform.forward (bay song song mặt đất)
        if (!isHit)
        {
            transform.position += transform.forward * speed * Time.deltaTime;
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
            if (other.transform.root == caster.transform.root || other.transform.IsChildOf(caster.transform))
            {
                return;
            }
        }

        // Bỏ qua va chạm với quái vật khác
        if (other.CompareTag("Enemy") || other.gameObject.layer == LayerMask.NameToLayer("Enemy"))
        {
            return;
        }

        // Bỏ qua va chạm với đất đai / địa hình trong 0.15 giây đầu vừa sinh ra để tránh đạn bị cắm xuống đất
        if (Time.time - spawnTime < 0.15f)
        {
            string n = other.name.ToLower();
            if (n.Contains("ground") || n.Contains("terrain") || n.Contains("floor") || n.Contains("map") || n.Contains("mesh"))
            {
                return;
            }
        }

        bool isServerOrStandalone = !IsNetworkActive || IsServer;

        // Kiểm tra xem đối tượng va chạm có phải là Player hay không
        bool isPlayer = other.CompareTag("Player") ||
                        other.gameObject.layer == LayerMask.NameToLayer("Player") ||
                        other.GetComponentInParent<IPlayerHUDTarget>() != null ||
                        other.GetComponentInChildren<IPlayerHUDTarget>() != null;

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