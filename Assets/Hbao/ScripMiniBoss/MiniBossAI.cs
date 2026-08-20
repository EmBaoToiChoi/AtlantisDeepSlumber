using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.UIElements;

/// <summary>
/// Mini Boss AI Script using FSM (Finite State Machine).
/// States: Idle, Chase, Attack, Hit, Enrage, Dead.
/// Features:
///   - Super Armor & Stagger Cooldown to prevent hit-react stun lock loops.
///   - Dynamic target switching to nearest reachable player or weak/low-HP player.
///   - NavMesh reachability & edge stopping (prevents walking off unbaked maps/walls).
///   - Shadow Teleport Blink execution attack behind target players.
///   - 50% HP Shadow Clone Summoning Skill (triệu hồi 2 phân thân đồng chiến đấu).
///   - Alternates between 3 attacks: Nhaychemdat, NhayDanh, and XoayChem.
/// </summary>
public class MiniBossAI : NetworkBehaviour, ISwordRainOwner
{
    //cus
    [Header("Death Event Trigger")]
    [Tooltip("Kéo object chứa VideoCutsceneController vào đây và chọn hàm StartCutscene")]
    public UnityEvent onBossDeathEvent;

    public enum MiniBossState { Idle, Chase, Attack, SwordRain, Hit, Enrage, Dead }

    [Header("Health Settings")]
    public float phase1MaxHealth = 700f;
    public float phase2MaxHealth = 700f;
    [HideInInspector] public float maxHealth = 700f;
    public NetworkVariable<float> currentHealth = new NetworkVariable<float>(
        700f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Phase 2 Settings")]
    public float phase2SpeedMultiplier = 1.3f;
    public float phase2AnimSpeed = 1.35f;
    public float phase2CooldownMultiplier = 0.6f;
    public float enrageDuration = 3f;
    public string enrageTrigger = "Enrage";

    [Header("Phase 2 Enrage Transition Settings")]
    public GameObject enrageVFXPrefab;
    public AudioClip enrageSFXSound;

    [Header("Summon Clones Skill Settings (50% HP)")]
    public bool isClone = false; // Đánh dấu nếu đây là bản sao phân thân
    public GameObject clonePrefab; // Prefab phân thân (nếu null sẽ dùng chính bản thân MiniBoss)
    [Tooltip("Prefab hố tử thần / Ma trận triệu hồi hố tối dưới đất tại vị trí 2 phân thân nhô lên")]
    public GameObject summonHoleVFXPrefab;
    [Tooltip("Kích thước scale cho VFX vùng triệu hồi hố tử thần (mặc định 0.5)")]
    public float summonHoleVFXScale = 0.5f;
    public bool hasSummonedClones = false;
    public bool isSummonInvulnerable = false; // Trạng thái MIỄN THƯƠNG trong lúc đang gồng triệu hồi
    public NetworkVariable<int> summonCloneCounter = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Earth Spikes Skill Settings (5-10s triệu hồi 1 lần, 4-5 bãi đá)")]
    [Tooltip("Prefab hiệu ứng gai đất nhô lên VFX_Earth_Area_01")]
    public GameObject earthSpikesVFXPrefab;
    [Tooltip("Khoảng thời gian tối thiểu giữa các lần gọi kỹ năng Gai Đất (5-10 giây)")]
    public float earthSpikesIntervalMin = 5f;
    public float earthSpikesIntervalMax = 10f;
    [Tooltip("Sát thương mỗi lần đâm gai (-5 HP)")]
    public float earthSpikesDamage = 5f;
    [Tooltip("Thời gian chờ đòn gồng/vòng sáng vàng trước khi gai đá nhô lên và gây sát thương (1.85s)")]
    public float earthSpikesEmergenceDelay = 1.85f;
    private float earthSpikesTimer = 5f;

    [Header("Furious Charge Skill Settings (Chỉ thỉnh thoảng mới tăng tốc)")]
    public float furiousChargeSpeed = 15.5f;
    public float furiousChargeCooldown = 20.0f; // Cooldown 20 giây lâu lâu mới thực hiện 1 lần
    private float furiousChargeTimer = 0f;
    private bool isFuriousCharging = false;
    private float furiousChargeDurationTimer = 0f;

    [Header("Sword Rain (Mưa Kiếm 10s) Settings (5-10s triệu hồi 1 lần)")]
    public GameObject swordPrefab;
    public GameObject warningDecalPrefab;
    public GameObject swordImpactVFX;
    public AudioClip swordImpactSFX;
    public float swordRainDuration = 10.0f;
    public float swordSpawnInterval = 0.4f;
    public float warningDuration = 1.5f;
    public float swordDropSpeed = 35.0f;
    public float swordDamage = 15.0f;
    public float swordImpactRadius = 2.5f;
    public string swordRainTriggerParam = "AttackCombo";
    public Vector3 swordSpawnRotationOffset = new Vector3(90f, 0f, 0f); // Xoay bù để kiếm cắm thẳng xuống
    public float swordRainIntervalMin = 5.0f;
    public float swordRainIntervalMax = 10.0f;
    private float swordRainTimer = 5.0f;
    private bool isSwordRainActive = false;

    [Header("Shadow Teleport Skill Settings (Chỉ dùng khi lỗi địa hình)")]
    public float shadowBlinkCooldown = 18.0f;
    private float shadowBlinkTimer = 0f;
    public GameObject shadowBlinkVFX;
    public AudioClip shadowBlinkSFX;

    [Header("MiniBoss Synchronized Looping Audio Settings (Mọi người đều nghe)")]
    [Tooltip("Âm thanh / Nhạc nền lúc CHƯA triệu hồi phân thân (Trạng thái chiến đấu bình thường, tự động Loop)")]
    public AudioClip preSummonAudioClip;
    [Tooltip("Âm thanh / Nhạc nền LÚC TRIỆU HỒI phân thân (Trạng thái gồng skill triệu hồi 2 phân thân, tự động Loop)")]
    public AudioClip summonSkillAudioClip;
    [Tooltip("Âm lượng âm thanh MiniBoss (0.0 đến 1.0)")]
    [Range(0f, 1f)] public float bossAudioVolume = 0.85f;
    [Tooltip("Tự động lặp lại (Loop) âm thanh")]
    public bool loopBossAudio = true;

    [Tooltip("AudioSource dùng phát nhạc (Nếu để trống script sẽ tự tìm/tạo AudioSource)")]
    public AudioSource bossAudioSource;

    [Header("Network State Sync")]
    public NetworkVariable<MiniBossState> currentState = new NetworkVariable<MiniBossState>(
        MiniBossState.Idle, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> isBossActive = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Network Animation Sync Counters")]
    public NetworkVariable<float> netSpeed = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> attackCounter = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> attackTypeSync = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> hitCounter = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> dieCounter = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> isPhase2Network = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> enrageCounter = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> shadowBlinkCounter = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> swordRainCounter = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Standalone fallback variables
    private float localHealth;
    private MiniBossState localState = MiniBossState.Idle;
    private bool localIsBossActive = false;
    private bool localIsPhase2 = false;
    public bool isStandaloneMode = false;
    private bool IsNetworkActive => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

    public bool IsPhase2 => (isStandaloneMode || isClone) ? localIsPhase2 : isPhase2Network.Value;

    public MiniBossState CurrentStateValue
    {
        get => (isStandaloneMode || isClone) ? localState : currentState.Value;
        set { if (isStandaloneMode || isClone) localState = value; else currentState.Value = value; }
    }

    [Header("Clones Tracking & Network Synchronization")]
    public MiniBossAI clone1Instance;
    public MiniBossAI clone2Instance;
    private static bool isDefeatCutsceneSequenceRunning = false;

    // --- SERVER-AUTHORITATIVE DEATH & CLONE HEALTH TRACKING ---
    // Máu của 2 phân thân đồng bộ qua mạng bằng NetworkVariable trên Boss chính
    public NetworkVariable<float> clone1HealthNet = new NetworkVariable<float>(
        315f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> clone2HealthNet = new NetworkVariable<float>(
        315f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> clone1DeadNet = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> clone2DeadNet = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> mainBossDeadNet = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> allMiniBossEntitiesDeadNet = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Counter để đồng bộ cutscene defeat cho tất cả Client qua OnValueChanged callback
    public NetworkVariable<int> defeatCutsceneCounter = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public float ActualCurrentHealth => (isStandaloneMode || isClone || !IsSpawned) ? localHealth : currentHealth.Value;
    public bool IsBossActive => (isStandaloneMode || isClone) ? localIsBossActive : isBossActive.Value;
    public bool IsDead => CurrentStateValue == MiniBossState.Dead;
    // ISwordRainOwner: Cho FallingSwordProjectile biết boss đã chết → ngừng gây sát thương
    public bool IsOwnerDead => IsDead || ActualCurrentHealth <= 0;

    public bool AreAllBossesAndClonesDead()
    {
        if (allMiniBossEntitiesDeadNet.Value) return true;

        var mainBoss = FindMainBoss() ?? (isClone ? null : this);
        if (mainBoss != null && mainBoss.allMiniBossEntitiesDeadNet.Value) return true;

        if (mainBoss != null && mainBoss.IsSpawned && !mainBoss.isStandaloneMode)
        {
            bool mainDead = mainBoss.mainBossDeadNet.Value || mainBoss.IsDead || mainBoss.ActualCurrentHealth <= 0;
            bool c1Dead = !mainBoss.hasSummonedClones || mainBoss.clone1DeadNet.Value;
            bool c2Dead = !mainBoss.hasSummonedClones || mainBoss.clone2DeadNet.Value;
            return mainDead && c1Dead && c2Dead;
        }

        // Fallback Standalone
        var allBosses = FindObjectsByType<MiniBossAI>(FindObjectsSortMode.None);
        foreach (var b in allBosses)
        {
            if (b != null && b.gameObject.activeInHierarchy)
            {
                if (!b.IsDead && b.ActualCurrentHealth > 0f)
                {
                    return false;
                }
            }
        }
        return true;
    }

    /// <summary>
    /// Server kiểm tra xem cả MiniBoss chính và 2 phân thân đã chết hoàn toàn chưa.
    /// Nếu tất cả đã chết -> Kích hoạt biến mạng, broadcast dọn sạch phân thân và chạy Cutscene!
    /// </summary>
    public void CheckAndTriggerAllMiniBossesDead()
    {
        if (!IsServer && !isStandaloneMode) return;

        var mainBoss = FindMainBoss() ?? this;

        if (isStandaloneMode)
        {
            if (AreAllBossesAndClonesDead())
            {
                TriggerBossDefeatCutscene();
            }
            return;
        }

        bool mainDead = mainBoss.mainBossDeadNet.Value || mainBoss.IsDead || mainBoss.ActualCurrentHealth <= 0f;
        bool c1Dead = !mainBoss.hasSummonedClones || mainBoss.clone1DeadNet.Value;
        bool c2Dead = !mainBoss.hasSummonedClones || mainBoss.clone2DeadNet.Value;

        Debug.Log($"[MiniBossAI Server] CheckAndTriggerAllMiniBossesDead: MainDead={mainDead}, Clone1Dead={c1Dead}, Clone2Dead={c2Dead}");

        if (mainDead && c1Dead && c2Dead)
        {
            if (!mainBoss.allMiniBossEntitiesDeadNet.Value)
            {
                mainBoss.allMiniBossEntitiesDeadNet.Value = true;
                mainBoss.defeatCutsceneCounter.Value++;
                Debug.Log("[MiniBossAI Server] TẤT CẢ 3 MINIBOSS ĐÃ BỊ TIÊU DIỆT HOÀN TOÀN -> Gửi ClientRpc dọn sạch phân thân và chạy cutscene trên toàn bộ máy!");
                mainBoss.TriggerDefeatCutsceneAndCleanupClientRpc();
            }
        }
    }

    [Header("Components")]
    public NavMeshAgent agent;
    public Animator anim;
    [Tooltip("The root of the visual mesh child. Leaps/spins will offset this transform Y position to keep NavMesh tracking on the ground.")]
    public Transform visualRoot;
    [HideInInspector] public Vector3 initialVisualLocalPos = Vector3.zero;
    private bool hasStoredInitialVisualPos = false;

    public void StoreInitialVisualPos()
    {
        if (visualRoot != null && !hasStoredInitialVisualPos)
        {
            initialVisualLocalPos = visualRoot.localPosition;
            hasStoredInitialVisualPos = true;
        }
    }
    [Tooltip("Raycast eye height target for detecting players")]
    public Transform eyeTransform;
    public Transform swordBase;
    public Transform swordTip;

    [Header("Movement Speeds")]
    public float walkSpeed = 2.2f;
    public float runSpeed = 5.8f;

    [Header("AI Vision & Attack Ranges")]
    public float sightRange = 22f;
    public float fieldOfView = 140f;
    public float attackRange = 3.5f;
    public float attackCooldown = 2.2f;
    private float attackCooldownTimer;

    [Header("Weapon & Damage Settings")]
    public float attackDamage = 35f;
    public float knockbackForce = 12f;
    public float swordThickness = 0.45f;
    public LayerMask playerLayer;
    public LayerMask obstacleLayer;

    [Header("Phase 2 Death Explosion Settings")]
    public GameObject deathExplosionVFX;
    public AudioClip deathExplosionSound;
    public float explosionRadius = 6f;
    public float explosionDamage = 50f;
    public float explosionKnockback = 20f;
    public NetworkVariable<int> deathExplosionCounter = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Animator Param Names")]
    public string speedParam = "Speed";       // Float: 0 = Idle, 0.5 = Walk, 1.0 = Run
    public string hitTrigger = "Hit";         // Trigger: Zombie Reaction Hit / Hit
    public string dieTrigger = "Die";         // Trigger: Sword and Shield Death / Die
    public string[] attackTriggers = new string[] { "Nhaychemdat", "NhayDanh", "XoayChem" }; // Triggers for the 3 attack animations

    [System.Serializable]
    public struct AttackConfig
    {
        public float duration;             // Duration of the attack state
        public float leapStartPercent;     // Percentage of duration when leap starts (0.0 to 1.0)
        public float leapDurationPercent;  // Percentage of duration when leap occurs
        public float forwardSpeed;         // Speed of movement during leap
        public float peakHeight;           // Height of the Y parabola leap
        public float damageStartPercent;   // Start of continuous damage window (0.0 to 1.0)
        public float damageEndPercent;     // End of continuous damage window (0.0 to 1.0)
    }

    [Header("Attack Configs (0: Nhaychemdat, 1: NhayDanh, 2: XoayChem)")]
    public AttackConfig[] attackConfigs = new AttackConfig[]
    {
        new AttackConfig { duration = 1.8f, leapStartPercent = 0.15f, leapDurationPercent = 0.5f, forwardSpeed = 10f, peakHeight = 3.5f, damageStartPercent = 0.48f, damageEndPercent = 0.72f },  // Nhaychemdat: jump and slam
        new AttackConfig { duration = 1.4f, leapStartPercent = 0.1f, leapDurationPercent = 0.45f, forwardSpeed = 12f, peakHeight = 2.5f, damageStartPercent = 0.28f, damageEndPercent = 0.62f },  // NhayDanh: fast forward leap
        new AttackConfig { duration = 2.0f, leapStartPercent = 0.05f, leapDurationPercent = 0.65f, forwardSpeed = 7f, peakHeight = 1.2f, damageStartPercent = 0.12f, damageEndPercent = 0.72f }   // XoayChem: spin and glide forward
    };

    private IEnemyState currentFSMState;
    private IdleState idleState;
    private ChaseState chaseState;
    private AttackState attackState;
    private SwordRainState swordRainState;
    private HitState hitState;
    private EnrageState enrageState;
    private DeadState deadState;

    private Transform targetPlayer;
    private float stateTimer;
    private float hitStaggerDuration = 0.35f;
    private float hitStaggerCooldownTimer = 0f;
    private bool hasDealtDamage;
    private int currentAttackIndex = 0;

    // Wander patrol state
    private bool hasWanderDestination;
    private Vector3 wanderDestination;
    private float wanderWaitTimer;

    // Leaping tracking
    private bool isLeaping = false;
    private float leapTimer = 0f;
    private float currentLeapDuration = 0f;
    private float currentLeapForwardSpeed = 0f;
    private float currentLeapHeight = 0f;

    // AI scan rate
    private float scanTimer;
    private float scanInterval = 0.15f;

    private bool AgentReady => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;

    private void CleanupConflictingEnemyScripts()
    {
        var dapBua = GetComponent<Enemy1_DapBua>();
        if (dapBua != null) DestroyImmediate(dapBua);

        var zombie = GetComponent<Enemy2_Zombie>();
        if (zombie != null) DestroyImmediate(zombie);

        var phuThuy = GetComponent<Enemy5_PhuThuy>();
        if (phuThuy != null) DestroyImmediate(phuThuy);

        var childBars = GetComponentsInChildren<EnemyHealthBar>(true);
        foreach (var hp in childBars)
        {
            if (hp != null && hp.gameObject != gameObject)
            {
                if (hp.enemy != null && hp.enemy.gameObject != gameObject)
                    DestroyImmediate(hp.enemy.gameObject);
                if (hp.enemy2 != null && hp.enemy2.gameObject != gameObject)
                    DestroyImmediate(hp.enemy2.gameObject);
                if (hp.enemy5 != null && hp.enemy5.gameObject != gameObject)
                    DestroyImmediate(hp.enemy5.gameObject);

                DestroyImmediate(hp.gameObject);
            }
        }
    }

    private void Awake()
    {
        gameObject.tag = "Enemy";
        CleanupConflictingEnemyScripts();
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (agent != null && !agent.enabled) agent.enabled = true;
        if (anim == null) anim = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);

        if (visualRoot == null && anim != null && anim.transform != transform)
        {
            visualRoot = anim.transform;
        }
        StoreInitialVisualPos();

        var na = GetComponent<Unity.Netcode.Components.NetworkAnimator>();
        if (na != null && anim != null)
        {
            na.Animator = anim;
        }

        maxHealth = phase1MaxHealth;
        localHealth = phase1MaxHealth;
        idleState = new IdleState(this);
        chaseState = new ChaseState(this);
        attackState = new AttackState(this);
        swordRainState = new SwordRainState(this);
        hitState = new HitState(this);
        enrageState = new EnrageState(this);
        deadState = new DeadState(this);
    }

    private void Start()
    {
        ResetEarthSpikesTimer();
        ResetSwordRainTimer();
        if (agent != null)
        {
            agent.stoppingDistance = 2.4f;
            agent.obstacleAvoidanceType = UnityEngine.AI.ObstacleAvoidanceType.LowQualityObstacleAvoidance;
        }

        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        if (!IsNetworkActive)
        {
            isStandaloneMode = true;
            InitStandalone();
        }
    }

    private void InitStandalone()
    {
        if (!isClone)
        {
            maxHealth = phase1MaxHealth;
            localHealth = phase1MaxHealth;
            localIsPhase2 = false;
        }
        SnapToNavMesh();
        ApplySpeedAnim(0f);
        ChangeState(MiniBossState.Idle);
    }

    public override void OnNetworkSpawn()
    {
        isStandaloneMode = false;

        netSpeed.OnValueChanged += (_, v) => ApplySpeedAnim(v);
        attackCounter.OnValueChanged += (_, _) => {
            if (anim != null && attackTypeSync.Value >= 0 && attackTypeSync.Value < attackTriggers.Length)
                anim.SetTrigger(attackTriggers[attackTypeSync.Value]);

            if (!IsServer && !isStandaloneMode)
            {
                StartCoroutine(ExecuteAttackLeapClientRoutine(attackTypeSync.Value));
            }
        };
        hitCounter.OnValueChanged += (_, _) => { if (anim != null) anim.SetTrigger(hitTrigger); };
        dieCounter.OnValueChanged += (_, _) => OnDieCounterChanged();
        defeatCutsceneCounter.OnValueChanged += (_, _) => OnDefeatCutsceneCounterChanged();
        enrageCounter.OnValueChanged += (_, _) => {
            if (anim != null) anim.SetTrigger(enrageTrigger);
            PlayEnrageVFX();
        };
        shadowBlinkCounter.OnValueChanged += (_, _) => PlayShadowBlinkVisuals();
        summonCloneCounter.OnValueChanged += (_, _) => PlaySummonCloneVisuals();
        swordRainCounter.OnValueChanged += (_, _) => TriggerSwordRainAnim();
        isPhase2Network.OnValueChanged += (oldVal, newVal) => {
            if (newVal)
            {
                maxHealth = phase2MaxHealth;
                if (anim != null) anim.speed = phase2AnimSpeed;
                Debug.Log("[MiniBossAI Client] Synced to Phase 2!");
            }
        };
        currentHealth.OnValueChanged += OnHealthNetChanged;
        deathExplosionCounter.OnValueChanged += (_, _) => PlayDeathExplosionEffects();
        allMiniBossEntitiesDeadNet.OnValueChanged += (oldVal, newVal) => {
            if (newVal)
            {
                ForceDestroyAllRemainingClones();
                var allUIDocs = FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var doc in allUIDocs)
                {
                    if (doc != null && doc.rootVisualElement != null)
                    {
                        var mbHud = doc.rootVisualElement.Q<VisualElement>("miniboss-hud-container");
                        if (mbHud != null) mbHud.style.display = DisplayStyle.None;
                    }
                }
                var hb = FindFirstObjectByType<MiniBossHealthBar>();
                if (hb != null) { hb.HideUI(); }
            }
        };

        ApplySpeedAnim(netSpeed.Value);

        if (IsServer)
        {
            if (isClone)
            {
                maxHealth = phase1MaxHealth * 0.45f;
                currentHealth.Value = maxHealth;
                localHealth = maxHealth;
                isBossActive.Value = true;
            }
            else
            {
                maxHealth = phase1MaxHealth;
                currentHealth.Value = phase1MaxHealth;
                ChangeState(MiniBossState.Idle);
            }
            SnapToNavMesh();
        }
        else
        {
            if (agent != null) agent.enabled = false;
        }

        if (isClone)
        {
            EnsureCloneOverheadHealthBar();
        }
    }

    public override void OnNetworkDespawn()
    {
        netSpeed.OnValueChanged -= (_, v) => ApplySpeedAnim(v);
        attackCounter.OnValueChanged -= (_, _) => {
            if (anim != null && attackTypeSync.Value >= 0 && attackTypeSync.Value < attackTriggers.Length)
                anim.SetTrigger(attackTriggers[attackTypeSync.Value]);
        };
        hitCounter.OnValueChanged -= (_, _) => { if (anim != null) anim.SetTrigger(hitTrigger); };
        dieCounter.OnValueChanged -= (_, _) => OnDieCounterChanged();
        defeatCutsceneCounter.OnValueChanged -= (_, _) => OnDefeatCutsceneCounterChanged();
        enrageCounter.OnValueChanged -= (_, _) => {
            if (anim != null) anim.SetTrigger(enrageTrigger);
            PlayEnrageVFX();
        };
        shadowBlinkCounter.OnValueChanged -= (_, _) => PlayShadowBlinkVisuals();
        summonCloneCounter.OnValueChanged -= (_, _) => PlaySummonCloneVisuals();
        swordRainCounter.OnValueChanged -= (_, _) => TriggerSwordRainAnim();
        currentHealth.OnValueChanged -= OnHealthNetChanged;
        deathExplosionCounter.OnValueChanged -= (_, _) => PlayDeathExplosionEffects();
    }

    private void TriggerSwordRainAnim()
    {
        if (anim != null)
        {
            if (!string.IsNullOrEmpty(swordRainTriggerParam) && HasParameter(anim, swordRainTriggerParam))
            {
                anim.SetTrigger(swordRainTriggerParam);
            }
            else if (!string.IsNullOrEmpty(enrageTrigger) && HasParameter(anim, enrageTrigger))
            {
                anim.SetTrigger(enrageTrigger);
            }
            else if (attackTriggers != null && attackTriggers.Length > 0 && HasParameter(anim, attackTriggers[0]))
            {
                anim.SetTrigger(attackTriggers[0]);
            }
        }
    }

    private static bool HasParameter(Animator animator, string paramName)
    {
        if (animator == null || string.IsNullOrEmpty(paramName)) return false;
        foreach (var param in animator.parameters)
        {
            if (param.name == paramName) return true;
        }
        return false;
    }

    private void OnHealthNetChanged(float oldVal, float newVal)
    {
        localHealth = newVal;
        float diff = oldVal - newVal;
        if (diff > 0)
        {
            EnemyDamageEffectHelper.PlayDamageEffects(gameObject, diff);
        }
    }

    private AudioSource EnsureAudioSource()
    {
        if (bossAudioSource == null)
        {
            bossAudioSource = GetComponent<AudioSource>();
            if (bossAudioSource == null)
            {
                bossAudioSource = gameObject.AddComponent<AudioSource>();
            }
            bossAudioSource.playOnAwake = false;
            bossAudioSource.spatialBlend = 0f; // 2D Sound so all 4 players hear clearly across the scene
            bossAudioSource.volume = bossAudioVolume;
            bossAudioSource.loop = loopBossAudio;
        }
        return bossAudioSource;
    }

    public void PlayBossAudioNet(int audioType)
    {
        if (!isStandaloneMode && IsNetworkActive && IsServer)
        {
            PlayBossAudioClientRpc(audioType);
        }
        else
        {
            ExecutePlayBossAudio(audioType);
        }
    }

    [ClientRpc]
    private void PlayBossAudioClientRpc(int audioType)
    {
        ExecutePlayBossAudio(audioType);
    }

    private void ExecutePlayBossAudio(int audioType)
    {
        AudioSource audio = EnsureAudioSource();
        if (audio == null) return;

        if (audioType == 0)
        {
            if (audio.isPlaying) audio.Stop();
            if (!isClone && AudioManager.Instance != null)
            {
                AudioManager.Instance.SetBossMusicActive(false, 1.5f);
            }
            return;
        }

        AudioClip clipToPlay = (audioType == 2) ? summonSkillAudioClip : preSummonAudioClip;
        if (clipToPlay == null) return;

        // Tắt nhạc nền game khi MiniBoss phát nhạc chiến đấu
        if (!isClone && AudioManager.Instance != null)
        {
            AudioManager.Instance.SetBossMusicActive(true, 0.8f);
        }

        if (audio.clip == clipToPlay && audio.isPlaying) return;

        audio.clip = clipToPlay;
        audio.volume = bossAudioVolume;
        audio.loop = loopBossAudio;
        audio.Play();
    }

    public void ActivateBoss()
    {
        bool auth = isStandaloneMode || (IsNetworkActive && IsServer) || isClone || !IsSpawned;
        if (!auth) return;

        if (isStandaloneMode || !IsSpawned)
        {
            localIsBossActive = true;
            localHealth = maxHealth > 0 ? maxHealth : phase1MaxHealth * 0.45f;
        }
        
        if (!isStandaloneMode && IsServer && IsSpawned)
        {
            isBossActive.Value = true;
        }

        // Kích hoạt phát âm thanh lúc chiến đấu bình thường (lặp lại & đồng bộ cho 4 Player)
        if (!isClone)
        {
            PlayBossAudioNet(1);
        }

        // BẢO ĐẢM 100%: Lập tức quét tìm Player gần nhất và chuyển sang trạng thái Chase rượt đuổi ngay lập tức!
        DetectAndSwitchTarget();
        if (targetPlayer == null)
        {
            targetPlayer = FindNearestReachablePlayer();
        }
        if (targetPlayer != null)
        {
            ChangeState(MiniBossState.Chase);
        }

        // Tự động khôi phục/gắn Thanh Máu hiển thị trên đầu nếu đây là Phân Thân
        if (isClone)
        {
            EnsureCloneOverheadHealthBar();
        }

        Debug.Log($"[MiniBossAI] {(isClone ? "Phân Thân Mini Boss" : "Mini Boss")} đã KÍCH HOẠT! Nhắm mục tiêu: {(targetPlayer != null ? targetPlayer.name : "None")} - State: {CurrentStateValue}");
    }

    public void EnsureCloneOverheadHealthBar()
    {
        // Theo yêu cầu người dùng: Không tạo bất kỳ UI World Space nào trên đầu phân thân.
        // Chỉ hiển thị và trừ máu trên Thanh UI HUD chính (MiniBossHealthBar) ở góc trên màn hình.
        var existingCanvas = GetComponentsInChildren<Canvas>(true);
        foreach (var c in existingCanvas)
        {
            if (c != null && (c.name.Contains("CloneHealthBar") || c.name.Contains("HealthBar")))
            {
                DestroyImmediate(c.gameObject);
            }
        }
        var fallbacks = GetComponentsInChildren<CloneWorldHealthBarFallback>(true);
        foreach (var f in fallbacks)
        {
            if (f != null) DestroyImmediate(f.gameObject);
        }
    }

    public void ApplyStun(float duration)
    {
        if (IsDead) return;
        EnemyStunVfxBehaviour.ApplyStunVfx(gameObject, duration, null, 2.8f, 2.2f);
        if (!isStandaloneMode && IsSpawned && IsServer)
        {
            ApplyStunVfxClientRpc(duration);
        }
    }

    [ClientRpc]
    private void ApplyStunVfxClientRpc(float duration)
    {
        if (IsDead) return;
        EnemyStunVfxBehaviour.ApplyStunVfx(gameObject, duration, null, 2.8f, 2.2f);
    }

    public MiniBossAI FindMainBoss()
    {
        var all = FindObjectsByType<MiniBossAI>(FindObjectsSortMode.None);
        foreach (var b in all)
        {
            if (b != null && !b.isClone && b.gameObject.activeInHierarchy)
            {
                return b;
            }
        }
        return null;
    }

    public void DamageCloneNet(int cloneIndex, float damage)
    {
        if (!IsServer && !isStandaloneMode) return;

        if (cloneIndex == 1)
        {
            clone1HealthNet.Value = Mathf.Max(0f, clone1HealthNet.Value - damage);
            float newHp = clone1HealthNet.Value;
            DamageCloneClientRpc(1, damage, newHp);

            if (newHp <= 0f && !clone1DeadNet.Value)
            {
                clone1DeadNet.Value = true;
                ForceCloneDeathClientRpc(1);
                CheckAndTriggerAllMiniBossesDead();
            }
        }
        else
        {
            clone2HealthNet.Value = Mathf.Max(0f, clone2HealthNet.Value - damage);
            float newHp = clone2HealthNet.Value;
            DamageCloneClientRpc(2, damage, newHp);

            if (newHp <= 0f && !clone2DeadNet.Value)
            {
                clone2DeadNet.Value = true;
                ForceCloneDeathClientRpc(2);
                CheckAndTriggerAllMiniBossesDead();
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void DamageCloneServerRpc(int cloneIndex, float damage)
    {
        DamageCloneNet(cloneIndex, damage);
    }

    [ClientRpc]
    private void DamageCloneClientRpc(int cloneIndex, float damage, float newHp)
    {
        var all = FindObjectsByType<MiniBossAI>(FindObjectsSortMode.None);
        foreach (var b in all)
        {
            if (b != null && b.isClone && b.gameObject.activeInHierarchy)
            {
                bool isClone2 = b.gameObject.name.Contains("2") || b.gameObject.name.Contains("Right");
                if ((cloneIndex == 2 && isClone2) || (cloneIndex == 1 && !isClone2))
                {
                    b.localHealth = newHp;
                    EnemyDamageEffectHelper.PlayDamageEffects(b.gameObject, damage);
                    if (newHp <= 0f && !b.IsDead)
                    {
                        b.ChangeState(MiniBossState.Dead);
                        Destroy(b.gameObject, 1.5f);
                    }
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Server broadcast cho tất cả Client: Force clone vào trạng thái Dead + Destroy.
    /// Đảm bảo trên MỌI máy, phân thân đều biến mất cùng lúc.
    /// </summary>
    [ClientRpc]
    private void ForceCloneDeathClientRpc(int cloneIndex)
    {
        var all = FindObjectsByType<MiniBossAI>(FindObjectsSortMode.None);
        foreach (var b in all)
        {
            if (b != null && b.isClone && b.gameObject.activeInHierarchy)
            {
                bool isClone2 = b.gameObject.name.Contains("2") || b.gameObject.name.Contains("Right");
                if ((cloneIndex == 2 && isClone2) || (cloneIndex == 1 && !isClone2))
                {
                    b.localHealth = 0f;
                    if (!b.IsDead)
                    {
                        b.ChangeState(MiniBossState.Dead);
                    }
                    Destroy(b.gameObject, 1f);
                    break;
                }
            }
        }
    }

    [ClientRpc]
    private void TriggerDefeatCutsceneAndCleanupClientRpc()
    {
        Debug.Log("[MiniBossAI ClientRpc] Nhận tín hiệu tiêu diệt toàn bộ MiniBoss -> Hủy hoàn toàn phân thân & chuẩn bị chạy Cutscene!");
        ForceDestroyAllRemainingClones();
        TriggerBossDefeatCutscene();
    }

    [ServerRpc(RequireOwnership = false)]
    public void TakeDamageServerRpc(float damageAmount)
    {
        TakeDamage(damageAmount);
    }

    public void TakeDamage(float damage)
    {
        if (isClone)
        {
            isSummonInvulnerable = false;
            if (localState == MiniBossState.Enrage) localState = MiniBossState.Chase;

            int idx = (gameObject.name.Contains("2") || gameObject.name.Contains("Right")) ? 2 : 1;

            if (IsNetworkActive)
            {
                var mainBoss = FindMainBoss();
                if (mainBoss != null && mainBoss.IsSpawned)
                {
                    if (IsServer)
                    {
                        mainBoss.DamageCloneNet(idx, damage);
                    }
                    else
                    {
                        mainBoss.DamageCloneServerRpc(idx, damage);
                    }
                    return;
                }
            }

            // Fallback Offline / Standalone
            localHealth = Mathf.Max(0f, localHealth - damage);
            EnemyDamageEffectHelper.PlayDamageEffects(gameObject, damage);
            if (localHealth <= 0f)
            {
                ChangeState(MiniBossState.Dead);
                Destroy(gameObject, 2f);
            }
            return;
        }

        if (IsDead) return;

        // Chỉ kiểm tra gồng/cuồng nộ bất tử cho Boss chính, KHÔNG áp dụng cho Phân Thân!
        if (!isClone && (CurrentStateValue == MiniBossState.Enrage || isSummonInvulnerable)) return;

        // Nếu là Client đánh Boss trên mạng -> Gửi ServerRpc để Server trừ máu chuẩn xác 100%
        if (!isStandaloneMode && !isClone && IsSpawned && !IsServer)
        {
            TakeDamageServerRpc(damage);
            return;
        }

        localHealth = Mathf.Max(0f, localHealth - damage);
        if (!isStandaloneMode && !isClone && IsSpawned && IsServer)
        {
            currentHealth.Value = Mathf.Max(0f, currentHealth.Value - damage);
            localHealth = currentHealth.Value;
        }

        EnemyDamageEffectHelper.PlayDamageEffects(gameObject, damage);

        bool isAuth = isStandaloneMode || isClone || (IsSpawned && IsServer) || !IsSpawned;
        if (!isAuth) return;

        float activeHp = ActualCurrentHealth;
        if (activeHp <= 0f)
        {
            ChangeState(MiniBossState.Dead);
            return;
        }

        // Tự động kích hoạt khi nhận sát thương nếu chưa active
        if (!IsBossActive) ActivateBoss();

        // KÍCH HOẠT PHASE 2 & TRIỆU HỒI PHÂN THÂN KHI MÁU XUỐNG DƯỚI 50% HP (<= 350 HP)
        if (!isClone && !IsPhase2 && (activeHp / maxHealth) <= 0.50f)
        {
            TriggerPhase2Transition();
            if (!hasSummonedClones)
            {
                hasSummonedClones = true;
                SummonClones();
            }
        }

        // KIỂM TRA TỐC BIẾN NÉ ĐÒN KHI BỊ DỒN SÁT THƯƠNG
        CheckShadowBlinkDodge(damage);

        // HYPER ARMOR FIX: Khi đang tấn công (Attack State), Mưa Kiếm (SwordRain), Enrage hoặc Dead -> Không bị hủy đòn chém
        if (CurrentStateValue == MiniBossState.Attack || CurrentStateValue == MiniBossState.SwordRain || CurrentStateValue == MiniBossState.Enrage || CurrentStateValue == MiniBossState.Dead)
        {
            return;
        }

        // COOLDOWN HIT STAGGER FIX: Chỉ giật đòn khi sát thương lớn (>= 30 HP) và đã qua thời gian hồi giật đòn (1.6s)
        if (damage >= 30f && hitStaggerCooldownTimer <= 0f)
        {
            hitStaggerCooldownTimer = 1.6f;
            if (!isStandaloneMode && IsSpawned && IsServer)
            {
                hitCounter.Value++;
            }
            else if (anim != null)
            {
                anim.SetTrigger(hitTrigger);
            }
            ChangeState(MiniBossState.Hit);
        }
    }

    private void SummonClones()
    {
        Debug.Log("[MiniBossAI] Máu xuống 50% HP! Bắt đầu chuỗi đồng bộ camera zoom triệu hồi 2 phân thân cho cả 4 Player!");

        // Phát âm thanh triệu hồi phân thân (lặp lại & đồng bộ cho 4 Player)
        PlayBossAudioNet(2);

        isSummonInvulnerable = true;
        if (AgentReady)
        {
            agent.isStopped = true;
        }
        SetSpeedNet(0f);

        Vector3 targetPosLeft = transform.position - transform.right * 5.5f;
        Vector3 targetPosRight = transform.position + transform.right * 5.5f;

        if (NavMesh.SamplePosition(targetPosLeft, out NavMeshHit hitL, 5.0f, NavMesh.AllAreas)) targetPosLeft = hitL.position;
        if (NavMesh.SamplePosition(targetPosRight, out NavMeshHit hitR, 5.0f, NavMesh.AllAreas)) targetPosRight = hitR.position;

        if (!isStandaloneMode && IsServer)
        {
            float cHp = phase1MaxHealth * 0.45f;
            clone1HealthNet.Value = cHp;
            clone2HealthNet.Value = cHp;
            clone1DeadNet.Value = false;
            clone2DeadNet.Value = false;
            summonCloneCounter.Value++;
            TriggerSummonCutsceneClientRpc(targetPosLeft, targetPosRight);
        }
        else if (isStandaloneMode)
        {
            StartCoroutine(SummonCloneCameraCutsceneClientRoutine(targetPosLeft, targetPosRight));
        }

        StartCoroutine(SummonCloneSequenceServerRoutine(targetPosLeft, targetPosRight));
    }

    [ClientRpc]
    private void TriggerSummonCutsceneClientRpc(Vector3 posLeft, Vector3 posRight)
    {
        StartCoroutine(SummonCloneCameraCutsceneClientRoutine(posLeft, posRight));
        if (!IsServer)
        {
            StartCoroutine(ExecuteRisingClonesSequence(posLeft, posRight));
        }
    }

    private IEnumerator SummonCloneSequenceServerRoutine(Vector3 targetPosLeft, Vector3 targetPosRight)
    {
        PlaySummonCloneVisuals();
        if (anim != null && !string.IsNullOrEmpty(enrageTrigger))
        {
            anim.SetTrigger(enrageTrigger);
        }

        // Chờ 1.2s cho camera zoom in cận cảnh hoàn tất
        yield return new WaitForSeconds(1.2f);

        // Server thực hiện chuỗi sinh 2 hố tử thần và cho 2 phân thân nhô lên từ dưới đất
        bool auth = isStandaloneMode || (IsNetworkActive && IsServer);
        if (auth)
        {
            yield return StartCoroutine(ExecuteRisingClonesSequence(targetPosLeft, targetPosRight));
        }
        else
        {
            yield return new WaitForSeconds(3.5f);
        }

        // Chờ thêm 1.2s cho camera zoom out quay về player
        yield return new WaitForSeconds(1.2f);

        isSummonInvulnerable = false;

        // Chuyển âm thanh quay về nhạc nền chiến đấu bình thường
        PlayBossAudioNet(1);

        if (targetPlayer != null) ChangeState(MiniBossState.Chase);
        else ChangeState(MiniBossState.Idle);
    }

    private IEnumerator ExecuteRisingClonesSequence(Vector3 targetPosLeft, Vector3 targetPosRight)
    {
        Vector3 bossScale = transform.localScale;
        if (bossScale.sqrMagnitude < 0.1f) bossScale = new Vector3(3f, 3f, 3f);

        yield return new WaitForSeconds(0.4f);

        // 2. Sinh 2 phân thân từ chính bản thể MiniBoss khổng lồ (độ sâu -1.2m)
        Vector3 leftStartPos = targetPosLeft - Vector3.up * 1.2f;
        Vector3 rightStartPos = targetPosRight - Vector3.up * 1.2f;

        GameObject prefabToSpawn = (clonePrefab != null) ? clonePrefab : gameObject;

        GameObject cloneLeft = Instantiate(prefabToSpawn, leftStartPos, transform.rotation);
        GameObject cloneRight = Instantiate(prefabToSpawn, rightStartPos, transform.rotation);

        cloneLeft.name = "MiniBoss_Clone1";
        cloneRight.name = "MiniBoss_Clone2";

        // Hàm dọn dẹp các component Netcode tránh gây lỗi ngoại lệ khi chạy Standalone/Client
        System.Action<GameObject> cleanNetComponents = (clone) =>
        {
            var no = clone.GetComponent<NetworkObject>();
            if (no != null) DestroyImmediate(no);
            var nt = clone.GetComponent<Unity.Netcode.Components.NetworkTransform>();
            if (nt != null) DestroyImmediate(nt);
            var na = clone.GetComponent<Unity.Netcode.Components.NetworkAnimator>();
            if (na != null) DestroyImmediate(na);
            var db = clone.GetComponent<Enemy1_DapBua>();
            if (db != null) DestroyImmediate(db);
        };
        cleanNetComponents(cloneLeft);
        cleanNetComponents(cloneRight);

        // Hàm dọn sạch mọi child test capsule, VFX kế thừa hoặc aura rác từ bản thể gốc
        System.Action<GameObject> cleanCloneStrayObjects = (clone) =>
        {
            var allChildren = clone.GetComponentsInChildren<Transform>(true);
            foreach (var t in allChildren)
            {
                if (t == null || t == clone.transform) continue;
                string tName = t.name.ToLower();
                // Xóa bỏ các mesh/object Capsule thử nghiệm trong VFX pack (nếu không phải SkinnedMeshRenderer của Boss)
                if (tName.Contains("capsule") && t.GetComponent<CapsuleCollider>() == null && t.GetComponent<SkinnedMeshRenderer>() == null)
                {
                    DestroyImmediate(t.gameObject);
                }
                else if (tName.Contains("aura") || tName.Contains("fireaura") || tName.Contains("enrage") || tName.Contains("testingchar") || tName.Contains("vfx_"))
                {
                    if (t.GetComponent<SkinnedMeshRenderer>() == null)
                    {
                        DestroyImmediate(t.gameObject);
                    }
                }
            }
        };
        cleanCloneStrayObjects(cloneLeft);
        cleanCloneStrayObjects(cloneRight);

        cloneLeft.SetActive(true);
        cloneRight.SetActive(true);

        cloneLeft.tag = "Enemy";
        cloneRight.tag = "Enemy";
        int enemyLayerIndex = LayerMask.NameToLayer("Enemy");
        if (enemyLayerIndex != -1)
        {
            cloneLeft.layer = enemyLayerIndex;
            cloneRight.layer = enemyLayerIndex;
            foreach (var t in cloneLeft.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.tag = "Enemy";
                t.gameObject.layer = enemyLayerIndex;
            }
            foreach (var t in cloneRight.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.tag = "Enemy";
                t.gameObject.layer = enemyLayerIndex;
            }
        }

        cloneLeft.transform.position = leftStartPos;
        cloneRight.transform.position = rightStartPos;
        cloneLeft.transform.localScale = bossScale;
        cloneRight.transform.localScale = bossScale;

        MiniBossAI leftAI = cloneLeft.GetComponent<MiniBossAI>();
        MiniBossAI rightAI = cloneRight.GetComponent<MiniBossAI>();

        System.Action<MiniBossAI> initCloneAI = (cAI) =>
        {
            if (cAI == null) return;
            cAI.isStandaloneMode = true;
            cAI.isClone = true;
            cAI.hasSummonedClones = true;
            cAI.isSummonInvulnerable = false;
            cAI.localIsBossActive = true;
            cAI.phase1MaxHealth = phase1MaxHealth * 0.45f;
            cAI.maxHealth = cAI.phase1MaxHealth;
            cAI.localHealth = cAI.maxHealth;
            if (cAI.agent != null) cAI.agent.enabled = false;
            cAI.StoreInitialVisualPos();
            if (cAI.visualRoot != null) cAI.visualRoot.localPosition = cAI.initialVisualLocalPos;
            if (cAI.anim != null)
            {
                cAI.anim.Rebind();
                cAI.anim.Update(0f);
            }
        };
        initCloneAI(leftAI);
        initCloneAI(rightAI);

        // BẢO ĐẢM HIỂN THỊ: Bật toàn bộ SkinnedMeshRenderer của phân thân và tắt các Capsule mesh thử nghiệm
        System.Action<GameObject> enableBossVisuals = (clone) =>
        {
            foreach (var smr in clone.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr != null) { smr.enabled = true; smr.gameObject.SetActive(true); }
            }
            foreach (var mr in clone.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr != null)
                {
                    if (mr.gameObject.name.ToLower().Contains("capsule"))
                    {
                        mr.enabled = false;
                        mr.gameObject.SetActive(false);
                    }
                    else
                    {
                        mr.enabled = true;
                        mr.gameObject.SetActive(true);
                    }
                }
            }
        };
        enableBossVisuals(cloneLeft);
        enableBossVisuals(cloneRight);

        // Tạm thời tắt Collider của phân thân trong lúc đang nhô lên
        var leftColliders = cloneLeft.GetComponentsInChildren<Collider>();
        foreach (var c in leftColliders) if (c != null) c.enabled = false;

        var rightColliders = cloneRight.GetComponentsInChildren<Collider>();
        foreach (var c in rightColliders) if (c != null) c.enabled = false;

        // 3. TỪ TỪ CHO 2 CON NHÂN BẢN NHÔ LÊN TỪ DƯỚI ĐẤT (2.5 GIÂY)
        float riseDuration = 2.5f;
        float riseElapsed = 0f;
        while (riseElapsed < riseDuration)
        {
            riseElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(riseElapsed / riseDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            if (cloneLeft != null) cloneLeft.transform.position = Vector3.Lerp(leftStartPos, targetPosLeft, smoothT);
            if (cloneRight != null) cloneRight.transform.position = Vector3.Lerp(rightStartPos, targetPosRight, smoothT);

            yield return null;
        }

        if (cloneLeft != null) cloneLeft.transform.position = targetPosLeft;
        if (cloneRight != null) cloneRight.transform.position = targetPosRight;

        // Bật lại Collider và kích hoạt AI cho 2 phân thân khi đã nhô lên mặt đất
        foreach (var c in leftColliders) if (c != null) c.enabled = true;
        foreach (var c in rightColliders) if (c != null) c.enabled = true;

        ConfigureClone(leftAI, targetPosLeft);
        ConfigureClone(rightAI, targetPosRight);

        clone1Instance = leftAI;
        clone2Instance = rightAI;

        // Đăng ký trực tiếp 2 phân thân vào thanh máu UI HUD chính của MiniBoss
        var hudBar = FindFirstObjectByType<MiniBossHealthBar>();
        if (hudBar != null)
        {
            hudBar.RegisterClones(leftAI, rightAI);
        }

        // Đợi thêm 0.6s để người chơi ngắm cả 3 con Boss đứng oai phong trên mặt đất trước khi camera zoom out
        yield return new WaitForSeconds(0.6f);
    }

    private GameObject PlayCloneSpawnVFX(Vector3 spawnPos)
    {
        Vector3 fxPos = spawnPos;
        GameObject vfx = null;
        if (summonHoleVFXPrefab != null)
        {
            vfx = Instantiate(summonHoleVFXPrefab, fxPos, Quaternion.identity);
            Destroy(vfx, 6.0f);
        }
        else if (enrageVFXPrefab != null)
        {
            vfx = Instantiate(enrageVFXPrefab, fxPos + Vector3.up * 0.2f, Quaternion.identity);
            Destroy(vfx, 5.0f);
        }
        else if (shadowBlinkVFX != null)
        {
            vfx = Instantiate(shadowBlinkVFX, fxPos + Vector3.up * 0.2f, Quaternion.identity);
            Destroy(vfx, 4.0f);
        }

        if (vfx != null)
        {
            // Tắt bỏ toàn bộ mesh thử nghiệm "Capsule" hoặc dummy testing character trong VFX prefab
            foreach (var t in vfx.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && t.name.ToLower().Contains("capsule") && t.GetComponent<ParticleSystem>() == null)
                {
                    t.gameObject.SetActive(false);
                }
            }

            // Scale vừa vặn gọn gàng cho vùng triệu hồi hố tử thần
            float s = summonHoleVFXScale > 0.01f ? summonHoleVFXScale : 0.5f;
            vfx.transform.localScale = new Vector3(s, s, s);

            // Bật Looping cho tất cả ParticleSystem con để VFX không bị tắt nhanh trong quá trình triệu hồi (6.0s)
            var particleSystems = vfx.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in particleSystems)
            {
                if (ps != null)
                {
                    var main = ps.main;
                    main.loop = true;
                    if (!ps.isPlaying) ps.Play();
                }
            }
        }

        if (shadowBlinkSFX != null)
        {
            AudioSource.PlayClipAtPoint(shadowBlinkSFX, fxPos + Vector3.up * 1.0f, 1.0f);
        }
        return vfx;
    }

    private IEnumerator SummonCloneCameraCutsceneClientRoutine(Vector3 posLeft, Vector3 posRight)
    {
        Camera mainCam = Camera.main;
        if (mainCam == null) mainCam = FindFirstObjectByType<Camera>();
        if (mainCam == null) yield break;

        MonoBehaviour localPlayer = GetLocalPlayerScript();
        if (localPlayer != null)
        {
            SetPlayerField(localPlayer, "enableCameraFollow", false);
        }

        Vector3 startCamPos = mainCam.transform.position;
        Quaternion startCamRot = mainCam.transform.rotation;

        // Tính toán tâm điểm bao quát giữa MiniBoss chính và 2 vị trí hố triệu hồi 2 bên
        Vector3 bossFocusCenter = (transform.position + posLeft + posRight) / 3.0f + Vector3.up * 2.0f;

        Vector3 dirFromBossToCam = (startCamPos - bossFocusCenter).normalized;
        if (dirFromBossToCam.sqrMagnitude < 0.01f) dirFromBossToCam = -transform.forward;
        dirFromBossToCam.y = 0.25f;
        dirFromBossToCam = dirFromBossToCam.normalized;

        // Camera lùi xa (11.5m) và nâng cao (+3.5m) để thấy rộng bao quát toàn bộ 3 con Boss từ trên xuống
        float cutsceneDistance = 11.5f;
        Vector3 targetCamPos = bossFocusCenter + dirFromBossToCam * cutsceneDistance + Vector3.up * 3.5f;
        Quaternion targetCamRot = Quaternion.LookRotation(bossFocusCenter - targetCamPos);

        Vector3 playerCamOffset = Vector3.zero;
        if (localPlayer != null)
        {
            playerCamOffset = startCamPos - localPlayer.transform.position;
        }

        // 1. ZOOM IN TUTU LẠI GẦN MINI BOSS (1.2 GIÂY)
        float zoomInDuration = 1.2f;
        float elapsed = 0f;
        while (elapsed < zoomInDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / zoomInDuration);
            mainCam.transform.position = Vector3.Lerp(startCamPos, targetCamPos, t);
            mainCam.transform.rotation = Quaternion.Slerp(startCamRot, targetCamRot, t);
            yield return null;
        }
        mainCam.transform.position = targetCamPos;
        mainCam.transform.rotation = targetCamRot;

        // 2. SINH 2 HỐ TỬ THẦN TẠI VỊ TRÍ ĐỒNG BỘ CHO TẤT CẢ CLIENT
        PlayCloneSpawnVFX(posLeft);
        PlayCloneSpawnVFX(posRight);

        // 3. DỪNG XEM CẢNH HỐ TỬ THẦN & PHÂN THÂN NHÔ LÊN (3.5 GIÂY)
        float holdDuration = 3.5f;
        float holdElapsed = 0f;
        CameraShakeHelper.Shake(3.5f, 1.1f); // Rung dữ dội toàn màn hình khi 2 hố tử thần trồi lên
        while (holdElapsed < holdDuration)
        {
            holdElapsed += Time.deltaTime;
            mainCam.transform.position = targetCamPos;
            mainCam.transform.rotation = targetCamRot;
            yield return null;
        }

        // 4. ZOOM OUT TUTU QUAY VỀ VỊ TRÍ PLAYER (1.2 GIÂY)
        float zoomOutDuration = 1.2f;
        elapsed = 0f;
        while (elapsed < zoomOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / zoomOutDuration);

            Vector3 returnCamPos = startCamPos;
            Quaternion returnCamRot = startCamRot;

            if (localPlayer != null)
            {
                returnCamPos = localPlayer.transform.position + playerCamOffset;
            }

            mainCam.transform.position = Vector3.Lerp(targetCamPos, returnCamPos, t);
            mainCam.transform.rotation = Quaternion.Slerp(targetCamRot, returnCamRot, t);
            yield return null;
        }

        // KHÔI PHỤC THEO DÕI CAMERA CHO LOCAL PLAYER
        if (localPlayer != null)
        {
            SetPlayerField(localPlayer, "enableCameraFollow", true);
        }
    }

    private MonoBehaviour GetLocalPlayerScript()
    {
        var scripts = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        foreach (var script in scripts)
        {
            if (script == null) continue;
            System.Type t = script.GetType();
            string n = t.Name;
            if (n == "LeoPlayer" || n == "ArthurPlayer" || n == "ElenaPlayer" || n == "MayaPlayer" || n == "SimplePlayerTest")
            {
                var isOwnerProp = t.GetProperty("IsOwner", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var isSpawnedProp = t.GetProperty("IsSpawned", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var isStandaloneField = t.GetField("isStandaloneMode", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                bool isStandalone = isStandaloneField != null && (bool)isStandaloneField.GetValue(script);
                bool isOwner = isOwnerProp != null && (bool)isOwnerProp.GetValue(script, null);
                bool isSpawned = isSpawnedProp != null && (bool)isSpawnedProp.GetValue(script, null);

                if (isStandalone || (isSpawned && isOwner))
                {
                    return script;
                }
            }
        }
        return null;
    }

    private void SetPlayerField(MonoBehaviour playerScript, string fieldName, object value)
    {
        if (playerScript == null) return;
        System.Type type = playerScript.GetType();
        var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(playerScript, value);
        }
    }

    private void ConfigureClone(MiniBossAI cloneAI, Vector3 spawnPos)
    {
        if (cloneAI == null) return;
        cloneAI.isStandaloneMode = true;
        cloneAI.isClone = true;
        cloneAI.hasSummonedClones = true;
        cloneAI.isSummonInvulnerable = false;
        cloneAI.phase1MaxHealth = phase1MaxHealth * 0.45f;
        cloneAI.maxHealth = cloneAI.phase1MaxHealth;
        cloneAI.localHealth = cloneAI.maxHealth;
        cloneAI.localIsBossActive = true;
        cloneAI.localState = MiniBossState.Chase;

        var db = cloneAI.GetComponent<Enemy1_DapBua>();
        if (db != null) DestroyImmediate(db);

        // Tự động xóa sạch hiệu ứng lửa gồng/aura kế thừa nếu có để phân thân trở về trạng thái chiến đấu sạch sẽ
        var oldAuras = cloneAI.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in oldAuras)
        {
            if (ps != null && (ps.gameObject.name.Contains("FireAura") || ps.gameObject.name.Contains("Enrage") || ps.gameObject.name.Contains("Aura") || ps.gameObject.name.Contains("Capsule")))
            {
                DestroyImmediate(ps.gameObject);
            }
        }

        if (!isStandaloneMode && IsServer && cloneAI.IsSpawned)
        {
            cloneAI.isBossActive.Value = true;
            cloneAI.currentHealth.Value = cloneAI.maxHealth;
        }

        // Bật NavMeshAgent cho phân thân và tìm mục tiêu Player để nhắm đánh ngay
        if (cloneAI.agent != null)
        {
            cloneAI.agent.enabled = true;
            cloneAI.agent.isStopped = false;
            cloneAI.agent.speed = runSpeed > 1f ? runSpeed : 5.8f;
            cloneAI.agent.stoppingDistance = 2.4f;
            cloneAI.agent.Warp(spawnPos);
        }

        cloneAI.ActivateBoss();
        cloneAI.DetectAndSwitchTarget();
        if (cloneAI.targetPlayer == null)
        {
            cloneAI.targetPlayer = cloneAI.FindNearestReachablePlayer();
        }
        cloneAI.ChangeState(MiniBossState.Chase);
    }

    private void FinishCloneSetup(GameObject cloneObj, Vector3 finalPos)
    {
        if (cloneObj == null) return;

        MiniBossAI cloneAI = cloneObj.GetComponent<MiniBossAI>();
        if (cloneAI != null)
        {
            if (cloneAI.agent != null)
            {
                cloneAI.agent.enabled = true;
                cloneAI.agent.Warp(finalPos);
            }
            else
            {
                cloneObj.transform.position = finalPos;
            }
            cloneObj.transform.localScale = transform.localScale;
            cloneAI.ActivateBoss();
        }
        else
        {
            cloneObj.transform.position = finalPos;
        }
    }

    private void PlaySummonCloneVisuals()
    {
        if (enrageVFXPrefab != null)
        {
            // Đi theo đúng giữa tâm ngực/thân MiniBoss (Y=0.20f x scale 3.0 = 0.6m ngực) và thu nhỏ scale vừa vặn (0.35x)
            GameObject vfx = Instantiate(enrageVFXPrefab, transform.position + Vector3.up * 0.6f, transform.rotation);
            var cap = vfx.transform.Find("Capsule");
            if (cap != null) cap.gameObject.SetActive(false);
            vfx.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
            Destroy(vfx, 3.5f);
        }
        if (enrageSFXSound != null)
        {
            AudioSource.PlayClipAtPoint(enrageSFXSound, transform.position + Vector3.up * 1.35f, 1.0f);
        }
    }

    private void ResetEarthSpikesTimer()
    {
        earthSpikesTimer = Random.Range(earthSpikesIntervalMin, earthSpikesIntervalMax);
    }

    private void ResetSwordRainTimer()
    {
        swordRainTimer = Random.Range(swordRainIntervalMin, swordRainIntervalMax);
    }

    private void TryLaunchEarthSpikesSkill()
    {
        bool auth = isStandaloneMode || (IsNetworkActive && IsServer);
        if (!auth) return;

        Transform target = targetPlayer;
        if (target == null) target = FindNearestReachablePlayer();
        if (target == null) return;

        int spikeCount = Random.Range(4, 6); // 4 đến 5 bãi đá
        List<Vector3> spikePositions = new List<Vector3>();

        // Bãi 1: Ngay vị trí Player mục tiêu
        Vector3 playerPos = target.position;
        if (NavMesh.SamplePosition(playerPos, out NavMeshHit hitCenter, 4.0f, NavMesh.AllAreas))
        {
            spikePositions.Add(hitCenter.position);
        }
        else
        {
            spikePositions.Add(playerPos);
        }

        // Các bãi còn lại: Phân bố ngẫu nhiên xung quanh khu vực giao tranh (bán kính 4m - 14m)
        for (int i = 1; i < spikeCount; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle.normalized * Random.Range(4.5f, 13.5f);
            Vector3 randomCandidate = target.position + new Vector3(randomCircle.x, 0f, randomCircle.y);

            if (NavMesh.SamplePosition(randomCandidate, out NavMeshHit hitRnd, 5.0f, NavMesh.AllAreas))
            {
                spikePositions.Add(hitRnd.position);
            }
            else
            {
                spikePositions.Add(randomCandidate);
            }
        }

        Vector3[] finalPositions = spikePositions.ToArray();

        if (!isStandaloneMode && IsServer)
        {
            TriggerEarthSpikesClientRpc(finalPositions);
        }
        else if (isStandaloneMode)
        {
            ExecuteEarthSpikesSkill(finalPositions);
        }
    }

    [ClientRpc]
    private void TriggerEarthSpikesClientRpc(Vector3[] spawnPositions)
    {
        ExecuteEarthSpikesSkill(spawnPositions);
    }

    private void ExecuteEarthSpikesSkill(Vector3[] spawnPositions)
    {
        if (spawnPositions == null || spawnPositions.Length == 0) return;

        // 1. Animation phát đòn cho MiniBoss
        if (anim != null)
        {
            if (attackTriggers != null && attackTriggers.Length > 0 && !string.IsNullOrEmpty(attackTriggers[0]))
            {
                anim.SetTrigger(attackTriggers[0]);
            }
            else if (!string.IsNullOrEmpty(enrageTrigger))
            {
                anim.SetTrigger(enrageTrigger);
            }
        }

        // 2. Prefab Gai Đất nhô lên (VFX_Earth_Area_01)
        GameObject vfxPrefabToSpawn = earthSpikesVFXPrefab;
        if (vfxPrefabToSpawn == null)
        {
#if UNITY_EDITOR
            vfxPrefabToSpawn = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Vefects/Stylized AoE VFX/VFX/Earth/Particles/VFX_Earth_Area_01.prefab");
#endif
            if (vfxPrefabToSpawn == null) vfxPrefabToSpawn = summonHoleVFXPrefab;
        }

        if (vfxPrefabToSpawn != null)
        {
            for (int i = 0; i < spawnPositions.Length; i++)
            {
                Vector3 pos = spawnPositions[i];
                GameObject spikesObj = Instantiate(vfxPrefabToSpawn, pos, Quaternion.identity);

                // BẢO TỒN SCALE CỦA PREFAB (Ví dụ: Scale 2,2,2 bạn đã chỉnh trong Inspector)
                spikesObj.transform.localScale = vfxPrefabToSpawn.transform.localScale;

                EarthSpikesDamageZone dmgZone = spikesObj.GetComponent<EarthSpikesDamageZone>();
                if (dmgZone == null) dmgZone = spikesObj.AddComponent<EarthSpikesDamageZone>();
                dmgZone.damageAmount = earthSpikesDamage;
                dmgZone.baseDamageRadius = 3.2f;
                dmgZone.baseMaxSpikeHeight = 2.5f;
                dmgZone.spikeEmergenceDelay = earthSpikesEmergenceDelay;
                dmgZone.spikeActiveDuration = 1.6f;
                dmgZone.vfxLifespan = earthSpikesEmergenceDelay + 2.3f;
            }

            Debug.Log($"[MiniBossAI] Triệu hồi đồng loạt {spawnPositions.Length} bãi Gai Đất (VFX_Earth_Area_01) ở các vị trí ngẫu nhiên!");
        }
    }

    private void TryLaunchSwordRainSkill()
    {
        bool auth = isStandaloneMode || (IsNetworkActive && IsServer);
        if (!auth) return;

        var players = GetAllActivePlayers();
        List<Vector3> clusterSpots = new List<Vector3>();

        // 1. Quét tìm tất cả Player: Mỗi Player sẽ bị nhắm các bãi dội chùm kiếm (ngay dưới chân và quanh 2-3m)
        foreach (var p in players)
        {
            if (p != null && !IsPlayerDeadOrInvisible(p))
            {
                // Bãi 1: Ngay tâm dưới chân Player
                clusterSpots.Add(p.position);

                // Bãi 2: Bán kính 1.8m - 3.2m quanh Player
                Vector2 randomOffset = Random.insideUnitCircle.normalized * Random.Range(1.8f, 3.2f);
                Vector3 offsetPos = p.position + new Vector3(randomOffset.x, 0f, randomOffset.y);
                if (NavMesh.SamplePosition(offsetPos, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                {
                    clusterSpots.Add(hit.position);
                }
                else
                {
                    clusterSpots.Add(offsetPos);
                }
            }
        }

        // 2. Thêm các điểm bãi ngẫu nhiên xung quanh khu vực giao tranh (tổng cộng 5 - 8 bãi chùm kiếm)
        int minSpots = Mathf.Max(5, clusterSpots.Count);
        int targetTotalSpots = Random.Range(minSpots, minSpots + 3);
        int neededRandom = Mathf.Max(0, targetTotalSpots - clusterSpots.Count);

        for (int i = 0; i < neededRandom; i++)
        {
            Vector2 randomOffset = Random.insideUnitCircle * Random.Range(3.5f, 13f);
            Vector3 offsetPos = transform.position + new Vector3(randomOffset.x, 0, randomOffset.y);
            if (NavMesh.SamplePosition(offsetPos, out NavMeshHit hit, 6f, NavMesh.AllAreas))
            {
                clusterSpots.Add(hit.position);
            }
            else
            {
                clusterSpots.Add(offsetPos);
            }
        }

        // Tráo ngẫu nhiên thứ tự bãi rơi
        for (int i = 0; i < clusterSpots.Count; i++)
        {
            int rnd = Random.Range(0, clusterSpots.Count);
            Vector3 temp = clusterSpots[i];
            clusterSpots[i] = clusterSpots[rnd];
            clusterSpots[rnd] = temp;
        }

        Vector3[] finalPositions = clusterSpots.ToArray();

        // 3. Kích hoạt animation triệu hồi và đồng bộ qua ClientRpc
        if (!isStandaloneMode && IsServer)
        {
            swordRainCounter.Value++;
            TriggerSwordRainSequenceClientRpc(finalPositions);
        }
        else
        {
            TriggerSwordRainAnim();
            StartCoroutine(RoutineExecuteSwordRainBackground(finalPositions));
        }

        Debug.Log($"[MiniBossAI] Triệu hồi Mưa Kiếm Hoàng Kim: {finalPositions.Length} bãi dội kiếm (mỗi bãi 3-5 thanh kiếm cắm chụm vào 1 chỗ)! Boss vẫn tiếp tục đánh trả bình thường!");
    }

    [ClientRpc]
    private void TriggerSwordRainSequenceClientRpc(Vector3[] finalPositions)
    {
        TriggerSwordRainAnim();
        StartCoroutine(RoutineExecuteSwordRainBackground(finalPositions));
    }

    private IEnumerator RoutineExecuteSwordRainBackground(Vector3[] targetPositions)
    {
        if (targetPositions == null || targetPositions.Length == 0) yield break;

        isSwordRainActive = true;

        // 1. Phóng hàng loạt thanh kiếm vút từ đầu/thân MiniBoss bay vút lên trời
        StartCoroutine(RoutineLaunchAscendingSwords());

        // Chờ 0.4s sau khi kiếm bay vút lên cao thì bắt đầu dội các chùm kiếm từ trên trời xuống
        yield return new WaitForSeconds(0.4f);

        float elapsed = 0f;
        int spawnIdx = 0;
        float dynamicInterval = Mathf.Clamp(swordRainDuration / Mathf.Max(1, targetPositions.Length), 0.8f, 1.4f);

        while (spawnIdx < targetPositions.Length && elapsed < swordRainDuration)
        {
            if (IsDead) yield break;

            Vector3 targetPos = targetPositions[spawnIdx];
            spawnIdx++;

            StartCoroutine(RoutineDropSwordAtPosition(targetPos));

            yield return new WaitForSeconds(dynamicInterval);
            elapsed += dynamicInterval;
        }

        isSwordRainActive = false;
    }

    private IEnumerator RoutineLaunchAscendingSwords()
    {
        int count = Random.Range(12, 18); // 12 đến 18 thanh kiếm bay vút lên trời từ đầu MiniBoss
        Vector3 headPos = transform.position + Vector3.up * 2.6f;

        for (int i = 0; i < count; i++)
        {
            if (IsDead) yield break;

            Vector2 randomSpread = Random.insideUnitCircle * 1.0f;
            Vector3 spawnPos = headPos + new Vector3(randomSpread.x, Random.Range(-0.2f, 0.4f), randomSpread.y);

            Vector3 upwardDir = (Vector3.up * 3.5f + new Vector3(randomSpread.x * 0.6f, 0f, randomSpread.y * 0.6f)).normalized;
            Quaternion rot = Quaternion.LookRotation(upwardDir) * Quaternion.Euler(swordSpawnRotationOffset);

            GameObject sword = GetPooledSword(spawnPos, rot);
            var ascProj = sword.GetComponent<AscendingSwordProjectile>();
            if (ascProj == null) ascProj = sword.AddComponent<AscendingSwordProjectile>();

            ascProj.Initialize(this, spawnPos, upwardDir, 30.0f, 26.0f);

            yield return new WaitForSeconds(0.04f); // Bắn liên hồi từng thanh kiếm vút lên trời cực nhanh
        }
    }

    private void TriggerPhase2Transition()
    {
        if (IsPhase2) return;

        if (isStandaloneMode)
        {
            localIsPhase2 = true;
        }
        else if (IsServer)
        {
            isPhase2Network.Value = true;
            enrageCounter.Value++;
        }

        if (anim != null) anim.speed = phase2AnimSpeed;
        Debug.Log($"[MiniBossAI] Đạt mốc 50% máu ({ActualCurrentHealth}/{maxHealth} HP) -> Chuyển Phase 2 Cuồng Nộ và Triệu Hồi Phân Thân!");
    }

    private void Update()
    {
        // Khóa 100% di chuyển và tấn công của MiniBoss trong suốt thời gian triệu hồi phân thân
        if (isSummonInvulnerable)
        {
            if (AgentReady) agent.isStopped = true;
            SetSpeedNet(0f);
            return;
        }

        if (hitStaggerCooldownTimer > 0f) hitStaggerCooldownTimer -= Time.deltaTime;
        if (attackCooldownTimer > 0) attackCooldownTimer -= Time.deltaTime;
        if (shadowBlinkTimer > 0) shadowBlinkTimer -= Time.deltaTime;
        if (furiousChargeTimer > 0) furiousChargeTimer -= Time.deltaTime;

        if (recentDamageResetTimer > 0f)
        {
            recentDamageResetTimer -= Time.deltaTime;
            if (recentDamageResetTimer <= 0f) recentDamageTaken = 0f;
        }

        // SKILL GAI ĐẤT: MiniBoss chính thi triển kỹ năng Gai Đất (mỗi 5-10s triệu hồi 1 lần, 4-5 bãi đá)
        if (!isClone && IsBossActive && !IsDead && !isSummonInvulnerable)
        {
            earthSpikesTimer -= Time.deltaTime;
            if (earthSpikesTimer <= 0f)
            {
                ResetEarthSpikesTimer();
                TryLaunchEarthSpikesSkill();
            }
        }

        // SKILL MƯA KIẾM: MiniBoss chính thi triển kỹ năng Mưa Kiếm (mỗi 5-10s triệu hồi 1 lần, 5-10 kiếm rơi 10s)
        if (!isClone && IsBossActive && !IsDead && !isSummonInvulnerable)
        {
            swordRainTimer -= Time.deltaTime;
            if (swordRainTimer <= 0f && !isSwordRainActive)
            {
                ResetSwordRainTimer();
                TryLaunchSwordRainSkill();
            }
        }

        bool aiAuth = isStandaloneMode || (IsNetworkActive && IsServer);
        if (!aiAuth) return;

        if (agent != null && agent.isActiveAndEnabled && !agent.isOnNavMesh) SnapToNavMesh();

        // Wall collision resolver
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh && agent.velocity.sqrMagnitude > 0.01f)
        {
            Vector3 rayOrigin = transform.position + Vector3.up * 1.0f;
            Vector3 moveDir = agent.velocity.normalized;
            if (Physics.Raycast(rayOrigin, moveDir, out RaycastHit hit, 0.8f, obstacleLayer, QueryTriggerInteraction.Ignore))
            {
                if (!hit.collider.CompareTag("Player") && !hit.collider.CompareTag("Enemy") && hit.collider.gameObject.layer != LayerMask.NameToLayer("Enemy") && !hit.collider.isTrigger)
                {
                    Vector3 pushBack = hit.normal * 0.15f;
                    agent.Warp(transform.position + pushBack);
                }
            }
        }

        scanTimer -= Time.deltaTime;
        if (scanTimer <= 0)
        {
            scanTimer = scanInterval;
            DetectAndSwitchTarget();
        }

        if (currentFSMState != null)
        {
            currentFSMState.Update();
        }
    }

    private void ChangeState(MiniBossState newState)
    {
        if (currentFSMState != null)
        {
            currentFSMState.Exit();
        }

        if (!isStandaloneMode && IsServer)
        {
            currentState.Value = newState;
        }
        else
        {
            localState = newState;
        }

        switch (newState)
        {
            case MiniBossState.Idle:      currentFSMState = idleState;      break;
            case MiniBossState.Chase:     currentFSMState = chaseState;     break;
            case MiniBossState.Attack:    currentFSMState = attackState;    break;
            case MiniBossState.SwordRain: currentFSMState = swordRainState; break;
            case MiniBossState.Hit:       currentFSMState = hitState;       break;
            case MiniBossState.Enrage:    currentFSMState = enrageState;    break;
            case MiniBossState.Dead:      currentFSMState = deadState;      break;
        }

        if (currentFSMState != null)
        {
            currentFSMState.Enter();
        }
    }

    // ══════════════════════════════════════════════════════════
    //  FSM STATE ACTIONS
    // ══════════════════════════════════════════════════════════

    private void HandleIdle()
    {
        if (IsBossActive && targetPlayer != null)
        {
            hasWanderDestination = false;
            ChangeState(MiniBossState.Chase);
            return;
        }

        if (!hasWanderDestination)
        {
            wanderWaitTimer -= Time.deltaTime;
            if (wanderWaitTimer <= 0)
            {
                Vector3 randomDirection = Random.insideUnitSphere * 8f + transform.position;
                if (NavMesh.SamplePosition(randomDirection, out NavMeshHit navHit, 8f, NavMesh.AllAreas))
                {
                    wanderDestination = navHit.position;
                    hasWanderDestination = true;
                    if (AgentReady)
                    {
                        agent.isStopped = false;
                        agent.speed = walkSpeed;
                        agent.SetDestination(wanderDestination);
                    }
                }
            }
            SetSpeedNet(0f);
        }
        else
        {
            SetSpeedNet(0.5f); // Walk animation

            if (AgentReady)
            {
                if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f)
                {
                    hasWanderDestination = false;
                    wanderWaitTimer = Random.Range(2.0f, 4.0f);
                }
            }
            else
            {
                hasWanderDestination = false;
            }
        }
    }

    private void HandleChase()
    {
        if (targetPlayer == null || IsPlayerDeadOrInvisible(targetPlayer))
        {
            targetPlayer = null;
            isFuriousCharging = false;
            ChangeState(MiniBossState.Idle);
            return;
        }

        // 1. Kiểm tra NavMesh Reachability (Chỉ dùng Tốc Biến khi Player ở vị trí lỗi địa hình/ngoài bản đồ)
        if (!IsTargetReachableOnNavMesh(targetPlayer))
        {
            Transform altTarget = FindNearestReachablePlayer();
            if (altTarget != null)
            {
                targetPlayer = altTarget;
            }
            else
            {
                if (AgentReady) agent.isStopped = true;
                SetSpeedNet(0f);
                
                if (shadowBlinkTimer <= 0f)
                {
                    ExecuteShadowBlink(targetPlayer);
                }
                return;
            }
        }

        float dist = Vector3.Distance(transform.position, targetPlayer.position);

        // 2. Kỹ năng Cuồng Phong Tăng Tốc Lao Tới (Chỉ thỉnh thoảng 20s kích hoạt 1 lần khi Player ở rất xa > 9.5m)
        if (!isFuriousCharging && furiousChargeTimer <= 0f && dist > 9.5f)
        {
            if (Random.value < 0.40f)
            {
                isFuriousCharging = true;
                furiousChargeDurationTimer = 2.2f;
                furiousChargeTimer = IsPhase2 ? 15.0f : furiousChargeCooldown;
                PlayShadowBlinkVisuals();
                Debug.Log($"[MiniBossAI] Lâu lâu Mini Boss gồng nộ KÍCH HOẠT CUỒNG PHONG LAO TỐC ĐỘ CAO (15.5 m/s)!");
            }
            else
            {
                furiousChargeTimer = 3.0f; // Nếu chưa kích hoạt thì chờ thêm 3s mới quét lại
            }
        }

        if (isFuriousCharging)
        {
            furiousChargeDurationTimer -= Time.deltaTime;
            if (furiousChargeDurationTimer <= 0f || dist <= attackRange)
            {
                isFuriousCharging = false;
            }
        }

        if (dist <= attackRange && attackCooldownTimer <= 0)
        {
            isFuriousCharging = false;
            if (AgentReady) agent.isStopped = true;
            SetSpeedNet(0f);
            ChangeState(MiniBossState.Attack);
            return;
        }

        // CHIẾN THUẬT BAO VÂY GỌNG KÌM 3 PHÍA CHO PHÂN THÂN
        Vector3 chaseDestination = targetPlayer.position;
        if (isClone)
        {
            int flankSign = (GetInstanceID() % 2 == 0) ? 1 : -1;
            float angleOffset = flankSign * 60f; // Trái +60 độ, Phải -60 độ
            Vector3 dirFromPlayer = (transform.position - targetPlayer.position).normalized;
            if (dirFromPlayer.sqrMagnitude < 0.01f) dirFromPlayer = -targetPlayer.forward;
            
            Vector3 flankDir = Quaternion.Euler(0, angleOffset, 0) * dirFromPlayer;
            Vector3 desiredFlankPos = targetPlayer.position + flankDir * (attackRange * 0.85f);

            if (NavMesh.SamplePosition(desiredFlankPos, out NavMeshHit flankHit, 3.5f, NavMesh.AllAreas))
            {
                chaseDestination = flankHit.position;
            }
        }

        // Di chuyển áp sát mục tiêu (Tự động tăng tốc cực mạnh nếu đang Cuồng Phong Lao Tới)
        if (AgentReady)
        {
            agent.isStopped = false;
            float baseRun = IsPhase2 ? (runSpeed * phase2SpeedMultiplier) : runSpeed;
            float activeRunSpeed = isFuriousCharging 
                ? (IsPhase2 ? (furiousChargeSpeed * phase2SpeedMultiplier) : furiousChargeSpeed)
                : baseRun;

            agent.speed = activeRunSpeed;
            agent.SetDestination(chaseDestination);
        }
        
        SetSpeedNet(AgentReady && !agent.isStopped ? 1.0f : 0f); // Run animation
        RotateTowards(targetPlayer.position);
    }

    private void ExecuteShadowBlink(Transform target)
    {
        if (target == null) return;

        shadowBlinkTimer = IsPhase2 ? (shadowBlinkCooldown * 0.65f) : shadowBlinkCooldown;

        Vector3 backPos = target.position - target.forward * 1.5f;
        if (!NavMesh.SamplePosition(backPos, out NavMeshHit navHit, 3.5f, NavMesh.AllAreas))
        {
            Vector3 sidePos = target.position + target.right * 1.5f;
            if (!NavMesh.SamplePosition(sidePos, out navHit, 3.5f, NavMesh.AllAreas))
            {
                navHit.position = target.position;
            }
        }

        if (!isStandaloneMode && IsServer)
        {
            shadowBlinkCounter.Value++;
        }
        else
        {
            PlayShadowBlinkVisuals();
        }

        if (AgentReady)
        {
            agent.Warp(navHit.position);
        }
        else
        {
            transform.position = navHit.position;
        }

        FaceTargetImmediately(target.position);
        CameraShakeHelper.ShakeAtPosition(navHit.position, 0.5f, 1.0f, 35.0f);
        Debug.Log($"[MiniBossAI] Bí thuật Tốc Biến Bóng Tối xuất hiện đằng sau {target.name}!");

        ChangeState(MiniBossState.Attack);
    }

    private void PlayShadowBlinkVisuals()
    {
        if (shadowBlinkVFX != null)
        {
            // Gán VFX đi theo tâm thân người MiniBoss 100% (cao 0.45m x scale 3.0 = 1.35m giữa ngực)
            GameObject vfx = Instantiate(shadowBlinkVFX, transform.position, transform.rotation, transform);
            vfx.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            vfx.transform.localRotation = Quaternion.identity;
            Destroy(vfx, 2.5f);
        }
        if (shadowBlinkSFX != null)
        {
            AudioSource.PlayClipAtPoint(shadowBlinkSFX, transform.position + Vector3.up * 1.35f, 1.0f);
        }
    }

    private void HandleAttack()
    {
        stateTimer -= Time.deltaTime;

        if (isLeaping && leapTimer >= 0f)
        {
            leapTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(leapTimer / currentLeapDuration);

            if (AgentReady)
            {
                agent.Move(transform.forward * currentLeapForwardSpeed * Time.deltaTime);
            }

            if (visualRoot != null)
            {
                float yOffset = currentLeapHeight * Mathf.Sin(Mathf.PI * progress);
                visualRoot.localPosition = initialVisualLocalPos + new Vector3(0, yOffset, 0);
            }

            if (leapTimer >= currentLeapDuration)
            {
                isLeaping = false;
                if (visualRoot != null) visualRoot.localPosition = initialVisualLocalPos;
                if (currentAttackIndex == 1 || currentLeapHeight > 2.0f)
                {
                    CameraShakeHelper.ShakeAtPosition(transform.position, 0.7f, 1.3f, 40.0f);
                }
            }
        }
        else if (isLeaping && leapTimer < 0f)
        {
            leapTimer += Time.deltaTime;
        }

        if (targetPlayer != null)
        {
            RotateTowards(targetPlayer.position);
        }

        if (stateTimer <= 0)
        {
            attackCooldownTimer = IsPhase2 ? (attackCooldown * phase2CooldownMultiplier) : attackCooldown;
            if (targetPlayer != null) ChangeState(MiniBossState.Chase);
            else ChangeState(MiniBossState.Idle);
        }
    }

    private void HandleHit()
    {
        if (AgentReady) agent.isStopped = true;
        SetSpeedNet(0f);

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0)
        {
            if (targetPlayer != null) ChangeState(MiniBossState.Chase);
            else ChangeState(MiniBossState.Idle);
        }
    }

    private void HandleEnrage()
    {
        if (AgentReady) agent.isStopped = true;
        SetSpeedNet(0f);

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0)
        {
            if (targetPlayer != null) ChangeState(MiniBossState.Chase);
            else ChangeState(MiniBossState.Idle);
        }
    }

private void Die()
    {
        // 1. Dừng toàn bộ Coroutine đòn đánh/gây sát thương của boss ngay lập tức để không gây mất máu player sau khi chết
        StopAllCoroutines();
        CameraShakeHelper.StopShake(); // Dừng rung camera khi boss chết để chuẩn bị cutscene mượt mà

        if (!isClone)
        {
            PlayBossAudioNet(0);
        }

        // *** TRIỆT ĐỂ DỌN SẠCH TẤT CẢ ĐỐI TƯỢNG GÂY SÁT THƯƠNG CÒN SÓT LẠI TRÊN SÀN ĐẤU ***
        // Dọn bãi gai đá (EarthSpikesDamageZone)
        var allSpikes = FindObjectsByType<EarthSpikesDamageZone>(FindObjectsSortMode.None);
        foreach (var spike in allSpikes)
        {
            if (spike != null) Destroy(spike.gameObject);
        }

        // Dọn kiếm rơi đang bay trên trời (FallingSwordProjectile) - đây là nguyên nhân chính gây mất máu sau khi boss chết
        var allFallingSwords = FindObjectsByType<FallingSwordProjectile>(FindObjectsSortMode.None);
        foreach (var sword in allFallingSwords)
        {
            if (sword != null) Destroy(sword.gameObject);
        }

        // Dọn kiếm bay vút lên trời (AscendingSwordProjectile)
        var allAscendingSwords = FindObjectsByType<AscendingSwordProjectile>(FindObjectsSortMode.None);
        foreach (var sword in allAscendingSwords)
        {
            if (sword != null) Destroy(sword.gameObject);
        }

        if (AgentReady) agent.isStopped = true;
        if (agent != null) agent.enabled = false;
        SetSpeedNet(0f);

        if (visualRoot != null) visualRoot.localPosition = initialVisualLocalPos;

        // 2. Tắt toàn bộ Collider và Trigger để boss đã chết không còn gây sát thương
        var colliders = GetComponentsInChildren<Collider>();
        foreach (var c in colliders)
        {
            if (c != null) c.enabled = false;
        }

        if (isClone)
        {
            TriggerSoulBurstExplosion();
        }
        else if (IsPhase2)
        {
            TriggerDeathExplosion();
        }

        // --- ĐỒNG BỘ NETWORK: Server quản lý death count và trigger cutscene ---
        if (!isStandaloneMode && IsServer)
        {
            dieCounter.Value++;

            if (!isClone)
            {
                mainBossDeadNet.Value = true;
                CheckAndTriggerAllMiniBossesDead();
            }
        }
        else if (isStandaloneMode)
        {
            // Standalone mode: kiểm tra cục bộ như cũ
            if (anim != null) anim.SetTrigger(dieTrigger);
            if (AreAllBossesAndClonesDead())
            {
                TriggerBossDefeatCutscene();
            }
        }
        else if (anim != null)
        {
            anim.SetTrigger(dieTrigger);
        }

        Debug.Log($"[MiniBossAI] {(isClone ? "Phân thân" : "Boss chính")} is dead!");
        Destroy(gameObject, isClone ? 1.5f : 5f);
    }

    /// <summary>
    /// Callback khi dieCounter thay đổi trên mạng (Đồng bộ cái chết tới tất cả Client).
    /// </summary>
    private void OnDieCounterChanged()
    {
        if (anim != null) anim.SetTrigger(dieTrigger);
        StopAllCoroutines();
        localState = MiniBossState.Dead;
        localHealth = 0f;

        // Tắt toàn bộ Collider trên Client
        var colliders = GetComponentsInChildren<Collider>();
        foreach (var c in colliders)
        {
            if (c != null) c.enabled = false;
        }

        if (agent != null) agent.enabled = false;

        // Dọn sạch toàn bộ các đòn đánh, bãi gai đá, kiếm rơi tồn tại trên máy Client
        var allSpikes = FindObjectsByType<EarthSpikesDamageZone>(FindObjectsSortMode.None);
        foreach (var spike in allSpikes)
        {
            if (spike != null) Destroy(spike.gameObject);
        }

        var allFallingSwords = FindObjectsByType<FallingSwordProjectile>(FindObjectsSortMode.None);
        foreach (var sword in allFallingSwords)
        {
            if (sword != null) Destroy(sword.gameObject);
        }

        var allAscendingSwords = FindObjectsByType<AscendingSwordProjectile>(FindObjectsSortMode.None);
        foreach (var sword in allAscendingSwords)
        {
            if (sword != null) Destroy(sword.gameObject);
        }

        // Ẩn ngay lập tức thanh máu MiniBoss trên toàn bộ UIDocument của máy Client
        var allUIDocs = FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var doc in allUIDocs)
        {
            if (doc != null && doc.rootVisualElement != null)
            {
                var mbHud = doc.rootVisualElement.Q<VisualElement>("miniboss-hud-container");
                if (mbHud != null) mbHud.style.display = DisplayStyle.None;
            }
        }

        var allHealthBars = FindObjectsByType<MiniBossHealthBar>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var hb in allHealthBars)
        {
            if (hb != null) hb.HideUI();
        }
    }

    /// <summary>
    /// Callback khi defeatCutsceneCounter thay đổi (Server đã xác nhận tất cả đều chết).
    /// Chạy trên TẤT CẢ client đồng thời.
    /// </summary>
    private void OnDefeatCutsceneCounterChanged()
    {
        Debug.Log("[MiniBossAI] [Network] defeatCutsceneCounter changed → Tất cả Client bắt đầu chạy cutscene!");
        TriggerBossDefeatCutscene();
    }

    public void TriggerBossDefeatCutscene()
    {
        Debug.Log("[MiniBossAI] TẤT CẢ BOSS CHÍNH VÀ 2 PHÂN THÂN ĐÃ BỊ TIÊU DIỆT HOÀN TOÀN -> BẮT ĐẦU QUY TRÌNH HỦY UI VÀ CHẠY CUTSCENE!");
        CameraShakeHelper.StopShake(); // Dừng ngay lập tức mọi rung lắc camera để chuẩn bị chuyển cảnh Cutscene mượt mà

        // 1. Dọn sạch mọi clone còn sót lại trên máy local
        ForceDestroyAllRemainingClones();

        // 2. Ẩn / Hủy hoàn toàn UI MiniBossHealthBar trên tất cả UIDocument
        var allUIDocs = FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var doc in allUIDocs)
        {
            if (doc != null && doc.rootVisualElement != null)
            {
                var mbHud = doc.rootVisualElement.Q<VisualElement>("miniboss-hud-container");
                if (mbHud != null)
                {
                    mbHud.style.display = DisplayStyle.None;
                }
            }
        }

        var allHealthBars = FindObjectsByType<MiniBossHealthBar>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var hb in allHealthBars)
        {
            if (hb != null)
            {
                hb.HideUI();
            }
        }

        // 3. Kích hoạt onBossDeathEvent của Boss chính
        var mainBoss = FindMainBoss() ?? this;
        bool hasInvoked = false;

        if (mainBoss != null && mainBoss.onBossDeathEvent != null && mainBoss.onBossDeathEvent.GetPersistentEventCount() > 0)
        {
            try 
            { 
                mainBoss.onBossDeathEvent.Invoke(); 
                hasInvoked = true;
                Debug.Log("[MiniBossAI] Đã gọi mainBoss.onBossDeathEvent.Invoke() thành công!");
            } 
            catch (System.Exception ex) { Debug.LogError($"[MiniBossAI] Error invoking onBossDeathEvent: {ex}"); }
        }
        else if (onBossDeathEvent != null && onBossDeathEvent.GetPersistentEventCount() > 0)
        {
            try 
            { 
                onBossDeathEvent.Invoke(); 
                hasInvoked = true;
                Debug.Log("[MiniBossAI] Đã gọi local onBossDeathEvent.Invoke() thành công!");
            } 
            catch (System.Exception ex) { Debug.LogError($"[MiniBossAI] Error invoking local onBossDeathEvent: {ex}"); }
        }

        // 4. Tìm VideoCutsceneController liên kết trong Scene (cuscene 11 / miniboss / boss) để kích hoạt StartCutscene()
        if (!hasInvoked)
        {
            var allCutscenes = FindObjectsByType<VideoCutsceneController>(FindObjectsSortMode.None);
            foreach (var cs in allCutscenes)
            {
                if (cs != null && !cs.isPlaying && (!cs.playOnlyOnce || !cs.hasPlayed))
                {
                    string n = cs.gameObject.name.ToLower();
                    if (n.Contains("11") || n.Contains("miniboss") || n.Contains("boss") || n.Contains("cutscene3") || n.Contains("cutscene4"))
                    {
                        Debug.Log($"[MiniBossAI] Tự động kích hoạt VideoCutsceneController: '{cs.gameObject.name}'");
                        cs.StartCutscene();
                        break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Dọn sạch tất cả phân thân còn sót lại trên máy local khi cutscene bắt đầu hoặc khi nhận tín hiệu kết thúc.
    /// Đảm bảo không còn bất kỳ phân thân nào hiện trên bất kỳ máy nào.
    /// </summary>
    public void ForceDestroyAllRemainingClones()
    {
        var allBosses = FindObjectsByType<MiniBossAI>(FindObjectsSortMode.None);
        foreach (var b in allBosses)
        {
            if (b != null && b.isClone)
            {
                b.localHealth = 0f;
                Destroy(b.gameObject);
            }
        }

        var allSpikes = FindObjectsByType<EarthSpikesDamageZone>(FindObjectsSortMode.None);
        foreach (var s in allSpikes)
        {
            if (s != null) Destroy(s.gameObject);
        }

        var allFallingSwords = FindObjectsByType<FallingSwordProjectile>(FindObjectsSortMode.None);
        foreach (var sword in allFallingSwords)
        {
            if (sword != null) Destroy(sword.gameObject);
        }

        var allAscendingSwords = FindObjectsByType<AscendingSwordProjectile>(FindObjectsSortMode.None);
        foreach (var sword in allAscendingSwords)
        {
            if (sword != null) Destroy(sword.gameObject);
        }
    }

    private void TriggerDeathExplosion()
    {
        if (!isStandaloneMode && IsServer)
        {
            deathExplosionCounter.Value++;
        }
        else if (isStandaloneMode)
        {
            PlayDeathExplosionEffects();
        }

        bool auth = isStandaloneMode || (IsNetworkActive && IsServer);
        if (auth)
        {
            Vector3 explodePos = transform.position + Vector3.up * 1.2f;
            Collider[] hits = Physics.OverlapSphere(explodePos, explosionRadius, playerLayer);
            HashSet<Transform> damagedRoots = new HashSet<Transform>();

            foreach (var hit in hits)
            {
                Transform root = GetPlayerRoot(hit.transform);
                if (root != null && !damagedRoots.Contains(root))
                {
                    damagedRoots.Add(root);
                    Vector3 knockbackDir = (root.position - transform.position);
                    knockbackDir.y = 0.5f;
                    knockbackDir = knockbackDir.normalized;
                    Vector3 force = knockbackDir * explosionKnockback;

                    EnemyDamageHelper.DealDamage(root, explosionDamage, force);
                }
            }
        }
    }

    public void HealBoss(float amount)
    {
        if (IsDead || ActualCurrentHealth <= 0) return;

        if (isStandaloneMode)
        {
            localHealth = Mathf.Min(localHealth + amount, maxHealth);
        }
        else if (IsServer)
        {
            currentHealth.Value = Mathf.Min(currentHealth.Value + amount, maxHealth);
        }

        PlaySummonCloneVisuals();
    }

    private void TriggerSoulBurstExplosion()
    {
        Vector3 explodePos = transform.position + Vector3.up * 1.2f;
        PlayDeathExplosionEffects();

        bool auth = isStandaloneMode || (IsNetworkActive && IsServer);
        if (auth)
        {
            // Sát thương bạo nổ linh hồn gây dame cho Player đứng gần
            Collider[] hits = Physics.OverlapSphere(explodePos, 3.5f, playerLayer);
            HashSet<Transform> damagedRoots = new HashSet<Transform>();

            foreach (var hit in hits)
            {
                Transform root = GetPlayerRoot(hit.transform);
                if (root != null && !damagedRoots.Contains(root))
                {
                    damagedRoots.Add(root);
                    Vector3 kbDir = (root.position - transform.position).normalized + Vector3.up * 0.5f;
                    EnemyDamageHelper.DealDamage(root, 25f, kbDir * 6f);
                }
            }

            // Hồi 10% máu cho Trùm Phụ chính nếu đứng gần dưới 15m
            var allBosses = FindObjectsByType<MiniBossAI>(FindObjectsSortMode.None);
            foreach (var b in allBosses)
            {
                if (b != null && !b.isClone && !b.IsDead && b.ActualCurrentHealth > 0)
                {
                    if (Vector3.Distance(transform.position, b.transform.position) <= 15.0f)
                    {
                        float healAmount = b.maxHealth * 0.10f;
                        b.HealBoss(healAmount);
                        Debug.Log($"[MiniBossAI] Phân thân hy sinh! Trùm Phụ chính đứng gần được hồi {healAmount} HP!");
                    }
                }
            }
        }
    }

    private float recentDamageTaken = 0f;
    private float recentDamageResetTimer = 0f;

    public void CheckShadowBlinkDodge(float damage)
    {
        // Tắt hoàn toàn tốc biến / dịch chuyển né đòn khi đánh nhau theo yêu cầu!
        return;
    }

    private void ExecuteShadowBlinkDodge()
    {
        if (targetPlayer == null) return;

        Vector3 escapeDir = (transform.position - targetPlayer.position).normalized;
        escapeDir += (Random.insideUnitSphere * 0.4f);
        escapeDir.y = 0;
        escapeDir = escapeDir.normalized;

        Vector3 blinkTarget = transform.position + escapeDir * 4.5f;
        if (NavMesh.SamplePosition(blinkTarget, out NavMeshHit hit, 4.0f, NavMesh.AllAreas))
        {
            PlayShadowBlinkVisuals();
            if (AgentReady) agent.Warp(hit.position);
            FaceTargetImmediately(targetPlayer.position);
            Debug.Log($"[MiniBossAI] Mini Boss/Phân Thân bị dồn dame sát thương! KÍCH HOẠT TỐC BIẾN NÉ ĐÒN 4.5M!");
        }
    }

    private void PlayDeathExplosionEffects()
    {
        Vector3 spawnPos = transform.position + Vector3.up * 1.2f;
        CameraShakeHelper.StopShake(); // Dừng rung camera khi boss/phân thân chết (chỉ rung khi Boss dùng Skill)
        if (deathExplosionVFX != null)
        {
            GameObject vfx = Instantiate(deathExplosionVFX, spawnPos, Quaternion.identity);
            Destroy(vfx, 4f);
        }
        if (deathExplosionSound != null)
        {
            AudioSource.PlayClipAtPoint(deathExplosionSound, spawnPos, 1.0f);
        }
    }

    public void PlayEnrageVFX()
    {
        Vector3 spawnPos = transform.position + Vector3.up * 1f;
        CameraShakeHelper.Shake(1.8f, 1.0f); // Rung chấn động khi Boss gầm thét chuyển Phase
        if (enrageVFXPrefab != null)
        {
            GameObject vfx = Instantiate(enrageVFXPrefab, spawnPos, transform.rotation);
            vfx.transform.SetParent(transform);
            Destroy(vfx, 5f);
        }
        if (enrageSFXSound != null)
        {
            AudioSource.PlayClipAtPoint(enrageSFXSound, spawnPos, 1.0f);
        }
    }

    // ══════════════════════════════════════════════════════════
    //  DYNAMIC TARGET DETECTION & NEAREST TARGET SWITCHING
    // ══════════════════════════════════════════════════════════

    private List<Transform> GetAllActivePlayers()
    {
        var list = new List<Transform>();

        var leos = FindObjectsByType<LeoPlayer>(FindObjectsSortMode.None);
        foreach (var p in leos) if (p != null && !list.Contains(p.transform)) list.Add(p.transform);

        var arthurs = FindObjectsByType<ArthurPlayer>(FindObjectsSortMode.None);
        foreach (var p in arthurs) if (p != null && !list.Contains(p.transform)) list.Add(p.transform);

        var elenas = FindObjectsByType<ElenaPlayer>(FindObjectsSortMode.None);
        foreach (var p in elenas) if (p != null && !list.Contains(p.transform)) list.Add(p.transform);

        var elenaArchers = FindObjectsByType<ElenaArcher>(FindObjectsSortMode.None);
        foreach (var p in elenaArchers) if (p != null && !list.Contains(p.transform)) list.Add(p.transform);

        var mayas = FindObjectsByType<MayaPlayer>(FindObjectsSortMode.None);
        foreach (var p in mayas) if (p != null && !list.Contains(p.transform)) list.Add(p.transform);

        var mayaSupports = FindObjectsByType<MayaSupport>(FindObjectsSortMode.None);
        foreach (var p in mayaSupports) if (p != null && !list.Contains(p.transform)) list.Add(p.transform);

        var simples = FindObjectsByType<SimplePlayerTest>(FindObjectsSortMode.None);
        foreach (var p in simples) if (p != null && !list.Contains(p.transform)) list.Add(p.transform);

        var skeletons = FindObjectsByType<Skeleton>(FindObjectsSortMode.None);
        foreach (var p in skeletons) if (p != null && !list.Contains(p.transform)) list.Add(p.transform);

        var taggedPlayers = GameObject.FindGameObjectsWithTag("Player");
        foreach (var go in taggedPlayers) if (go != null && !list.Contains(go.transform)) list.Add(go.transform);

        return list;
    }

    private void DetectAndSwitchTarget()
    {
        if (IsDead) return;

        Transform bestTarget = null;
        float minPathDist = float.MaxValue;
        float currentTargetDist = float.MaxValue;

        if (targetPlayer != null && !IsPlayerDeadOrInvisible(targetPlayer) && IsTargetReachableOnNavMesh(targetPlayer))
        {
            currentTargetDist = GetNavMeshPathDistance(transform.position, targetPlayer.position);
        }

        var activePlayers = GetAllActivePlayers();
        foreach (var pTrans in activePlayers)
        {
            if (pTrans == null || IsPlayerDeadOrInvisible(pTrans)) continue;

            if (!IsTargetReachableOnNavMesh(pTrans)) continue;

            float d = GetNavMeshPathDistance(transform.position, pTrans.position);
            float currentSight = IsBossActive ? 50f : sightRange;
            if (d > currentSight) continue;

            float hpRatio = GetPlayerHealthRatio(pTrans);
            if (hpRatio <= 0.35f)
            {
                d *= 0.6f;
            }

            if (d < minPathDist)
            {
                minPathDist = d;
                bestTarget = pTrans;
            }
        }

        if (bestTarget != null)
        {
            bool shouldSwitch = false;
            if (targetPlayer == null)
            {
                shouldSwitch = true;
            }
            else if (bestTarget != targetPlayer)
            {
                if (minPathDist < currentTargetDist - 1.8f || minPathDist < 3.5f)
                {
                    shouldSwitch = true;
                }
            }

            if (shouldSwitch)
            {
                targetPlayer = bestTarget;
            }
        }
        else if (CurrentStateValue == MiniBossState.Chase)
        {
            targetPlayer = null;
        }
    }

    private Transform FindNearestReachablePlayer()
    {
        Transform best = null;
        float minScore = float.MaxValue;
        var players = GetAllActivePlayers();
        foreach (var p in players)
        {
            if (p == null || IsPlayerDeadOrInvisible(p)) continue;
            if (IsTargetReachableOnNavMesh(p))
            {
                float d = GetNavMeshPathDistance(transform.position, p.position);
                float hpRatio = GetPlayerHealthRatio(p);
                float score = d * (0.5f + 0.5f * hpRatio);
                if (hpRatio < 0.35f) score *= 0.6f; // Ưu tiên tập trung săn Player yếu máu (<35% HP)

                if (score < minScore)
                {
                    minScore = score;
                    best = p;
                }
            }
        }
        return best;
    }

    private bool IsTargetReachableOnNavMesh(Transform player)
    {
        if (player == null) return false;
        NavMeshPath path = new NavMeshPath();
        if (NavMesh.CalculatePath(transform.position, player.position, NavMesh.AllAreas, path))
        {
            if (path.status == NavMeshPathStatus.PathComplete) return true;
        }
        return false;
    }

    private float GetNavMeshPathDistance(Vector3 start, Vector3 target)
    {
        NavMeshPath path = new NavMeshPath();
        if (NavMesh.CalculatePath(start, target, NavMesh.AllAreas, path))
        {
            if (path.status == NavMeshPathStatus.PathComplete || path.status == NavMeshPathStatus.PathPartial)
            {
                float dist = 0f;
                for (int i = 0; i < path.corners.Length - 1; i++)
                {
                    dist += Vector3.Distance(path.corners[i], path.corners[i + 1]);
                }
                return dist;
            }
        }
        return Vector3.Distance(start, target);
    }

    private float GetPlayerHealthRatio(Transform player)
    {
        if (player == null) return 1f;
        var ps = player.GetComponentInParent<IPlayerHUDTarget>() ?? player.GetComponentInChildren<IPlayerHUDTarget>();
        if (ps != null && ps.MaxHealth > 0) return ps.CurrentHealth / ps.MaxHealth;

        var sk = player.GetComponentInParent<Skeleton>();
        if (sk != null && sk.maxHealth > 0) return sk.CurrentHealthValue / sk.maxHealth;

        return 1f;
    }

    private bool IsPlayerDeadOrInvisible(Transform player)
    {
        var ps = player.GetComponentInParent<IPlayerHUDTarget>();
        var sk = player.GetComponentInParent<Skeleton>();
        return (ps != null && (ps.CurrentHealth <= 0 || ps.IsInvisible)) || (sk != null && sk.CurrentHealthValue <= 0);
    }

    public void DealSwordDamageContinuously(HashSet<Transform> hitThisAttack)
    {
        Vector3 finalKnockback = Vector3.zero; // Không đẩy Player khi chém
        float finalDamage = IsPhase2 ? (attackDamage * 1.25f) : attackDamage;

        if (swordBase != null && swordTip != null)
        {
            Vector3 start = swordBase.position;
            Vector3 end = swordTip.position;
            Vector3 dir = (end - start).normalized;
            float dist = Vector3.Distance(start, end);

            RaycastHit[] hits = Physics.SphereCastAll(start, swordThickness, dir, dist, playerLayer);
            foreach (var hit in hits)
            {
                Transform root = GetPlayerRoot(hit.collider.transform);
                if (root != null && !hitThisAttack.Contains(root))
                {
                    hitThisAttack.Add(root);
                    EnemyDamageHelper.DealDamage(root, finalDamage, finalKnockback);
                }
            }
        }
        else
        {
            Vector3 origin = transform.position + Vector3.up * 1f;
            RaycastHit[] hits = Physics.SphereCastAll(origin, swordThickness, transform.forward, attackRange, playerLayer);
            foreach (var hit in hits)
            {
                Transform root = GetPlayerRoot(hit.collider.transform);
                if (root != null && !hitThisAttack.Contains(root))
                {
                    hitThisAttack.Add(root);
                    EnemyDamageHelper.DealDamage(root, finalDamage, finalKnockback);
                }
            }
        }
    }

    private Transform GetPlayerRoot(Transform t)
    {
        if (t.GetComponentInParent<LeoPlayer>() != null) return t.GetComponentInParent<LeoPlayer>().transform;
        if (t.GetComponentInParent<ArthurPlayer>() != null) return t.GetComponentInParent<ArthurPlayer>().transform;
        if (t.GetComponentInParent<ElenaPlayer>() != null) return t.GetComponentInParent<ElenaPlayer>().transform;
        if (t.GetComponentInParent<ElenaArcher>() != null) return t.GetComponentInParent<ElenaArcher>().transform;
        if (t.GetComponentInParent<MayaPlayer>() != null) return t.GetComponentInParent<MayaPlayer>().transform;
        if (t.GetComponentInParent<MayaSupport>() != null) return t.GetComponentInParent<MayaSupport>().transform;
        if (t.GetComponentInParent<SimplePlayerTest>() != null) return t.GetComponentInParent<SimplePlayerTest>().transform;
        if (t.GetComponentInParent<Skeleton>() != null) return t.GetComponentInParent<Skeleton>().transform;
        return t;
    }

    private void SnapToNavMesh()
    {
        if (agent == null || !agent.isActiveAndEnabled) return;
        if (!agent.isOnNavMesh)
        {
            NavMeshHit h;
            if (NavMesh.SamplePosition(transform.position, out h, 10f, NavMesh.AllAreas))
            {
                agent.Warp(h.position);
            }
        }
    }

    private void RotateTowards(Vector3 targetPos)
    {
        Vector3 dir = (targetPos - transform.position);
        dir.y = 0;
        if (dir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);
            float rotSpeed = (CurrentStateValue == MiniBossState.Attack) ? 540f : 360f;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotSpeed * Time.deltaTime);
        }
    }

    public void FaceTargetImmediately(Vector3 targetPos)
    {
        Vector3 dir = (targetPos - transform.position);
        dir.y = 0;
        if (dir.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(dir);
        }
    }

    private void ApplySpeedAnim(float speed)
    {
        if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null) return;
        anim.SetFloat(speedParam, speed);
    }

    private void SetSpeedNet(float val)
    {
        if (isStandaloneMode) ApplySpeedAnim(val);
        else if (IsServer && !Mathf.Approximately(netSpeed.Value, val)) netSpeed.Value = val;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        if (swordBase != null && swordTip != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(swordBase.position, swordTip.position);
            Gizmos.DrawWireSphere(swordBase.position, swordThickness);
            Gizmos.DrawWireSphere(swordTip.position, swordThickness);
        }
    }

    // ══════════════════════════════════════════════════════════
    //  STATE MACHINE INNER CLASSES
    // ══════════════════════════════════════════════════════════

    private class IdleState : IEnemyState
    {
        private MiniBossAI boss;
        public IdleState(MiniBossAI boss) { this.boss = boss; }
        public void Enter()
        {
            boss.hasWanderDestination = false;
            boss.wanderWaitTimer = Random.Range(1f, 3f);
        }
        public void Update() { boss.HandleIdle(); }
        public void Exit() { }
    }

    private class ChaseState : IEnemyState
    {
        private MiniBossAI boss;
        public ChaseState(MiniBossAI boss) { this.boss = boss; }
        public void Enter() { }
        public void Update() { boss.HandleChase(); }
        public void Exit() { }
    }

    private class AttackState : IEnemyState
    {
        private MiniBossAI boss;
        private AttackConfig config;
        private HashSet<Transform> hitPlayersThisAttack = new HashSet<Transform>();

        public AttackState(MiniBossAI boss) { this.boss = boss; }

        public void Enter()
        {
            hitPlayersThisAttack.Clear();
            boss.hasDealtDamage = false;

            // BẢO ĐẢM 100%: Lập tức quay mặt chính diện thẳng 100% về phía Player đang đuổi theo ngay khi bắt đầu vung kiếm!
            if (boss.targetPlayer != null)
            {
                boss.FaceTargetImmediately(boss.targetPlayer.position);
            }

            // TẢN NHỊP ĐÁNH LIÊN HOÀN 1-2-3 GIỮA TRÙM PHỤ VÀ PHÂN THÂN
            var allBosses = Object.FindObjectsByType<MiniBossAI>(FindObjectsSortMode.None);
            foreach (var b in allBosses)
            {
                if (b != null && b != boss && b.isClone && b.CurrentStateValue == MiniBossState.Chase)
                {
                    b.attackCooldownTimer = Mathf.Max(b.attackCooldownTimer, 0.4f);
                }
            }

            if (!boss.isStandaloneMode)
            {
                boss.attackTypeSync.Value = boss.currentAttackIndex;
                boss.attackCounter.Value++;
            }
            else if (boss.anim != null)
            {
                boss.anim.SetTrigger(boss.attackTriggers[boss.currentAttackIndex]);
            }

            config = boss.attackConfigs[boss.currentAttackIndex];
            boss.stateTimer = config.duration;

            boss.isLeaping = true;
            boss.leapTimer = - (config.duration * config.leapStartPercent);
            boss.currentLeapDuration = config.duration * config.leapDurationPercent;
            boss.currentLeapForwardSpeed = config.forwardSpeed;
            boss.currentLeapHeight = config.peakHeight;

            boss.currentAttackIndex = (boss.currentAttackIndex + 1) % boss.attackConfigs.Length;
        }

        public void Update()
        {
            boss.HandleAttack();

            float elapsed = config.duration - boss.stateTimer;
            float percent = Mathf.Clamp01(elapsed / config.duration);

            if (percent >= config.damageStartPercent && percent <= config.damageEndPercent)
            {
                boss.DealSwordDamageContinuously(hitPlayersThisAttack);
            }
        }

        public void Exit()
        {
            boss.isLeaping = false;
            if (boss.visualRoot != null) boss.visualRoot.localPosition = boss.initialVisualLocalPos;
        }
    }

    private IEnumerator ExecuteAttackLeapClientRoutine(int attackIndex)
    {
        if (attackIndex < 0 || attackIndex >= attackConfigs.Length) yield break;
        AttackConfig config = attackConfigs[attackIndex];

        float startDelay = config.duration * config.leapStartPercent;
        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

        float leapDuration = config.duration * config.leapDurationPercent;
        float elapsed = 0f;

        while (elapsed < leapDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / leapDuration);
            if (visualRoot != null)
            {
                float yOffset = config.peakHeight * Mathf.Sin(Mathf.PI * progress);
                visualRoot.localPosition = initialVisualLocalPos + new Vector3(0, yOffset, 0);
            }
            yield return null;
        }

        if (visualRoot != null) visualRoot.localPosition = initialVisualLocalPos;

        // Rung camera chấn động khi đòn Nhaychemdat tiếp đất trên Client
        if (attackIndex == 0 || config.peakHeight > 2.0f)
        {
            CameraShakeHelper.ShakeAtPosition(transform.position, 0.7f, 1.3f, 40.0f);
        }
    }

    private class HitState : IEnemyState
    {
        private MiniBossAI boss;
        public HitState(MiniBossAI boss) { this.boss = boss; }
        public void Enter()
        {
            boss.stateTimer = boss.hitStaggerDuration;
        }
        public void Update() { boss.HandleHit(); }
        public void Exit() { }
    }

    private class EnrageState : IEnemyState
    {
        private MiniBossAI boss;
        public EnrageState(MiniBossAI boss) { this.boss = boss; }
        public void Enter()
        {
            boss.stateTimer = boss.enrageDuration;
            if (boss.isStandaloneMode && boss.anim != null)
            {
                boss.anim.SetTrigger(boss.enrageTrigger);
                boss.PlayEnrageVFX();
            }
        }
        public void Update() { boss.HandleEnrage(); }
        public void Exit() { }
    }

    private class DeadState : IEnemyState
    {
        private MiniBossAI boss;
        public DeadState(MiniBossAI boss) { this.boss = boss; }
        public void Enter()
        {
            boss.Die();
        }
        public void Update() { }
        public void Exit() { }
    }

    private class SwordRainState : IEnemyState
    {
        private MiniBossAI boss;
        public SwordRainState(MiniBossAI boss) { this.boss = boss; }

        public void Enter()
        {
            boss.TryLaunchSwordRainSkill();
            if (boss.targetPlayer != null) boss.ChangeState(MiniBossState.Chase);
            else boss.ChangeState(MiniBossState.Idle);
        }

        public void Update() { }
        public void Exit() { }
    }

    // ══════════════════════════════════════════════════════════
    //  SWORD RAIN OBJECT POOLING & LOGIC
    // ══════════════════════════════════════════════════════════

    private Queue<GameObject> swordPool = new Queue<GameObject>();
    private Queue<GameObject> warningPool = new Queue<GameObject>();

    public GameObject GetPooledSword(Vector3 spawnPos, Quaternion rotation)
    {
        GameObject sword = null;
        while (swordPool.Count > 0)
        {
            var candidate = swordPool.Dequeue();
            if (candidate != null)
            {
                sword = candidate;
                break;
            }
        }

        if (sword == null)
        {
            if (swordPrefab != null)
            {
                sword = Instantiate(swordPrefab);
            }
            else
            {
                // Tạo đại kiếm 3D phát sáng màu hoàng kim hiển thị cực rõ khi Inspector chưa gán Prefab
                sword = new GameObject("SpectralSummonedSword_MiniBoss");
                
                GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
                blade.name = "Blade";
                blade.transform.SetParent(sword.transform);
                blade.transform.localPosition = new Vector3(0, 1.2f, 0);
                blade.transform.localScale = new Vector3(0.35f, 2.8f, 0.12f);

                GameObject hilt = GameObject.CreatePrimitive(PrimitiveType.Cube);
                hilt.name = "Hilt";
                hilt.transform.SetParent(sword.transform);
                hilt.transform.localPosition = new Vector3(0, 0f, 0);
                hilt.transform.localScale = new Vector3(1.2f, 0.2f, 0.2f);

                GameObject pommel = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pommel.name = "Pommel";
                pommel.transform.SetParent(sword.transform);
                pommel.transform.localPosition = new Vector3(0, -0.3f, 0);
                pommel.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);

                Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default");
                Material goldMat = new Material(shader != null ? shader : Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply"));
                goldMat.color = new Color(1.0f, 0.75f, 0.1f, 1.0f); // Màu hoàng kim rực rỡ

                var renderers = sword.GetComponentsInChildren<Renderer>();
                foreach (var r in renderers) { if (r != null) r.material = goldMat; }
            }
        }

        // Bật hiển thị tất cả Renderer và ParticleSystem của thanh kiếm
        var allRenderers = sword.GetComponentsInChildren<Renderer>(true);
        foreach (var r in allRenderers) { if (r != null) r.enabled = true; }

        var allParticles = sword.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in allParticles) { if (ps != null) { ps.Clear(); ps.Play(); } }

        // Đảm bảo toàn bộ Collider đều là Trigger
        var allColliders = sword.GetComponentsInChildren<Collider>(true);
        foreach (var c in allColliders) { if (c != null) c.isTrigger = true; }

        var rootCol = sword.GetComponent<BoxCollider>();
        if (rootCol == null) rootCol = sword.AddComponent<BoxCollider>();
        rootCol.size = new Vector3(1.5f, 3.5f, 1.5f);
        rootCol.center = new Vector3(0f, 1.2f, 0f);
        rootCol.isTrigger = true;

        // Đảm bảo luôn có Rigidbody Kinematic ở Root để va chạm Trigger hoạt động chuẩn xác 100%
        var rb = sword.GetComponent<Rigidbody>();
        if (rb == null) rb = sword.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        sword.transform.position = spawnPos;
        sword.transform.rotation = rotation;
        sword.SetActive(true);
        return sword;
    }

    public void RecycleSword(GameObject sword)
    {
        if (sword == null) return;
        sword.SetActive(false);
        if (!swordPool.Contains(sword))
        {
            swordPool.Enqueue(sword);
        }
    }

    public GameObject GetPooledWarning(Vector3 spawnPos)
    {
        GameObject warning = null;
        while (warningPool.Count > 0)
        {
            var candidate = warningPool.Dequeue();
            if (candidate != null)
            {
                warning = candidate;
                break;
            }
        }

        if (warning == null)
        {
            if (warningDecalPrefab != null)
            {
                warning = Instantiate(warningDecalPrefab);
            }
            else
            {
                warning = new GameObject("ProceduralWarningRing_MiniBoss");
                warning.AddComponent<ProceduralWarningCircle>();
            }
        }

        warning.transform.position = spawnPos;
        warning.SetActive(true);
        return warning;
    }

    public void RecycleWarning(GameObject warning)
    {
        if (warning == null) return;
        warning.SetActive(false);
        if (!warningPool.Contains(warning))
        {
            warningPool.Enqueue(warning);
        }
    }

    [ClientRpc]
    private void TriggerSwordRainDropClientRpc(Vector3[] positions)
    {
        ExecuteSwordRainDropLocal(positions);
    }

    public void ExecuteSwordRainDropLocal(Vector3[] positions)
    {
        foreach (var pos in positions)
        {
            StartCoroutine(RoutineDropSwordAtPosition(pos));
        }
    }

    private IEnumerator RoutineDropSwordAtPosition(Vector3 groundPos)
    {
        Debug.Log($"[MiniBossAI] Bắt đầu dội chùm kiếm rơi tại {groundPos}");
        GameObject warning = GetPooledWarning(groundPos + Vector3.up * 0.05f);

        var flasher = warning.GetComponent<WarningDecalFlash>();
        if (flasher != null) flasher.StartFlashing(warningDuration);

        var ring = warning.GetComponent<ProceduralWarningCircle>();
        if (ring != null) ring.StartWarning(warningDuration, swordImpactRadius);

        yield return new WaitForSeconds(warningDuration);

        RecycleWarning(warning);

        // RƠI NHIỀU KIẾM VÀO CÙNG 1 CHỖ (Chùm 3 đến 5 thanh kiếm cắm chụm xuống cùng 1 vị trí)
        int swordsInCluster = Random.Range(3, 6);
        for (int k = 0; k < swordsInCluster; k++)
        {
            Vector3 offset = Vector3.zero;
            if (k > 0)
            {
                Vector2 r = Random.insideUnitCircle * Random.Range(0.2f, 0.65f);
                offset = new Vector3(r.x, 0f, r.y);
            }

            Vector3 targetDropPos = groundPos + offset;
            Vector3 skyPos = targetDropPos + Vector3.up * (22.0f + k * 1.5f);
            Quaternion rot = Quaternion.LookRotation(Vector3.down) * Quaternion.Euler(swordSpawnRotationOffset);

            GameObject sword = GetPooledSword(skyPos, rot);
            var ascProj = sword.GetComponent<AscendingSwordProjectile>();
            if (ascProj != null) ascProj.enabled = false;

            var proj = sword.GetComponent<FallingSwordProjectile>();
            if (proj == null) proj = sword.AddComponent<FallingSwordProjectile>();
            proj.enabled = true;

            proj.Initialize(this, targetDropPos, swordDropSpeed, swordDamage, swordImpactRadius, playerLayer);

            // Giãn cách cực ngắn 0.05s giữa các kiếm trong cùng 1 chùm để tạo hiệu ứng cắm phập phập phập liên hồi
            if (k < swordsInCluster - 1)
            {
                yield return new WaitForSeconds(0.05f);
            }
        }
    }

    public void PlaySwordImpactEffects(Vector3 impactPos)
    {
        CameraShakeHelper.ShakeAtPosition(impactPos, 0.6f, 1.2f, 40.0f);
        if (swordImpactVFX != null)
        {
            GameObject vfx = Instantiate(swordImpactVFX, impactPos, Quaternion.identity);
            vfx.transform.localScale = swordImpactVFX.transform.localScale;
            Destroy(vfx, 2.5f);
        }
        else
        {
#if UNITY_EDITOR
            GameObject fallbackVfx = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Vefects/Stylized AoE VFX/VFX/Fire/Particles/VFX_Fire_Area_01.prefab");
            if (fallbackVfx != null)
            {
                GameObject vfx = Instantiate(fallbackVfx, impactPos, Quaternion.identity);
                vfx.transform.localScale = fallbackVfx.transform.localScale;
                Destroy(vfx, 2.5f);
            }
#endif
        }

        if (swordImpactSFX != null)
        {
            AudioSource.PlayClipAtPoint(swordImpactSFX, impactPos, 1.0f);
        }
    }

    public void DealSwordImpactDamage(Vector3 impactPos, float damage, float radius, LayerMask layer)
    {
        bool auth = isStandaloneMode || !IsNetworkActive || (IsNetworkActive && IsServer);
        if (!auth) return;

        HashSet<Transform> hitRoots = new HashSet<Transform>();

        // 1. Quét theo khoảng cách trực tiếp tới toàn bộ Player đang chơi (Bảo đảm 100% trừ máu không phụ thuộc LayerMask/Collider)
        var allPlayers = GetAllActivePlayers();
        foreach (var p in allPlayers)
        {
            if (p != null && !IsPlayerDeadOrInvisible(p))
            {
                float dist = Vector3.Distance(impactPos, p.position);
                float xzDist = Vector2.Distance(new Vector2(impactPos.x, impactPos.z), new Vector2(p.position.x, p.position.z));
                float heightDiff = p.position.y - impactPos.y;
                if (dist <= radius + 0.8f || (xzDist <= radius && heightDiff >= -1.0f && heightDiff <= 3.5f))
                {
                    hitRoots.Add(p);
                    Vector3 knockbackDir = (p.position - impactPos).normalized + Vector3.up * 0.4f;
                    EnemyDamageHelper.DealDamage(p, damage, knockbackDir * 4f);
                    Debug.Log($"[MiniBossAI] Mưa Kiếm đánh trúng player: {p.name} trừ {damage} HP!");
                }
            }
        }

        // 2. Quét dự phòng thêm bằng Physics.OverlapSphere
        LayerMask mask = (layer.value == 0) ? ~0 : layer;
        Collider[] hits = Physics.OverlapSphere(impactPos, radius, mask, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            Transform root = GetPlayerRoot(hit.transform);
            if (root != null && !hitRoots.Contains(root))
            {
                hitRoots.Add(root);
                Vector3 knockbackDir = (root.position - impactPos).normalized + Vector3.up * 0.4f;
                EnemyDamageHelper.DealDamage(root, damage, knockbackDir * 4f);
                Debug.Log($"[MiniBossAI] Mưa Kiếm đánh trúng collider player: {root.name} trừ {damage} HP!");
            }
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        if (!isClone && AudioManager.Instance != null)
        {
            AudioManager.Instance.SetBossMusicActive(false, 1.0f);
        }
    }
}

public class AscendingSwordProjectile : MonoBehaviour
{
    private ISwordRainOwner bossOwner;
    private Vector3 flyDirection;
    private float flySpeed;
    private float maxDistance;
    private float currentDistance;
    private bool isFlying;

    public void Initialize(ISwordRainOwner owner, Vector3 startPos, Vector3 direction, float speed, float distance)
    {
        bossOwner = owner;
        transform.position = startPos;
        flyDirection = direction.normalized;
        flySpeed = speed;
        maxDistance = distance;
        currentDistance = 0f;
        isFlying = true;
        enabled = true;

        var fallProj = GetComponent<FallingSwordProjectile>();
        if (fallProj != null) fallProj.enabled = false;

        var renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers) { if (r != null) r.enabled = true; }

        var particles = GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in particles) { if (ps != null) { ps.Clear(); ps.Play(); } }
    }

    private void Update()
    {
        if (!isFlying) return;

        float step = flySpeed * Time.deltaTime;
        transform.position += flyDirection * step;
        currentDistance += step;

        if (currentDistance >= maxDistance)
        {
            isFlying = false;
            if (bossOwner != null)
            {
                bossOwner.RecycleSword(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}

public class CloneWorldHealthBarFallback : MonoBehaviour
{
    public MiniBossAI miniBoss;
    public RectTransform fillRect;
    private Camera mainCam;

    private void Update()
    {
        if (mainCam == null || !mainCam.gameObject.activeInHierarchy) mainCam = Camera.main;
        if (mainCam != null)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - mainCam.transform.position);
        }

        if (miniBoss != null && fillRect != null)
        {
            float hpRatio = Mathf.Clamp01(miniBoss.ActualCurrentHealth / Mathf.Max(miniBoss.maxHealth, 1f));
            fillRect.anchorMax = new Vector2(hpRatio, 1f);
        }
    }
}
