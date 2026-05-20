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

    [Header("Knockback Settings")]
    private Vector3 knockbackVelocity;

    [Header("Camera Follow Settings")]
    public bool enableCameraFollow = true;
    public Vector3 cameraOffset = new Vector3(0f, 12f, -8f);
    public float cameraSmoothSpeed = 5f;
    public bool cameraLookAtPlayer = true;
    private Camera targetCamera;

    public override void OnNetworkSpawn()
    {
        // Chỉ Local Owner mới lắng nghe sự thay đổi của máu để cập nhật lên UI HUD cá nhân
        if (IsOwner)
        {
            currentHealth.OnValueChanged += OnHealthChanged;
            UpdateHealthHUD(currentHealth.Value);

            // Tìm camera chính hoặc bất kỳ camera nào trong Scene
            targetCamera = Camera.main;
            if (targetCamera == null)
            {
                targetCamera = FindObjectOfType<Camera>();
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            currentHealth.OnValueChanged -= OnHealthChanged;
        }
    }

    private void OnHealthChanged(float oldHealth, float newHealth)
    {
        UpdateHealthHUD(newHealth);
    }

    private void UpdateHealthHUD(float health)
    {
        PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
        if (hud != null)
        {
            // Đồng bộ HP phần trăm lên UI HUD chính thức
            hud.SetHealth(health / maxHealth);
        }
    }

    void Update()
    {
        // Chỉ điều khiển nếu là máy của mình (Local Player)
        if (!IsOwner) return;

        // Áp dụng lực đẩy lùi (Knockback) vật lý giảm dần mượt mà theo thời gian
        if (knockbackVelocity.magnitude > 0.01f)
        {
            transform.Translate(knockbackVelocity * Time.deltaTime, Space.World);
            knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, Time.deltaTime * 8f);
        }

        // 1. Logic Di chuyển đơn giản
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        Vector3 move = new Vector3(moveX, 0, moveZ);
        transform.Translate(move * moveSpeed * Time.deltaTime, Space.World);

        if (move != Vector3.zero)
        {
            transform.forward = move; // Xoay Cube về hướng di chuyển
        }

        // 2. Logic Tấn công bằng Chuột Trái
        if (Input.GetMouseButtonDown(0))
        {
            AttackServerRpc();
        }
    }

    void LateUpdate()
    {
        // Chỉ Camera Follow hoạt động với Local Owner
        if (!IsOwner || !enableCameraFollow) return;

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
            {
                targetCamera = FindObjectOfType<Camera>();
            }
        }

        if (targetCamera != null)
        {
            // Tính toán vị trí camera mục tiêu dựa trên offset
            Vector3 targetPosition = transform.position + cameraOffset;

            // Di chuyển camera mượt mà
            targetCamera.transform.position = Vector3.Lerp(
                targetCamera.transform.position,
                targetPosition,
                Time.deltaTime * cameraSmoothSpeed
            );

            // Tự động xoay camera hướng về phía Player nếu được bật
            if (cameraLookAtPlayer)
            {
                // Thêm Vector3.up để camera hướng vào phần thân của Player (tránh nhìn vào chân)
                Quaternion targetRotation = Quaternion.LookRotation((transform.position + Vector3.up * 1f) - targetCamera.transform.position);
                targetCamera.transform.rotation = Quaternion.Slerp(
                    targetCamera.transform.rotation,
                    targetRotation,
                    Time.deltaTime * cameraSmoothSpeed
                );
            }
        }
    }

    [ServerRpc]
    void AttackServerRpc()
    {
        // Bắn Raycast từ vị trí Cube ra phía trước để tìm Enemy
        RaycastHit hit;
        // Bắn raycast từ vị trí cao hơn mặt đất một chút để chắc chắn không trượt qua dưới chân hoặc trên đầu
        Vector3 rayStart = transform.position + Vector3.up * 0.5f;
        if (Physics.Raycast(rayStart, transform.forward, out hit, attackRange))
        {
            // Kiểm tra và gây sát thương cho toàn bộ 5 loại Enemy
            var enemy1 = hit.collider.GetComponentInParent<Enemy1_DapBua>();
            if (enemy1 != null)
            {
                enemy1.TakeDamage(damageAmount);
                Debug.Log("Đã gây " + damageAmount + " sát thương lên " + enemy1.gameObject.name);
                return;
            }
            
            var enemy2 = hit.collider.GetComponentInParent<Enemy2_Zombie>();
            if (enemy2 != null)
            {
                enemy2.TakeDamage(damageAmount);
                Debug.Log("Đã gây " + damageAmount + " sát thương lên " + enemy2.gameObject.name);
                return;
            }
            
            var enemy3 = hit.collider.GetComponentInParent<Enemy3_Buaa>();
            if (enemy3 != null)
            {
                enemy3.TakeDamage(damageAmount);
                Debug.Log("Đã gây " + damageAmount + " sát thương lên " + enemy3.gameObject.name);
                return;
            }
            
            var enemy4 = hit.collider.GetComponentInParent<Enemy4_Bongtoi>();
            if (enemy4 != null)
            {
                enemy4.TakeDamage(damageAmount);
                Debug.Log("Đã gây " + damageAmount + " sát thương lên " + enemy4.gameObject.name);
                return;
            }
            
            var enemy5 = hit.collider.GetComponentInParent<Enemy5_PhuThuy>();
            if (enemy5 != null)
            {
                enemy5.TakeDamage(damageAmount);
                Debug.Log("Đã gây " + damageAmount + " sát thương lên " + enemy5.gameObject.name);
                return;
            }
        }
        
        // Vẽ tia đỏ trong Scene để dễ nhìn thấy tầm đánh
        Debug.DrawRay(rayStart, transform.forward * attackRange, Color.red, 0.5f);
    }

    /// <summary>
    /// Hàm nhận sát thương (chỉ được gọi trên Server)
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (!IsServer) return;

        currentHealth.Value = Mathf.Max(currentHealth.Value - damage, 0f);
        Debug.Log($"[Server] {gameObject.name} nhận {damage} sát thương. Máu còn lại: {currentHealth.Value}");

        if (currentHealth.Value <= 0)
        {
            Debug.LogWarning($"[Server] {gameObject.name} đã chết!");
            // Thêm các logic khi Player chết tại đây nếu cần (hồi sinh, vô hiệu hóa di chuyển...)
        }
    }

    /// <summary>
    /// Nhận lực đẩy lùi từ bên ngoài (Server gọi và phát xuống Client sở hữu)
    /// </summary>
    [ClientRpc]
    public void ApplyKnockbackClientRpc(Vector3 force)
    {
        if (IsOwner)
        {
            knockbackVelocity = force;
        }
    }

    /// <summary>
    /// Giao diện áp dụng Knockback (Server-authoritative)
    /// </summary>
    public void ApplyKnockback(Vector3 force)
    {
        if (!IsServer) return;
        ApplyKnockbackClientRpc(force);
    }
}
