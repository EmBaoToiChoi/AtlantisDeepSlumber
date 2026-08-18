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
    public enum FinalBossState { Sitting, JumpDown, Grow, AerialLaser, DroneLaser, FireSpew, Idle, Chase, Attack, Shockwave, Hit, Dead }

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
    public NetworkVariable<int> aerialLaserCounter = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> droneLaserCounter = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> growCounter = new NetworkVariable<int>(
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
    public float growScaleMultiplier = 1.35f;
    public string growTriggerParam = "Shockwave";
    public GameObject growVFX;
    public AudioClip growSFX;

    // ─── Thiết lập Nhạc Chiến Đấu Boss (Boss Battle Music) ────
    [Header("Boss Battle Music (Nhạc Chiến Đấu Boss)")]
    [Tooltip("Âm thanh/Nhạc nền trận chiến duy trì lặp lại trong suốt trận đánh Final Boss")]
    public AudioClip bossBattleBGM;
    [Tooltip("Âm lượng nhạc trận chiến (0.0 đến 1.0)")]
    [Range(0f, 1f)] public float bossBattleBGMVolume = 0.8f;
    [Tooltip("Tự động lặp lại âm thanh trong suốt trận đấu")]
    public bool loopBattleBGM = true;
    [Tooltip("Thời gian Fade In khi bắt đầu chiến đấu (giây)")]
    public float battleBGMFadeInDuration = 1.5f;
    [Tooltip("Thời gian Fade Out khi Final Boss bị tiêu diệt (giây)")]
    public float battleBGMFadeOutDuration = 2.5f;
    private AudioSource battleBgmAudioSource;
    private Coroutine battleBgmFadeCoroutine;

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
    private bool hasTriggeredPhase2Grow = false;

    [Header("Phase 2 Aerial Genesis Laser (Bay Lên Cao & Bắn 5 Tia Genesis Breaker)")]
    public GameObject genesisLaserVFX;
    public float aerialLaserCooldown = 14f;
    public float aerialLaserCooldownTimer = 0f;
    public float aerialLaserDamage = 5f;
    public float aerialHeight = 5.5f;
    public float aerialFlyDuration = 4.5f;

    [Header("Phase 2 Drone Laser Barrage (12 Tia Laser Xoay Tròn Quanh Boss)")]
    public GameObject droneBlasterVFX;
    public float droneLaserCooldown = 18f;
    public float droneLaserCooldownTimer = 0f;
    public float droneLaserDamage = 5f;
    public float droneLaserDuration = 6.0f;
    public float droneLaserRotationSpeed = 35f;

    [Header("Fire Spew (Phun Lửa) Settings")]
    public float fireSpewInterval = 10f;
    public float fireSpewWindupDuration = 1.0f;
    public float fireSpewActiveDuration = 1.5f;
    public float fireSpewRadius = 7f;
    public float fireSpewDamage = 5f;
    public float fireSpewKnockback = 12f;
    public GameObject fireSpewVFX;
    public GameObject skyFireGroundImpactVFX;
    public AudioClip fireSpewSFX;
    public string fireSpewTriggerParam = "FireSpew";
    public float fireSpewCooldownTimer;

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
    private AerialLaserState stateAerialLaser;
    private DroneLaserState stateDroneLaser;
    private HitState stateHit;
    private DeadState stateDead;

    private Transform targetPlayer;
    private Vector3 lastTargetPos;
    private Vector3 targetVelocity;
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
        stateAerialLaser = new AerialLaserState(this);
        stateDroneLaser = new DroneLaserState(this);
        stateHit = new HitState(this);
        localHealth = maxHealth;
        stateDead = new DeadState(this);

#if UNITY_EDITOR
        if (rangedProjectilePrefab == null)
        {
            rangedProjectilePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/SpecialSkillsEffectsPack/AllEffects/EffectsSet_2(ScriptBased)/Effects/Effect_25_CriticalSlash/Effect_25_CriticalSlash.prefab");
        }
        if (shockwaveVFX == null)
        {
            shockwaveVFX = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/SpecialSkillsEffectsPack/AllEffects/EffectsSet_2(ScriptBased)/Effects/Effect_09_GloryShield/Effect_09_InfernoShield(IncludeHit).prefab")
                ?? UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/SpecialSkillsEffectsPack/AllEffects/EffectsSet_2(ScriptBased)/Effects/Effect_09_GloryShield/Effect_09_GloryShield.prefab");
        }
        if (growVFX == null)
        {
            growVFX = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/SpecialSkillsEffectsPack/AllEffects/EffectsSet_2(ScriptBased)/Effects/Effect_09_GloryShield/Effect_09_GloryShield.prefab");
        }
        if (fireSpewVFX == null)
        {
            fireSpewVFX = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/PixPlays/ElementalBeams/FireBeam/Version_BuiltIn/FireBeam.prefab");
        }
        if (skyFireGroundImpactVFX == null)
        {
            skyFireGroundImpactVFX = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/PixPlays/ElementalAOE/FireAOE/Version_BuiltIn/FireAoeVFX.prefab")
                ?? UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/SpecialSkillsEffectsPack/AllEffects/EffectsSet_2(ScriptBased)/Effects/Effect_08_FlameEruption/Effect_08_FlameEruption.prefab");
        }
        if (genesisLaserVFX == null)
        {
            genesisLaserVFX = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/SpecialSkillsEffectsPack/AllEffects/EffectsSet_2(ScriptBased)/Effects/Effect_50_WingsofGenesis/Effect_50_GenesisBreaker.prefab");
        }
        if (droneBlasterVFX == null)
        {
            droneBlasterVFX = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/SpecialSkillsEffectsPack/AllEffects/EffectsSet_2(ScriptBased)/Effects/Effect_50_WingsofGenesis/Effect_50_GenesisDroneBlaster.prefab");
        }
#endif
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
        aerialLaserCounter.OnValueChanged += (_, _) => {
            if (anim != null && attackTriggers != null && attackTriggers.Length > 2)
                anim.SetTrigger(attackTriggers[2]);
        };
        droneLaserCounter.OnValueChanged += (_, _) => {
            if (anim != null && attackTriggers != null && attackTriggers.Length > 2)
                anim.SetTrigger(attackTriggers[2]);
        };
        growCounter.OnValueChanged += (_, _) => {
            if (anim != null) anim.SetTrigger(growTriggerParam);
            PlayGrowVFX();
        };
        isBossActive.OnValueChanged += (oldVal, newVal) => { if (newVal) StartBattleMusic(); else StopBattleMusic(true); };
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
            hasTriggeredPhase2Grow = false;
            fireSpewCooldownTimer = 2.0f; // Sẵn sàng tung combo sớm ngay ở Phase 1
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
        aerialLaserCounter.OnValueChanged -= (_, _) => {
            if (anim != null && attackTriggers != null && attackTriggers.Length > 2)
                anim.SetTrigger(attackTriggers[2]);
        };
        droneLaserCounter.OnValueChanged -= (_, _) => {
            if (anim != null && attackTriggers != null && attackTriggers.Length > 2)
                anim.SetTrigger(attackTriggers[2]);
        };
        growCounter.OnValueChanged -= (_, _) => {
            if (anim != null) anim.SetTrigger(growTriggerParam);
            PlayGrowVFX();
        };
        StopBattleMusic(false);
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

        // Tự động đồng bộ cờ Cuồng Bạo trên tất cả Client khi máu giảm xuống <= 15%
        if (newVal <= (maxHealth * 0.15f) && newVal > 0f)
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
        if (IsDead || CurrentStateValue == FinalBossState.Sitting || CurrentStateValue == FinalBossState.JumpDown) return;

        // HOÀN TOÀN BẤT TỬ KHI ĐANG GỒNG BIẾN HÌNH QUA PHASE 2 (GROW STATE)!
        if (CurrentStateValue == FinalBossState.Grow)
        {
            Debug.Log("[FinalBossAI] Boss đang Gồng Biến Hình qua Phase 2 -> BẤT TỬ, miễn nhiễm mọi sát thương!");
            return;
        }

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

        // CHUYỂN PHASE 2 KHI MÁU XUỐNG DƯỚI/BẰNG 50% MÁU TỐI ĐA (maxHealth * 0.5f) -> GẦM THÉT VÀ PHÓNG TO!
        if (!hasTriggeredPhase2Grow && activeHp <= (maxHealth * 0.5f) && activeHp > 0f)
        {
            hasTriggeredPhase2Grow = true;
            ChangeState(FinalBossState.Grow);
            return;
        }

        // TRẠNG THÁI CUỒNG BẠO CUỐI TRẬN (LAST STAND) KHI MÁU <= 15% MÁU TỐI ĐA: TĂNG TỐC ĐÁNH +25% VÀ BẮN 5 VỆT CHÉM QUẠT RỘNG!
        if (!isLastStand && activeHp <= (maxHealth * 0.15f) && activeHp > 0f)
        {
            isLastStand = true;
            attackCooldown = 1.35f; // Tăng +25% tốc độ ra chiêu!
            Debug.Log($"[FinalBossAI] ===> KING ATLANTIS VÀO TRẠNG THÁI CUỒNG BẠO CUỐI TRẬN (<= 15% HP: {activeHp}/{maxHealth})! MẮT RỰC ĐỎ, TỐC ĐỘ +25%, BẮN 5 VỆT CHÉM QUẠT RỘNG!");
        }

        // Trigger stagger hit animation if not attacking/roaring/spewing/growing/dead/already hit
        if (CurrentStateValue != FinalBossState.Attack && CurrentStateValue != FinalBossState.Shockwave && CurrentStateValue != FinalBossState.FireSpew && CurrentStateValue != FinalBossState.Grow && CurrentStateValue != FinalBossState.Dead && CurrentStateValue != FinalBossState.Hit)
        {
            ChangeState(FinalBossState.Hit);
        }
    }

    public void ApplyStun(float duration)
    {
        if (CurrentStateValue == FinalBossState.Grow || CurrentStateValue == FinalBossState.Dead) return;

        EnemyStunVfxBehaviour.ApplyStunVfx(gameObject, duration, null, 3.5f, 2.5f);
        if (!isStandaloneMode && IsServer)
        {
            ApplyStunVfxClientRpc(duration);
        }
        TakeDamage(0f);
    }

    [ClientRpc]
    private void ApplyStunVfxClientRpc(float duration)
    {
        EnemyStunVfxBehaviour.ApplyStunVfx(gameObject, duration, null, 3.5f, 2.5f);
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
        if (aerialLaserCooldownTimer > 0) aerialLaserCooldownTimer -= Time.deltaTime;
        if (droneLaserCooldownTimer > 0) droneLaserCooldownTimer -= Time.deltaTime;
        if (fireSpewCooldownTimer > 0) fireSpewCooldownTimer -= Time.deltaTime;
        if (IsBossActive && !IsDead && CurrentStateValue != FinalBossState.Sitting && CurrentStateValue != FinalBossState.JumpDown)
        {
            StartBattleMusic();
        }

        // BẢO ĐẢM TỰ ĐỘNG KÍCH HOẠT PHASE 2 NGAY KHI MÁU <= 50% MÁU TỐI ĐA
        if (!hasTriggeredPhase2Grow && ActualCurrentHealth <= (maxHealth * 0.5f) && ActualCurrentHealth > 0f
            && CurrentStateValue != FinalBossState.Sitting && CurrentStateValue != FinalBossState.JumpDown
            && CurrentStateValue != FinalBossState.Dead && CurrentStateValue != FinalBossState.Grow)
        {
            hasTriggeredPhase2Grow = true;
            ChangeState(FinalBossState.Grow);
        }

        // Update target velocity tracking for predictive aiming (đoán hướng người chơi di chuyển)
        if (targetPlayer != null)
        {
            Vector3 currentPos = targetPlayer.position;
            if (Time.deltaTime > 0.0001f)
            {
                Vector3 instantVel = (currentPos - lastTargetPos) / Time.deltaTime;
                targetVelocity = Vector3.Lerp(targetVelocity, instantVel, Time.deltaTime * 12f);
            }
            lastTargetPos = currentPos;
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
            case FinalBossState.AerialLaser:
                currentFSMState = stateAerialLaser;
                break;
            case FinalBossState.DroneLaser:
                currentFSMState = stateDroneLaser;
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

        HashSet<Transform> hitRoots = new HashSet<Transform>();

        // 1. Quét cự ly trực tiếp tất cả Player còn sống trong bán kính shockwaveRadius
        var activePlayers = GetAllActivePlayers();
        foreach (var p in activePlayers)
        {
            if (p != null && !IsPlayerDeadOrInvisible(p))
            {
                float dist = Vector3.Distance(transform.position, p.position);
                if (dist <= shockwaveRadius + 0.8f)
                {
                    hitRoots.Add(p);
                    Vector3 knockbackDir = (p.position - transform.position);
                    if (knockbackDir.sqrMagnitude < 0.01f) knockbackDir = transform.forward;
                    knockbackDir.y = 0.5f; // Lift
                    Vector3 force = knockbackDir.normalized * shockwaveKnockback + Vector3.up * 8f;

                    // Gây sát thương và KÍCH HOẠT HIỆU ỨNG TÉ NGÃ (Kicked Stun)
                    EnemyDamageHelper.DealKickDamageWithStun(p, shockwaveDamage, force, shockwaveStunDuration);
                    Debug.Log($"[FinalBossAI] Gồng nổ hất tung trúng Player: {p.name} (-{shockwaveDamage} HP, Té Ngã & Choáng {shockwaveStunDuration}s)");
                }
            }
        }

        // 2. Quét dự phòng thêm bằng Physics.OverlapSphere
        Collider[] hits = Physics.OverlapSphere(transform.position, shockwaveRadius, playerLayer);
        foreach (var hit in hits)
        {
            Transform root = GetPlayerRoot(hit.GetComponent<Collider>().transform);
            if (root != null && !hitRoots.Contains(root))
            {
                hitRoots.Add(root);
                Vector3 knockbackDir = (root.position - transform.position);
                if (knockbackDir.sqrMagnitude < 0.01f) knockbackDir = transform.forward;
                knockbackDir.y = 0.5f;
                Vector3 force = knockbackDir.normalized * shockwaveKnockback + Vector3.up * 8f;

                EnemyDamageHelper.DealKickDamageWithStun(root, shockwaveDamage, force, shockwaveStunDuration);
                Debug.Log($"[FinalBossAI] Gồng nổ hất tung trúng Collider Player: {root.name}");
            }
        }
    }

    public Vector3 GetPredictedTargetPosition(Transform target, float projSpeed)
    {
        if (target == null) return transform.position + transform.forward * 10f + Vector3.up * 0.7f;

        Vector3 playerPos = target.position + Vector3.up * 0.7f;
        Vector3 vel = targetVelocity;

        var cc = target.GetComponentInParent<CharacterController>() ?? target.GetComponentInChildren<CharacterController>();
        if (cc != null && cc.velocity.sqrMagnitude > 0.1f)
        {
            vel = cc.velocity;
        }
        else
        {
            var rb = target.GetComponentInParent<Rigidbody>() ?? target.GetComponentInChildren<Rigidbody>();
            if (rb != null && rb.linearVelocity.sqrMagnitude > 0.1f)
            {
                vel = rb.linearVelocity;
            }
        }

        // Nếu Player đứng yên thì ngắm thẳng vào vị trí Player
        if (vel.sqrMagnitude < 0.05f) return playerPos;

        float dist = Vector3.Distance(transform.position, playerPos);
        float timeToHit = dist / Mathf.Max(projSpeed, 8f);
        // Giới hạn thời gian đón đầu tối đa 0.85s để không bắn lệch ra ngoài tầm
        timeToHit = Mathf.Clamp(timeToHit, 0.05f, 0.85f);

        Vector3 predictedPos = playerPos + vel * timeToHit;
        predictedPos.y = playerPos.y; // Giữ nguyên độ cao ngực/bụng

        return predictedPos;
    }

    public void SpawnRangedProjectile()
    {
        if (rangedProjectilePrefab == null)
        {
#if UNITY_EDITOR
            rangedProjectilePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/SpecialSkillsEffectsPack/AllEffects/EffectsSet_2(ScriptBased)/Effects/Effect_25_CriticalSlash/Effect_25_CriticalSlash.prefab");
#endif
            if (rangedProjectilePrefab == null)
            {
                Debug.LogWarning("[FinalBossAI] Ô 'Ranged Projectile Prefab' chưa được gán trong Inspector trên King Atlantis!");
                return;
            }
        }

        // TÍNH TOÁN ĐOÁN TRƯỚC HƯỚNG DI CHUYỂN CỦA PLAYER (PREDICTIVE AIMING / LEAD TARGETING)
        Vector3 targetPos = GetPredictedTargetPosition(targetPlayer, projectileSpeed);

        // Căn chỉnh điểm xuất phát phù hợp theo độ phồng to của Boss
        float scaleY = transform.localScale.y;
        Vector3 spawnPos = projectileSpawnPoint != null ? projectileSpawnPoint.position : transform.position + Vector3.up * (1.2f * scaleY) + transform.forward * (1.0f * scaleY);

        Vector3 centerDir = (targetPos - spawnPos).normalized;
        centerDir.y = 0f; // Giữ vệt chém bay thẳng ngang mặt sàn
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
        if (rangedProjectilePrefab == null)
        {
#if UNITY_EDITOR
            rangedProjectilePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/SpecialSkillsEffectsPack/AllEffects/EffectsSet_2(ScriptBased)/Effects/Effect_25_CriticalSlash/Effect_25_CriticalSlash.prefab");
#endif
            if (rangedProjectilePrefab == null) return;
        }

        // Bình thường bắn 3 vệt chém (0°, -15°, 15°). Khi Cuồng Bạo (<=100 HP) bắn hẳn 5 vệt chém quạt rộng!
        float[] angles = isLastStand ? new float[] { 0f, -15f, 15f, -30f, 30f } : new float[] { 0f, -15f, 15f };

        foreach (float angle in angles)
        {
            Vector3 shootDir = Quaternion.Euler(0, angle, 0) * centerDir;
            Quaternion rot = Quaternion.LookRotation(shootDir) * Quaternion.Euler(projectileRotationOffset);

            // Vẽ tia cảnh báo đường bắn đỏ rực trong Scene view
            Debug.DrawRay(spawnPos, shootDir * 25f, Color.red, 3.0f);

            GameObject proj = Instantiate(rangedProjectilePrefab, spawnPos, rot);
            // Thu nhỏ scale vừa vặn, sắc nét và thẩm mỹ (0.4x kích thước gốc)
            proj.transform.localScale = rangedProjectilePrefab.transform.localScale * 0.4f;

            var comp = proj.GetComponent<FinalBossProjectile>() ?? proj.AddComponent<FinalBossProjectile>();
            comp.Initialize(shootDir, projectileSpeed, projectileDamage, transform);
        }

        Debug.Log($"[FinalBossAI] ===> ĐỒNG BỘ NETWORK: BẮN {angles.Length} VỆT CHÉM SLASH VFX '{rangedProjectilePrefab.name}' THÀNH CÔNG từ vị trí {spawnPos}!");
    }

    public Transform GetPlayerRootPublic(Transform t)
    {
        return GetPlayerRoot(t);
    }

    private void PlayShockwaveVFX()
    {
        Vector3 spawnPos = transform.position + Vector3.up * 0.5f;
        CameraShakeHelper.Shake(1.2f, 1.4f); // Rung dữ dội toàn màn hình khi gồng nổ hất tung

        if (shockwaveVFX != null)
        {
            // Khởi tạo VFX gồng hất tung (Effect_09_InfernoShield) gắn theo vị trí Boss và tự động nở to dần theo bán kính
            GameObject vfx = Instantiate(shockwaveVFX, spawnPos, transform.rotation, transform);
            var expandComp = vfx.GetComponent<ExpandingShockwaveVFX>() ?? vfx.AddComponent<ExpandingShockwaveVFX>();
            expandComp.Initialize(shockwaveRadius, shockwaveDuration);
            Destroy(vfx, shockwaveDuration + 2.5f);
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

    public List<Transform> GetAllActivePlayers()
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

            // Đứng từ xa bắn chiêu tầm xa hoặc dùng combo kết hợp chưởng lửa + chém hoặc Bay Bắn Laser Phase 2
            if (boss.attackCooldownTimer <= 0)
            {
                if (boss.AgentReady) boss.agent.isStopped = true;
                boss.SetSpeedNet(0f);

                if (boss.isEnraged || boss.ActualCurrentHealth <= (boss.maxHealth * 0.5f))
                {
                    // PHASE 2: HOÀN TOÀN KHÔNG CÓ CHƯỞNG LỬA (NO FIRE SPEW IN PHASE 2)
                    // CHIÊU 1: Triệu hồi 3 cụm Drone Blaster (12 tia laser xanh xoay tròn 360° quanh Boss)
                    if (boss.droneLaserCooldownTimer <= 0)
                    {
                        boss.ChangeState(FinalBossState.DroneLaser);
                        return;
                    }

                    // CHIÊU 2: Bay Lên Cao Bắn 5 Tia Laser Genesis Breaker
                    if (boss.aerialLaserCooldownTimer <= 0)
                    {
                        boss.ChangeState(FinalBossState.AerialLaser);
                        return;
                    }

                    // CHIÊU 3: Đòn đánh chém kiếm phát ra các vệt chém tầm xa Critical Slash
                    boss.ChangeState(FinalBossState.Attack);
                    return;
                }

                // PHASE 1: Dùng Chưởng Lửa Lên Trời & Giáng Cột Lửa hoặc Đòn Đánh Cận Chiến
                if (boss.fireSpewCooldownTimer <= 0)
                {
                    boss.ChangeState(FinalBossState.FireSpew);
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
                // Xoay hướng mặt Boss đoán trước hướng di chuyển của người chơi
                Vector3 aimPos = boss.GetPredictedTargetPosition(boss.targetPlayer, boss.projectileSpeed);
                boss.RotateTowards(aimPos);
            }

            // BẮN ĐẠN KIẾM TẦM XA (CRITICAL SLASH) KHI Ở PHASE 2 (MÁU <= 50% HOẶC ENRAGED)
            if (!hasSpawnedProjectile)
            {
                spawnTimer -= Time.deltaTime;
                if (spawnTimer <= 0f)
                {
                    hasSpawnedProjectile = true;
                    if (boss.isEnraged || boss.ActualCurrentHealth <= (boss.maxHealth * 0.5f))
                    {
                        boss.SpawnRangedProjectile();
                    }
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
            boss.hasTriggeredPhase2Grow = true;
            startScale = boss.transform.localScale;
            // Tăng kích thước Phase 2 vừa vặn, oai vệ (x1.35) không bị to quá choán hết bản đồ
            float mult = Mathf.Clamp(boss.growScaleMultiplier, 1.2f, 1.45f);
            targetScale = startScale * mult;

            if (boss.AgentReady)
            {
                boss.agent.isStopped = true;
                boss.agent.velocity = Vector3.zero;
            }
            boss.SetSpeedNet(0f);

            if (!boss.isStandaloneMode && boss.IsServer)
            {
                boss.growCounter.Value++;
            }
            else if (boss.isStandaloneMode && boss.anim != null)
            {
                boss.anim.SetTrigger(boss.growTriggerParam);
            }

            boss.PlayGrowVFX();
            Debug.Log($"[FinalBossAI] PHASE 2 KÍCH HOẠT (<= 50% HP: {boss.ActualCurrentHealth}/{boss.maxHealth})! KING ATLANTIS GẦM THÉT VÀ PHÓNG TO THÂN THỂ x{mult}!");
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
                
                // NGAY KHI VỪA PHÓNG TO PHASE 2 THÀNH CÔNG -> BAY VÚT LÊN CAO VÀ XẢ 5 TIA GENESIS BREAKER!
                boss.ChangeState(FinalBossState.AerialLaser);
            }
        }

        public void Exit()
        {
            if (boss.anim != null) boss.anim.ResetTrigger(boss.growTriggerParam);
            if (boss.AgentReady) boss.agent.isStopped = false;
        }
    }

    private class AerialLaserState : IEnemyState
    {
        private FinalBossAI boss;
        private float stateTimer;
        private Vector3 groundStartPos;
        private Vector3 airTargetPos;
        private bool hasFiredBeams = false;

        public AerialLaserState(FinalBossAI boss) { this.boss = boss; }

        public void Enter()
        {
            hasFiredBeams = false;
            stateTimer = 0f;

            groundStartPos = boss.transform.position;
            airTargetPos = groundStartPos + Vector3.up * boss.aerialHeight;

            if (boss.AgentReady)
            {
                boss.agent.isStopped = true;
                boss.agent.velocity = Vector3.zero;
                boss.agent.enabled = false;
            }
            boss.SetSpeedNet(0f);

            if (!boss.isStandaloneMode && boss.IsServer)
            {
                boss.aerialLaserCounter.Value++;
            }

            // Kích hoạt hoạt ảnh gầm tung cánh
            if (boss.anim != null && boss.attackTriggers != null && boss.attackTriggers.Length > 2)
            {
                boss.anim.SetTrigger(boss.attackTriggers[2]);
            }
        }

        public void Update()
        {
            stateTimer += Time.deltaTime;
            float totalDuration = boss.aerialFlyDuration;

            // GIAI ĐOẠN 1: Bay vút lên không trung (0s -> 1.0s)
            if (stateTimer <= 1.0f)
            {
                float t = Mathf.SmoothStep(0f, 1f, stateTimer / 1.0f);
                boss.transform.position = Vector3.Lerp(groundStartPos, airTargetPos, t);
                if (boss.targetPlayer != null)
                {
                    boss.RotateTowards(boss.targetPlayer.position);
                }
            }
            // GIAI ĐOẠN 2: Lơ lửng trên không và BẮN 5 TIA GENESIS BREAKER KHỔNG LỒ (1.0s -> 3.5s)
            else if (stateTimer <= 3.5f)
            {
                boss.transform.position = airTargetPos + Vector3.up * (Mathf.Sin(Time.time * 3f) * 0.12f);

                if (boss.targetPlayer != null)
                {
                    Vector3 aimPos = boss.GetPredictedTargetPosition(boss.targetPlayer, 30f);
                    boss.RotateTowards(aimPos);
                }

                if (!hasFiredBeams)
                {
                    hasFiredBeams = true;
                    boss.ExecuteAerialGenesisBeams();
                }
            }
            // GIAI ĐOẠN 3: Hạ cánh đáp xuống mặt đất (3.5s -> 4.5s)
            else if (stateTimer <= totalDuration)
            {
                float t = Mathf.SmoothStep(0f, 1f, (stateTimer - 3.5f) / (totalDuration - 3.5f));
                boss.transform.position = Vector3.Lerp(airTargetPos, groundStartPos, t);
            }
            // HOÀN TẤT KỸ NĂNG
            else
            {
                boss.transform.position = groundStartPos;
                boss.aerialLaserCooldownTimer = boss.aerialLaserCooldown;
                if (boss.targetPlayer != null) boss.ChangeState(FinalBossState.Chase);
                else boss.ChangeState(FinalBossState.Idle);
            }
        }

        public void Exit()
        {
            boss.transform.position = groundStartPos;
            boss.aerialLaserCooldownTimer = boss.aerialLaserCooldown;
            if (boss.agent != null)
            {
                boss.agent.enabled = true;
                boss.SnapToNavMesh();
            }
            CameraShakeHelper.Shake(0.5f, 1.2f);
        }
    }

    private class DroneLaserState : IEnemyState
    {
        private FinalBossAI boss;
        private float stateTimer;

        public DroneLaserState(FinalBossAI boss) { this.boss = boss; }

        public void Enter()
        {
            stateTimer = boss.droneLaserDuration + 0.4f;

            if (boss.AgentReady)
            {
                boss.agent.isStopped = true;
                boss.agent.velocity = Vector3.zero;
            }
            boss.SetSpeedNet(0f);

            // Kích hoạt hoạt ảnh gầm gồng năng lượng
            if (boss.anim != null && boss.attackTriggers != null && boss.attackTriggers.Length > 2)
            {
                boss.anim.SetTrigger(boss.attackTriggers[2]);
            }

            // Kích hoạt triệu hồi cụm Drone Blaster xoay tròn đồng bộ mạng
            boss.ExecuteDroneBarrage();
        }

        public void Update()
        {
            stateTimer -= Time.deltaTime;

            if (boss.targetPlayer != null)
            {
                boss.RotateTowards(boss.targetPlayer.position);
            }

            if (stateTimer <= 0)
            {
                boss.droneLaserCooldownTimer = boss.droneLaserCooldown;
                if (boss.targetPlayer != null) boss.ChangeState(FinalBossState.Chase);
                else boss.ChangeState(FinalBossState.Idle);
            }
        }

        public void Exit()
        {
            boss.droneLaserCooldownTimer = boss.droneLaserCooldown;
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

            if (!boss.isStandaloneMode && boss.IsServer)
            {
                boss.PlayBattleBGMClientRpc(false);
            }
            else
            {
                boss.StopBattleMusic(true);
            }
            Debug.Log("[FinalBossAI] Final Boss is dead!");
            Destroy(boss.gameObject, 6f);
        }

        public void Update() { }
        public void Exit() { }
    }

    private class FireSpewState : IEnemyState
    {
        private FinalBossAI boss;
        private float stateTimer;

        public FireSpewState(FinalBossAI boss) { this.boss = boss; }

        public void Enter()
        {
            stateTimer = 1.8f;

            if (boss.AgentReady)
            {
                boss.agent.isStopped = true;
                boss.agent.velocity = Vector3.zero;
            }
            boss.SetSpeedNet(0f);

            // Kích hoạt hoạt ảnh gồng bắn lửa
            if (!boss.isStandaloneMode && boss.IsServer)
            {
                boss.fireSpewCounter.Value++;
            }
            else if (boss.isStandaloneMode && boss.anim != null)
            {
                boss.anim.SetTrigger(boss.fireSpewTriggerParam);
            }

            // Bắn chưởng lửa lên trời và gọi các cột lửa giáng xuống vị trí Player
            boss.ExecuteSkyFireBarrage();
        }

        public void Update()
        {
            stateTimer -= Time.deltaTime;

            if (boss.targetPlayer != null)
            {
                // Xoay hướng đón đầu Player trong suốt nhịp tung chiêu
                Vector3 aimPos = boss.GetPredictedTargetPosition(boss.targetPlayer, boss.projectileSpeed);
                boss.RotateTowards(aimPos);
            }

            if (stateTimer <= 0)
            {
                boss.fireSpewCooldownTimer = boss.fireSpewInterval;
                if (boss.targetPlayer != null) boss.ChangeState(FinalBossState.Chase);
                else boss.ChangeState(FinalBossState.Idle);
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
    //  SKY FIRE BEAM BARRAGE (CHƯỞNG LỬA LÊN TRỜI & 5-6 TIA GIÁNG XUỐNG PLAYER)
    // ══════════════════════════════════════════════════════════

    public void ExecuteSkyFireBarrage()
    {
        bool auth = isStandaloneMode || (IsNetworkActive && IsServer);
        if (!auth) return;

        // 1. Thu thập chính xác 5 - 6 vị trí mục tiêu Player đang đứng
        Vector3[] targetPositions = CalculateSkyFireTargetPositions(6);

        if (!isStandaloneMode && IsServer)
        {
            ExecuteSkyFireBarrageClientRpc(targetPositions);
        }
        else
        {
            StartCoroutine(ExecuteSkyFireBarrageRoutine(targetPositions));
        }
    }

    [ClientRpc]
    private void ExecuteSkyFireBarrageClientRpc(Vector3[] targetPositions)
    {
        StartCoroutine(ExecuteSkyFireBarrageRoutine(targetPositions));
    }

    private Vector3[] CalculateSkyFireTargetPositions(int totalCount = 6)
    {
        List<Vector3> targets = new List<Vector3>();
        var activePlayers = GetAllActivePlayers();
        var livingPlayers = new List<Transform>();
        foreach (var p in activePlayers)
        {
            if (p != null && !IsPlayerDeadOrInvisible(p)) livingPlayers.Add(p);
        }

        // 1. MỖI PLAYER CHỈ NHẬN DUY NHẤT 1 TIA LỬA NGAY TẠI VỊ TRÍ ĐỨNG (1 PLAYER = 1 TIA, 4 PLAYERS = 4 TIA)
        foreach (var p in livingPlayers)
        {
            if (p != null)
            {
                targets.Add(p.position);
            }
        }

        // 2. CÁC TIA LỬA CÒN LẠI (CHO ĐỦ totalCount = 6 TIA) SẼ ĐƯỢC RANDOM RẢI RÁC TRONG ĐẤU TRƯỜNG
        Vector3 centerPoint = livingPlayers.Count > 0 ? livingPlayers[0].position : transform.position;
        while (targets.Count < totalCount)
        {
            // Random vị trí ngẫu nhiên trong bán kính 3.5m đến 13m quanh đấu trường
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float dist = Random.Range(3.5f, 13.0f);
            Vector3 randomPos = centerPoint + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
            randomPos.y = transform.position.y;
            targets.Add(randomPos);
        }

        return targets.ToArray();
    }

    private IEnumerator ExecuteSkyFireBarrageRoutine(Vector3[] targetPositions)
    {
        if (fireSpewVFX == null)
        {
#if UNITY_EDITOR
            fireSpewVFX = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/PixPlays/ElementalBeams/FireBeam/Version_BuiltIn/FireBeam.prefab");
#endif
            if (fireSpewVFX == null) yield break;
        }

        // BƯỚC 1: BẮN TIA LỬA THẲNG LÊN TRỜI TỪ NGỰC/MIỆNG BOSS (THON GỌN, SẮC NÉT)
        Vector3 bossMouthPos = transform.position + Vector3.up * 1.6f;
        Vector3 skyTarget = bossMouthPos + Vector3.up * 22.0f;
        Quaternion upRot = Quaternion.Euler(-90f, 0f, 0f);
        GameObject upBeam = Instantiate(fireSpewVFX, bossMouthPos, upRot);
        TriggerVfxPlayback(upBeam, bossMouthPos, skyTarget, 1.5f, 0.8f, 0.45f);

        CameraShakeHelper.Shake(0.8f, 1.0f);
        if (fireSpewSFX != null) AudioSource.PlayClipAtPoint(fireSpewSFX, bossMouthPos, 1.0f);
        Destroy(upBeam, 1.5f);

        // Chờ tia lửa bay lên tầng mây (0.35 giây)
        yield return new WaitForSeconds(0.35f);

        // BƯỚC 2: CÁC CỘT LỬA GIÁNG TỪ TRÊN TRỜI CẮM SÂU XUỐNG ĐẤT (THON GỌN, ĐẸP MẮT)
        for (int i = 0; i < targetPositions.Length; i++)
        {
            Vector3 groundPos = targetPositions[i];
            Vector3 skySpawnPos = groundPos + Vector3.up * 20.0f;
            Quaternion downRot = Quaternion.Euler(90f, 0f, 0f);

            // 1. Cột lửa cắm từ trời xuống (Scale 0.45x thon gọn)
            GameObject downBeam = Instantiate(fireSpewVFX, skySpawnPos, downRot);
            TriggerVfxPlayback(downBeam, skySpawnPos, groundPos, 1.8f, 0.9f, 0.45f);

            // Gắn vùng gây sát thương va chạm
            var damageZone = downBeam.GetComponent<FireBeamDamageZone>() ?? downBeam.AddComponent<FireBeamDamageZone>();
            damageZone.Initialize(this, fireSpewDamage, fireSpewKnockback);

            // 2. Vùng nổ mặt đất (Scale 0.5x vừa vặn)
            GameObject groundImpactPrefab = skyFireGroundImpactVFX != null ? skyFireGroundImpactVFX : fireSpewVFX;
            if (groundImpactPrefab != null)
            {
                GameObject groundImpact = Instantiate(groundImpactPrefab, groundPos + Vector3.up * 0.1f, Quaternion.identity);
                TriggerVfxPlayback(groundImpact, groundPos, groundPos + Vector3.up, 2.5f, 1.2f, 0.5f);
                Destroy(groundImpact, 2.5f);
            }

            // Rung giật camera mặt đất tại điểm nổ
            CameraShakeHelper.ShakeAtPosition(groundPos, 0.6f, 1.3f, 35f);

            // Gây sát thương nổ diện rộng tại mặt đất (-5 HP)
            bool auth = isStandaloneMode || (IsNetworkActive && IsServer);
            if (auth)
            {
                ApplySkyFireImpactDamage(groundPos, fireSpewDamage, 2.0f);
            }

            Destroy(downBeam, 1.8f);

            // Giãn cách nhịp rơi giữa các tia chưởng (0.12s)
            yield return new WaitForSeconds(0.12f);
        }
    }

    private void ApplySkyFireImpactDamage(Vector3 center, float dmg, float radius)
    {
        var players = GetAllActivePlayers();
        foreach (var p in players)
        {
            if (p == null || IsPlayerDeadOrInvisible(p)) continue;
            float dist = Vector3.Distance(center, p.position);
            if (dist <= radius)
            {
                Vector3 knockbackDir = (p.position - center).normalized;
                if (knockbackDir.sqrMagnitude < 0.01f) knockbackDir = Vector3.up;
                Vector3 force = knockbackDir * fireSpewKnockback + Vector3.up * 5f;
                EnemyDamageHelper.DealDamage(p, dmg, force);
                Debug.Log($"[FinalBossAI] Tia lửa giáng trúng Player '{p.name}' (-{dmg} HP)!");
            }
        }
    }

    public bool isStandaloneModePublic => isStandaloneMode;
    public bool IsNetworkActivePublic => IsNetworkActive;
    public bool IsServerPublic => IsServer;
    public bool IsPlayerDeadOrInvisiblePublic(Transform p) => IsPlayerDeadOrInvisible(p);

    public void ExecuteAerialGenesisBeams()
    {
        if (!isStandaloneMode && IsSpawned)
        {
            if (IsServer)
            {
                ExecuteAerialGenesisBeamsClientRpc();
            }
        }
        else
        {
            ExecuteAerialGenesisBeamsLocally();
        }
    }

    [ClientRpc]
    private void ExecuteAerialGenesisBeamsClientRpc()
    {
        ExecuteAerialGenesisBeamsLocally();
    }

    public IEnumerator ExecuteAerialGenesisBeamsClientRoutine()
    {
        ExecuteAerialGenesisBeamsLocally();
        yield return null;
    }

    private void ExecuteAerialGenesisBeamsLocally()
    {
        if (genesisLaserVFX == null)
        {
#if UNITY_EDITOR
            genesisLaserVFX = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/SpecialSkillsEffectsPack/AllEffects/EffectsSet_2(ScriptBased)/Effects/Effect_50_WingsofGenesis/Effect_50_GenesisBreaker.prefab");
#endif
            if (genesisLaserVFX == null) return;
        }

        // Bắn 5 tia Laser Genesis Breaker theo hình quạt quanh thân Boss, chĩa chéo cắm xuống mặt đất
        float[] angles = new float[] { -24f, -12f, 0f, 12f, 24f };
        Vector3[] localOffsets = new Vector3[]
        {
            new Vector3(-3.0f, 0.4f, 0.6f),
            new Vector3(-1.5f, 0.8f, 1.0f),
            new Vector3(0f, 1.2f, 1.4f),
            new Vector3(1.5f, 0.8f, 1.0f),
            new Vector3(3.0f, 0.4f, 0.6f)
        };

        for (int i = 0; i < angles.Length; i++)
        {
            float angle = angles[i];
            Vector3 offset = localOffsets[i];
            Vector3 spawnPoint = transform.TransformPoint(offset);
            // Chếch góc 18° cắm thẳng xuống mặt đất nơi Player đang đứng
            Vector3 shootDir = Quaternion.Euler(18f, angle, 0) * transform.forward;
            Quaternion rot = Quaternion.LookRotation(shootDir);

            GameObject beam = Instantiate(genesisLaserVFX, spawnPoint, rot);
            beam.transform.localScale = new Vector3(1.2f, 1.2f, 2.5f); // Kéo dài tia laser đâm xuống mặt đất
            TriggerVfxPlayback(beam, spawnPoint, spawnPoint + shootDir * 45f, 3.2f, 2.2f, 1.0f, true);

            // Gắn vùng gây sát thương va chạm liên tục (-5 HP)
            var damageZone = beam.GetComponent<GenesisBeamDamageZone>() ?? beam.AddComponent<GenesisBeamDamageZone>();
            damageZone.Initialize(this, aerialLaserDamage, 8f, 45f, 2.5f);

            Destroy(beam, 3.0f);
        }

        CameraShakeHelper.Shake(2.0f, 1.0f);
        Debug.Log("[FinalBossAI] ===> XẢ 5 TIA GENESIS BREAKER LASER CẮM XUỐNG MẶT ĐẤT (LOOP = TRUE, -5 HP)!");
    }

    public void ExecuteDroneBarrage()
    {
        if (!isStandaloneMode && IsSpawned)
        {
            if (IsServer)
            {
                droneLaserCounter.Value++;
                ExecuteDroneBarrageClientRpc();
            }
        }
        else
        {
            ExecuteDroneBarrageLocally();
        }
    }

    [ClientRpc]
    private void ExecuteDroneBarrageClientRpc()
    {
        ExecuteDroneBarrageLocally();
    }

    private void ExecuteDroneBarrageLocally()
    {
        if (droneBlasterVFX == null)
        {
#if UNITY_EDITOR
            droneBlasterVFX = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/SpecialSkillsEffectsPack/AllEffects/EffectsSet_2(ScriptBased)/Effects/Effect_50_WingsofGenesis/Effect_50_GenesisDroneBlaster.prefab");
#endif
            if (droneBlasterVFX == null) return;
        }

        Vector3 centerPos = transform.position + Vector3.up * 1.8f;
        GameObject holder = new GameObject("RotatingDroneCluster");
        holder.transform.position = centerPos;
        holder.transform.rotation = Quaternion.identity;

        var rotator = holder.AddComponent<RotatingDroneBlasterGroup>();
        rotator.Initialize(this, droneLaserRotationSpeed, droneLaserDuration, droneLaserDamage);

        // Sinh ra 3 cụm Drone Blaster xếp cách đều 120° quanh Boss (Mỗi cụm có 4 tia -> Tổng 12 tia laser!)
        float[] angles = new float[] { 0f, 120f, 240f };
        float radius = 4.6f; // Bán kính cách tâm Boss

        foreach (float angle in angles)
        {
            Vector3 offsetDir = Quaternion.Euler(0, angle, 0) * Vector3.forward;
            Vector3 spawnPos = centerPos + offsetDir * radius;
            // Chếch góc 12° cắm thẳng xuống mặt đất để tia laser quét dọc trên sàn đấu
            Quaternion spawnRot = Quaternion.LookRotation(offsetDir) * Quaternion.Euler(12f, 0f, 0f);

            GameObject droneUnit = Instantiate(droneBlasterVFX, spawnPos, spawnRot, holder.transform);
            droneUnit.transform.localScale = new Vector3(1.1f, 1.1f, 2.0f); // Kéo dài tia laser chạm xuống đất
            TriggerVfxPlayback(droneUnit, spawnPos, spawnPos + offsetDir * 40f, droneLaserDuration, 2.0f, 1.0f, true);

            rotator.RegisterDroneUnit(droneUnit.transform);
        }

        CameraShakeHelper.Shake(1.2f, 0.8f);
        Debug.Log("[FinalBossAI] ===> TRIỆU HỒI 3 CỤM DRONE BLASTER (12 TIA LASER CẮM XUỐNG ĐẤT, LOOP = TRUE) QUAY 360°!");
    }

    /// <summary>
    /// Kích hoạt đúng cơ chế phát của tất cả các hệ thống VFX (PixPlays BaseVfx, ParticleSystems) với Loop và kích thước tùy chỉnh
    /// </summary>
    private void TriggerVfxPlayback(GameObject vfx, Vector3 source, Vector3 target, float duration = 2.5f, float radius = 3.0f, float scaleMultiplier = 1.0f, bool forceLoop = true)
    {
        if (vfx == null) return;

        vfx.transform.localScale = Vector3.one * scaleMultiplier;

        // 1. Nếu là VFX của gói PixPlays (FireBeam, FireAoeVFX...) -> Khởi tạo VfxData và gọi Play(data) để kích hoạt Coroutine bắn chùm tia!
        var baseVfxList = vfx.GetComponentsInChildren<PixPlays.ElementalVFX.BaseVfx>(true);
        foreach (var bv in baseVfxList)
        {
            if (bv != null)
            {
                var data = new PixPlays.ElementalVFX.VfxData(source, target, duration, radius);
                bv.Play(data);
            }
        }

        // 2. Kích hoạt và ép Loop toàn bộ ParticleSystem bên trong
        var particles = vfx.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in particles)
        {
            if (ps != null)
            {
                ps.gameObject.SetActive(true);
                var main = ps.main;
                if (forceLoop)
                {
                    main.loop = true; // Ép Loop liên tục
                    main.stopAction = ParticleSystemStopAction.None;
                    main.startLifetime = Mathf.Max(main.startLifetime.constant, duration + 1.0f);
                }
                var em = ps.emission;
                em.enabled = true;
                ps.Clear(true);
                ps.Play(true);
            }
        }

        var vfxGraphs = vfx.GetComponentsInChildren<UnityEngine.VFX.VisualEffect>(true);
        foreach (var ve in vfxGraphs)
        {
            if (ve != null)
            {
                ve.gameObject.SetActive(true);
                ve.Play();
            }
        }

        var renderers = vfx.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            if (r != null) r.enabled = true;
        }
    }

    private void PlayGrowVFX()
    {
        Vector3 spawnPos = transform.position + Vector3.up * 1.2f;
        CameraShakeHelper.Shake(growDuration, 1.1f); // Rung dữ dội màn hình khi gầm thét biến hình

        GameObject prefabToUse = growVFX != null ? growVFX : shockwaveVFX;

#if UNITY_EDITOR
        if (prefabToUse == null)
        {
            prefabToUse = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/SpecialSkillsEffectsPack/AllEffects/EffectsSet_2(ScriptBased)/Effects/Effect_09_GloryShield/Effect_09_GloryShield.prefab");
        }
#endif

        if (prefabToUse != null)
        {
            // Gắn VFX trực tiếp theo thân người Boss ở vị trí ngực bụng
            GameObject vfx = Instantiate(prefabToUse, spawnPos, transform.rotation, transform);
            vfx.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            vfx.transform.localRotation = Quaternion.identity;

            // Đặt kích thước bao bọc vừa vặn xung quanh cơ thể của Boss
            vfx.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);

            // Ép Loop toàn bộ ParticleSystems để phát sáng liên tục trong 3s biến hình
            var particles = vfx.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in particles)
            {
                if (ps == null) continue;
                ps.gameObject.SetActive(true);
                var main = ps.main;
                main.loop = true; // Ép Loop liên tục
                main.stopAction = ParticleSystemStopAction.None;
                main.startLifetime = Mathf.Max(main.startLifetime.constant, growDuration + 1.0f);
                var em = ps.emission;
                em.enabled = true;
                ps.Clear(true);
                ps.Play(true);
            }

            var vfxGraphs = vfx.GetComponentsInChildren<UnityEngine.VFX.VisualEffect>(true);
            foreach (var ve in vfxGraphs)
            {
                if (ve != null)
                {
                    ve.gameObject.SetActive(true);
                    ve.Play();
                }
            }

            var renderers = vfx.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r != null) r.enabled = true;
            }

            Destroy(vfx, growDuration + 0.8f);
        }

        if (growSFX != null)
        {
            AudioSource.PlayClipAtPoint(growSFX, spawnPos, 1.0f);
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
    // ══════════════════════════════════════════════════════════
    //  BOSS BATTLE MUSIC (NHẠC CHIẾN ĐẤU DUY TRÌ)
    // ══════════════════════════════════════════════════════════

    [ClientRpc]
    private void PlayBattleBGMClientRpc(bool play)
    {
        if (play) StartBattleMusic();
        else StopBattleMusic(true);
    }

    public void StartBattleMusic()
    {
        if (bossBattleBGM == null || IsDead) return;

        if (battleBgmAudioSource == null)
        {
            battleBgmAudioSource = gameObject.AddComponent<AudioSource>();
            battleBgmAudioSource.playOnAwake = false;
            battleBgmAudioSource.spatialBlend = 0f; // 2D BGM toàn diện cho toàn sàn đấu
        }

        // Đảm bảo LUÔN LUÔN BẬT LOOP để nhạc duy trì lặp lại vô tận trong suốt trận đấu
        battleBgmAudioSource.clip = bossBattleBGM;
        battleBgmAudioSource.loop = true;

        if (!battleBgmAudioSource.isPlaying)
        {
            battleBgmAudioSource.volume = 0f;
            battleBgmAudioSource.Play();
            if (battleBgmFadeCoroutine != null) StopCoroutine(battleBgmFadeCoroutine);
            battleBgmFadeCoroutine = StartCoroutine(RoutineFadeBattleMusic(bossBattleBGMVolume, battleBGMFadeInDuration));
            Debug.Log("[FinalBossAI] Bắt đầu phát âm thanh chiến đấu Final Boss duy trì LẶP LẠI (Loop = True) đồng bộ mạng!");
        }
    }

    public void StopBattleMusic(bool fade = true)
    {
        if (battleBgmAudioSource == null || !battleBgmAudioSource.isPlaying) return;

        if (battleBgmFadeCoroutine != null) StopCoroutine(battleBgmFadeCoroutine);
        if (fade && gameObject.activeInHierarchy)
        {
            battleBgmFadeCoroutine = StartCoroutine(RoutineFadeOutAndStop(battleBGMFadeOutDuration));
        }
        else
        {
            battleBgmAudioSource.Stop();
        }
    }

    private System.Collections.IEnumerator RoutineFadeBattleMusic(float targetVol, float duration)
    {
        if (battleBgmAudioSource == null) yield break;
        float elapsed = 0f;
        float startVol = battleBgmAudioSource.volume;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (battleBgmAudioSource != null)
            {
                battleBgmAudioSource.volume = Mathf.Lerp(startVol, targetVol, elapsed / duration);
            }
            yield return null;
        }
        if (battleBgmAudioSource != null) battleBgmAudioSource.volume = targetVol;
    }

    private System.Collections.IEnumerator RoutineFadeOutAndStop(float duration)
    {
        if (battleBgmAudioSource == null) yield break;
        float elapsed = 0f;
        float startVol = battleBgmAudioSource.volume;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (battleBgmAudioSource != null)
            {
                battleBgmAudioSource.volume = Mathf.Lerp(startVol, 0f, elapsed / duration);
            }
            yield return null;
        }
        if (battleBgmAudioSource != null)
        {
            battleBgmAudioSource.volume = 0f;
            battleBgmAudioSource.Stop();
        }
    }

    private void OnDestroy()
    {
        StopBattleMusic(false);
    }
}

public interface ISwordRainOwner
{
    void PlaySwordImpactEffects(Vector3 impactPos);
    void DealSwordImpactDamage(Vector3 impactPos, float damage, float radius, LayerMask layer);
    void RecycleSword(GameObject sword);
}

public interface IFireBarrageOwner
{
    void PlayFireBarrageImpactEffects(Vector3 impactPos);
    void DealFireBarrageImpactDamage(Vector3 impactPos, float damage, float radius, LayerMask layer);
    void RecycleFireBarrage(GameObject orb);
}

public class FallingSwordProjectile : MonoBehaviour
{
    private ISwordRainOwner bossOwner;
    private Vector3 targetGroundPos;
    private float dropSpeed;
    private float damage;
    private float impactRadius;
    private LayerMask playerLayer;
    private bool isFalling;
    private HashSet<Transform> hitTargets = new HashSet<Transform>();

    public void Initialize(ISwordRainOwner owner, Vector3 groundPos, float speed, float dmg, float radius, LayerMask layer)
    {
        bossOwner = owner;
        targetGroundPos = groundPos;
        dropSpeed = speed;
        damage = dmg;
        impactRadius = Mathf.Max(radius, 2.5f);
        playerLayer = layer;
        isFalling = true;
        enabled = true;
        hitTargets.Clear();

        // Đảm bảo toàn bộ Collider là Trigger và có BoxCollider Trigger bao phủ toàn thân kiếm
        var colliders = GetComponentsInChildren<Collider>(true);
        foreach (var c in colliders) { if (c != null) c.isTrigger = true; }

        var rootCol = GetComponent<BoxCollider>();
        if (rootCol == null) rootCol = gameObject.AddComponent<BoxCollider>();
        rootCol.size = new Vector3(1.5f, 3.5f, 1.5f);
        rootCol.center = new Vector3(0f, 1.2f, 0f);
        rootCol.isTrigger = true;

        var renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers) { if (r != null) r.enabled = true; }

        var particles = GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in particles) { if (ps != null) { ps.Clear(); ps.Play(); } }
    }

    public void Initialize(FinalBossAI owner, Vector3 groundPos, float speed, float dmg, float radius, LayerMask layer)
    {
        Initialize((ISwordRainOwner)owner, groundPos, speed, dmg, radius, layer);
    }

    private void Update()
    {
        if (!isFalling) return;

        Vector3 prevPos = transform.position;
        Vector3 newPos = prevPos + Vector3.down * dropSpeed * Time.deltaTime;
        transform.position = newPos;

        // Quét tìm va chạm trực tiếp khi kiếm đang lao vút từ trên trời xuống
        CheckFallingDamage(prevPos, newPos);

        if (transform.position.y <= targetGroundPos.y + 0.2f)
        {
            isFalling = false;
            OnImpact();
        }
    }

    private void CheckFallingDamage(Vector3 prevPos, Vector3 newPos)
    {
        var allPlayers = FindAllPlayers();
        foreach (var p in allPlayers)
        {
            if (p != null && !hitTargets.Contains(p))
            {
                float xzDist = Vector2.Distance(new Vector2(newPos.x, newPos.z), new Vector2(p.position.x, p.position.z));
                float minY = Mathf.Min(prevPos.y, newPos.y) - 1.0f;
                float maxY = Mathf.Max(prevPos.y, newPos.y) + 2.5f;

                if (xzDist <= 1.5f && p.position.y >= minY && p.position.y <= maxY)
                {
                    ApplyDamageToPlayer(p, "Mid-air strike");
                }
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isFalling) return;

        Transform playerRoot = ResolvePlayerRoot(other.transform);
        if (playerRoot != null && !hitTargets.Contains(playerRoot))
        {
            ApplyDamageToPlayer(playerRoot, "Trigger contact");
        }
    }

    private void ApplyDamageToPlayer(Transform playerRoot, string source)
    {
        if (playerRoot == null || hitTargets.Contains(playerRoot)) return;
        hitTargets.Add(playerRoot);

        Vector3 knockbackDir = (playerRoot.position - transform.position).normalized + Vector3.up * 0.4f;
        EnemyDamageHelper.DealDamage(playerRoot, damage, knockbackDir * 4f);
        Debug.Log($"[FallingSwordProjectile] {source} -> Trúng Player '{playerRoot.name}' trừ -{damage} HP!");
    }

    private void OnImpact()
    {
        transform.position = targetGroundPos;

        // 1. Gây sát thương nổ bãi chùm kiếm cho toàn bộ Player trong bán kính impactRadius
        var allPlayers = FindAllPlayers();
        foreach (var p in allPlayers)
        {
            if (p != null && !hitTargets.Contains(p))
            {
                float dist = Vector3.Distance(targetGroundPos, p.position);
                float xzDist = Vector2.Distance(new Vector2(targetGroundPos.x, targetGroundPos.z), new Vector2(p.position.x, p.position.z));
                float heightDiff = p.position.y - targetGroundPos.y;

                if (dist <= impactRadius + 0.8f || (xzDist <= impactRadius && heightDiff >= -1.0f && heightDiff <= 3.5f))
                {
                    ApplyDamageToPlayer(p, "Ground Impact AOE");
                }
            }
        }

        // 2. Quét dự phòng thêm bằng Physics.OverlapSphere
        LayerMask mask = (playerLayer.value == 0) ? ~0 : playerLayer;
        Collider[] hits = Physics.OverlapSphere(targetGroundPos, impactRadius, mask, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            Transform root = ResolvePlayerRoot(h.transform);
            if (root != null && !hitTargets.Contains(root))
            {
                ApplyDamageToPlayer(root, "OverlapSphere Impact");
            }
        }

        // 3. Kích hoạt hiệu ứng âm thanh, VFX và thông báo cho Boss Owner
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

    private System.Collections.Generic.List<Transform> FindAllPlayers()
    {
        var list = new System.Collections.Generic.List<Transform>();

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

        var simplePlayers = FindObjectsByType<SimplePlayerTest>(FindObjectsSortMode.None);
        foreach (var p in simplePlayers) if (p != null && !list.Contains(p.transform)) list.Add(p.transform);

        var skeletons = FindObjectsByType<Skeleton>(FindObjectsSortMode.None);
        foreach (var p in skeletons) if (p != null && !list.Contains(p.transform)) list.Add(p.transform);

        var taggedPlayers = GameObject.FindGameObjectsWithTag("Player");
        foreach (var go in taggedPlayers) if (go != null && !list.Contains(go.transform)) list.Add(go.transform);

        return list;
    }

    private Transform ResolvePlayerRoot(Transform t)
    {
        if (t == null) return null;
        if (t.CompareTag("Player")) return t;

        var hudTarget = t.GetComponentInParent<IPlayerHUDTarget>();
        if (hudTarget != null)
        {
            var mono = hudTarget as MonoBehaviour;
            if (mono != null && mono.gameObject != null) return mono.transform;
        }

        var leo = t.GetComponentInParent<LeoPlayer>();
        if (leo != null) return leo.transform;

        var arthur = t.GetComponentInParent<ArthurPlayer>();
        if (arthur != null) return arthur.transform;

        var elena = t.GetComponentInParent<ElenaPlayer>() ?? (MonoBehaviour)t.GetComponentInParent<ElenaArcher>();
        if (elena != null) return elena.transform;

        var maya = t.GetComponentInParent<MayaPlayer>() ?? (MonoBehaviour)t.GetComponentInParent<MayaSupport>();
        if (maya != null) return maya.transform;

        var simple = t.GetComponentInParent<SimplePlayerTest>();
        if (simple != null) return simple.transform;

        var skeleton = t.GetComponentInParent<Skeleton>();
        if (skeleton != null) return skeleton.transform;

        return null;
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
    private IFireBarrageOwner bossOwner;
    private Vector3 targetGroundPos;
    private float dropSpeed;
    private float damage;
    private float impactRadius;
    private LayerMask playerLayer;
    private bool isFalling;

    public void Initialize(IFireBarrageOwner owner, Vector3 groundPos, float speed, float dmg, float radius, LayerMask layer)
    {
        bossOwner = owner;
        targetGroundPos = groundPos;
        dropSpeed = speed;
        damage = dmg;
        impactRadius = radius;
        playerLayer = layer;
        isFalling = true;

        // Tắt tất cả script PixPlays bên thứ 3 để tránh tự hủy hay can thiệp
        var mbList = GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var mb in mbList)
        {
            if (mb != null && mb != this && mb.GetType().Namespace != null && mb.GetType().Namespace.Contains("PixPlays"))
            {
                mb.enabled = false;
            }
        }

        // Kích hoạt tất cả Particle Systems
        var particles = GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in particles)
        {
            if (ps != null)
            {
                var main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.loop = true;
                ps.Clear(true);
                ps.Play(true);
            }
        }

        // Kích hoạt tất cả VFX Graphs
        var vfxGraphs = GetComponentsInChildren<UnityEngine.VFX.VisualEffect>(true);
        foreach (var ve in vfxGraphs)
        {
            if (ve != null)
            {
                ve.Reinit();
                ve.Play();
            }
        }
    }

    private void Update()
    {
        if (!isFalling) return;

        transform.position += Vector3.down * dropSpeed * Time.deltaTime;

        // Quét va chạm liên tục với Player khi đang rơi (bán kính 2.0m)
        Collider[] hits = Physics.OverlapSphere(transform.position, 2.0f);
        foreach (var hit in hits)
        {
            if (hit == null) continue;
            var hudTarget = hit.GetComponentInParent<IPlayerHUDTarget>() ?? hit.GetComponentInChildren<IPlayerHUDTarget>();
            if (hudTarget != null && hudTarget is MonoBehaviour mb)
            {
                Transform playerRoot = mb.transform;
                isFalling = false;
                OnDirectHitPlayer(playerRoot);
                return;
            }
        }

        if (transform.position.y <= targetGroundPos.y + 0.2f)
        {
            isFalling = false;
            OnImpact();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isFalling) return;

        var hudTarget = other.GetComponentInParent<IPlayerHUDTarget>() ?? other.GetComponentInChildren<IPlayerHUDTarget>();
        if (hudTarget != null && hudTarget is MonoBehaviour mb)
        {
            Transform playerRoot = mb.transform;
            isFalling = false;
            OnDirectHitPlayer(playerRoot);
        }
    }

    private void OnDirectHitPlayer(Transform playerRoot)
    {
        Vector3 knockbackDir = (playerRoot.position - transform.position).normalized + Vector3.up * 0.5f;
        EnemyDamageHelper.DealDamage(playerRoot, damage, knockbackDir * 5f);
        Debug.Log($"[FallingFireOrb] Cầu lửa rơi trúng trực tiếp Player: {playerRoot.name} -> Trừ {damage} HP!");

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

        // TỰ ĐỘNG GẮN / CẤU HÌNH BOX COLLIDER VỪA VẶN PHỦ KÍN VỆT CHÉM TRĂNG KHUYẾT
        var box = GetComponent<BoxCollider>();
        if (box == null) box = gameObject.AddComponent<BoxCollider>();
        box.size = new Vector3(2.0f, 1.4f, 1.4f);
        box.center = new Vector3(0f, 0.35f, 0f);
        box.isTrigger = true;

        var colliders = GetComponentsInChildren<Collider>(true);
        foreach (var c in colliders)
        {
            if (c != null) c.isTrigger = true;
        }

        // ÉP TẤT CẢ PARTICLE SYSTEM TRONG PREFAB PHẢI LOOP LIÊN TỤC KHÔNG BỊ TẮT GIỮA ĐƯỜNG
        var particles = GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in particles)
        {
            if (ps == null) continue;
            ps.gameObject.SetActive(true);
            var main = ps.main;
            main.loop = true; // Ép lặp lại liên tục (Loop)
            main.stopAction = ParticleSystemStopAction.None; // Không tự hủy
            main.startLifetime = Mathf.Max(main.startLifetime.constant, 4.0f); // Thời gian tồn tại dài
            var em = ps.emission;
            em.enabled = true;
            ps.Clear(true);
            ps.Play(true);
        }

        var vfxGraphs = GetComponentsInChildren<UnityEngine.VFX.VisualEffect>(true);
        foreach (var ve in vfxGraphs)
        {
            if (ve != null)
            {
                ve.gameObject.SetActive(true);
                ve.Play();
            }
        }

        var renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            if (r != null) r.enabled = true;
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

        // QUÉT LIÊN TỤC VÀ TRỰC TIẾP TẤT CẢ PLAYER ĐỨNG TRÊN ĐƯỜNG BAY CỦA VỆT CHÉM (BẢO ĐẢM 100% TRỪ MÁU)
        ScanAndDamagePlayers();

        // Đảm bảo tất cả Renderers của VFX luôn được bật hiển thị liên tục khi bay
        var renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            if (r != null && !r.enabled) r.enabled = true;
        }
    }

    private void ScanAndDamagePlayers()
    {
        Vector3 currentPos = transform.position;

        // 1. Quét theo khoảng cách trực tiếp tới toàn bộ người chơi đang chơi
        var players = FindAllActivePlayers();
        foreach (var p in players)
        {
            if (p == null || hitPlayers.Contains(p)) continue;

            float xzDist = Vector2.Distance(new Vector2(currentPos.x, currentPos.z), new Vector2(p.position.x, p.position.z));
            float heightDiff = Mathf.Abs(p.position.y - currentPos.y);

            // Bán kính phủ sóng vệt chém vừa vặn 1.6m, chiều cao 2.0m
            if (xzDist <= 1.6f && heightDiff <= 2.0f)
            {
                ApplyDamageToPlayer(p);
            }
        }

        // 2. Quét phủ hitbox bằng Physics.OverlapBox
        Collider[] hits = Physics.OverlapBox(currentPos + Vector3.up * 0.35f, new Vector3(1.0f, 0.8f, 0.8f), transform.rotation);
        foreach (var hit in hits)
        {
            if (hit == null) continue;
            Transform root = GetPlayerRoot(hit.transform);
            if (root != null && !hitPlayers.Contains(root))
            {
                ApplyDamageToPlayer(root);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other != null) ApplyDamageToPlayer(GetPlayerRoot(other.transform));
    }

    private void OnTriggerStay(Collider other)
    {
        if (other != null) ApplyDamageToPlayer(GetPlayerRoot(other.transform));
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision != null && collision.gameObject != null) ApplyDamageToPlayer(GetPlayerRoot(collision.gameObject.transform));
    }

    private void ApplyDamageToPlayer(Transform playerRoot)
    {
        if (playerRoot == null || hitPlayers.Contains(playerRoot)) return;
        if (ownerBoss != null && (playerRoot.IsChildOf(ownerBoss) || playerRoot == ownerBoss)) return;

        hitPlayers.Add(playerRoot);
        Vector3 knockback = moveDirection * 12f + Vector3.up * 2f;

        // Chỉ gửi lệnh trừ máu nếu là Server HOẶC là Client sở hữu Player này để đồng bộ sát thương chuẩn xác trên mạng 4 người
        bool shouldDealDamage = !Unity.Netcode.NetworkManager.Singleton || !Unity.Netcode.NetworkManager.Singleton.IsListening || Unity.Netcode.NetworkManager.Singleton.IsServer || IsLocalPlayer(playerRoot);

        if (shouldDealDamage)
        {
            EnemyDamageHelper.DealDamage(playerRoot, damage, knockback);
            Debug.Log($"[FinalBossProjectile] Vệt chém CriticalSlash đánh trúng Player '{playerRoot.name}' -> Chắc chắn trừ -{damage} HP!");
        }

        CameraShakeHelper.ShakeAtPosition(transform.position, 0.6f, 1.2f, 35f);
    }

    private bool IsLocalPlayer(Transform playerRoot)
    {
        if (playerRoot == null) return false;
        var netObj = playerRoot.GetComponent<Unity.Netcode.NetworkObject>() ?? playerRoot.GetComponentInParent<Unity.Netcode.NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            return netObj.IsOwner;
        }
        return false;
    }

    private List<Transform> FindAllActivePlayers()
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

        var simplePlayers = FindObjectsByType<SimplePlayerTest>(FindObjectsSortMode.None);
        foreach (var p in simplePlayers) if (p != null && !list.Contains(p.transform)) list.Add(p.transform);

        var skeletons = FindObjectsByType<Skeleton>(FindObjectsSortMode.None);
        foreach (var p in skeletons) if (p != null && !list.Contains(p.transform)) list.Add(p.transform);

        return list;
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
                outward.y = 0.4f;
                Vector3 force = Vector3.up * knockupForce + outward.normalized * 8.0f;

                // Gây sát thương và KÍCH HOẠT TÉ NGÃ
                EnemyDamageHelper.DealKickDamageWithStun(root, damage, force, 1.5f);
                Debug.Log($"[ExpandingShockwaveRing] Sóng xung kích đánh trúng: {root.name} (-{damage} HP, Hất Tung & Té Ngã)");
            }
        }

        if (currentRadius >= maxRadius)
        {
            Destroy(gameObject);
        }
    }
}

/// <summary>
/// Quản lý hiệu ứng VFX Gồng Hất Tung (Effect_09_InfernoShield) nở to dần theo bán kính và ép Loop các ParticleSystem
/// </summary>
public class ExpandingShockwaveVFX : MonoBehaviour
{
    private float targetRadius = 6.0f;
    private float expandDuration = 1.8f;
    private float elapsed = 0f;
    private Vector3 initialScale;
    private Vector3 maxScale;

    public void Initialize(float radius, float duration)
    {
        targetRadius = Mathf.Max(radius, 1.0f);
        expandDuration = Mathf.Max(duration, 0.5f);
        elapsed = 0f;

        initialScale = Vector3.one * 0.8f;
        // Tỷ lệ scale tự động nở to theo bán kính shockwaveRadius của Boss (đường kính = radius * 2.2)
        maxScale = new Vector3(targetRadius * 2.2f, targetRadius * 1.6f, targetRadius * 2.2f);
        transform.localScale = initialScale;

        // Ép toàn bộ ParticleSystem bên trong phải Loop và tiếp tục phát sáng liên tục
        var particles = GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in particles)
        {
            if (ps == null) continue;
            ps.gameObject.SetActive(true);
            var main = ps.main;
            main.loop = true; // Ép Loop liên tục
            main.stopAction = ParticleSystemStopAction.None;
            main.startLifetime = Mathf.Max(main.startLifetime.constant, expandDuration + 2.0f);
            var em = ps.emission;
            em.enabled = true;
            ps.Clear(true);
            ps.Play(true);
        }

        var vfxGraphs = GetComponentsInChildren<UnityEngine.VFX.VisualEffect>(true);
        foreach (var ve in vfxGraphs)
        {
            if (ve != null)
            {
                ve.gameObject.SetActive(true);
                ve.Play();
            }
        }

        var renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            if (r != null) r.enabled = true;
        }
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / expandDuration);
        // Nở to mượt mà theo hàm SmoothStep
        float smoothT = Mathf.SmoothStep(0f, 1f, t);
        transform.localScale = Vector3.Lerp(initialScale, maxScale, smoothT);
    }
}

/// <summary>
/// Quản lý vùng gây sát thương của tia Genesis Breaker Laser khổng lồ (-5 HP cho mỗi người chơi đứng trong chùm tia)
/// </summary>
public class GenesisBeamDamageZone : MonoBehaviour
{
    private FinalBossAI bossOwner;
    private float damage = 5f;
    private float knockback = 8f;
    private float tickTimer = 0f;
    private float tickInterval = 0.35f;
    private float beamLength = 50f;
    private float beamRadius = 3.2f;

    public void Initialize(FinalBossAI owner, float dmg, float kb, float length = 50f, float radius = 3.2f)
    {
        bossOwner = owner;
        damage = dmg;
        knockback = kb;
        beamLength = length;
        beamRadius = radius;
    }

    private void Update()
    {
        tickTimer -= Time.deltaTime;
        if (tickTimer <= 0f)
        {
            tickTimer = tickInterval;
            CheckAndDamagePlayersInBeam();
        }
    }

    private void CheckAndDamagePlayersInBeam()
    {
        if (bossOwner == null) return;
        bool isAuth = bossOwner.isStandaloneModePublic || (bossOwner.IsNetworkActivePublic && bossOwner.IsServerPublic);
        if (!isAuth) return;

        var players = bossOwner.GetAllActivePlayers();
        Vector3 origin = transform.position;
        Vector3 forward = transform.forward;

        foreach (var p in players)
        {
            if (p == null || bossOwner.IsPlayerDeadOrInvisiblePublic(p)) continue;

            Vector3 toPlayer = p.position - origin;
            float projection = Vector3.Dot(toPlayer, forward);

            // Nằm dọc theo chiều dài của tia (từ 0 đến beamLength)
            if (projection >= 0f && projection <= beamLength)
            {
                Vector3 closestPointOnRay = origin + forward * projection;
                float distToBeam = Vector3.Distance(closestPointOnRay, p.position);

                // Nằm trong bán kính bao phủ của tia laser
                if (distToBeam <= beamRadius)
                {
                    Vector3 force = forward * knockback + Vector3.up * 2f;
                    EnemyDamageHelper.DealDamage(p, damage, force);
                    Debug.Log($"[GenesisBeamDamageZone] Tia Laser Genesis Breaker quét trúng Player '{p.name}' (-{damage} HP)!");
                }
            }
        }
    }
}

/// <summary>
/// Quản lý nhóm 3 cụm Drone Blaster (Tổng cộng 12 tia laser xanh) xoay tròn 360° quanh Boss để người chơi né đòn
/// </summary>
public class RotatingDroneBlasterGroup : MonoBehaviour
{
    private FinalBossAI bossOwner;
    private float rotationSpeed = 35f; // Tốc độ xoay vòng tròn quanh Boss (độ/giây)
    private float duration = 6.0f;
    private float timer = 0f;
    private float damage = 5f;
    private float damageTickInterval = 0.35f;
    private float damageTickTimer = 0f;
    private float laserLength = 35f;
    private float laserWidth = 2.2f;
    private List<Transform> droneUnits = new List<Transform>();

    public void Initialize(FinalBossAI owner, float rotSpeed = 35f, float lifeTime = 6.0f, float dmg = 5f)
    {
        bossOwner = owner;
        rotationSpeed = rotSpeed;
        duration = lifeTime;
        damage = dmg;
        timer = 0f;
    }

    public void RegisterDroneUnit(Transform unit)
    {
        if (unit != null && !droneUnits.Contains(unit))
            droneUnits.Add(unit);
    }

    private void Update()
    {
        timer += Time.deltaTime;

        // Đi theo vị trí của Boss để tâm xoay luôn bám sát theo Boss
        if (bossOwner != null)
        {
            Vector3 targetCenter = bossOwner.transform.position + Vector3.up * 1.5f;
            transform.position = Vector3.Lerp(transform.position, targetCenter, Time.deltaTime * 10f);
        }

        // Xoay tròn toàn bộ cụm drone theo trục Y
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);

        // Kiểm tra gây sát thương dọc theo các tia laser cho người chơi (-5 HP)
        damageTickTimer -= Time.deltaTime;
        if (damageTickTimer <= 0f)
        {
            damageTickTimer = damageTickInterval;
            CheckLaserDamage();
        }

        if (timer >= duration)
        {
            Destroy(gameObject);
        }
    }

    private void CheckLaserDamage()
    {
        if (bossOwner == null) return;
        bool isAuth = bossOwner.isStandaloneModePublic || (bossOwner.IsNetworkActivePublic && bossOwner.IsServerPublic);
        if (!isAuth) return;

        var players = bossOwner.GetAllActivePlayers();
        foreach (var unit in droneUnits)
        {
            if (unit == null) continue;
            Vector3 origin = unit.position;
            Vector3 forward = unit.forward;

            foreach (var p in players)
            {
                if (p == null || bossOwner.IsPlayerDeadOrInvisiblePublic(p)) continue;

                Vector3 toPlayer = p.position - origin;
                float projection = Vector3.Dot(toPlayer, forward);

                if (projection >= 0f && projection <= laserLength)
                {
                    Vector3 closestPoint = origin + forward * projection;
                    float dist = Vector3.Distance(closestPoint, p.position);
                    if (dist <= laserWidth)
                    {
                        Vector3 pushDir = unit.right; // Đẩy người chơi theo chiều quay của laser
                        EnemyDamageHelper.DealDamage(p, damage, pushDir * 6f + Vector3.up * 2f);
                        Debug.Log($"[RotatingDroneBlasterGroup] Tia laser drone quét trúng Player '{p.name}' (-{damage} HP)!");
                    }
                }
            }
        }
    }
}
