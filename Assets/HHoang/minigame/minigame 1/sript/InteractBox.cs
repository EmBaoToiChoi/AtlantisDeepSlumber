using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class InteractBox : NetworkBehaviour
{
    public int stationIndex = 0; 
    public Transform crystalSnapPoint;
    private OptimizedNetworkMiniGame gameManager;
    
    private bool isPlayerInside = false; // Player đang đứng ở trạm
    private bool isUsingStation = false;
    // SỬA: Phải truyền giá trị mặc định vào constructor
    public NetworkVariable<bool> isCrystalLocked = new NetworkVariable<bool>(false);
    private PlayerMovement localPlayerMovement;

    void Start() { gameManager = Object.FindFirstObjectByType<OptimizedNetworkMiniGame>(); }

    void Update()
    {
        // Kiểm tra xem hệ thống input có sẵn sàng không
        if (Keyboard.current == null) return;

        // Kiểm tra điều kiện chính
        if (isPlayerInside && localPlayerMovement != null && localPlayerMovement.IsOwner && Keyboard.current.eKey.wasPressedThisFrame)
        {
            // 1. Logic đặt tinh thể (Crystal)
            if (localPlayerMovement.isCarryingCore.Value && localPlayerMovement.currentHeldCore != null)
            {
                // Kiểm tra trạm 2 và 3 có bị khóa chưa
                if (!isCrystalLocked.Value && (stationIndex == 2 || stationIndex == 3))
                {
                    SnapAndLockCrystalServerRpc(stationIndex);
                }
            }
            // 2. Logic tương tác Mini-game
            else
            {
                if (gameManager == null) 
                {
                    Debug.LogError("GameManager chưa được tìm thấy!");
                    return;
                }

                if (!isUsingStation) OpenStation();
                else ExitStation();
            }
        }
    }

    // Trong InteractBox.cs
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Tìm kiếm gameManager
        gameManager = Object.FindFirstObjectByType<OptimizedNetworkMiniGame>();
        
        // Kiểm tra an toàn: nếu không tìm thấy thì báo lỗi rõ ràng trong Console
        if (gameManager == null)
        {
            Debug.LogError($"[InteractBox] Không tìm thấy OptimizedNetworkMiniGame trên Scene! Hãy kiểm tra lại.");
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

// TRONG InteractBox.cs
    [ServerRpc(RequireOwnership = false)]
    private void SnapAndLockCrystalServerRpc(int index, ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(senderClientId, out var client))
        {
            var player = client.PlayerObject.GetComponent<PlayerMovement>();
            var core = player.currentHeldCore;

            if (core != null)
            {
                isCrystalLocked.Value = true;
                core.isSnapped.Value = true; 

                // Logic vật lý giữ nguyên
                Rigidbody rb = core.GetComponent<Rigidbody>();
                if (rb != null) { rb.linearVelocity = Vector3.zero; rb.isKinematic = true; }
                core.transform.position = crystalSnapPoint.position;
                core.transform.rotation = crystalSnapPoint.rotation;

                var snapFollow = core.GetComponent<CrystalSnapFollow>();
                if (snapFollow != null) snapFollow.targetSnapPoint = crystalSnapPoint;
                
                player.DropCore();
                
                // --- ĐÂY LÀ CHỖ CẦN SỬA ---
                // Thay vì gán trực tiếp gameManager.station2HasCrystal.Value = true;
                // Hãy gọi đúng hàm ServerRpc của gameManager:
                gameManager.SetStationCrystalStatusServerRpc(index, true); 
            }
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