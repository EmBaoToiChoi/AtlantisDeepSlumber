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
        SetSpeedNet(0.5f);
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
            SetSpeedNet(0f); // Idle tại điểm thoáng 2-3s
            waypointWaitTimer -= Time.deltaTime;
            if (waypointWaitTimer <= 0f)
            {
                GoToNextWaypoint();
            }
        }
        else
        {
            bool moving = AgentReady && agent.velocity.magnitude > 0.15f;
            SetSpeedNet(moving ? 0.5f : 0f);

            if (AgentReady)
            {
                if (!agent.pathPending && (agent.remainingDistance <= agent.stoppingDistance + 0.4f || !agent.hasPath))
                {
                    agent.isStopped = true;
                    SetSpeedNet(0f);
                    waitingAtWaypoint = true;
                    waypointWaitTimer = Random.Range(2.0f, 3.0f); // Dừng 2 - 3 giây
                }
            }
            else
            {
                SnapToNavMesh();
                if (AgentReady) GoToNextWaypoint();
            }
        }
    }

    private void ExecuteBlinkEscape()
    {
        if (blinkTimer > 0 || targetPlayer == null || !AgentReady) return;

        Vector3 away = (transform.position - targetPlayer.position).normalized;
        Vector3 blinkDir = (away + transform.right * (Random.value < 0.5f ? 0.6f : -0.6f)).normalized;
        float blinkDist = 6.5f;

        // TIA RAYCAST CHỐNG TỐC BIẾN XUYÊN TƯỜNG
        if (Physics.Raycast(transform.position + Vector3.up * 0.8f, blinkDir, out RaycastHit wallHit, blinkDist, obstacleLayer, QueryTriggerInteraction.Ignore))
        {
            blinkDist = Mathf.Max(1.0f, wallHit.distance - 0.8f);
        }

        Vector3 blinkTarget = transform.position + blinkDir * blinkDist;

        if (NavMesh.SamplePosition(blinkTarget, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
            blinkTimer = 3.5f;
            isBlinking = true;
            if (anim != null) anim.SetTrigger(hitTrigger);
        }
    }

    // ── Chase (Kiting) ──
    private void HandleChase()
    {
        if (targetPlayer == null) { ReturnToPatrol(); return; }
        IPlayerHUDTarget ps = targetPlayer.GetComponentInParent<IPlayerHUDTarget>();
        Skeleton sk = targetPlayer.GetComponentInParent<Skeleton>();
        bool isTargetDead = (ps != null && (ps.CurrentHealth <= 0 || ps.IsInvisible)) || (sk != null && sk.CurrentHealthValue <= 0);
        if ((ps == null && sk == null) || isTargetDead) { targetPlayer = null; ReturnToPatrol(); return; }

        Vector3 ep1 = eyeTransform != null ? eyeTransform.position : transform.position + Vector3.up * 1.5f;
        Vector3 ep2 = transform.position + Vector3.up * 0.6f;
        Vector3 targetCenter = targetPlayer.position + Vector3.up * 1.0f;
        float d = Vector3.Distance(ep1, targetCenter);
        Vector3 dir = (targetCenter - ep1).normalized;

        // Wall blocking line of sight check - ngắt rượt đuổi nếu bị tường chắn
        bool wallBlocked = Physics.Raycast(ep1, dir, d, obstacleLayer, QueryTriggerInteraction.Ignore) ||
                           Physics.Raycast(ep2, dir, d, obstacleLayer, QueryTriggerInteraction.Ignore);

        if (wallBlocked)
        {
            loseSightTimer += Time.deltaTime;
            if (loseSightTimer > 0.8f || (AgentReady && agent.hasPath && agent.pathStatus == NavMeshPathStatus.PathPartial))
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
        if (ld.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(ld), Time.deltaTime * 18f);
        Vector3 flatEnemy = transform.position; flatEnemy.y = 0;
        Vector3 flatPlayer = targetPlayer.position; flatPlayer.y = 0;
        float dist = Vector3.Distance(flatEnemy, flatPlayer);
        float spd = CurrentHealthValue < maxHealth * 0.5f ? chaseRunSpeed * 1.35f : chaseRunSpeed;

        // EMERGENCY BLINK ESCAPE if player gets too close (< 4.2m)
        if (dist < 4.2f && blinkTimer <= 0)
        {
            ExecuteBlinkEscape();
            return;
        }

        // A. QUÁ GẦN (< minAttackRange = 7.5m): Thoái lui Kiting tốc độ cao
        if (dist < minAttackRange)
        {
            Vector3 away = (transform.position - targetPlayer.position).normalized;
            Vector3 rp = transform.position + away * 5.5f;
            NavMeshHit h;
            if (NavMesh.SamplePosition(rp, out h, 5.5f, NavMesh.AllAreas))
            {
                if (AgentReady)
                {
                    agent.isStopped = false;
                    agent.speed = spd + 2.2f;
                    agent.SetDestination(h.position);
                }
            }
            else
            {
                Vector3 perp = new Vector3(-away.z, 0, away.x);
                Vector3 alt = transform.position + perp * (Random.value < 0.5f ? 5f : -5f);
                if (NavMesh.SamplePosition(alt, out NavMeshHit h2, 5f, NavMesh.AllAreas) && AgentReady)
                {
                    agent.speed = spd + 2.2f;
                    agent.SetDestination(h2.position);
                }
            }

            // Fire spell while backpedaling if cooldown ready
            if (attackCooldownTimer <= 0 && dist >= 3.5f)
            {
                ChangeState(EnemyState.Attack);
            }

            SetSpeedNet(AgentReady && !agent.isStopped ? 1f : 0f);
            return;
        }

        // B. TẦM LÝ TƯỞNG (7.5m <= dist <= 16m): Đứng bắn phép
        if (dist >= minAttackRange && dist <= maxAttackRange)
        {
            if (AgentReady) agent.isStopped = true;
            SetSpeedNet(0f);
            if (attackCooldownTimer <= 0) ChangeState(EnemyState.Attack);
            return;
        }

        // C. QUÁ XA (> 16m): Tiến lại gần
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
        SetSpeedNet(AgentReady && !agent.isStopped ? 1f : 0f);
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

    private void ReturnToPatrol() { targetPlayer = null; CurrentStateValue = EnemyState.Patrol; waitingAtWaypoint = false; waypointWaitTimer = 0f; GoToNextWaypoint(); }

    private void HandleStagger() { if (AgentReady) agent.isStopped = true; SetSpeedNet(0f); staggerTimer -= Time.deltaTime; if (staggerTimer <= 0) { if (targetPlayer != null) ChangeState(EnemyState.Chase); else ReturnToPatrol(); } }

    private void HandleAttack()
    {
        if (targetPlayer == null) { EndAttack(); return; }

        float dist = Vector3.Distance(transform.position, targetPlayer.position);
        if (dist < 4.5f && blinkTimer <= 0)
        {
            ExecuteBlinkEscape();
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

    private void DetectPlayer()
    {
        EnemyState s = CurrentStateValue;
        if (s == EnemyState.Dead || s == EnemyState.Stagger || s == EnemyState.Attack) return;

        Vector3 ep = eyeTransform != null ? eyeTransform.position : transform.position + Vector3.up * 1.5f;

        int num = Physics.OverlapSphereNonAlloc(transform.position, sightRange, detectionResults, playerLayer);
        if (num == 0) num = FallbackDetect();

        bool found = false;
        Transform bestTarget = null;
        float minD = float.MaxValue;

        // Current target distance (if valid)
        float currentTargetDist = float.MaxValue;
        if (targetPlayer != null)
        {
            IPlayerHUDTarget ps = targetPlayer.GetComponentInParent<IPlayerHUDTarget>();
            Skeleton sk = targetPlayer.GetComponentInParent<Skeleton>();
            bool isDead = (ps != null && (ps.CurrentHealth <= 0 || ps.IsInvisible)) || (sk != null && sk.CurrentHealthValue <= 0);
            if ((ps != null || sk != null) && !isDead)
            {
                currentTargetDist = Vector3.Distance(transform.position, targetPlayer.position);
            }
            else
            {
                targetPlayer = null;
            }
        }

        for (int i = 0; i < num; i++)
        {
            if (detectionResults[i] == null) continue;
            Transform pt = detectionResults[i].transform;
            IPlayerHUDTarget ps = pt.GetComponentInParent<IPlayerHUDTarget>();
            Skeleton sk = pt.GetComponentInParent<Skeleton>();
            bool isTargetDead = (ps != null && (ps.CurrentHealth <= 0 || ps.IsInvisible)) || (sk != null && sk.CurrentHealthValue <= 0);
            if ((ps == null && sk == null) || isTargetDead) continue;

            Vector3 center = pt.position + Vector3.up;
            float d = Vector3.Distance(ep, center);
            Vector3 dir = (center - ep).normalized;

            // Vision Cone OR Proximity Detection Radius (4.5m radius for walking past)
            bool inFOV = Vector3.Angle(transform.forward, dir) < fieldOfView / 2f;
            bool isProximity = d <= 4.5f;

            if ((inFOV || isProximity || pt == targetPlayer) && !Physics.Raycast(ep, dir, d, obstacleLayer))
            {
                if (d < minD)
                {
                    minD = d;
                    bestTarget = pt;
                    found = true;
                }
            }
        }

        if (found && bestTarget != null)
        {
            // Dynamic Retargeting:
            // Switch target if no target, or if bestTarget is significantly closer (> 2.5m closer) or within melee range (< 4.0m)
            if (targetPlayer == null || (bestTarget != targetPlayer && (minD < currentTargetDist - 2.5f || minD < 4.0f)))
            {
                targetPlayer = bestTarget;
            }

            AlertNearbyAllies(targetPlayer);
            if (s != EnemyState.Chase) ChangeState(EnemyState.Chase);
        }
        else if (s == EnemyState.Chase)
        {
            ReturnToPatrol();
        }
    }

    private int FallbackDetect()
    {
        int c = 0;
        foreach (var p in GameObject.FindGameObjectsWithTag("Player")) { if (c >= detectionResults.Length) break; if (Vector3.Distance(transform.position, p.transform.position) <= sightRange) { var col = p.GetComponent<Collider>(); if (col != null) detectionResults[c++] = col; } }
        if (c == 0 && IsNetworkActive && NetworkManager.Singleton != null) { foreach (var kvp in NetworkManager.Singleton.ConnectedClients) { if (c >= detectionResults.Length) break; var po = kvp.Value.PlayerObject; if (po == null || Vector3.Distance(transform.position, po.transform.position) > sightRange) continue; var col = po.GetComponent<Collider>(); if (col != null) detectionResults[c++] = col; } }
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