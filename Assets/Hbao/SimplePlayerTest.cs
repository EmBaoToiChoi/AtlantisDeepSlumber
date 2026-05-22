using Unity.Netcode;
using UnityEngine;

public class SimplePlayerTest : NetworkBehaviour
{
    [Header("Movement & Attack Settings")]
    public float moveSpeed = 5f;
    public float damageAmount = 20f;
    public float attackRange = 3f;

    [Header("Player Health Settings")]
    public float maxHealth = 100f;
    public NetworkVariable<float> currentHealth = new NetworkVariable<float>(
        100f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Header("Player Class Settings")]
    [Tooltip("0 = Sát Thủ, 1 = Hỏa Thuật, 2 = Cung Thủ, 3 = Tanker")]
    public int characterClassIndex = 0;

    [Header("Knockback Settings")]
    private Vector3 knockbackVelocity;

    [Header("Camera Follow Settings")]
    public bool enableCameraFollow = true;
    public Vector3 cameraOffset = new Vector3(0f, 12f, -8f);
    public float cameraSmoothSpeed = 5f;
    public bool cameraLookAtPlayer = true;
    private Camera targetCamera;

    // ------------------------------------------------------------------
    //  Biến nội bộ cho chế độ Standalone (không có Netcode)
    // ------------------------------------------------------------------
    private float localHealth;
    private bool isStandaloneMode = false; // true khi chạy đơn lẻ không qua NetworkManager

    /// <summary>
    /// Trả về true nếu NetworkManager đang hoạt động và đã kết nối/host.
    /// </summary>
    private bool IsNetworkActive =>
        NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

    /// <summary>
    /// Máu hiện tại: đọc từ NetworkVariable khi online, đọc từ biến local khi standalone.
    /// </summary>
    public float CurrentHealth =>
        isStandaloneMode ? localHealth : currentHealth.Value;

    // ------------------------------------------------------------------
    //  Khởi tạo Standalone (không qua Netcode)
    // ------------------------------------------------------------------
    private void Start()
    {
        // Nếu không có NetworkManager hoặc chưa listen → chạy đơn lẻ
        if (!IsNetworkActive)
        {
            isStandaloneMode = true;
            localHealth = maxHealth;
            InitStandaloneMode();
        }
        // Nếu có Netcode nhưng chưa spawn (vd: đang chờ) thì không làm gì thêm
        // OnNetworkSpawn() sẽ lo phần còn lại
    }

    /// <summary>
    /// Khởi tạo tất cả chức năng khi chơi đơn lẻ trong Editor mà không cần Host/Server.
    /// </summary>
    private void InitStandaloneMode()
    {
        Debug.Log("[SimplePlayerTest] Chạy ở chế độ STANDALONE (không có NetworkManager). " +
                  "Di chuyển và tấn công hoạt động cục bộ.");

        // Tìm camera
        targetCamera = Camera.main;
        if (targetCamera == null)
            targetCamera = FindObjectOfType<Camera>();

        // Khởi tạo HUD với profile nhân vật
        PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
        if (hud != null)
        {
            hud.SetupPlayerProfile(characterClassIndex);
            hud.SetHealth(1f); // Máu đầy khi vào
        }
    }

    // ------------------------------------------------------------------
    //  Netcode Spawn / Despawn
    // ------------------------------------------------------------------
    public override void OnNetworkSpawn()
    {
        isStandaloneMode = false;

        if (IsOwner)
        {
            currentHealth.OnValueChanged += OnHealthChanged;
            UpdateHealthHUD(currentHealth.Value);

            PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
            if (hud != null)
                hud.SetupPlayerProfile(characterClassIndex);

            targetCamera = Camera.main;
            if (targetCamera == null)
                targetCamera = FindObjectOfType<Camera>();
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
            currentHealth.OnValueChanged -= OnHealthChanged;
    }

    private void OnHealthChanged(float oldHealth, float newHealth)
    {
        UpdateHealthHUD(newHealth);
    }

    private void UpdateHealthHUD(float health)
    {
        PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
        if (hud != null)
            hud.SetHealth(health / maxHealth);
    }

    // ------------------------------------------------------------------
    //  Update (hoạt động cả Standalone lẫn Netcode)
    // ------------------------------------------------------------------
    void Update()
    {
        // Standalone: xử lý hoàn toàn cục bộ
        if (isStandaloneMode)
        {
            HandleStandaloneUpdate();
            return;
        }

        // Netcode: chỉ chủ sở hữu mới điều khiển
        if (!IsOwner) return;
        HandleOwnerUpdate();
    }

    private void HandleStandaloneUpdate()
    {
        // Knockback
        if (knockbackVelocity.magnitude > 0.01f)
        {
            transform.Translate(knockbackVelocity * Time.deltaTime, Space.World);
            knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, Time.deltaTime * 8f);
        }

        // Di chuyển
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");
        Vector3 move = new Vector3(moveX, 0, moveZ);
        transform.Translate(move * moveSpeed * Time.deltaTime, Space.World);
        if (move != Vector3.zero)
            transform.forward = move;

        // Tấn công đơn lẻ
        if (Input.GetMouseButtonDown(0))
            StandaloneAttack();
    }

    private void HandleOwnerUpdate()
    {
        // Knockback
        if (knockbackVelocity.magnitude > 0.01f)
        {
            transform.Translate(knockbackVelocity * Time.deltaTime, Space.World);
            knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, Time.deltaTime * 8f);
        }

        // Di chuyển
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");
        Vector3 move = new Vector3(moveX, 0, moveZ);
        transform.Translate(move * moveSpeed * Time.deltaTime, Space.World);
        if (move != Vector3.zero)
            transform.forward = move;

        // Tấn công qua RPC (chỉ khi đã spawn trên mạng)
        if (Input.GetMouseButtonDown(0))
        {
            if (IsSpawned)
                AttackServerRpc();
        }
    }

    void LateUpdate()
    {
        // Camera follow hoạt động cho cả standalone lẫn Netcode owner
        bool shouldFollow = isStandaloneMode || (IsSpawned && IsOwner);
        if (!shouldFollow || !enableCameraFollow) return;

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
                targetCamera = FindObjectOfType<Camera>();
        }

        if (targetCamera != null)
        {
            Vector3 targetPosition = transform.position + cameraOffset;
            targetCamera.transform.position = Vector3.Lerp(
                targetCamera.transform.position,
                targetPosition,
                Time.deltaTime * cameraSmoothSpeed
            );

            if (cameraLookAtPlayer)
            {
                Quaternion targetRotation = Quaternion.LookRotation(
                    (transform.position + Vector3.up * 1f) - targetCamera.transform.position
                );
                targetCamera.transform.rotation = Quaternion.Slerp(
                    targetCamera.transform.rotation,
                    targetRotation,
                    Time.deltaTime * cameraSmoothSpeed
                );
            }
        }
    }

    // ------------------------------------------------------------------
    //  Tấn công Standalone (không cần Server RPC)
    // ------------------------------------------------------------------
    private void StandaloneAttack()
    {
        Vector3 rayStart = transform.position + Vector3.up * 0.5f;
        Debug.DrawRay(rayStart, transform.forward * attackRange, Color.red, 0.5f);

        if (!Physics.Raycast(rayStart, transform.forward, out RaycastHit hit, attackRange)) return;

        TryDamageEnemy(hit.collider);
    }

    private void TryDamageEnemy(Collider col)
    {
        var e1 = col.GetComponentInParent<Enemy1_DapBua>();
        if (e1 != null) { e1.TakeDamage(damageAmount); return; }

        var e2 = col.GetComponentInParent<Enemy2_Zombie>();
        if (e2 != null) { e2.TakeDamage(damageAmount); return; }

        var e3 = col.GetComponentInParent<Enemy3_Buaa>();
        if (e3 != null) { e3.TakeDamage(damageAmount); return; }

        var e4 = col.GetComponentInParent<Enemy4_Bongtoi>();
        if (e4 != null) { e4.TakeDamage(damageAmount); return; }

        var e5 = col.GetComponentInParent<Enemy5_PhuThuy>();
        if (e5 != null) { e5.TakeDamage(damageAmount); return; }
    }

    // ------------------------------------------------------------------
    //  Server RPC Tấn công (chỉ dùng khi Netcode online)
    // ------------------------------------------------------------------
    [ServerRpc]
    void AttackServerRpc()
    {
        Vector3 rayStart = transform.position + Vector3.up * 0.5f;
        Debug.DrawRay(rayStart, transform.forward * attackRange, Color.red, 0.5f);

        if (!Physics.Raycast(rayStart, transform.forward, out RaycastHit hit, attackRange)) return;

        var enemy1 = hit.collider.GetComponentInParent<Enemy1_DapBua>();
        if (enemy1 != null) { enemy1.TakeDamage(damageAmount); return; }

        var enemy2 = hit.collider.GetComponentInParent<Enemy2_Zombie>();
        if (enemy2 != null) { enemy2.TakeDamage(damageAmount); return; }

        var enemy3 = hit.collider.GetComponentInParent<Enemy3_Buaa>();
        if (enemy3 != null) { enemy3.TakeDamage(damageAmount); return; }

        var enemy4 = hit.collider.GetComponentInParent<Enemy4_Bongtoi>();
        if (enemy4 != null) { enemy4.TakeDamage(damageAmount); return; }

        var enemy5 = hit.collider.GetComponentInParent<Enemy5_PhuThuy>();
        if (enemy5 != null) { enemy5.TakeDamage(damageAmount); return; }
    }

    // ------------------------------------------------------------------
    //  Nhận sát thương
    // ------------------------------------------------------------------
    /// <summary>
    /// Nhận sát thương. Chỉ Server xử lý khi online; cục bộ xử lý khi standalone.
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (isStandaloneMode)
        {
            localHealth = Mathf.Max(localHealth - damage, 0f);
            UpdateHealthHUD(localHealth);
            Debug.Log($"[Standalone] {gameObject.name} nhận {damage} sát thương. Máu còn: {localHealth}");
            if (localHealth <= 0)
                Debug.LogWarning($"[Standalone] {gameObject.name} đã chết!");
            return;
        }

        if (!IsServer) return;

        currentHealth.Value = Mathf.Max(currentHealth.Value - damage, 0f);
        Debug.Log($"[Server] {gameObject.name} nhận {damage} sát thương. Máu còn lại: {currentHealth.Value}");

        if (currentHealth.Value <= 0)
            Debug.LogWarning($"[Server] {gameObject.name} đã chết!");
    }

    // ------------------------------------------------------------------
    //  Knockback
    // ------------------------------------------------------------------
    /// <summary>
    /// Giao diện áp dụng Knockback (Server-authoritative khi online, cục bộ khi standalone).
    /// </summary>
    public void ApplyKnockback(Vector3 force)
    {
        if (isStandaloneMode)
        {
            knockbackVelocity = force;
            return;
        }

        if (!IsServer) return;
        ApplyKnockbackClientRpc(force);
    }

    [ClientRpc]
    public void ApplyKnockbackClientRpc(Vector3 force)
    {
        if (IsOwner)
            knockbackVelocity = force;
    }
}
