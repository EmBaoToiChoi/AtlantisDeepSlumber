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

        // --- ĐOẠN NÀY LÀ CHỐT CHẶN CHỐNG CƯỚP TRẠM ---
        ulong currentOwner = gameManager.GetOwner(stationIndex);
        // Nếu trạm đã có người xài (không phải ulong.MaxValue) VÀ người đó không phải là mình
        if (currentOwner != ulong.MaxValue && currentOwner != NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log($"Trạm {stationIndex} đã có người xài, chặn lệnh mở Canvas!");
            return; // Đuổi về, không chạy code bên dưới nữa
        }
        // ---------------------------------------------

        isUsingStation = true;
        gameManager.ToggleMiniGame(stationIndex, true);

        // Tắt điều khiển nhân vật
        if (localPlayerController != null) 
        {
            var mover = localPlayerController.GetComponent<MovementController>();
            if(mover != null) mover.ToggleMovement(false);
        }
    }

    private void ExitStation()
    {
        if (gameManager == null) return;
        isUsingStation = false;
        gameManager.ToggleMiniGame(stationIndex, false);

        // Bật lại điều khiển
        if (localPlayerController != null) 
        {
            var mover = localPlayerController.GetComponent<MovementController>();
            if(mover != null) mover.ToggleMovement(true);
        }
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