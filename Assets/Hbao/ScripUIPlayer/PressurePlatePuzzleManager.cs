using UnityEngine;
using Unity.Netcode;

public class PressurePlatePuzzleManager : NetworkBehaviour
{
    [Header("Puzzle Configuration")]
    [Tooltip("Danh sách các nút sàn bắt buộc phải đạp (chọn từ các cục đá)")]
    public PressurePlateTrigger[] requiredPlates;

    [Tooltip("Danh sách các cánh cửa sẽ mở khi giải xong câu đố")]
    public PushableDoor[] targetDoors;

    // Biến mạng đồng bộ trạng thái giải xong câu đố
    public NetworkVariable<bool> isSolvedNet = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private bool localIsSolved = false; // Dùng khi chơi offline

    private void Start()
    {
        int requiredCount = requiredPlates != null ? requiredPlates.Length : 0;
        int doorCount = targetDoors != null ? targetDoors.Length : 0;
        Debug.Log($"[PressurePlatePuzzleManager] Khởi tạo puzzle. Số nút sàn yêu cầu: {requiredCount}, Số cửa điều khiển: {doorCount}");

        if (requiredCount == 0)
        {
            Debug.LogWarning("[PressurePlatePuzzleManager] CẢNH BÁO: Danh sách requiredPlates đang trống! Nút sàn sẽ không điều khiển cửa nào.");
        }
    }

    public override void OnNetworkSpawn()
    {
        isSolvedNet.OnValueChanged += OnSolvedChanged;
        
        if (IsServer)
        {
            isSolvedNet.Value = false;
        }
    }

    public override void OnNetworkDespawn()
    {
        isSolvedNet.OnValueChanged -= OnSolvedChanged;
    }

    private void OnSolvedChanged(bool oldVal, bool newVal)
    {
        Debug.Log($"[PressurePlatePuzzleManager Client] Trạng thái giải câu đố thay đổi: {newVal}");
        // Có thể thêm hiệu ứng âm thanh/hạt (SFX/VFX) ở đây nếu muốn
    }

    private void Update()
    {
        // Chỉ Server hoặc máy chơi offline mới kiểm tra trạng thái câu đố và cập nhật
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || IsServer)
        {
            bool allPressed = true;

            if (requiredPlates == null || requiredPlates.Length == 0)
            {
                allPressed = false;
            }
            else
            {
                foreach (var plate in requiredPlates)
                {
                    if (plate == null || !plate.IsPressed)
                    {
                        allPressed = false;
                        break;
                    }
                }
            }

            bool currentSolved = (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) ? isSolvedNet.Value : localIsSolved;

            if (allPressed != currentSolved)
            {
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                {
                    isSolvedNet.Value = allPressed;
                }
                else
                {
                    localIsSolved = allPressed;
                }

                Debug.Log($"[PressurePlatePuzzleManager] Trạng thái câu đố thay đổi -> Giải xong: {allPressed}. Đang cập nhật trạng thái cửa...");
                UpdateDoors(allPressed);
            }
        }
    }

    private void UpdateDoors(bool open)
    {
        if (targetDoors == null) return;
        foreach (var door in targetDoors)
        {
            if (door != null)
            {
                if (open)
                {
                    door.Open();
                }
                else
                {
                    door.Close();
                }
            }
        }
    }
}
