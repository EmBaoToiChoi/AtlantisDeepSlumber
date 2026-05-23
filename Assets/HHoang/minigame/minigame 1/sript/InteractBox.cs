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
                
                // 2. KHÓA CHÂN: Gọi ServerRpc để khóa di chuyển của chính mình
                if (localPlayerMovement != null)
                {
                    localPlayerMovement.SetCanMoveServerRpc(false);
                }
            }
            else
            {
                // TẮT UI và mở khóa chân (Nếu nhấn E lần nữa)
                ExitStation();
            }
        }

        // Nếu đang chơi mà bấm ESC thì tự động thoát ra ngoài và mở khóa chân
        if (isUsingStation && Input.GetKeyDown(KeyCode.Escape))
        {
            ExitStation();
        }
    }

    private void ExitStation()
    {
        gameManager.ToggleMiniGame(stationIndex, false);
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
                
                // Lấy script di chuyển của chính chủ máy này lưu lại để xử lý khóa/mở khóa
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
                if (isUsingStation)
                {
                    ExitStation();
                }
                
                // Xóa tham chiếu khi đi ra ngoài hẳn
                localPlayerMovement = null;
            }
        }
    }
}