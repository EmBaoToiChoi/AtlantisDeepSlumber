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
    private NetworkBehaviour localPlayerController;

    public void TrySnapCrystal()
    {
        if ((stationIndex == 2 || stationIndex == 3) && !isCrystalLocked.Value)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                SnapAndLockCrystalServerRpc(stationIndex);
            }
        }
    }

    void Update()
    {
        if (Application.isBatchMode || localPlayerInteraction == null) return;

        if (localPlayerInteraction.IsOwner && Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
        {
            if (localPlayerInteraction.isCarryingCore.Value) return;

            if (gameManager != null)
            {
                if (!isUsingStation) OpenStation();
                else ExitStation();
            }
        }
    }

    private void OpenStation()
    {
        if (gameManager == null) return;

        ulong currentOwner = gameManager.GetOwner(stationIndex);
        if (currentOwner != ulong.MaxValue && currentOwner != NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log($"Trạm {stationIndex} đã có người xài, chặn lệnh mở Canvas!");
            return; 
        }

        isUsingStation = true;
        gameManager.ToggleMiniGame(stationIndex, true);

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