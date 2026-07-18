using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class MazeGemQuestTrigger : NetworkBehaviour
{
    [Header("Quest Configuration")]
    [Tooltip("Danh sách các viên ngọc trong mê cung cần thu thập")]
    public CollectibleItemDrop[] gemObjects;

    [Header("Quest UI Settings")]
    [Tooltip("Tiêu đề nhiệm vụ hiển thị trên UI (Ví dụ: TÌM NGỌC)")]
    public string questTitle = "TÌM NGỌC";

    [Tooltip("Icon nhiệm vụ hiển thị bên cạnh tiêu đề. Nếu trống, script tự động lấy ngọc tím của HUD")]
    public Sprite questIconSprite;

    [Tooltip("Nội dung mô tả nhiệm vụ hiển thị trên UI")]
    [TextArea(3, 5)]
    public string questDescription = "Tìm đường để lấy những viên ngọc trong mê cung.";

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

    // Biến mạng đồng bộ số lượng ngọc đã thu thập
    public NetworkVariable<int> collectedCount = new NetworkVariable<int>(
        0,
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
    private int lastPressedCount = -1;
    private int localCollectedCount = 0; // Dùng khi chơi offline

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider == null)
        {
            Debug.LogError($"[MazeGemQuestTrigger] LỖI: GameObject '{gameObject.name}' bắt buộc phải có một Collider (ví dụ Box Collider) được bật 'Is Trigger'!");
        }
        else if (!triggerCollider.isTrigger)
        {
            triggerCollider.isTrigger = true;
            Debug.LogWarning($"[MazeGemQuestTrigger] Tự động chuyển Collider trên '{gameObject.name}' thành Trigger.");
        }
    }

    private void Start()
    {
        if (gemObjects == null || gemObjects.Length == 0)
        {
            Debug.LogWarning("[MazeGemQuestTrigger] CẢNH BÁO: Danh sách gemObjects đang trống! Hãy gán các viên ngọc trong Inspector.");
            // Tìm thử xem trong scene có viên ngọc nào không để cảnh báo cụ thể hơn
            var allGems = FindObjectsByType<CollectibleItemDrop>(FindObjectsSortMode.None);
            if (allGems.Length > 0)
            {
                Debug.LogWarning($"[MazeGemQuestTrigger] Gợi ý: Tìm thấy {allGems.Length} viên ngọc CollectibleItemDrop trong Scene. Hãy kéo chúng vào script này!");
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        isQuestActive.OnValueChanged += OnQuestActiveChanged;
        collectedCount.OnValueChanged += OnCollectedCountChanged;
        
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
        collectedCount.OnValueChanged -= OnCollectedCountChanged;
    }

    private void OnQuestActiveChanged(bool oldVal, bool newVal)
    {
        if (newVal)
        {
            hasTriggeredQuest = true;
            lastPressedCount = -1; // Ép cập nhật UI lập tức
            UpdateQuestProgressUI();
            Debug.Log("[MazeGemQuestTrigger] Nhiệm vụ tìm ngọc đã được kích hoạt đồng bộ từ mạng!");
        }
    }

    private void OnCollectedCountChanged(int oldVal, int newVal)
    {
        if (isQuestActive.Value)
        {
            UpdateQuestProgressUI();
        }
    }

    private void Update()
    {
        if (isQuestCompleted) return;

        if (localPlayer == null)
        {
            FindLocalPlayer();
        }

        // 1. Chỉ Server (hoặc offline) giám sát tiến trình thu thập ngọc
        bool isNetwork = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (!isNetwork || IsServer)
        {
            bool active = isNetwork ? isQuestActive.Value : hasTriggeredQuest;
            if (active && gemObjects != null && gemObjects.Length > 0)
            {
                // Đếm số lượng ngọc đã thu thập (các object bị null/hủy hoặc ẩn đi)
                int currentCollected = 0;
                foreach (var gem in gemObjects)
                {
                    if (gem == null || gem.gameObject == null || !gem.gameObject.activeInHierarchy)
                    {
                        currentCollected++;
                    }
                }

                // Cập nhật giá trị
                if (isNetwork)
                {
                    if (collectedCount.Value != currentCollected)
                    {
                        collectedCount.Value = currentCollected;
                        Debug.Log($"[MazeGemQuestTrigger Server] Đã thu thập: {currentCollected}/{gemObjects.Length} ngọc.");
                    }
                }
                else
                {
                    if (localCollectedCount != currentCollected)
                    {
                        localCollectedCount = currentCollected;
                        UpdateQuestProgressUI();
                        Debug.Log($"[MazeGemQuestTrigger Offline] Đã thu thập: {currentCollected}/{gemObjects.Length} ngọc.");
                    }
                }

                // Kiểm tra điều kiện hoàn thành
                int totalNeeded = gemObjects.Length;
                if (currentCollected >= totalNeeded)
                {
                    CompleteQuest();
                    return;
                }
            }
        }

        // 2. Client & Server định kỳ cập nhật UI
        bool currentActive = isNetwork ? isQuestActive.Value : hasTriggeredQuest;
        if (currentActive && Time.time >= nextCheckTime)
        {
            nextCheckTime = Time.time + checkInterval;
            UpdateQuestProgressUI();
        }
    }

    private void UpdateQuestProgressUI()
    {
        if (localHudCtl == null)
        {
            localHudCtl = FindAnyObjectByType<PlayerHUDController>();
        }

        if (localHudCtl != null)
        {
            bool isNetwork = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
            bool active = isNetwork ? isQuestActive.Value : hasTriggeredQuest;

            if (active)
            {
                localHudCtl.ShowQuest(true);
                localHudCtl.UpdateQuestDescription(questDescription);
                localHudCtl.UpdateQuestTitle(questTitle);

                // Tự động giải quyết Sprite Icon (ưu tiên Inspector, sau đó đến cache HUD, sau đó đến Resources)
                Sprite targetIcon = questIconSprite;
                if (targetIcon == null && localHudCtl != null)
                {
                    targetIcon = localHudCtl.ngoc1Sprite;
                }
                if (targetIcon == null)
                {
                    targetIcon = Resources.Load<Sprite>("crystal_purple");
                }
                localHudCtl.UpdateQuestIcon(targetIcon);

                int current = isNetwork ? collectedCount.Value : localCollectedCount;
                int total = gemObjects != null ? gemObjects.Length : 2;

                if (current != lastPressedCount)
                {
                    lastPressedCount = current;
                    localHudCtl.UpdateQuestProgress(current, total);
                }
            }
        }
    }

    private void CompleteQuest()
    {
        isQuestCompleted = true;
        Debug.Log("[MazeGemQuestTrigger] Đã thu thập đủ ngọc trong mê cung! Nhiệm vụ hoàn thành.");

        if (localHudCtl == null)
        {
            localHudCtl = FindAnyObjectByType<PlayerHUDController>();
        }

        if (localHudCtl != null)
        {
            int total = gemObjects != null ? gemObjects.Length : 2;
            localHudCtl.ShowQuest(true);
            localHudCtl.UpdateQuestProgress(total, total);
            localHudCtl.UpdateQuestDescription("Nhiệm vụ hoàn thành: Đã thu thập đủ ngọc!");
            localHudCtl.UpdateQuestTitle(questTitle);
            
            Sprite targetIcon = questIconSprite != null ? questIconSprite : (localHudCtl.ngoc1Sprite != null ? localHudCtl.ngoc1Sprite : Resources.Load<Sprite>("crystal_purple"));
            localHudCtl.UpdateQuestIcon(targetIcon);

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
            Debug.Log("[MazeGemQuestTrigger] Đã ẩn UI nhiệm vụ hoàn thành.");
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
                    Debug.Log($"[MazeGemQuestTrigger Server] Người chơi '{other.gameObject.name}' chạm Trigger - Kích hoạt nhiệm vụ tìm ngọc cho toàn bộ mạng!");
                }
            }
            else
            {
                hasTriggeredQuest = true;
                lastPressedCount = -1; // Ép cập nhật ngay lập tức
                UpdateQuestProgressUI();
                Debug.Log("[MazeGemQuestTrigger Offline] Người chơi chạm Trigger - Kích hoạt nhiệm vụ tìm ngọc.");
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
