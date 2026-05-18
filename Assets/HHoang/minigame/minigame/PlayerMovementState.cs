using UnityEngine;
// BẮT BUỘC: Phải thêm thư viện này ở trên cùng để dùng Input System mới
using UnityEngine.InputSystem; 

public class PlayerMovementState : MonoBehaviour
{
    [Header("Movement Check")]
    public bool isCrouching = false;
    public bool isMoving = false;

    private CharacterController cc;
    private Vector3 lastPosition;

    void Start()
    {
        cc = GetComponent<CharacterController>();
        lastPosition = transform.position;
    }

    void Update()
    {
        // 1. KIỂM TRA BẤM NÚT NGỒI (Hệ thống Input mới)
        // Kiểm tra nếu nút C hoặc nút Shift được nhấn giữ
        if (Keyboard.current.cKey.isPressed || Keyboard.current.leftShiftKey.isPressed)
        {
            isCrouching = true;
        }
        else
        {
            isCrouching = false;
        }

        // 2. Kiểm tra xem Player có đang thực sự di chuyển không
        if (cc != null)
        {
            isMoving = cc.velocity.magnitude > 0.1f;
        }
        else
        {
            isMoving = Vector3.Distance(transform.position, lastPosition) > 0.01f;
            lastPosition = transform.position;
        }
    }

    // Hàm để quái gọi check tiếng động
    public bool IsMakingNoise()
    {
        return isMoving && !isCrouching;
    }
}