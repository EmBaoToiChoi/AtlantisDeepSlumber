using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Final Boss AI Script using FSM (Finite State Machine).
/// States: Sitting, JumpDown, Idle, Chase, Attack, Shockwave, Hit, Dead.
/// Features:
///   - Starts in Sitting state on the throne/chair.
///   - Transitions to JumpDown state when Rakan (MiniBossAI) dies, performing a parabolic jump to landingPoint.
///   - On landing, deals landing slam damage and knockback to nearby players.
///   - Natural movement (walk speed when close, run speed when far away).
///   - Alternate through 3 random phase 1 attacks: Left Punch, Right Punch, Swipe.
///   - Radial shockwave defensive skill triggered if any player gets too close, dealing stun (kicked stun) and knockback.
///   - Synchronizes health, states, and animator triggers across clients via Unity Netcode.
/// </summary>
public class FinalBossAI : NetworkBehaviour
{
    public enum FinalBossState { Sitting, JumpDown, Grow, SwordRain, FireSpew, FireBarrage, Idle, Chase, Attack, Shockwave, Hit, Dead }

    [Header("Miniboss Reference")]
    [Tooltip("Reference to the Boss AI (e.g. Silas) that must die before the final boss jumps down.")]
    public BossAI bossMiniboss;
    [Tooltip("Alternative reference to MiniBossAI (Rakan) if used.")]
    public MiniBossAI miniBoss;
    [Tooltip("If true, the boss will transition to JumpDown immediately without waiting for the miniboss.")]
    public bool startActiveWithoutMiniboss = false;

    [Header("Health Settings")]
    public float maxHealth = 1100f;
    public NetworkVariable<float> currentHealth = new NetworkVariable<float>(
        1100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Network State Sync")]
    public NetworkVariable<FinalBossState> currentState = new NetworkVariable<FinalBossState>(
        FinalBossState.Sitting, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> isBossActive = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> isHUDVisible = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Network Animation Sync Counters")]
    public NetworkVariable<float> netSpeed = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> attackCounter = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> attackTypeSync = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> shockwaveCounter = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> jumpDownCounter = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> landingSlamCounter = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> hitCounter = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> dieCounter = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> fireSpewCounter = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> fireSpewActiveCounter = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> growCounter = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> swordRainCounter = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> fireBarrageCounter = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<Vector3> netScale = new NetworkVariable<Vector3>(
        Vector3.one, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> netIsEnraged = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> netIsLastStand = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Standalone fallback variables
    private float localHealth;
    private FinalBossState localState = FinalBossState.Sitting;
    private bool localIsBossActive = false;
    private bool localIsHUDVisible = false;
    private bool isStandaloneMode = false;
    private bool IsNetworkActive => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

    public FinalBossState CurrentStateValue
    {
        get => isStandaloneMode ? localState : currentState.Value;
        set { if (isStandaloneMode) localState = value; else currentState.Value = value; }
    }

    public float ActualCurrentHealth => (isStandaloneMode || !IsSpawned) ? localHealth : currentHealth.Value;
    public bool IsBossActive => isStandaloneMode ? localIsBossActive : isBossActive.Value;
    public bool IsHUDVisible => isStandaloneMode ? localIsHUDVisible : isHUDVisible.Value;
    public bool IsDead => CurrentStateValue == FinalBossState.Dead;

    public void SetHUDVisible(bool visible)
    {
        bool auth = isStandaloneMode || (IsNetworkActive && IsServer);
        if (!auth) return;

        if (isStandaloneMode)
            localIsHUDVisible = visible;
        else
            isHUDVisible.Value = visible;
    }

    [Header("Components")]
    public NavMeshAgent agent;
    public Animator anim;
    [Tooltip("The root of the visual mesh child. Offsets will keep NavMesh tracking on the ground.")]
    public Transform visualRoot;
    [Tooltip("Raycast eye height target for detecting players")]
    public Transform eyeTransform;

    [Header("Intro / Jump Down Settings")]
    [Tooltip("Transform of the landing spot on the ground after jumping down from the chair.")]
    public Transform landingPoint;
    public float jumpDownDuration = 2.0f;
    public float jumpDownPeakHeight = 4.0f;
    public float landingSlamRadius = 6.0f;
    public float landingSlamDamage = 40f;
    public float landingSlamKnockback = 18f;
    public GameObject landingSlamVFX;
    public AudioClip landingSlamSFX;

    [Header("Enrage / Grow (Gồng & Phóng To) Settings")]
    public float growDuration = 3.0f;
    public float growScaleMultiplier = 3.0f;
    public string growTriggerParam = "Shockwave";
    public GameObject growVFX;
    public AudioClip growSFX;

    [Header("Sword Rain (Mưa Kiếm 10s) Settings")]
    public GameObject swordPrefab;
    public GameObject warningDecalPrefab;
    public GameObject swordImpactVFX;
    public AudioClip swordImpactSFX;
    public float swordRainDuration = 10.0f;
    public float swordSpawnInterval = 0.4f;
    public float warningDuration = 0.7f;
    public float swordDropSpeed = 35.0f;
    public float swordDamage = 5.0f;
    public float swordImpactRadius = 2.0f;
    public string swordRainTriggerParam = "AttackCombo";
    public Vector3 swordSpawnRotationOffset = new Vector3(90f, 0f, 0f); // Xoay bù để kiếm cắm thẳng xuống

    [Header("Fire Barrage (Chưởng Đốm Lửa) Settings")]
    public GameObject fireBarragePrefab;
    public GameObject fireBarrageWarningDecalPrefab; // Cảnh báo dưới sàn cho cầu lửa
    public GameObject fireBarrageImpactVFX;
    public AudioClip fireBarrageImpactSFX;
    public float fireBarrageDuration = 6.0f;
    public float fireBarrageInterval = 0.5f;
    public float fireBarrageWarningDuration = 0.7f;
    public float fireBarrageDropSpeed = 30.0f;
    public float fireBarrageDamage = 20.0f;
    public float fireBarrageImpactRadius = 2.5f;
    public string fireBarrageTriggerParam = "AttackCombo";

    [Header("Movement Speeds")]
    public float walkSpeed = 2.2f;
    public float runSpeed = 6.0f;
    [Tooltip("Distance threshold: run when far, walk when close to feel natural.")]
    public float runDistanceThreshold = 5.0f;

    [Header("AI Vision & Attack Ranges")]
    public float sightRange = 50f;
    public float fieldOfView = 360f;
    [Tooltip("Tầm đánh tầm xa (bắn 3 vệt chém)")]
    public float attackRange = 35.0f;
    public float attackCooldown = 1.8f;
    private float attackCooldownTimer;

    [Header("Shockwave (Gồng Hất Tung) Settings")]
    [Tooltip("Trigger range for the defensive shockwave skill.")]
    public float shockwaveTriggerRange = 3.5f;
    public float shockwaveCooldown = 10f;
    public float shockwaveRadius = 6f;
    public float shockwaveDamage = 45f;
    public float shockwaveKnockback = 20f;
    public float shockwaveStunDuration = 2.0f;
    public float shockwaveDuration = 1.8f;
    public float shockwaveDamageDelay = 0.5f;
    public GameObject shockwaveVFX;
    public AudioClip shockwaveSFX;
    private float shockwaveCooldownTimer;

    [Header("Expanding Shockwave Ring (Sóng Xung Kích Hất Tung -5 HP) Settings")]
    public float shockwaveWaveSpeed = 14.0f;
    public float shockwaveWaveMaxRadius = 16.0f;
    public float shockwaveWaveDamage = 5.0f;
    public float shockwaveWaveKnockup = 9.0f;

    [Header("Ranged Projectile Settings (Đòn Đánh Tầm Xa)")]
    [Tooltip("Prefab đạn tầm xa (Slash_B_vertical Variant 1)")]
    public GameObject rangedProjectilePrefab;
    [Tooltip("Vị trí xuất phát của chưởng đạn (nếu để trống sẽ tự động lấy trước ngực Boss)")]
    public Transform projectileSpawnPoint;
    [Tooltip("Tốc độ bay của chưởng đạn")]
    public float projectileSpeed = 18.0f;
    [Tooltip("Sát thương của 1 chưởng đạn (mỗi tia trúng -5 HP, dính cả 3 tia -15 HP)")]
    public float projectileDamage = 5.0f;
    [Tooltip("Bù góc xoay cho VFX nếu vệt chém bị lệch hướng (X, Y, Z)")]
    public Vector3 projectileRotationOffset = Vector3.zero;

    [Header("Phase 2 & Enrage Settings")]
    public bool isEnraged = false;
    public bool isLastStand = false;

    [Header("Fire Spew (Phun Lửa) Settings")]
    public float fireSpewInterval = 10f;
    public float fireSpewWindupDuration = 1.0f;
    public float fireSpewActiveDuration = 1.5f;
    public float fireSpewRadius = 7f;
    public float fireSpewDamage = 35f;
    public float fireSpewKnockback = 12f;
    public GameObject fireSpewVFX;
    public AudioClip fireSpewSFX;
    public string fireSpewTriggerParam = "FireSpew";
    public float fireSpewCooldownTimer;

    [Header("Fire Barrage Cooldown")]
    public float fireBarrageCooldown = 12f;
    public float fireBarrageCooldownTimer;

    [Header("Sword Rain Cooldown")]
    public float swordRainCooldown = 10f;
    public float swordRainCooldownTimer;

    [Header("Weapon & Hand Detection Settings")]
    public Transform leftHandBase;
    public Transform leftHandTip;
    public Transform rightHandBase;
    public Transform rightHandTip;
    public float attackThickness = 0.5f;
    public LayerMask playerLayer;
    public LayerMask obstacleLayer;

    [Header("Animator Param Names")]
    public string speedParam = "Speed";                 // Float: 0 = Idle, 0.5 = Walk, 1.0 = Run
    public string sitTrigger = "Sit";                   // Trigger name to play sitting animation
    public bool useSittingBool = false;                 // If checked, sets 'IsSitting' boolean parameter as well
    public string jumpDownTriggerParam = "JumpDown";     // Trigger for jump down from chair
    public string[] attackTriggers = new string[] { "LeftPunch", "RightPunch", "Swipe" }; // Triggers for the 3 attack animations
    public string shockwaveTriggerParam = "Shockwave";  // Trigger for radial knockback stomp/roar
    public string hitTriggerParam = "Hit";               // Trigger for stagger
    public string dieTriggerParam = "Die";               // Trigger for death

    [System.Serializable]
    public struct FinalBossAttackConfig
    {
        public float duration;             // Duration of the attack state
        public float damageStartPercent;   // Start of continuous damage window (0.0 to 1.0)
        public float damageEndPercent;     // End of continuous damage window (0.0 to 1.0)
        public float forwardSpeed;         // Small glide/forward movement during punch
        public float glideDuration;        // Glide duration in seconds
    }

    [Header("Attack Configs (0: LeftPunch, 1: RightPunch, 2: Swipe)")]
    public FinalBossAttackConfig[] attackConfigs = new FinalBossAttackConfig[]
    {
        new FinalBossAttackConfig { duration = 1.2f, damageStartPercent = 0.3f, damageEndPercent = 0.6f, forwardSpeed = 3f, glideDuration = 0.4f },
        new FinalBossAttackConfig { duration = 1.2f, damageStartPercent = 0.3f, damageEndPercent = 0.6f, forwardSpeed = 3f, glideDuration = 0.4f },
        new FinalBossAttackConfig { duration = 1.6f, damageStartPercent = 0.25f, damageEndPercent = 0.65f, forwardSpeed = 4f, glideDuration = 0.6f }
    };

    private IEnemyState currentFSMState;
    private SittingState stateSitting;
    private JumpDownState stateJumpDown;
    private IdleState stateIdle;
    private ChaseState stateChase;
    private AttackState stateAttack;
    private ShockwaveState stateShockwave;
    private FireSpewState stateFireSpew;
    private GrowState stateGrow;
    private SwordRainState stateSwordRain;
    private FireBarrageState stateFireBarrage;
    private HitState stateHit;
    private DeadState stateDead;

    private Transform targetPlayer;
    private float stateTimer;
    private float hitStaggerDuration = 0.6f;
    private int currentAttackIndex = 0;

    // Wander patrol state
    private bool hasWanderDestination;
    private Vector3 wanderDestination;
    private float wanderWaitTimer;

    // Attack glides
    private bool isGliding = false;
    private float glideTimer = 0f;
    private float currentGlideDuration = 0f;
    private float currentGlideSpeed = 0f;

    // AI scan rate
    private float scanTimer;
    private float scanInterval = 0.15f;

    private bool AgentReady => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;

    private void Awake()
    {
        gameObject.tag = "Enemy";
        if (anim == null) anim = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);

        // Auto config NetworkAnimator if present
        var na = GetComponent<Unity.Netcode.Components.NetworkAnimator>();
        if (na != null && anim != null)
        {
            na.Animator = anim;
        }

        // Initialize state instances for FSM
        stateSitting = new SittingState(this);
        stateJumpDown = new JumpDownState(this);
        stateIdle = new IdleState(this);
        stateChase = new ChaseState(this);
        stateAttack = new AttackState(this);
        stateShockwave = new ShockwaveState(this);
        stateFireSpew = new FireSpewState(this);
        stateGrow = new GrowState(this);
        stateSwordRain = new SwordRainState(this);
        stateFireBarrage = new FireBarrageState(this);
        stateHit = new HitState(this);
        localHealth = maxHealth;
        stateDead = new DeadState(this);
    }

    private void Start()
    {
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
        localHealth = maxHealth;
        localIsBossActive = false;
        localIsHUDVisible = startActiveWithoutMiniboss;
        fireSpewCooldownTimer = fireSpewInterval;
        SnapToNavMesh();
        ApplySpeedAnim(0f);
        ChangeState(FinalBossState.Sitting);
    }

    public override void OnNetworkSpawn()
    {
        isStandaloneMode = false;

        // Register network synchronization handlers
        netSpeed.OnValueChanged += (_, v) => ApplySpeedAnim(v);
        attackCounter.OnValueChanged += (_, _) => {
            if (anim != null && attackTypeSync.Value >= 0 && attackTypeSync.Value < attackTriggers.Length)
                anim.SetTrigger(attackTriggers[attackTypeSync.Value]);
        };
        shockwaveCounter.OnValueChanged += (_, _) => {
            if (anim != null) anim.SetTrigger(shockwaveTriggerParam);
            PlayShockwaveVFX();
        };
        jumpDownCounter.OnValueChanged += (_, _) => {
            if (anim != null) anim.SetTrigger(jumpDownTriggerParam);
        };
        landingSlamCounter.OnValueChanged += (_, _) => PlayLandingSlamVFX();
        hitCounter.OnValueChanged += (_, _) => { if (anim != null) anim.SetTrigger(hitTriggerParam); };
        dieCounter.OnValueChanged += (_, _) => { if (anim != null) anim.SetTrigger(dieTriggerParam); };
        fireSpewCounter.OnValueChanged += (_, _) => {
            if (anim != null) anim.SetTrigger(fireSpewTriggerParam);
        };
        fireSpewActiveCounter.OnValueChanged += (_, _) => PlayFireSpewVFX();
        growCounter.OnValueChanged += (_, _) => {
            if (anim != null) anim.SetTrigger(growTriggerParam);
            PlayGrowVFX();
        };
        swordRainCounter.OnValueChanged += (_, _) => {
            if (anim != null) anim.SetTrigger(swordRainTriggerParam);
        };
        fireBarrageCounter.OnValueChanged += (_, _) => {
            if (anim != null) anim.SetTrigger(fireBarrageTriggerParam);
        };
        netScale.OnValueChanged += (_, newScale) => transform.localScale = newScale;
        netIsEnraged.OnValueChanged += (_, enraged) => isEnraged = enraged;
        netIsLastStand.OnValueChanged += (_, lastStand) => {
            isLastStand = lastStand;
            if (lastStand) attackCooldown = 1.35f;
        };
        currentHealth.OnValueChanged += OnHealthNetChanged;

        ApplySpeedAnim(netSpeed.Value);

        if (IsServer)
        {
            currentHealth.Value = maxHealth;
            netScale.Value = transform.localScale;
            netIsEnraged.Value = false;
            netIsLastStand.Value = false;
            isHUDVisible.Value = startActiveWithoutMiniboss;
            fireSpewCooldownTimer = fireSpewInterval;
            SnapToNavMesh();
            ChangeState(FinalBossState.Sitting);
        }
        else
        {
            if (agent != null) agent.enabled = false;
        }
    }

    public override void OnNetworkDespawn()
    {
        netSpeed.OnValueChanged -= (_, v) => ApplySpeedAnim(v);
        attackCounter.OnValueChanged -= (_, _) => {
            if (anim != null && attackTypeSync.Value >= 0 && attackTypeSync.Value < attackTriggers.Length)
                anim.SetTrigger(attackTriggers[attackTypeSync.Value]);
        };
        shockwaveCounter.OnValueChanged -= (_, _) => {
            if (anim != null) anim.SetTrigger(shockwaveTriggerParam);
            PlayShockwaveVFX();
        };
        jumpDownCounter.OnValueChanged -= (_, _) => {
            if (anim != null) anim.SetTrigger(jumpDownTriggerParam);
        };
        landingSlamCounter.OnValueChanged -= (_, _) => PlayLandingSlamVFX();
        hitCounter.OnValueChanged -= (_, _) => { if (anim != null) anim.SetTrigger(hitTriggerParam); };
        dieCounter.OnValueChanged -= (_, _) => { if (anim != null) anim.SetTrigger(dieTriggerParam); };
        fireSpewCounter.OnValueChanged -= (_, _) => {
            if (anim != null) anim.SetTrigger(fireSpewTriggerParam);
        };
        fireSpewActiveCounter.OnValueChanged -= (_, _) => PlayFireSpewVFX();
        growCounter.OnValueChanged -= (_, _) => {
            if (anim != null) anim.SetTrigger(growTriggerParam);
            PlayGrowVFX();
        };
        swordRainCounter.OnValueChanged -= (_, _) => {
            if (anim != null) anim.SetTrigger(swordRainTriggerParam);
        };
        fireBarrageCounter.OnValueChanged -= (_, _) => {
            if (anim != null) anim.SetTrigger(fireBarrageTriggerParam);
        };
        netScale.OnValueChanged -= (_, newScale) => transform.localScale = newScale;
        netIsEnraged.OnValueChanged -= (_, enraged) => isEnraged = enraged;
        netIsLastStand.OnValueChanged -= (_, lastStand) => { isLastStand = lastStand; };
        currentHealth.OnValueChanged -= OnHealthNetChanged;
    }

    private void OnHealthNetChanged(float oldVal, float newVal)
    {
        localHealth = newVal;
        float diff = oldVal - newVal;
        if (diff > 0)
        {
            EnemyDamageEffectHelper.PlayDamageEffects(gameObject, diff);
        }

        // Tự động đồng bộ cờ Phase 2 và Cuồng Bạo trên tất cả Client khi máu giảm
        if (newVal <= 600f) isEnraged = true;
        if (newVal <= 100f && newVal > 0f)
        {
            isLastStand = true;
            attackCooldown = 1.35f;
        }
    }

    [ContextMenu("Trigger Jump Down Now")]
    public void TriggerJumpDownForce()
    {
        bool auth = isStandaloneMode || (IsNetworkActive && IsServer);
        if (!auth) return;

        if (CurrentStateValue == FinalBossState.Sitting)
        {
            ChangeState(FinalBossState.JumpDown);
        }
    }

    public void ActivateBoss()
    {
        bool auth = isStandaloneMode || (IsNetworkActive && IsServer);
        if (!auth) return;

        if (isStandaloneMode)
            localIsBossActive = true;
        else
            isBossActive.Value = true;

        Debug.Log("[FinalBossAI] Final Boss has been activated! Combat start!");
    }

    [ServerRpc(RequireOwnership = false)]
    public void TakeDamageServerRpc(float damageAmount)
    {
        TakeDamage(damageAmount);
    }

    public void TakeDamage(float damage)
    {
        // Loại bỏ việc chặn sát thương khi đang cast chiêu SwordRain/FireBarrage để đảm bảo chém là LUÔN MÔT 100% TRỪ MÁU!
        if (IsDead || CurrentStateValue == FinalBossState.Sitting || CurrentStateValue == FinalBossState.JumpDown || CurrentStateValue == FinalBossState.Grow) return;

        // Nếu là Client đánh Boss trên mạng -> Gửi ServerRpc để Server trừ máu chuẩn xác 100%
        if (!isStandaloneMode && IsSpawned && !IsServer)
        {
            TakeDamageServerRpc(damage);
            return;
        }

        localHealth = Mathf.Max(0f, localHealth - damage);
        if (!isStandaloneMode && IsSpawned && IsServer)
        {
            currentHealth.Value = Mathf.Max(0f, currentHealth.Value - damage);
            localHealth = currentHealth.Value;
        }

        EnemyDamageEffectHelper.PlayDamageEffects(gameObject, damage);

        bool isAuth = isStandaloneMode || (IsSpawned && IsServer) || !IsSpawned;
        if (!isAuth) return;

        float activeHp = ActualCurrentHealth;
        if (activeHp <= 0f)
        {
            ChangeState(FinalBossState.Dead);
            return;
        }

        // CHUYỂN PHASE 2 KHI MÁU XUỐNG DƯỚI/BẰNG 600 HP (PHASE 1 CÓ 500 HP, PHASE 2 CÓ 600 HP -> MAX 1100 HP) -> GẦM THÉT VÀ PHÓNG TO!
        if (!isEnraged && activeHp <= 600f && CurrentStateValue != FinalBossState.Grow)
        {
            ChangeState(FinalBossState.Grow);
            return;
        }

        // TRẠNG THÁI CUỒNG BẠO CUỐI TRẬN (LAST STAND) KHI MÁU <= 100 HP: TĂNG TỐC ĐÁNH +25% VÀ BẮN 5 VỆT CHÉM QUẠT RỘNG!
        if (!isLastStand && activeHp <= 100f && activeHp > 0f)
        {
            isLastStand = true;
            attackCooldown = 1.35f; // Tăng +25% tốc độ ra chiêu!
            Debug.Log("[FinalBossAI] ===> KING ATLANTIS VÀO TRẠNG THÁI CUỒNG BẠO CUỐI TRẬN (<=100 HP)! MẮT RỰC ĐỎ, TỐC ĐỘ +25%, BẮN 5 VỆT CHÉM QUẠT RỘNG!");
        }

        // Trigger stagger hit animation if not attacking/roaring/spewing/growing/raining/barraging/dead/already hit
        if (CurrentStateValue != FinalBossState.Attack && CurrentStateValue != FinalBossState.Shockwave && CurrentStateValue != FinalBossState.FireSpew && CurrentStateValue != FinalBossState.Grow && CurrentStateValue != FinalBossState.SwordRain && CurrentStateValue != FinalBossState.FireBarrage && CurrentStateValue != FinalBossState.Dead && CurrentStateValue != FinalBossState.Hit)
        {
            ChangeState(FinalBossState.Hit);
        }
    }

    public void ApplyStun(float duration)
    {
        // Stun logic can be added here if desired, or we can trigger hit stagger
        TakeDamage(0f);
    }

    private void Update()
    {
#if UNITY_EDITOR
        // Press keyboard key 9 during play mode to instantly force the boss to jump down and fight!
        if (Input.GetKeyDown(KeyCode.Alpha9))
        {
            TriggerJumpDownForce();
        }
        // Press keyboard key K during play mode to force fire the 3 slash projectiles immediately for debug testing!
        if (Input.GetKeyDown(KeyCode.K))
        {
            Debug.Log("[FinalBossAI] PHÍM K ĐƯỢC NHẤN: BẮN THỬ 3 VỆT CHÉM SLASH VFX NGAY LẬP TỨC!");
            SpawnRangedProjectile();
        }
#endif
        bool aiAuth = isStandaloneMode || (IsNetworkActive && IsServer);
        if (!aiAuth) return;

        if (agent != null && agent.isActiveAndEnabled && !agent.isOnNavMesh) SnapToNavMesh();

        // Wall collision resolver (stops boss passing through solid walls)
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh && agent.velocity.sqrMagnitude > 0.01f)
        {
            Vector3 rayOrigin = transform.position + Vector3.up * 1.0f;
            Vector3 moveDir = agent.velocity.normalized;
            if (Physics.Raycast(rayOrigin, moveDir, out RaycastHit hit, 0.8f))
            {
                if (!hit.collider.CompareTag("Player") && !hit.collider.CompareTag("Enemy") && hit.collider.gameObject.layer != LayerMask.NameToLayer("Enemy") && !hit.collider.isTrigger)
                {
                    Vector3 pushBack = hit.normal * 0.15f;
                    agent.Warp(transform.position + pushBack);
                }
            }
        }

        // Update cooldowns
        if (attackCooldownTimer > 0) attackCooldownTimer -= Time.deltaTime;
        if (shockwaveCooldownTimer > 0) shockwaveCooldownTimer -= Time.deltaTime;
        if (IsBossActive && !IsDead && CurrentStateValue != FinalBossState.Sitting && CurrentStateValue != FinalBossState.JumpDown)
        {
            if (fireSpewCooldownTimer > 0) fireSpewCooldownTimer -= Time.deltaTime;
            if (fireBarrageCooldownTimer > 0) fireBarrageCooldownTimer -= Time.deltaTime;
            if (swordRainCooldownTimer > 0) swordRainCooldownTimer -= Time.deltaTime;
        }

        // Perform target scans at intervals
        scanTimer -= Time.deltaTime;
        if (scanTimer <= 0)
        {
            scanTimer = scanInterval;
            DetectAndSwitchTarget();
        }

        // Update FSM state
        if (currentFSMState != null)
        {
            currentFSMState.Update();
        }
    }

    private void ChangeState(FinalBossState newState)
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
            case FinalBossState.Sitting:
                currentFSMState = stateSitting;
                break;
            case FinalBossState.JumpDown:
                currentFSMState = stateJumpDown;
                break;
            case FinalBossState.Idle:
                currentFSMState = stateIdle;
                break;
            case FinalBossState.Chase:
                currentFSMState = stateChase;
                break;
            case FinalBossState.Attack:
                currentFSMState = stateAttack;
                break;
            case FinalBossState.Shockwave:
                currentFSMState = stateShockwave;
                break;
            case FinalBossState.Hit:
                currentFSMState = stateHit;
                break;
            case FinalBossState.FireSpew:
                currentFSMState = stateFireSpew;
                break;
            case FinalBossState.Grow:
                currentFSMState = stateGrow;
                break;
            case FinalBossState.SwordRain:
                currentFSMState = stateSwordRain;
                break;
            case FinalBossState.FireBarrage:
                currentFSMState = stateFireBarrage;
                break;
            case FinalBossState.Dead:
                currentFSMState = stateDead;
                break;
        }

        if (currentFSMState != null)
        {
            currentFSMState.Enter();
        }
    }

    // ══════════════════════════════════════════════════════════
    //  LANDING SLAM & SHOCKWAVE TRIGGERS
    // ══════════════════════════════════════════════════════════

    public void TriggerLandingSlam()
    {
        if (!isStandaloneMode && IsServer)
        {
            landingSlamCounter.Value++;
        }
        else if (isStandaloneMode)
        {
            PlayLandingSlamVFX();
        }

        bool auth = isStandaloneMode || (IsNetworkActive && IsServer);
        if (auth)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, landingSlamRadius, playerLayer);
            HashSet<Transform> damagedRoots = new HashSet<Transform>();

            foreach (var hit in hits)
            {
                Transform root = GetPlayerRoot(hit.transform);
                if (root != null && !damagedRoots.Contains(root))
                {
                    damagedRoots.Add(root);
                    Vector3 knockbackDir = (root.position - transform.position);
                    knockbackDir.y = 0.5f; // Lift
                    Vector3 force = knockbackDir.normalized * landingSlamKnockback;

                    EnemyDamageHelper.DealDamage(root, landingSlamDamage, force);
                    Debug.Log($"[FinalBossAI] Slam hit player: {root.name}");
                }
            }
        }
    }

    private void PlayLandingSlamVFX()
    {
        Vector3 spawnPos = transform.position;
        if (landingSlamVFX != null)
        {
            GameObject vfx = Instantiate(landingSlamVFX, spawnPos, Quaternion.identity);
            Destroy(vfx, 4f);
        }
        if (landingSlamSFX != null)
        {
            AudioSource.PlayClipAtPoint(landingSlamSFX, spawnPos, 1.0f);
        }
    }

    private bool CheckShockwaveProximity()
    {
        if (shockwaveCooldownTimer > 0) return false;

        var activePlayers = GetAllActivePlayers();
        foreach (var p in activePlayers)
        {
            if (p == null || IsPlayerDeadOrInvisible(p)) continue;

            float dist = Vector3.Distance(transform.position, p.position);
            if (dist <= shockwaveTriggerRange)
            {
                ChangeState(FinalBossState.Shockwave);
                return true;
            }
        }

        return false;
    }

    public void TriggerShockwaveDamage()
    {
        bool auth = isStandaloneMode || (IsNetworkActive && IsServer);
        if (!auth) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, shockwaveRadius, playerLayer);
        HashSet<Transform> hitRoots = new HashSet<Transform>();
        var list = new List<Transform>();

        foreach (var hit in hits)
        {
            Transform root = GetPlayerRoot(hit.transform);
            if (root != null && !hitRoots.Contains(root))
            {
                hitRoots.Add(root);
                list.Add(root);
            }
        }

        // Limit to max 4 players
        int hitCount = Mathf.Min(list.Count, 4);
        for (int i = 0; i < hitCount; i++)
        {
            Transform root = list[i];
            Vector3 knockbackDir = (root.position - transform.position);
            knockbackDir.y = 0.5f; // Lift
            Vector3 force = knockbackDir.normalized * shockwaveKnockback;

            // Deal kick damage and trigger knocked stun animation
            EnemyDamageHelper.DealKickDamageWithStun(root, shockwaveDamage, force, shockwaveStunDuration);
            Debug.Log($"[FinalBossAI] Shockwave hit player: {root.name}");
        }
    }

    public void SpawnRangedProjectile()
    {
        if (rangedProjectilePrefab == null)
        {
            Debug.LogWarning("[FinalBossAI] Ô 'Ranged Projectile Prefab' chưa được gán trong Inspector trên King Atlantis!");
            return;
        }

        // Căn chỉnh điểm ngắm đúng tầm ngực/bụng Player (0.7m Y height)
        Vector3 targetPos = targetPlayer != null ? targetPlayer.position + Vector3.up * 0.7f : transform.position + transform.forward * 10f + Vector3.up * 0.8f;

        // Căn chỉnh điểm xuất phát phù hợp theo độ phồng to của Boss
        float scaleY = transform.localScale.y;
        Vector3 spawnPos = projectileSpawnPoint != null ? projectileSpawnPoint.position : transform.position + Vector3.up * (1.2f * scaleY) + transform.forward * (1.0f * scaleY);

        Vector3 centerDir = (targetPos - spawnPos).normalized;
        if (centerDir.sqrMagnitude < 0.01f) centerDir = transform.forward;

        if (!isStandaloneMode && IsSpawned)
        {
            if (IsServer)
            {
                SpawnRangedProjectileClientRpc(spawnPos, centerDir);
            }
        }
        else
        {
            SpawnRangedProjectileLocally(spawnPos, centerDir);
        }
    }

    [ClientRpc]
    private void SpawnRangedProjectileClientRpc(Vector3 spawnPos, Vector3 centerDir)
    {
        SpawnRangedProjectileLocally(spawnPos, centerDir);
    }

    private void SpawnRangedProjectileLocally(Vector3 spawnPos, Vector3 centerDir)
    {
        if (rangedProjectilePrefab == null) return;

        // Bình thường bắn 3 vệt chém (0°, -15°, 15°). Khi Cuồng Bạo (<=100 HP) bắn hẳn 5 vệt chém quạt rộng!
        float[] angles = isLastStand ? new float[] { 0f, -15f, 15f, -30f, 30f } : new float[] { 0f, -15f, 15f };

        foreach (float angle in angles)
        {
            Vector3 shootDir = Quaternion.Euler(0, angle, 0) * centerDir;
            Quaternion rot = Quaternion.LookRotation(shootDir) * Quaternion.Euler(projectileRotationOffset);

            // Vẽ tia cảnh báo đường bắn đỏ rực trong Scene view
            Debug.DrawRay(spawnPos, shootDir * 25f, Color.red, 3.0f);

            GameObject proj = Instantiate(rangedProjectilePrefab, spawnPos, rot);
            proj.transform.localScale = rangedProjectilePrefab.transform.localScale;

            var comp = proj.GetComponent<FinalBossProjectile>() ?? proj.AddComponent<FinalBossProjectile>();
            comp.Initialize(shootDir, projectileSpeed, projectileDamage, transform);
        }

        Debug.Log($"[FinalBossAI] ===> XUẤT VIỆN BẮN {angles.Length} VỆT CHÉM SLASH VFX '{rangedProjectilePrefab.name}' THÀNH CÔNG từ vị trí {spawnPos}!");
    }

    public Transform GetPlayerRootPublic(Transform t)
    {
        return GetPlayerRoot(t);
    }

    private void PlayShockwaveVFX()
    {
        Vector3 spawnPos = transform.position + Vector3.up * 0.1f;
        if (shockwaveVFX != null)
        {
            GameObject vfx = Instantiate(shockwaveVFX, spawnPos, Quaternion.identity);
            Destroy(vfx, 4f);
        }
        if (shockwaveSFX != null)
        {
            AudioSource.PlayClipAtPoint(shockwaveSFX, spawnPos, 1.0f);
        }

        // Tạo sóng xung kích mở rộng hất tung người chơi & -5 HP
        GameObject waveObj = new GameObject("ExpandingShockwaveRing");
        waveObj.transform.position = transform.position;
        var waveComp = waveObj.AddComponent<ExpandingShockwaveRing>();
        waveComp.Initialize(this, shockwaveWaveMaxRadius, shockwaveWaveSpeed, shockwaveWaveDamage, shockwaveWaveKnockup, playerLayer);
    }

    // ══════════════════════════════════════════════════════════
    //  DAMAGE SWEEPS (FISTS / SWIPES)
    // ══════════════════════════════════════════════════════════

    public void DealFistDamageContinuously(HashSet<Transform> hitThisAttack, int attackIdx)
    {
        Transform baseT = null;
        Transform tipT = null;

        if (attackIdx == 0) // Left Punch
        {
            baseT = leftHandBase;
            tipT = leftHandTip;
        }
        else if (attackIdx == 1) // Right Punch
        {
            baseT = rightHandBase;
            tipT = rightHandTip;
        }
        else // Swipe (Attack 2) - checks right hand as base or fallback
        {
            baseT = rightHandBase != null ? rightHandBase : leftHandBase;
            tipT = rightHandTip != null ? rightHandTip : leftHandTip;
        }

        Vector3 kbDir = transform.forward;
        Vector3 finalKnockback = kbDir * 10f; // punching knockback
        float finalDamage = 5f; // Đập/cào tay cận chiến -5 HP

        if (baseT != null && tipT != null)
        {
            Vector3 start = baseT.position;
            Vector3 end = tipT.position;
            Vector3 dir = (end - start).normalized;
            float dist = Vector3.Distance(start, end);

            RaycastHit[] hits = Physics.SphereCastAll(start, attackThickness, dir, dist, playerLayer);
            foreach (var hit in hits)
            {
                Transform root = GetPlayerRoot(hit.collider.transform);
                if (root != null && !hitThisAttack.Contains(root))
                {
                    hitThisAttack.Add(root);
                    EnemyDamageHelper.DealDamage(root, finalDamage, finalKnockback);
                    Debug.Log($"[FinalBossAI] Fist hit on: {root.name}, damage={finalDamage}");
                }
            }
        }
        else
        {
            // Fallback: SphereCast cận chiến ngay trước mặt Boss (2.5m)
            Vector3 origin = transform.position + Vector3.up * 1.2f;
            RaycastHit[] hits = Physics.SphereCastAll(origin, attackThickness * 1.5f, transform.forward, 2.5f, playerLayer);
            foreach (var hit in hits)
            {
                Transform root = GetPlayerRoot(hit.collider.transform);
                if (root != null && !hitThisAttack.Contains(root))
                {
                    hitThisAttack.Add(root);
                    EnemyDamageHelper.DealDamage(root, finalDamage, finalKnockback);
                    Debug.Log($"[FinalBossAI] Fist fallback hit on: {root.name}, damage={finalDamage}");
                }
            }
        }
    }

    // ══════════════════════════════════════════════════════════
    //  TARGET DETECTION & HELPERS
    // ══════════════════════════════════════════════════════════

    private List<Transform> GetAllActivePlayers()
    {
        var list = new List<Transform>();

        // 1. Tag lookup
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (var p in players)
        {
            if (p != null && !list.Contains(p.transform))
            {
                list.Add(p.transform);
            }
        }

        // 2. Character components lookup (Leo, Arthur, Elena, Maya)
        var leos = FindObjectsByType<LeoPlayer>(FindObjectsSortMode.None);
        foreach (var p in leos) { if (p != null && !list.Contains(p.transform)) list.Add(p.transform); }

        var arthurs = FindObjectsByType<ArthurPlayer>(FindObjectsSortMode.None);
        foreach (var p in arthurs) { if (p != null && !list.Contains(p.transform)) list.Add(p.transform); }

        var elenas = FindObjectsByType<ElenaPlayer>(FindObjectsSortMode.None);
        foreach (var p in elenas) { if (p != null && !list.Contains(p.transform)) list.Add(p.transform); }

        var mayas = FindObjectsByType<MayaPlayer>(FindObjectsSortMode.None);
        foreach (var p in mayas) { if (p != null && !list.Contains(p.transform)) list.Add(p.transform); }

        // 3. Fallback: PlayerHUDManager
        if (PlayerHUDManager.ActivePlayers != null)
        {
            foreach (var p in PlayerHUDManager.ActivePlayers)
            {
                var mono = p as MonoBehaviour;
                if (mono != null && mono.gameObject != null && !list.Contains(mono.transform))
                {
                    list.Add(mono.transform);
                }
            }
        }

        return list;
    }

    private int roundRobinPlayerIndex = 0;

    private void DetectAndSwitchTarget()
    {
        if (IsDead || CurrentStateValue == FinalBossState.Sitting || CurrentStateValue == FinalBossState.JumpDown) return;

        var activePlayers = GetAllActivePlayers();
        var validPlayers = new List<Transform>();
        foreach (var p in activePlayers)
        {
            if (p != null && p != transform && !IsPlayerDeadOrInvisible(p))
            {
                validPlayers.Add(p);
            }
        }

        if (validPlayers.Count == 0)
        {
            targetPlayer = null;
            return;
        }

        // Xoay luân phiên quanh các Player (Round-Robin) để bắn chém xa liên tục tới từng Player!
        roundRobinPlayerIndex = roundRobinPlayerIndex % validPlayers.Count;
        targetPlayer = validPlayers[roundRobinPlayerIndex];
        roundRobinPlayerIndex = (roundRobinPlayerIndex + 1) % validPlayers.Count;

        Debug.Log($"[FinalBossAI] Xoay mục tiêu luân phiên sang Player: {targetPlayer.name} ({validPlayers.Count} players đang chơi)!");
    }

    private bool IsPlayerDeadOrInvisible(Transform player)
    {
        var ps = player.GetComponentInParent<IPlayerHUDTarget>();
        var sk = player.GetComponentInParent<Skeleton>();
        return (ps != null && (ps.CurrentHealth <= 0 || ps.IsInvisible)) || (sk != null && sk.CurrentHealthValue <= 0);
    }

    private Transform GetPlayerRoot(Transform t)
    {
        if (t == null) return null;
        if (t.CompareTag("Player")) return t;

        // Try getting the root Player component directly via interface lookup (optimized)
        var hudTarget = t.GetComponentInParent<IPlayerHUDTarget>();
        if (hudTarget != null)
        {
            var mono = hudTarget as MonoBehaviour;
            if (mono != null && mono.gameObject != null) return mono.transform;
        }

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
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 12f);
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
        // Sight range (yellow)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);

        // Attack range (red)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Shockwave trigger range (blue)
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, shockwaveTriggerRange);

        // Shockwave impact radius (cyan)
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, shockwaveRadius);

        // Hand markers
        if (leftHandBase != null && leftHandTip != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(leftHandBase.position, leftHandTip.position);
            Gizmos.DrawWireSphere(leftHandBase.position, attackThickness);
            Gizmos.DrawWireSphere(leftHandTip.position, attackThickness);
        }
        if (rightHandBase != null && rightHandTip != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(rightHandBase.position, rightHandTip.position);
            Gizmos.DrawWireSphere(rightHandBase.position, attackThickness);
            Gizmos.DrawWireSphere(rightHandTip.position, attackThickness);
        }
    }

    // ══════════════════════════════════════════════════════════
    //  STATE MACHINE INNER CLASSES
    // ══════════════════════════════════════════════════════════

    private class SittingState : IEnemyState
    {
        private FinalBossAI boss;
        public SittingState(FinalBossAI boss) { this.boss = boss; }

        public void Enter()
        {
            if (boss.AgentReady) boss.agent.isStopped = true;
            boss.SetSpeedNet(0f);
            if (boss.anim != null)
            {
                if (boss.useSittingBool) boss.anim.SetBool("IsSitting", true);
                boss.anim.SetTrigger(boss.sitTrigger);
            }
        }

        public void Update()
        {
            if (boss.startActiveWithoutMiniboss)
            {
                boss.ActivateBoss();
                boss.SetHUDVisible(true);
                boss.ChangeState(FinalBossState.JumpDown);
                return;
            }

            bool silasDead = boss.bossMiniboss != null && boss.bossMiniboss.IsDead;
            bool rakanDead = boss.miniBoss != null && boss.miniBoss.IsDead;

            // When Silas dies (or Rakan dies if configured), show HUD and jump down immediately!
            if (silasDead || rakanDead)
            {
                boss.ActivateBoss();
                boss.SetHUDVisible(true);
                boss.ChangeState(FinalBossState.JumpDown);
            }
        }

        public void Exit()
        {
            if (boss.anim != null && boss.useSittingBool)
            {
                boss.anim.SetBool("IsSitting", false);
            }
        }
    }

    private class JumpDownState : IEnemyState
    {
        private FinalBossAI boss;
        private Vector3 startPos;
        private Vector3 endPos;
        private float timer;
        private bool originalRootMotion;

        public JumpDownState(FinalBossAI boss) { this.boss = boss; }

        public void Enter()
        {
            timer = 0f;
            startPos = boss.transform.position;

            if (boss.landingPoint != null)
            {
                endPos = boss.landingPoint.position;
            }
            else
            {
                endPos = startPos + boss.transform.forward * 6f;
                if (NavMesh.SamplePosition(endPos, out NavMeshHit hit, 10f, NavMesh.AllAreas))
                {
                    endPos = hit.position;
                }
                else
                {
                    endPos.y = startPos.y - 4f;
                }
            }

            if (!boss.isStandaloneMode)
            {
                boss.jumpDownCounter.Value++;
                boss.SetHUDVisible(true);
            }
            else
            {
                boss.SetHUDVisible(true);
                if (boss.anim != null) boss.anim.SetTrigger(boss.jumpDownTriggerParam);
            }

            // Disable Root Motion during manual parabolic translation to prevent conflict/jerking
            if (boss.anim != null)
            {
                originalRootMotion = boss.anim.applyRootMotion;
                boss.anim.applyRootMotion = false;
            }

            // Disable agent so we can lerp positions manually
            if (boss.agent != null) boss.agent.enabled = false;
        }

        public void Update()
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / boss.jumpDownDuration);

            Vector3 currentPos = Vector3.Lerp(startPos, endPos, progress);
            float arc = boss.jumpDownPeakHeight * Mathf.Sin(Mathf.PI * progress);
            currentPos.y += arc;

            boss.transform.position = currentPos;

            Vector3 dir = (endPos - startPos);
            dir.y = 0;
            if (dir.sqrMagnitude > 0.01f)
            {
                boss.transform.rotation = Quaternion.Slerp(boss.transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 5f);
            }

            if (timer >= boss.jumpDownDuration)
            {
                Landed();
            }
        }

        private void Landed()
        {
            boss.transform.position = endPos;
            boss.ActivateBoss();

            if (boss.agent != null)
            {
                boss.agent.enabled = true;
                boss.agent.Warp(endPos);
                boss.agent.velocity = Vector3.zero;
            }

            boss.TriggerLandingSlam();
            // ĐÃ XÓA TÍNH NĂNG HÓA TO VÀ LỬA/KIẾM: Chuyển thẳng sang tấn công tầm xa với 3 hoạt ảnh!
            boss.ChangeState(FinalBossState.Chase);
        }

        public void Exit()
        {
            if (boss.anim != null)
            {
                boss.anim.applyRootMotion = originalRootMotion;
            }
        }
    }

    private class IdleState : IEnemyState
    {
        private FinalBossAI boss;
        public IdleState(FinalBossAI boss) { this.boss = boss; }

        public void Enter()
        {
            boss.hasWanderDestination = false;
            boss.wanderWaitTimer = Random.Range(1.5f, 3f);
            if (boss.AgentReady) boss.agent.isStopped = true;
            boss.SetSpeedNet(0f);
        }

        public void Update()
        {
            if (!boss.IsBossActive) return;

            if (boss.targetPlayer != null)
            {
                boss.hasWanderDestination = false;
                boss.ChangeState(FinalBossState.Chase);
                return;
            }

            if (!boss.hasWanderDestination)
            {
                boss.wanderWaitTimer -= Time.deltaTime;
                if (boss.wanderWaitTimer <= 0)
                {
                    Vector3 randomDirection = Random.insideUnitSphere * 10f + boss.transform.position;
                    if (NavMesh.SamplePosition(randomDirection, out NavMeshHit navHit, 10f, NavMesh.AllAreas))
                    {
                        boss.wanderDestination = navHit.position;
                        boss.hasWanderDestination = true;
                        if (boss.AgentReady)
                        {
                            boss.agent.isStopped = false;
                            boss.agent.speed = boss.walkSpeed;
                            boss.agent.SetDestination(boss.wanderDestination);
                        }
                    }
                }
                boss.SetSpeedNet(0f);
            }
            else
            {
                boss.SetSpeedNet(0.5f); // Play Walk animation

                if (boss.AgentReady)
                {
                    if (!boss.agent.pathPending && boss.agent.remainingDistance <= boss.agent.stoppingDistance + 0.5f)
                    {
                        boss.hasWanderDestination = false;
                        boss.wanderWaitTimer = Random.Range(1.5f, 3f);
                    }
                }
                else
                {
                    boss.hasWanderDestination = false;
                }
            }
        }

        public void Exit() { }
    }

    private class ChaseState : IEnemyState
    {
        private FinalBossAI boss;
        public ChaseState(FinalBossAI boss) { this.boss = boss; }

        public void Enter() { }

        public void Update()
        {
            if (boss.targetPlayer == null || boss.IsPlayerDeadOrInvisible(boss.targetPlayer))
            {
                boss.targetPlayer = null;
                boss.ChangeState(FinalBossState.Idle);
                return;
            }

            // Xoay hướng mặt về phía Player
            boss.RotateTowards(boss.targetPlayer.position);

            float dist = Vector3.Distance(boss.transform.position, boss.targetPlayer.position);

            // Đứng từ xa bắn chiêu tầm xa hoặc dùng chiêu Phase 2
            if (boss.attackCooldownTimer <= 0)
            {
                if (boss.AgentReady) boss.agent.isStopped = true;
                boss.SetSpeedNet(0f);

                if (boss.isEnraged || boss.ActualCurrentHealth <= 600f)
                {
                    // PHASE 2: Chọn ngẫu nhiên công bằng giữa tất cả các kỹ năng đã hồi chiêu (Mưa Kiếm, Cầu Lửa, Phun Lửa, Vệt Chém)
                    var availableSkills = new List<FinalBossState>();
                    availableSkills.Add(FinalBossState.Attack); // Đòn chém thường (3 vệt / 5 vệt chém) luôn có thể dùng

                    if (boss.fireSpewCooldownTimer <= 0) availableSkills.Add(FinalBossState.FireSpew);
                    if (boss.fireBarrageCooldownTimer <= 0) availableSkills.Add(FinalBossState.FireBarrage);
                    if (boss.swordRainCooldownTimer <= 0) availableSkills.Add(FinalBossState.SwordRain);

                    FinalBossState chosenSkill = availableSkills[Random.Range(0, availableSkills.Count)];
                    boss.ChangeState(chosenSkill);
                }
                else
                {
                    boss.ChangeState(FinalBossState.Attack);
                }
                return;
            }

            if (boss.AgentReady)
            {
                boss.agent.isStopped = false;
                if (dist > boss.runDistanceThreshold)
                {
                    boss.agent.speed = boss.runSpeed;
                    boss.SetSpeedNet(1.0f); // Run animation
                }
                else
                {
                    boss.agent.speed = boss.walkSpeed;
                    boss.SetSpeedNet(0.5f); // Walk animation
                }
                boss.agent.SetDestination(boss.targetPlayer.position);
            }
            else
            {
                boss.SetSpeedNet(0f);
            }
        }

        public void Exit() { }
    }

    private class AttackState : IEnemyState
    {
        private FinalBossAI boss;
        private bool hasSpawnedProjectile;
        private float spawnTimer;
        private HashSet<Transform> hitPlayersThisAttack = new HashSet<Transform>();

        public AttackState(FinalBossAI boss) { this.boss = boss; }

        public void Enter()
        {
            hitPlayersThisAttack.Clear();
            hasSpawnedProjectile = false;
            spawnTimer = 0.35f; // Đếm lùi 0.35s từ khi vung tay là bắn đạn 3 tia

            // Chọn ngẫu nhiên 1 trong 3 đòn đánh: RightPunch (DamPhai), LeftPunch (DamTrai), Swipe (Cào Xa)
            boss.currentAttackIndex = Random.Range(0, boss.attackTriggers.Length);

            // Luôn luôn kích hoạt Trigger hoạt ảnh Animator trên máy này
            if (boss.anim != null && boss.attackTriggers != null && boss.currentAttackIndex < boss.attackTriggers.Length)
            {
                boss.anim.SetTrigger(boss.attackTriggers[boss.currentAttackIndex]);
            }

            if (!boss.isStandaloneMode && boss.IsServer)
            {
                boss.attackTypeSync.Value = boss.currentAttackIndex;
                boss.attackCounter.Value++;
            }

            float duration = 1.8f;
            if (boss.attackConfigs != null && boss.currentAttackIndex < boss.attackConfigs.Length && boss.attackConfigs[boss.currentAttackIndex].duration > 0f)
            {
                duration = boss.attackConfigs[boss.currentAttackIndex].duration;
            }
            boss.stateTimer = duration;

            if (boss.AgentReady)
            {
                boss.agent.isStopped = true;
                boss.agent.velocity = Vector3.zero;
            }
            boss.SetSpeedNet(0f);
        }

        public void Update()
        {
            boss.stateTimer -= Time.deltaTime;

            if (boss.targetPlayer != null)
            {
                boss.RotateTowards(boss.targetPlayer.position);
            }

            // BẮN ĐẠN PREFAB TẦM XA KHI ĐẾN TIMER VUNG TAY (0.35s)
            if (!hasSpawnedProjectile)
            {
                spawnTimer -= Time.deltaTime;
                if (spawnTimer <= 0f)
                {
                    hasSpawnedProjectile = true;
                    boss.SpawnRangedProjectile();
                }
            }

            // CÀO/ĐẬP TAY VẬT LÝ TRỰC TIẾP LÊN PLAYER Ở GẦN (-5 HP)
            boss.DealFistDamageContinuously(hitPlayersThisAttack, boss.currentAttackIndex);

            if (boss.stateTimer <= 0)
            {
                boss.attackCooldownTimer = boss.attackCooldown;
                if (boss.targetPlayer != null) boss.ChangeState(FinalBossState.Chase);
                else boss.ChangeState(FinalBossState.Idle);
            }
        }

        public void Exit()
        {
            if (boss.anim != null && boss.attackTriggers != null && boss.currentAttackIndex < boss.attackTriggers.Length)
            {
                boss.anim.ResetTrigger(boss.attackTriggers[boss.currentAttackIndex]);
            }
            if (boss.AgentReady) boss.agent.isStopped = false;
        }
    }

    private class ShockwaveState : IEnemyState
    {
        private FinalBossAI boss;
        private bool dealtDamage;

        public ShockwaveState(FinalBossAI boss) { this.boss = boss; }

        public void Enter()
        {
            dealtDamage = false;
            boss.stateTimer = boss.shockwaveDuration;

            if (boss.AgentReady)
            {
                boss.agent.isStopped = true;
                boss.agent.velocity = Vector3.zero;
            }
            boss.SetSpeedNet(0f);

            if (!boss.isStandaloneMode)
            {
                boss.shockwaveCounter.Value++;
            }
            else
            {
                if (boss.anim != null) boss.anim.SetTrigger(boss.shockwaveTriggerParam);
                boss.PlayShockwaveVFX();
            }
        }

        public void Update()
        {
            boss.stateTimer -= Time.deltaTime;

            float elapsed = boss.shockwaveDuration - boss.stateTimer;
            if (elapsed >= boss.shockwaveDamageDelay && !dealtDamage)
            {
                dealtDamage = true;
                boss.TriggerShockwaveDamage();
            }

            if (boss.stateTimer <= 0)
            {
                boss.shockwaveCooldownTimer = boss.shockwaveCooldown;
                if (boss.targetPlayer != null) boss.ChangeState(FinalBossState.Chase);
                else boss.ChangeState(FinalBossState.Idle);
            }
        }

        public void Exit() { }
    }

    private class GrowState : IEnemyState
    {
        private FinalBossAI boss;
        private float timer;
        private Vector3 startScale;
        private Vector3 targetScale;

        public GrowState(FinalBossAI boss) { this.boss = boss; }

        public void Enter()
        {
            timer = 0f;
            startScale = boss.transform.localScale;
            targetScale = startScale * 1.6f; // Phóng to kích thước thân thể 1.6 lần

            if (boss.AgentReady)
            {
                boss.agent.isStopped = true;
                boss.agent.velocity = Vector3.zero;
            }
            boss.SetSpeedNet(0f);

            if (!boss.isStandaloneMode) boss.growCounter.Value++;
            else if (boss.anim != null) boss.anim.SetTrigger(boss.growTriggerParam);

            boss.PlayGrowVFX();
            Debug.Log("[FinalBossAI] PHASE 2 KÍCH HOẠT (<= 500 HP)! KING ATLANTIS GẦM THÉT VÀ PHÓNG TO THÂN THỂ!");
        }

        public void Update()
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / boss.growDuration);
            boss.transform.localScale = Vector3.Lerp(startScale, targetScale, progress);

            if (!boss.isStandaloneMode && boss.IsServer)
            {
                boss.netScale.Value = boss.transform.localScale;
            }

            if (timer >= boss.growDuration)
            {
                boss.isEnraged = true; // Đạt Phase 2 thành công!
                if (!boss.isStandaloneMode && boss.IsServer)
                {
                    boss.netIsEnraged.Value = true;
                }
                if (boss.targetPlayer != null) boss.ChangeState(FinalBossState.Chase);
                else boss.ChangeState(FinalBossState.Idle);
            }
        }

        public void Exit()
        {
            if (boss.anim != null) boss.anim.ResetTrigger(boss.growTriggerParam);
            if (boss.AgentReady) boss.agent.isStopped = false;
        }
    }

    private class HitState : IEnemyState
    {
        private FinalBossAI boss;
        public HitState(FinalBossAI boss) { this.boss = boss; }

        public void Enter()
        {
            boss.stateTimer = boss.hitStaggerDuration;
            if (boss.AgentReady) boss.agent.isStopped = true;
            boss.SetSpeedNet(0f);

            if (!boss.isStandaloneMode) boss.hitCounter.Value++;
            else if (boss.anim != null) boss.anim.SetTrigger(boss.hitTriggerParam);
        }

        public void Update()
        {
            boss.stateTimer -= Time.deltaTime;
            if (boss.stateTimer <= 0)
            {
                if (boss.targetPlayer != null) boss.ChangeState(FinalBossState.Chase);
                else boss.ChangeState(FinalBossState.Idle);
            }
        }

        public void Exit()
        {
            if (boss.anim != null) boss.anim.ResetTrigger(boss.hitTriggerParam);
            if (boss.AgentReady) boss.agent.isStopped = false;
        }
    }

    private class DeadState : IEnemyState
    {
        private FinalBossAI boss;
        public DeadState(FinalBossAI boss) { this.boss = boss; }

        public void Enter()
        {
            if (boss.AgentReady) boss.agent.isStopped = true;
            if (boss.agent != null) boss.agent.enabled = false;
            boss.SetSpeedNet(0f);

            if (boss.visualRoot != null) boss.visualRoot.localPosition = Vector3.zero;

            var colliders = boss.GetComponentsInChildren<Collider>();
            foreach (var c in colliders)
            {
                if (c != null && !c.isTrigger) c.enabled = false;
            }

            if (!boss.isStandaloneMode) boss.dieCounter.Value++;
            else if (boss.anim != null) boss.anim.SetTrigger(boss.dieTriggerParam);

            Debug.Log("[FinalBossAI] Final Boss is dead!");
            Destroy(boss.gameObject, 6f);
        }

        public void Update() { }
        public void Exit() { }
    }

    private class FireSpewState : IEnemyState
    {
        private FinalBossAI boss;
        private float stageTimer;
        private bool isSpewing;

        public FireSpewState(FinalBossAI boss) { this.boss = boss; }

        public void Enter()
        {
            isSpewing = false;
            stageTimer = boss.fireSpewWindupDuration;

            if (boss.AgentReady)
            {
                boss.agent.isStopped = true;
                boss.agent.velocity = Vector3.zero;
            }
            boss.SetSpeedNet(0f);

            if (!boss.isStandaloneMode)
            {
                boss.fireSpewCounter.Value++;
            }
            else
            {
                if (boss.anim != null) boss.anim.SetTrigger(boss.fireSpewTriggerParam);
            }
        }

        public void Update()
        {
            stageTimer -= Time.deltaTime;

            if (boss.targetPlayer != null)
            {
                boss.RotateTowards(boss.targetPlayer.position);
            }

            if (stageTimer <= 0)
            {
                if (!isSpewing)
                {
                    isSpewing = true;
                    stageTimer = boss.fireSpewActiveDuration;

                    boss.TriggerFireSpewActive();
                }
                else
                {
                    if (stageTimer <= -1.0f)
                    {
                        if (boss.targetPlayer != null) boss.ChangeState(FinalBossState.Chase);
                        else boss.ChangeState(FinalBossState.Idle);
                    }
                }
            }
        }

        public void Exit()
        {
            boss.fireSpewCooldownTimer = boss.fireSpewInterval;
            if (boss.anim != null) boss.anim.ResetTrigger(boss.fireSpewTriggerParam);
            if (boss.AgentReady) boss.agent.isStopped = false;
        }
    }

    // ══════════════════════════════════════════════════════════
    //  FIRE SPEW TRIGGERS
    // ══════════════════════════════════════════════════════════

    [ClientRpc]
    private void PlayFireSpewVFXClientRpc()
    {
        PlayFireSpewVFX();
    }

    public void TriggerFireSpewActive()
    {
        if (!isStandaloneMode)
        {
            if (IsServer)
            {
                fireSpewActiveCounter.Value++;
                PlayFireSpewVFXClientRpc();
            }
        }
        else if (isStandaloneMode)
        {
            PlayFireSpewVFX();
        }

        bool auth = isStandaloneMode || (IsNetworkActive && IsServer);
        if (auth)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, fireSpewRadius, playerLayer);
            HashSet<Transform> hitRoots = new HashSet<Transform>();
            var list = new List<Transform>();

            foreach (var hit in hits)
            {
                Transform root = GetPlayerRoot(hit.transform);
                if (root != null && !hitRoots.Contains(root))
                {
                    hitRoots.Add(root);
                    list.Add(root);
                }
            }

            int hitCount = Mathf.Min(list.Count, 4);
            for (int i = 0; i < hitCount; i++)
            {
                Transform root = list[i];
                Vector3 knockbackDir = (root.position - transform.position);
                knockbackDir.y = 0.3f;
                Vector3 force = knockbackDir.normalized * fireSpewKnockback;

                EnemyDamageHelper.DealDamage(root, fireSpewDamage, force);
                Debug.Log($"[FinalBossAI] Fire Spew hit player: {root.name}");
            }
        }
    }

    private void PlayFireSpewVFX()
    {
        Vector3 spawnPos = transform.position + Vector3.up * 1.0f;
        if (fireSpewVFX != null)
        {
            GameObject vfx = Instantiate(fireSpewVFX, spawnPos, transform.rotation);
            vfx.transform.SetParent(transform);
            vfx.transform.localScale = Vector3.one;

            // Bổ sung Rigidbody Kinematic để bắt va chạm Trigger hoạt động
            var rb = vfx.GetComponent<Rigidbody>();
            if (rb == null) rb = vfx.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // Bổ sung script gây sát thương cho tia lửa
            var damageZone = vfx.GetComponent<FireBeamDamageZone>();
            if (damageZone == null) damageZone = vfx.AddComponent<FireBeamDamageZone>();
            damageZone.Initialize(this, fireSpewDamage, fireSpewKnockback);

            var particles = vfx.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in particles)
            {
                ps.Play();
            }
            var vfxGraphs = vfx.GetComponentsInChildren<UnityEngine.VFX.VisualEffect>(true);
            foreach (var ve in vfxGraphs)
            {
                ve.Play();
            }

            Destroy(vfx, fireSpewActiveDuration + 1f);
        }
        if (fireSpewSFX != null)
        {
            AudioSource.PlayClipAtPoint(fireSpewSFX, spawnPos, 1.0f);
        }
    }

    private void PlayGrowVFX()
    {
        Vector3 spawnPos = transform.position + Vector3.up * 1.0f;
        if (growVFX != null)
        {
            GameObject vfx = Instantiate(growVFX, spawnPos, transform.rotation);
            vfx.transform.SetParent(transform);
            Destroy(vfx, growDuration + 1f);
        }
        else if (shockwaveVFX != null)
        {
            // Fallback VFX sóng năng lượng bùng nổ xung quanh khi hóa to qua Phase 2
            GameObject vfx = Instantiate(shockwaveVFX, spawnPos, Quaternion.identity);
            vfx.transform.localScale = Vector3.one * 2.5f;
            Destroy(vfx, growDuration + 1f);
        }
        if (growSFX != null)
        {
            AudioSource.PlayClipAtPoint(growSFX, spawnPos, 1.0f);
        }
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
                sword = new GameObject("SpectralSummonedSword");
                
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
                warning = new GameObject("ProceduralWarningRing");
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

    private System.Collections.IEnumerator RoutineDropSwordAtPosition(Vector3 groundPos)
    {
        Debug.Log($"[FinalBossAI] RoutineDropSwordAtPosition bắt đầu tại {groundPos}");
        GameObject warning = GetPooledWarning(groundPos + Vector3.up * 0.05f);

        var flasher = warning.GetComponent<WarningDecalFlash>();
        if (flasher != null) flasher.StartFlashing(warningDuration);

        var ring = warning.GetComponent<ProceduralWarningCircle>();
        if (ring != null) ring.StartWarning(warningDuration, swordImpactRadius);

        yield return new WaitForSeconds(warningDuration);

        RecycleWarning(warning);

        Vector3 skyPos = groundPos + Vector3.up * 18.0f;
        Quaternion rot = Quaternion.LookRotation(Vector3.down) * Quaternion.Euler(swordSpawnRotationOffset);
        GameObject sword = GetPooledSword(skyPos, rot);
        Debug.Log($"[FinalBossAI] Đã triệu hồi thanh kiếm tại {skyPos} với góc xoay {rot.eulerAngles}");

        var proj = sword.GetComponent<FallingSwordProjectile>();
        if (proj == null) proj = sword.AddComponent<FallingSwordProjectile>();

        proj.Initialize(this, groundPos, swordDropSpeed, swordDamage, swordImpactRadius, playerLayer);
    }

    public void PlaySwordImpactEffects(Vector3 impactPos)
    {
        if (swordImpactVFX != null)
        {
            GameObject vfx = Instantiate(swordImpactVFX, impactPos, Quaternion.identity);
            Destroy(vfx, 2.5f);
        }
        if (swordImpactSFX != null)
        {
            AudioSource.PlayClipAtPoint(swordImpactSFX, impactPos, 1.0f);
        }
    }

    public void DealSwordImpactDamage(Vector3 impactPos, float damage, float radius, LayerMask layer)
    {
        bool auth = isStandaloneMode || (IsNetworkActive && IsServer);
        if (!auth) return;

        Collider[] hits = Physics.OverlapSphere(impactPos, radius, layer);
        HashSet<Transform> hitRoots = new HashSet<Transform>();

        foreach (var hit in hits)
        {
            Transform root = GetPlayerRoot(hit.transform);
            if (root != null && !hitRoots.Contains(root))
            {
                hitRoots.Add(root);
                Vector3 knockbackDir = (root.position - impactPos).normalized + Vector3.up * 0.5f;
                EnemyDamageHelper.DealDamage(root, damage, knockbackDir * 5f);
                Debug.Log($"[FinalBossAI] Sword Rain hit player: {root.name} for {damage} HP");
            }
        }
    }

    private Vector3[] CalculateSwordRainTargets()
    {
        var activePlayers = GetAllActivePlayers();
        var spots = new List<Vector3>();

        foreach (var p in activePlayers)
        {
            if (p == null || IsPlayerDeadOrInvisible(p)) continue;

            spots.Add(p.position);

            for (int k = 0; k < 2; k++)
            {
                Vector2 randomOffset = Random.insideUnitCircle * 2.5f;
                Vector3 offsetPos = p.position + new Vector3(randomOffset.x, 0f, randomOffset.y);
                if (NavMesh.SamplePosition(offsetPos, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
                {
                    spots.Add(hit.position);
                }
                else
                {
                    spots.Add(offsetPos);
                }
            }
        }

        if (spots.Count == 0)
        {
            for (int i = 0; i < 4; i++)
            {
                Vector2 randomOffset = Random.insideUnitCircle * 6f;
                Vector3 offsetPos = transform.position + new Vector3(randomOffset.x, 0, randomOffset.y);
                if (NavMesh.SamplePosition(offsetPos, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                {
                    spots.Add(hit.position);
                }
            }
        }

        return spots.ToArray();
    }

    private class SwordRainState : IEnemyState
    {
        private FinalBossAI boss;
        private float timer;
        private float spawnTimer;
        private List<Vector3> targetPositions = new List<Vector3>();
        private int currentSpawnIndex = 0;

        public SwordRainState(FinalBossAI boss) { this.boss = boss; }

        public void Enter()
        {
            timer = 0f;
            spawnTimer = 0f;
            currentSpawnIndex = 0;
            targetPositions.Clear();

            // 1. Chỉ khóa vị trí hiện tại dưới chân toàn bộ người chơi khi bắt đầu chiêu (không đuổi theo)
            var players = boss.GetAllActivePlayers();
            foreach (var p in players)
            {
                if (p != null && !boss.IsPlayerDeadOrInvisible(p))
                {
                    targetPositions.Add(p.position);
                }
            }
            Debug.Log($"[SwordRainState] Enter: Tìm thấy {targetPositions.Count} mục tiêu người chơi.");

            // 2. Thêm các điểm ngẫu nhiên xung quanh khu vực để đạt tổng số từ 5 đến 10 thanh kiếm
            int totalSwords = Random.Range(5, 11);
            int neededRandom = Mathf.Max(0, totalSwords - targetPositions.Count);
            Debug.Log($"[SwordRainState] Enter: Tổng số kiếm cần rơi = {totalSwords}, cần tạo thêm = {neededRandom} điểm ngẫu nhiên.");
            for (int i = 0; i < neededRandom; i++)
            {
                Vector2 randomOffset = Random.insideUnitCircle * 8f;
                Vector3 offsetPos = boss.transform.position + new Vector3(randomOffset.x, 0, randomOffset.y);
                if (UnityEngine.AI.NavMesh.SamplePosition(offsetPos, out UnityEngine.AI.NavMeshHit hit, 6f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    targetPositions.Add(hit.position);
                }
                else
                {
                    targetPositions.Add(boss.transform.position + new Vector3(randomOffset.x, 0, randomOffset.y));
                }
            }

            Debug.Log($"[SwordRainState] Enter: Tổng danh sách điểm rơi kiếm = {targetPositions.Count}");

            // Tráo ngẫu nhiên thứ tự rơi
            for (int i = 0; i < targetPositions.Count; i++)
            {
                int rnd = Random.Range(0, targetPositions.Count);
                Vector3 temp = targetPositions[i];
                targetPositions[i] = targetPositions[rnd];
                targetPositions[rnd] = temp;
            }

            if (boss.AgentReady)
            {
                boss.agent.isStopped = true;
                boss.agent.velocity = Vector3.zero;
            }
            boss.SetSpeedNet(0f);

            if (!boss.isStandaloneMode)
            {
                boss.swordRainCounter.Value++;
            }
            else
            {
                if (boss.anim != null) boss.anim.SetTrigger(boss.swordRainTriggerParam);
            }
        }

        public void Update()
        {
            timer += Time.deltaTime;
            spawnTimer -= Time.deltaTime;

            if (boss.agent != null && boss.agent.isActiveAndEnabled)
            {
                boss.agent.isStopped = true;
                boss.agent.velocity = Vector3.zero;
            }
            boss.SetSpeedNet(0f);

            // Sinh kiếm tuần tự từ các vị trí cố định đã khóa từ trước
            if (spawnTimer <= 0f && currentSpawnIndex < targetPositions.Count && timer < boss.swordRainDuration)
            {
                spawnTimer = boss.swordSpawnInterval;

                Vector3 targetPos = targetPositions[currentSpawnIndex];
                Debug.Log($"[SwordRainState] Update: Đang bắn ClientRpc/Local để gọi kiếm {currentSpawnIndex + 1}/{targetPositions.Count} tại {targetPos}");
                currentSpawnIndex++;

                Vector3[] targets = new Vector3[] { targetPos };
                if (!boss.isStandaloneMode)
                {
                    boss.TriggerSwordRainDropClientRpc(targets);
                }
                else
                {
                    boss.ExecuteSwordRainDropLocal(targets);
                }
            }

            if (timer >= boss.swordRainDuration + 1.0f)
            {
                if (boss.targetPlayer != null) boss.ChangeState(FinalBossState.Chase);
                else boss.ChangeState(FinalBossState.Idle);
            }
        }

        public void Exit()
        {
            boss.swordRainCooldownTimer = boss.swordRainCooldown;
            if (boss.anim != null) boss.anim.ResetTrigger(boss.swordRainTriggerParam);
            if (boss.AgentReady) boss.agent.isStopped = false;
        }
    }

    // ══════════════════════════════════════════════════════════
    //  FIRE BARRAGE OBJECT POOLING & LOGIC
    // ══════════════════════════════════════════════════════════

    private Queue<GameObject> fireBarragePool = new Queue<GameObject>();

    public GameObject GetPooledFireBarrage(Vector3 spawnPos, Quaternion rotation)
    {
        GameObject orb = null;
        while (fireBarragePool.Count > 0)
        {
            var candidate = fireBarragePool.Dequeue();
            if (candidate != null)
            {
                orb = candidate;
                break;
            }
        }

        if (orb == null)
        {
            if (fireBarragePrefab != null)
            {
                orb = Instantiate(fireBarragePrefab);
            }
            else if (fireSpewVFX != null)
            {
                orb = Instantiate(fireSpewVFX);
            }
            else
            {
                orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                orb.transform.localScale = Vector3.one * 0.8f;
                var r = orb.GetComponent<Renderer>();
                if (r != null)
                {
                    r.material = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default"));
                    r.material.color = new Color(1f, 0.3f, 0f);
                }
            }
        }

        // Đảm bảo luôn có Rigidbody Kinematic ở Root để va chạm Trigger hoạt động chuẩn xác 100%
        var rb = orb.GetComponent<Rigidbody>();
        if (rb == null) rb = orb.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        orb.transform.position = spawnPos;
        orb.transform.rotation = rotation;
        orb.transform.localScale = Vector3.one * 2.0f; // Tăng kích thước cầu lửa lên x2
        orb.SetActive(true);
        return orb;
    }

    public void RecycleFireBarrage(GameObject orb)
    {
        if (orb == null) return;
        orb.SetActive(false);
        if (!fireBarragePool.Contains(orb))
        {
            fireBarragePool.Enqueue(orb);
        }
    }

    [ClientRpc]
    private void TriggerFireBarrageDropClientRpc(Vector3[] positions)
    {
        ExecuteFireBarrageDropLocal(positions);
    }

    public void ExecuteFireBarrageDropLocal(Vector3[] positions)
    {
        foreach (var pos in positions)
        {
            StartCoroutine(RoutineDropFireBarrageAtPosition(pos));
        }
    }

    private System.Collections.IEnumerator RoutineDropFireBarrageAtPosition(Vector3 groundPos)
    {
        GameObject prefabToUse = fireBarrageWarningDecalPrefab != null ? fireBarrageWarningDecalPrefab : warningDecalPrefab;
        GameObject warning = null;

        if (prefabToUse != null)
        {
            warning = Instantiate(prefabToUse, groundPos + Vector3.up * 0.05f, Quaternion.identity);
            var flasher = warning.GetComponent<WarningDecalFlash>();
            if (flasher == null) flasher = warning.AddComponent<WarningDecalFlash>();
            flasher.StartFlashing(fireBarrageWarningDuration);
        }
        else
        {
            warning = new GameObject("ProceduralWarningRing");
            warning.transform.position = groundPos;
            var ring = warning.AddComponent<ProceduralWarningCircle>();
            if (ring != null)
            {
                ring.StartWarning(fireBarrageWarningDuration, fireBarrageImpactRadius);
            }
        }

        yield return new WaitForSeconds(fireBarrageWarningDuration);

        if (warning != null)
        {
            Destroy(warning);
        }

        Vector3 skyPos = groundPos + Vector3.up * 16.0f;
        GameObject fireOrb = GetPooledFireBarrage(skyPos, Quaternion.LookRotation(Vector3.down));

        var proj = fireOrb.GetComponent<FallingFireOrbProjectile>();
        if (proj == null) proj = fireOrb.AddComponent<FallingFireOrbProjectile>();

        proj.Initialize(this, groundPos, fireBarrageDropSpeed, fireBarrageDamage, fireBarrageImpactRadius, playerLayer);
    }

    public void PlayFireBarrageImpactEffects(Vector3 impactPos)
    {
        if (fireBarrageImpactVFX != null)
        {
            GameObject vfx = Instantiate(fireBarrageImpactVFX, impactPos, Quaternion.identity);
            Destroy(vfx, 2.5f);
        }
        if (fireBarrageImpactSFX != null)
        {
            AudioSource.PlayClipAtPoint(fireBarrageImpactSFX, impactPos, 1.0f);
        }
    }

    public void DealFireBarrageImpactDamage(Vector3 impactPos, float damage, float radius, LayerMask layer)
    {
        bool auth = isStandaloneMode || (IsNetworkActive && IsServer);
        if (!auth) return;

        Collider[] hits = Physics.OverlapSphere(impactPos, radius, layer);
        HashSet<Transform> hitRoots = new HashSet<Transform>();

        foreach (var hit in hits)
        {
            Transform root = GetPlayerRoot(hit.transform);
            if (root != null && !hitRoots.Contains(root))
            {
                hitRoots.Add(root);
                Vector3 knockbackDir = (root.position - impactPos).normalized + Vector3.up * 0.4f;
                EnemyDamageHelper.DealDamage(root, damage, knockbackDir * 6f);
                Debug.Log($"[FinalBossAI] Fire Barrage hit player: {root.name} for {damage} HP");
            }
        }
    }

    private Vector3[] CalculateFireBarrageTargets()
    {
        var activePlayers = GetAllActivePlayers();
        var spots = new List<Vector3>();

        foreach (var p in activePlayers)
        {
            if (p == null || IsPlayerDeadOrInvisible(p)) continue;

            spots.Add(p.position);

            for (int k = 0; k < 2; k++)
            {
                Vector2 randomOffset = Random.insideUnitCircle * 3.0f;
                Vector3 offsetPos = p.position + new Vector3(randomOffset.x, 0f, randomOffset.y);
                if (NavMesh.SamplePosition(offsetPos, out NavMeshHit hit, 3.5f, NavMesh.AllAreas))
                {
                    spots.Add(hit.position);
                }
                else
                {
                    spots.Add(offsetPos);
                }
            }
        }

        if (spots.Count == 0)
        {
            for (int i = 0; i < 4; i++)
            {
                Vector2 randomOffset = Random.insideUnitCircle * 6f;
                Vector3 offsetPos = transform.position + new Vector3(randomOffset.x, 0, randomOffset.y);
                if (NavMesh.SamplePosition(offsetPos, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                {
                    spots.Add(hit.position);
                }
            }
        }

        return spots.ToArray();
    }

    private class FireBarrageState : IEnemyState
    {
        private FinalBossAI boss;
        private float timer;
        private float spawnTimer;

        public FireBarrageState(FinalBossAI boss) { this.boss = boss; }

        public void Enter()
        {
            timer = 0f;
            spawnTimer = 0f;

            if (boss.AgentReady)
            {
                boss.agent.isStopped = true;
                boss.agent.velocity = Vector3.zero;
            }
            boss.SetSpeedNet(0f);

            if (!boss.isStandaloneMode)
            {
                boss.fireBarrageCounter.Value++;
            }
            else
            {
                if (boss.anim != null) boss.anim.SetTrigger(boss.fireBarrageTriggerParam);
            }
        }

        public void Update()
        {
            timer += Time.deltaTime;
            spawnTimer -= Time.deltaTime;

            if (boss.agent != null && boss.agent.isActiveAndEnabled)
            {
                boss.agent.isStopped = true;
                boss.agent.velocity = Vector3.zero;
            }
            boss.SetSpeedNet(0f);

            if (boss.targetPlayer != null)
            {
                boss.RotateTowards(boss.targetPlayer.position);
            }

            // Chỉ spawn cầu lửa khi chưa hết thời lượng chiêu
            if (spawnTimer <= 0f && timer < boss.fireBarrageDuration)
            {
                spawnTimer = boss.fireBarrageInterval;

                Vector3[] targets = boss.CalculateFireBarrageTargets();
                if (!boss.isStandaloneMode)
                {
                    boss.TriggerFireBarrageDropClientRpc(targets);
                }
                else
                {
                    boss.ExecuteFireBarrageDropLocal(targets);
                }
            }

            if (timer >= boss.fireBarrageDuration + 1.0f)
            {
                boss.ActivateBoss();

                if (boss.targetPlayer != null) boss.ChangeState(FinalBossState.Chase);
                else boss.ChangeState(FinalBossState.Idle);
            }
        }

        public void Exit()
        {
            boss.fireBarrageCooldownTimer = boss.fireBarrageCooldown;
            if (boss.anim != null) boss.anim.ResetTrigger(boss.fireBarrageTriggerParam);
            if (boss.AgentReady) boss.agent.isStopped = false;
        }
    }

    // ══════════════════════════════════════════════════════════
    //  DUMMY ANIMATION EVENT RECEIVERS TO PREVENT WARNINGS
    // ══════════════════════════════════════════════════════════
    public void OnLeftPunchSwing() { }
    public void OnRightPunchSwing() { }
    public void OnSwipeSwing() { }
    public void OnShockwaveImpact() { }
    public void OnFireSpewImpact() { }
}

public class FallingSwordProjectile : MonoBehaviour
{
    private FinalBossAI bossOwner;
    private Vector3 targetGroundPos;
    private float dropSpeed;
    private float damage;
    private float impactRadius;
    private LayerMask playerLayer;
    private bool isFalling;

    public void Initialize(FinalBossAI owner, Vector3 groundPos, float speed, float dmg, float radius, LayerMask layer)
    {
        bossOwner = owner;
        targetGroundPos = groundPos;
        dropSpeed = speed;
        damage = dmg;
        impactRadius = radius;
        playerLayer = layer;
        isFalling = true;
    }

    private void Update()
    {
        if (!isFalling) return;

        transform.position += Vector3.down * dropSpeed * Time.deltaTime;

        if (transform.position.y <= targetGroundPos.y + 0.2f)
        {
            isFalling = false;
            OnImpact();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isFalling) return;

        // Quét tìm Player bằng IPlayerHUDTarget (chính xác tuyệt đối kể cả chạm collider con)
        var hudTarget = other.GetComponentInParent<IPlayerHUDTarget>() ?? other.GetComponentInChildren<IPlayerHUDTarget>();
        if (hudTarget != null)
        {
            Transform playerRoot = hudTarget.transform;
            isFalling = false;
            Vector3 knockbackDir = (playerRoot.position - transform.position).normalized + Vector3.up * 0.5f;
            EnemyDamageHelper.DealDamage(playerRoot, damage, knockbackDir * 5f);

            if (bossOwner != null)
            {
                bossOwner.PlaySwordImpactEffects(transform.position);
                bossOwner.RecycleSword(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }

    private void OnImpact()
    {
        transform.position = targetGroundPos;

        if (bossOwner != null)
        {
            bossOwner.PlaySwordImpactEffects(targetGroundPos);
            bossOwner.DealSwordImpactDamage(targetGroundPos, damage, impactRadius, playerLayer);
            StartCoroutine(RoutineRecycleAfterImpale());
        }
        else
        {
            Destroy(gameObject, 2.0f);
        }
    }

    private System.Collections.IEnumerator RoutineRecycleAfterImpale()
    {
        // Giữ thanh kiếm cắm sâu xuống mặt đất 2.0 giây cho người chơi nhìn thấy kiếm triệu hồi cực kỳ đẹp mắt!
        yield return new WaitForSeconds(2.0f);
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

public class FallingFireOrbProjectile : MonoBehaviour
{
    private FinalBossAI bossOwner;
    private Vector3 targetGroundPos;
    private float dropSpeed;
    private float damage;
    private float impactRadius;
    private LayerMask playerLayer;
    private bool isFalling;

    public void Initialize(FinalBossAI owner, Vector3 groundPos, float speed, float dmg, float radius, LayerMask layer)
    {
        bossOwner = owner;
        targetGroundPos = groundPos;
        dropSpeed = speed;
        damage = dmg;
        impactRadius = radius;
        playerLayer = layer;
        isFalling = true;

        // Restart child Particle Systems and Visual Effects upon activation from Object Pool
        var particles = GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in particles)
        {
            ps.Clear();
            ps.Play();
        }
        var vfxGraphs = GetComponentsInChildren<UnityEngine.VFX.VisualEffect>(true);
        foreach (var ve in vfxGraphs)
        {
            ve.Reinit();
            ve.Play();
        }
    }

    private void Update()
    {
        if (!isFalling) return;

        transform.position += Vector3.down * dropSpeed * Time.deltaTime;

        if (transform.position.y <= targetGroundPos.y + 0.2f)
        {
            isFalling = false;
            OnImpact();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isFalling) return;

        // Quét tìm Player bằng IPlayerHUDTarget (chính xác tuyệt đối kể cả chạm collider con)
        var hudTarget = other.GetComponentInParent<IPlayerHUDTarget>() ?? other.GetComponentInChildren<IPlayerHUDTarget>();
        if (hudTarget != null)
        {
            Transform playerRoot = hudTarget.transform;
            isFalling = false;
            Vector3 knockbackDir = (playerRoot.position - transform.position).normalized + Vector3.up * 0.5f;
            EnemyDamageHelper.DealDamage(playerRoot, damage, knockbackDir * 5f);

            if (bossOwner != null)
            {
                bossOwner.PlayFireBarrageImpactEffects(transform.position);
                bossOwner.RecycleFireBarrage(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }

    private void OnImpact()
    {
        transform.position = targetGroundPos;

        if (bossOwner != null)
        {
            bossOwner.PlayFireBarrageImpactEffects(targetGroundPos);
            bossOwner.DealFireBarrageImpactDamage(targetGroundPos, damage, impactRadius, playerLayer);
            bossOwner.RecycleFireBarrage(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}

public class FinalBossProjectile : MonoBehaviour
{
    private Vector3 moveDirection;
    private float moveSpeed = 18f;
    private float damage = 15f;
    private Transform ownerBoss;
    private HashSet<Transform> hitPlayers = new HashSet<Transform>();

    public void Initialize(Vector3 direction, float speed, float damageAmount, Transform boss)
    {
        moveDirection = direction.normalized;
        moveSpeed = speed;
        damage = damageAmount;
        ownerBoss = boss;

        var rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        var colliders = GetComponentsInChildren<Collider>(true);
        if (colliders.Length == 0)
        {
            // Tự động tạo BoxCollider kích thước chuẩn cho tia chém VFX nếu Prefab chưa có Collider
            var box = gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(2.0f, 2.0f, 2.0f);
            box.isTrigger = true;
        }
        else
        {
            foreach (var c in colliders)
            {
                if (c != null) c.isTrigger = true;
            }
        }

        // GIỮ NGUYÊN HÌNH DẠNG VÀ ĐỘ SÁNG CỦA VỆT CHÉM (SLASH VFX) TRONG SUỐT HÀNH TRÌNH BAY
        var particles = GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in particles)
        {
            ps.gameObject.SetActive(true);
            var main = ps.main;
            main.loop = true; // Ép lặp lại liên tục
            main.stopAction = ParticleSystemStopAction.None; // Ngăn tự động tắt/hủy object
            main.startLifetime = 5.0f; // Kéo dài thời gian tồn tại hạt lên 5 giây để không bị tắt giữa đường
            ps.Clear(true);
            ps.Play(true);
        }

        var vfxGraphs = GetComponentsInChildren<UnityEngine.VFX.VisualEffect>(true);
        foreach (var ve in vfxGraphs)
        {
            ve.gameObject.SetActive(true);
            ve.Play();
        }

        var renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            r.enabled = true;
        }

        Destroy(gameObject, 5f);
    }

    private void Update()
    {
        transform.position += moveDirection * moveSpeed * Time.deltaTime;
        if (moveDirection.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(moveDirection);
        }

        // Đảm bảo tất cả Renderers của VFX luôn được bật hiển thị liên tục khi bay
        var renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            if (r != null && !r.enabled) r.enabled = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        ProcessDamage(other.gameObject);
    }

    private void OnTriggerStay(Collider other)
    {
        ProcessDamage(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        ProcessDamage(collision.gameObject);
    }

    private void ProcessDamage(GameObject target)
    {
        if (target == null) return;
        if (ownerBoss != null && (target.transform.IsChildOf(ownerBoss) || target == ownerBoss.gameObject)) return;

        Transform playerRoot = GetPlayerRoot(target.transform);
        if (playerRoot != null && !hitPlayers.Contains(playerRoot))
        {
            hitPlayers.Add(playerRoot);
            Vector3 knockback = moveDirection * 12f;
            EnemyDamageHelper.DealDamage(playerRoot, damage, knockback);
            Debug.Log($"[FinalBossProjectile] Đạn trúng Player '{playerRoot.name}' -> Trừ {damage} HP!");

            Destroy(gameObject);
        }
    }

    private Transform GetPlayerRoot(Transform t)
    {
        if (t == null) return null;
        Transform root = t.root;
        if (root.CompareTag("Player") || t.CompareTag("Player")) return root;

        var leo = t.GetComponentInParent<LeoPlayer>();
        if (leo != null) return leo.transform;

        var arthur = t.GetComponentInParent<ArthurPlayer>();
        if (arthur != null) return arthur.transform;

        var elena = t.GetComponentInParent<ElenaPlayer>() ?? (MonoBehaviour)t.GetComponentInParent<ElenaArcher>();
        if (elena != null) return elena.transform;

        var maya = t.GetComponentInParent<MayaPlayer>() ?? (MonoBehaviour)t.GetComponentInParent<MayaSupport>();
        if (maya != null) return maya.transform;

        var cc = t.GetComponentInParent<CharacterController>();
        if (cc != null) return cc.transform;

        var p = t.GetComponentInParent<IPlayerHUDTarget>() ?? t.GetComponentInChildren<IPlayerHUDTarget>();
        if (p != null && p is MonoBehaviour mono) return mono.transform;

        return null;
    }
}

public class ExpandingShockwaveRing : MonoBehaviour
{
    private FinalBossAI bossOwner;
    private float maxRadius;
    private float expandSpeed;
    private float damage;
    private float knockupForce;
    private LayerMask playerLayer;
    private float currentRadius;
    private HashSet<Transform> hitPlayers = new HashSet<Transform>();
    private LineRenderer line;

    public void Initialize(FinalBossAI owner, float maxRad, float speed, float dmg, float knockup, LayerMask layer)
    {
        bossOwner = owner;
        maxRadius = maxRad;
        expandSpeed = speed;
        damage = dmg;
        knockupForce = knockup;
        playerLayer = layer;
        currentRadius = 0.5f;

        // Visual expanding ring
        line = gameObject.AddComponent<LineRenderer>();
        line.positionCount = 51;
        line.useWorldSpace = true;
        line.startWidth = 0.35f;
        line.endWidth = 0.35f;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default");
        line.material = new Material(shader != null ? shader : Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply"));
        line.startColor = new Color(1f, 0.4f, 0f, 1f);
        line.endColor = new Color(1f, 0.4f, 0f, 1f);
    }

    private void Update()
    {
        currentRadius += expandSpeed * Time.deltaTime;

        // Draw expanding visual ring
        if (line != null)
        {
            Vector3[] points = new Vector3[51];
            for (int i = 0; i <= 50; i++)
            {
                float angle = i * (2f * Mathf.PI / 50f);
                points[i] = transform.position + new Vector3(Mathf.Cos(angle) * currentRadius, 0.15f, Mathf.Sin(angle) * currentRadius);
            }
            line.SetPositions(points);

            float alpha = Mathf.Clamp01(1f - (currentRadius / maxRadius));
            Color c = new Color(1f, 0.35f, 0f, alpha);
            line.startColor = c;
            line.endColor = c;
        }

        // Detect players touched by the expanding ring
        Collider[] hits = Physics.OverlapSphere(transform.position, currentRadius, playerLayer);
        foreach (var hit in hits)
        {
            Transform root = bossOwner != null ? bossOwner.GetPlayerRootPublic(hit.transform) : hit.transform.root;
            if (root != null && !hitPlayers.Contains(root))
            {
                hitPlayers.Add(root);

                // Calculate upward knockup + outward push
                Vector3 outward = (root.position - transform.position);
                outward.y = 0f;
                Vector3 force = Vector3.up * knockupForce + outward.normalized * 3.5f;

                EnemyDamageHelper.DealDamage(root, damage, force);
                Debug.Log($"[ExpandingShockwaveRing] Shockwave wave hit player: {root.name} (-{damage} HP & Knockup)");
            }
        }

        if (currentRadius >= maxRadius)
        {
            Destroy(gameObject);
        }
    }
}
