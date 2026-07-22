using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;

public class ZombieQuestTargetManager : NetworkBehaviour
{
    public static ZombieQuestTargetManager Instance;

    [Header("Quest Settings")]
    public string questTitle = "DỌN SẠCH KHU VỰC";
    [TextArea(3, 5)]
    public string questDescription = "Tiêu diệt tất cả Zombie đang bị giam cầm trong các lồng.";
    public Sprite questIconSprite;

    [Header("Quest Targets")]
    public List<DestroyReporter> zombieTargets = new List<DestroyReporter>();

    [Header("Delay Settings")]
    public float hideDelayAfterComplete = 3f;

    // ==========================================
    // THÊM 2 BIẾN NÀY ĐỂ KÍCH HOẠT SAU NHIỆM VỤ
    // ==========================================
    [Header("Next Actions (Sau Nhiệm Vụ)")]
    [Tooltip("Kéo cục Cutscene của bạn vào đây")]
    public VideoCutsceneController cutsceneToPlayAfter;
    
    [Tooltip("Kéo Object bạn muốn BẬT LÊN sau khi xong nhiệm vụ (VD: Cánh cửa mới, ngọc rớt ra...)")]
    public GameObject objectToEnableAfterQuest;
    // ==========================================

    // --- BIẾN ĐỒNG BỘ MẠNG ---
    private NetworkVariable<int> currentKills = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> isQuestActive = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> isQuestCompleted = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    private int totalKillsNeeded = 0;
    private PlayerHUDController localHudCtl;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        totalKillsNeeded = (zombieTargets != null) ? zombieTargets.Count : 0;
    }

    public override void OnNetworkSpawn()
    {
        currentKills.OnValueChanged += OnKillsChanged;
        isQuestActive.OnValueChanged += OnQuestActiveChanged;
        isQuestCompleted.OnValueChanged += OnQuestCompletedChanged;

        if (isQuestActive.Value && !isQuestCompleted.Value)
        {
            UpdateQuestUI();
        }
    }

    public override void OnNetworkDespawn()
    {
        currentKills.OnValueChanged -= OnKillsChanged;
        isQuestActive.OnValueChanged -= OnQuestActiveChanged;
        isQuestCompleted.OnValueChanged -= OnQuestCompletedChanged;
    }

    public void StartQuest()
    {
        if (!IsServer) return;
        if (isQuestCompleted.Value || isQuestActive.Value) return;

        if (totalKillsNeeded == 0)
        {
            Debug.LogError($"[ZombieQuest] LỖI: Danh sách ZombieTargets trống!");
            return;
        }

        foreach (var zombie in zombieTargets)
        {
            if (zombie != null)
            {
                zombie.OnTargetDestroyed.AddListener(OnZombieKilledServer);
            }
        }

        currentKills.Value = 0;
        isQuestActive.Value = true;
    }

    private void OnZombieKilledServer()
    {
        if (!isQuestActive.Value || isQuestCompleted.Value) return;

        currentKills.Value++; 
        
        if (currentKills.Value >= totalKillsNeeded)
        {
            isQuestCompleted.Value = true;
            isQuestActive.Value = false;

            // ===============================================
            // GỌI CUTSCENE GỐC TỪ SERVER KHI ĐỦ SỐ KILL
            // ===============================================
            if (cutsceneToPlayAfter != null)
            {
                cutsceneToPlayAfter.StartCutscene();
            }
            // ===============================================
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
            CompleteQuestUI();

            // ===============================================
            // BẬT LẠI COLLIDER THAY VÌ BẬT NGUYÊN OBJECT
            // ===============================================
            if (objectToEnableAfterQuest != null)
            {
                Collider col = objectToEnableAfterQuest.GetComponent<Collider>();
                if (col != null)
                {
                    col.enabled = true; // Mở lại cho người chơi chạm vào
                }
            }
            // ===============================================
        }
    }

    private void UpdateQuestUI()
    {
        if (localHudCtl == null) localHudCtl = FindAnyObjectByType<PlayerHUDController>();

        if (localHudCtl != null)
        {
            localHudCtl.ShowQuest(true);
            localHudCtl.UpdateQuestTitle(questTitle);
            localHudCtl.UpdateQuestDescription(questDescription);
            localHudCtl.UpdateQuestIcon(questIconSprite);
            localHudCtl.UpdateQuestProgress(currentKills.Value, totalKillsNeeded);
        }
    }

    private void CompleteQuestUI()
    {
        if (localHudCtl == null) localHudCtl = FindAnyObjectByType<PlayerHUDController>();

        if (localHudCtl != null)
        {
            localHudCtl.ShowQuest(true);
            localHudCtl.UpdateQuestProgress(totalKillsNeeded, totalKillsNeeded);
            localHudCtl.UpdateQuestDescription("Hoàn thành: Khu vực đã an toàn!");
            StartCoroutine(HideQuestAfterDelay(hideDelayAfterComplete));
        }
    }

    private IEnumerator HideQuestAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (localHudCtl != null)
        {
            localHudCtl.ShowQuest(false);
        }
    }
}