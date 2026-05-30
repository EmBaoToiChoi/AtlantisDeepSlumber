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

    // Biến lưu trạm hiện tại nhân vật đang đứng
    public PillarStation currentStation = null; 

    private NetworkVariable<Vector2> networkMoveInput = new NetworkVariable<Vector2>();
    public CrystalCore currentHeldCore = null;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
    }

    void Update()
    {
    if (!IsOwner) return;

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (currentHeldCore == null) 
            {
                TryPickupCore();
            }
            else 
            {
                // Kiểm tra xem có đang đứng gần trạm nào không
                if (currentStation != null)
                {
                    // Gọi TryInteract của trụ đó
                    bool success = currentStation.TryInteract(this);
                    if (!success) DropCore(); // Nếu trạm từ chối, mới thả xuống đất
                }
                else
                {
                    DropCore();
                }
            }
        }

        // 2. Xử lý di chuyển
        Vector2 input = Vector2.zero;
        if (canMoveNet.Value && Keyboard.current != null)
        {
            float x = (Keyboard.current.aKey.isPressed ? -1 : 0) + (Keyboard.current.dKey.isPressed ? 1 : 0);
            float y = (Keyboard.current.sKey.isPressed ? -1 : 0) + (Keyboard.current.wKey.isPressed ? 1 : 0);
            input = new Vector2(x, y).normalized;
        }
        
        UpdateInputServerRpc(input);

        // 3. Crouch logic
        if (Keyboard.current != null)
        {
            bool isPressingCrouch = Keyboard.current.cKey.isPressed || Keyboard.current.leftShiftKey.isPressed;
            if (isPressingCrouch != isCrouchingNet.Value) UpdateCrouchStatusServerRpc(isPressingCrouch);
        }
    }

    void FixedUpdate()
    {
        float targetSpeed = isCrouchingNet.Value ? crouchSpeed : moveSpeed;
        if (isCarryingCore.Value && currentHeldCore != null) 
        {
            targetSpeed = moveSpeed * currentHeldCore.GetMoveSpeedMultiplier();
        }
        
        rb.linearVelocity = new Vector3(networkMoveInput.Value.x * targetSpeed, rb.linearVelocity.y, networkMoveInput.Value.y * targetSpeed);
    }

    [ServerRpc]
    private void UpdateInputServerRpc(Vector2 input) => networkMoveInput.Value = input;

    void TryPickupCore()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, 2f, interactableLayer);
        foreach (var hit in hitColliders)
        {
            CrystalCore core = hit.GetComponent<CrystalCore>();
            if (core != null && !core.isSnapped.Value) 
            {
                // Gửi yêu cầu nhặt, không tự ý gán ở client
                RequestPickupServerRpc(core.NetworkObject.NetworkObjectId);
                break;
            }
        }
    }

    [ServerRpc]
    private void RequestPickupServerRpc(ulong networkObjectId)
    {
        var networkObject = NetworkManager.Singleton.SpawnManager.SpawnedObjects[networkObjectId];
        var core = networkObject.GetComponent<CrystalCore>();
        
        currentHeldCore = core; // Gán trên Server
        isCarryingCore.Value = true; // Sync sang client
        core.RequestPickup(OwnerClientId);
        
        // Gửi về cho Client đó biết nó đã cầm vật phẩm nào
        AssignHeldCoreClientRpc(networkObjectId);
    }

    [ClientRpc]
    private void AssignHeldCoreClientRpc(ulong networkObjectId)
    {
        var networkObject = NetworkManager.Singleton.SpawnManager.SpawnedObjects[networkObjectId];
        currentHeldCore = networkObject.GetComponent<CrystalCore>();
    }

    public void DropCore()
    {
        // Gọi ServerRpc để đảm bảo Server thực hiện việc thả (đúng chuẩn Network)
        DropCoreServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void DropCoreServerRpc()
    {
        if (currentHeldCore != null)
        {
            currentHeldCore.RequestDrop(OwnerClientId);
            currentHeldCore = null; // Reset trên Server
            isCarryingCore.Value = false; // Sync sang client
            
            ClearHeldCoreClientRpc(); // Ép Client xóa
        }
    }

    [ClientRpc]
    private void ClearHeldCoreClientRpc()
    {
        currentHeldCore = null;
    }

    [ClientRpc]
    private void ResetClientCoreStateClientRpc()
    {
        // Chỉ reset biến local nếu đây là máy của chính người chơi đó
        if (IsOwner)
        {
            currentHeldCore = null;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetCarryingCoreServerRpc(bool state) => isCarryingCore.Value = state;

    [ServerRpc(RequireOwnership = false)]
    public void SetCanMoveServerRpc(bool state) => canMoveNet.Value = state;

    [ServerRpc(RequireOwnership = false)]
    private void UpdateCrouchStatusServerRpc(bool state) => isCrouchingNet.Value = state;
}