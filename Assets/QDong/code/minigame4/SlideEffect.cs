using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Gắn vào TẤT CẢ nhân vật (Leo, Maya, Elena, Arthur).
/// Nhận lực trượt từ Puzzle4Manager và áp dụng qua transform trực tiếp,
/// không phụ thuộc vào Rigidbody hay CharacterController.
/// </summary>
public class SlideEffect : NetworkBehaviour
{
    [HideInInspector]
    public Vector3 slideVelocity = Vector3.zero;

    // Hệ số cản dần - nhân vật sẽ giảm lực trượt sau khi không bị đẩy nữa
    [SerializeField] private float drag = 3f;

    private CharacterController cc;
    private Rigidbody rb;

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        rb  = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        // Chỉ áp dụng trên client làm chủ (Owner)
        if (!IsOwner) return;
        if (slideVelocity.sqrMagnitude < 0.001f) return;

        if (cc != null && cc.enabled)
        {
            // Nhân vật dùng CharacterController (Maya, Elena, Arthur...)
            cc.Move(slideVelocity * Time.deltaTime);
        }
        else if (rb == null)
        {
            // Không có gì hết, di chuyển thẳng qua transform
            transform.position += slideVelocity * Time.deltaTime;
        }
        // Nếu có Rigidbody (Leo) thì để Puzzle4Manager tự AddForce như cũ - không cần làm gì thêm

        // Giảm dần lực trượt theo thời gian
        slideVelocity = Vector3.MoveTowards(slideVelocity, Vector3.zero, drag * Time.deltaTime);
    }

    /// <summary>
    /// Gọi từ Puzzle4Manager mỗi FixedUpdate khi đĩa đang nghiêng.
    /// </summary>
    public void ApplySlide(Vector3 force)
    {
        if (!IsOwner) return;
        slideVelocity += force * Time.fixedDeltaTime;

        // Giới hạn tốc độ trượt tối đa để tránh văng ra ngoài, giảm xuống để người chơi có thể chạy ngược lại
        float maxSpeed = 4.5f;
        if (slideVelocity.magnitude > maxSpeed)
            slideVelocity = slideVelocity.normalized * maxSpeed;
    }
}
