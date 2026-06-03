using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : NetworkBehaviour
{
    private Rigidbody rb;
    public float moveSpeed = 7f;
    public float crouchSpeed = 3f;

    public NetworkVariable<bool> canMoveNet = new NetworkVariable<bool>(true);
    public NetworkVariable<bool> isCrouchingNet = new NetworkVariable<bool>(false);
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
        // CHỈ Client thực hiện phần này
        if (!IsOwner) return;

        // Tránh chạy logic Input nếu đang ở chế độ Batch/Headless
        if (Application.isBatchMode) return;

        Vector2 input = Vector2.zero;
        if (canMoveNet.Value && Keyboard.current != null)
        {
            float x = (Keyboard.current.aKey.isPressed ? -1 : 0) + (Keyboard.current.dKey.isPressed ? 1 : 0);
            float y = (Keyboard.current.sKey.isPressed ? -1 : 0) + (Keyboard.current.wKey.isPressed ? 1 : 0);
            input = new Vector2(x, y).normalized;
        }

        UpdateInputServerRpc(input);

        if (Keyboard.current != null)
        {
            bool isPressingCrouch = Keyboard.current.cKey.isPressed || Keyboard.current.leftShiftKey.isPressed;
            if (isPressingCrouch != isCrouchingNet.Value) 
                UpdateCrouchStatusServerRpc(isPressingCrouch);
        }
    }

    public void SetCanMove(bool state)
    {
        if (IsServer) 
        {
            canMoveNet.Value = state;
        }
        else 
        {
            // Vì chỉ Server mới có quyền ghi (WritePermission) vào NetworkVariable,
            // nên nếu gọi từ Client, bạn phải dùng ServerRpc
            SetCanMoveServerRpc(state);
        }
    }

    void FixedUpdate()
    {
        // Server chạy vật lý cho tất cả, Client không can thiệp vào vận tốc vật lý
        if (!IsServer) return;

        float targetSpeed = isCrouchingNet.Value ? crouchSpeed : moveSpeed;
        rb.linearVelocity = new Vector3(networkMoveInput.Value.x * targetSpeed, rb.linearVelocity.y, networkMoveInput.Value.y * targetSpeed);
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void SetCanMoveServerRpc(bool state)
    {
        canMoveNet.Value = state;
    }

    [ServerRpc] private void UpdateInputServerRpc(Vector2 input) => networkMoveInput.Value = input;
    [ServerRpc] private void UpdateCrouchStatusServerRpc(bool state) => isCrouchingNet.Value = state;
}