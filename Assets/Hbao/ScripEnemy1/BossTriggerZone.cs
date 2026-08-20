using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Vùng kích hoạt Boss (Boss Trigger Zone).
/// Khi bất kỳ người chơi (Player) nào bước vào vùng này (Trigger Collider),
/// Boss sẽ được kích hoạt và hiển thị thanh máu lên màn hình cho toàn bộ người chơi.
/// </summary>
public class BossTriggerZone : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Kéo thả đối tượng Boss AI vào đây (nếu để trống, sẽ tự tìm kiếm BossAI trong scene)")]
    public BossAI boss;

    [Header("Settings")]
    [Tooltip("Hộp va chạm kích hoạt (nếu để trống, sẽ tự động lấy Collider trên đối tượng này)")]
    public Collider triggerCollider;
    
    [Tooltip("Chỉ cho phép kích hoạt một lần duy nhất")]
    public bool triggerOnlyOnce = true;
    private bool hasTriggered = false;

    private void Awake()
    {
        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<Collider>();
        }

        // Đảm bảo Collider được thiết lập là IsTrigger
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
        else
        {
            Debug.LogWarning($"[BossTriggerZone] {gameObject.name} chưa có Collider! Hãy thêm một Box Collider hoặc Sphere Collider và tích chọn 'Is Trigger'.");
        }
    }

    private void Start()
    {
        if (boss == null)
        {
            boss = FindFirstObjectByType<BossAI>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered) return;

        // Kiểm tra xem đối tượng va chạm có phải là Player không
        if (other.CompareTag("Player") || IsPlayerObject(other.gameObject))
        {
            if (boss != null)
            {
                boss.ActivateBoss();
                hasTriggered = true;

                Debug.Log($"[BossTriggerZone] Người chơi '{other.name}' đã đi vào vùng kích hoạt! Kích hoạt Boss '{boss.name}'.");

                // Tắt vùng kích hoạt để tránh kích hoạt lại nhiều lần
                if (triggerOnlyOnce)
                {
                    if (triggerCollider != null) triggerCollider.enabled = false;
                    gameObject.SetActive(false);
                }
            }
        }
    }

    private bool IsPlayerObject(GameObject go)
    {
        // Kiểm tra Tag hoặc kiểm tra xem có Component đại diện cho Player không
        if (go.CompareTag("Player")) return true;
        
        // Hỗ trợ kiểm tra component của các loại Player khác nhau trong project
        if (go.GetComponentInParent<IPlayerHUDTarget>() != null) return true;
        if (go.GetComponentInChildren<IPlayerHUDTarget>() != null) return true;

        return false;
    }

    private void OnDrawGizmos()
    {
        // Vẽ vùng kích hoạt màu đỏ trong suốt trên Scene để lập trình viên dễ dàng căn chỉnh
        var col = GetComponent<BoxCollider>();
        if (col != null)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.25f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(col.center, col.size);
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(col.center, col.size);
        }
        else
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.25f);
            Gizmos.DrawSphere(transform.position, 1.5f);
        }
    }
}
