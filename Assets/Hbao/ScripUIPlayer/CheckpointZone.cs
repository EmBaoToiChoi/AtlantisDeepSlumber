using UnityEngine;

/// <summary>
/// Đại diện cho một vùng Checkpoint vòng tròn phát sáng.
/// Khi người chơi chạm vào, checkpoint này sẽ được lưu cho riêng người chơi đó.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class CheckpointZone : MonoBehaviour
{
    [Header("Checkpoint Config")]
    [Tooltip("Chỉ số định danh của Checkpoint này (dùng để lưu trong danh sách)")]
    public int checkpointIndex = 0;

    [Tooltip("Bán kính vùng lưu Checkpoint và bán kính vòng tròn phát sáng")]
    public float radius = 2.5f;

    [Tooltip("Điểm hồi sinh khi chết. Nếu để trống, sẽ dùng chính vị trí của CheckpointZone")]
    public Transform spawnPointOverride;

    [Header("Visual Colors")]
    [ColorUsage(true, true)]
    public Color activeColor = new Color(0f, 1f, 1f, 1f); // Neon Cyan phát sáng

    [ColorUsage(true, true)]
    public Color inactiveColor = new Color(0.2f, 0.4f, 0.4f, 0.25f); // Neon Cyan mờ nhạt

    [Header("Animation Settings")]
    public float rotationSpeed = 20f;
    public float pulseSpeed = 2f;
    public float pulseMinAlpha = 0.4f;
    public float pulseMaxAlpha = 1f;

    private LineRenderer lineRenderer;
    private SphereCollider triggerCollider;
    private float currentRotationAngle = 0f;
    private bool isInitialized = false;

    private void Awake()
    {
        InitializeComponents();
    }

    private void Start()
    {
        // Tự động gán vị trí trên mặt đất nếu chưa khớp
        AlignToGround();
    }

    private void InitializeComponents()
    {
        if (isInitialized) return;

        // Thiết lập LineRenderer
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.useWorldSpace = false; // Vẽ theo không gian cục bộ để dễ di chuyển/quay
        lineRenderer.loop = true;
        lineRenderer.positionCount = 51; // 50 điểm vẽ + 1 điểm đóng vòng tròn
        lineRenderer.startWidth = 0.12f;
        lineRenderer.endWidth = 0.12f;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;

        // Cố gắng tìm hoặc tạo vật liệu sáng/neon cho LineRenderer tránh màu hồng lỗi mặc định
        if (lineRenderer.sharedMaterial == null || lineRenderer.sharedMaterial.name.Contains("Default-Line"))
        {
            Shader glowingShader = Shader.Find("Sprites/Default");
            if (glowingShader == null) glowingShader = Shader.Find("UI/Default");
            if (glowingShader == null) glowingShader = Shader.Find("Legacy Shaders/Particles/Additive");
            if (glowingShader != null)
            {
                lineRenderer.material = new Material(glowingShader);
            }
        }

        // Tự động thêm trigger collider nếu chưa có
        triggerCollider = GetComponent<SphereCollider>();
        if (triggerCollider == null)
        {
            triggerCollider = gameObject.AddComponent<SphereCollider>();
        }
        triggerCollider.isTrigger = true;
        triggerCollider.radius = radius;
        triggerCollider.center = Vector3.zero;

        isInitialized = true;
    }

    private void Update()
    {
        UpdateVisuals();
    }

    /// <summary>
    /// Vẽ vòng tròn phát sáng và tạo hiệu ứng nhấp nháy, xoay vòng tròn.
    /// </summary>
    private void UpdateVisuals()
    {
        if (lineRenderer == null) return;

        // Xác định xem checkpoint này có đang active đối với người chơi local hay không
        bool isActiveForLocal = false;
        if (PlayerCheckpointManager.Instance != null)
        {
            isActiveForLocal = PlayerCheckpointManager.Instance.IsCheckpointActiveForLocalPlayer(checkpointIndex);
        }

        // Tính toán màu sắc hiện tại có nhấp nháy (pulsate) alpha
        Color baseColor = isActiveForLocal ? activeColor : inactiveColor;
        float pulse = Mathf.PingPong(Time.time * pulseSpeed, 1f);
        float alpha = Mathf.Lerp(pulseMinAlpha, pulseMaxAlpha, pulse);
        
        // Nếu không active, giảm độ sáng đáng kể
        if (!isActiveForLocal)
        {
            alpha *= 0.5f;
        }

        Color finalColor = baseColor;
        finalColor.a = alpha;

        lineRenderer.startColor = finalColor;
        lineRenderer.endColor = finalColor;

        // Xoay vòng tròn vẽ
        currentRotationAngle += rotationSpeed * Time.deltaTime;
        if (currentRotationAngle >= 360f) currentRotationAngle -= 360f;

        // Cập nhật vị trí các điểm vẽ vòng tròn trên XZ plane
        for (int i = 0; i <= 50; i++)
        {
            float angle = (i * 360f / 50f) + currentRotationAngle;
            float rad = angle * Mathf.Deg2Rad;
            float x = Mathf.Sin(rad) * radius;
            float z = Mathf.Cos(rad) * radius;
            
            // Vẽ trên mặt phẳng XZ, nâng cao nhẹ 0.05f so với vị trí gốc để tránh Z-fighting với mặt đất
            lineRenderer.SetPosition(i, new Vector3(x, 0.05f, z));
        }
    }

    /// <summary>
    /// Tự động dò mặt đất để đặt CheckpointZone sát mặt đất.
    /// </summary>
    private void AlignToGround()
    {
        RaycastHit hit;
        // Bắn tia ray xuống dưới 10m từ vị trí Checkpoint
        if (Physics.Raycast(transform.position + Vector3.up * 1f, Vector3.down, out hit, 11f, ~LayerMask.GetMask("Player", "Ignore Raycast")))
        {
            transform.position = hit.point;
        }
    }

    /// <summary>
    /// Trả về vị trí hồi sinh cho người chơi tại checkpoint này.
    /// </summary>
    public Vector3 GetSpawnPosition()
    {
        if (spawnPointOverride != null)
        {
            return spawnPointOverride.position;
        }
        // Thêm khoảng cách đứng trên mặt đất nhẹ để tránh kẹt chân trong địa hình
        return transform.position + Vector3.up * 0.1f;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // Tìm component SimplePlayerTest trên người chơi chạm vào
        SimplePlayerTest player = other.GetComponentInParent<SimplePlayerTest>();
        if (player == null)
        {
            player = other.GetComponent<SimplePlayerTest>();
        }

        if (player != null && PlayerCheckpointManager.Instance != null)
        {
            PlayerCheckpointManager.Instance.RegisterCheckpoint(player, this);
        }
    }

    private void OnDrawGizmos()
    {
        // Vẽ vòng tròn phát sáng trong editor để lập trình viên dễ đặt vị trí
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radius);
        
        if (spawnPointOverride != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(spawnPointOverride.position, 0.3f);
            Gizmos.DrawLine(transform.position, spawnPointOverride.position);
        }
    }
}
