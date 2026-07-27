using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class MazeGemQuestTrigger : NetworkBehaviour, IQuestTrigger
{
    public bool IsQuestCompleted => (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) ? isQuestCompletedNet.Value : isQuestCompletedLocal;
    public bool IsQuestActive => (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) ? isQuestActive.Value : hasTriggeredQuest;

    [Header("Quest Prerequisite Settings")]
    [Tooltip("Nhiệm vụ tiền đề bắt buộc phải hoàn thành trước khi nhiệm vụ này được hiển thị/kích hoạt")]
    public MonoBehaviour prerequisiteQuest;

    public bool IsPrerequisiteCompleted()
    {
        if (prerequisiteQuest == null) return true;
        if (prerequisiteQuest is IQuestTrigger quest) return quest.IsQuestCompleted;
        if (prerequisiteQuest is BridgeCollapseTrigger bridge) return bridge.IsBridgeRepaired();
        return true;
    }

    [Header("Quest Configuration")]
    [Tooltip("Danh sách các GameObject viên ngọc trong mê cung cần thu thập (Hỗ trợ cả CrystalCore, CollectibleItemDrop hoặc GameObject bất kỳ)")]
    public GameObject[] gemObjects;

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

    // Biến mạng đồng bộ trạng thái hoàn thành nhiệm vụ
    public NetworkVariable<bool> isQuestCompletedNet = new NetworkVariable<bool>(
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
    private bool isQuestCompletedLocal = false;
    
    private float nextPlayerSearchTime = 0f;
    private float nextCheckTime = 0f;
    private int lastPressedCount = -1;
    private int localCollectedCount = 0; // Dùng khi chơi offline
    private System.Collections.Generic.HashSet<int> collectedGemIndices = new System.Collections.Generic.HashSet<int>();

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
            Debug.LogWarning("[MazeGemQuestTrigger] CẢNH BÁO: Danh sách gemObjects đang trống! Hãy kéo các GameObject viên ngọc (như CrystalCore hoặc CollectibleItemDrop) vào Inspector.");
        }
    }

    public override void OnNetworkSpawn()
    {
        isQuestActive.OnValueChanged += OnQuestActiveChanged;
        collectedCount.OnValueChanged += OnCollectedCountChanged;
        isQuestCompletedNet.OnValueChanged += OnQuestCompletedChanged;
        
        if (isQuestCompletedNet.Value)
        {
            isQuestCompletedLocal = true;
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
        collectedCount.OnValueChanged -= OnCollectedCountChanged;
        isQuestCompletedNet.OnValueChanged -= OnQuestCompletedChanged;
    }

    private void OnQuestCompletedChanged(bool oldVal, bool newVal)
    {
        if (newVal)
        {
            isQuestCompletedLocal = true;
            ShowCompletionUI();
        }
    }

    private void OnQuestActiveChanged(bool oldVal, bool newVal)
    {
        if (newVal && IsPrerequisiteCompleted() && !IsQuestCompleted)
        {
            hasTriggeredQuest = true;
            lastPressedCount = -1; // Ép cập nhật UI lập tức
            UpdateQuestProgressUI();
            Debug.Log("[MazeGemQuestTrigger] Nhiệm vụ tìm ngọc đã được kích hoạt đồng bộ từ mạng!");
        }
    }

    private void OnCollectedCountChanged(int oldVal, int newVal)
    {
        if (isQuestActive.Value && IsPrerequisiteCompleted() && !IsQuestCompleted)
        {
            UpdateQuestProgressUI();
        }
    }

    private bool IsGemCollected(GameObject gem)
    {
        if (gem == null) return true;

        // 1. Kiểm tra CrystalCore (nếu là ngọc CrystalCore trong minigame/mê cung)
        var crystal = gem.GetComponent<CrystalCore>();
        if (crystal != null)
        {
            if (!gem.activeInHierarchy) return true;
            if (crystal.isSnapped != null && crystal.isSnapped.Value) return true;
            if (crystal.holderId != null && crystal.holderId.Value != ulong.MaxValue) return true;
            return false;
        }

        // 2. Kiểm tra CollectibleItemDrop (nếu là ngọc nhặt dạng vật phẩm drop)
        var collectible = gem.GetComponent<CollectibleItemDrop>();
        if (collectible != null)
        {
            if (!gem.activeInHierarchy) return true;
            return false;
        }

        // 3. Nếu là GameObject bình thường, kiểm tra xem đã bị ẩn (SetActive false) hoặc Destroy chưa
        return !gem.activeInHierarchy;
    }

    private void Update()
    {
        if (IsQuestCompleted || !IsPrerequisiteCompleted()) return;

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
                // Kiểm tra từng ngọc trong danh sách
                for (int i = 0; i < gemObjects.Length; i++)
                {
                    var gem = gemObjects[i];
                    if (collectedGemIndices.Contains(i) || IsGemCollected(gem))
                    {
                        collectedGemIndices.Add(i);
                    }
                }

                int currentCollected = collectedGemIndices.Count;

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
        if (!IsPrerequisiteCompleted() || IsQuestCompleted) return;

        PlayerHUDController activeHud = PlayerHUDController.Instance ?? localHudCtl ?? FindAnyObjectByType<PlayerHUDController>();
        if (activeHud != null)
        {
            localHudCtl = activeHud;
            bool isNetwork = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
            bool active = isNetwork ? isQuestActive.Value : hasTriggeredQuest;

            if (active)
            {
                activeHud.ShowQuest(true, this);
                activeHud.UpdateQuestDescription(questDescription, this);
                activeHud.UpdateQuestTitle(questTitle, this);

                Sprite targetIcon = questIconSprite;
                if (targetIcon == null && activeHud != null)
                {
                    targetIcon = activeHud.ngoc1Sprite;
                }
                if (targetIcon == null)
                {
                    targetIcon = Resources.Load<Sprite>("crystal_purple");
                }
                activeHud.UpdateQuestIcon(targetIcon, this);

                int current = isNetwork ? collectedCount.Value : localCollectedCount;
                int total = gemObjects != null ? gemObjects.Length : 2;

                if (current != lastPressedCount)
                {
                    lastPressedCount = current;
                    activeHud.UpdateQuestProgress(current, total, this);
                }
            }
        }
    }

    private void CompleteQuest()
    {
        if (isQuestCompletedLocal) return;
        isQuestCompletedLocal = true;

        bool isNetwork = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (isNetwork && IsServer)
        {
            isQuestCompletedNet.Value = true;
            isQuestActive.Value = false;
        }

        ShowCompletionUI();
    }

    private void ShowCompletionUI()
    {
        Debug.Log("[MazeGemQuestTrigger] Đã thu thập đủ ngọc trong mê cung! Nhiệm vụ hoàn thành.");

        if (localHudCtl == null)
        {
            localHudCtl = FindAnyObjectByType<PlayerHUDController>();
        }

        if (localHudCtl != null)
        {
            int total = gemObjects != null ? gemObjects.Length : 2;
            localHudCtl.ShowQuest(true, this);
            localHudCtl.UpdateQuestProgress(total, total, this);
            localHudCtl.UpdateQuestDescription("Nhiệm vụ hoàn thành: Đã thu thập đủ ngọc!", this);
            localHudCtl.UpdateQuestTitle(questTitle, this);
            
            Sprite targetIcon = questIconSprite != null ? questIconSprite : (localHudCtl != null && localHudCtl.ngoc1Sprite != null ? localHudCtl.ngoc1Sprite : Resources.Load<Sprite>("crystal_purple"));
            localHudCtl.UpdateQuestIcon(targetIcon, this);

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
        if (other == null || IsQuestCompleted || !IsPrerequisiteCompleted()) return;

        bool isNetwork = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        if (IsPlayer(other.gameObject))
        {
            hasTriggeredQuest = true;
            lastPressedCount = -1;
            UpdateQuestProgressUI();

            if (isNetwork)
            {
                if (!isQuestActive.Value)
                {
                    if (IsServer)
                    {
                        isQuestActive.Value = true;
                        Debug.Log($"[MazeGemQuestTrigger Server] Người chơi '{other.gameObject.name}' chạm Trigger - Kích hoạt nhiệm vụ cho toàn bộ mạng!");
                    }
                    else
                    {
                        RequestActivateQuestServerRpc();
                    }
                }
            }
            else
            {
                Debug.Log("[MazeGemQuestTrigger Offline] Người chơi chạm Trigger - Kích hoạt nhiệm vụ.");
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestActivateQuestServerRpc()
    {
        if (!isQuestActive.Value)
        {
            isQuestActive.Value = true;
            Debug.Log("[MazeGemQuestTrigger ServerRpc] Client yêu cầu kích hoạt nhiệm vụ cho toàn bộ mạng!");
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
