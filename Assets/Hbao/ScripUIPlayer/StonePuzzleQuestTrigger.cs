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
        var trigger = prerequisiteQuest.GetComponent<IQuestTrigger>();
        if (trigger != null) return trigger.IsQuestCompleted;
        return true;
    }

    [Header("Puzzle Configuration")]
    [Tooltip("Tham chiếu tới PressurePlatePuzzleManager quản lý các nút sàn")]
    public PressurePlatePuzzleManager puzzleManager;

    [Header("Quest UI Settings")]
    [Tooltip("Tiêu đề nhiệm vụ hiển thị trên UI (Ví dụ: ĐẨY ĐÁ)")]
    public string questTitle = "ĐẨY ĐÁ";

    [Tooltip("Icon nhiệm vụ hiển thị bên cạnh tiêu đề")]
    public Sprite questIconSprite;

    [Tooltip("Nội dung mô tả nhiệm vụ hiển thị trên UI")]
    [TextArea(3, 5)]
    public string questDescription = "Đẩy đá để tìm kiếm các phiến đá có hình dạng giống trên cửa để mở cửa.";

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
    }

    private void Start()
    {
        if (puzzleManager == null)
        {
            puzzleManager = FindAnyObjectByType<PressurePlatePuzzleManager>();
            if (puzzleManager != null)
            {
                Debug.Log("[StonePuzzleQuestTrigger] Tự động tìm thấy PressurePlatePuzzleManager trong Start!");
            }
            else
            {
                Debug.LogError("[StonePuzzleQuestTrigger] LỖI: Không tìm thấy PressurePlatePuzzleManager trong Scene. Vui lòng kéo gán thủ công!");
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
        if (isQuestCompleted || !IsPrerequisiteCompleted()) return;

        if (localPlayer == null)
        {
            FindLocalPlayer();
        }

        if (puzzleManager == null) return;

        // Xác định nhiệm vụ đã active hay chưa
        bool active = (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) 
            ? isQuestActive.Value 
            : hasTriggeredQuest;

        if (active)
        {
            // Kiểm tra trạng thái giải câu đố của puzzleManager
            if (puzzleManager.IsSolved())
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
        if (!IsPrerequisiteCompleted()) return;

        if (localHudCtl == null)
        {
            localHudCtl = FindAnyObjectByType<PlayerHUDController>();
        }

        if (localHudCtl != null)
        {
            // Trong chế độ mạng, chỉ cần 1 người kích hoạt thì tất cả mọi người đều hiện UI liên tục (không phụ thuộc việc ở trong trigger)
            bool isNetwork = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
            bool shouldShow = isNetwork ? isQuestActive.Value : (isPlayerInside || !hideWhenExitTrigger);

            if (shouldShow)
            {
                localHudCtl.ShowQuest(true, this);
                localHudCtl.UpdateQuestDescription(questDescription, this);
                localHudCtl.UpdateQuestTitle(questTitle, this);
                localHudCtl.UpdateQuestIcon(questIconSprite, this);

                // Tính toán số lượng nút sàn đang được kích hoạt
                int pressedCount = 0;
                int totalCount = 2;

                if (puzzleManager == null)
                {
                    puzzleManager = FindAnyObjectByType<PressurePlatePuzzleManager>();
                }

                if (puzzleManager != null && puzzleManager.requiredPlates != null && puzzleManager.requiredPlates.Length > 0)
                {
                    totalCount = puzzleManager.requiredPlates.Length;
                    foreach (var plate in puzzleManager.requiredPlates)
                    {
                        if (plate != null && plate.IsPressed)
                        {
                            pressedCount++;
                        }
                    }
                }

                // Chỉ gọi API của UI khi giá trị tiến trình thay đổi để giảm chi phí render
                if (pressedCount != lastPressedCount)
                {
                    lastPressedCount = pressedCount;
                    localHudCtl.UpdateQuestProgress(pressedCount, totalCount, this);
                    Debug.Log($"[StonePuzzleQuestTrigger] Cập nhật tiến độ nhiệm vụ: {pressedCount}/{totalCount}");
                }
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

        if (localHudCtl == null)
        {
            localHudCtl = FindAnyObjectByType<PlayerHUDController>();
        }

        if (localHudCtl != null)
        {
            // Cập nhật UI hiển thị hoàn tất tối đa
            int totalCount = puzzleManager.requiredPlates != null ? puzzleManager.requiredPlates.Length : 0;
            localHudCtl.ShowQuest(true, this);
            localHudCtl.UpdateQuestProgress(totalCount, totalCount, this);
            localHudCtl.UpdateQuestDescription("Nhiệm vụ hoàn thành: Cửa đã được mở!", this);
            localHudCtl.UpdateQuestTitle(questTitle, this);
            localHudCtl.UpdateQuestIcon(questIconSprite, this);
            
            // Bắt đầu Coroutine để ẩn UI sau độ trễ
            StartCoroutine(HideQuestAfterDelay(hideDelayAfterComplete));
        }
        else
        {
            // Nếu không có HUD, tự vô hiệu hóa luôn
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

        // Kiểm tra va chạm: 
        // - Chế độ chơi mạng: Chỉ Server được quyền thay đổi trạng thái của NetworkVariable
        // - Chế độ offline: Xử lý bình thường
        bool isNetwork = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (isNetwork && !IsServer) return;

        if (IsPlayer(other.gameObject))
        {
            if (isNetwork)
            {
                if (!isQuestActive.Value)
                {
                    isQuestActive.Value = true;
                    Debug.Log($"[StonePuzzleQuestTrigger Server] Người chơi '{other.gameObject.name}' chạm Trigger - Kích hoạt nhiệm vụ cho toàn bộ mạng!");
                }
            }
            else
            {
                isPlayerInside = true;
                hasTriggeredQuest = true;
                lastPressedCount = -1; // Reset để ép cập nhật UI ngay lập tức
                UpdateQuestProgressUI();
                Debug.Log("[StonePuzzleQuestTrigger Offline] Người chơi chạm Trigger - Kích hoạt nhiệm vụ đẩy đá.");
            }
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

        if (go.GetComponent<IPlayerHUDTarget>() != null || go.GetComponentInParent<IPlayerHUDTarget>() != null) return true;

        if (go.CompareTag("Player") || (go.transform.parent != null && go.transform.parent.CompareTag("Player"))) return true;

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
