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
    public enum FinalBossState { Sitting, JumpDown, Idle, Chase, Attack, Shockwave, FireSpew, EarthSummon, Hit, Dead }

    [Header("Miniboss Reference")]
    [Tooltip("Reference to the Boss AI (e.g. Silas) that must die before the final boss jumps down.")]
    public BossAI bossMiniboss;
    [Tooltip("Alternative reference to MiniBossAI (Rakan) if used.")]
    public MiniBossAI miniBoss;
    [Tooltip("If true, the boss will transition to JumpDown immediately without waiting for the miniboss.")]
    public bool startActiveWithoutMiniboss = false;

    [Header("Health Settings")]
    public float maxHealth = 1200f;
    public NetworkVariable<float> currentHealth = new NetworkVariable<float>(
        1200f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

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

    public float ActualCurrentHealth => isStandaloneMode ? localHealth : currentHealth.Value;
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

    [Header("Movement Speeds")]
    public float walkSpeed = 2.2f;
    public float runSpeed = 6.0f;
    [Tooltip("Distance threshold: run when far, walk when close to feel natural.")]
    public float runDistanceThreshold = 5.0f;

    [Header("AI Vision & Attack Ranges")]
    public float sightRange = 25f;
    public float fieldOfView = 140f;
    public float attackRange = 3.0f;
    public float attackCooldown = 2.0f;
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
    private float fireSpewCooldownTimer;

    [Header("Earth Summon (Triệu Hồi Đá) Settings")]
    public GameObject warningDecalPrefab;
    public GameObject earthBlastPrefab;
    public float warningDuration = 1.5f;
    public float earthBlastRadius = 3.0f;
    public float earthBlastDamage = 50f;
    public float earthBlastKnockback = 15f;
    public string earthSummonTriggerParam = "EarthSummon";
    private bool hasTriggeredEarthSummon = false;

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
    private EarthSummonState stateEarthSummon;
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
        stateEarthSummon = new EarthSummonState(this);
        stateHit = new HitState(this);
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
        hasTriggeredEarthSummon = false;
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
        currentHealth.OnValueChanged += OnHealthNetChanged;

        ApplySpeedAnim(netSpeed.Value);

        if (IsServer)
        {
            currentHealth.Value = maxHealth;
            isHUDVisible.Value = startActiveWithoutMiniboss;
            fireSpewCooldownTimer = fireSpewInterval;
            hasTriggeredEarthSummon = false;
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
        currentHealth.OnValueChanged -= OnHealthNetChanged;
    }

    private void OnHealthNetChanged(float oldVal, float newVal)
    {
        float diff = oldVal - newVal;
        if (diff > 0)
        {
            EnemyDamageEffectHelper.PlayDamageEffects(gameObject, diff);
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

    public void TakeDamage(float damage)
    {
        bool auth = isStandaloneMode || (IsNetworkActive && IsServer);
        if (!auth || IsDead || CurrentStateValue == FinalBossState.Sitting || CurrentStateValue == FinalBossState.JumpDown) return;

        if (isStandaloneMode)
        {
            localHealth -= damage;
            EnemyDamageEffectHelper.PlayDamageEffects(gameObject, damage);
            if (localHealth <= 0)
            {
                ChangeState(FinalBossState.Dead);
                return;
            }
            else if (localHealth <= maxHealth * 0.5f && !hasTriggeredEarthSummon)
            {
                hasTriggeredEarthSummon = true;
                ChangeState(FinalBossState.EarthSummon);
                return;
            }
        }
        else
        {
            currentHealth.Value -= damage;
            if (currentHealth.Value <= 0)
            {
                ChangeState(FinalBossState.Dead);
                return;
            }
            else if (currentHealth.Value <= maxHealth * 0.5f && !hasTriggeredEarthSummon)
            {
                hasTriggeredEarthSummon = true;
                ChangeState(FinalBossState.EarthSummon);
                return;
            }
        }

        // Trigger stagger hit animation if not attacking/roaring/spewing/summoning/dead/already hit
        if (CurrentStateValue != FinalBossState.Attack && CurrentStateValue != FinalBossState.Shockwave && CurrentStateValue != FinalBossState.FireSpew && CurrentStateValue != FinalBossState.EarthSummon && CurrentStateValue != FinalBossState.Dead && CurrentStateValue != FinalBossState.Hit)
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
            case FinalBossState.EarthSummon:
                currentFSMState = stateEarthSummon;
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
        float finalDamage = 25f;

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
            // Fallback: SphereCast in front
            Vector3 origin = transform.position + Vector3.up * 1.2f;
            RaycastHit[] hits = Physics.SphereCastAll(origin, attackThickness * 1.5f, transform.forward, attackRange, playerLayer);
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

        // 1. Fast path: Use cached players from PlayerHUDManager (No GC / search overhead!)
        if (PlayerHUDManager.ActivePlayers != null && PlayerHUDManager.ActivePlayers.Count > 0)
        {
            foreach (var p in PlayerHUDManager.ActivePlayers)
            {
                var mono = p as MonoBehaviour;
                if (mono != null && mono.gameObject != null)
                {
                    Transform t = mono.transform;
                    if (!list.Contains(t)) list.Add(t);
                }
            }
        }

        // 2. Fallback: Tag lookup (much faster than FindObjectsByType)
        if (list.Count == 0)
        {
            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            foreach (var p in players)
            {
                if (p != null && !list.Contains(p.transform))
                {
                    list.Add(p.transform);
                }
            }
        }

        // 3. Last resort: Simple test script check
        if (list.Count == 0)
        {
            var simples = FindObjectsByType<SimplePlayerTest>(FindObjectsSortMode.None);
            foreach (var p in simples)
            {
                if (p != null && !list.Contains(p.transform))
                {
                    list.Add(p.transform);
                }
            }
        }

        return list;
    }

    private void DetectAndSwitchTarget()
    {
        if (IsDead || CurrentStateValue == FinalBossState.Sitting || CurrentStateValue == FinalBossState.JumpDown) return;

        Transform closest = null;
        float minD = float.MaxValue;
        Vector3 eyePos = eyeTransform != null ? eyeTransform.position : transform.position + Vector3.up * 1.5f;
        int raycastMask = obstacleLayer.value & ~LayerMask.GetMask("Player", "Enemy");

        // KHÓA MỤC TIÊU ƯU TIÊN: Nếu đang có mục tiêu và mục tiêu đó vẫn hợp lệ thì tiếp tục dí mục tiêu đó
        if (targetPlayer != null)
        {
            if (!IsPlayerDeadOrInvisible(targetPlayer))
            {
                Vector3 targetCenter = targetPlayer.position + Vector3.up * 1.0f;
                float d = Vector3.Distance(eyePos, targetCenter);
                float currentSight = IsBossActive ? 50f : sightRange;
                if (d <= currentSight)
                {
                    Vector3 dir = (targetCenter - eyePos).normalized;
                    if (!Physics.Raycast(eyePos, dir, d, raycastMask, QueryTriggerInteraction.Ignore))
                    {
                        return; // Khóa mục tiêu thành công!
                    }
                }
            }
        }

        var activePlayers = GetAllActivePlayers();
        for (int i = 0; i < activePlayers.Count; i++)
        {
            Transform pTrans = activePlayers[i];
            if (pTrans == null || pTrans == transform) continue;
            if (IsPlayerDeadOrInvisible(pTrans)) continue;

            Vector3 targetCenter = pTrans.position + Vector3.up * 1.0f;
            float d = Vector3.Distance(eyePos, targetCenter);
            
            float currentSight = IsBossActive ? 50f : sightRange;
            if (d <= currentSight)
            {
                Vector3 dir = (targetCenter - eyePos).normalized;
                bool inFOV = IsBossActive || Vector3.Angle(transform.forward, dir) < fieldOfView / 2f || d <= 5f;

                if (inFOV && !Physics.Raycast(eyePos, dir, d, raycastMask, QueryTriggerInteraction.Ignore))
                {
                    if (d < minD)
                    {
                        minD = d;
                        closest = pTrans;
                    }
                }
            }
        }

        if (closest != null)
        {
            targetPlayer = closest;
        }
        else if (CurrentStateValue == FinalBossState.Chase)
        {
            targetPlayer = null;
        }
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
                boss.ChangeState(FinalBossState.JumpDown);
                return;
            }

            // 1. If Silas dies completely in Phase 2, show the Final Boss HUD
            if (boss.bossMiniboss != null && boss.bossMiniboss.IsDead)
            {
                boss.SetHUDVisible(true);
            }

            // 2. Determine jump down trigger
            // Case A: No Rakan (miniBoss) is assigned -> jump down as soon as Silas (bossMiniboss) dies.
            if (boss.miniBoss == null)
            {
                if (boss.bossMiniboss == null || boss.bossMiniboss.IsDead)
                {
                    boss.ChangeState(FinalBossState.JumpDown);
                }
            }
            // Case B: Both are assigned -> Boss sits when Silas dies (shows HUD), and only jumps down when Rakan dies!
            else
            {
                if (boss.miniBoss.IsDead)
                {
                    boss.ChangeState(FinalBossState.JumpDown);
                }
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

            if (boss.agent != null)
            {
                boss.agent.enabled = true;
                boss.agent.Warp(endPos);
                boss.agent.velocity = Vector3.zero;
            }

            boss.TriggerLandingSlam();
            boss.ActivateBoss();
            boss.ChangeState(FinalBossState.Idle);
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

            // High priority close range defensive shockwave
            if (boss.CheckShockwaveProximity())
            {
                return;
            }

            // Every 10s fire spew
            if (boss.fireSpewCooldownTimer <= 0)
            {
                boss.ChangeState(FinalBossState.FireSpew);
                return;
            }

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

            // High priority close range defensive shockwave
            if (boss.CheckShockwaveProximity())
            {
                return;
            }

            // Every 10s fire spew
            if (boss.fireSpewCooldownTimer <= 0)
            {
                boss.ChangeState(FinalBossState.FireSpew);
                return;
            }

            float dist = Vector3.Distance(boss.transform.position, boss.targetPlayer.position);

            if (dist <= boss.attackRange && boss.attackCooldownTimer <= 0)
            {
                if (boss.AgentReady) boss.agent.isStopped = true;
                boss.SetSpeedNet(0f);
                boss.ChangeState(FinalBossState.Attack);
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

            boss.RotateTowards(boss.targetPlayer.position);
        }

        public void Exit() { }
    }

    private class AttackState : IEnemyState
    {
        private FinalBossAI boss;
        private FinalBossAttackConfig config;
        private HashSet<Transform> hitPlayersThisAttack = new HashSet<Transform>();

        public AttackState(FinalBossAI boss) { this.boss = boss; }

        public void Enter()
        {
            hitPlayersThisAttack.Clear();

            // Select attack type randomly
            boss.currentAttackIndex = Random.Range(0, boss.attackTriggers.Length);

            if (!boss.isStandaloneMode)
            {
                boss.attackTypeSync.Value = boss.currentAttackIndex;
                boss.attackCounter.Value++;
            }
            else
            {
                if (boss.anim != null) boss.anim.SetTrigger(boss.attackTriggers[boss.currentAttackIndex]);
            }

            config = boss.attackConfigs[boss.currentAttackIndex];
            boss.stateTimer = config.duration;

            if (config.forwardSpeed > 0 && config.glideDuration > 0)
            {
                boss.isGliding = true;
                boss.glideTimer = 0f;
                boss.currentGlideDuration = config.glideDuration;
                boss.currentGlideSpeed = config.forwardSpeed;
            }
            else
            {
                boss.isGliding = false;
            }

            if (boss.AgentReady)
            {
                boss.agent.isStopped = true;
                boss.agent.velocity = Vector3.zero;
            }
        }

        public void Update()
        {
            boss.stateTimer -= Time.deltaTime;

            if (boss.isGliding)
            {
                boss.glideTimer += Time.deltaTime;
                if (boss.AgentReady)
                {
                    boss.agent.Move(boss.transform.forward * boss.currentGlideSpeed * Time.deltaTime);
                }
                if (boss.glideTimer >= boss.currentGlideDuration)
                {
                    boss.isGliding = false;
                }
            }

            if (boss.targetPlayer != null && boss.stateTimer > config.duration * 0.5f)
            {
                boss.RotateTowards(boss.targetPlayer.position);
            }

            float elapsed = config.duration - boss.stateTimer;
            float elapsedPercent = elapsed / config.duration;
            if (elapsedPercent >= config.damageStartPercent && elapsedPercent <= config.damageEndPercent)
            {
                boss.DealFistDamageContinuously(hitPlayersThisAttack, boss.currentAttackIndex);
            }

            if (boss.stateTimer <= 0)
            {
                boss.attackCooldownTimer = boss.attackCooldown;
                if (boss.targetPlayer != null) boss.ChangeState(FinalBossState.Chase);
                else boss.ChangeState(FinalBossState.Idle);
            }
        }

        public void Exit()
        {
            boss.isGliding = false;
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

        public void Exit() { }
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
                    boss.fireSpewCooldownTimer = boss.fireSpewInterval;
                    if (boss.targetPlayer != null) boss.ChangeState(FinalBossState.Chase);
                    else boss.ChangeState(FinalBossState.Idle);
                }
            }
        }

        public void Exit() { }
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

    // ══════════════════════════════════════════════════════════
    //  EARTH SUMMON TRIGGERS & RPCs
    // ══════════════════════════════════════════════════════════

    [ClientRpc]
    private void SpawnWarningIndicatorsClientRpc(Vector3[] positions)
    {
        SpawnWarningIndicatorsLocal(positions);
    }

    [ClientRpc]
    private void PlayEarthBlastClientRpc(Vector3[] positions)
    {
        PlayEarthBlastLocal(positions);
    }

    private void SpawnWarningIndicatorsLocal(Vector3[] positions)
    {
        foreach (var pos in positions)
        {
            if (warningDecalPrefab != null)
            {
                GameObject warning = Instantiate(warningDecalPrefab, pos + Vector3.up * 0.05f, Quaternion.identity);
                
                // Gắn script chớp đỏ cảnh báo
                var flasher = warning.AddComponent<WarningDecalFlash>();
                if (flasher != null)
                {
                    flasher.StartFlashing(warningDuration);
                }

                Destroy(warning, warningDuration);
            }
        }
    }

    private void PlayEarthBlastLocal(Vector3[] positions)
    {
        foreach (var pos in positions)
        {
            if (earthBlastPrefab != null)
            {
                GameObject blast = Instantiate(earthBlastPrefab, pos, Quaternion.identity);
                blast.transform.localScale *= 3.0f;
                
                var locationVfx = blast.GetComponent<PixPlays.ElementalVFX.LocationVfx>();
                if (locationVfx != null)
                {
                    var data = new PixPlays.ElementalVFX.VfxData(pos, pos + Vector3.up, 3.0f, earthBlastRadius * 3.0f);
                    locationVfx.Play(data);
                }
                
                Destroy(blast, 4.0f);
            }
        }
    }

    private Vector3[] CalculateEarthBlastPositions()
    {
        var activePlayers = GetAllActivePlayers();
        var spots = new List<Vector3>();

        foreach (var p in activePlayers)
        {
            if (p == null || IsPlayerDeadOrInvisible(p)) continue;
            
            // 1. Triệu hồi chính xác dưới chân người chơi
            spots.Add(p.position);
            
            // 2. Triệu hồi ngẫu nhiên xung quanh người chơi đó (2-3 đốm xung quanh mỗi player)
            int extraSpots = Random.Range(2, 4);
            for (int k = 0; k < extraSpots; k++)
            {
                Vector2 randomOffset = Random.insideUnitCircle * 3.0f; // Bán kính 3m
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

        // Nếu không có người chơi nào hoạt động, triệu hồi ngẫu nhiên xung quanh Boss
        if (spots.Count == 0)
        {
            int targetCount = Random.Range(4, 7);
            for (int i = 0; i < targetCount; i++)
            {
                Vector2 randomOffset = Random.insideUnitCircle * 8f;
                Vector3 offsetPos = transform.position + new Vector3(randomOffset.x, 0, randomOffset.y);
                if (NavMesh.SamplePosition(offsetPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                {
                    spots.Add(hit.position);
                }
            }
        }

        return spots.ToArray();
    }

    public void DealEarthBlastDamage(Vector3[] positions)
    {
        bool auth = isStandaloneMode || (IsNetworkActive && IsServer);
        if (!auth) return;

        foreach (var pos in positions)
        {
            Collider[] hits = Physics.OverlapSphere(pos, earthBlastRadius, playerLayer);
            HashSet<Transform> hitRoots = new HashSet<Transform>();

            foreach (var hit in hits)
            {
                Transform root = GetPlayerRoot(hit.transform);
                if (root != null && !hitRoots.Contains(root))
                {
                    hitRoots.Add(root);
                    Vector3 knockbackDir = (root.position - pos);
                    knockbackDir.y = 1.0f;
                    Vector3 force = knockbackDir.normalized * earthBlastKnockback;

                    EnemyDamageHelper.DealDamage(root, earthBlastDamage, force);
                    Debug.Log($"[FinalBossAI] Earth Blast hit player: {root.name} at {pos}");
                }
            }
        }
    }

    private class EarthSummonState : IEnemyState
    {
        private FinalBossAI boss;
        private float timer;
        private bool blastsTriggered;
        private Vector3[] spawnPositions;

        public EarthSummonState(FinalBossAI boss) { this.boss = boss; }

        public void Enter()
        {
            timer = boss.warningDuration;
            blastsTriggered = false;

            if (boss.AgentReady)
            {
                boss.agent.isStopped = true;
                boss.agent.velocity = Vector3.zero;
            }
            boss.SetSpeedNet(0f);

            if (boss.anim != null)
            {
                boss.anim.SetTrigger(boss.earthSummonTriggerParam);
            }

            spawnPositions = boss.CalculateEarthBlastPositions();

            if (!boss.isStandaloneMode)
            {
                boss.SpawnWarningIndicatorsClientRpc(spawnPositions);
            }
            else
            {
                boss.SpawnWarningIndicatorsLocal(spawnPositions);
            }
        }

        public void Update()
        {
            timer -= Time.deltaTime;

            if (timer <= 0 && !blastsTriggered)
            {
                blastsTriggered = true;

                if (!boss.isStandaloneMode)
                {
                    boss.PlayEarthBlastClientRpc(spawnPositions);
                }
                else
                {
                    boss.PlayEarthBlastLocal(spawnPositions);
                }

                boss.DealEarthBlastDamage(spawnPositions);

                if (boss.targetPlayer != null) boss.ChangeState(FinalBossState.Chase);
                else boss.ChangeState(FinalBossState.Idle);
            }
        }

        public void Exit() { }
    }

    // ══════════════════════════════════════════════════════════
    //  DUMMY ANIMATION EVENT RECEIVERS TO PREVENT WARNINGS
    // ══════════════════════════════════════════════════════════
    public void OnLeftPunchSwing() { }
    public void OnRightPunchSwing() { }
    public void OnSwipeSwing() { }
    public void OnShockwaveImpact() { }
    public void OnFireSpewImpact() { }
    public void OnEarthSummonImpact() { }
}
