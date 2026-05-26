using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class InteractBox : MonoBehaviour // Không kế thừa NetworkBehaviour để tránh lỗi Permission
{
    public int stationIndex = 0; 
    private OptimizedNetworkMiniGame gameManager;
    private bool isPlayerInside = false;
    private bool isUsingStation = false;
    private PlayerMovement localPlayerMovement;

    void Start() { gameManager = Object.FindFirstObjectByType<OptimizedNetworkMiniGame>(); }

    void Update()
    {
        // Kiểm tra xem player có đang đứng trong vùng và nhấn E không
        if (isPlayerInside && localPlayerMovement != null && localPlayerMovement.IsOwner && Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (!isUsingStation) OpenStation();
            else ExitStation();
        }
    }

    private void OpenStation()
    {
        isUsingStation = true;
        gameManager.RequestStationAccessServerRpc(stationIndex, localPlayerMovement.OwnerClientId);
        gameManager.ToggleMiniGame(stationIndex, true);
        localPlayerMovement.SetCanMoveServerRpc(false);
    }

    private void ExitStation()
    {
        isUsingStation = false;
        gameManager.ReleaseStationServerRpc(stationIndex, localPlayerMovement.OwnerClientId);
        gameManager.ToggleMiniGame(stationIndex, false);
        localPlayerMovement.SetCanMoveServerRpc(true);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && other.GetComponent<NetworkObject>().IsOwner)
        {
            isPlayerInside = true;
            localPlayerMovement = other.GetComponent<PlayerMovement>();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && other.GetComponent<NetworkObject>().IsOwner)
        {
            if (isUsingStation) ExitStation();
            isPlayerInside = false;
            localPlayerMovement = null;
        }
    }
}