using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class InteractBox : NetworkBehaviour
{
    [Header("Cấu hình Mini-game")]
    public int stationIndex = 1; 

    private OptimizedNetworkMiniGame gameManager;
    private bool isPlayerInside = false;
    private bool isUsingStation = false;
    private PlayerMovement localPlayerMovement; 

    void Start()
    {
        // Cách tìm gameManager an toàn hơn cho NetworkBehaviour
        gameManager = Object.FindFirstObjectByType<OptimizedNetworkMiniGame>();
    }

    void Update()
    {
        // Chỉ owner mới được điều khiển trạm của họ
       // if (!IsOwner) return;

        if (isPlayerInside && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (!isUsingStation) OpenStation();
            else ExitStation();
        }
    }

    private void OpenStation()
    {
        if (gameManager == null) return;

        // Lấy ID chuẩn của người chơi này
        ulong myId = GetComponentInParent<NetworkObject>().OwnerClientId; 
        
        // Chỉ gọi 1 lần duy nhất lên Server
        gameManager.RequestStationAccessServerRpc(stationIndex, myId);
        
        // Cập nhật trạng thái cục bộ
        gameManager.ToggleMiniGame(stationIndex, true);
        isUsingStation = true;
        
        if (localPlayerMovement != null)
            localPlayerMovement.SetCanMoveServerRpc(false);
    }

    private void ExitStation()
    {
        if (gameManager == null) return;

        gameManager.ReleaseStationServerRpc(stationIndex, OwnerClientId);
        gameManager.ToggleMiniGame(stationIndex, false);
        isUsingStation = false;

        if (localPlayerMovement != null)
            localPlayerMovement.SetCanMoveServerRpc(true);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Bỏ qua check IsOwner ở đây, chỉ cần là Player là được
        if (other.CompareTag("Player"))
        {
            var networkObject = other.GetComponent<NetworkObject>();
            if (networkObject != null)
            {
                isPlayerInside = true;
                localPlayerMovement = other.GetComponent<PlayerMovement>();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var networkObject = other.GetComponent<NetworkObject>();
        if (networkObject != null && networkObject.IsOwner && other.CompareTag("Player"))
        {
            isPlayerInside = false;
            if (isUsingStation) ExitStation();
            localPlayerMovement = null;
        }
    }
}