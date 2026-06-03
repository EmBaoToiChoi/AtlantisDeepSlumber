using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class InteractBox : NetworkBehaviour
{
    public int stationIndex = 0; 
    public Transform crystalSnapPoint;
    [SerializeField] private OptimizedNetworkMiniGame gameManager; 
    
    private bool isPlayerInside = false;
    private bool isUsingStation = false;
    public NetworkVariable<bool> isCrystalLocked = new NetworkVariable<bool>(false);
    
    private PlayerInteraction localPlayerInteraction;

    void Update()
    {
        // BỎ DÒNG NÀY: if (!IsOwner) return; 
        // Vì InteractBox là trạm, không phải nhân vật, nên không có owner là người chơi.

        // 2. Không xử lý input nếu đang chạy Headless
        if (Application.isBatchMode) return;

        // Chỉ kiểm tra khi có người chơi bên trong và người chơi đó là chính mình (Local Player)
        if (isPlayerInside && localPlayerInteraction != null)
        {
            // Kiểm tra xem localPlayerInteraction có đúng là người chơi hiện tại trên máy này không
            if (localPlayerInteraction.IsOwner && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                if (localPlayerInteraction.isCarryingCore.Value && localPlayerInteraction.currentHeldCore != null)
                {
                    if (!isCrystalLocked.Value && (stationIndex == 2 || stationIndex == 3))
                        SnapAndLockCrystalServerRpc(stationIndex);
                }
                else if (gameManager != null)
                {
                    if (!isUsingStation) OpenStation();
                    else ExitStation();
                }
            }
        }
    }

    private void OpenStation()
    {
        if (gameManager == null) 
        {
            Debug.LogError($"[InteractBox] GameManager chưa được gán tại station {stationIndex}!");
            return;
        }
        isUsingStation = true;
        RequestStationAccessServerRpc(stationIndex);
        gameManager.ToggleMiniGame(stationIndex, true);
        localPlayerInteraction.GetComponent<PlayerMovement>()?.SetCanMove(false);
    }

    private void ExitStation()
    {
        isUsingStation = false;
        RequestStationReleaseServerRpc(stationIndex);
        gameManager.ToggleMiniGame(stationIndex, false);
        
        // An toàn tuyệt đối
        if (localPlayerInteraction != null)
        {
            var movement = localPlayerInteraction.GetComponent<PlayerMovement>();
            if (movement != null) 
            {
                movement.SetCanMove(true);
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestStationAccessServerRpc(int index, ServerRpcParams rpcParams = default)
    {
        gameManager.HandleStationAccess(index, rpcParams.Receive.SenderClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestStationReleaseServerRpc(int index, ServerRpcParams rpcParams = default)
    {
        gameManager.HandleStationRelease(index, rpcParams.Receive.SenderClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SnapAndLockCrystalServerRpc(int index, ServerRpcParams rpcParams = default)
    {
        // Kiểm tra lại lần nữa ngay trên Server để tránh race condition
        if (isCrystalLocked.Value) return; 

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(rpcParams.Receive.SenderClientId, out var client) && client.PlayerObject != null)
        {
            var playerInt = client.PlayerObject.GetComponent<PlayerInteraction>();
            if (playerInt.currentHeldCore != null)
            {
                isCrystalLocked.Value = true; // Cập nhật này sẽ khóa ngay lập tức cho các request sau
                playerInt.currentHeldCore.isSnapped.Value = true;
                playerInt.DropCore(); 
                gameManager.SetStationCrystalStatusServerRpc(index, true);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<PlayerInteraction>(out var pInt) && pInt.IsOwner)
        {
            isPlayerInside = true;
            localPlayerInteraction = pInt;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<PlayerInteraction>(out var pInt) && pInt.IsOwner)
        {
            if (isUsingStation) ExitStation();
            isPlayerInside = false;
            localPlayerInteraction = null;
        }
    }
}