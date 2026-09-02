using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class WaterFreezeQuestTrigger : NetworkBehaviour, IQuestTrigger
{
    public bool IsQuestCompleted => (SaveManager.IsContinueMode && SaveManager.IsQuestCompleted("WaterFreezeQuest")) || 
        ((NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) ? isQuestCompletedNet.Value : isQuestCompleted);

    public bool IsQuestActive => !(SaveManager.IsContinueMode && SaveManager.IsQuestCompleted("WaterFreezeQuest")) && 
        ((NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) ? isQuestActive.Value : hasTriggeredQuest);

    [Header("Quest Prerequisite Settings")]
    [Tooltip("Nhiệm vụ tiền đề bắt buộc phải hoàn thành trước khi nhiệm vụ này được hiển thị/kích hoạt")]
    public MonoBehaviour prerequisiteQuest;

    public bool IsPrerequisiteCompleted()
    {
        if (prerequisiteQuest == null) return true;
        if (prerequisiteQuest is IQuestTrigger quest) return quest.IsQuestCompleted;
        var trigger = prerequisiteQuest.GetComponent<IQuestTrigger>() ?? prerequisiteQuest.GetComponentInChildren<IQuestTrigger>();
        if (trigger != null) return trigger.IsQuestCompleted;
        if (prerequisiteQuest is BridgeCollapseTrigger bridge) return bridge.IsBridgeRepaired();
        return true;
    }

    [Header("Quest Configuration")]
    [Tooltip("Tham chiếu tới WaterPuzzleController quản lý trạng thái nguồn nước")]
    public WaterPuzzleController puzzController;

    [Header("Quest UI Settings")]
    [Tooltip("Tiêu đề nhiệm vụ hiển thị trên UI (Ví dụ: ĐÓNG BĂNG)")]
    public string questTitle = "ĐÓNG BĂNG";

    [Tooltip("Icon nhiệm vụ hiển thị bên cạnh tiêu đề")]
    public Sprite questIconSprite;

    [Tooltip("Nội dung mô tả nhiệm vụ hiển thị trên UI")]
    [TextArea(3, 5)]
    public string questDescription = "Đóng băng nguồn nước để đi qua.";

    [Tooltip("Thời gian chờ trước khi ẩn bảng nhiệm vụ sau khi giải xong (giây)")]
    public float hideDelayAfterComplete = 3f;

    [Tooltip("Khoảng thời gian (giây) giữa các lần kiểm tra cập nhật tiến trình trên UI")]
    public float checkInterval = 0.2f;

    // Biến mạng đồng bộ trạng thái kích hoạt nhiệm vụ
    public NetworkVariable<bool> isQuestActive = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Biến mạng đồng bộ trạng thái hoàn thành nhiệm vụ
    public NetworkVariable<bool> isQuestCompletedNet = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private IPlayerHUDTarget localPlayer;
    private PlayerHUDController localHudCtl;
    private Collider triggerCollider;

    private bool hasTriggeredQuest = false;
    private bool isQuestCompleted = false;
    
    private float nextPlayerSearchTime = 0f;
    private float nextCheckTime = 0f;
    private int lastProgressCount = -1;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider == null)
        {
            Debug.LogError($"[WaterFreezeQuestTrigger] LỖI: GameObject '{gameObject.name}' bắt buộc phải có một Collider (ví dụ Box Collider) được bật 'Is Trigger'!");
        }
        else if (!triggerCollider.isTrigger)
        {
            triggerCollider.isTrigger = true;
            Debug.LogWarning($"[WaterFreezeQuestTrigger] Tự động chuyển Collider trên '{gameObject.name}' thành Trigger.");
        }
    }

    private void Start()
    {
        if (puzzController == null)
        {
            puzzController = FindAnyObjectByType<WaterPuzzleController>();
            if (puzzController != null)
            {
                Debug.Log("[WaterFreezeQuestTrigger] Tự động tìm thấy WaterPuzzleController trong Start!");
            }
            else
            {
                Debug.LogError("[WaterFreezeQuestTrigger] LỖI: Không tìm thấy WaterPuzzleController trong Scene. Vui lòng kéo gán thủ công!");
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        isQuestActive.OnValueChanged += OnQuestActiveChanged;
        isQuestCompletedNet.OnValueChanged += OnQuestCompletedChanged;
        
        if (SaveManager.IsContinueMode && SaveManager.IsQuestCompleted("WaterFreezeQuest"))
        {
            isQuestCompleted = true;
            SaveManager.MarkQuestCompleted("WaterFreezeQuest");
            if (IsServer)
            {
                isQuestCompletedNet.Value = true;
                isQuestActive.Value = false;
            }
        }
        else
        {
            if (IsServer)
            {
                isQuestCompletedNet.Value = false;
                isQuestActive.Value = false;
            }
            isQuestCompleted = false;
            hasTriggeredQuest = false;
        }

        if (isQuestActive.Value)
        {
            hasTriggeredQuest = true;
            StartCoroutine(DelayedShowQuestUI());
        }
    }

    private System.Collections.IEnumerator DelayedShowQuestUI()
    {
        yield return new WaitForSeconds(0.6f);
        UpdateQuestProgressUI();
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
            CompleteQuest();
        }
    }

    private void OnQuestActiveChanged(bool oldVal, bool newVal)
    {
        if (newVal && IsPrerequisiteCompleted())
        {
            hasTriggeredQuest = true;
            lastProgressCount = -1; // Ép cập nhật UI lập tức
            UpdateQuestProgressUI();
            Debug.Log("[WaterFreezeQuestTrigger] Nhiệm vụ đóng băng nước đã được kích hoạt đồng bộ từ mạng!");
        }
    }

    private void Update()
    {
        if (isQuestCompleted || !IsPrerequisiteCompleted()) return;

        if (localPlayer == null)
        {
            FindLocalPlayer();
        }

        if (puzzController == null)
        {
            puzzController = FindAnyObjectByType<WaterPuzzleController>();
        }

        if (puzzController == null) return;

        // Xác định nhiệm vụ đã active hay chưa
        bool active = (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) 
            ? isQuestActive.Value 
            : hasTriggeredQuest;

        // Nếu nguồn nước đã được đóng băng (Frozen = 3), hoàn thành nhiệm vụ ngay lập tức bất kể đã chạm Trigger hay chưa
        if (puzzController.CurrentState == WaterPuzzleController.WaterPuzzleState.Frozen)
        {
            if (!active)
            {
                bool isNetwork = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
                if (isNetwork && IsServer)
                {
                    isQuestActive.Value = true;
                }
                hasTriggeredQuest = true;
            }
            CompleteQuest();
            return;
        }

        if (active)
        {
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

        if (localHudCtl != null && puzzController != null)
        {
            // Kích hoạt UI
            localHudCtl.ShowQuest(true, this);
            localHudCtl.UpdateQuestDescription(questDescription, this);
            localHudCtl.UpdateQuestTitle(questTitle, this);
            localHudCtl.UpdateQuestIcon(questIconSprite, this);

            // Kiểm tra xem đã hoàn thành chưa (Frozen là bước 1/1, còn lại là 0/1)
            int currentProgress = (puzzController.CurrentState == WaterPuzzleController.WaterPuzzleState.Frozen) ? 1 : 0;
            lastProgressCount = currentProgress;
            localHudCtl.UpdateQuestProgress(currentProgress, 1, this);
        }
    }

    private void CompleteQuest()
    {
        if (isQuestCompleted) return;
        isQuestCompleted = true;
        SaveManager.MarkQuestCompleted("WaterFreezeQuest");

        bool isNetwork = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (isNetwork && IsServer)
        {
            isQuestCompletedNet.Value = true;
            isQuestActive.Value = false;
        }

        Debug.Log("[WaterFreezeQuestTrigger] Nguồn nước đã được đóng băng! Nhiệm vụ hoàn thành.");

        if (localHudCtl == null)
        {
            localHudCtl = FindAnyObjectByType<PlayerHUDController>();
        }

        if (localHudCtl != null)
        {
            // Cập nhật UI hiển thị hoàn tất tối đa
            localHudCtl.ShowQuest(true, this);
            localHudCtl.UpdateQuestProgress(1, 1, this);
            localHudCtl.UpdateQuestDescription("Nhiệm vụ hoàn thành: Nguồn nước đã được đóng băng!", this);
            localHudCtl.UpdateQuestTitle(questTitle, this);
            localHudCtl.UpdateQuestIcon(questIconSprite, this);
            
            // Bắt đầu Coroutine để ẩn UI sau độ trễ
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
            Debug.Log("[WaterFreezeQuestTrigger] Đã ẩn UI nhiệm vụ hoàn thành.");
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
        if (isNetwork && !IsServer) return;

        if (IsPlayer(other.gameObject))
        {
            if (isNetwork)
            {
                if (!isQuestActive.Value)
                {
                    isQuestActive.Value = true;
                    Debug.Log($"[WaterFreezeQuestTrigger Server] Người chơi '{other.gameObject.name}' chạm Trigger - Kích hoạt nhiệm vụ đóng băng nước cho toàn bộ mạng!");
                }
            }
            else
            {
                hasTriggeredQuest = true;
                lastProgressCount = -1; // Reset để ép cập nhật UI ngay lập tức
                UpdateQuestProgressUI();
                Debug.Log("[WaterFreezeQuestTrigger Offline] Người chơi chạm Trigger - Kích hoạt nhiệm vụ đóng băng nước.");
            }
        }
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
        if (PlayerHUDController.LocalPlayerTarget != null)
        {
            var p = PlayerHUDController.LocalPlayerTarget;
            if (p != null && (p.IsStandaloneMode || p.IsOwner))
            {
                localPlayer = p;
                return;
            }
        }

        var activePlayers = PlayerHUDManager.ActivePlayers;
        foreach (var p in activePlayers)
        {
            if (p != null && (p.IsStandaloneMode || p.IsOwner))
            {
                localPlayer = p;
                return;
            }
        }

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
