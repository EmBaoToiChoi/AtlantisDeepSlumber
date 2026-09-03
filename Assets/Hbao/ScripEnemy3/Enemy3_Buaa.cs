using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Enemy 3 - Búa Nặng
/// Animator Float "Speed": 0=Idle, 0.5=Walk (patrol), 1=Run (chase)
/// Attack triggers: quai3attack, quai3combo, quai3runlumpattack
/// </summary>
public class Enemy3_Buaa : NetworkBehaviour
{
    public enum EnemyState { Patrol, Chase, Stagger, Attack, Dead, Flee }

    [Header("Health")]
    public float maxHealth = 500f;
    public NetworkVariable<float> currentHealth = new NetworkVariable<float>(500f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<EnemyState> currentState = new NetworkVariable<EnemyState>(EnemyState.Patrol, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Network Anim Sync")]
    public NetworkVariable<float> netSpeed = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> hitCounter = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> attackType = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private float localHealth;
    private EnemyState localState = EnemyState.Patrol;
    private bool isStandaloneMode;
    private bool IsNetworkActive => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    private EnemyState CurrentStateValue { get => isStandaloneMode ? localState : currentState.Value; set { if (isStandaloneMode) localState = value; else currentState.Value = value; } }
    private float CurrentHealthValue { get => isStandaloneMode ? localHealth : currentHealth.Value; set { if (isStandaloneMode) localHealth = value; else currentHealth.Value = value; } }
    public bool IsDead => isStandaloneMode ? (localState == EnemyState.Dead) : (currentState.Value == EnemyState.Dead);
    public float ActualCurrentHealth => (isStandaloneMode || !IsSpawned) ? localHealth : currentHealth.Value;

    [Header("Components")]
    public NavMeshAgent agent;
    public Animator anim;
    public Transform eyeTransform;

    [Header("Melee Hitbox (Bypassed - now using raycast/cone sweeps)")]
    public GameObject hammerHitbox;

    [Header("Patrol Waypoints (3 điểm hình tam giác)")]
    public Transform[] waypoints = new Transform[3];

    [Header("Drops")]
    public GameObject expGemPrefab;
    public float expDropAmount = 35f;
    public Transform expDropPoint;
    public GameObject repairItemPrefab;
    [Range(0f, 1f)] public float repairItemDropChance = 0.35f;

    [Header("Hit & Death Sound / VFX")]
    public AudioClip hitSoundClip;
    public AudioClip deathSoundClip;
    public GameObject deathVfxPrefab;
    [Tooltip("Kích thước/Bán kính hiển thị của VFX hố rớt bên dưới chân quái để ôm trọn xác quái.")]
    public float deathVfxScale = 2.2f;

    [Header("Stun VFX Settings")]
    public GameObject stunVfxPrefab;
    public float stunVfxHeightOffset = 2.6f;
    public float stunVfxScale = 2.2f;

    [Header("AI Settings")]
    public float sightRange = 14f;
    public float fieldOfView = 95f;
    public float attackRange = 2.5f;
    public float maxChaseDistance = 18.0f;
    public float patrolWalkSpeed = 2.5f;
    public float chaseRunSpeed   = 5f;
    public float patrolWaitMin = 1.5f;
    public float patrolWaitMax = 5f;
    [Range(0f, 1f)] public float patrolMoveChance = 0.7f;

    [Header("Layers")]
    public LayerMask playerLayer;
    public LayerMask obstacleLayer;

    [Header("Animator Parameter Names")]
    [Tooltip("Float: 0=Idle, 0.5=Walk, 1=Run — Base Layer")]
    public string speedParam   = "Speed";              // Float  — Base Layer
    public string hitTrigger   = "quai3Anhit";         // Trigger — Anhit Layer
    public string dieTrigger   = "quai3Die";           // Trigger — Base Layer
    public string atk1Trigger  = "quai3attack";        // Trigger — Attack Layer
    public string atk2Trigger  = "quai3combo";         // Trigger — Attack Layer
    public string atk3Trigger  = "quai3runlumpattack"; // Trigger — Attack Layer

    private Transform targetPlayer;
    private int currentWaypointIndex = -1;
    private bool waitingAtWaypoint;
    private float waypointWaitTimer;
    private float detectionTimer;
    private const float DETECTION_INTERVAL = 0.15f;
    private float staggerTimer;
    private float staggerCooldownTimer;
    private float attackCooldownTimer;
    private float attackDuration;
    private float stateTimer;
    private bool hasDealtDamage1, hasDealtDamage2;
    private int attackSwingCount;
    private float lastDamageTime;
    private int recentHitCount;
    private bool isFrenzied, isEnraged;
    private float loseSightTimer;
    [Header("Tactical Flee AI")]
    public bool canTacticalFlee = true;
    public float fleeHealthThreshold = 0.25f;
    private bool hasFledTactically = false;
    private float fleeTimer;

    // ─── FSM States ───
    private IEnemyState currentFSMState;
    private PatrolState patrolState;
    private ChaseState chaseState;
    private AttackState attackState;
    private StaggerState staggerState;
    private DeadState deadState;
    private FleeState fleeState;
    public Renderer[] modelRenderers;

    private readonly Collider[] detectionResults = new Collider[8];
    private readonly Collider[] damageResults    = new Collider[8];
    private bool AgentReady => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;

    private void Awake()
    {
        gameObject.tag = "Enemy";
        if (agent == null) agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null && !agent.enabled) agent.enabled = true;
        if (anim == null) anim = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);

        if (obstacleLayer.value == 0)
        {
            obstacleLayer = LayerMask.GetMask("Default", "Obstacle", "Environment", "Ground", "Wall", "Map", "Structure", "Terrain");
            if (obstacleLayer.value == 0)
            {
                obstacleLayer = ~(LayerMask.GetMask("Player", "Enemy", "Ignore Raycast", "UI", "Water"));
            }
        }
        // Luôn loại bỏ layer Player và Enemy khỏi obstacleLayer để tia Raycast không đụng nhầm
        int excludeMask = LayerMask.GetMask("Player", "Enemy", "Ignore Raycast", "UI");
        if (excludeMask != 0) obstacleLayer &= ~excludeMask;

        if (playerLayer.value == 0)
        {
            playerLayer = LayerMask.GetMask("Player");
            if (playerLayer.value == 0)
            {
                playerLayer = ~0;
            }
        }

        var na = GetComponent<Unity.Netcode.Components.NetworkAnimator>();
        if (na != null)
        {
            if (anim != null) na.Animator = anim;
            if (anim == null || anim.runtimeAnimatorController == null) na.enabled = false;
        }

        if (anim != null)
        {
            bool hasQuai3Attack = false;
            bool hasAttack = false;
            bool hasQuai3Combo = false;
            bool hasCombo = false;
            bool hasQuai3RunLump = false;
            bool hasRunLumpAttack = false;
            bool hasQuai3Anhit = false;
            bool hasAnhit = false;
            bool hasQuai3Die = false;
            bool hasDie = false;

            foreach (var param in anim.parameters)
            {
                if (param.name == "quai3attack") hasQuai3Attack = true;
                if (param.name == "Attack") hasAttack = true;
                if (param.name == "quai3combo") hasQuai3Combo = true;
                if (param.name == "Combo") hasCombo = true;
                if (param.name == "quai3runlumpattack") hasQuai3RunLump = true;
                if (param.name == "RunLumpAttack" || param.name == "RunLump") hasRunLumpAttack = true;
                if (param.name == "quai3Anhit") hasQuai3Anhit = true;
                if (param.name == "Anhit") hasAnhit = true;
                if (param.name == "quai3Die") hasQuai3Die = true;
                if (param.name == "Die") hasDie = true;
            }

            if (hasAttack && !hasQuai3Attack) atk1Trigger = "Attack";
            if (hasCombo && !hasQuai3Combo) atk2Trigger = "Combo";
            if (hasRunLumpAttack && !hasQuai3RunLump) atk3Trigger = "RunLumpAttack";
            if (hasAnhit && !hasQuai3Anhit) hitTrigger = "Anhit";
            if (hasDie && !hasQuai3Die) dieTrigger = "Die";
        }

        // Initialize state instances for FSM
        localHealth = maxHealth;
        patrolState = new PatrolState(this);
        chaseState = new ChaseState(this);
        attackState = new AttackState(this);
        staggerState = new StaggerState(this);
        deadState = new DeadState(this);
        fleeState = new FleeState(this);
    }

    private void Start() { if (!IsNetworkActive) { isStandaloneMode = true; InitStandalone(); } }

    private void InitStandalone()
    {
        localHealth = maxHealth;
        SnapToNavMesh(); if (hammerHitbox != null) hammerHitbox.SetActive(false);
        ApplySpeedAnim(0f); 
        ChangeState(EnemyState.Patrol);
        GoToNextWaypoint();
    }

    public override void OnNetworkSpawn()
    {
        isStandaloneMode = false;
        var nt = GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (nt != null) { nt.PositionThreshold = 0.001f; nt.RotAngleThreshold = 0.01f; nt.ScaleThreshold = 0.01f; }
        netSpeed.OnValueChanged       += (_, v) => ApplySpeedAnim(v);
        hitCounter.OnValueChanged     += (_, _) => { if (anim != null) anim.SetTrigger(hitTrigger); };
        currentHealth.OnValueChanged  += OnHealthNetChanged;
        currentState.OnValueChanged   += OnStateChanged;
        ApplySpeedAnim(netSpeed.Value);
        if (IsServer) { currentHealth.Value = maxHealth; SnapToNavMesh(); if (hammerHitbox != null) hammerHitbox.SetActive(false); ChangeState(EnemyState.Patrol); GoToNextWaypoint(); }
        else { if (agent != null) agent.enabled = false; OnStateChanged(EnemyState.Patrol, currentState.Value); }
    }

    public override void OnNetworkDespawn()
    {
        netSpeed.OnValueChanged       -= (_, v) => ApplySpeedAnim(v);
        hitCounter.OnValueChanged     -= (_, _) => { if (anim != null) anim.SetTrigger(hitTrigger); };
        currentHealth.OnValueChanged  -= OnHealthNetChanged;
        currentState.OnValueChanged   -= OnStateChanged;
    }

    private void OnStateChanged(EnemyState oldState, EnemyState newState)
    {
        if (isStandaloneMode) localState = newState;
        if (newState == EnemyState.Patrol)
        {
            targetPlayer = null;
        }
        if (newState == EnemyState.Stagger)
        {
            EnemyStunVfxBehaviour.ApplyStunVfx(gameObject, staggerTimer > 0 ? staggerTimer : 5.0f, stunVfxPrefab, stunVfxHeightOffset, stunVfxScale);
        }
        else
        {
            EnemyStunVfxBehaviour.RemoveStunVfx(gameObject);
        }
        if (newState == EnemyState.Dead)
        {
            if (!IsServer)
            {
                ApplyLocalDeathEffects();
            }
        }
    }

    public void DisableHeadUI()
    {
        var uiDocs = GetComponentsInChildren<UnityEngine.UIElements.UIDocument>(true);
        foreach (var doc in uiDocs) { if (doc != null) doc.gameObject.SetActive(false); }

        var healthBars = GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var hb in healthBars)
        {
            if (hb is EnemyHealthBar || hb is ZombieHealthBar || hb is Enemy3HealthBar || hb is Enemy4HealthBar || hb is Enemy5HealthBar)
            {
                hb.gameObject.SetActive(false);
            }
        }

        Transform quad = transform.Find("Quad");
        if (quad != null) quad.gameObject.SetActive(false);
        Transform headUi = transform.Find("HeadUI");
        if (headUi != null) headUi.gameObject.SetActive(false);
    }

    private void ApplyLocalDeathEffects()
    {
        DisableHeadUI();
        if (hammerHitbox != null) hammerHitbox.SetActive(false);
        if (anim != null)
        {
            anim.ResetTrigger(atk1Trigger);
            anim.ResetTrigger(atk2Trigger);
            anim.ResetTrigger(atk3Trigger);
            anim.ResetTrigger(hitTrigger);
            anim.ResetTrigger(dieTrigger);

            for (int i = 1; i < anim.layerCount; i++)
            {
                anim.SetLayerWeight(i, 0f);
            }
            anim.Play("quai3 die", 0, 0f);
        }

        EnemyStunVfxBehaviour.RemoveStunVfx(gameObject);
        EnemyDeathSinkBehaviour.ApplyDeathEffects(gameObject, deathSoundClip, deathVfxPrefab, deathVfxScale);
    }

    private void OnHealthNetChanged(float oldVal, float newVal)
    {
        localHealth = newVal;
        if (newVal <= 0f)
        {
            DisableHeadUI();
            if (CurrentStateValue != EnemyState.Dead)
            {
                ApplyLocalDeathEffects();
            }
        }
        float diff = oldVal - newVal;
        if (diff > 0)
        {
            EnemyDamageEffectHelper.PlayDamageEffects(gameObject, diff, hitSoundClip);
        }
    }

    private void Update()
    {
        float hp = CurrentHealthValue / maxHealth;
        isFrenzied = hp <= 0.3f; isEnraged = hp <= 0.6f && !isFrenzied;
        UpdateEnrageVisuals();

        bool aiAuth = isStandaloneMode || (IsNetworkActive && IsServer);
        if (!aiAuth) return;
        if (agent != null && agent.isActiveAndEnabled && !agent.isOnNavMesh) SnapToNavMesh();

        // --- BỘ GIẢI QUYẾT VA CHẠM TRÁNH XUYÊN TƯỜNG (WALL COLLISION RESOLVER) ---
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh && agent.velocity.sqrMagnitude > 0.01f)
        {
            Vector3 rayOrigin = transform.position + Vector3.up * 1.0f;
            Vector3 moveDir = agent.velocity.normalized;
            if (Physics.Raycast(rayOrigin, moveDir, out RaycastHit hit, 0.8f))
            {
                if (!hit.collider.CompareTag("Player") && !hit.collider.CompareTag("Enemy") && hit.collider.gameObject.layer != LayerMask.NameToLayer("Enemy") && !hit.collider.name.ToLower().Contains("skeleton") && !hit.collider.isTrigger)
                {
                    Vector3 pushBack = hit.normal * 0.15f;
                    agent.Warp(transform.position + pushBack);
                }
            }
        }
        // ------------------------------------------------------------------------

        if (staggerCooldownTimer > 0f) staggerCooldownTimer -= Time.deltaTime;
        if (attackCooldownTimer > 0) attackCooldownTimer -= Time.deltaTime;
        detectionTimer -= Time.deltaTime;
        if (detectionTimer <= 0) { detectionTimer = DETECTION_INTERVAL; DetectPlayer(); }
        if (currentFSMState != null)
        {
            currentFSMState.Update();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, sightRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(GetPatrolCenterPosition(), maxChaseDistance);
    }



    private void UpdateEnrageVisuals()
    {
        if (modelRenderers == null || modelRenderers.Length == 0) return;
        if (isFrenzied) { float pp = Mathf.PingPong(Time.time * 4f, 1f); Color c = Color.Lerp(Color.white, new Color(1f, 0.25f, 0f), pp); foreach (var r in modelRenderers) { if (r != null && r.material != null) r.material.color = c; } }
        else if (isEnraged) { Color c = new Color(1f, 0.75f, 0.5f); foreach (var r in modelRenderers) { if (r != null && r.material != null) r.material.color = c; } }
    }

    // ── Patrol (Walk) ──
    private Vector3 GetRandomNavMeshPosition(float range)
    {
        for (int i = 0; i < 30; i++)
        {
            Vector3 randomDirection = Random.insideUnitSphere * range;
            randomDirection += transform.position;
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(randomDirection, out navHit, range, NavMesh.AllAreas))
            {
                if (Vector3.Distance(transform.position, navHit.position) > 2.0f)
                {
                    return navHit.position;
                }
            }
        }
        return transform.position;
    }

    private Vector3 GetUniquePatrolPosition(Vector3 basePos)
    {
        float angle = (System.Math.Abs(GetHashCode()) % 8) * 45f;
        Vector3 offset = Quaternion.Euler(0, angle, 0) * Vector3.forward * 1.8f;
        Vector3 targetPos = basePos + offset;

        Collider[] cols = Physics.OverlapSphere(targetPos, 1.5f);
        foreach (var c in cols)
        {
            if (c != null && c.gameObject != gameObject && (c.CompareTag("Enemy") || c.gameObject.layer == LayerMask.NameToLayer("Enemy")))
            {
                Vector3 shift = (targetPos - c.transform.position).normalized * 1.5f;
                targetPos += shift;
            }
        }

        if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
        {
            return hit.position;
        }
        return basePos;
    }

    private void GoToNextWaypoint()
    {
        bool hasWaypoints = false;
        if (waypoints != null && waypoints.Length > 0)
        {
            foreach (var wp in waypoints)
            {
                if (wp != null) { hasWaypoints = true; break; }
            }
        }

        Vector3 nextPosition;
        if (hasWaypoints)
        {
            int n;
            int attempts = 0;
            do { n = Random.Range(0, waypoints.Length); attempts++; } while (waypoints[n] == null && attempts < 10);
            if (waypoints[n] == null)
            {
                nextPosition = GetUniquePatrolPosition(GetRandomNavMeshPosition(12f));
            }
            else
            {
                currentWaypointIndex = n;
                nextPosition = GetUniquePatrolPosition(waypoints[currentWaypointIndex].position);
            }
        }
        else
        {
            nextPosition = GetUniquePatrolPosition(GetRandomNavMeshPosition(12f));
        }

        waitingAtWaypoint = false;
        if (AgentReady)
        {
            agent.isStopped = false;
            agent.speed = patrolWalkSpeed;
            agent.SetDestination(nextPosition);
        }
    }

    private void ApplyPatrolEnemySeparation()
    {
        if (!AgentReady) return;

        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        if (agent.radius < 0.45f) agent.radius = 0.45f;
        agent.avoidancePriority = Mathf.Clamp(30 + System.Math.Abs(GetHashCode() % 40), 10, 80);

        Collider[] nearby = Physics.OverlapSphere(transform.position, 1.8f);
        Vector3 separation = Vector3.zero;
        int count = 0;
        foreach (var col in nearby)
        {
            if (col != null && col.gameObject != gameObject && (col.CompareTag("Enemy") || col.gameObject.layer == LayerMask.NameToLayer("Enemy")))
            {
                Vector3 diff = transform.position - col.transform.position;
                diff.y = 0;
                float dist = diff.magnitude;
                if (dist > 0.01f && dist < 1.8f)
                {
                    separation += diff.normalized * ((1.8f - dist) / 1.8f);
                    count++;
                }
            }
        }

        if (count > 0 && !waitingAtWaypoint)
        {
            separation /= count;
            agent.Move(separation * Time.deltaTime * 1.5f);
        }
    }

    private void CheckForwardMapBoundaryAndTurn()
    {
        if (!AgentReady || waitingAtWaypoint) return;

        Vector3 forwardPos = transform.position + transform.forward * 1.8f;
        bool hasNavMeshAhead = NavMesh.SamplePosition(forwardPos, out NavMeshHit hit, 1.2f, NavMesh.AllAreas);
        bool hasGroundAhead = Physics.Raycast(forwardPos + Vector3.up * 1f, Vector3.down, 2.5f);

        if (!hasNavMeshAhead || !hasGroundAhead)
        {
            transform.rotation = Quaternion.LookRotation(-transform.forward);
            GoToNextWaypoint();
        }
    }

    private void HandlePatrol()
    {
        if (targetPlayer != null) { ChangeState(EnemyState.Chase); return; }
        if (!AgentReady) return;

        ApplyPatrolEnemySeparation();
        CheckForwardMapBoundaryAndTurn();

        // 1. Gặp tường: Quay mặt né tường ngay lập tức và chuyển hướng
        if (Physics.Raycast(transform.position + Vector3.up * 0.8f, transform.forward, out RaycastHit wallHit, 1.2f, obstacleLayer, QueryTriggerInteraction.Ignore))
        {
            if (!wallHit.collider.CompareTag("Player") && !wallHit.collider.CompareTag("Enemy"))
            {
                Vector3 avoidDir = Vector3.Reflect(transform.forward, wallHit.normal);
                avoidDir.y = 0;
                if (avoidDir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(avoidDir.normalized);
                GoToNextWaypoint();
                return;
            }
        }

        if (waitingAtWaypoint)
        {
            if (AgentReady) agent.isStopped = true;
            SetSpeedNet(0f);
            waypointWaitTimer -= Time.deltaTime;
            if (waypointWaitTimer <= 0f)
            {
                GoToNextWaypoint();
            }
        }
        else
        {
            if (AgentReady)
            {
                if (agent.isStopped) agent.isStopped = false;

                bool isMoving = agent.velocity.magnitude > 0.15f;
                SetSpeedNet(isMoving ? 0.5f : 0f);

                if (!agent.pathPending && agent.hasPath)
                {
                    if (agent.remainingDistance > 0.1f && agent.remainingDistance <= agent.stoppingDistance + 0.5f)
                    {
                        agent.isStopped = true;
                        SetSpeedNet(0f);
                        waitingAtWaypoint = true;
                        waypointWaitTimer = Random.Range(2.0f, 3.0f);
                    }
                }
                else if (!agent.pathPending && (!agent.hasPath || agent.pathStatus == NavMeshPathStatus.PathInvalid))
                {
                    GoToNextWaypoint();
                }
            }
            else
            {
                SnapToNavMesh();
                if (AgentReady) GoToNextWaypoint();
            }
        }
    }

    private void HandleChase()
    {
        if (targetPlayer == null || !IsPlayerAliveAndValid(targetPlayer)) { targetPlayer = null; ReturnToPatrol(); return; }

        // 1. Kiểm tra nếu Player hiện tại không nằm trên NavMesh hoặc đường đi bị đứt đoạn
        if (!IsTargetReachableOnNavMesh(targetPlayer))
        {
            Transform altTarget = null;
            var alivePlayers = GetAllAlivePlayers();
            foreach (var pt in alivePlayers)
            {
                if (pt != null && pt != targetPlayer && IsPlayerAliveAndValid(pt))
                {
                    int chasers = GetChaserCountForPlayer(pt);
                    if (chasers < 2 || alivePlayers.Count == 1)
                    {
                        float d = Vector3.Distance(transform.position, pt.position);
                        if (d <= sightRange && IsTargetReachableOnNavMesh(pt))
                        {
                            altTarget = pt;
                            break;
                        }
                    }
                }
            }

            if (altTarget != null)
            {
                targetPlayer = altTarget;
                AlertNearbyAllies(targetPlayer);
            }
            else
            {
                targetPlayer = null;
                ReturnToPatrol();
                return;
            }
        }

        // 2. Kiểm tra nếu Player đã chạy vượt quá vùng rượt đuổi tối đa (maxChaseDistance)
        float dToPatrolCenter = Vector3.Distance(GetPatrolCenterPosition(), targetPlayer.position);
        float dToSelf = Vector3.Distance(transform.position, targetPlayer.position);

        if (dToSelf > maxChaseDistance || dToPatrolCenter > maxChaseDistance + 4f)
        {
            targetPlayer = null;
            ReturnToPatrol();
            return;
        }

        Vector3 ep1 = eyeTransform != null ? eyeTransform.position : transform.position + Vector3.up * 1.5f;
        Vector3 targetCenter = targetPlayer.position + Vector3.up * 1.0f;
        float dToPlayer = Vector3.Distance(ep1, targetCenter);
        Vector3 dirToPlayer = (targetCenter - ep1).normalized;

        bool wallBlocked = false;
        if (dToPlayer > 3.5f)
        {
            if (Physics.Raycast(ep1, dirToPlayer, out RaycastHit h1, dToPlayer - 0.3f, obstacleLayer, QueryTriggerInteraction.Ignore))
            {
                if (h1.collider != null && h1.transform != targetPlayer && !h1.transform.IsChildOf(targetPlayer) && !h1.collider.CompareTag("Player"))
                {
                    wallBlocked = true;
                }
            }
        }

        if (wallBlocked)
        {
            loseSightTimer += Time.deltaTime;
            if (loseSightTimer > 1.5f)
            {
                targetPlayer = null;
                ReturnToPatrol();
                return;
            }
        }
        else
        {
            loseSightTimer = 0f;
        }
        Vector3 ld = (targetPlayer.position - transform.position); ld.y = 0;
        if (ld.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(ld), Time.deltaTime * 12f);
        Vector3 flatEnemy = transform.position; flatEnemy.y = 0;
        Vector3 flatPlayer = targetPlayer.position; flatPlayer.y = 0;
        float dist = Vector3.Distance(flatEnemy, flatPlayer);
        
        if (dist <= attackRange)
        {
            if (AgentReady) agent.isStopped = true;
            SetSpeedNet(0f); // Dừng lại tại chỗ
            if (attackCooldownTimer <= 0)
            {
                ChangeState(EnemyState.Attack);
            }
            return;
        }
        
        float spd = isFrenzied ? chaseRunSpeed * 1.5f : (isEnraged ? chaseRunSpeed * 1.2f : chaseRunSpeed);
        if (AgentReady)
        {
            agent.isStopped = false;
            agent.speed = spd;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            if (agent.radius < 0.45f) agent.radius = 0.45f;

            float distToPlayer = Vector3.Distance(transform.position, targetPlayer.position);
            agent.avoidancePriority = Mathf.Clamp(10 + Mathf.RoundToInt(distToPlayer * 4f), 5, 95);

            Vector3 chaseDestination = GetPredictedTargetPosition(targetPlayer);
            int chasers = GetChaserCountForPlayer(targetPlayer);
            if (chasers == 1)
            {
                Vector3 dirToP = (targetPlayer.position - transform.position).normalized;
                Vector3 sideDir = Vector3.Cross(dirToP, Vector3.up) * (System.Math.Abs(GetHashCode()) % 2 == 0 ? 1.6f : -1.6f);
                Vector3 flankPos = chaseDestination + sideDir;
                if (NavMesh.SamplePosition(flankPos, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
                {
                    chaseDestination = hit.position;
                }
            }

            agent.SetDestination(chaseDestination);

            if (agent.pathStatus == NavMeshPathStatus.PathInvalid || 
               (!agent.pathPending && agent.hasPath && agent.pathStatus == NavMeshPathStatus.PathPartial && agent.remainingDistance < 1.5f))
            {
                Transform alt = null;
                var alivePlayers = GetAllAlivePlayers();
                foreach (var pt in alivePlayers)
                {
                    if (pt != null && pt != targetPlayer && IsPlayerAliveAndValid(pt) && IsTargetReachableOnNavMesh(pt))
                    {
                        if (GetChaserCountForPlayer(pt) < 2 || alivePlayers.Count == 1)
                        {
                            alt = pt;
                            break;
                        }
                    }
                }
                if (alt != null)
                {
                    targetPlayer = alt;
                }
                else
                {
                    targetPlayer = null;
                    ReturnToPatrol();
                    return;
                }
            }
        }
        bool isMoving = AgentReady && agent.velocity.magnitude > 0.2f;
        SetSpeedNet(isMoving ? 1f : 0f);
    }

    private Vector3 GetPredictedTargetPosition(Transform player)
    {
        if (player == null) return transform.position;
        Vector3 pPos = player.position;

        Vector3 velocity = Vector3.zero;
        var rb = player.GetComponent<Rigidbody>() ?? player.GetComponentInChildren<Rigidbody>();
        if (rb != null) velocity = rb.linearVelocity;
        else
        {
            var cc = player.GetComponent<CharacterController>() ?? player.GetComponentInChildren<CharacterController>();
            if (cc != null) velocity = cc.velocity;
        }

        velocity.y = 0;
        if (velocity.sqrMagnitude > 0.5f)
        {
            Vector3 predicted = pPos + velocity.normalized * Mathf.Min(velocity.magnitude * 0.4f, 2.5f);
            if (NavMesh.SamplePosition(predicted, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
            {
                return hit.position;
            }
        }
        return pPos;
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

    private Vector3 GetPatrolCenterPosition()
    {
        if (waypoints != null && waypoints.Length > 0 && waypoints[0] != null)
        {
            return waypoints[0].position;
        }
        return transform.position;
    }

    public void AlertNearbyAllies(Transform target, float radius = 15f)
    {
        if (target == null) return;
        Collider[] allies = Physics.OverlapSphere(transform.position, radius);
        foreach (var col in allies)
        {
            if (col == null || col.gameObject == gameObject) continue;
            var e1 = col.GetComponentInParent<Enemy1_DapBua>();
            if (e1 != null && !e1.HasTargetPlayer) e1.SetTargetPlayer(target);
            var e2 = col.GetComponentInParent<Enemy2_Zombie>();
            if (e2 != null && !e2.HasTargetPlayer) e2.SetTargetPlayer(target);
            var e3 = col.GetComponentInParent<Enemy3_Buaa>();
            if (e3 != null && !e3.HasTargetPlayer) e3.SetTargetPlayer(target);
            var e4 = col.GetComponentInParent<Enemy4_Bongtoi>();
            if (e4 != null && !e4.HasTargetPlayer) e4.SetTargetPlayer(target);
            var e5 = col.GetComponentInParent<Enemy5_PhuThuy>();
            if (e5 != null && !e5.HasTargetPlayer) e5.SetTargetPlayer(target);
        }
    }

    private int GetChaserCountForPlayer(Transform pt)
    {
        if (pt == null) return 0;
        int chasers = 0;
        Collider[] enemies = Physics.OverlapSphere(pt.position, sightRange * 1.2f);
        System.Collections.Generic.HashSet<GameObject> countedEnemies = new System.Collections.Generic.HashSet<GameObject>();

        foreach (var c in enemies)
        {
            if (c == null) continue;
            MonoBehaviour otherEnemy = c.GetComponentInParent<MonoBehaviour>();
            if (otherEnemy == null) otherEnemy = c.GetComponent<MonoBehaviour>();
            if (otherEnemy != null && otherEnemy.gameObject != gameObject && !countedEnemies.Contains(otherEnemy.gameObject))
            {
                countedEnemies.Add(otherEnemy.gameObject);
                Transform target = GetEnemyTargetPlayer(otherEnemy);
                if (target == pt)
                {
                    chasers++;
                }
            }
        }
        return chasers;
    }

    private Transform GetEnemyTargetPlayer(MonoBehaviour mono)
    {
        if (mono == null) return null;
        try
        {
            var type = mono.GetType();
            var field = type.GetField("targetPlayer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                return field.GetValue(mono) as Transform;
            }
        }
        catch { }
        return null;
    }

    public bool HasTargetPlayer => targetPlayer != null;

    public void SetTargetPlayer(Transform player)
    {
        if (player == null || IsDead) return;
        if (targetPlayer == null)
        {
            targetPlayer = player;
            if (CurrentStateValue == EnemyState.Patrol)
            {
                ChangeState(EnemyState.Chase);
            }
        }
    }

    private void DetectPlayer()
    {
        EnemyState s = CurrentStateValue;
        if (s == EnemyState.Dead || s == EnemyState.Stagger || s == EnemyState.Attack) return;

        Vector3 ep = eyeTransform != null ? eyeTransform.position : transform.position + Vector3.up * 1.5f;
        System.Collections.Generic.List<Transform> alivePlayers = GetAllAlivePlayers();

        bool found = false;
        Transform bestTarget = null;
        float minD = float.MaxValue;

        // Nếu mục tiêu hiện tại đã bị >= 2 quái khác rượt đuổi, tự động quét xem có player nào khác rảnh hơn không
        if (targetPlayer != null && IsPlayerAliveAndValid(targetPlayer))
        {
            int currentChasers = GetChaserCountForPlayer(targetPlayer);
            if (currentChasers >= 2 && alivePlayers.Count > 1)
            {
                foreach (var otherPt in alivePlayers)
                {
                    if (otherPt != null && otherPt != targetPlayer && IsPlayerAliveAndValid(otherPt) && IsTargetReachableOnNavMesh(otherPt))
                    {
                        if (GetChaserCountForPlayer(otherPt) < 2)
                        {
                            float dOther = GetNavMeshPathDistance(transform.position, otherPt.position);
                            if (dOther <= sightRange)
                            {
                                targetPlayer = otherPt;
                                break;
                            }
                        }
                    }
                }
            }
        }

        float currentTargetDist = float.MaxValue;
        if (targetPlayer != null)
        {
            if (IsPlayerAliveAndValid(targetPlayer))
            {
                currentTargetDist = GetNavMeshPathDistance(transform.position, targetPlayer.position);
            }
            else
            {
                targetPlayer = null;
            }
        }

        foreach (var pt in alivePlayers)
        {
            if (pt == null || !IsPlayerAliveAndValid(pt)) continue;

            // Bỏ qua nếu Player đứng ngoài NavMesh hoặc không có đường đi tới
            if (!IsTargetReachableOnNavMesh(pt)) continue;

            Vector3 center = pt.position + Vector3.up * 1.0f;
            float d = Vector3.Distance(ep, center);
            if (d > sightRange) continue;

            // Kiểm tra số lượng quái đang dí player này (Tối đa 2 quái dí 1 player)
            int chasers = GetChaserCountForPlayer(pt);
            if (chasers >= 2 && pt != targetPlayer && alivePlayers.Count > 1)
            {
                continue;
            }

            Vector3 dir = (center - ep).normalized;

            // 1. Cảm biến Âm thanh (Nghe tiếng bước chân chạy hoặc giao tranh phía sau lưng trong 8.5m)
            bool isMovingFast = false;
            var rb = pt.GetComponent<Rigidbody>() ?? pt.GetComponentInChildren<Rigidbody>();
            if (rb != null && rb.linearVelocity.sqrMagnitude > 2.5f) isMovingFast = true;
            else {
                var cc = pt.GetComponent<CharacterController>() ?? pt.GetComponentInChildren<CharacterController>();
                if (cc != null && cc.velocity.sqrMagnitude > 2.5f) isMovingFast = true;
            }

            bool hearingSound = d <= 8.5f && isMovingFast;
            bool inFOV = Vector3.Angle(transform.forward, dir) < fieldOfView / 2f;
            bool isProximity = d <= 5.5f;

            if (inFOV || isProximity || hearingSound || pt == targetPlayer)
            {
                bool clearLOS = true;
                if (d > 3.0f)
                {
                    if (Physics.Raycast(ep, dir, out RaycastHit hit, d - 0.2f, obstacleLayer, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.collider != null && hit.transform != pt && !hit.transform.IsChildOf(pt) && !hit.collider.CompareTag("Player"))
                        {
                            clearLOS = false;
                        }
                    }
                }

                if (clearLOS)
                {
                    float pathDist = GetNavMeshPathDistance(transform.position, pt.position);
                    if (pathDist < minD)
                    {
                        minD = pathDist;
                        bestTarget = pt;
                        found = true;
                    }
                }
            }
        }

        if (found && bestTarget != null)
        {
            bool shouldSwitch = false;
            if (targetPlayer == null)
            {
                shouldSwitch = true;
            }
            else if (bestTarget != targetPlayer)
            {
                if (minD < currentTargetDist - 1.8f || minD < 4.5f)
                {
                    shouldSwitch = true;
                }
                else if (minD <= 7.0f && (System.Math.Abs(GetHashCode()) % 2 == 0))
                {
                    shouldSwitch = true;
                }
            }

            if (shouldSwitch)
            {
                targetPlayer = bestTarget;
            }

            AlertNearbyAllies(targetPlayer);
            if (s != EnemyState.Chase) ChangeState(EnemyState.Chase);
        }
    }

    public Vector3 GetSurroundingPosition(Transform target, float desiredDist)
    {
        if (target == null) return transform.position;

        // 1. Calculate direction from target to this enemy
        Vector3 toEnemy = transform.position - target.position;
        toEnemy.y = 0;
        if (toEnemy.sqrMagnitude < 0.001f) toEnemy = transform.forward;
        else toEnemy.Normalize();

        // 2. Separation force from nearby enemies to prevent clumping
        Vector3 separation = Vector3.zero;
        int count = 0;
        Collider[] nearby = Physics.OverlapSphere(transform.position, 2.2f);
        foreach (var col in nearby)
        {
            if (col != null && col.gameObject != gameObject && (col.CompareTag("Enemy") || col.gameObject.layer == LayerMask.NameToLayer("Enemy")))
            {
                Vector3 diff = transform.position - col.transform.position;
                diff.y = 0;
                float dist = diff.magnitude;
                if (dist > 0.01f && dist < 2.2f)
                {
                    separation += diff.normalized * ((2.2f - dist) / 2.2f);
                    count++;
                }
            }
        }
        if (count > 0) separation /= count;

        // 3. Blend direction toward target ring position with separation vector
        Vector3 finalDir = (toEnemy + separation * 1.2f).normalized;
        Vector3 desiredPos = target.position + finalDir * desiredDist;

        if (NavMesh.SamplePosition(desiredPos, out NavMeshHit hit, 3.5f, NavMesh.AllAreas))
        {
            return hit.position;
        }
        return target.position;
    }

    private void ReturnToPatrol()
    {
        targetPlayer = null;
        ChangeState(EnemyState.Patrol);
        waitingAtWaypoint = false;
        waypointWaitTimer = 0f;
        if (AgentReady)
        {
            agent.isStopped = false;
            agent.ResetPath();
        }
        SetSpeedNet(0f);
        GoToNextWaypoint();
    }


    private void HandleStagger() { if (AgentReady) agent.isStopped = true; SetSpeedNet(0f); staggerTimer -= Time.deltaTime; if (staggerTimer <= 0) { if (targetPlayer != null && IsPlayerAliveAndValid(targetPlayer)) ChangeState(EnemyState.Chase); else ReturnToPatrol(); } }

    private void HandleAttack()
    {
        if (targetPlayer == null || !IsPlayerAliveAndValid(targetPlayer))
        {
            targetPlayer = null;
            EndAttack();
            return;
        }
        if (AgentReady) agent.isStopped = true; SetSpeedNet(0f);
        stateTimer -= Time.deltaTime;
        float elapsed = attackDuration - stateTimer;
        if (elapsed < attackDuration * 0.35f) { Vector3 ld = (targetPlayer.position - transform.position); ld.y = 0; if (ld.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(ld), Time.deltaTime * 18f); }
        if (stateTimer <= 0) EndAttack();
    }

    private void EndAttack()
    {
        attackCooldownTimer = (CurrentHealthValue < maxHealth * 0.5f) ? 0.3f : 0.7f;
        if (hammerHitbox != null) hammerHitbox.SetActive(false); detectionTimer = 0f;
        if (targetPlayer != null && IsPlayerAliveAndValid(targetPlayer)) ChangeState(EnemyState.Chase); else { targetPlayer = null; ReturnToPatrol(); }
    }

    private bool IsPlayerAliveAndValid(Transform pt)
    {
        if (pt == null || !pt.gameObject.activeInHierarchy) return false;

        // Lọc hoàn toàn các đối tượng UI / Canvas / HealthBar
        if (pt.gameObject.layer == LayerMask.NameToLayer("UI")) return false;
        string n = pt.name.ToLower();
        if (n.Contains("ui") || n.Contains("canvas") || n.Contains("hud") || n.Contains("healthbar") || n.Contains("health_bar")) return false;

        IPlayerHUDTarget ps = pt.GetComponentInParent<IPlayerHUDTarget>();
        if (ps == null) ps = pt.GetComponentInChildren<IPlayerHUDTarget>();
        if (ps != null)
        {
            if (ps.CurrentHealth <= 0 || ps.IsInvisible) return false;
            return true;
        }

        Skeleton sk = pt.GetComponentInParent<Skeleton>();
        if (sk != null)
        {
            if (sk.CurrentHealthValue <= 0) return false;
            return true;
        }

        Transform curr = pt;
        while (curr != null)
        {
            if (curr.CompareTag("Player"))
            {
                var monoComponents = curr.GetComponents<MonoBehaviour>();
                foreach (var mono in monoComponents)
                {
                    if (mono == null) continue;
                    var prop = mono.GetType().GetProperty("CurrentHealth");
                    if (prop != null && prop.PropertyType == typeof(float))
                    {
                        float hp = (float)prop.GetValue(mono);
                        if (hp <= 0) return false;
                    }
                    var deadField = mono.GetType().GetField("isDead", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (deadField != null && deadField.FieldType == typeof(bool))
                    {
                        bool dead = (bool)deadField.GetValue(mono);
                        if (dead) return false;
                    }
                    var isDeadProp = mono.GetType().GetProperty("IsDead", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (isDeadProp != null && isDeadProp.PropertyType == typeof(bool))
                    {
                        bool dead = (bool)isDeadProp.GetValue(mono);
                        if (dead) return false;
                    }
                }
                return true;
            }
            curr = curr.parent;
        }

        return false;
    }

    private System.Collections.Generic.List<Transform> GetAllAlivePlayers()
    {
        System.Collections.Generic.List<Transform> list = new System.Collections.Generic.List<Transform>();

        if (IsNetworkActive && Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
        {
            foreach (var kvp in Unity.Netcode.NetworkManager.Singleton.ConnectedClients)
            {
                var clientObj = kvp.Value.PlayerObject;
                if (clientObj != null && clientObj.gameObject.activeInHierarchy)
                {
                    Transform t = clientObj.transform;
                    if (IsPlayerAliveAndValid(t) && !list.Contains(t))
                    {
                        list.Add(t);
                    }
                }
            }
        }

        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (var p in players)
        {
            if (p != null && p.activeInHierarchy)
            {
                Transform t = p.transform;
                if (IsPlayerAliveAndValid(t) && !list.Contains(t))
                {
                    list.Add(t);
                }
            }
        }

        return list;
    }



    private int FallbackDetect()
    {
        int c = 0;
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (var p in players)
        {
            if (c >= detectionResults.Length) break;
            if (p == null || !p.activeInHierarchy) continue;
            if (Vector3.Distance(transform.position, p.transform.position) <= sightRange)
            {
                Collider col = p.GetComponent<Collider>() ?? p.GetComponentInChildren<Collider>();
                if (col != null) detectionResults[c++] = col;
            }
        }
        if (c == 0 && IsNetworkActive && NetworkManager.Singleton != null)
        {
            foreach (var kvp in NetworkManager.Singleton.ConnectedClients)
            {
                if (c >= detectionResults.Length) break;
                var po = kvp.Value.PlayerObject;
                if (po == null || !po.gameObject.activeInHierarchy || Vector3.Distance(transform.position, po.transform.position) > sightRange) continue;
                Collider col = po.GetComponent<Collider>() ?? po.GetComponentInChildren<Collider>();
                if (col != null) detectionResults[c++] = col;
            }
        }
        return c;
    }

    private void ChangeState(EnemyState newState)
    {
        if (currentFSMState != null)
        {
            currentFSMState.Exit();
        }

        CurrentStateValue = newState;

        switch (newState)
        {
            case EnemyState.Patrol:  currentFSMState = patrolState; break;
            case EnemyState.Chase:   currentFSMState = chaseState;  break;
            case EnemyState.Attack:  currentFSMState = attackState; break;
            case EnemyState.Stagger: currentFSMState = staggerState; break;
            case EnemyState.Dead:    currentFSMState = deadState;   break;
            case EnemyState.Flee:    currentFSMState = fleeState;   break;
        }

        if (currentFSMState != null)
        {
            currentFSMState.Enter();
        }
    }

    private void ApplySpeedAnim(float speed) { if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null) return; anim.SetFloat(speedParam, speed); }
    private void SetSpeedNet(float val) { if (isStandaloneMode) ApplySpeedAnim(val); else if (IsServer && !Mathf.Approximately(netSpeed.Value, val)) netSpeed.Value = val; }
    private void PlayAttackAnimLocal(int type) { if (anim == null) return; anim.ResetTrigger(atk1Trigger); anim.ResetTrigger(atk2Trigger); anim.ResetTrigger(atk3Trigger); if (type == 0) anim.SetTrigger(atk1Trigger); else if (type == 1) anim.SetTrigger(atk2Trigger); else anim.SetTrigger(atk3Trigger); }
    [ClientRpc] private void PlayAttackClientRpc(int type) { if (!IsServer) PlayAttackAnimLocal(type); }

    private void DealConeDamage(float damage, float range, float angle, float knockback)
    {
        int num = Physics.OverlapSphereNonAlloc(transform.position, range, damageResults, playerLayer);
        if (num == 0)
        {
            int c = 0;
            var activePlayers = PlayerHUDManager.ActivePlayers;
            if (activePlayers != null)
            {
                foreach (var p in activePlayers)
                {
                    if (p == null || p.gameObject == null) continue;
                    if (c >= damageResults.Length) break;
                    if (Vector3.Distance(transform.position, p.transform.position) <= range)
                    {
                        var col = p.gameObject.GetComponentInChildren<Collider>() ?? p.gameObject.GetComponentInParent<Collider>();
                        if (col != null) damageResults[c++] = col;
                    }
                }
            }
            num = c;
        }
        Vector3 ep = eyeTransform != null ? eyeTransform.position : transform.position + Vector3.up * 1.5f;
        
        System.Collections.Generic.HashSet<IPlayerHUDTarget> hitTargets = new System.Collections.Generic.HashSet<IPlayerHUDTarget>();
        
        for (int i = 0; i < num; i++)
        {
            if (damageResults[i] == null) continue;
            Transform pl = damageResults[i].transform;
            
            IPlayerHUDTarget target = pl.GetComponentInParent<IPlayerHUDTarget>();
            if (target == null) target = pl.GetComponentInChildren<IPlayerHUDTarget>();
            if (target == null) continue;
            if (hitTargets.Contains(target)) continue;
            hitTargets.Add(target);
            
            Transform playerBody = target.transform;
            float actualDist = Vector3.Distance(transform.position, playerBody.position);
            if (actualDist > range) continue;
            
            // Hướng từ chân quái vật tới chân player (bỏ qua độ cao Y để tính góc nón chính xác trên mặt phẳng ngang)
            Vector3 diff = playerBody.position - transform.position;
            Vector3 horizDiff = new Vector3(diff.x, 0, diff.z);
            Vector3 forward = new Vector3(transform.forward.x, 0, transform.forward.z).normalized;
            
            float targetAngle = Vector3.Angle(forward, horizDiff.normalized);
            
            if (targetAngle <= angle / 2f)
            {
                // Kiểm tra tia raycast từ ngực/mắt quái vật tới ngực/mắt player để kiểm tra vật cản
                Vector3 targetCenter = playerBody.position + Vector3.up * 1.0f;
                Vector3 rayDir = (targetCenter - ep).normalized;
                float rayDist = Vector3.Distance(ep, targetCenter);
                
                if (!Physics.Raycast(ep, rayDir, rayDist, obstacleLayer))
                {
                    Vector3 kb = horizDiff.normalized;
                    EnemyDamageHelper.DealDamage(playerBody, damage, kb * knockback);
                }
            }
        }
    }

    public void TakeDamage(float damage)
    {
        if (CurrentStateValue == EnemyState.Dead) return;

        localHealth = Mathf.Max(0f, localHealth - damage);
        if (!isStandaloneMode && IsSpawned && IsServer)
        {
            currentHealth.Value = Mathf.Max(0f, currentHealth.Value - damage);
            localHealth = currentHealth.Value;
            hitCounter.Value++;
        }
        else if (anim != null && staggerCooldownTimer <= 0f)
        {
            staggerCooldownTimer = 1.2f;
            anim.SetTrigger(hitTrigger);
        }

        EnemyDamageEffectHelper.PlayDamageEffects(gameObject, damage, hitSoundClip);

        bool isAuth = isStandaloneMode || (IsSpawned && IsServer) || !IsSpawned;
        if (!isAuth) return;

        float activeHp = ActualCurrentHealth;
        if (activeHp <= 0f) { ChangeState(EnemyState.Dead); return; }

        if (canTacticalFlee && !hasFledTactically && (activeHp / maxHealth) <= fleeHealthThreshold && CurrentStateValue != EnemyState.Flee)
        {
            hasFledTactically = true;
            AlertNearbyAllies(targetPlayer, 22f);
            ChangeState(EnemyState.Flee);
            return;
        }
        float now = Time.time; if (now - lastDamageTime > 3f) recentHitCount = 0; recentHitCount++; lastDamageTime = now;
        if ((damage >= 30f || recentHitCount >= 3) && CurrentStateValue != EnemyState.Stagger) { recentHitCount = 0; staggerTimer = 0.55f; ChangeState(EnemyState.Stagger); }
    }

    /// <summary>
    /// Áp dụng hiệu ứng choáng từ Skill Q của Arthur. Chỉ chạy trên Server hoặc Standalone.
    /// </summary>
    public void ApplyStun(float duration)
    {
        bool auth = isStandaloneMode || (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsServer);
        if (!auth) return;
        if (CurrentStateValue == EnemyState.Dead) return;

        staggerTimer = duration;
        ChangeState(EnemyState.Stagger);
        EnemyStunVfxBehaviour.ApplyStunVfx(gameObject, duration, stunVfxPrefab, stunVfxHeightOffset, stunVfxScale);
        if (!isStandaloneMode && IsServer)
        {
            ApplyStunVfxClientRpc(duration);
        }
        Debug.Log($"[Enemy3_Buaa] Bị choáng (Skill Q Arthur) trong {duration}s");
    }

    [ClientRpc]
    private void ApplyStunVfxClientRpc(float duration)
    {
        staggerTimer = duration;
        EnemyStunVfxBehaviour.ApplyStunVfx(gameObject, duration, stunVfxPrefab, stunVfxHeightOffset, stunVfxScale);
    }

    private void Die()
    {
        if (AgentReady) agent.isStopped = true; SetSpeedNet(0f);
        ApplyLocalDeathEffects();
        DropExperience(); DropItems(); Invoke(nameof(DespawnEnemy), 5.2f);
    }

    private void DropExperience()
    {
        if (expGemPrefab == null) return;
        string uid = System.Guid.NewGuid().ToString(); Transform sp = expDropPoint != null ? expDropPoint : transform; float d = 0.6f;
        Vector3[] pos = { sp.position + new Vector3(d,0.1f,d), sp.position + new Vector3(-d,0.1f,d), sp.position + new Vector3(d,0.1f,-d), sp.position + new Vector3(-d,0.1f,-d) };
        bool na = IsNetworkActive;
        foreach (var p in pos) 
        { 
            if (isStandaloneMode || !na) 
            { 
                var g = ExperienceGemObjectPool.Instance.GetOrCreate(expGemPrefab, p, Quaternion.identity); 
                var gem = g.GetComponent<ExperienceGem>(); 
                if (gem != null) { gem.expAmount = expDropAmount; gem.DropGroupId = uid; } 
            } 
            else if (IsServer) 
            { 
                var g = ExperienceGemObjectPool.Instance.GetOrCreate(expGemPrefab, p, Quaternion.identity); 
                var gem = g.GetComponent<ExperienceGem>(); 
                if (gem != null) { gem.expAmount = expDropAmount; gem.DropGroupId = uid; } 
                var no = g.GetComponent<NetworkObject>(); 
                if (no != null) no.Spawn(); 
            } 
        }
    }

    private void DropItems()
    {
        if (repairItemPrefab == null || Random.value > repairItemDropChance) return;
        Transform sp = expDropPoint != null ? expDropPoint : transform; Vector3 pv = sp.position + Vector3.up * 0.2f; bool na = IsNetworkActive;
        if (isStandaloneMode || !na) Instantiate(repairItemPrefab, pv, Quaternion.identity);
        else if (IsServer) { var g = Instantiate(repairItemPrefab, pv, Quaternion.identity); var no = g.GetComponent<NetworkObject>(); if (no != null) no.Spawn(); }
    }

    private void DespawnEnemy() { if (isStandaloneMode) { Destroy(gameObject); return; } if (IsServer && IsSpawned) GetComponent<NetworkObject>().Despawn(); }
    private void SnapToNavMesh() { if (agent == null || !agent.isActiveAndEnabled) return; if (!agent.isOnNavMesh) { NavMeshHit h; if (NavMesh.SamplePosition(transform.position, out h, 10f, NavMesh.AllAreas)) agent.Warp(h.position); } }
    public void EnableWeaponHitbox()
    {
        bool auth = isStandaloneMode || (IsNetworkActive && IsServer);
        if (auth)
        {
            attackSwingCount++;
            int aType = isStandaloneMode ? 0 : attackType.Value;
            if (aType == 2)
            {
                if (!hasDealtDamage1)
                {
                    hasDealtDamage1 = true;
                    DealConeDamage(10f, attackRange + 1.5f, 110f, 4.0f);
                }
            }
            else if (aType == 1)
            {
                if (attackSwingCount == 1 && !hasDealtDamage1)
                {
                    hasDealtDamage1 = true;
                    DealConeDamage(6f, attackRange + 1f, 90f, 2.0f);
                }
                else if (attackSwingCount >= 2 && !hasDealtDamage2)
                {
                    hasDealtDamage2 = true;
                    DealConeDamage(8f, attackRange + 1f, 90f, 2.5f);
                }
            }
            else
            {
                if (!hasDealtDamage1)
                {
                    hasDealtDamage1 = true;
                    DealConeDamage(7f, attackRange + 0.5f, 80f, 2.0f);
                }
            }
        }
    }
    public void DisableWeaponHitbox() { hasDealtDamage1 = false; hasDealtDamage2 = false; }

    // ─── Nested FSM States ───
    private class PatrolState : IEnemyState
    {
        private Enemy3_Buaa enemy;
        public PatrolState(Enemy3_Buaa enemy) { this.enemy = enemy; }
        public void Enter() {}
        public void Update() { enemy.HandlePatrol(); }
        public void Exit() {}
    }

    private class ChaseState : IEnemyState
    {
        private Enemy3_Buaa enemy;
        public ChaseState(Enemy3_Buaa enemy) { this.enemy = enemy; }
        public void Enter()
        {
            enemy.waitingAtWaypoint = false;
            if (enemy.AgentReady)
            {
                enemy.agent.isStopped = false;
                enemy.agent.speed = enemy.chaseRunSpeed;
            }
        }
        public void Update() { enemy.HandleChase(); }
        public void Exit() {}
    }

    private class AttackState : IEnemyState
    {
        private Enemy3_Buaa enemy;
        public AttackState(Enemy3_Buaa enemy) { this.enemy = enemy; }
        public void Enter()
        {
            if (enemy.AgentReady) enemy.agent.isStopped = true;
            enemy.SetSpeedNet(0f);
            enemy.hasDealtDamage1 = false;
            enemy.hasDealtDamage2 = false;
            enemy.attackSwingCount = 0;
            float hp = enemy.CurrentHealthValue / enemy.maxHealth;
            int chosen;
            float dur;
            if (hp <= 0.4f) { chosen = 2; dur = 1.9f; }
            else if (hp <= 0.7f) { chosen = 1; dur = 1.6f; }
            else { chosen = 0; dur = 1.0f; }
            if (!enemy.isStandaloneMode) enemy.attackType.Value = chosen;
            enemy.attackDuration = dur;
            enemy.stateTimer = dur;
            enemy.PlayAttackAnimLocal(chosen);
            if (!enemy.isStandaloneMode) enemy.PlayAttackClientRpc(chosen);
        }
        public void Update() { enemy.HandleAttack(); }
        public void Exit()
        {
        }
    }

    private class StaggerState : IEnemyState
    {
        private Enemy3_Buaa enemy;
        public StaggerState(Enemy3_Buaa enemy) { this.enemy = enemy; }
        public void Enter()
        {
            if (enemy.AgentReady) enemy.agent.isStopped = true;
            enemy.SetSpeedNet(0f);
            if (enemy.staggerTimer <= 0) enemy.staggerTimer = 0.55f;
        }
        public void Update() { enemy.HandleStagger(); }
        public void Exit() {}
    }

    private class DeadState : IEnemyState
    {
        private Enemy3_Buaa enemy;
        public DeadState(Enemy3_Buaa enemy) { this.enemy = enemy; }
        public void Enter()
        {
            enemy.Die();
        }
        public void Update() {}
        public void Exit() {}
    }

    private void HandleFlee()
    {
        fleeTimer -= Time.deltaTime;

        if (targetPlayer != null && IsPlayerAliveAndValid(targetPlayer))
        {
            Vector3 fleeDir = (transform.position - targetPlayer.position).normalized;
            Vector3 desiredFleePos = transform.position + fleeDir * 12.0f;

            if (NavMesh.SamplePosition(desiredFleePos, out NavMeshHit hit, 6.0f, NavMesh.AllAreas))
            {
                if (AgentReady)
                {
                    agent.isStopped = false;
                    agent.speed = chaseRunSpeed * 1.35f;
                    agent.SetDestination(hit.position);
                }
            }
            SetSpeedNet(1.0f);

            float distToPlayer = Vector3.Distance(transform.position, targetPlayer.position);
            if (distToPlayer > 16.0f || fleeTimer <= 0f)
            {
                targetPlayer = null;
                ReturnToPatrol();
                return;
            }
        }
        else
        {
            ReturnToPatrol();
        }
    }

    private class FleeState : IEnemyState
    {
        private Enemy3_Buaa enemy;
        public FleeState(Enemy3_Buaa enemy) { this.enemy = enemy; }
        public void Enter()
        {
            enemy.fleeTimer = 4.0f;
            if (enemy.AgentReady)
            {
                enemy.agent.isStopped = false;
                enemy.agent.speed = enemy.chaseRunSpeed * 1.35f;
            }
        }
        public void Update() { enemy.HandleFlee(); }
        public void Exit() {}
    }


}