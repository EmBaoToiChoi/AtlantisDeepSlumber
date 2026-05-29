using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class InteractBox : MonoBehaviour
{
    public int stationIndex = 0; 
    public Transform crystalSnapPoint;
    private OptimizedNetworkMiniGame gameManager;
    
    private bool isPlayerInside = false; // Player đang đứng ở trạm
    private bool isUsingStation = false;
    private bool isCrystalLocked = false; // Đã lắp tinh thể chưa?
    private PlayerMovement localPlayerMovement;

    void Start() { gameManager = Object.FindFirstObjectByType<OptimizedNetworkMiniGame>(); }

    void Update()
    {
        if (isPlayerInside && localPlayerMovement != null && localPlayerMovement.IsOwner && Keyboard.current.eKey.wasPressedThisFrame)
        {
            // 1. Nếu cầm tinh thể và trạm chưa khóa -> Đặt tinh thể
            if (localPlayerMovement.isCarryingCore.Value && !isCrystalLocked && (stationIndex == 2 || stationIndex == 3))
            {
                SnapAndLockCrystalServerRpc(stationIndex);
            }
            // 2. Nếu không đặt tinh thể thì làm hành động Mini-game
            else
            {
                if (!isUsingStation) OpenStation();
                else ExitStation();
            }
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

    [ServerRpc(RequireOwnership = false)]
    private void SnapAndLockCrystalServerRpc(int index)
    {
        var core = localPlayerMovement.currentHeldCore;
        if (core != null)
        {
            // Đánh dấu đã khóa để không cho đặt lại
            isCrystalLocked = true; 
            core.isSnapped.Value = true; 

            // Tắt vật lý để không rơi xuyên đất
            Rigidbody rb = core.GetComponent<Rigidbody>();
            if (rb != null) { rb.linearVelocity = Vector3.zero; rb.isKinematic = true; }

            // Gắn vị trí
            core.transform.position = crystalSnapPoint.position;
            core.transform.rotation = crystalSnapPoint.rotation;
            
            // LƯU Ý: Nếu vẫn lỗi "Invalid parenting", BỎ DÒNG SetParent dưới đây
            // core.transform.SetParent(crystalSnapPoint); 

            localPlayerMovement.DropCore();
            
            if (index == 2) gameManager.station2HasCrystal.Value = true;
            else if (index == 3) gameManager.station3HasCrystal.Value = true;
        }
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