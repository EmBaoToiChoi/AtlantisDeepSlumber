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
    
    // THAY ĐỔI: Tham chiếu đến PlayerInteraction thay vì PlayerMovement
    private PlayerInteraction localPlayerInteraction;

    void Start() { gameManager = Object.FindFirstObjectByType<OptimizedNetworkMiniGame>(); }

    void Update()
    {
        if (Keyboard.current == null) return;

        // Kiểm tra điều kiện chính với PlayerInteraction
        if (isPlayerInside && localPlayerInteraction != null && localPlayerInteraction.IsOwner && Keyboard.current.eKey.wasPressedThisFrame)
        {
            // 1. Logic đặt tinh thể (Crystal)
            if (localPlayerInteraction.isCarryingCore.Value && localPlayerInteraction.currentHeldCore != null)
            {
                if (!isCrystalLocked.Value && (stationIndex == 2 || stationIndex == 3))
                {
                    SnapAndLockCrystalServerRpc(stationIndex);
                }
            }
            // 2. Logic tương tác Mini-game
            else
            {
                if (gameManager == null) return;
                if (!isUsingStation) OpenStation();
                else ExitStation();
            }
        }
    }

    private void OpenStation()
    {
        isUsingStation = true;
        gameManager.RequestStationAccessServerRpc(stationIndex, localPlayerInteraction.OwnerClientId);
        gameManager.ToggleMiniGame(stationIndex, true);
        
        // Gọi đến thành phần di chuyển để vô hiệu hóa nó
        var movement = localPlayerInteraction.GetComponent<PlayerMovement>();
        if (movement != null) movement.SetCanMoveServerRpc(false);
    }

    private void ExitStation()
    {
        isUsingStation = false;
        gameManager.ReleaseStationServerRpc(stationIndex, localPlayerInteraction.OwnerClientId);
        gameManager.ToggleMiniGame(stationIndex, false);
        
        var movement = localPlayerInteraction.GetComponent<PlayerMovement>();
        if (movement != null) movement.SetCanMoveServerRpc(true);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SnapAndLockCrystalServerRpc(int index, ServerRpcParams rpcParams = default)
    {
        var senderClientId = rpcParams.Receive.SenderClientId;
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(senderClientId, out var client))
        {
            var playerInt = client.PlayerObject.GetComponent<PlayerInteraction>();
            var core = playerInt.currentHeldCore;

            if (core != null)
            {
                isCrystalLocked.Value = true;
                core.isSnapped.Value = true;

                Rigidbody rb = core.GetComponent<Rigidbody>();
                if (rb != null) { rb.linearVelocity = Vector3.zero; rb.isKinematic = true; }
                core.transform.position = crystalSnapPoint.position;
                core.transform.rotation = crystalSnapPoint.rotation;
                
                playerInt.DropCore(); 
                gameManager.SetStationCrystalStatusServerRpc(index, true);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && other.GetComponent<NetworkObject>().IsOwner)
        {
            isPlayerInside = true;
            localPlayerInteraction = other.GetComponent<PlayerInteraction>();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && other.GetComponent<NetworkObject>().IsOwner)
        {
            if (isUsingStation) ExitStation();
            isPlayerInside = false;
            localPlayerInteraction = null;
        }
    }
}