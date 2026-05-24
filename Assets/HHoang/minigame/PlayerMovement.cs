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

    [Header("Cấu hình Nhặt Đồ")]
    public Transform holdPoint;
    public LayerMask interactableLayer;

    [Header("Trạng thái Mạng")]
    public NetworkVariable<bool> canMoveNet = new NetworkVariable<bool>(true);
    public NetworkVariable<bool> isCrouchingNet = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> isCarryingCore = new NetworkVariable<bool>(false);

    private Vector2 moveInput;
    public CrystalCore currentHeldCore = null;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
    }

    void Update()
    {
        if (!IsOwner) return;

        // --- ĐOẠN ĐÃ SỬA CÓ LOGIC CẮM TRỤ ---
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (currentHeldCore == null) 
            {
                TryPickupCore();
            }
            else 
            {
                // Tìm tất cả các Trụ trong Scene
                PillarStation[] stations = Object.FindObjectsByType<PillarStation>(FindObjectsSortMode.None);
                bool snapped = false;
                
                // Kiểm tra xem người chơi có đang đứng gần trụ nào không
                foreach (var station in stations)
                {
                    // Nếu TryInteract trả về true, nghĩa là đã cắm thành công vào trụ
                    if (station.TryInteract(this)) 
                    { 
                        snapped = true; 
                        break; 
                    }
                }

                // Nếu KHÔNG cắm vào trụ nào, thì mới thực hiện thả đồ xuống đất
                if (!snapped) 
                {
                    DropCore();
                }
            }
        }
        // --- KẾT THÚC ĐOẠN ĐÃ SỬA ---

        if (!canMoveNet.Value) { moveInput = Vector2.zero; return; }

        // Input logic
        Vector2 keyboardInput = Vector2.zero;
        if (Keyboard.current != null)
        {
            float x = (Keyboard.current.aKey.isPressed ? -1 : 0) + (Keyboard.current.dKey.isPressed ? 1 : 0);
            float y = (Keyboard.current.sKey.isPressed ? -1 : 0) + (Keyboard.current.wKey.isPressed ? 1 : 0);
            keyboardInput = new Vector2(x, y).normalized;
        }
        moveInput = keyboardInput;

        // Crouch logic
        if (Keyboard.current != null)
        {
            bool isPressingCrouch = Keyboard.current.cKey.isPressed || Keyboard.current.leftShiftKey.isPressed;
            if (isPressingCrouch != isCrouchingNet.Value) UpdateCrouchStatusServerRpc(isPressingCrouch);
        }
    }

    void FixedUpdate()
    {
        if (!IsOwner || !canMoveNet.Value || moveInput.magnitude <= 0.1f)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            return;
        }

        // TÍNH TOÁN TỐC ĐỘ ĐỘNG
        float targetSpeed = moveSpeed;
        if (isCrouchingNet.Value) targetSpeed = crouchSpeed;
        else if (isCarryingCore.Value && currentHeldCore != null) 
        {
            // Nhân tốc độ cơ bản với hệ số từ Lõi (1.0 nếu 2 người, 0.6 nếu 1 người)
            targetSpeed = moveSpeed * currentHeldCore.GetMoveSpeedMultiplier();
        }
        
        rb.linearVelocity = new Vector3(moveInput.x * targetSpeed, rb.linearVelocity.y, moveInput.y * targetSpeed);
    }

// Trong PlayerMovement.cs
    void TryPickupCore()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, 2f, interactableLayer);
        foreach (var hit in hitColliders)
        {
            CrystalCore core = hit.GetComponent<CrystalCore>();
            // Chỉ cần core tồn tại là được, không cần quan tâm nó đã có người giữ chưa
            if (core != null)
            {
                currentHeldCore = core;
                core.RequestPickup(OwnerClientId); // Server sẽ xử lý việc add vào list
                SetCarryingCoreServerRpc(true);
                break;
            }
        }
    }

    void DropCore()
    {
        if (currentHeldCore != null)
        {
            currentHeldCore.RequestDrop(OwnerClientId);
            currentHeldCore = null;
            SetCarryingCoreServerRpc(false);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetCarryingCoreServerRpc(bool state) => isCarryingCore.Value = state;

    [ServerRpc(RequireOwnership = false)]
    public void SetCanMoveServerRpc(bool state) => canMoveNet.Value = state;

    [ServerRpc(RequireOwnership = false)]
    private void UpdateCrouchStatusServerRpc(bool state) => isCrouchingNet.Value = state;
}