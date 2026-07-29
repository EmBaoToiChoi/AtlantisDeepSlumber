using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Enemy 5 - Phù Thủy (Ranged/Kiting)
/// Animator Float "Speed": 0=Idle, 0.5=Walk (patrol), 1=Run (kiting/chase)
/// Attack trigger: quai5Attack
/// </summary>
public class Enemy5_PhuThuy : NetworkBehaviour
{
    public enum EnemyState { Patrol, Chase, Stagger, Attack, Dead }

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
    public float spellSpeed  = 12f;
    public float spellDamage = 7f;

    [Header("Drops")]
    public GameObject expGemPrefab;
    public float expDropAmount = 25f;
    public Transform expDropPoint;
    public GameObject repairItemPrefab;
    [Range(0f, 1f)] public float repairItemDropChance = 0.3f;

    [Header("AI Settings")]
    public float sightRange     = 24f;     // Tầm phát hiện xa hơn
    public float fieldOfView    = 120f;
    public float maxAttackRange = 16f;     // Tầm bắn xa
    public float minAttackRange = 7.5f;    // Kiting: bắt đầu thoái lui khi Player gần hơn 7.5m
    public float patrolWalkSpeed = 2.5f;
    public float chaseRunSpeed   = 4.5f;    // Tốc độ chạy kiting nhanh hơn
    public float patrolWaitMin = 1.5f;
    public float patrolWaitMax = 5f;
    [Range(0f, 1f)] public float patrolMoveChance = 0.65f;

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
    private bool wasEnraged;

    private Transform targetPlayer;
    private int currentWaypointIndex = -1;
    private bool waitingAtWaypoint;
    private float waypointWaitTimer;
    private float detectionTimer;
    private const float DETECTION_INTERVAL = 0.15f;
    private float staggerTimer;
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
        if (anim != null)
        {
            anim.ResetTrigger(attackTrigger);
            anim.ResetTrigger(hitTrigger);
            anim.ResetTrigger(dieTrigger);

            for (int i = 1; i < anim.layerCount; i++)
            {
                anim.SetLayerWeight(i, 0f);
            }
            anim.Play("Quai5Die", 0, 0f);
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

        if (attackCooldownTimer > 0) attackCooldownTimer -= Time.deltaTime;
        if (blinkTimer > 0) { blinkTimer -= Time.deltaTime; if (blinkTimer <= 0) isBlinking = false; }
        detectionTimer -= Time.deltaTime;
        if (detectionTimer <= 0) { detectionTimer = DETECTION_INTERVAL; DetectPlayer(); }
        if (currentFSMState != null)
        {
            currentFSMState.Update();
        }
    }



    private void UpdateEnrageVisuals()
    {
        if (modelRenderers == null || modelRenderers.Length == 0 || propBlock == null) return;
        bool enraged = CurrentHealthValue < maxHealth * 0.5f;
        if (enraged) { wasEnraged = true; float pp = Mathf.PingPong(Time.time * 3f, 1f); Color c = Color.Lerp(Color.white, new Color(0f, 0.6f, 1f), pp); foreach (var r in modelRenderers) { if (r != null) { r.GetPropertyBlock(propBlock); propBlock.SetColor("_Color", c); r.SetPropertyBlock(propBlock); } } }
        else if (wasEnraged) { wasEnraged = false; foreach (var r in modelRenderers) { if (r != null) { r.GetPropertyBlock(propBlock); propBlock.SetColor("_Color", Color.white); r.SetPropertyBlock(propBlock); } } }
    }

    // ── Patrol (Walk) ──
    private Vector3 GetRandomNavMeshPosition(float range)
    {
        Vector3 origin = transform.position;
        for (int i = 0; i < 30; i++)
        {
            Vector3 randomDirection = Random.insideUnitSphere * range;
            randomDirection += origin;
            if (NavMesh.SamplePosition(randomDirection, out NavMeshHit navHit, range, NavMesh.AllAreas))
            {
                float dist = Vector3.Distance(origin, navHit.position);
                if (dist > 2.5f)
                {
                    Vector3 dir = (navHit.position - origin).normalized;
                    if (!Physics.Raycast(origin + Vector3.up * 0.5f, dir, dist, obstacleLayer, QueryTriggerInteraction.Ignore))
                    {
                        return navHit.position;
                    }
                }
            }
        }
        return origin;
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

    // ── Chase (Kiting) ──
    private void HandleChase()
    {
        if (targetPlayer == null || !IsPlayerAliveAndValid(targetPlayer)) { targetPlayer = null; ReturnToPatrol(); return; }

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

        // Luôn quay mặt về phía Player
        Vector3 ld = (targetPlayer.position - transform.position); ld.y = 0;
        if (ld.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(ld), Time.deltaTime * 18f);
        Vector3 flatEnemy = transform.position; flatEnemy.y = 0;
        Vector3 flatPlayer = targetPlayer.position; flatPlayer.y = 0;
        float dist = Vector3.Distance(flatEnemy, flatPlayer);
        float spd = CurrentHealthValue < maxHealth * 0.5f ? chaseRunSpeed * 1.35f : chaseRunSpeed;

        // A. QUÁ GẦN (< minAttackRange): ĐI LÙI ra xa Player (walk backward)
        if (dist < minAttackRange)
        {
            Vector3 away = (transform.position - targetPlayer.position);
            away.y = 0;
            away = away.normalized;

            // Tính điểm đến phía sau lưng (hướng xa Player)
            float retreatDist = Mathf.Clamp(minAttackRange - dist + 2.0f, 2.0f, 6.0f);
            Vector3 retreatPos = transform.position + away * retreatDist;

            // Kiểm tra tường phía sau: nếu bị chặn thì đi chéo sang bên
            if (Physics.Raycast(transform.position + Vector3.up * 0.8f, away, out RaycastHit wallCheck, retreatDist, obstacleLayer, QueryTriggerInteraction.Ignore))
            {
                // Bị tường chặn phía sau → thử đi chéo sang trái/phải
                Vector3 perp = new Vector3(-away.z, 0, away.x);
                Vector3 sideDir = (away + perp * (Random.value < 0.5f ? 1.2f : -1.2f)).normalized;
                retreatPos = transform.position + sideDir * retreatDist;
            }

            if (NavMesh.SamplePosition(retreatPos, out NavMeshHit navHit, 4.0f, NavMesh.AllAreas))
            {
                if (AgentReady)
                {
                    agent.isStopped = false;
                    agent.speed = spd * 0.85f; // Đi lùi chậm hơn chạy tới một chút
                    agent.SetDestination(navHit.position);
                }
            }

            // Vừa đi lùi vừa bắn phép nếu cooldown sẵn sàng
            if (attackCooldownTimer <= 0 && dist >= 3.0f)
            {
                ChangeState(EnemyState.Attack);
            }

            SetSpeedNet(AgentReady && !agent.isStopped ? 0.5f : 0f); // Walk animation khi đi lùi
            return;
        }

        // B. TẦM LÝ TƯỞNG (minAttackRange <= dist <= maxAttackRange): Đứng bắn phép
        if (dist >= minAttackRange && dist <= maxAttackRange)
        {
            if (AgentReady) agent.isStopped = true;
            SetSpeedNet(0f);
            if (attackCooldownTimer <= 0) ChangeState(EnemyState.Attack);
            return;
        }

        // C. QUÁ XA (> maxAttackRange): Tiến lại gần
        if (AgentReady)
        {
            agent.isStopped = false;
            agent.speed = spd;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            if (agent.radius < 0.45f) agent.radius = 0.45f;

            float distToPlayer = Vector3.Distance(transform.position, targetPlayer.position);
            agent.avoidancePriority = Mathf.Clamp(10 + Mathf.RoundToInt(distToPlayer * 4f), 5, 95);

            Vector3 targetPos = GetSurroundingPosition(targetPlayer, minAttackRange * 1.1f);
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

        Vector3 toEnemy = transform.position - target.position;
        toEnemy.y = 0;
        if (toEnemy.sqrMagnitude < 0.001f) toEnemy = transform.forward;
        else toEnemy.Normalize();

        Vector3 separation = Vector3.zero;
        int count = 0;
        Collider[] nearby = Physics.OverlapSphere(transform.position, 2.5f);
        foreach (var col in nearby)
        {
            if (col != null && col.gameObject != gameObject && (col.CompareTag("Enemy") || col.gameObject.layer == LayerMask.NameToLayer("Enemy")))
            {
                Vector3 diff = transform.position - col.transform.position;
                diff.y = 0;
                float dist = diff.magnitude;
                if (dist > 0.01f && dist < 2.5f)
                {
                    separation += diff.normalized * ((2.5f - dist) / 2.5f);
                    count++;
                }
            }
        }
        if (count > 0) separation /= count;

        Vector3 finalDir = (toEnemy + separation * 1.5f).normalized;
        Vector3 desiredPos = target.position + finalDir * desiredDist;

        if (NavMesh.SamplePosition(desiredPos, out NavMeshHit hit, 4.0f, NavMesh.AllAreas))
        {
            return hit.position;
        }
        return target.position;
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

    private void HandleStagger() { if (AgentReady) agent.isStopped = true; SetSpeedNet(0f); staggerTimer -= Time.deltaTime; if (staggerTimer <= 0) { if (targetPlayer != null) ChangeState(EnemyState.Chase); else ReturnToPatrol(); } }

    private void HandleAttack()
    {
        if (targetPlayer == null) { EndAttack(); return; }

        float dist = Vector3.Distance(transform.position, targetPlayer.position);
        // Nếu Player áp sát quá gần trong lúc đang cast phép → hủy đánh, quay về Chase để đi lùi
        if (dist < 4.5f)
        {
            EndAttack();
            return;
        }

        if (AgentReady) agent.isStopped = true; SetSpeedNet(0f);
        stateTimer -= Time.deltaTime;
        float elapsed = attackDuration - stateTimer;
        if (elapsed < attackDuration * 0.5f) { Vector3 ld = (targetPlayer.position - transform.position); ld.y = 0; if (ld.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(ld); }
        if (stateTimer <= 0) EndAttack();
    }

    private void EndAttack()
    {
        attackCooldownTimer = (CurrentHealthValue < maxHealth * 0.5f) ? 0.3f : 0.7f;
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
    private void PlayAttackAnimLocal() { if (anim == null) return; anim.ResetTrigger(attackTrigger); anim.SetTrigger(attackTrigger); }
    [ClientRpc] private void PlayAttackClientRpc() { if (!IsServer) PlayAttackAnimLocal(); }

    // Animation Event (từ keyframe chưởng)
    public void TriggerSpellLaunch()
    {
        bool auth = isStandaloneMode || (IsNetworkActive && IsServer);
        if (!auth || CurrentStateValue != EnemyState.Attack || hasCastSpell) return;
        hasCastSpell = true; LaunchSpellBall();
    }

    private void LaunchSpellBall()
    {
        if (targetPlayer == null) return;
        Vector3 spawnPt = staffTipTransform != null ? staffTipTransform.position : transform.position + transform.forward * 1.2f + Vector3.up * 1.2f;
        Vector3 dir = (targetPlayer.position + Vector3.up - spawnPt).normalized;
        if (spellProjectilePrefab != null)
        {
            var proj = Instantiate(spellProjectilePrefab, spawnPt, Quaternion.LookRotation(dir));
            var rb = proj.GetComponent<Rigidbody>(); if (rb != null) rb.linearVelocity = dir * spellSpeed;
            var spellBall = proj.GetComponent<SpellBall>();
            if (spellBall == null)
            {
                spellBall = proj.AddComponent<SpellBall>();
            }
            spellBall.damage = spellDamage;
            spellBall.knockback = 1.5f;
            
            var no = proj.GetComponent<NetworkObject>(); if (no != null && !isStandaloneMode) no.Spawn(true);
        }
        else
        {
            if (Physics.Raycast(spawnPt, dir, out RaycastHit hit, maxAttackRange + 2f))
            { EnemyDamageHelper.DealDamage(hit.collider.transform, spellDamage, dir * 1.5f); }
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
        if ((damage >= 22f || recentHitCount >= 3) && CurrentStateValue != EnemyState.Stagger) { recentHitCount = 0; staggerTimer = 0.5f; ChangeState(EnemyState.Stagger); }
        else if (!isBlinking && Random.value < 0.35f && CurrentStateValue == EnemyState.Chase) ExecuteBlinkDodge();
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
        Vector3 perp = new Vector3(-tp.z, 0, tp.x) * (Random.value < 0.5f ? 1f : -1f);
        NavMeshHit h; if (NavMesh.SamplePosition(transform.position + perp * 3.2f, out h, 3.2f, NavMesh.AllAreas))
        { isBlinking = true; blinkTimer = 0.25f; agent.isStopped = false; agent.speed = 16f; agent.SetDestination(h.position); }
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

    private void OnDrawGizmosSelected()
    {
        Vector3 eye = eyeTransform != null ? eyeTransform.position : transform.position + Vector3.up * 1.5f;
        Gizmos.color = new Color(0.85f, 0.1f, 0.95f, 0.4f); Gizmos.DrawWireSphere(eye, sightRange);
        Vector3 l = Quaternion.AngleAxis(-fieldOfView / 2f, Vector3.up) * transform.forward;
        Vector3 r = Quaternion.AngleAxis( fieldOfView / 2f, Vector3.up) * transform.forward;
        Gizmos.DrawRay(eye, l * sightRange); Gizmos.DrawRay(eye, r * sightRange);
        Gizmos.color = new Color(1f, 0.64f, 0f, 0.8f); Gizmos.DrawWireSphere(transform.position, minAttackRange);
        Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(transform.position, maxAttackRange);
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


}