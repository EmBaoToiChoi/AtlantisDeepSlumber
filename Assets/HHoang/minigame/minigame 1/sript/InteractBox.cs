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
   public NetworkVariable<bool> isCrystalLocked = new NetworkVariable<bool>(false);
    private PlayerMovement localPlayerMovement;

    void Start() { gameManager = Object.FindFirstObjectByType<OptimizedNetworkMiniGame>(); }

    void Update()
    {
        if (isPlayerInside && localPlayerMovement != null && localPlayerMovement.IsOwner && Keyboard.current.eKey.wasPressedThisFrame)
        {
            // SỬA DÒNG NÀY: Thêm .Value vào đây
            if (localPlayerMovement.isCarryingCore.Value && !isCrystalLocked.Value && (stationIndex == 2 || stationIndex == 3))
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
            isCrystalLocked.Value = true;
            core.isSnapped.Value = true; 

            Rigidbody rb = core.GetComponent<Rigidbody>();
            if (rb != null) { rb.linearVelocity = Vector3.zero; rb.isKinematic = true; }

            // Gán vị trí
            core.transform.position = crystalSnapPoint.position;
            core.transform.rotation = crystalSnapPoint.rotation;
            
            // --- ĐÂY LÀ PHẦN QUAN TRỌNG ---
            // Gán cái point này vào script SnapFollow của tinh thể
            var snapFollow = core.GetComponent<CrystalSnapFollow>();
            if (snapFollow != null)
            {
                snapFollow.targetSnapPoint = crystalSnapPoint;
            }
            
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