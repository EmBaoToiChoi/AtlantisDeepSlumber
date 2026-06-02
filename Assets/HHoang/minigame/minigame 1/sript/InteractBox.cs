using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class InteractBox : NetworkBehaviour
{
    public int stationIndex = 0; 
    public Transform crystalSnapPoint;
    private OptimizedNetworkMiniGame gameManager;
    
    private bool isPlayerInside = false;
    private bool isUsingStation = false;
    public NetworkVariable<bool> isCrystalLocked = new NetworkVariable<bool>(false);
    
    private PlayerInteraction localPlayerInteraction;

    void Start() 
    { 
        // Thay vì FindFirstObjectByType (có thể chậm), nếu miniGame là singleton hoặc có ref thì tốt hơn
        gameManager = Object.FindFirstObjectByType<OptimizedNetworkMiniGame>(); 
    }

    void Update()
    {
        // 1. Kiểm tra an toàn cho Dedicated Server
        if (Keyboard.current == null) return;

        if (isPlayerInside && localPlayerInteraction != null && localPlayerInteraction.IsOwner && Keyboard.current.eKey.wasPressedThisFrame)
        {
            // Logic đặt tinh thể
            if (localPlayerInteraction.isCarryingCore.Value && localPlayerInteraction.currentHeldCore != null)
            {
                if (!isCrystalLocked.Value && (stationIndex == 2 || stationIndex == 3))
                {
                    SnapAndLockCrystalServerRpc(stationIndex);
                }
            }
            // Logic tương tác Mini-game
            else if (gameManager != null)
            {
                if (!isUsingStation) OpenStation();
                else ExitStation();
            }
        }
    }

    private void OpenStation()
    {
        isUsingStation = true;
        // Gửi lệnh lên server
        RequestStationAccessServerRpc(stationIndex);
        gameManager.ToggleMiniGame(stationIndex, true);
        
        // Vô hiệu hóa di chuyển
        localPlayerInteraction.GetComponent<PlayerMovement>()?.SetCanMove(false);
    }

    private void ExitStation()
    {
        isUsingStation = false;
        RequestStationReleaseServerRpc(stationIndex);
        gameManager.ToggleMiniGame(stationIndex, false);
        
        localPlayerInteraction.GetComponent<PlayerMovement>()?.SetCanMove(true);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestStationAccessServerRpc(int index, ServerRpcParams rpcParams = default)
    {
        // Gọi trực tiếp hàm logic (chỉ chạy trên Server)
        gameManager.HandleStationAccess(index, rpcParams.Receive.SenderClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestStationReleaseServerRpc(int index, ServerRpcParams rpcParams = default)
    {
        // Tương tự, tạo một hàm HandleStationRelease trong MiniGame
        gameManager.HandleStationRelease(index, rpcParams.Receive.SenderClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SnapAndLockCrystalServerRpc(int index, ServerRpcParams rpcParams = default)
    {
        if (NetworkManager.ConnectedClients.TryGetValue(rpcParams.Receive.SenderClientId, out var client) && client.PlayerObject != null)
        {
            var playerInt = client.PlayerObject.GetComponent<PlayerInteraction>();
            var core = playerInt.currentHeldCore;

            if (core != null)
            {
                isCrystalLocked.Value = true;
                core.isSnapped.Value = true;

                Rigidbody rb = core.GetComponent<Rigidbody>();
                if (rb != null) { rb.linearVelocity = Vector3.zero; rb.isKinematic = true; }
                
                core.transform.SetPositionAndRotation(crystalSnapPoint.position, crystalSnapPoint.rotation);
                
                playerInt.DropCore(); 
                gameManager.SetStationCrystalStatusServerRpc(index, true);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && other.TryGetComponent<NetworkObject>(out var netObj) && netObj.IsOwner)
        {
            isPlayerInside = true;
            localPlayerInteraction = other.GetComponent<PlayerInteraction>();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Khi rời trigger, nếu đang dùng máy thì phải thoát
        if (other.CompareTag("Player") && other.TryGetComponent<NetworkObject>(out var netObj) && netObj.IsOwner)
        {
            if (isUsingStation) ExitStation();
            isPlayerInside = false;
            localPlayerInteraction = null;
        }
    }
}