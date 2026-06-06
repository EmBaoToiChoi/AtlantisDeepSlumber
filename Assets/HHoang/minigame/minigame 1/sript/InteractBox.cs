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
    // Dùng NetworkBehaviour để tóm gọn mọi script nhân vật (Leo, Elena, v.v...)
    private NetworkBehaviour localPlayerController;

    void Update()
    {
        if (Application.isBatchMode || localPlayerInteraction == null) return;

        if (localPlayerInteraction.IsOwner && Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
        {
            // Kiểm tra ngọc bằng NetworkVariable thay vì biến cục bộ
            bool isHoldingCore = localPlayerInteraction.isCarryingCore.Value && localPlayerInteraction.heldCoreNetworkId.Value != ulong.MaxValue;

            if (isHoldingCore && (stationIndex == 2 || stationIndex == 3) && !isCrystalLocked.Value)
            {
                SnapAndLockCrystalServerRpc(stationIndex);
            }
            else if (gameManager != null)
            {
                if (!isUsingStation) OpenStation();
                else ExitStation();
            }
        }
    }

    private void OpenStation()
    {
        if (gameManager == null) return;
        isUsingStation = true;
        gameManager.ToggleMiniGame(stationIndex, true);

        // Khóa mọi script điều khiển nhân vật
        if (localPlayerController != null) localPlayerController.enabled = false;
    }

    private void ExitStation()
    {
        if (gameManager == null) return;
        isUsingStation = false;
        gameManager.ToggleMiniGame(stationIndex, false);

        // Mở khóa script điều khiển
        if (localPlayerController != null) localPlayerController.enabled = true;
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void SnapAndLockCrystalServerRpc(int index, ServerRpcParams rpcParams = default)
    {
        if (isCrystalLocked.Value) return; 

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(rpcParams.Receive.SenderClientId, out var client) && client.PlayerObject != null)
        {
            var playerInt = client.PlayerObject.GetComponent<PlayerInteraction>();
            
            // Lấy ID ngọc từ NetworkVariable của Player
            ulong netId = playerInt.heldCoreNetworkId.Value;
            if (netId != ulong.MaxValue && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(netId, out var netObj))
            {
                var core = netObj.GetComponent<CrystalCore>();
                
                playerInt.ForceDropFromStation(); 
                core.LockToStation();
                
                core.transform.position = crystalSnapPoint.position;
                core.transform.rotation = crystalSnapPoint.rotation;
                
                var snapFollow = core.GetComponent<CrystalSnapFollow>();
                if (snapFollow != null) snapFollow.targetSnapPoint = crystalSnapPoint;
                
                isCrystalLocked.Value = true;
                gameManager.SetStationCrystalStatus(index, true); 
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<PlayerInteraction>(out var pInt) && pInt.IsOwner)
        {
            isPlayerInside = true;
            localPlayerInteraction = pInt;
            pInt.currentInteractBox = this; 

            // Tự động tìm script điều khiển (LeoPlayer, ElenaPlayer, v.v...)
            // Mày chỉ cần đảm bảo script điều khiển cũng là NetworkBehaviour
            localPlayerController = other.GetComponent<NetworkBehaviour>(); 
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<PlayerInteraction>(out var pInt) && pInt.IsOwner)
        {
            if (isUsingStation) ExitStation();
            isPlayerInside = false;
            
            if (pInt.currentInteractBox == this) pInt.currentInteractBox = null;
            localPlayerInteraction = null;
            localPlayerController = null;
        }
    }
}