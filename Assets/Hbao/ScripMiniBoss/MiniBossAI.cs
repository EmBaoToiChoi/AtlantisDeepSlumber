using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Mini Boss AI Script using FSM (Finite State Machine).
/// States: Idle, Chase, Attack, Hit, Dead.
/// Features:
///   - Alternates between 3 attacks: Nhaychemdat, NhayDanh, and XoayChem.
///   - Applies forward and upward leaps/spins safely using a visual Y-offset technique on the NavMesh.
///   - Performs Raycast/SphereCast checks during attacks for precise hitbox damage.
///   - Synchronizes health, states, and animator triggers across clients.
/// </summary>
public class MiniBossAI : NetworkBehaviour
{
    public enum MiniBossState { Idle, Chase, Attack, Hit, Enrage, Dead }

    [Header("Health Settings")]
    public float phase1MaxHealth = 500f;
    public float phase2MaxHealth = 700f;
    [HideInInspector] public float maxHealth = 500f;
    public NetworkVariable<float> currentHealth = new NetworkVariable<float>(
        500f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Phase 2 Settings")]
    public float phase2SpeedMultiplier = 1.3f;
    public float phase2AnimSpeed = 1.35f;
    public float phase2CooldownMultiplier = 0.6f;
    public float enrageDuration = 3f;
    public string enrageTrigger = "Enrage";

    [Header("Phase 2 Enrage Transition Settings")]
    public GameObject enrageVFXPrefab;
    public AudioClip enrageSFXSound;

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

    // Standalone fallback variables
    private float localHealth;
    private MiniBossState localState = MiniBossState.Idle;
    private bool localIsBossActive = false;
    private bool localIsPhase2 = false;
    private bool isStandaloneMode = false;
    private bool IsNetworkActive => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

    public bool IsPhase2 => isStandaloneMode ? localIsPhase2 : isPhase2Network.Value;

    public MiniBossState CurrentStateValue
    {
        get => isStandaloneMode ? localState : currentState.Value;
        set { if (isStandaloneMode) localState = value; else currentState.Value = value; }
    }

    public float ActualCurrentHealth => (isStandaloneMode || !IsSpawned || !IsServer) ? localHealth : currentHealth.Value;
    public bool IsBossActive => isStandaloneMode ? localIsBossActive : isBossActive.Value;
    public bool IsDead => CurrentStateValue == MiniBossState.Dead;

    [Header("Components")]
    public NavMeshAgent agent;
    public Animator anim;
    [Tooltip("The root of the visual mesh child. Leaps/spins will offset this transform Y position to keep NavMesh tracking on the ground.")]
    public Transform visualRoot;
    [Tooltip("Raycast eye height target for detecting players")]
    public Transform eyeTransform;
    public Transform swordBase;
    public Transform swordTip;

    [Header("Movement Speeds")]
    public float walkSpeed = 2f;
    public float runSpeed = 5.5f;

    [Header("AI Vision & Attack Ranges")]
    public float sightRange = 18f;
    public float fieldOfView = 140f;
    public float attackRange = 3.2f;
    public float attackCooldown = 2.5f;
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
    private HitState hitState;
    private EnrageState enrageState;
    private DeadState deadState;

    private Transform targetPlayer;
    private float stateTimer;
    private float hitStaggerDuration = 0.55f;
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
        maxHealth = phase1MaxHealth;
        localHealth = phase1MaxHealth;
        idleState = new IdleState(this);
        chaseState = new ChaseState(this);
        attackState = new AttackState(this);
        hitState = new HitState(this);
        enrageState = new EnrageState(this);
        deadState = new DeadState(this);
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
        maxHealth = phase1MaxHealth;
        localHealth = phase1MaxHealth;
        localIsPhase2 = false;
        SnapToNavMesh();
        ApplySpeedAnim(0f);
        ChangeState(MiniBossState.Idle);
    }

    public override void OnNetworkSpawn()
    {
        isStandaloneMode = false;

        // Register event hooks for network synchronization
        netSpeed.OnValueChanged += (_, v) => ApplySpeedAnim(v);
        attackCounter.OnValueChanged += (_, _) => {
            if (anim != null) anim.SetTrigger(attackTriggers[attackTypeSync.Value]);
        };
        hitCounter.OnValueChanged += (_, _) => { if (anim != null) anim.SetTrigger(hitTrigger); };
        dieCounter.OnValueChanged += (_, _) => { if (anim != null) anim.SetTrigger(dieTrigger); };
        enrageCounter.OnValueChanged += (_, _) => {
            if (anim != null) anim.SetTrigger(enrageTrigger);
            PlayEnrageVFX();
        };
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

        ApplySpeedAnim(netSpeed.Value);

        if (IsServer)
        {
            maxHealth = phase1MaxHealth;
            currentHealth.Value = phase1MaxHealth;
            SnapToNavMesh();
            ChangeState(MiniBossState.Idle);
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
            if (anim != null) anim.SetTrigger(attackTriggers[attackTypeSync.Value]);
        };
        hitCounter.OnValueChanged -= (_, _) => { if (anim != null) anim.SetTrigger(hitTrigger); };
        dieCounter.OnValueChanged -= (_, _) => { if (anim != null) anim.SetTrigger(dieTrigger); };
        enrageCounter.OnValueChanged -= (_, _) => {
            if (anim != null) anim.SetTrigger(enrageTrigger);
            PlayEnrageVFX();
        };
        currentHealth.OnValueChanged -= OnHealthNetChanged;
        deathExplosionCounter.OnValueChanged -= (_, _) => PlayDeathExplosionEffects();
    }

    private void OnHealthNetChanged(float oldVal, float newVal)
    {
        float diff = oldVal - newVal;
        if (diff > 0)
        {
            // Trigger local hit effects
            EnemyDamageEffectHelper.PlayDamageEffects(gameObject, diff);
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

        Debug.Log("[MiniBossAI] Mini Boss has been activated! Combat start!");
    }

    public void TakeDamage(float damage)
    {
        if (IsDead || CurrentStateValue == MiniBossState.Enrage) return;

        localHealth = Mathf.Max(0f, localHealth - damage);
        if (!isStandaloneMode && IsSpawned && IsServer)
        {
            currentHealth.Value = Mathf.Max(0f, currentHealth.Value - damage);
            localHealth = currentHealth.Value;
        }

        EnemyDamageEffectHelper.PlayDamageEffects(gameObject, damage);

        float activeHp = ActualCurrentHealth;
        if (activeHp <= 0f)
        {
            if (!localIsPhase2 && (!IsSpawned || !isPhase2Network.Value))
            {
                ChangeState(MiniBossState.Enrage);
            }
            else
            {
                ChangeState(MiniBossState.Dead);
            }
            return;
        }

        // Trigger stagger hit animation if not in attack or already in hit state
        if (CurrentStateValue != MiniBossState.Attack && CurrentStateValue != MiniBossState.Dead && CurrentStateValue != MiniBossState.Hit)
        {
            ChangeState(MiniBossState.Hit);
        }
    }

    private void Update()
    {
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
            case MiniBossState.Idle:
                currentFSMState = idleState;
                break;
            case MiniBossState.Chase:
                currentFSMState = chaseState;
                break;
            case MiniBossState.Attack:
                currentFSMState = attackState;
                break;
            case MiniBossState.Hit:
                currentFSMState = hitState;
                break;
            case MiniBossState.Enrage:
                currentFSMState = enrageState;
                break;
            case MiniBossState.Dead:
                currentFSMState = deadState;
                break;
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

        // Slow patrol/wander walk around when inactive
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
            SetSpeedNet(0.5f); // Play Walk animation

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
            ChangeState(MiniBossState.Idle);
            return;
        }

        float dist = Vector3.Distance(transform.position, targetPlayer.position);

        if (dist <= attackRange && attackCooldownTimer <= 0)
        {
            if (AgentReady) agent.isStopped = true;
            SetSpeedNet(0f);
            ChangeState(MiniBossState.Attack);
            return;
        }

        // Run towards target
        if (AgentReady)
        {
            agent.isStopped = false;
            agent.speed = IsPhase2 ? (runSpeed * phase2SpeedMultiplier) : runSpeed;
            agent.SetDestination(targetPlayer.position);
        }
        SetSpeedNet(AgentReady && !agent.isStopped ? 1.0f : 0f); // Run animation
        RotateTowards(targetPlayer.position);
    }

    private void HandleAttack()
    {
        stateTimer -= Time.deltaTime;

        // Apply visual and physics movement leap/glide ONLY after the wind-up phase has finished (leapTimer >= 0f)
        if (isLeaping && leapTimer >= 0f)
        {
            leapTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(leapTimer / currentLeapDuration);

            // Move the NavMeshAgent forward along the ground
            if (AgentReady)
            {
                agent.Move(transform.forward * currentLeapForwardSpeed * Time.deltaTime);
            }

            // Offset the visual mesh Y position in a parabolic arc
            if (visualRoot != null)
            {
                float yOffset = currentLeapHeight * Mathf.Sin(Mathf.PI * progress);
                visualRoot.localPosition = new Vector3(0, yOffset, 0);
            }

            if (leapTimer >= currentLeapDuration)
            {
                isLeaping = false;
                if (visualRoot != null) visualRoot.localPosition = Vector3.zero;
            }
        }
        else if (isLeaping && leapTimer < 0f)
        {
            // Just advance the wind-up timer
            leapTimer += Time.deltaTime;
        }

        // Rotate face towards target player ONLY during the wind-up phase (before the actual leap forward starts)
        if (targetPlayer != null && (!isLeaping || leapTimer < 0f))
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

    private void Die()
    {
        if (AgentReady) agent.isStopped = true;
        if (agent != null) agent.enabled = false;
        SetSpeedNet(0f);

        if (visualRoot != null) visualRoot.localPosition = Vector3.zero;

        // Disable colliders
        var colliders = GetComponentsInChildren<Collider>();
        foreach (var c in colliders)
        {
            if (c != null && !c.isTrigger) c.enabled = false;
        }

        // Trigger Death Explosion only if Phase 2
        if (IsPhase2)
        {
            TriggerDeathExplosion();
        }

        if (!isStandaloneMode && IsServer)
        {
            dieCounter.Value++;
        }
        else if (anim != null)
        {
            anim.SetTrigger(dieTrigger);
        }

        Debug.Log("[MiniBossAI] Mini Boss is dead!");
        
        // Destroy object after 5 seconds
        Destroy(gameObject, 5f);
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
                    knockbackDir.y = 0.5f; // Propel players into the air
                    knockbackDir = knockbackDir.normalized;
                    Vector3 force = knockbackDir * explosionKnockback;

                    EnemyDamageHelper.DealDamage(root, explosionDamage, force);
                    Debug.Log($"[MiniBossAI] Player {root.name} caught in death blast! Took {explosionDamage} damage.");
                }
            }
        }
    }

    private void PlayDeathExplosionEffects()
    {
        Vector3 spawnPos = transform.position + Vector3.up * 1.2f;
        if (deathExplosionVFX != null)
        {
            GameObject vfx = Instantiate(deathExplosionVFX, spawnPos, Quaternion.identity);
            Destroy(vfx, 4f);
        }
        if (deathExplosionSound != null)
        {
            AudioSource.PlayClipAtPoint(deathExplosionSound, spawnPos, 1.0f);
        }
        Debug.Log("[MiniBossAI] Death Explosion visual/audio effects triggered locally.");
    }

    public void PlayEnrageVFX()
    {
        Vector3 spawnPos = transform.position + Vector3.up * 1f;
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
        Debug.Log("[MiniBossAI] Enrage Transition VFX/SFX played!");
    }

    // ══════════════════════════════════════════════════════════
    //  TARGET DETECTION & DAMAGE SWEEPS (RAYCAST)
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

        return list;
    }

    private void DetectAndSwitchTarget()
    {
        if (IsDead) return;

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
                bool inFOV = IsBossActive || Vector3.Angle(transform.forward, dir) < fieldOfView / 2f || d <= 4f;

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
        else if (CurrentStateValue == MiniBossState.Chase)
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

    public void DealSwordDamageContinuously(HashSet<Transform> hitThisAttack)
    {
        Vector3 kbDir = transform.forward;
        Vector3 finalKnockback = kbDir * knockbackForce;
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
                    Debug.Log($"[MiniBossAI] Continuous Sword hit on: {root.name}, damage={finalDamage}");
                }
            }
        }
        else
        {
            // Fallback raycast sweep: SphereCast forward
            Vector3 origin = transform.position + Vector3.up * 1f;
            RaycastHit[] hits = Physics.SphereCastAll(origin, swordThickness, transform.forward, attackRange, playerLayer);
            foreach (var hit in hits)
            {
                Transform root = GetPlayerRoot(hit.collider.transform);
                if (root != null && !hitThisAttack.Contains(root))
                {
                    hitThisAttack.Add(root);
                    EnemyDamageHelper.DealDamage(root, finalDamage, finalKnockback);
                    Debug.Log($"[MiniBossAI] Continuous Sword fallback hit on: {root.name}, damage={finalDamage}");
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

    // ══════════════════════════════════════════════════════════
    //  HELPERS
    // ══════════════════════════════════════════════════════════

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
        // Sight range (yellow)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);

        // Attack range (red)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Sword sweep helper (magenta)
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

            // Alternate through the 3 attacks sequentially
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
            
            // Instantly align rotation to face the player at the start of the attack
            if (boss.targetPlayer != null)
            {
                boss.FaceTargetImmediately(boss.targetPlayer.position);
            }
            
            float animMult = boss.IsPhase2 ? boss.phase2AnimSpeed : 1.0f;
            float speedMult = boss.IsPhase2 ? boss.phase2SpeedMultiplier : 1.0f;

            boss.stateTimer = config.duration / animMult;

            // Configure Leap parameters
            boss.isLeaping = true;
            boss.leapTimer = 0f;
            boss.currentLeapDuration = (config.duration * config.leapDurationPercent) / animMult;
            boss.currentLeapForwardSpeed = config.forwardSpeed * speedMult;
            boss.currentLeapHeight = config.peakHeight;

            // Trigger actual leap delay inside state timer
            boss.leapTimer = - ((config.duration * config.leapStartPercent) / animMult);

            // Stop NavMeshAgent pathfinding during attack
            if (boss.AgentReady)
            {
                boss.agent.isStopped = true;
                boss.agent.velocity = Vector3.zero;
            }
        }

        public void Update()
        {
            boss.HandleAttack();

            float animMult = boss.IsPhase2 ? boss.phase2AnimSpeed : 1.0f;
            float totalDuration = config.duration / animMult;
            float elapsedTime = totalDuration - boss.stateTimer;
            float elapsedPercent = elapsedTime / totalDuration;

            if (elapsedPercent >= config.damageStartPercent && elapsedPercent <= config.damageEndPercent)
            {
                boss.DealSwordDamageContinuously(hitPlayersThisAttack);
            }
        }

        public void Exit()
        {
            // Clean up visual positions and increment attack type
            boss.isLeaping = false;
            if (boss.visualRoot != null) boss.visualRoot.localPosition = Vector3.zero;
            boss.currentAttackIndex = (boss.currentAttackIndex + 1) % 3;
        }
    }

    private class HitState : IEnemyState
    {
        private MiniBossAI boss;
        public HitState(MiniBossAI boss) { this.boss = boss; }
        public void Enter()
        {
            boss.stateTimer = boss.hitStaggerDuration;
            if (!boss.isStandaloneMode) boss.hitCounter.Value++;
            else if (boss.anim != null) boss.anim.SetTrigger(boss.hitTrigger);
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
            if (boss.AgentReady) boss.agent.isStopped = true;
            boss.SetSpeedNet(0f);
            boss.stateTimer = boss.enrageDuration;

            if (boss.visualRoot != null) boss.visualRoot.localPosition = Vector3.zero;

            // Reset animation speed multiplier during enrage roar
            if (boss.anim != null) boss.anim.speed = 1.0f;

            if (!boss.isStandaloneMode)
            {
                boss.enrageCounter.Value++;
                boss.maxHealth = boss.phase2MaxHealth;
                boss.currentHealth.Value = boss.phase2MaxHealth;
                boss.isPhase2Network.Value = true;
            }
            else
            {
                if (boss.anim != null) boss.anim.SetTrigger(boss.enrageTrigger);
                boss.PlayEnrageVFX();
                boss.maxHealth = boss.phase2MaxHealth;
                boss.localHealth = boss.phase2MaxHealth;
                boss.localIsPhase2 = true;
            }

            Debug.Log("[MiniBossAI] BOSS ENRAGED! Entering Phase 2: HP=" + boss.phase2MaxHealth + ", speed & anim speed increased!");
        }

        public void Update()
        {
            boss.stateTimer -= Time.deltaTime;
            if (boss.stateTimer <= 0)
            {
                if (boss.targetPlayer != null) boss.ChangeState(MiniBossState.Chase);
                else boss.ChangeState(MiniBossState.Idle);
            }
        }

        public void Exit()
        {
            // Apply speed multiplier on Animator
            if (boss.anim != null)
            {
                boss.anim.speed = boss.phase2AnimSpeed;
            }
        }
    }

    private class DeadState : IEnemyState
    {
        private MiniBossAI boss;
        public DeadState(MiniBossAI boss) { this.boss = boss; }
        public void Enter() { boss.Die(); }
        public void Update() { }
        public void Exit() { }
    }

    // ══════════════════════════════════════════════════════════
    //  DUMMY ANIMATION EVENT RECEIVERS
    // ══════════════════════════════════════════════════════════
    public void OnSkillEAnimEnd() { }
    public void OnSwordSwing() { }
    public void OnKickHit() { }
}
