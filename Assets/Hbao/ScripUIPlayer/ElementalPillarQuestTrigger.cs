using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class ElementalPillarQuestTrigger : NetworkBehaviour, IQuestTrigger
{
    public bool IsQuestCompleted => isQuestCompleted;
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
    private bool isQuestCompleted = false;

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

        if (isQuestActive.Value && IsPrerequisiteCompleted())
        {
            hasTriggeredQuest = true;
            UpdateQuestProgressUI();
        }
    }

    public override void OnNetworkDespawn()
    {
        isQuestActive.OnValueChanged -= OnQuestActiveChanged;
        activatedCount.OnValueChanged -= OnActivatedCountChanged;
    }

    private void OnQuestActiveChanged(bool oldVal, bool newVal)
    {
        if (newVal && IsPrerequisiteCompleted())
        {
            hasTriggeredQuest = true;
            lastActivatedCount = -1;
            UpdateQuestProgressUI();
        }
    }

    private void OnActivatedCountChanged(int oldVal, int newVal)
    {
        if (isQuestActive.Value && IsPrerequisiteCompleted())
        {
            UpdateQuestProgressUI();
        }
    }

    private bool IsPillarActivated(ElementalPillar pillar)
    {
        if (pillar == null) return false;
        if (pillar.currentElement != null)
        {
            return pillar.currentElement.Value != ElementType.None;
        }
        return false;
    }

    private void Update()
    {
        if (isQuestCompleted || !IsPrerequisiteCompleted()) return;

        if (localPlayer == null)
        {
            FindLocalPlayer();
        }

        bool isNetwork = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        // Server / Offline kiểm tra tiến độ kích hoạt các trụ nguyên tố
        if (!isNetwork || IsServer)
        {
            bool active = isNetwork ? isQuestActive.Value : hasTriggeredQuest;
            if (active && pillars != null && pillars.Length > 0)
            {
                int currentActivated = 0;
                bool allCorrect = true;

                for (int i = 0; i < pillars.Length; i++)
                {
                    var pillar = pillars[i];
                    if (IsPillarActivated(pillar))
                    {
                        currentActivated++;
                    }
                    else
                    {
                        allCorrect = false;
                    }

                    // Kiểm tra so khớp đáp án nguyên tố với AscensionManager
                    if (ascensionManager != null && ascensionManager.correctCombination != null && i < ascensionManager.correctCombination.Length)
                    {
                        if (pillar == null || pillar.currentElement.Value != ascensionManager.correctCombination[i])
                        {
                            allCorrect = false;
                        }
                    }
                }

                if (isNetwork)
                {
                    if (activatedCount.Value != currentActivated)
                    {
                        activatedCount.Value = currentActivated;
                        Debug.Log($"[ElementalPillarQuestTrigger Server] Tiến độ trụ nguyên tố: {currentActivated}/{pillars.Length}");
                    }
                }
                else
                {
                    if (localActivatedCount != currentActivated)
                    {
                        localActivatedCount = currentActivated;
                        UpdateQuestProgressUI();
                        Debug.Log($"[ElementalPillarQuestTrigger Offline] Tiến độ trụ nguyên tố: {currentActivated}/{pillars.Length}");
                    }
                }

                int totalNeeded = pillars.Length;
                // Chỉ hoàn thành nhiệm vụ khi cả 4 trụ đều bật VÀ đúng đáp án nguyên tố
                if (currentActivated >= totalNeeded && allCorrect)
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
        if (!IsPrerequisiteCompleted()) return;

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
        isQuestCompleted = true;
        Debug.Log("[ElementalPillarQuestTrigger] Đã chưởng kích hoạt đủ 4 trụ nguyên tố! Nhiệm vụ hoàn thành.");

        if (localHudCtl == null)
        {
            localHudCtl = FindAnyObjectByType<PlayerHUDController>();
        }

        if (localHudCtl != null)
        {
            int total = (pillars != null && pillars.Length > 0) ? pillars.Length : 4;
            localHudCtl.ShowQuest(true, this);
            localHudCtl.UpdateQuestProgress(total, total, this);
            localHudCtl.UpdateQuestDescription("Nhiệm vụ hoàn thành: Đã kích hoạt tất cả các trụ nguyên tố!", this);
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
