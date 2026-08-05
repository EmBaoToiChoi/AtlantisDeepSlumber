using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Enemy 5 - Phù Thủy (Ranged/Kiting/Predictive Fireball AI)
/// Animator Float "Speed": 0=Idle, 0.5=Walk (patrol/retreat), 1=Run (kiting/chase/flee)
/// Attack trigger: quai5Attack
/// </summary>
public class Enemy5_PhuThuy : NetworkBehaviour
{
    public enum EnemyState { Patrol, Chase, Stagger, Attack, Dead, Flee }

    [Header("Health")]
    public float maxHealth = 90f;
    public NetworkVariable<float> currentHealth = new NetworkVariable<float>(90f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<EnemyState> currentState = new NetworkVariable<EnemyState>(EnemyState.Patrol, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Network Anim Sync")]
    public NetworkVariable<float> netSpeed = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> hitCounter = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

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
    public Transform staffTipTransform;

    [Header("Patrol Waypoints (3 điểm hình tam giác)")]
    public Transform[] waypoints = new Transform[3];

    [Header("Spell Settings")]
    public GameObject spellProjectilePrefab;
    public float spellSpeed  = 14f;
    public float spellDamage = 10f;

    [Header("Drops")]
    public GameObject expGemPrefab;
    public float expDropAmount = 25f;
    public Transform expDropPoint;
    public GameObject repairItemPrefab;
    [Range(0f, 1f)] public float repairItemDropChance = 0.3f;

    [Header("AI Settings")]
    public float sightRange     = 24f;     // Tầm phát hiện xa
    public float fieldOfView    = 120f;
    public float maxAttackRange = 16f;     // Tầm bắn xa
    public float minAttackRange = 7.5f;    // Kiting: bắt đầu thoái lui khi Player gần hơn 7.5m
    public float maxChaseDistance = 22f;   // Tầm đuổi tối đa
    public float patrolWalkSpeed = 2.5f;
    public float chaseRunSpeed   = 4.5f;    // Tốc độ chạy kiting
    public float patrolWaitMin = 1.5f;
    public float patrolWaitMax = 5f;
    [Range(0f, 1f)] public float patrolMoveChance = 0.65f;

    [Header("Tactical Flee AI")]
    public bool canTacticalFlee = true;
    public float fleeHealthThreshold = 0.30f; // Bắt đầu bỏ chạy và kiting xa khi HP < 30%
    private bool hasFledTactically = false;
    private float fleeTimer;

    [Header("Layers")]
    public LayerMask playerLayer;
    public LayerMask obstacleLayer;

    [Header("Animator Parameter Names")]
    [Tooltip("Float: 0=Idle, 0.5=Walk, 1=Run — Base Layer")]
    public string speedParam    = "Speed";        // Float  — Base Layer
    public string hitTrigger    = "Quai5Anhit";   // Trigger — Anhit Layer
    public string dieTrigger    = "Quai5Die";     // Trigger — Base Layer
    public string attackTrigger = "quai5Attack";  // Trigger — Attack Layer

    public Renderer[] modelRenderers;
    private MaterialPropertyBlock propBlock;

    private Transform targetPlayer;
    private int currentWaypointIndex = -1;
    private bool waitingAtWaypoint;
    private float waypointWaitTimer;
    private float detectionTimer;
    private const float DETECTION_INTERVAL = 0.15f;
    private float staggerTimer;
    private float staggerCooldownTimer;
    private float attackCooldownTimer;
    private float attackDuration = 1.2f;
    private float stateTimer;
    private bool hasCastSpell;
    private float lastDamageTime;
    private int recentHitCount;
    private bool isBlinking;
    private float blinkTimer;
    private float loseSightTimer;

    // ─── FSM States ───
    private IEnemyState currentFSMState;
    private PatrolState patrolState;
    private ChaseState chaseState;
    private AttackState attackState;
    private StaggerState staggerState;
    private DeadState deadState;
    private FleeState fleeState;

    private readonly Collider[] detectionResults = new Collider[8];
    private bool AgentReady => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;

    private void Awake()
    {
        gameObject.tag = "Enemy";
        if (agent == null) agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null && !agent.enabled) agent.enabled = true;
        propBlock = new MaterialPropertyBlock();
        if (anim == null) anim = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);

        if (obstacleLayer.value == 0)
        {
            obstacleLayer = LayerMask.GetMask("Default", "Obstacle", "Environment", "Ground", "Wall", "Map", "Structure", "Terrain");
            if (obstacleLayer.value == 0)
            {
                obstacleLayer = ~(LayerMask.GetMask("Player", "Enemy", "Ignore Raycast", "UI", "Water"));
            }
        }
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
        SnapToNavMesh(); ApplySpeedAnim(0f); 
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
        if (IsServer) { currentHealth.Value = maxHealth; SnapToNavMesh(); ChangeState(EnemyState.Patrol); GoToNextWaypoint(); }
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
        if (newState == EnemyState.Dead)
        {
            if (!IsServer)
            {
                ApplyLocalDeathEffects();
            }
        }
    }

    private void ApplyLocalDeathEffects()
    {
        hasCastSpell = true;
        CancelInvoke(nameof(DespawnEnemy));
        StopAllCoroutines();
        if (anim != null)
        {
            anim.ResetTrigger(attackTrigger);
            anim.ResetTrigger(hitTrigger);
            anim.SetTrigger(dieTrigger);
        }
        Collider col = GetComponent<Collider>(); if (col != null) col.enabled = false;
        Collider[] cols = GetComponentsInChildren<Collider>(); foreach (var c in cols) c.enabled = false;
    }

    private void OnHealthNetChanged(float oldHealth, float newHealth)
    {
        localHealth = newHealth;
        if (newHealth <= 0f && CurrentStateValue != EnemyState.Dead)
        {
            ApplyLocalDeathEffects();
        }
    }

    private void Update()
    {
        bool aiAuth = isStandaloneMode || (IsNetworkActive && IsServer);
        if (!aiAuth) return;
        if (agent != null && agent.isActiveAndEnabled && !agent.isOnNavMesh) SnapToNavMesh();

        if (staggerCooldownTimer > 0f) staggerCooldownTimer -= Time.deltaTime;
        if (attackCooldownTimer > 0) attackCooldownTimer -= Time.deltaTime;

        if (isBlinking)
        {
            blinkTimer -= Time.deltaTime;
            if (blinkTimer <= 0f)
            {
                isBlinking = false;
                if (AgentReady) agent.speed = chaseRunSpeed;
            }
        }

        detectionTimer -= Time.deltaTime;
        if (detectionTimer <= 0) { detectionTimer = DETECTION_INTERVAL; DetectPlayer(); }
        if (currentFSMState != null)
        {
            currentFSMState.Update();
        }
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
                shift.y = 0;
                targetPos += shift;
            }
        }

        NavMeshHit hit;
        if (NavMesh.SamplePosition(targetPos, out hit, 3.0f, NavMesh.AllAreas))
        {
            return hit.position;
        }
        return basePos;
    }

    private void GoToNextWaypoint()
    {
        if (!AgentReady) return;
        waitingAtWaypoint = false;
        agent.isStopped = false;
        agent.speed = patrolWalkSpeed;

        if (waypoints != null && waypoints.Length > 0)
        {
            System.Collections.Generic.List<Transform> validWaypoints = new System.Collections.Generic.List<Transform>();
            foreach (var wp in waypoints)
            {
                if (wp != null) validWaypoints.Add(wp);
            }

            if (validWaypoints.Count > 0)
            {
                currentWaypointIndex = (currentWaypointIndex + 1) % validWaypoints.Count;
                Transform targetWp = validWaypoints[currentWaypointIndex];
                Vector3 targetPos = GetUniquePatrolPosition(targetWp.position);
                agent.SetDestination(targetPos);
                SetSpeedNet(0.5f); // Walk animation
                return;
            }
        }

        Vector3 randomPos = GetRandomNavMeshPosition(8f);
        agent.SetDestination(randomPos);
        SetSpeedNet(0.5f);
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

        CheckForwardMapBoundaryAndTurn();

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
            SetSpeedNet(0f);
            waypointWaitTimer -= Time.deltaTime;
            if (waypointWaitTimer <= 0f)
            {
                waitingAtWaypoint = false;
                GoToNextWaypoint();
            }
            return;
        }

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.6f)
        {
            agent.isStopped = true;
            SetSpeedNet(0f);
            waitingAtWaypoint = true;
            waypointWaitTimer = Random.Range(patrolWaitMin, patrolWaitMax);
        }
        else if (!agent.pathPending && (!agent.hasPath || agent.pathStatus == NavMeshPathStatus.PathInvalid))
        {
            GoToNextWaypoint();
        }
    }

    // ── Chase & Kiting AI ──
    private void HandleChase()
    {
        if (targetPlayer == null || !IsPlayerAliveAndValid(targetPlayer)) { targetPlayer = null; ReturnToPatrol(); return; }

        // 1. Kiểm tra NavMesh reachability
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
                        float dCheck = Vector3.Distance(transform.position, pt.position);
                        if (dCheck <= sightRange && IsTargetReachableOnNavMesh(pt))
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

        // 2. Kiểm tra maxChaseDistance
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
        float d = Vector3.Distance(ep1, targetCenter);
        Vector3 dir = (targetCenter - ep1).normalized;

        bool wallBlocked = false;
        if (d > 3.5f)
        {
            if (Physics.Raycast(ep1, dir, out RaycastHit h1, d - 0.3f, obstacleLayer, QueryTriggerInteraction.Ignore))
            {
                if (h1.collider != null && h1.transform != targetPlayer && !h1.transform.IsChildOf(targetPlayer) && !h1.collider.CompareTag("Player"))
                {
                    wallBlocked = true;
                }
            }
        }

        if (wallBlocked && d > sightRange + 3.0f)
        {
            loseSightTimer += Time.deltaTime;
            if (loseSightTimer > 4.5f)
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

        // Quay mặt xoay mượt về phía Player
        Vector3 ld = (targetPlayer.position - transform.position); ld.y = 0;
        if (ld.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(ld), Time.deltaTime * 18f);
        Vector3 flatEnemy = transform.position; flatEnemy.y = 0;
        Vector3 flatPlayer = targetPlayer.position; flatPlayer.y = 0;
        float dist = Vector3.Distance(flatEnemy, flatPlayer);
        float spd = CurrentHealthValue < maxHealth * 0.5f ? chaseRunSpeed * 1.35f : chaseRunSpeed;

        // A. QUÁ GẦN (< minAttackRange): Đi lùi thả diều lượn vòng ra xa Player
        if (dist < minAttackRange)
        {
            Vector3 away = (transform.position - targetPlayer.position);
            away.y = 0;
            away = away.normalized;

            // Thêm độ lệch góc strafe 35 độ để di chuyển lượn hình vòng cung quanh Player
            float strafeAngle = (System.Math.Abs(GetHashCode()) % 2 == 0 ? 35f : -35f);
            Vector3 strafeDir = Quaternion.Euler(0, strafeAngle, 0) * away;

            float retreatDist = Mathf.Clamp(minAttackRange - dist + 2.8f, 2.8f, 7.5f);
            Vector3 retreatPos = transform.position + strafeDir * retreatDist;

            if (Physics.Raycast(transform.position + Vector3.up * 0.8f, strafeDir, out RaycastHit wallCheck, retreatDist, obstacleLayer, QueryTriggerInteraction.Ignore))
            {
                Vector3 perp = new Vector3(-away.z, 0, away.x);
                Vector3 sideDir = (away + perp * (System.Math.Abs(GetHashCode()) % 2 == 0 ? 1.4f : -1.4f)).normalized;
                retreatPos = transform.position + sideDir * retreatDist;
            }

            if (NavMesh.SamplePosition(retreatPos, out NavMeshHit navHit, 4.0f, NavMesh.AllAreas))
            {
                if (AgentReady)
                {
                    agent.isStopped = false;
                    agent.speed = spd * 0.95f;
                    agent.SetDestination(navHit.position);
                }
            }

            if (attackCooldownTimer <= 0 && dist >= 3.0f)
            {
                ChangeState(EnemyState.Attack);
            }

            SetSpeedNet(AgentReady && !agent.isStopped ? 0.5f : 0f);
            return;
        }

        // B. TẦM LÝ TƯỞNG (minAttackRange <= dist <= effectiveMaxAttackRange): Dừng lại bắn phép
        float effectiveMaxAttackRange = Mathf.Min(maxAttackRange, 14.0f);
        if (dist >= minAttackRange && dist <= effectiveMaxAttackRange)
        {
            if (AgentReady) agent.isStopped = true;
            SetSpeedNet(0f);
            if (attackCooldownTimer <= 0) ChangeState(EnemyState.Attack);
            return;
        }

        // C. QUÁ XA (> effectiveMaxAttackRange): Tiến lại gần + Đoán hướng di chuyển
        if (AgentReady)
        {
            agent.isStopped = false;
            agent.speed = spd;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            if (agent.radius < 0.45f) agent.radius = 0.45f;

            float distToPlayer = Vector3.Distance(transform.position, targetPlayer.position);
            agent.avoidancePriority = Mathf.Clamp(10 + Mathf.RoundToInt(distToPlayer * 4f), 5, 95);

            Vector3 targetPos = GetPredictedTargetPosition(targetPlayer);
            agent.SetDestination(targetPos);

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
                            alt = pt; break;
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

    // ── Dự đoán vị trí của Target Player (Mượt mà không bị giật rung) ──
    private Vector3 GetPredictedTargetPosition(Transform player)
    {
        if (player == null) return transform.position;
        Vector3 pPos = player.position;
        float dist = Vector3.Distance(transform.position, pPos);

        // Nếu khoảng cách xa (> 10m), nhắm thẳng vị trí hiện tại để xoay mượt tuyệt đối
        if (dist > 10.0f) return pPos;

        Vector3 velocity = Vector3.zero;
        var rb = player.GetComponent<Rigidbody>() ?? player.GetComponentInChildren<Rigidbody>();
        if (rb != null) velocity = rb.linearVelocity;
        else
        {
            var cc = player.GetComponent<CharacterController>() ?? player.GetComponentInChildren<CharacterController>();
            if (cc != null) velocity = cc.velocity;
        }

        velocity.y = 0;
        if (velocity.sqrMagnitude > 1.0f)
        {
            Vector3 predicted = pPos + velocity.normalized * Mathf.Clamp(velocity.magnitude * 0.35f, 0.4f, 1.2f);
            return predicted;
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

    private void HandleStagger()
    {
        if (AgentReady) agent.isStopped = true;
        SetSpeedNet(0f);
        staggerTimer -= Time.deltaTime;
        if (staggerTimer <= 0)
        {
            if (targetPlayer != null && IsPlayerAliveAndValid(targetPlayer)) ChangeState(EnemyState.Chase);
            else ReturnToPatrol();
        }
    }

    private void HandleAttack()
    {
        if (IsDead || CurrentStateValue == EnemyState.Dead)
        {
            EndAttack();
            return;
        }

        if (AgentReady) agent.isStopped = true;
        SetSpeedNet(0f);

        stateTimer -= Time.deltaTime;

        Transform aimTarget = (targetPlayer != null && IsPlayerAliveAndValid(targetPlayer)) ? targetPlayer : null;
        if (aimTarget != null)
        {
            Vector3 targetPos = GetPredictedTargetPosition(aimTarget);
            Vector3 ld = (targetPos - transform.position); ld.y = 0;
            if (ld.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(ld);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, 540f * Time.deltaTime);
            }
        }

        // BẢO ĐẢM 100%: Dự phòng nếu Animation Event từ Keyframe bị lỡ/bỏ qua, đạn vẫn sẽ tự động phóng ở 55% thời lượng chiêu!
        bool auth = isStandaloneMode || (IsNetworkActive && IsServer);
        if (auth && !hasCastSpell && stateTimer <= attackDuration * 0.55f)
        {
            hasCastSpell = true;
            LaunchSpellBall();
        }

        if (stateTimer <= 0)
        {
            EndAttack();
        }
    }

    private void EndAttack()
    {
        attackCooldownTimer = 1.6f;
        detectionTimer = 0f;
        
        // Nếu Player chết hoặc rời khỏi tầm nhìn -> Lập tức quay về điểm tuần tra, không đứng giật giật!
        if (targetPlayer == null || !IsPlayerAliveAndValid(targetPlayer))
        {
            targetPlayer = FindNearestAlivePlayer();
        }

        if (targetPlayer != null && IsPlayerAliveAndValid(targetPlayer))
        {
            float d = Vector3.Distance(transform.position, targetPlayer.position);
            if (d <= sightRange)
            {
                ChangeState(EnemyState.Chase);
                return;
            }
        }

        targetPlayer = null;
        ReturnToPatrol();
    }

    private bool IsPlayerAliveAndValid(Transform pt)
    {
        if (pt == null || !pt.gameObject.activeInHierarchy) return false;

        if (pt.gameObject.layer == LayerMask.NameToLayer("UI")) return false;

        // Bỏ qua nếu người chơi đang tàng hình (Invisible)
        var monoComponents = pt.GetComponentsInParent<MonoBehaviour>();
        foreach (var mono in monoComponents)
        {
            if (mono == null) continue;
            var invisProp = mono.GetType().GetProperty("IsInvisible", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (invisProp != null && invisProp.PropertyType == typeof(bool))
            {
                bool isInvis = (bool)invisProp.GetValue(mono);
                if (isInvis) return false;
            }
        }

        Transform curr = pt;
        while (curr != null)
        {
            if (curr.CompareTag("Player"))
            {
                var monoComps = curr.GetComponents<MonoBehaviour>();
                foreach (var mono in monoComps)
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

    private Transform FindNearestAlivePlayer()
    {
        var list = GetAllAlivePlayers();
        Transform nearest = null;
        float minDist = sightRange; // CHỈ TÌM PLAYER TRONG TẦM NHÌN (sightRange), KHÔNG TÌM XA 100M!
        foreach (var p in list)
        {
            if (p != null)
            {
                float d = Vector3.Distance(transform.position, p.position);
                if (d <= minDist)
                {
                    minDist = d;
                    nearest = p;
                }
            }
        }
        return nearest;
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

            int chasers = GetChaserCountForPlayer(pt);
            if (chasers >= 2 && pt != targetPlayer && alivePlayers.Count > 1)
            {
                continue;
            }

            Vector3 dir = (center - ep).normalized;

            bool isMovingFast = false;
            var rb = pt.GetComponent<Rigidbody>() ?? pt.GetComponentInChildren<Rigidbody>();
            if (rb != null && rb.linearVelocity.sqrMagnitude > 2.5f) isMovingFast = true;
            else {
                var cc = pt.GetComponent<CharacterController>() ?? pt.GetComponentInChildren<CharacterController>();
                if (cc != null && cc.velocity.sqrMagnitude > 2.5f) isMovingFast = true;
            }

            bool hearingSound = d <= 10.0f && isMovingFast;
            bool inFOV = Vector3.Angle(transform.forward, dir) < fieldOfView / 2f;
            bool isProximity = d <= 6.5f;

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
    private void PlayAttackAnimLocal() { if (anim == null) return; anim.ResetTrigger(attackTrigger); anim.SetTrigger(attackTrigger); }
    [ClientRpc] private void PlayAttackClientRpc() { if (!IsServer) PlayAttackAnimLocal(); }

    // Animation Event (từ keyframe chưởng)
    public void TriggerSpellLaunch()
    {
        bool auth = isStandaloneMode || (IsNetworkActive && IsServer);
        if (!auth || IsDead || CurrentStateValue == EnemyState.Dead || !gameObject.activeInHierarchy || hasCastSpell) return;
        hasCastSpell = true;
        LaunchSpellBall();
    }

    private void LaunchSpellBall()
    {
        if (IsDead || CurrentStateValue == EnemyState.Dead || !gameObject.activeInHierarchy) return;

        // Tự động tìm lại Prefab quả cầu lửa từ Resources nếu bị null
        if (spellProjectilePrefab == null)
        {
            spellProjectilePrefab = Resources.Load<GameObject>("Fireball 1") ?? 
                                   Resources.Load<GameObject>("Fireball") ?? 
                                   Resources.Load<GameObject>("SpellBall") ?? 
                                   Resources.Load<GameObject>("Prefab/SpellBall") ?? 
                                   Resources.Load<GameObject>("Hbao/Prefab/SpellBall");
        }

        // Spawn point vừa tầm ngực/bụng Người chơi (cao 0.75m), dùng staffTipTransform nếu có
        Vector3 spawnPt = staffTipTransform != null ? staffTipTransform.position : (transform.position + transform.forward * 0.9f + Vector3.up * 0.75f);

        // Nếu targetPlayer bị null đúng lúc bắn, bắn thẳng về phía trước theo transform.forward
        Vector3 targetPos = (targetPlayer != null && IsPlayerAliveAndValid(targetPlayer))
            ? GetPredictedTargetPosition(targetPlayer)
            : transform.position + transform.forward * 10f;
        
        // Ép hướng bay 100% NGANG SONG SONG MẶT ĐẤT (bỏ qua độ dốc Y để đạn không bị cắm xuống đất)
        Vector3 dirToTarget = targetPos - spawnPt;
        dirToTarget.y = 0f;
        Vector3 mainDir = dirToTarget.sqrMagnitude > 0.01f ? dirToTarget.normalized : new Vector3(transform.forward.x, 0, transform.forward.z).normalized;

        float activeHpRatio = ActualCurrentHealth / maxHealth;
        bool multiShot = activeHpRatio <= 0.65f || (targetPlayer != null && Vector3.Distance(transform.position, targetPlayer.position) > 10.0f);

        float[] angles = multiShot ? new float[] { 0f, -14f, 14f } : new float[] { 0f };

        foreach (float angle in angles)
        {
            Vector3 dir = Quaternion.Euler(0, angle, 0) * mainDir;
            dir.y = 0f; // Bảo đảm 100% đạn bay ngang thẳng ra

            if (spellProjectilePrefab != null)
            {
                var proj = Instantiate(spellProjectilePrefab, spawnPt, Quaternion.LookRotation(dir));
                var spellBall = proj.GetComponent<SpellBall>();
                if (spellBall == null)
                {
                    spellBall = proj.AddComponent<SpellBall>();
                }
                spellBall.caster = gameObject;
                spellBall.speed = spellSpeed;
                spellBall.damage = spellDamage;
                spellBall.knockback = 1.5f;

                var rb = proj.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.useGravity = false;
                    rb.isKinematic = true;
                }
                
                var no = proj.GetComponent<NetworkObject>();
                if (no != null && !isStandaloneMode && IsServer) no.Spawn(true);
            }
            else
            {
                if (Physics.Raycast(spawnPt, dir, out RaycastHit hit, maxAttackRange + 2f))
                {
                    EnemyDamageHelper.DealDamage(hit.collider.transform, spellDamage, dir * 1.5f);
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

        EnemyDamageEffectHelper.PlayDamageEffects(gameObject, damage);

        bool isAuth = isStandaloneMode || (IsSpawned && IsServer) || !IsSpawned;
        if (!isAuth) return;

        float activeHp = ActualCurrentHealth;
        if (activeHp <= 0f) { ChangeState(EnemyState.Dead); return; }

        // CHIẾN THUẬT RÚT LUI BỎ CHẠY KHI THẤP MÁU (< 30% HP)
        if (canTacticalFlee && !hasFledTactically && (activeHp / maxHealth) <= fleeHealthThreshold && CurrentStateValue != EnemyState.Flee)
        {
            hasFledTactically = true;
            AlertNearbyAllies(targetPlayer, 22f); // Gọi tất cả đồng đội xung quanh đến cứu
            ChangeState(EnemyState.Flee);
            return;
        }

        float now = Time.time; if (now - lastDamageTime > 3f) recentHitCount = 0; recentHitCount++; lastDamageTime = now;
        if ((damage >= 22f || recentHitCount >= 3) && CurrentStateValue != EnemyState.Stagger)
        {
            recentHitCount = 0;
            staggerTimer = 0.5f;
            ChangeState(EnemyState.Stagger);
        }
        else if (!isBlinking && Random.value < 0.4f && CurrentStateValue == EnemyState.Chase)
        {
            ExecuteBlinkDodge();
        }
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
        Debug.Log($"[Enemy5_PhuThuy] Bị choáng (Skill Q Arthur) trong {duration}s");
    }

    private void ExecuteBlinkDodge()
    {
        if (targetPlayer == null || !AgentReady) return;
        Vector3 tp = (targetPlayer.position - transform.position).normalized;
        float sideAngle = (Random.value < 0.5f ? 1f : -1f) * Random.Range(65f, 115f);
        Vector3 blinkDir = Quaternion.Euler(0, sideAngle, 0) * (-tp);
        
        NavMeshHit h;
        if (NavMesh.SamplePosition(transform.position + blinkDir * 4.5f, out h, 4.5f, NavMesh.AllAreas))
        {
            isBlinking = true;
            blinkTimer = 0.35f;
            agent.isStopped = false;
            agent.Warp(h.position); // Tốc biến bí thuật tức thì sang vị trí an toàn
            agent.speed = chaseRunSpeed;
        }
    }

    private void Die()
    {
        hasCastSpell = true;
        CancelInvoke();
        StopAllCoroutines();
        if (AgentReady) agent.isStopped = true; SetSpeedNet(0f);
        ApplyLocalDeathEffects();
        DropExperience(); DropItems(); Invoke(nameof(DespawnEnemy), 2.5f);
    }

    private void OnDestroy()
    {
        CancelInvoke();
        StopAllCoroutines();
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

    private void HandleFlee()
    {
        fleeTimer -= Time.deltaTime;

        if (targetPlayer != null && IsPlayerAliveAndValid(targetPlayer))
        {
            Vector3 fleeDir = (transform.position - targetPlayer.position).normalized;
            Vector3 desiredFleePos = transform.position + fleeDir * 14.0f;

            if (NavMesh.SamplePosition(desiredFleePos, out NavMeshHit hit, 6.0f, NavMesh.AllAreas))
            {
                if (AgentReady)
                {
                    agent.isStopped = false;
                    agent.speed = chaseRunSpeed * 1.35f;
                    agent.SetDestination(hit.position);
                }
            }
            SetSpeedNet(1.0f); // Run animation

            float distToPlayer = Vector3.Distance(transform.position, targetPlayer.position);
            if (distToPlayer > minAttackRange + 4f || fleeTimer <= 0f)
            {
                ChangeState(EnemyState.Chase);
                return;
            }
        }
        else
        {
            ReturnToPatrol();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 eye = eyeTransform != null ? eyeTransform.position : transform.position + Vector3.up * 1.5f;
        Gizmos.color = new Color(0.85f, 0.1f, 0.95f, 0.4f); Gizmos.DrawWireSphere(eye, sightRange);
        Vector3 l = Quaternion.AngleAxis(-fieldOfView / 2f, Vector3.up) * transform.forward;
        Vector3 r = Quaternion.AngleAxis( fieldOfView / 2f, Vector3.up) * transform.forward;
        Gizmos.DrawRay(eye, l * sightRange); Gizmos.DrawRay(eye, r * sightRange);
        Gizmos.color = new Color(1f, 0.64f, 0f, 0.8f); Gizmos.DrawWireSphere(transform.position, minAttackRange);
        Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(transform.position, maxAttackRange);
        Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(GetPatrolCenterPosition(), maxChaseDistance);
    }

    // ─── Nested FSM States ───
    private class PatrolState : IEnemyState
    {
        private Enemy5_PhuThuy enemy;
        public PatrolState(Enemy5_PhuThuy enemy) { this.enemy = enemy; }
        public void Enter() {}
        public void Update() { enemy.HandlePatrol(); }
        public void Exit() {}
    }

    private class ChaseState : IEnemyState
    {
        private Enemy5_PhuThuy enemy;
        public ChaseState(Enemy5_PhuThuy enemy) { this.enemy = enemy; }
        public void Enter()
        {
            enemy.waitingAtWaypoint = false;
            enemy.isBlinking = false;
            if (enemy.AgentReady) enemy.agent.isStopped = false;
        }
        public void Update() { enemy.HandleChase(); }
        public void Exit() {}
    }

    private class AttackState : IEnemyState
    {
        private Enemy5_PhuThuy enemy;
        public AttackState(Enemy5_PhuThuy enemy) { this.enemy = enemy; }
        public void Enter()
        {
            if (enemy.AgentReady) enemy.agent.isStopped = true;
            enemy.SetSpeedNet(0f);
            enemy.hasCastSpell = false;
            enemy.stateTimer = enemy.attackDuration;
            enemy.PlayAttackAnimLocal();
            if (!enemy.isStandaloneMode) enemy.PlayAttackClientRpc();
        }
        public void Update() { enemy.HandleAttack(); }
        public void Exit() {}
    }

    private class StaggerState : IEnemyState
    {
        private Enemy5_PhuThuy enemy;
        public StaggerState(Enemy5_PhuThuy enemy) { this.enemy = enemy; }
        public void Enter()
        {
            if (enemy.AgentReady) enemy.agent.isStopped = true;
            enemy.SetSpeedNet(0f);
            if (enemy.staggerTimer <= 0) enemy.staggerTimer = 0.5f;
        }
        public void Update() { enemy.HandleStagger(); }
        public void Exit() {}
    }

    private class DeadState : IEnemyState
    {
        private Enemy5_PhuThuy enemy;
        public DeadState(Enemy5_PhuThuy enemy) { this.enemy = enemy; }
        public void Enter()
        {
            enemy.Die();
        }
        public void Update() {}
        public void Exit() {}
    }

    private class FleeState : IEnemyState
    {
        private Enemy5_PhuThuy enemy;
        public FleeState(Enemy5_PhuThuy enemy) { this.enemy = enemy; }
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