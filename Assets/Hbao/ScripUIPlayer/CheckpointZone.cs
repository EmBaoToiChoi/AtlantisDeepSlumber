using UnityEngine;

/// <summary>
/// Đại diện cho một vùng Checkpoint vô hình.
/// Chỉ cần người chơi đi chạm vào vùng collider trigger này, checkpoint sẽ được lưu cho riêng người chơi đó.
/// </summary>
public class CheckpointZone : MonoBehaviour
{
    [Header("Checkpoint Config")]
    [Tooltip("Chỉ số định danh của Checkpoint này (dùng để lưu trong danh sách)")]
    public int checkpointIndex = 0;

    [Tooltip("Bán kính vùng lưu Checkpoint")]
    public float radius = 2.5f;

    [Tooltip("Điểm hồi sinh khi chết. Nếu để trống, sẽ dùng chính vị trí của CheckpointZone")]
    public Transform spawnPointOverride;

    private SphereCollider triggerCollider;
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

        // Tìm component IPlayerHUDTarget trên người chơi chạm vào để hỗ trợ cả 4 class nhân vật
        IPlayerHUDTarget player = other.GetComponentInParent<IPlayerHUDTarget>();
        if (player == null)
        {
            player = other.GetComponent<IPlayerHUDTarget>();
        }

        if (player != null && PlayerCheckpointManager.Instance != null)
        {
            PlayerCheckpointManager.Instance.RegisterCheckpoint(player, this);
        }
    }

    private void OnDrawGizmos()
    {
        // Vẽ vòng tròn phát sáng trong Editor để lập trình viên dễ đặt vị trí (vô hình khi chơi game)
        Gizmos.color = new Color(0f, 1f, 1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, radius);
        Gizmos.color = new Color(0f, 1f, 1f, 0.1f);
        Gizmos.DrawSphere(transform.position, radius);
        
        if (spawnPointOverride != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(spawnPointOverride.position, 0.3f);
            Gizmos.DrawLine(transform.position, spawnPointOverride.position);
        }
    }
}
