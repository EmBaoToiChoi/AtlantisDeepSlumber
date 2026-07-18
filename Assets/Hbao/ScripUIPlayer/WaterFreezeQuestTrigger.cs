using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class WaterFreezeQuestTrigger : NetworkBehaviour
{
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
        
        // Nếu nhiệm vụ đã được kích hoạt trước khi player này kết nối
        if (isQuestActive.Value)
        {
            hasTriggeredQuest = true;
            UpdateQuestProgressUI();
        }
    }

    public override void OnNetworkDespawn()
    {
        isQuestActive.OnValueChanged -= OnQuestActiveChanged;
    }

    private void OnQuestActiveChanged(bool oldVal, bool newVal)
    {
        if (newVal)
        {
            hasTriggeredQuest = true;
            lastProgressCount = -1; // Ép cập nhật UI lập tức
            UpdateQuestProgressUI();
            Debug.Log("[WaterFreezeQuestTrigger] Nhiệm vụ đóng băng nước đã được kích hoạt đồng bộ từ mạng!");
        }
    }

    private void Update()
    {
        if (isQuestCompleted) return;

        if (localPlayer == null)
        {
            FindLocalPlayer();
        }

        if (puzzController == null) return;

        // Xác định nhiệm vụ đã active hay chưa
        bool active = (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) 
            ? isQuestActive.Value 
            : hasTriggeredQuest;

        if (active)
        {
            // Kiểm tra xem nước đã đóng băng chưa (Frozen = 3)
            if (puzzController.CurrentState == WaterPuzzleController.WaterPuzzleState.Frozen)
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
        if (localHudCtl == null)
        {
            localHudCtl = FindAnyObjectByType<PlayerHUDController>();
        }

        if (localHudCtl != null && puzzController != null)
        {
            // Kích hoạt UI
            localHudCtl.ShowQuest(true);
            localHudCtl.UpdateQuestDescription(questDescription);
            localHudCtl.UpdateQuestTitle(questTitle);
            localHudCtl.UpdateQuestIcon(questIconSprite);

            // Kiểm tra xem đã hoàn thành chưa (Frozen là bước 1/1, còn lại là 0/1)
            int currentProgress = (puzzController.CurrentState == WaterPuzzleController.WaterPuzzleState.Frozen) ? 1 : 0;

            if (currentProgress != lastProgressCount)
            {
                lastProgressCount = currentProgress;
                localHudCtl.UpdateQuestProgress(currentProgress, 1);
                Debug.Log($"[WaterFreezeQuestTrigger] Cập nhật tiến độ đóng băng: {currentProgress}/1");
            }
        }
    }

    private void CompleteQuest()
    {
        isQuestCompleted = true;
        Debug.Log("[WaterFreezeQuestTrigger] Nguồn nước đã được đóng băng! Nhiệm vụ hoàn thành.");

        if (localHudCtl == null)
        {
            localHudCtl = FindAnyObjectByType<PlayerHUDController>();
        }

        if (localHudCtl != null)
        {
            // Cập nhật UI hiển thị hoàn tất tối đa
            localHudCtl.ShowQuest(true);
            localHudCtl.UpdateQuestProgress(1, 1);
            localHudCtl.UpdateQuestDescription("Nhiệm vụ hoàn thành: Nguồn nước đã được đóng băng!");
            localHudCtl.UpdateQuestTitle(questTitle);
            localHudCtl.UpdateQuestIcon(questIconSprite);
            
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
            localHudCtl.ShowQuest(false);
            localHudCtl.UpdateQuestIcon(null);
            localHudCtl.UpdateQuestTitle("NHIỆM VỤ");
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
        if (other == null || isQuestCompleted) return;

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
