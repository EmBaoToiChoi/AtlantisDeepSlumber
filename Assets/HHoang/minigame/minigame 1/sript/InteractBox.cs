using UnityEngine;
using Unity.Netcode;

public class InteractBox : MonoBehaviour
{
    [Header("Cấu hình Trạm")]
    [Tooltip("Điền số 1 nếu là Hộp 1, điền số 2 nếu là Hộp 2")]
    public int stationIndex = 1; 

    private OptimizedNetworkMiniGame gameManager;
    private bool isPlayerInside = false;
    private bool isUsingStation = false;

    // Lưu lại script di chuyển của người chơi cục bộ đang đứng trong vùng
    private PlayerMovement localPlayerMovement; 

    void Start()
    {
        gameManager = Object.FindFirstObjectByType<OptimizedNetworkMiniGame>();
    }

    void Update()
    {
        // Nếu người chơi đang đứng trong vùng hộp trắng và nhấn phím E
        if (isPlayerInside && Input.GetKeyDown(KeyCode.E))
        {
            if (!isUsingStation)
            {
                // 1. BẬT UI MINI-GAME
                gameManager.ToggleMiniGame(stationIndex, true);
                isUsingStation = true;
                
                // THÔNG BÁO CHO SERVER: Có 1 người bắt đầu tương tác
                gameManager.UpdateInteractingCountServerRpc(true);
                
                // 2. KHÓA CHÂN: Gọi ServerRpc để khóa di chuyển của chính mình
                if (localPlayerMovement != null)
                {
                    localPlayerMovement.SetCanMoveServerRpc(false);
                }
            }
            else
            {
                // TẮT UI và mở khóa chân
                ExitStation();
            }
        }

        // Nếu đang chơi mà bấm ESC thì tự động thoát ra ngoài
        if (isUsingStation && Input.GetKeyDown(KeyCode.Escape))
        {
            ExitStation();
        }
    }

    private void ExitStation()
    {
        if (!isUsingStation) return; // Tránh gọi nhiều lần

        gameManager.ToggleMiniGame(stationIndex, false);
        
        // THÔNG BÁO CHO SERVER: Có 1 người dừng tương tác
        gameManager.UpdateInteractingCountServerRpc(false);
        
        isUsingStation = false;

        // MỞ KHÓA CHÂN: Cho phép nhân vật đi lại bình thường
        if (localPlayerMovement != null)
        {
            localPlayerMovement.SetCanMoveServerRpc(true);
        }
    }

    // Hàm kiểm tra khi nhân vật chạy VÀO vùng hộp trắng
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

    // Hàm kiểm tra khi nhân vật chạy RA KHỎI vùng hộp trắng
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            var networkObject = other.GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.IsOwner)
            {
                isPlayerInside = false;
                // Nếu đi ra ngoài khi đang đứng ở trạm thì thoát trạm luôn
                if (isUsingStation)
                {
                    ExitStation();
                }
                localPlayerMovement = null;
            }
        }
    }
}