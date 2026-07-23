using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class ChargePillarQuestTrigger : NetworkBehaviour, IQuestTrigger
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

    [Header("Pillars Configuration")]
    [Tooltip("Danh sách 3 trụ cần sạc điện (Kéo 3 GameObject trụ như FirePillarActivator, FinalEnergyPillar, hoặc PillarInteract vào đây)")]
    public MonoBehaviour[] pillars;

    [Header("Quest UI Settings")]
    [Tooltip("Tiêu đề nhiệm vụ hiển thị trên UI")]
    public string questTitle = "SẠC ĐIỆN TRỤ";

    [Tooltip("Icon nhiệm vụ hiển thị bên cạnh tiêu đề")]
    public Sprite questIconSprite;

    [Tooltip("Nội dung mô tả nhiệm vụ hiển thị trên UI")]
    [TextArea(3, 5)]
    public string questDescription = "Hãy dùng kỹ năng của nhân vật để sạc điện 3 trụ.";

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

    // Biến mạng đồng bộ số lượng trụ đã sạc điện
    public NetworkVariable<int> chargedCount = new NetworkVariable<int>(
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
    private int lastChargedCount = -1;
    private int localChargedCount = 0;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider == null)
        {
            Debug.LogWarning($"[ChargePillarQuestTrigger] GameObject '{gameObject.name}' chưa có Collider trigger. Cần có Box Collider trigger để tự động phát hiện người chơi khi lại gần vùng trụ.");
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
            var firePillars = FindObjectsByType<FirePillarActivator>(FindObjectsSortMode.None);
            if (firePillars != null && firePillars.Length > 0)
            {
                pillars = firePillars;
                Debug.Log($"[ChargePillarQuestTrigger] Tự động tìm thấy {pillars.Length} trụ FirePillarActivator.");
            }
            else
            {
                var energyPillars = FindObjectsByType<FinalEnergyPillar>(FindObjectsSortMode.None);
                if (energyPillars != null && energyPillars.Length > 0)
                {
                    pillars = energyPillars;
                    Debug.Log($"[ChargePillarQuestTrigger] Tự động tìm thấy {pillars.Length} trụ FinalEnergyPillar.");
                }
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        isQuestActive.OnValueChanged += OnQuestActiveChanged;
        chargedCount.OnValueChanged += OnChargedCountChanged;

        if (isQuestActive.Value && IsPrerequisiteCompleted())
        {
            hasTriggeredQuest = true;
            UpdateQuestProgressUI();
        }
    }

    public override void OnNetworkDespawn()
    {
        isQuestActive.OnValueChanged -= OnQuestActiveChanged;
        chargedCount.OnValueChanged -= OnChargedCountChanged;
    }

    private void OnQuestActiveChanged(bool oldVal, bool newVal)
    {
        if (newVal && IsPrerequisiteCompleted())
        {
            hasTriggeredQuest = true;
            lastChargedCount = -1;
            UpdateQuestProgressUI();
        }
    }

    private void OnChargedCountChanged(int oldVal, int newVal)
    {
        if (isQuestActive.Value && IsPrerequisiteCompleted())
        {
            UpdateQuestProgressUI();
        }
    }

    private bool IsPillarCharged(MonoBehaviour pillar)
    {
        if (pillar == null) return false;

        if (pillar is FirePillarActivator f) return f.IsActivated;
        if (pillar is FinalEnergyPillar e) return e.IsActivated;
        if (pillar is PillarInteract p) return p.IsCorrectDirection();

        var prop = pillar.GetType().GetProperty("IsActivated");
        if (prop != null && prop.PropertyType == typeof(bool))
        {
            return (bool)prop.GetValue(pillar);
        }

        var field = pillar.GetType().GetField("isActivated");
        if (field != null && field.FieldType == typeof(bool))
        {
            return (bool)field.GetValue(pillar);
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

        // Server / Offline kiểm tra tiến độ sạc điện các trụ
        if (!isNetwork || IsServer)
        {
            bool active = isNetwork ? isQuestActive.Value : hasTriggeredQuest;
            if (active && pillars != null && pillars.Length > 0)
            {
                int currentCharged = 0;
                foreach (var pillar in pillars)
                {
                    if (IsPillarCharged(pillar))
                    {
                        currentCharged++;
                    }
                }

                if (isNetwork)
                {
                    if (chargedCount.Value != currentCharged)
                    {
                        chargedCount.Value = currentCharged;
                        Debug.Log($"[ChargePillarQuestTrigger Server] Tiến độ sạc điện trụ: {currentCharged}/{pillars.Length}");
                    }
                }
                else
                {
                    if (localChargedCount != currentCharged)
                    {
                        localChargedCount = currentCharged;
                        UpdateQuestProgressUI();
                        Debug.Log($"[ChargePillarQuestTrigger Offline] Tiến độ sạc điện trụ: {currentCharged}/{pillars.Length}");
                    }
                }

                int totalNeeded = pillars.Length;
                if (currentCharged >= totalNeeded)
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

                int current = isNetwork ? chargedCount.Value : localChargedCount;
                int total = (pillars != null && pillars.Length > 0) ? pillars.Length : 3;

                if (current != lastChargedCount)
                {
                    lastChargedCount = current;
                    localHudCtl.UpdateQuestProgress(current, total, this);
                    Debug.Log($"[ChargePillarQuestTrigger] Cập nhật tiến độ UI: {current}/{total}");
                }
            }
        }
    }

    private void CompleteQuest()
    {
        isQuestCompleted = true;
        Debug.Log("[ChargePillarQuestTrigger] Đã sạc điện thành công tất cả 3 trụ! Nhiệm vụ hoàn thành.");

        if (localHudCtl == null)
        {
            localHudCtl = FindAnyObjectByType<PlayerHUDController>();
        }

        if (localHudCtl != null)
        {
            int total = (pillars != null && pillars.Length > 0) ? pillars.Length : 3;
            localHudCtl.ShowQuest(true, this);
            localHudCtl.UpdateQuestProgress(total, total, this);
            localHudCtl.UpdateQuestDescription("Nhiệm vụ hoàn thành: Đã sạc điện thành công 3 trụ!", this);
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
            Debug.Log("[ChargePillarQuestTrigger] Đã ẩn UI nhiệm vụ hoàn thành.");
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
        if (isNetwork && !IsServer) return;

        if (IsPlayer(other.gameObject))
        {
            if (isNetwork)
            {
                if (!isQuestActive.Value)
                {
                    isQuestActive.Value = true;
                    Debug.Log($"[ChargePillarQuestTrigger Server] Người chơi '{other.gameObject.name}' chạm Trigger - Kích hoạt nhiệm vụ sạc điện 3 trụ cho toàn bộ mạng!");
                }
            }
            else
            {
                hasTriggeredQuest = true;
                lastChargedCount = -1;
                UpdateQuestProgressUI();
                Debug.Log("[ChargePillarQuestTrigger Offline] Người chơi chạm Trigger - Kích hoạt nhiệm vụ sạc điện 3 trụ.");
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
