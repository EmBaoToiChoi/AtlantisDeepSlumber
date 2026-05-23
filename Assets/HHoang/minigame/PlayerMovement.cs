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
    public float carryCoreSpeed = 3.5f;

    [Header("Cấu hình Nhặt Đồ")]
    public Transform holdPoint; // Kéo Empty Object ở ngực vào đây
    public LayerMask interactableLayer; // Chọn Layer "Interactable"

    [Header("Trạng thái Mạng")]
    public NetworkVariable<bool> canMoveNet = new NetworkVariable<bool>(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> isCrouchingNet = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> isCarryingCore = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public bool isMoving = false;
    private Vector2 moveInput;
    private CrystalCore currentHeldCore = null;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
    }

    void Update()
    {
        if (!IsOwner) return;

        // Xử lý phím E để Nhặt / Thả (Không dùng SetParent để tránh lỗi Netcode)
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (currentHeldCore == null) TryPickupCore();
            else DropCore();
        }

        if (!canMoveNet.Value)
        {
            moveInput = Vector2.zero;
            isMoving = false;
            return; 
        }

        // --- XỬ LÝ INPUT ---
        Vector2 keyboardInput = Vector2.zero;
        if (Keyboard.current != null)
        {
            float x = 0f; float y = 0f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) y = 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) y = -1f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) x = -1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) x = 1f;
            keyboardInput = new Vector2(x, y).normalized;
        }

        moveInput = keyboardInput;
        isMoving = moveInput.magnitude > 0.1f;

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

        if (!canMoveNet.Value || moveInput.magnitude <= 0.1f)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            return;
        }

        float currentSpeed = moveSpeed;
        if (isCrouchingNet.Value) currentSpeed = crouchSpeed;
        else if (isCarryingCore.Value) currentSpeed = carryCoreSpeed;
        
        Vector3 targetVelocity = new Vector3(moveInput.x * currentSpeed, rb.linearVelocity.y, moveInput.y * currentSpeed);
        rb.linearVelocity = targetVelocity;

        Vector3 lookDirection = new Vector3(moveInput.x, 0f, moveInput.y);
        if (lookDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 15f * Time.fixedDeltaTime);
        }
    }

    // --- LOGIC NHẶT / THẢ LÕI ---
    void TryPickupCore()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, 2f, interactableLayer);
        foreach (var hit in hitColliders)
        {
            CrystalCore core = hit.GetComponent<CrystalCore>();
            if (core != null && !core.IsBeingHeld)
            {
                currentHeldCore = core;
                currentHeldCore.RequestPickup(OwnerClientId);
                // KHÔNG DÙNG SetParent ở đây để tránh lỗi Netcode
                SetCarryingCoreServerRpc(true);
                break;
            }
        }
    }

    void DropCore()
    {
        if (currentHeldCore != null)
        {
            currentHeldCore.RequestDrop();
            SetCarryingCoreServerRpc(false);
            currentHeldCore = null;
        }
    }

    // --- CÁC HÀM GIAO TIẾP VỚI SERVER ---
    [ServerRpc(RequireOwnership = false)]
    public void SetCarryingCoreServerRpc(bool state) { isCarryingCore.Value = state; }

    [ServerRpc(RequireOwnership = false)]
    public void SetCanMoveServerRpc(bool state) { canMoveNet.Value = state; }

    [ServerRpc(RequireOwnership = false)]
    private void UpdateCrouchStatusServerRpc(bool crouchState) { isCrouchingNet.Value = crouchState; }
}