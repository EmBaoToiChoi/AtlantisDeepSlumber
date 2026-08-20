using UnityEngine;
using Unity.Netcode;

public class PressurePlatePuzzleManager : NetworkBehaviour
{
    [Header("Puzzle Configuration")]
    [Tooltip("Danh sách các nút sàn bắt buộc phải đạp (kéo 2 phiến đá đúng vào đây)")]
    public PressurePlateTrigger[] requiredPlates;

    [Tooltip("Số lượng phiến đá cần đạp để mở cửa (Mặc định: 2)")]
    public int requiredPlateCount = 2;

    [Tooltip("Danh sách các cánh cửa sẽ mở khi giải xong câu đố")]
    public PushableDoor[] targetDoors;

    // Biến mạng đồng bộ trạng thái giải xong câu đố
    public NetworkVariable<bool> isSolvedNet = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private bool localIsSolved = false; // Dùng khi chơi offline

    [Header("Timer Configuration")]
    [Tooltip("Thời gian cửa tự động đóng lại sau khi mở (giây)")]
    public float closeDelay = 5f;

    private float timer = 0f;
    private bool isTimerActive = false;
    private bool doorsAreOpen = false;

    private void Awake()
    {
        EnsureReferences();
    }

    private void Start()
    {
        EnsureReferences();

        // Tự động kiểm tra và sửa lỗi nếu người dùng kéo nhầm Prefab Asset từ cửa sổ Project thay vì đối tượng Scene trong Hierarchy
        if (requiredPlates != null)
        {
            for (int i = 0; i < requiredPlates.Length; i++)
            {
                if (requiredPlates[i] != null && !requiredPlates[i].gameObject.scene.IsValid())
                {
                    string prefabName = requiredPlates[i].name;
                    Debug.LogWarning($"[PressurePlatePuzzleManager] Phát hiện requiredPlates[{i}] ('{prefabName}') là Prefab Asset từ Project. Đang tự động tìm đối tượng Scene tương ứng...");
                    
                    // Tìm đối tượng có cùng tên đang chạy trong Scene (Hierarchy)
                    PressurePlateTrigger[] scenePlates = FindObjectsOfType<PressurePlateTrigger>();
                    PressurePlateTrigger matchingScenePlate = null;
                    foreach (var sp in scenePlates)
                    {
                        if (sp.gameObject.scene.IsValid() && sp.name == prefabName)
                        {
                            matchingScenePlate = sp;
                            break;
                        }
                    }

                    if (matchingScenePlate != null)
                    {
                        requiredPlates[i] = matchingScenePlate;
                        Debug.Log($"[PressurePlatePuzzleManager] Đã tự động thay thế bằng đối tượng Scene: '{matchingScenePlate.name}'");
                    }
                    else
                    {
                        Debug.LogError($"[PressurePlatePuzzleManager] LỖI CỰC KỲ NGHIÊM TRỌNG: Không thể tìm thấy đối tượng '{prefabName}' nào trong Scene (Hierarchy) để gán cho requiredPlates[{i}]!");
                    }
                }
            }
        }

        if (targetDoors != null)
        {
            for (int i = 0; i < targetDoors.Length; i++)
            {
                if (targetDoors[i] != null && !targetDoors[i].gameObject.scene.IsValid())
                {
                    string prefabName = targetDoors[i].name;
                    Debug.LogWarning($"[PressurePlatePuzzleManager] Phát hiện targetDoors[{i}] ('{prefabName}') là Prefab Asset từ Project. Đang tự động tìm đối tượng Scene tương ứng...");
                    
                    PushableDoor[] sceneDoors = FindObjectsOfType<PushableDoor>();
                    PushableDoor matchingSceneDoor = null;
                    foreach (var sd in sceneDoors)
                    {
                        if (sd.gameObject.scene.IsValid() && sd.name == prefabName)
                        {
                            matchingSceneDoor = sd;
                            break;
                        }
                    }

                    if (matchingSceneDoor != null)
                    {
                        targetDoors[i] = matchingSceneDoor;
                        Debug.Log($"[PressurePlatePuzzleManager] Đã tự động thay thế cửa bằng đối tượng Scene: '{matchingSceneDoor.name}'");
                    }
                    else
                    {
                        Debug.LogError($"[PressurePlatePuzzleManager] LỖI CỰC KỲ NGHIÊM TRỌNG: Không thể tìm thấy cánh cửa '{prefabName}' nào trong Scene (Hierarchy) để gán cho targetDoors[{i}]!");
                    }
                }
            }
        }

        int requiredCount = requiredPlates != null ? requiredPlates.Length : 0;
        int doorCount = targetDoors != null ? targetDoors.Length : 0;
        Debug.Log($"[PressurePlatePuzzleManager] Khởi tạo puzzle. Số nút sàn yêu cầu: {requiredCount}, Số cửa điều khiển: {doorCount}");

        if (requiredCount == 0)
        {
            Debug.LogWarning("[PressurePlatePuzzleManager] CẢNH BÁO: Danh sách requiredPlates đang trống! Nút sàn sẽ không điều khiển cửa nào.");
        }
    }

    public void EnsureReferences()
    {
        if (requiredPlates == null || requiredPlates.Length == 0)
        {
            requiredPlates = FindObjectsByType<PressurePlateTrigger>(FindObjectsSortMode.None);
            if (requiredPlates != null && requiredPlates.Length > 0)
            {
                Debug.Log($"[PressurePlatePuzzleManager] Tự động tìm thấy {requiredPlates.Length} nút sàn (PressurePlateTrigger) trong Scene.");
            }
        }

        if (targetDoors == null || targetDoors.Length == 0)
        {
            targetDoors = FindObjectsByType<PushableDoor>(FindObjectsSortMode.None);
            if (targetDoors != null && targetDoors.Length > 0)
            {
                Debug.Log($"[PressurePlatePuzzleManager] Tự động tìm thấy {targetDoors.Length} cánh cửa (PushableDoor) trong Scene.");
            }
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

    public bool IsSolved()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            return isSolvedNet.Value;
        }
        return localIsSolved;
    }

    private void OnSolvedChanged(bool oldVal, bool newVal)
    {
        Debug.Log($"[PressurePlatePuzzleManager Client] Trạng thái giải câu đố thay đổi: {newVal}");
        if (newVal)
        {
            if (IntroDialogueController.Instance != null)
            {
                IntroDialogueController.Instance.StartNpcFollowingPlayer();
            }
        }
    }

    private void Update()
    {
        // Chỉ Server hoặc máy chơi offline mới kiểm tra trạng thái câu đố và cập nhật
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || IsServer)
        {
            bool allPressed = false;

            if (requiredPlates != null && requiredPlates.Length > 0)
            {
                if (requiredPlates.Length <= requiredPlateCount)
                {
                    // Nếu người dùng đã gán cụ thể danh sách phiến đá (ví dụ đúng 2 phiến đá)
                    allPressed = true;
                    foreach (var plate in requiredPlates)
                    {
                        if (plate == null || !plate.IsPressed)
                        {
                            allPressed = false;
                            break;
                        }
                    }
                }
                else
                {
                    // Nếu danh sách chứa nhiều hơn (ví dụ 4 phiến), chỉ cần đạp đủ requiredPlateCount (2) phiến
                    int pressed = 0;
                    foreach (var plate in requiredPlates)
                    {
                        if (plate != null && plate.IsPressed) pressed++;
                    }
                    allPressed = (pressed >= requiredPlateCount);
                }
            }
            else
            {
                // Fallback: Tìm tất cả các phiến đá trong Scene và đếm số lượng phiến đang bị đè
                var allPlates = FindObjectsByType<PressurePlateTrigger>(FindObjectsSortMode.None);
                int pressed = 0;
                foreach (var plate in allPlates)
                {
                    if (plate != null && plate.IsPressed) pressed++;
                }
                allPressed = (pressed >= requiredPlateCount);
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
                    if (allPressed)
                    {
                        if (IntroDialogueController.Instance != null)
                        {
                            IntroDialogueController.Instance.StartNpcFollowingPlayer();
                        }
                    }
                }

                // Khi bắt đầu giải xong câu đố (đạp đủ các nút yêu cầu)
                if (allPressed)
                {
                    Debug.Log($"[PressurePlatePuzzleManager] Đạp đủ nút sàn! Mở cửa.");
                    UpdateDoors(true);
                    doorsAreOpen = true;
                    isTimerActive = false; // Hủy đếm ngược tự đóng nếu đang chạy
                }
                else
                {
                    // Khi người chơi rời khỏi nút sàn (hết đè), bắt đầu đếm ngược trước khi đóng cửa
                    Debug.Log($"[PressurePlatePuzzleManager] Người chơi rời nút sàn. Bắt đầu đếm ngược {closeDelay} giây trước khi tự đóng cửa...");
                    timer = closeDelay;
                    isTimerActive = true;
                }
            }

            // Xử lý đếm ngược tự động đóng cửa
            if (isTimerActive && doorsAreOpen)
            {
                timer -= Time.deltaTime;
                if (timer <= 0f)
                {
                    Debug.Log($"[PressurePlatePuzzleManager] Hết {closeDelay} giây! Tự động đóng cửa.");
                    UpdateDoors(false);
                    doorsAreOpen = false;
                    isTimerActive = false;
                }
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
