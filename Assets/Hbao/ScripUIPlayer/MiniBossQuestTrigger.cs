using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class MiniBossQuestTrigger : NetworkBehaviour, IQuestTrigger
{
    public bool IsQuestCompleted => (SaveManager.IsContinueMode && SaveManager.IsQuestCompleted("MiniBossQuest")) || 
        ((NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) ? isQuestCompletedNet.Value : isQuestCompletedLocal);

    public bool IsQuestActive => !(SaveManager.IsContinueMode && SaveManager.IsQuestCompleted("MiniBossQuest")) && 
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

    [Header("Mini Boss Target Settings")]
    [Tooltip("Kéo GameObject Mini Boss (có component MiniBossAI) vào đây")]
    public MiniBossAI miniBoss;

    [Header("Quest UI Settings")]
    [Tooltip("Tiêu đề nhiệm vụ hiển thị trên UI")]
    public string questTitle = "TIÊU DIỆT MINI BOSS";

    [Tooltip("Icon nhiệm vụ hiển thị bên cạnh tiêu đề")]
    public Sprite questIconSprite;

    [Tooltip("Nội dung mô tả nhiệm vụ hiển thị trên UI")]
    [TextArea(3, 5)]
    public string questDescription = "Tiêu diệt Mini Boss đang trấn giữ khu vực.";

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

    // Biến mạng đồng bộ số lượng Mini Boss đã diệt (0 hoặc 1)
    public NetworkVariable<int> bossDefeatedCount = new NetworkVariable<int>(
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
    private int lastBossDefeatedCount = -1;
    private int localBossDefeatedCount = 0;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider == null)
        {
            Debug.LogWarning($"[MiniBossQuestTrigger] GameObject '{gameObject.name}' chưa có Collider trigger. Cần có Box Collider trigger để tự động phát hiện người chơi khi bước vào vùng Mini Boss.");
        }
        else if (!triggerCollider.isTrigger)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void Start()
    {
        if (SaveManager.IsContinueMode && SaveManager.IsQuestCompleted("MiniBossQuest"))
        {
            isQuestCompletedLocal = true;
            if (miniBoss != null) miniBoss.gameObject.SetActive(false);
            return;
        }

        if (miniBoss == null)
        {
            miniBoss = FindFirstObjectByType<MiniBossAI>();
            if (miniBoss != null)
            {
                Debug.Log($"[MiniBossQuestTrigger] Tự động tìm thấy MiniBossAI trên '{miniBoss.gameObject.name}'.");
            }
            else
            {
                Debug.LogWarning("[MiniBossQuestTrigger] CẢNH BÁO: Chưa gán miniBoss (MiniBossAI) trong Inspector!");
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        isQuestActive.OnValueChanged += OnQuestActiveChanged;
        bossDefeatedCount.OnValueChanged += OnBossDefeatedCountChanged;
        isQuestCompletedNet.OnValueChanged += OnQuestCompletedChanged;

        if (isQuestCompletedNet.Value || (SaveManager.IsContinueMode && SaveManager.IsQuestCompleted("MiniBossQuest")))
        {
            isQuestCompletedLocal = true;
            SaveManager.MarkQuestCompleted("MiniBossQuest");
            if (miniBoss != null) miniBoss.gameObject.SetActive(false);
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
        bossDefeatedCount.OnValueChanged -= OnBossDefeatedCountChanged;
        isQuestCompletedNet.OnValueChanged -= OnQuestCompletedChanged;
    }

    private void OnQuestCompletedChanged(bool oldVal, bool newVal)
    {
        if (newVal)
        {
            isQuestCompletedLocal = true;
            SaveManager.MarkQuestCompleted("MiniBossQuest");
            if (miniBoss != null) miniBoss.gameObject.SetActive(false);
            ShowCompletionUI();
        }
    }

    private void OnQuestActiveChanged(bool oldVal, bool newVal)
    {
        if (newVal && IsPrerequisiteCompleted() && !IsQuestCompleted)
        {
            hasTriggeredQuest = true;
            lastBossDefeatedCount = -1;
            UpdateQuestProgressUI();
        }
    }

    private void OnBossDefeatedCountChanged(int oldVal, int newVal)
    {
        if (isQuestActive.Value && IsPrerequisiteCompleted() && !IsQuestCompleted)
        {
            UpdateQuestProgressUI();
        }
    }

    private bool IsBossDead()
    {
        if (miniBoss == null) return false;
        // Chỉ xác nhận hoàn thành khi CẢ 1 MINIBOSS CHÍNH VÀ 2 PHÂN THÂN ĐỀU ĐÃ BỊ TIÊU DIỆT HOÀN TOÀN
        return miniBoss.AreAllBossesAndClonesDead();
    }

    private void Update()
    {
        if (IsQuestCompleted || !IsPrerequisiteCompleted()) return;

        if (localPlayer == null)
        {
            FindLocalPlayer();
        }

        bool isNetwork = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        // Server / Offline kiểm tra Mini Boss đã bị hạ gục chưa
        if (!isNetwork || IsServer)
        {
            bool active = isNetwork ? isQuestActive.Value : hasTriggeredQuest;
            if (active && miniBoss != null)
            {
                int currentDefeated = IsBossDead() ? 1 : 0;

                if (isNetwork)
                {
                    if (bossDefeatedCount.Value != currentDefeated)
                    {
                        bossDefeatedCount.Value = currentDefeated;
                        Debug.Log($"[MiniBossQuestTrigger Server] Tiến độ tiêu diệt Mini Boss: {currentDefeated}/1");
                    }
                }
                else
                {
                    if (localBossDefeatedCount != currentDefeated)
                    {
                        localBossDefeatedCount = currentDefeated;
                        UpdateQuestProgressUI();
                        Debug.Log($"[MiniBossQuestTrigger Offline] Tiến độ tiêu diệt Mini Boss: {currentDefeated}/1");
                    }
                }

                if (currentDefeated >= 1)
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

                int current = isNetwork ? bossDefeatedCount.Value : localBossDefeatedCount;
                int total = 1;
                lastBossDefeatedCount = current;
                localHudCtl.UpdateQuestProgress(current, total, this);
            }
        }
    }

    private void CompleteQuest()
    {
        if (isQuestCompletedLocal) return;
        isQuestCompletedLocal = true;
        SaveManager.MarkQuestCompleted("MiniBossQuest");

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
        Debug.Log("[MiniBossQuestTrigger] Đã tiêu diệt Mini Boss! Nhiệm vụ hoàn thành.");

        if (localHudCtl == null)
        {
            localHudCtl = FindAnyObjectByType<PlayerHUDController>();
        }

        if (localHudCtl != null)
        {
            localHudCtl.ShowQuest(true, this);
            localHudCtl.UpdateQuestProgress(1, 1, this);
            localHudCtl.UpdateQuestDescription("Nhiệm vụ hoàn thành: Đã tiêu diệt Mini Boss!", this);
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
            Debug.Log("[MiniBossQuestTrigger] Đã ẩn UI nhiệm vụ hoàn thành.");
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
                        Debug.Log($"[MiniBossQuestTrigger Server] Người chơi '{other.gameObject.name}' chạm Trigger - Kích hoạt nhiệm vụ cho toàn bộ mạng!");
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
                lastBossDefeatedCount = -1;
                UpdateQuestProgressUI();
                Debug.Log("[MiniBossQuestTrigger Offline] Người chơi chạm Trigger - Kích hoạt nhiệm vụ.");
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestActivateQuestServerRpc()
    {
        if (!isQuestActive.Value)
        {
            isQuestActive.Value = true;
            Debug.Log("[MiniBossQuestTrigger ServerRpc] Client yêu cầu kích hoạt nhiệm vụ Mini Boss cho toàn bộ mạng!");
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
