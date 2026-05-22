using UnityEngine;
using UnityEngine.InputSystem; 
using Unity.Netcode; 

public class PlayerMovement : NetworkBehaviour
{
    private Rigidbody rb;

    [Header("Cấu hình Di Chuyển")]
    [Tooltip("Tốc độ di chuyển của nhân vật lúc đứng bình thường")]
    public float moveSpeed = 7f;

    [Tooltip("Tốc độ di chuyển chậm lại khi ngồi (Giống game DOORS)")]
    public float crouchSpeed = 3f; 

    [Header("Trạng thái Mạng")]
    // Biến mạng: Đóng/Mở quyền di chuyển (True: đi được, False: khóa chân đứng yên gạt cần)
    public NetworkVariable<bool> canMoveNet = new NetworkVariable<bool>(
        true, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server
    );

    // Biến mạng: Tự động đồng bộ nút ngồi từ Server xuống tất cả các máy Client
    public NetworkVariable<bool> isCrouchingNet = new NetworkVariable<bool>(
        false, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server
    );

    public bool isMoving = false;
    private Vector2 moveInput;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (!IsOwner) return;

        // NẾU BỊ KHÓA DI CHUYỂN (canMoveNet = false): Ép đứng yên tại chỗ và không nhận phím đi bộ
        if (!canMoveNet.Value)
        {
            moveInput = Vector2.zero;
            isMoving = false;
            return; 
        }

        // --- XỬ LÝ DI CHUYỂN BÌNH THƯỜNG ---
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
        isMoving = moveInput.magnitude > 0.1f;

        // --- XỬ LÝ NÚT NGỒI ---
        if (Keyboard.current != null)
        {
            bool isPressingCrouch = Keyboard.current.cKey.isPressed || Keyboard.current.leftShiftKey.isPressed;

            if (isPressingCrouch != isCrouchingNet.Value)
            {
                UpdateCrouchStatusServerRpc(isPressingCrouch);
            }
        }
    }

    void FixedUpdate()
    {
        if (!IsOwner) return;

        // KHI BỊ KHÓA DI CHUYỂN HOẶC BUÔNG TAY: Phanh cứng ngắc liền lập tức!
        if (!canMoveNet.Value || moveInput.magnitude <= 0.1f)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            return;
        }

        // Áp dụng tốc độ chạy
        float currentSpeed = isCrouchingNet.Value ? crouchSpeed : moveSpeed;
        Vector3 targetVelocity = new Vector3(moveInput.x * currentSpeed, rb.linearVelocity.y, moveInput.y * currentSpeed);
        
        rb.linearVelocity = targetVelocity;

        // Xoay mặt
        Vector3 lookDirection = new Vector3(moveInput.x, 0f, moveInput.y);
        Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 15f * Time.fixedDeltaTime);
    }

    // ServerRpc đổi trạng thái Đóng/Mở di chuyển công bằng cho mạng
    [ServerRpc(RequireOwnership = false)]
    public void SetCanMoveServerRpc(bool state)
    {
        canMoveNet.Value = state;
    }

    [ServerRpc]
    private void UpdateCrouchStatusServerRpc(bool crouchState)
    {
        isCrouchingNet.Value = crouchState;
    }

    public bool IsMakingNoise()
    {
        return isMoving && !isCrouchingNet.Value;
    }
}