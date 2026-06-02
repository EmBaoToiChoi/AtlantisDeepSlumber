using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : NetworkBehaviour
{
    private Rigidbody rb;

    [Header("Cấu hình Di Chuyển")]
    public float moveSpeed = 7f;
    public float crouchSpeed = 3f;

    [Header("Trạng thái Mạng")]
    public NetworkVariable<bool> canMoveNet = new NetworkVariable<bool>(true);
    public NetworkVariable<bool> isCrouchingNet = new NetworkVariable<bool>(false);
    
    // Đảm bảo NetworkVariable được đồng bộ tốt
    private NetworkVariable<Vector2> networkMoveInput = new NetworkVariable<Vector2>(
        writePerm: NetworkVariableWritePermission.Owner
    );

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
    }

    void Update()
    {
        // CHỈ Client sở hữu nhân vật mới thực hiện lấy Input
        if (!IsOwner) return;

        // Xử lý Input an toàn: Kiểm tra Keyboard.current tồn tại không
        Vector2 input = Vector2.zero;
        if (canMoveNet.Value && Keyboard.current != null)
        {
            float x = (Keyboard.current.aKey.isPressed ? -1 : 0) + (Keyboard.current.dKey.isPressed ? 1 : 0);
            float y = (Keyboard.current.sKey.isPressed ? -1 : 0) + (Keyboard.current.wKey.isPressed ? 1 : 0);
            input = new Vector2(x, y).normalized;
        }

        // Gửi input lên Server
        UpdateInputServerRpc(input);

        // Xử lý Crouch
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
        // Logic vật lý NÊN chạy ở Server (IsServer) để đảm bảo đồng bộ cho tất cả Client
        if (!IsServer) return;

        float targetSpeed = isCrouchingNet.Value ? crouchSpeed : moveSpeed;
        
        // Dùng rb.linearVelocity (Unity 6+)
        Vector3 newVelocity = new Vector3(networkMoveInput.Value.x * targetSpeed, rb.linearVelocity.y, networkMoveInput.Value.y * targetSpeed);
        rb.linearVelocity = newVelocity;
    }

    [ServerRpc]
    private void UpdateInputServerRpc(Vector2 input) 
    {
        networkMoveInput.Value = input;
    }

    [ServerRpc]
    private void UpdateCrouchStatusServerRpc(bool state) 
    {
        isCrouchingNet.Value = state;
    }

    public void SetCanMove(bool state)
    {
        if (IsServer) canMoveNet.Value = state;
        else SetCanMoveServerRpc(state);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetCanMoveServerRpc(bool state) => canMoveNet.Value = state;
}