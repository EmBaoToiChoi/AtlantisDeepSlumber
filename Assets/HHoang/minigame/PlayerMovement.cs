using UnityEngine;
// BẮT BUỘC: Thêm thư viện Input System mới để Unity nhận diện được lệnh
using UnityEngine.InputSystem; 
using Unity.Netcode; // BẮT BUỘC: Thêm thư viện Netcode để làm game Multiplayer

// ĐỔI TỪ MonoBehaviour SANG NetworkBehaviour ĐỂ CHẠY MẠNG
public class PlayerMovement : NetworkBehaviour
{
    private Rigidbody rb;

    [Header("Cấu hình Di Chuyển")]
    [Tooltip("Tốc độ di chuyển của nhân vật lúc đứng bình thường")]
    public float moveSpeed = 7f;

    [Tooltip("Tốc độ di chuyển chậm lại khi ngồi (Giống game DOORS)")]
    public float crouchSpeed = 3f; 

    [Header("Trạng thái Mạng (Quái AI sẽ check cái này)")]
    // Biến mạng: Tự động đồng bộ nút ngồi từ Server xuống tất cả các máy Client
    // Đúng (True) nếu đang ngồi, Sai (False) nếu đang đứng
    public NetworkVariable<bool> isCrouchingNet = new NetworkVariable<bool>(
        false, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server
    );

    // Biến check xem nhân vật có đang di chuyển (bước đi) không
    public bool isMoving = false;

    private Vector2 moveInput;

    void Start()
    {
        // Tự động tìm Component Rigidbody gắn trên Player
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        // CHỈ MÁY CỦA CHÍNH BẠN (LOCAL PLAYER) MỚI ĐƯỢC ĐIỀU KHIỂN NÚT BẤM
        if (!IsOwner) return;

        // --- XỬ LÝ DI CHUYỂN ---
        Vector2 keyboardInput = Vector2.zero;

        if (Keyboard.current != null)
        {
            float x = 0f;
            float y = 0f;

            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) y = 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) y = -1f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) x = -1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) x = 1f;

            keyboardInput = new Vector2(x, y).normalized;
        }

        moveInput = keyboardInput;

        // Cập nhật trạng thái xem người chơi có đang di chuyển hay không dựa vào phím bấm
        isMoving = moveInput.magnitude > 0.1f;

        // --- XỬ LÝ NÚT NGỒI (C HOẶC SHIFT) ---
        if (Keyboard.current != null)
        {
            // Kiểm tra xem hiện tại có đang đè nút C hoặc nút Left Shift không
            bool isPressingCrouch = Keyboard.current.cKey.isPressed || Keyboard.current.leftShiftKey.isPressed;

            // Nếu trạng thái bấm nút thay đổi so với trên mạng hiện tại, gửi lệnh lên Server để đồng bộ
            if (isPressingCrouch != isCrouchingNet.Value)
            {
                UpdateCrouchStatusServerRpc(isPressingCrouch);
            }
        }
    }

    void FixedUpdate()
    {
        // CHỈ MÁY LOCAL MỚI ĐƯỢC ÁP DỤNG LỰC DI CHUYỂN, TRÁNH XUNG ĐỘT MẠNG
        if (!IsOwner) return;

        // QUYẾT ĐỊNH TỐC ĐỘ: Nếu biến mạng báo đang ngồi -> dùng crouchSpeed, ngược lại dùng moveSpeed
        float currentSpeed = isCrouchingNet.Value ? crouchSpeed : moveSpeed;

        // Áp dụng vận tốc di chuyển vào Rigidbody theo trục X và Z
        Vector3 targetVelocity = new Vector3(moveInput.x * currentSpeed, rb.linearVelocity.y, moveInput.y * currentSpeed);
        
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
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        }
    }

    // ServerRpc: Hàm chạy trên Server để thay đổi biến mạng, tự động đồng bộ cho cả phòng
    [ServerRpc]
    private void UpdateCrouchStatusServerRpc(bool crouchState)
    {
        isCrouchingNet.Value = crouchState;
    }

    // HÀM QUAN TRỌNG ĐỂ CON QUÁI AI GỌI CHECK TIẾNG ĐỘNG: 
    // Nếu ĐANG ĐI (isMoving) VÀ KHÔNG NGỒI (!isCrouchingNet.Value) -> Làm ồn (True)
    public bool IsMakingNoise()
    {
        return isMoving && !isCrouchingNet.Value;
    }
}