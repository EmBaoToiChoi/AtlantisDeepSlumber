using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class ElementalRockQuestTrigger : NetworkBehaviour
{
    [Header("Quest Configuration")]
    [Tooltip("Tham chiếu tới đối tượng đá nguyên tố (ElementalRockPuzzle) cần phá vỡ")]
    public ElementalRockPuzzle targetRock;

    [Tooltip("Số bước/nguyên tố cần kích hoạt của câu đố đá")]
    public int totalSteps = 4;

    [Header("Quest UI Settings")]
    [Tooltip("Tiêu đề nhiệm vụ hiển thị trên UI (Ví dụ: NGUYÊN TỐ)")]
    public string questTitle = "NGUYÊN TỐ";

    [Tooltip("Icon nhiệm vụ hiển thị bên cạnh tiêu đề")]
    public Sprite questIconSprite;

    [Tooltip("Nội dung mô tả nhiệm vụ hiển thị trên UI")]
    [TextArea(3, 5)]
    public string questDescription = "Hãy chưởng các nguyên tố(R) trùng khớp với các hình dạng trên đá để phá vỡ nó.";

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
    private int lastStepCount = -1;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider == null)
        {
            Debug.LogError($"[ElementalRockQuestTrigger] LỖI: GameObject '{gameObject.name}' bắt buộc phải có một Collider (ví dụ Box Collider) được bật 'Is Trigger'!");
        }
        else if (!triggerCollider.isTrigger)
        {
            triggerCollider.isTrigger = true;
            Debug.LogWarning($"[ElementalRockQuestTrigger] Tự động chuyển Collider trên '{gameObject.name}' thành Trigger.");
        }
    }

    private void Start()
    {
        if (targetRock == null)
        {
            targetRock = FindAnyObjectByType<ElementalRockPuzzle>();
            if (targetRock != null)
            {
                Debug.Log("[ElementalRockQuestTrigger] Tự động tìm thấy ElementalRockPuzzle trong Start!");
            }
            else
            {
                Debug.LogError("[ElementalRockQuestTrigger] LỖI: Không tìm thấy ElementalRockPuzzle trong Scene. Vui lòng kéo gán thủ công!");
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
            lastStepCount = -1; // Ép cập nhật UI lập tức
            UpdateQuestProgressUI();
            Debug.Log("[ElementalRockQuestTrigger] Nhiệm vụ đá nguyên tố đã được kích hoạt đồng bộ từ mạng!");
        }
    }

    private void Update()
    {
        if (isQuestCompleted) return;

        if (localPlayer == null)
        {
            FindLocalPlayer();
        }

        // Xác định nhiệm vụ đã active hay chưa
        bool active = (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) 
            ? isQuestActive.Value 
            : hasTriggeredQuest;

        if (active)
        {
            // Kiểm tra xem đá đã bị phá vỡ hoàn toàn chưa (bị hủy hoặc bị ẩn IsShown = false)
            if (targetRock == null || targetRock.gameObject == null || !targetRock.IsShown)
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

        if (localHudCtl != null && targetRock != null)
        {
            // Kích hoạt UI
            localHudCtl.ShowQuest(true);
            localHudCtl.UpdateQuestDescription(questDescription);
            localHudCtl.UpdateQuestTitle(questTitle);
            localHudCtl.UpdateQuestIcon(questIconSprite);

            // Lấy bước tiến hiện tại từ đá nguyên tố
            int currentStep = targetRock.CurrentStep;

            if (currentStep != lastStepCount)
            {
                lastStepCount = currentStep;
                localHudCtl.UpdateQuestProgress(currentStep, totalSteps);
                Debug.Log($"[ElementalRockQuestTrigger] Cập nhật tiến độ đá nguyên tố: {currentStep}/{totalSteps}");
            }
        }
    }

    private void CompleteQuest()
    {
        isQuestCompleted = true;
        Debug.Log("[ElementalRockQuestTrigger] Đá nguyên tố đã bị phá vỡ! Nhiệm vụ hoàn thành.");

        if (localHudCtl == null)
        {
            localHudCtl = FindAnyObjectByType<PlayerHUDController>();
        }

        if (localHudCtl != null)
        {
            // Cập nhật UI hiển thị hoàn tất tối đa
            localHudCtl.ShowQuest(true);
            localHudCtl.UpdateQuestProgress(totalSteps, totalSteps);
            localHudCtl.UpdateQuestDescription("Nhiệm vụ hoàn thành: Đá nguyên tố đã bị phá vỡ!");
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
            Debug.Log("[ElementalRockQuestTrigger] Đã ẩn UI nhiệm vụ hoàn thành.");
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
                    Debug.Log($"[ElementalRockQuestTrigger Server] Người chơi '{other.gameObject.name}' chạm Trigger - Kích hoạt nhiệm vụ đá nguyên tố cho toàn bộ mạng!");
                }
            }
            else
            {
                hasTriggeredQuest = true;
                lastStepCount = -1; // Reset để ép cập nhật UI ngay lập tức
                UpdateQuestProgressUI();
                Debug.Log("[ElementalRockQuestTrigger Offline] Người chơi chạm Trigger - Kích hoạt nhiệm vụ đá nguyên tố.");
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
