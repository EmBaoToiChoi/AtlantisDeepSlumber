using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class ElementalPillarQuestTrigger : NetworkBehaviour, IQuestTrigger
{
    public bool IsQuestCompleted => (SaveManager.IsContinueMode && SaveManager.IsQuestCompleted("ElementalPillarQuest")) || 
        ((NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) ? isQuestCompletedNet.Value : isQuestCompletedLocal);

    public bool IsQuestActive => !(SaveManager.IsContinueMode && SaveManager.IsQuestCompleted("ElementalPillarQuest")) && 
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

    [Header("Elemental Pillars Configuration")]
    [Tooltip("Danh sách 4 trụ nguyên tố (ElementalPillar)")]
    public ElementalPillar[] pillars;

    [Header("Ascension Puzzle Manager (Optional)")]
    [Tooltip("Kéo AscensionManager vào đây để tự động kiểm tra đáp án nguyên tố chính xác")]
    public AscensionManager ascensionManager;

    [Header("Quest UI Settings")]
    [Tooltip("Tiêu đề nhiệm vụ hiển thị trên UI")]
    public string questTitle = "NGUYÊN TỐ";

    [Tooltip("Icon nhiệm vụ hiển thị bên cạnh tiêu đề")]
    public Sprite questIconSprite;

    [Tooltip("Nội dung mô tả nhiệm vụ hiển thị trên UI")]
    [TextArea(3, 5)]
    public string questDescription = "Hãy chưởng các kỹ năng vào các trụ đá thích hợp.";

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

    // Biến mạng đồng bộ số lượng trụ đã kích hoạt
    public NetworkVariable<int> activatedCount = new NetworkVariable<int>(
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
    private int lastActivatedCount = -1;
    private int localActivatedCount = 0;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider == null)
        {
            Debug.LogWarning($"[ElementalPillarQuestTrigger] GameObject '{gameObject.name}' chưa có Collider trigger. Cần có Box Collider trigger để tự động phát hiện người chơi khi lại gần vùng trụ.");
        }
        else if (!triggerCollider.isTrigger)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void Start()
    {
        if (pillars == null || pillars.Length == 0)
        {
            pillars = FindObjectsByType<ElementalPillar>(FindObjectsSortMode.None);
            if (pillars != null && pillars.Length > 0)
            {
                Debug.Log($"[ElementalPillarQuestTrigger] Tự động tìm thấy {pillars.Length} trụ nguyên tố (ElementalPillar).");
            }
            else
            {
                Debug.LogWarning("[ElementalPillarQuestTrigger] CẢNH BÁO: Chưa gán pillars (các trụ nguyên tố) trong Inspector!");
            }
        }

        if (ascensionManager == null)
        {
            ascensionManager = FindFirstObjectByType<AscensionManager>();
        }
    }

    public override void OnNetworkSpawn()
    {
        isQuestActive.OnValueChanged += OnQuestActiveChanged;
        activatedCount.OnValueChanged += OnActivatedCountChanged;
        isQuestCompletedNet.OnValueChanged += OnQuestCompletedChanged;

        if (isQuestCompletedNet.Value || (SaveManager.IsContinueMode && SaveManager.IsQuestCompleted("ElementalPillarQuest")))
        {
            isQuestCompletedLocal = true;
            SaveManager.MarkQuestCompleted("ElementalPillarQuest");
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
        activatedCount.OnValueChanged -= OnActivatedCountChanged;
        isQuestCompletedNet.OnValueChanged -= OnQuestCompletedChanged;
    }

    private void OnQuestCompletedChanged(bool oldVal, bool newVal)
    {
        if (newVal)
        {
            isQuestCompletedLocal = true;
            SaveManager.MarkQuestCompleted("ElementalPillarQuest");
            ShowCompletionUI();
        }
    }

    private void OnQuestActiveChanged(bool oldVal, bool newVal)
    {
        if (newVal && IsPrerequisiteCompleted() && !IsQuestCompleted)
        {
            hasTriggeredQuest = true;
            lastActivatedCount = -1;
            UpdateQuestProgressUI();
        }
    }

    private bool IsPuzzleSolved()
    {
        // 1. Nếu có AscensionManager, bắt buộc kiểm tra xem đáp án đã ĐÚNG hay chưa
        if (ascensionManager != null)
        {
            if (ascensionManager.passwordPillar != null)
            {
                return ascensionManager.passwordPillar.isSolved.Value;
            }

            if (ascensionManager.correctCombination != null && ascensionManager.pillarPositions != null && ascensionManager.correctCombination.Length > 0)
            {
                if (ascensionManager.pillarPositions.Length == ascensionManager.correctCombination.Length)
                {
                    for (int i = 0; i < ascensionManager.pillarPositions.Length; i++)
                    {
                        var pTrans = ascensionManager.pillarPositions[i];
                        if (pTrans == null) return false;
                        if (pTrans.TryGetComponent<ElementalPillar>(out var p))
                        {
                            if (p == null || p.currentElement == null || p.currentElement.Value != ascensionManager.correctCombination[i])
                            {
                                return false;
                            }
                        }
                        else return false;
                    }
                    return true;
                }
            }
        }

        // 2. Nếu không có AscensionManager, kiểm tra xem tất cả các trụ trong mảng pillars đã kích hoạt hết chưa
        int totalNeeded = (pillars != null && pillars.Length > 0) ? pillars.Length : 4;
        int currentActivated = 0;
        if (pillars != null && pillars.Length > 0)
        {
            foreach (var pillar in pillars)
            {
                if (pillar != null && pillar.currentElement != null && pillar.currentElement.Value != ElementType.None)
                {
                    currentActivated++;
                }
            }
        }
        return currentActivated >= totalNeeded;
    }

    private void OnActivatedCountChanged(int oldVal, int newVal)
    {
        if (isQuestActive.Value && IsPrerequisiteCompleted() && !IsQuestCompleted)
        {
            lastActivatedCount = -1; // Ép cập nhật tiến độ UI ngay cả khi đếm lại từ 0
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

        // Server / Offline kiểm tra tiến độ kích hoạt từ AscensionManager hoặc pillars
        if (!isNetwork || IsServer)
        {
            bool active = isNetwork ? isQuestActive.Value : hasTriggeredQuest;
            if (active)
            {
                int currentActivated = 0;
                if (pillars != null && pillars.Length > 0)
                {
                    foreach (var pillar in pillars)
                    {
                        if (pillar != null && pillar.currentElement != null && pillar.currentElement.Value != ElementType.None)
                        {
                            currentActivated++;
                        }
                    }
                }
                else if (ascensionManager != null && ascensionManager.pillarPositions != null)
                {
                    foreach (var pTrans in ascensionManager.pillarPositions)
                    {
                        if (pTrans != null && pTrans.TryGetComponent<ElementalPillar>(out var pillar))
                        {
                            if (pillar != null && pillar.currentElement != null && pillar.currentElement.Value != ElementType.None)
                            {
                                currentActivated++;
                            }
                        }
                    }
                }

                if (isNetwork)
                {
                    if (activatedCount.Value != currentActivated)
                    {
                        activatedCount.Value = currentActivated;
                        Debug.Log($"[ElementalPillarQuestTrigger Server] Tiến độ trụ nguyên tố: {currentActivated}/4");
                    }
                }
                else
                {
                    if (localActivatedCount != currentActivated)
                    {
                        localActivatedCount = currentActivated;
                        UpdateQuestProgressUI();
                        Debug.Log($"[ElementalPillarQuestTrigger Offline] Tiến độ trụ nguyên tố: {currentActivated}/4");
                    }
                }

                // CHỈ KHÓA HOÀN THÀNH NHIỆM VỤ KHI CÂU ĐỐ ĐÃ GIẢI ĐÚNG ĐÁP ÁN!
                if (IsPuzzleSolved())
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
                localHudCtl.UpdateQuestIcon(questIconSprite, this);

                int current = isNetwork ? activatedCount.Value : localActivatedCount;
                int total = (pillars != null && pillars.Length > 0) ? pillars.Length : 4;

                if (current != lastActivatedCount)
                {
                    lastActivatedCount = current;
                    localHudCtl.UpdateQuestProgress(current, total, this);
                    Debug.Log($"[ElementalPillarQuestTrigger] Cập nhật tiến độ UI: {current}/{total}");
                }
            }
        }
    }

    private void CompleteQuest()
    {
        if (isQuestCompletedLocal) return;
        isQuestCompletedLocal = true;
        SaveManager.MarkQuestCompleted("ElementalPillarQuest");

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
        Debug.Log("[ElementalPillarQuestTrigger] Đã chưởng đủ 4 trụ nguyên tố! Nhiệm vụ hoàn thành.");

        if (localHudCtl == null)
        {
            localHudCtl = FindAnyObjectByType<PlayerHUDController>();
        }

        if (localHudCtl != null)
        {
            int total = (pillars != null && pillars.Length > 0) ? pillars.Length : 4;
            localHudCtl.ShowQuest(true, this);
            localHudCtl.UpdateQuestProgress(total, total, this);
            localHudCtl.UpdateQuestDescription("Nhiệm vụ hoàn thành: Đã kích hoạt 4 trụ nguyên tố!", this);
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
            Debug.Log("[ElementalPillarQuestTrigger] Đã ẩn UI nhiệm vụ hoàn thành.");
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
                        Debug.Log($"[ElementalPillarQuestTrigger Server] Người chơi '{other.gameObject.name}' chạm Trigger - Kích hoạt nhiệm vụ cho toàn bộ mạng!");
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
                lastActivatedCount = -1;
                UpdateQuestProgressUI();
                Debug.Log("[ElementalPillarQuestTrigger Offline] Người chơi chạm Trigger - Kích hoạt nhiệm vụ.");
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestActivateQuestServerRpc()
    {
        if (!isQuestActive.Value)
        {
            isQuestActive.Value = true;
            Debug.Log("[ElementalPillarQuestTrigger ServerRpc] Client yêu cầu kích hoạt nhiệm vụ trụ nguyên tố cho toàn bộ mạng!");
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
