using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class InteractBox : MonoBehaviour
{
    [Header("Cấu hình Mini-game")]
    [Tooltip("Điền số 1 nếu là Hộp 1, điền số 2 nếu là Hộp 2")]
    public int stationIndex = 1; 

    private OptimizedNetworkMiniGame gameManager;
    private bool isPlayerInside = false;
    private bool isUsingStation = false;
    private PlayerMovement localPlayerMovement; 

    void Start()
    {
        gameManager = Object.FindFirstObjectByType<OptimizedNetworkMiniGame>();
    }

    void Update()
    {
        // Chỉ xử lý bật/tắt Mini-game
        if (isPlayerInside && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (!isUsingStation) OpenStation();
            else ExitStation();
        }
    }

    private void OpenStation()
    {
        gameManager.ToggleMiniGame(stationIndex, true);
        isUsingStation = true;
        gameManager.UpdateInteractingCountServerRpc(true);
        
        if (localPlayerMovement != null)
            localPlayerMovement.SetCanMoveServerRpc(false);
    }

    private void ExitStation()
    {
        if (!isUsingStation) return;

        gameManager.ToggleMiniGame(stationIndex, false);
        gameManager.UpdateInteractingCountServerRpc(false);
        isUsingStation = false;

        if (localPlayerMovement != null)
            localPlayerMovement.SetCanMoveServerRpc(true);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            var networkObject = other.GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.IsOwner)
            {
                isPlayerInside = true;
                localPlayerMovement = other.GetComponent<PlayerMovement>();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            var networkObject = other.GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.IsOwner)
            {
                isPlayerInside = false;
                if (isUsingStation) ExitStation();
                localPlayerMovement = null;
            }
        }
    }
}