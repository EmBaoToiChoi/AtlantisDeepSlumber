using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Enemy 2 - Zombie
/// Animator Float "Speed": 0=Idle, 0.5=Walk (patrol), 1=Run (chase)
/// </summary>
public class Enemy2_Zombie : NetworkBehaviour
{
    public enum EnemyState { Patrol, Chase, Stagger, Attack, Dead }

    [Header("Health")]
    public float maxHealth = 80f;
    public NetworkVariable<float> currentHealth = new NetworkVariable<float>(80f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
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
    /// <summary>HP hiện tại đúng trong cả Standalone lẫn Network mode — dùng cho HP bar polling.</summary>
    public float ActualCurrentHealth => (isStandaloneMode || !IsSpawned) ? localHealth : currentHealth.Value;

    [Header("Components")]
    public NavMeshAgent agent;
    public Animator anim;
    public Transform eyeTransform;

    [Header("Melee Hitbox (Bypassed - now using raycast/cone sweeps)")]
    public GameObject clawHitbox;

    [Header("Patrol Waypoints (3 điểm hình tam giác)")]
    public Transform[] waypoints = new Transform[3];

    [Header("Drops")]
    public GameObject expGemPrefab;
    public float expDropAmount = 20f;
    public Transform expDropPoint;
    public GameObject repairItemPrefab;
    [Range(0f, 1f)] public float repairItemDropChance = 0.3f;

    [Header("AI Settings")]
    public float sightRange = 12f;
    public float fieldOfView = 110f;
    public float attackRange = 1.8f;
    public float patrolWalkSpeed = 2f;
    public float chaseRunSpeed   = 4.5f;
    public float patrolWaitMin = 1f;
    public float patrolWaitMax = 5f;
    [Range(0f, 1f)] public float patrolMoveChance = 0.65f;

    [Header("Layers")]
    public LayerMask playerLayer;
    public LayerMask obstacleLayer;

    [Header("Animator Parameter Names")]
    [Tooltip("Float: 0=Idle, 0.5=Walk, 1=Run — Base Layer")]
    public string speedParam  = "Speed";    // Float  — Base Layer
    public string hitTrigger  = "Anhit";    // Trigger — Anhit Layer
    public string dieTrigger  = "Die";      // Trigger — Base Layer
    public string atkTrigger  = "Attack";   // Trigger — Attack Layer

    private Transform targetPlayer;
    private int currentWaypointIndex = -1;
    private bool waitingAtWaypoint;
    private float waypointWaitTimer;
    private float detectionTimer;
    private const float DETECTION_INTERVAL = 0.15f;
    private float staggerTimer;
    private float attackCooldownTimer;
    private float attackDuration;
    private float stateTimer;
    private bool hasDealtDamage;
    private float lastDamageTime;
    private int recentHitCount;
    private float loseSightTimer;

    // ─── FSM States ───
    private IEnemyState currentFSMState;
    private PatrolState patrolState;
    private ChaseState chaseState;
    private AttackState attackState;
    private StaggerState staggerState;
    private DeadState deadState;

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

        // Initialize state instances for FSM
        localHealth = maxHealth;
        patrolState = new PatrolState(this);
        chaseState = new ChaseState(this);
        attackState = new AttackState(this);
        staggerState = new StaggerState(this);
        deadState = new DeadState(this);
    }

    private void Start() { if (!IsNetworkActive) { isStandaloneMode = true; InitStandalone(); } }

    private void InitStandalone()
    {
        localHealth = maxHealth;
        SnapToNavMesh(); if (clawHitbox != null) clawHitbox.SetActive(false);
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

        if (IsServer) { currentHealth.Value = maxHealth; SnapToNavMesh(); if (clawHitbox != null) clawHitbox.SetActive(false); ChangeState(EnemyState.Patrol); GoToNextWaypoint(); }
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
        if (clawHitbox != null) clawHitbox.SetActive(false);
        if (anim != null)
        {
            anim.ResetTrigger(atkTrigger);
            anim.ResetTrigger(hitTrigger);
            anim.ResetTrigger(dieTrigger);

            for (int i = 1; i < anim.layerCount; i++)
            {
                anim.SetLayerWeight(i, 0f);
            }
            anim.Play("quai2Die", 0, 0f);
        }
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

    private void Update()
    {
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
        // -----------------------------------------------------------------------------------------------------------------------------

        if (attackCooldownTimer > 0) attackCooldownTimer -= Time.deltaTime;
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

    private void HandlePatrol()
    {
        if (targetPlayer != null) { ChangeState(EnemyState.Chase); return; }
        if (!AgentReady) return;

        ApplyPatrolEnemySeparation();

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

    // ── Chase (Run) ──
    private void HandleChase()
    {
        if (targetPlayer == null || !IsPlayerAliveAndValid(targetPlayer)) { targetPlayer = null; ReturnToPatrol(); return; }

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
        
        bool frantic = CurrentHealthValue <= maxHealth * 0.4f;
        float swarmBonus = GetSwarmSpeedBonus();
        float moveSpeed = (frantic ? chaseRunSpeed * 1.4f : chaseRunSpeed) + swarmBonus;

        if (AgentReady)
        {
            agent.isStopped = false;
            agent.speed = moveSpeed;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            if (agent.radius < 0.45f) agent.radius = 0.45f;

            float distToPlayer = Vector3.Distance(transform.position, targetPlayer.position);
            agent.avoidancePriority = Mathf.Clamp(10 + Mathf.RoundToInt(distToPlayer * 4f), 5, 95);

            Vector3 targetPos = GetSurroundingPosition(targetPlayer, Mathf.Max(1.4f, attackRange * 0.85f));
            agent.SetDestination(targetPos);
        }
        bool isMoving = AgentReady && agent.velocity.magnitude > 0.2f;
        SetSpeedNet(isMoving ? 1f : 0f);
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

    public Vector3 GetSurroundingPosition(Transform target, float desiredDist)
    {
        if (target == null) return transform.position;

        // 1. Calculate direction from target to this zombie
        Vector3 toEnemy = transform.position - target.position;
        toEnemy.y = 0;
        if (toEnemy.sqrMagnitude < 0.001f) toEnemy = transform.forward;
        else toEnemy.Normalize();

        // 2. Separation force from nearby zombies/enemies to prevent clumping
        Vector3 separation = Vector3.zero;
        int count = 0;
        Collider[] nearby = Physics.OverlapSphere(transform.position, 2.0f);
        foreach (var col in nearby)
        {
            if (col != null && col.gameObject != gameObject && (col.CompareTag("Enemy") || col.gameObject.layer == LayerMask.NameToLayer("Enemy")))
            {
                Vector3 diff = transform.position - col.transform.position;
                diff.y = 0;
                float dist = diff.magnitude;
                if (dist > 0.01f && dist < 2.0f)
                {
                    separation += diff.normalized * ((2.0f - dist) / 2.0f);
                    count++;
                }
            }
        }
        if (count > 0) separation /= count;

        // 3. Blend direction toward target ring position with separation vector
        Vector3 finalDir = (toEnemy + separation * 1.5f).normalized;
        Vector3 desiredPos = target.position + finalDir * desiredDist;

        if (NavMesh.SamplePosition(desiredPos, out NavMeshHit hit, 3.5f, NavMesh.AllAreas))
        {
            return hit.position;
        }
        return target.position;
    }

    private float GetSwarmSpeedBonus()
    {
        int count = 0;
        Collider[] cols = Physics.OverlapSphere(transform.position, 8f);
        foreach (var c in cols)
        {
            if (c != null && c.gameObject != gameObject && c.GetComponentInParent<Enemy2_Zombie>() != null)
                count++;
        }
        return Mathf.Min(2.5f, count * 0.4f);
    }


    private void ReturnToPatrol()
    {
        targetPlayer = null;
        CurrentStateValue = EnemyState.Patrol;
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
        if (AgentReady) agent.isStopped = true; SetSpeedNet(0f);
        staggerTimer -= Time.deltaTime;
        if (staggerTimer <= 0) { if (targetPlayer != null) ChangeState(EnemyState.Chase); else ReturnToPatrol(); }
    }

    private void HandleAttack()
    {
        if (targetPlayer == null) { EndAttack(); return; }
        if (AgentReady) agent.isStopped = true; SetSpeedNet(0f);
        stateTimer -= Time.deltaTime;
        float elapsed = attackDuration - stateTimer;
        if (elapsed < attackDuration * 0.4f) { Vector3 ld = (targetPlayer.position - transform.position); ld.y = 0; if (ld.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(ld), Time.deltaTime * 18f); }
        if (stateTimer <= 0) EndAttack();
    }

    private void EndAttack()
    {
        bool frantic = CurrentHealthValue <= maxHealth * 0.4f;
        attackCooldownTimer = frantic ? 0.15f : 0.6f;
        if (clawHitbox != null) clawHitbox.SetActive(false);
        detectionTimer = 0f;
        if (targetPlayer != null) ChangeState(EnemyState.Chase); else ReturnToPatrol();
    }

    private bool IsPlayerAliveAndValid(Transform pt)
    {
        if (pt == null || !pt.gameObject.activeInHierarchy) return false;

        // Lọc hoàn toàn các đối tượng UI / Canvas / HealthBar
        if (pt.gameObject.layer == LayerMask.NameToLayer("UI")) return false;
        string n = pt.name.ToLower();
        if (n.Contains("ui") || n.Contains("canvas") || n.Contains("hud") || n.Contains("healthbar") || n.Contains("health_bar")) return false;

        IPlayerHUDTarget ps = pt.GetComponentInParent<IPlayerHUDTarget>();
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
            if (curr.CompareTag("Player")) return true;
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

    private void DetectPlayer()
    {
        EnemyState s = CurrentStateValue;
        if (s == EnemyState.Dead || s == EnemyState.Stagger || s == EnemyState.Attack) return;

        Vector3 ep = eyeTransform != null ? eyeTransform.position : transform.position + Vector3.up * 1.5f;
        System.Collections.Generic.List<Transform> alivePlayers = GetAllAlivePlayers();

        bool found = false;
        Transform bestTarget = null;
        float minD = float.MaxValue;

        float currentTargetDist = float.MaxValue;
        if (targetPlayer != null)
        {
            if (IsPlayerAliveAndValid(targetPlayer))
            {
                currentTargetDist = Vector3.Distance(transform.position, targetPlayer.position);
            }
            else
            {
                targetPlayer = null;
            }
        }

        foreach (var pt in alivePlayers)
        {
            if (pt == null || !IsPlayerAliveAndValid(pt)) continue;

            Vector3 center = pt.position + Vector3.up * 1.0f;
            float d = Vector3.Distance(ep, center);
            if (d > sightRange) continue;

            Vector3 dir = (center - ep).normalized;

            bool inFOV = Vector3.Angle(transform.forward, dir) < fieldOfView / 2f;
            bool isProximity = d <= 5.5f;

            if (inFOV || isProximity || pt == targetPlayer)
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

                if (clearLOS && d < minD)
                {
                    minD = d;
                    bestTarget = pt;
                    found = true;
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
        }

        if (currentFSMState != null)
        {
            currentFSMState.Enter();
        }
    }

    private void ApplySpeedAnim(float speed) { if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null) return; anim.SetFloat(speedParam, speed); }
    private void SetSpeedNet(float val) { if (isStandaloneMode) ApplySpeedAnim(val); else if (IsServer && !Mathf.Approximately(netSpeed.Value, val)) netSpeed.Value = val; }
    private void PlayAttackAnimLocal() { if (anim == null) return; anim.ResetTrigger(atkTrigger); anim.SetTrigger(atkTrigger); }
    [ClientRpc] private void PlayAttackClientRpc() { if (!IsServer) PlayAttackAnimLocal(); }

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
        Debug.Log($"[Enemy2_Zombie] TakeDamage: dmg={damage}, isServer={IsServer}, isClient={IsClient}, isSpawned={IsSpawned}, localHp={localHealth}, currentHpNet={currentHealth.Value}");
        if (CurrentStateValue == EnemyState.Dead) return;

        localHealth = Mathf.Max(0f, localHealth - damage);
        if (!isStandaloneMode && IsSpawned && IsServer)
        {
            currentHealth.Value = Mathf.Max(0f, currentHealth.Value - damage);
            localHealth = currentHealth.Value;
            hitCounter.Value++;
        }
        else if (anim != null)
        {
            anim.SetTrigger(hitTrigger);
        }

        EnemyDamageEffectHelper.PlayDamageEffects(gameObject, damage);

        bool isAuth = isStandaloneMode || (IsSpawned && IsServer) || !IsSpawned;
        if (!isAuth) return;

        float activeHp = ActualCurrentHealth;
        if (activeHp <= 0f) { ChangeState(EnemyState.Dead); return; }
        float now = Time.time; if (now - lastDamageTime > 3f) recentHitCount = 0; recentHitCount++; lastDamageTime = now;
        if ((damage >= 20f || recentHitCount >= 3) && CurrentStateValue != EnemyState.Stagger) { recentHitCount = 0; staggerTimer = 0.55f; ChangeState(EnemyState.Stagger); }
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
        Debug.Log($"[Enemy2_Zombie] Bị choáng (Skill Q Arthur) trong {duration}s");
    }

    private void Die()
    {
        if (AgentReady) agent.isStopped = true; SetSpeedNet(0f);
        ApplyLocalDeathEffects();
        DropExperience(); DropItems(); Invoke(nameof(DespawnEnemy), 2.5f);
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
    public void EnableClawHitbox()
    {
        bool auth = isStandaloneMode || (IsNetworkActive && IsServer);
        if (auth && !hasDealtDamage)
        {
            hasDealtDamage = true;
            bool frantic = CurrentHealthValue <= maxHealth * 0.4f;
            DealConeDamage(frantic ? 8f : 5f, 2.0f, 90f, 1.0f);
        }
    }
    public void DisableClawHitbox() { hasDealtDamage = false; }

    // ─── Nested FSM States ───
    private class PatrolState : IEnemyState
    {
        private Enemy2_Zombie enemy;
        public PatrolState(Enemy2_Zombie enemy) { this.enemy = enemy; }
        public void Enter() {}
        public void Update() { enemy.HandlePatrol(); }
        public void Exit() {}
    }

    private class ChaseState : IEnemyState
    {
        private Enemy2_Zombie enemy;
        public ChaseState(Enemy2_Zombie enemy) { this.enemy = enemy; }
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
        private Enemy2_Zombie enemy;
        public AttackState(Enemy2_Zombie enemy) { this.enemy = enemy; }
        public void Enter()
        {
            if (enemy.AgentReady) enemy.agent.isStopped = true;
            enemy.SetSpeedNet(0f);
            enemy.hasDealtDamage = false;
            bool frantic = enemy.CurrentHealthValue <= enemy.maxHealth * 0.4f;
            enemy.attackDuration = frantic ? 0.65f : 0.9f;
            enemy.stateTimer = enemy.attackDuration;
            enemy.PlayAttackAnimLocal();
            if (!enemy.isStandaloneMode) enemy.PlayAttackClientRpc();
        }
        public void Update() { enemy.HandleAttack(); }
        public void Exit()
        {
        }
    }

    private class StaggerState : IEnemyState
    {
        private Enemy2_Zombie enemy;
        public StaggerState(Enemy2_Zombie enemy) { this.enemy = enemy; }
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
        private Enemy2_Zombie enemy;
        public DeadState(Enemy2_Zombie enemy) { this.enemy = enemy; }
        public void Enter()
        {
            enemy.Die();
        }
        public void Update() {}
        public void Exit() {}
    }


}
