using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class CrystalPuzzleQuestTrigger : NetworkBehaviour, IQuestTrigger
{
    public bool IsQuestCompleted => (SaveManager.IsContinueMode && SaveManager.IsQuestCompleted("CrystalPuzzleQuest")) || 
        ((NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) ? isQuestCompletedNet.Value : isQuestCompletedLocal);

    public bool IsQuestActive => !(SaveManager.IsContinueMode && SaveManager.IsQuestCompleted("CrystalPuzzleQuest")) && 
        ((NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) ? isQuestActive.Value : hasTriggeredQuest);

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

    [Header("Pedestal Puzzle Configuration")]
    [Tooltip("Danh sách các trụ/phiến đá đặt ngọc (CrystalPuzzleSystem / CrystalPedestal)")]
    public CrystalPuzzleSystem[] pedestals;

    [Header("Quest UI Settings")]
    [Tooltip("Tiêu đề nhiệm vụ hiển thị trên UI")]
    public string questTitle = "ĐẶT NGỌC";

    [Tooltip("Icon nhiệm vụ hiển thị bên cạnh tiêu đề. Nếu trống, script tự lấy ngọc của HUD")]
    public Sprite questIconSprite;

    [Tooltip("Nội dung mô tả nhiệm vụ hiển thị trên UI")]
    [TextArea(3, 5)]
    public string questDescription = "Di chuyển lại gần các công tắc và ấn F để tương tác và đặt 2 viên ngọc lên phiến đá.";

    [Tooltip("Thời gian chờ trước khi ẩn bảng nhiệm vụ sau khi hoàn thành (giây)")]
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

    // Biến mạng đồng bộ số lượng ngọc đã đặt
    public NetworkVariable<int> placedCount = new NetworkVariable<int>(
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
    private int lastPlacedCount = -1;
    private int localPlacedCount = 0;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider == null)
        {
            Debug.LogWarning($"[CrystalPuzzleQuestTrigger] GameObject '{gameObject.name}' không có Collider trigger. Tạo/gán Box Collider trigger trên GameObject này để tự động phát hiện người chơi khi bước vào vùng nhiệm vụ.");
        }
        else if (!triggerCollider.isTrigger)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void Start()
    {
        if (pedestals == null || pedestals.Length == 0)
        {
            pedestals = FindObjectsByType<CrystalPuzzleSystem>(FindObjectsSortMode.None);
            if (pedestals != null && pedestals.Length > 0)
            {
                Debug.Log($"[CrystalPuzzleQuestTrigger] Tự động tìm thấy {pedestals.Length} trụ đặt ngọc (CrystalPuzzleSystem).");
            }
            else
            {
                Debug.LogWarning("[CrystalPuzzleQuestTrigger] CẢNH BÁO: Chưa gán pedestals (các trụ đặt ngọc) trong Inspector!");
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        isQuestActive.OnValueChanged += OnQuestActiveChanged;
        placedCount.OnValueChanged += OnPlacedCountChanged;
        isQuestCompletedNet.OnValueChanged += OnQuestCompletedChanged;

        if (isQuestCompletedNet.Value || (SaveManager.IsContinueMode && SaveManager.IsQuestCompleted("CrystalPuzzleQuest")))
        {
            isQuestCompletedLocal = true;
            SaveManager.MarkQuestCompleted("CrystalPuzzleQuest");
            if (IsServer)
            {
                isQuestCompletedNet.Value = true;
                isQuestActive.Value = false;
            }
        }
        else if (isQuestActive.Value)
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
        placedCount.OnValueChanged -= OnPlacedCountChanged;
        isQuestCompletedNet.OnValueChanged -= OnQuestCompletedChanged;
    }

    private void OnQuestCompletedChanged(bool oldVal, bool newVal)
    {
        if (newVal)
        {
            isQuestCompletedLocal = true;
            SaveManager.MarkQuestCompleted("CrystalPuzzleQuest");
            ShowCompletionUI();
        }
    }

    private void OnQuestActiveChanged(bool oldVal, bool newVal)
    {
        if (newVal && IsPrerequisiteCompleted() && !IsQuestCompleted)
        {
            hasTriggeredQuest = true;
            lastPlacedCount = -1;
            UpdateQuestProgressUI();
        }
    }

    private void OnPlacedCountChanged(int oldVal, int newVal)
    {
        if (isQuestActive.Value && IsPrerequisiteCompleted() && !IsQuestCompleted)
        {
            UpdateQuestProgressUI();
        }
    }

    private void Update()
    {
        if (IsQuestCompleted || !IsPrerequisiteCompleted()) return;

        if (localPlayer == null)
        {
            FindLocalPlayer();
        }

        bool isNetwork = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        
        // Server / Offline kiểm tra tiến độ đặt ngọc từ các pedestals
        if (!isNetwork || IsServer)
        {
            bool active = isNetwork ? isQuestActive.Value : hasTriggeredQuest;
            if (active && pedestals != null && pedestals.Length > 0)
            {
                int currentPlaced = 0;
                foreach (var pedestal in pedestals)
                {
                    if (pedestal != null && pedestal.IsCrystalPlaced)
                    {
                        currentPlaced++;
                    }
                }

                if (isNetwork)
                {
                    if (placedCount.Value != currentPlaced)
                    {
                        placedCount.Value = currentPlaced;
                        Debug.Log($"[CrystalPuzzleQuestTrigger Server] Tiến độ đặt ngọc: {currentPlaced}/{pedestals.Length}");
                    }
                }
                else
                {
                    if (localPlacedCount != currentPlaced)
                    {
                        localPlacedCount = currentPlaced;
                        UpdateQuestProgressUI();
                        Debug.Log($"[CrystalPuzzleQuestTrigger Offline] Tiến độ đặt ngọc: {currentPlaced}/{pedestals.Length}");
                    }
                }

                int totalNeeded = pedestals.Length;
                if (currentPlaced >= totalNeeded)
                {
                    CompleteQuest();
                    return;
                }
            }
        }

        // Định kỳ cập nhật UI
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
                localHudCtl.ShowQuest(true, this);
                localHudCtl.UpdateQuestDescription(questDescription, this);
                localHudCtl.UpdateQuestTitle(questTitle, this);

                Sprite targetIcon = questIconSprite;
                if (targetIcon == null && localHudCtl != null)
                {
                    targetIcon = localHudCtl.ngoc1Sprite;
                }
                if (targetIcon == null)
                {
                    targetIcon = Resources.Load<Sprite>("crystal_purple");
                }
                localHudCtl.UpdateQuestIcon(targetIcon, this);

                int current = isNetwork ? placedCount.Value : localPlacedCount;
                int total = (pedestals != null && pedestals.Length > 0) ? pedestals.Length : 2;

                if (current != lastPlacedCount)
                {
                    lastPlacedCount = current;
                    localHudCtl.UpdateQuestProgress(current, total, this);
                    Debug.Log($"[CrystalPuzzleQuestTrigger] Cập nhật tiến độ UI: {current}/{total}");
                }
            }
        }
    }

    private void CompleteQuest()
    {
        if (isQuestCompletedLocal) return;
        isQuestCompletedLocal = true;
        SaveManager.MarkQuestCompleted("CrystalPuzzleQuest");

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
        Debug.Log("[CrystalPuzzleQuestTrigger] Đã đặt đủ ngọc lên các bệ! Nhiệm vụ hoàn thành.");

        if (localHudCtl == null)
        {
            localHudCtl = FindAnyObjectByType<PlayerHUDController>();
        }

        if (localHudCtl != null)
        {
            int total = (pedestals != null && pedestals.Length > 0) ? pedestals.Length : 2;
            localHudCtl.ShowQuest(true, this);
            localHudCtl.UpdateQuestProgress(total, total, this);
            localHudCtl.UpdateQuestDescription("Nhiệm vụ hoàn thành: Đã đặt đủ ngọc lên các phiến đá!", this);
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
            Debug.Log("[CrystalPuzzleQuestTrigger] Đã ẩn UI nhiệm vụ hoàn thành.");
        }

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
            if (isNetwork)
            {
                if (!isQuestActive.Value)
                {
                    if (IsServer)
                    {
                        isQuestActive.Value = true;
                        Debug.Log($"[CrystalPuzzleQuestTrigger Server] Người chơi '{other.gameObject.name}' chạm Trigger - Kích hoạt nhiệm vụ cho toàn bộ mạng!");
                    }
                    else
                    {
                        RequestActivateQuestServerRpc();
                    }
                }
            }
            else
            {
                hasTriggeredQuest = true;
                lastPlacedCount = -1;
                UpdateQuestProgressUI();
                Debug.Log("[CrystalPuzzleQuestTrigger Offline] Người chơi chạm Trigger - Kích hoạt nhiệm vụ.");
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestActivateQuestServerRpc()
    {
        if (!isQuestActive.Value)
        {
            isQuestActive.Value = true;
            Debug.Log("[CrystalPuzzleQuestTrigger ServerRpc] Client yêu cầu kích hoạt nhiệm vụ đặt ngọc cho toàn bộ mạng!");
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
