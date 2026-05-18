using UnityEngine;
// BẮT BUỘC: Thêm thư viện Input System mới để Unity nhận diện được lệnh
using UnityEngine.InputSystem; 

public class PlayerMovement : MonoBehaviour
{
    private Rigidbody rb;

    [Header("Cấu hình Di Chuyển")]
    [Tooltip("Tốc độ di chuyển của nhân vật")]
    public float moveSpeed = 7f;

    private Vector2 moveInput;

    void Start()
    {
        // Tự động tìm Component Rigidbody gắn trên Player
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        // Khởi tạo vector hướng bấm phím mặc định bằng 0
        Vector2 keyboardInput = Vector2.zero;

        // Kiểm tra xem hệ thống có nhận diện được bàn phím hiện tại không
        if (Keyboard.current != null)
        {
            float x = 0f;
            float y = 0f;

            // Kiểm tra các phím di chuyển Lên / Xuống / Trái / Phải (Nhận cả W,A,S,D và phím mũi tên)
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) y = 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) y = -1f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) x = -1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) x = 1f;

            // Chuẩn hóa vector di chuyển (để khi đi xéo không bị chạy nhanh hơn đi thẳng)
            keyboardInput = new Vector2(x, y).normalized;
        }

        // Lưu hướng bấm nút vào biến toàn cục
        moveInput = keyboardInput;
    }

    void FixedUpdate()
    {
        // Áp dụng vận tốc di chuyển vào Rigidbody theo trục X và Z
        Vector3 targetVelocity = new Vector3(moveInput.x * moveSpeed, rb.linearVelocity.y, moveInput.y * moveSpeed);
        
        // Khi NGƯỜI CHƠI BẤM PHÍM: Di chuyển bình thường
        if (moveInput.magnitude > 0.1f)
        {
            rb.linearVelocity = targetVelocity;

            // Xoay mặt nhân vật hướng về phía đang chạy
            Vector3 lookDirection = new Vector3(moveInput.x, 0f, moveInput.y);
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 15f * Time.fixedDeltaTime);
        }
        // KHI NGƯỜI CHƠI BUÔNG TAY: Phanh lại lập tức!
        else
        {
            // Đưa vận tốc trục X và Z về 0 ngay lập tức để chống trượt, giữ nguyên trục Y để rơi tự do
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        }
    }
}