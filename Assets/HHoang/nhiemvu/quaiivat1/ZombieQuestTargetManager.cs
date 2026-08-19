using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;

public class ZombieQuestTargetManager : NetworkBehaviour, IQuestTrigger
{
    public static ZombieQuestTargetManager Instance;

    public bool IsQuestCompleted => (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) 
        ? (isQuestCompleted != null && isQuestCompleted.Value) 
        : isQuestCompletedLocal;

    public bool IsQuestActive => (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) 
        ? (isQuestActive != null && isQuestActive.Value) 
        : isQuestActiveLocal;

    [Header("Quest Prerequisite Settings")]
    [Tooltip("Nhiệm vụ tiền đề bắt buộc phải hoàn thành trước khi nhiệm vụ này được hiển thị/kích hoạt")]
    public MonoBehaviour prerequisiteQuest;

    public bool IsPrerequisiteCompleted()
    {
        if (prerequisiteQuest == null) return true;
        if (prerequisiteQuest is IQuestTrigger quest) return quest.IsQuestCompleted;
        if (prerequisiteQuest is BridgeCollapseTrigger bridge) return bridge.IsBridgeRepaired();
        var trigger = prerequisiteQuest.GetComponent<IQuestTrigger>() ?? prerequisiteQuest.GetComponentInChildren<IQuestTrigger>();
        if (trigger != null) return trigger.IsQuestCompleted;
        return true;
    }

    [Header("Quest Settings")]
    public string questTitle = "DỌN SẠCH KHU VỰC";
    [TextArea(3, 5)]
    public string questDescription = "Tiêu diệt tất cả Zombie đang bị giam cầm trong các lồng.";
    public Sprite questIconSprite;

    [Header("Quest Targets")]
    public List<DestroyReporter> zombieTargets = new List<DestroyReporter>();

    [Header("Delay Settings")]
    public float hideDelayAfterComplete = 3f;

    [Header("Auto Start & UI Settings")]
    [Tooltip("Tự động kích hoạt nhiệm vụ khi vào game nếu điều kiện tiên quyết đã thỏa")]
    public bool autoStartIfPrerequisiteMet = true;
    [Tooltip("Khoảng thời gian (giây) giữa các lần kiểm tra cập nhật tiến trình trên UI")]
    public float checkInterval = 0.2f;

    // ==========================================
    // THÊM 2 BIẾN NÀY ĐỂ KÍCH HOẠT SAU NHIỆM VỤ
    // ==========================================
    [Header("Next Actions (Sau Nhiệm Vụ)")]
    [Tooltip("Kéo cục Cutscene của bạn vào đây")]
    public VideoCutsceneController cutsceneToPlayAfter;

    // ĐÃ ĐỔI THÀNH LIST CHO PHÉP KÉO NHIỀU OBJECT
    [Tooltip("Kéo DANH SÁCH các Object bạn muốn BẬT LÊN sau khi xong nhiệm vụ")]
    public List<GameObject> objectsToEnableAfterQuest = new List<GameObject>();
    // ==========================================

    // --- BIẾN ĐỒNG BỘ MẠNG ---
    private NetworkVariable<int> currentKills = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> isQuestActive = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> isQuestCompleted = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Fallback cho chế độ Singleplayer / Offline
    private bool isQuestActiveLocal = false;
    private bool isQuestCompletedLocal = false;
    private int localKills = 0;

    private int totalKillsNeeded = 0;
    private PlayerHUDController localHudCtl;
    private float nextCheckTime = 0f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (zombieTargets != null && zombieTargets.Count > 0)
        {
            totalKillsNeeded = zombieTargets.Count;
        }
    }

    private void Start()
    {
        if (totalKillsNeeded <= 0)
        {
            totalKillsNeeded = GetTotalKillsNeeded();
        }
    }

    public override void OnNetworkSpawn()
    {
        currentKills.OnValueChanged += OnKillsChanged;
        isQuestActive.OnValueChanged += OnQuestActiveChanged;
        isQuestCompleted.OnValueChanged += OnQuestCompletedChanged;

        if (isQuestCompleted.Value)
        {
            isQuestCompletedLocal = true;
        }
        else if (isQuestActive.Value && IsPrerequisiteCompleted())
        {
            isQuestActiveLocal = true;
            UpdateQuestUI();
        }
    }

    public override void OnNetworkDespawn()
    {
        currentKills.OnValueChanged -= OnKillsChanged;
        isQuestActive.OnValueChanged -= OnQuestActiveChanged;
        isQuestCompleted.OnValueChanged -= OnQuestCompletedChanged;
    }

    private void Update()
    {
        if (IsQuestCompleted) return;

        bool isNetwork = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        // Tự động kích hoạt nhiệm vụ nếu đủ điều kiện tiên quyết và chưa active
        if (autoStartIfPrerequisiteMet && IsPrerequisiteCompleted() && !IsQuestActive)
        {
            if (!isNetwork || IsServer)
            {
                StartQuest();
            }
        }

        // Định kỳ cập nhật và làm mới UI nếu nhiệm vụ đang active
        if (IsQuestActive)
        {
            if (Time.time >= nextCheckTime)
            {
                nextCheckTime = Time.time + checkInterval;
                UpdateQuestUI();
            }
        }
    }

    public int GetTotalKillsNeeded()
    {
        if (totalKillsNeeded > 0) return totalKillsNeeded;
        if (zombieTargets != null && zombieTargets.Count > 0)
        {
            totalKillsNeeded = zombieTargets.Count;
            return totalKillsNeeded;
        }
        return totalKillsNeeded;
    }

    public void StartQuest()
    {
        bool isNetwork = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        if (isNetwork && !IsServer)
        {
            StartQuestServerRpc();
            return;
        }

        if (!IsPrerequisiteCompleted())
        {
            Debug.Log($"[ZombieQuest] Chưa hoàn thành nhiệm vụ tiền đề '{(prerequisiteQuest != null ? prerequisiteQuest.gameObject.name : "null")}'. Không thể khởi chạy.");
            return;
        }

        if (IsQuestCompleted || IsQuestActive) return;

        totalKillsNeeded = GetTotalKillsNeeded();
        if (totalKillsNeeded == 0)
        {
            Debug.LogError($"[ZombieQuest] LỖI: Danh sách ZombieTargets trống!");
            return;
        }

        foreach (var zombie in zombieTargets)
        {
            if (zombie != null)
            {
                zombie.OnTargetDestroyed.RemoveListener(OnZombieKilledServer);
                zombie.OnTargetDestroyed.AddListener(OnZombieKilledServer);
            }
        }

        if (isNetwork)
        {
            currentKills.Value = 0;
            isQuestActive.Value = true;
        }
        else
        {
            localKills = 0;
            isQuestActiveLocal = true;
        }

        UpdateQuestUI();
    }

    [ServerRpc(RequireOwnership = false)]
    public void StartQuestServerRpc()
    {
        StartQuest();
    }

    private void OnZombieKilledServer()
    {
        bool isNetwork = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (isNetwork && !IsServer) return;
        if (!IsQuestActive || IsQuestCompleted) return;

        int kills = 0;
        int target = GetTotalKillsNeeded();

        if (isNetwork)
        {
            currentKills.Value++;
            kills = currentKills.Value;
        }
        else
        {
            localKills++;
            kills = localKills;
            UpdateQuestUI();
        }

        if (kills >= target)
        {
            CompleteQuest();
        }
    }

    public void CompleteQuest()
    {
        bool isNetwork = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (isNetwork && !IsServer) return;

        if (isNetwork)
        {
            isQuestCompleted.Value = true;
            isQuestActive.Value = false;
        }
        else
        {
            isQuestCompletedLocal = true;
            isQuestActiveLocal = false;
            CompleteQuestUI();
            EnablePostQuestObjects();
        }

        // Kích hoạt cutscene sau khi hoàn thành nhiệm vụ
        if (cutsceneToPlayAfter != null)
        {
            cutsceneToPlayAfter.StartCutscene();
        }
    }

    private void OnKillsChanged(int oldVal, int newVal)
    {
        if (isQuestActive.Value) UpdateQuestUI();
    }

    private void OnQuestActiveChanged(bool oldVal, bool newVal)
    {
        if (newVal == true) UpdateQuestUI();
    }

    private void OnQuestCompletedChanged(bool oldVal, bool newVal)
    {
        if (newVal == true)
        {
            isQuestCompletedLocal = true;
            CompleteQuestUI();
            EnablePostQuestObjects();
        }
    }

    private void EnablePostQuestObjects()
    {
        foreach (var obj in objectsToEnableAfterQuest)
        {
            if (obj != null)
            {
                Collider col = obj.GetComponent<Collider>();
                if (col != null)
                {
                    col.enabled = true; // Mở lại cho người chơi chạm vào
                }
            }
        }
    }

    private void UpdateQuestUI()
    {
        if (!IsPrerequisiteCompleted() || IsQuestCompleted) return;

        if (localHudCtl == null) localHudCtl = FindAnyObjectByType<PlayerHUDController>();

        if (localHudCtl != null)
        {
            bool isNetwork = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
            int current = isNetwork ? currentKills.Value : localKills;
            int target = GetTotalKillsNeeded();

            localHudCtl.ShowQuest(true, this);
            localHudCtl.UpdateQuestTitle(questTitle, this);
            localHudCtl.UpdateQuestDescription(questDescription, this);
            localHudCtl.UpdateQuestIcon(questIconSprite, this);
            localHudCtl.UpdateQuestProgress(current, target, this);
        }
    }

    private void CompleteQuestUI()
    {
        if (localHudCtl == null) localHudCtl = FindAnyObjectByType<PlayerHUDController>();

        if (localHudCtl != null)
        {
            int target = GetTotalKillsNeeded();
            localHudCtl.ShowQuest(true, this);
            localHudCtl.UpdateQuestProgress(target, target, this);
            localHudCtl.UpdateQuestDescription("Hoàn thành: Khu vực đã an toàn!", this);
            localHudCtl.UpdateQuestTitle(questTitle, this);
            localHudCtl.UpdateQuestIcon(questIconSprite, this);
            StartCoroutine(HideQuestAfterDelay(hideDelayAfterComplete));
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
        }
    }

    // =========================================================================
    // CODE THÊM MỚI Ở ĐÂY: Hàm public để bật thủ công ô "Box Collider"
    // =========================================================================
    [Header("Manual Box Collider Settings")]
    [Tooltip("Kéo Object có chứa thành phần Box Collider mà bạn muốn bật thủ công vào đây")]
    public GameObject targetBoxColliderObject;

    /// <summary>
    /// Gọi hàm này từ bất kỳ đâu (Button, Script khác...) để bật dấu tick Box Collider.
    /// </summary>
    public void EnableSpecificBoxCollider()
    {
        if (targetBoxColliderObject != null)
        {
            BoxCollider boxCol = targetBoxColliderObject.GetComponent<BoxCollider>();
            if (boxCol != null)
            {
                boxCol.enabled = true; // Bật dấu tick "Box Collider"
                Debug.Log($"[ZombieQuest] Đã bật thành công Box Collider trên: {targetBoxColliderObject.name}");
            }
            else
            {
                Debug.LogWarning($"[ZombieQuest] Object {targetBoxColliderObject.name} không có BoxCollider!");
            }
        }
    }
    // =========================================================================
}