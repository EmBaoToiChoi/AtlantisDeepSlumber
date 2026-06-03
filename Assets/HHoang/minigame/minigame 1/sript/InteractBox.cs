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
        if (Application.isBatchMode) return;

        if (isPlayerInside && localPlayerInteraction != null)
        {
            if (localPlayerInteraction.IsOwner && Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame)
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
        
        // ĐÃ SỬA: Khóa chân LeoPlayer để bấm A/D không bị trượt ra ngoài
        var leoPlayer = localPlayerInteraction.GetComponent<LeoPlayer>();
        if (leoPlayer != null) leoPlayer.SetMovementLock(true);
    }

    private void ExitStation()
    {
        isUsingStation = false;
        RequestStationReleaseServerRpc(stationIndex);
        gameManager.ToggleMiniGame(stationIndex, false);
        
        // ĐÃ SỬA: Mở khóa chân cho LeoPlayer
        if (localPlayerInteraction != null)
        {
            var leoPlayer = localPlayerInteraction.GetComponent<LeoPlayer>();
            if (leoPlayer != null) leoPlayer.SetMovementLock(false);
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
        if (isCrystalLocked.Value) return; 

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(rpcParams.Receive.SenderClientId, out var client) && client.PlayerObject != null)
        {
            var playerInt = client.PlayerObject.GetComponent<PlayerInteraction>();
            if (playerInt != null && playerInt.currentHeldCore != null)
            {
                var core = playerInt.currentHeldCore;
                
                playerInt.ForceDropFromStation(); 
                
                core.LockToStation();
                core.transform.position = crystalSnapPoint.position;
                core.transform.rotation = crystalSnapPoint.rotation;
                
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