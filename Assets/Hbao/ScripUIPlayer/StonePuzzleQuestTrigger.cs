using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class StonePuzzleQuestTrigger : NetworkBehaviour, IQuestTrigger
{
    public bool IsQuestCompleted => (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) ? isQuestCompletedNet.Value : isQuestCompleted;
    public bool IsQuestActive => (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) ? isQuestActive.Value : hasTriggeredQuest;

    [Header("Quest Prerequisite Settings")]
    [Tooltip("Nhiệm vụ tiền đề bắt buộc phải hoàn thành trước khi nhiệm vụ này được hiển thị/kích hoạt")]
    public MonoBehaviour prerequisiteQuest;

    public bool IsPrerequisiteCompleted()
    {
        if (prerequisiteQuest == null) return true;
        if (prerequisiteQuest is IQuestTrigger quest) return quest.IsQuestCompleted;
        if (prerequisiteQuest is BridgeCollapseTrigger bridge) return bridge.IsBridgeRepaired();
        
        var bridgeComp = prerequisiteQuest.GetComponent<BridgeCollapseTrigger>() ?? 
                         prerequisiteQuest.GetComponentInParent<BridgeCollapseTrigger>() ?? 
                         prerequisiteQuest.GetComponentInChildren<BridgeCollapseTrigger>();
        if (bridgeComp != null) return bridgeComp.IsBridgeRepaired();

        var trigger = prerequisiteQuest.GetComponent<IQuestTrigger>() ?? 
                      prerequisiteQuest.GetComponentInParent<IQuestTrigger>() ?? 
                      prerequisiteQuest.GetComponentInChildren<IQuestTrigger>();
        if (trigger != null) return trigger.IsQuestCompleted;
        return true;
    }

    [Header("Puzzle Configuration")]
    [Tooltip("Tham chiếu tới PressurePlatePuzzleManager quản lý các nút sàn")]
    public PressurePlatePuzzleManager puzzleManager;

    [Tooltip("Danh sách 2 phiến đá đúng (Kéo thả 2 phiến đá vào đây để chỉ định)")]
    public PressurePlateTrigger[] requiredPlates;

    [Tooltip("Số lượng phiến đá cần đạp để mở cửa và hoàn thành nhiệm vụ (Mặc định: 2)")]
    public int requiredPlatesCount = 2;

    [Header("Quest UI Settings")]
    [Tooltip("Tiêu đề nhiệm vụ hiển thị trên UI (Ví dụ: ĐẨY ĐÁ)")]
    public string questTitle = "ĐẨY ĐÁ";

    [Tooltip("Icon nhiệm vụ hiển thị bên cạnh tiêu đề")]
    public Sprite questIconSprite;

    [Tooltip("Nội dung mô tả nhiệm vụ hiển thị trên UI")]
    [TextArea(3, 5)]
    public string questDescription = "Tìm kiếm các phiến đá có hình dạng giống trên cửa để đẩy chúng ra và đạp lên để mở cửa.";

    [Tooltip("Tự động kích hoạt nhiệm vụ khi vào game hoặc khi nhiệm vụ xây cầu hoàn thành")]
    public bool autoStartIfPrerequisiteMet = true;

    [Tooltip("Ẩn bảng nhiệm vụ khi người chơi rời khỏi vùng Trigger (chỉ áp dụng khi chơi Offline/nếu muốn)")]
    public bool hideWhenExitTrigger = false;

    [Tooltip("Thời gian chờ trước khi ẩn bảng nhiệm vụ sau khi giải xong (giây)")]
    public float hideDelayAfterComplete = 3f;

    [Tooltip("Khoảng thời gian (giây) giữa các lần kiểm tra cập nhật tiến trình trên UI")]
    public float checkInterval = 0.2f;

    // Biến mạng đồng bộ trạng thái kích hoạt nhiệm vụ cho toàn bộ Client
    public NetworkVariable<bool> isQuestActive = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Biến mạng đồng bộ trạng thái hoàn thành nhiệm vụ cho toàn bộ Client
    public NetworkVariable<bool> isQuestCompletedNet = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private IPlayerHUDTarget localPlayer;
    private PlayerHUDController localHudCtl;
    private Collider triggerCollider;

    private bool isPlayerInside = false;
    private bool hasTriggeredQuest = false;
    private bool isQuestCompleted = false;
    
    private float nextPlayerSearchTime = 0f;
    private float nextCheckTime = 0f;
    private int lastPressedCount = -1;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider == null)
        {
            Debug.LogError($"[StonePuzzleQuestTrigger] LỖI: GameObject '{gameObject.name}' bắt buộc phải có một Collider (ví dụ Box Collider) được bật 'Is Trigger'!");
        }
        else if (!triggerCollider.isTrigger)
        {
            triggerCollider.isTrigger = true;
            Debug.LogWarning($"[StonePuzzleQuestTrigger] Tự động chuyển Collider trên '{gameObject.name}' thành Trigger.");
        }
        EnsurePuzzleManager();
    }

    private void Start()
    {
        EnsurePuzzleManager();
    }

    public void EnsurePuzzleManager()
    {
        if (puzzleManager == null)
        {
            puzzleManager = FindAnyObjectByType<PressurePlatePuzzleManager>();
        }

        if (puzzleManager == null)
        {
            var existingManagers = FindObjectsByType<PressurePlatePuzzleManager>(FindObjectsSortMode.None);
            if (existingManagers != null && existingManagers.Length > 0)
            {
                puzzleManager = existingManagers[0];
            }
            else
            {
                GameObject puzzleObj = new GameObject("PressurePlatePuzzleManager_AutoManager");
                puzzleManager = puzzleObj.AddComponent<PressurePlatePuzzleManager>();
                Debug.Log("[StonePuzzleQuestTrigger] Tự động khởi tạo PressurePlatePuzzleManager GameObject trong Scene.");
            }
        }

        if (puzzleManager != null)
        {
            puzzleManager.requiredPlateCount = requiredPlatesCount;

            if (requiredPlates != null && requiredPlates.Length > 0)
            {
                puzzleManager.requiredPlates = requiredPlates;
            }

            if (puzzleManager.targetDoors == null || puzzleManager.targetDoors.Length == 0)
            {
                puzzleManager.targetDoors = FindObjectsByType<PushableDoor>(FindObjectsSortMode.None);
                Debug.Log($"[StonePuzzleQuestTrigger] Tự động gán {puzzleManager.targetDoors.Length} cánh cửa (PushableDoor) cho puzzleManager.");
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        isQuestActive.OnValueChanged += OnQuestActiveChanged;
        isQuestCompletedNet.OnValueChanged += OnQuestCompletedChanged;
        
        if (isQuestCompletedNet.Value)
        {
            isQuestCompleted = true;
        }
        else if (isQuestActive.Value && IsPrerequisiteCompleted())
        {
            hasTriggeredQuest = true;
            UpdateQuestProgressUI();
        }
    }

    public override void OnNetworkDespawn()
    {
        isQuestActive.OnValueChanged -= OnQuestActiveChanged;
        isQuestCompletedNet.OnValueChanged -= OnQuestCompletedChanged;
    }

    private void OnQuestCompletedChanged(bool oldVal, bool newVal)
    {
        if (newVal)
        {
            isQuestCompleted = true;
            ShowCompleteQuestUI();
        }
    }

    private void OnQuestActiveChanged(bool oldVal, bool newVal)
    {
        if (newVal && IsPrerequisiteCompleted())
        {
            hasTriggeredQuest = true;
            lastPressedCount = -1; // Ép cập nhật UI lập tức
            UpdateQuestProgressUI();
            Debug.Log("[StonePuzzleQuestTrigger] Nhiệm vụ đã được kích hoạt đồng bộ từ mạng!");
        }
    }

    private void Update()
    {
        if (isQuestCompleted) return;

        EnsurePuzzleManager();

        if (!IsPrerequisiteCompleted()) return;

        bool isNetwork = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        // Tự động kích hoạt nhiệm vụ khi đã thỏa điều kiện tiên quyết (sửa cầu xong)
        if (autoStartIfPrerequisiteMet && !IsQuestActive)
        {
            if (!isNetwork || IsServer)
            {
                if (isNetwork) isQuestActive.Value = true;
                hasTriggeredQuest = true;
                UpdateQuestProgressUI();
                Debug.Log("[StonePuzzleQuestTrigger] Tự động kích hoạt nhiệm vụ ĐẨY ĐÁ sau khi sửa cầu xong!");
            }
        }

        if (localPlayer == null)
        {
            FindLocalPlayer();
        }

        // Xác định nhiệm vụ đã active hay chưa
        bool active = isNetwork ? isQuestActive.Value : hasTriggeredQuest;

        if (active)
        {
            // Kiểm tra trạng thái giải câu đố của puzzleManager
            if (puzzleManager != null && puzzleManager.IsSolved())
            {
                CompleteQuest();
                return;
            }

            // Định kỳ cập nhật tiến trình UI để tránh overhead mỗi frame
            if (Time.time >= nextCheckTime)
            {
                nextCheckTime = Time.time + checkInterval;
                UpdateQuestProgressUI();
            }
        }
    }

    private void UpdateQuestProgressUI()
    {
        if (!IsPrerequisiteCompleted() || isQuestCompleted) return;

        EnsurePuzzleManager();

        if (localHudCtl == null)
        {
            localHudCtl = FindAnyObjectByType<PlayerHUDController>();
        }

        if (localHudCtl != null)
        {
            bool isNetwork = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
            bool active = isNetwork ? isQuestActive.Value : hasTriggeredQuest;
            bool shouldShow = active || isPlayerInside || !hideWhenExitTrigger;

            if (shouldShow)
            {
                localHudCtl.ShowQuest(true, this);
                localHudCtl.UpdateQuestDescription(questDescription, this);
                localHudCtl.UpdateQuestTitle(questTitle, this);
                localHudCtl.UpdateQuestIcon(questIconSprite, this);

                // Tính toán số lượng nút sàn đang được kích hoạt
                int pressedCount = 0;
                int totalCount = requiredPlatesCount; // Luôn là 2 phiến đá

                if (puzzleManager != null && puzzleManager.requiredPlates != null && puzzleManager.requiredPlates.Length > 0)
                {
                    if (puzzleManager.requiredPlates.Length <= requiredPlatesCount)
                    {
                        totalCount = puzzleManager.requiredPlates.Length;
                        foreach (var plate in puzzleManager.requiredPlates)
                        {
                            if (plate != null && plate.IsPressed) pressedCount++;
                        }
                    }
                    else
                    {
                        totalCount = requiredPlatesCount;
                        foreach (var plate in puzzleManager.requiredPlates)
                        {
                            if (plate != null && plate.IsPressed) pressedCount++;
                        }
                    }
                }
                else if (requiredPlates != null && requiredPlates.Length > 0)
                {
                    totalCount = requiredPlates.Length;
                    foreach (var plate in requiredPlates)
                    {
                        if (plate != null && plate.IsPressed) pressedCount++;
                    }
                }
                else
                {
                    totalCount = requiredPlatesCount;
                    var plates = FindObjectsByType<PressurePlateTrigger>(FindObjectsSortMode.None);
                    foreach (var plate in plates)
                    {
                        if (plate != null && plate.IsPressed) pressedCount++;
                    }
                }

                // Cập nhật tiến độ chính xác (ví dụ 0/2, 1/2, 2/2) ngay lập tức
                pressedCount = Mathf.Min(pressedCount, totalCount);
                lastPressedCount = pressedCount;
                localHudCtl.UpdateQuestProgress(pressedCount, totalCount, this);
            }
            else
            {
                localHudCtl.ShowQuest(false, this);
            }
        }
    }

    private void CompleteQuest()
    {
        if (isQuestCompleted) return;
        isQuestCompleted = true;

        bool isNetwork = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (isNetwork && IsServer)
        {
            isQuestCompletedNet.Value = true;
            isQuestActive.Value = false;
        }

        Debug.Log("[StonePuzzleQuestTrigger] Câu đố đã giải xong! Nhiệm vụ hoàn thành.");

        // Đảm bảo mở cửa khi giải xong câu đố
        if (puzzleManager != null && puzzleManager.targetDoors != null && puzzleManager.targetDoors.Length > 0)
        {
            foreach (var door in puzzleManager.targetDoors)
            {
                if (door != null) door.Open();
            }
        }
        else
        {
            var doors = FindObjectsByType<PushableDoor>(FindObjectsSortMode.None);
            foreach (var door in doors)
            {
                if (door != null) door.Open();
            }
        }

        ShowCompleteQuestUI();
    }

    private void ShowCompleteQuestUI()
    {
        if (localHudCtl == null)
        {
            localHudCtl = FindAnyObjectByType<PlayerHUDController>();
        }

        if (localHudCtl != null)
        {
            int totalCount = requiredPlatesCount;
            localHudCtl.ShowQuest(true, this);
            localHudCtl.UpdateQuestProgress(totalCount, totalCount, this);
            localHudCtl.UpdateQuestDescription("Nhiệm vụ hoàn thành: Cửa đã được mở!", this);
            localHudCtl.UpdateQuestTitle(questTitle, this);
            localHudCtl.UpdateQuestIcon(questIconSprite, this);
            
            StartCoroutine(HideQuestAfterDelay(hideDelayAfterComplete));
        }
        else
        {
            enabled = false;
        }
    }

    private IEnumerator HideQuestAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (localHudCtl != null)
        {
            localHudCtl.ShowQuest(false, this);
            localHudCtl.UpdateQuestIcon(null, this);
            localHudCtl.UpdateQuestTitle("NHIỆM VỤ", this);
            Debug.Log("[StonePuzzleQuestTrigger] Đã ẩn UI nhiệm vụ hoàn thành.");
        }

        // Vô hiệu hóa script và trigger để giải phóng tài nguyên
        enabled = false;
        var col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null || isQuestCompleted || !IsPrerequisiteCompleted()) return;

        bool isNetwork = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        if (IsPlayer(other.gameObject))
        {
            if (isNetwork)
            {
                if (!isQuestActive.Value)
                {
                    if (IsServer)
                    {
                        isQuestActive.Value = true;
                        Debug.Log($"[StonePuzzleQuestTrigger Server] Người chơi '{other.gameObject.name}' chạm Trigger - Kích hoạt nhiệm vụ cho toàn bộ mạng!");
                    }
                    else
                    {
                        RequestActivateQuestServerRpc();
                    }
                }
            }
            else
            {
                isPlayerInside = true;
                hasTriggeredQuest = true;
                UpdateQuestProgressUI();
                Debug.Log("[StonePuzzleQuestTrigger Offline] Người chơi chạm Trigger - Kích hoạt nhiệm vụ đẩy đá.");
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestActivateQuestServerRpc()
    {
        if (!isQuestActive.Value)
        {
            isQuestActive.Value = true;
            Debug.Log("[StonePuzzleQuestTrigger ServerRpc] Client yêu cầu kích hoạt nhiệm vụ Đẩy Đá cho toàn bộ mạng!");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other == null || isQuestCompleted) return;

        // Chỉ xử lý trong chế độ offline (vì chế độ mạng nhiệm vụ sẽ luôn bật cho đến khi hoàn thành)
        bool isNetwork = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (isNetwork) return;

        if (IsLocalPlayerObject(other.gameObject))
        {
            isPlayerInside = false;
            UpdateQuestProgressUI();
            Debug.Log("[StonePuzzleQuestTrigger Offline] Người chơi rời khỏi Trigger.");
        }
    }

    private bool IsLocalPlayerObject(GameObject go)
    {
        if (localPlayer == null) return IsPlayer(go);

        if (go == localPlayer.gameObject || go.transform.IsChildOf(localPlayer.transform) || localPlayer.transform.IsChildOf(go.transform))
        {
            return true;
        }
        return false;
    }

    private bool IsPlayer(GameObject go)
    {
        if (go == null) return false;

        if (go.GetComponent<IPlayerHUDTarget>() != null || 
            go.GetComponentInParent<IPlayerHUDTarget>() != null || 
            go.GetComponentInChildren<IPlayerHUDTarget>() != null ||
            go.transform.root.GetComponent<IPlayerHUDTarget>() != null || 
            go.transform.root.GetComponentInChildren<IPlayerHUDTarget>() != null) return true;

        if (go.CompareTag("Player") || go.transform.root.CompareTag("Player")) return true;

        if (go.layer == LayerMask.NameToLayer("Player") || go.transform.root.gameObject.layer == LayerMask.NameToLayer("Player")) return true;

        string nameLower = go.name.ToLower();
        string rootNameLower = go.transform.root.name.ToLower();
        if (nameLower.Contains("player") || rootNameLower.Contains("player") || 
            rootNameLower.Contains("leo") || rootNameLower.Contains("elena") || 
            rootNameLower.Contains("maya") || rootNameLower.Contains("arthur"))
        {
            return true;
        }

        return false;
    }

    private void FindLocalPlayer()
    {
        // 1. Dò tìm từ cache tĩnh của PlayerHUDController
        if (PlayerHUDController.LocalPlayerTarget != null)
        {
            var p = PlayerHUDController.LocalPlayerTarget;
            if (p != null && (p.IsStandaloneMode || p.IsOwner))
            {
                localPlayer = p;
                return;
            }
        }

        // 2. Dò tìm từ danh sách ActivePlayers của PlayerHUDManager
        var activePlayers = PlayerHUDManager.ActivePlayers;
        foreach (var p in activePlayers)
        {
            if (p != null && (p.IsStandaloneMode || p.IsOwner))
            {
                localPlayer = p;
                return;
            }
        }

        // 3. Dự phòng: tìm kiếm định kỳ trong các component của Scene
        if (Time.time >= nextPlayerSearchTime)
        {
            nextPlayerSearchTime = Time.time + 2f;

            var allComponents = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var mono in allComponents)
            {
                if (mono is IPlayerHUDTarget p)
                {
                    if (p.IsStandaloneMode || p.IsOwner)
                    {
                        localPlayer = p;
                        return;
                    }
                }
            }
        }
    }
}
