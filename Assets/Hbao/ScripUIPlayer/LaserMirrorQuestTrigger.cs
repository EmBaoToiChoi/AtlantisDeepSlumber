using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class LaserMirrorQuestTrigger : NetworkBehaviour, IQuestTrigger
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

    [Header("Laser & Mirror Puzzle Components")]
    [Tooltip("Trụ đá phát tia lửa ban đầu (FirePillarActivator - trụ_đá_optimized)")]
    public FirePillarActivator sourcePillar;

    [Tooltip("Danh sách 4 trụ gương xoay (RotatableMirrorPillar)")]
    public RotatableMirrorPillar[] mirrorPillars;

    [Tooltip("Trụ đích cuối cùng mở cửa (FinalEnergyPillar - TruFinal)")]
    public FinalEnergyPillar finalPillar;

    [Header("Quest UI Settings")]
    [Tooltip("Tiêu đề nhiệm vụ hiển thị trên UI")]
    public string questTitle = "DẪN TIA LỬA MỞ CỬA";

    [Tooltip("Icon nhiệm vụ hiển thị bên cạnh tiêu đề")]
    public Sprite questIconSprite;

    [Tooltip("Nội dung mô tả nhiệm vụ hiển thị trên UI")]
    [TextArea(3, 5)]
    public string questDescription = "Chưởng kỹ năng vào trụ đá, ấn F để xoay gương cho các tia lửa nối tiếp nhau đến trụ final để mở cửa.";

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

    // Biến mạng đồng bộ trạng thái đã kích hoạt trụ final mở cửa
    public NetworkVariable<bool> isFinalActivated = new NetworkVariable<bool>(
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
            Debug.LogWarning($"[LaserMirrorQuestTrigger] GameObject '{gameObject.name}' chưa có Collider trigger. Cần có Box Collider trigger để tự động phát hiện người chơi khi lại gần vùng puzzle.");
        }
        else if (!triggerCollider.isTrigger)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void Start()
    {
        if (sourcePillar == null)
        {
            sourcePillar = FindFirstObjectByType<FirePillarActivator>();
        }

        if (mirrorPillars == null || mirrorPillars.Length == 0)
        {
            mirrorPillars = FindObjectsByType<RotatableMirrorPillar>(FindObjectsSortMode.None);
        }

        if (finalPillar == null)
        {
            finalPillar = FindFirstObjectByType<FinalEnergyPillar>();
        }
    }

    public override void OnNetworkSpawn()
    {
        isQuestActive.OnValueChanged += OnQuestActiveChanged;
        isFinalActivated.OnValueChanged += OnFinalActivatedChanged;

        if (isQuestActive.Value && IsPrerequisiteCompleted())
        {
            hasTriggeredQuest = true;
            UpdateQuestProgressUI();
        }
    }

    public override void OnNetworkDespawn()
    {
        isQuestActive.OnValueChanged -= OnQuestActiveChanged;
        isFinalActivated.OnValueChanged -= OnFinalActivatedChanged;
    }

    private void OnQuestActiveChanged(bool oldVal, bool newVal)
    {
        if (newVal && IsPrerequisiteCompleted())
        {
            hasTriggeredQuest = true;
            lastProgressCount = -1;
            UpdateQuestProgressUI();
        }
    }

    private void OnFinalActivatedChanged(bool oldVal, bool newVal)
    {
        if (newVal && IsPrerequisiteCompleted())
        {
            UpdateQuestProgressUI();
        }
    }

    private bool IsFinalPillarActivated()
    {
        if (finalPillar != null)
        {
            return finalPillar.IsActivated;
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

        // Server / Offline kiểm tra trụ final đã nhận laser mở cửa chưa
        if (!isNetwork || IsServer)
        {
            bool active = isNetwork ? isQuestActive.Value : hasTriggeredQuest;
            if (active)
            {
                bool finalActive = IsFinalPillarActivated();

                if (isNetwork)
                {
                    if (isFinalActivated.Value != finalActive)
                    {
                        isFinalActivated.Value = finalActive;
                    }
                }
                else
                {
                    UpdateQuestProgressUI();
                }

                if (finalActive)
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

                bool finalActive = isNetwork ? isFinalActivated.Value : IsFinalPillarActivated();
                int current = finalActive ? 1 : 0;
                int total = 1;

                if (current != lastProgressCount || finalActive)
                {
                    lastProgressCount = current;
                    localHudCtl.UpdateQuestProgress(current, total, this);
                    Debug.Log($"[LaserMirrorQuestTrigger] Cập nhật tiến độ UI: {(finalActive ? "1/1" : "0/1")}");
                }
            }
        }
    }

    private void CompleteQuest()
    {
        isQuestCompleted = true;
        Debug.Log("[LaserMirrorQuestTrigger] Tia lửa đã dẫn thành công đến Trụ Final và mở cửa! Nhiệm vụ hoàn thành.");

        if (localHudCtl == null)
        {
            localHudCtl = FindAnyObjectByType<PlayerHUDController>();
        }

        if (localHudCtl != null)
        {
            localHudCtl.ShowQuest(true, this);
            localHudCtl.UpdateQuestProgress(1, 1, this);
            localHudCtl.UpdateQuestDescription("Nhiệm vụ hoàn thành: Tia lửa đã nối đến trụ final và cửa đã mở!", this);
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
            Debug.Log("[LaserMirrorQuestTrigger] Đã ẩn UI nhiệm vụ hoàn thành.");
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
                    Debug.Log($"[LaserMirrorQuestTrigger Server] Người chơi '{other.gameObject.name}' chạm Trigger - Kích hoạt nhiệm vụ xoay gương dẫn lửa cho toàn bộ mạng!");
                }
            }
            else
            {
                hasTriggeredQuest = true;
                lastProgressCount = -1;
                UpdateQuestProgressUI();
                Debug.Log("[LaserMirrorQuestTrigger Offline] Người chơi chạm Trigger - Kích hoạt nhiệm vụ xoay gương dẫn lửa.");
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
