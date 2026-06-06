using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Enemy 1 - Đập Búa
/// Patrol: 3 waypoints | Walk (tuần tra) → Run (đuổi Player) → Idle (đứng)
/// Animator Float "Speed": 0=Idle, 0.5=Walk, 1=Run
/// </summary>
public class Enemy1_DapBua : NetworkBehaviour
{
    public enum EnemyState { Patrol, Chase, Stagger, Attack, Dead }

    // ─── Health ────────────────────────────────────────────────
    [Header("Health")]
    public float maxHealth = 100f;
    public NetworkVariable<float> currentHealth = new NetworkVariable<float>(
        100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // ─── Network State Sync ────────────────────────────────────
    public NetworkVariable<EnemyState> currentState = new NetworkVariable<EnemyState>(
        EnemyState.Patrol, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> isEnraged = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Network Anim Sync")]
    /// <summary>Float: 0=Idle, 0.5=Walk, 1=Run — Base Layer</summary>
    public NetworkVariable<float> netSpeed = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> hitCounter = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> attackType = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // ─── Standalone Fallback ───────────────────────────────────
    private float localHealth;
    private EnemyState localState = EnemyState.Patrol;
    private bool localIsEnraged;
    private bool isStandaloneMode;
    private bool IsNetworkActive => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

    private EnemyState CurrentStateValue
    {
        get => isStandaloneMode ? localState : currentState.Value;
        set { if (isStandaloneMode) localState = value; else currentState.Value = value; }
    }
    private float CurrentHealthValue
    {
        get => isStandaloneMode ? localHealth : currentHealth.Value;
        set { if (isStandaloneMode) localHealth = value; else currentHealth.Value = value; }
    }
    private bool IsEnragedValue
    {
        get => isStandaloneMode ? localIsEnraged : isEnraged.Value;
        set { if (isStandaloneMode) localIsEnraged = value; else isEnraged.Value = value; }
    }
    public bool IsDead => isStandaloneMode ? (localState == EnemyState.Dead) : (currentState.Value == EnemyState.Dead);
    /// <summary>HP hiện tại đúng trong cả Standalone lẫn Network mode — dùng cho HP bar polling.</summary>
    public float ActualCurrentHealth => isStandaloneMode ? localHealth : currentHealth.Value;

    // ─── Components ────────────────────────────────────────────
    [Header("Components")]
    public NavMeshAgent agent;
    public Animator anim;
    public Transform eyeTransform;

    [Header("Melee Hitboxes")]
    public GameObject hammerHitbox;
    public GameObject hammerHitboxLeft;
    public GameObject hammerHitboxRight;

    // ─── Patrol Waypoints ──────────────────────────────────────
    [Header("Patrol Waypoints (3 điểm hình tam giác)")]
    public Transform[] waypoints = new Transform[3];

    // ─── Drops ─────────────────────────────────────────────────
    [Header("Drops")]
    public GameObject expGemPrefab;
    public float expDropAmount = 25f;
    public Transform expDropPoint;
    public GameObject repairItemPrefab;
    [Range(0f, 1f)] public float repairItemDropChance = 0.3f;

    // ─── AI Settings ───────────────────────────────────────────
    [Header("AI Settings")]
    public float sightRange = 15f;
    public float fieldOfView = 90f;
    public float attackRange = 2f;
    public float patrolWalkSpeed = 2.5f;  // Tốc di chuyển khi tuần tra (Walk)
    public float chaseRunSpeed  = 5f;     // Tốc đuổi Player (Run)
    public float patrolWaitMin = 1f;
    public float patrolWaitMax = 4f;
    [Range(0f, 1f)] public float patrolMoveChance = 0.7f;

    [Header("Layers")]
    public LayerMask playerLayer;
    public LayerMask obstacleLayer;

    // ─── Animator Parameters ───────────────────────────────────
    [Header("Animator Parameter Names")]
    [Tooltip("Float: 0=Idle, 0.5=Walk, 1=Run — Base Layer")]
    public string speedParam      = "Speed";       // Float  — Base Layer
    public string hitTrigger      = "Hit";         // Trigger — Anhit Layer
    public string dieTrigger      = "Die";         // Trigger — Base Layer
    public string atkLeftTrigger  = "AttackLeft";  // Trigger — Attack Layer
    public string atkRightTrigger = "AttackRight"; // Trigger — Attack Layer
    public string atkComboTrigger = "AttackCombo"; // Trigger — Attack Layer

    // ─── Runtime ───────────────────────────────────────────────
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
    private bool hasDealtDamage1, hasDealtDamage2;
    private bool isNextAttackLeft = true;
    private bool hasRoared;
    private float lastDamageTime;
    private int recentHitCount;
    private bool isDodging;
    private float dodgeTimer;

    private readonly Collider[] detectionResults = new Collider[8];
    private readonly Collider[] damageResults    = new Collider[8];
    private bool AgentReady => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;

    // ══════════════════════════════════════════════════════════
    //  AWAKE / START / SPAWN
    // ══════════════════════════════════════════════════════════
    private void Awake()
    {
        if (anim == null) anim = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);
        var na = GetComponent<Unity.Netcode.Components.NetworkAnimator>();
        if (na != null)
        {
            if (anim == null || anim.runtimeAnimatorController == null) na.enabled = false;
            else na.Animator = anim;
        }
    }

    private void Start()
    {
        if (!IsNetworkActive) { isStandaloneMode = true; InitStandalone(); }
    }

    private void InitStandalone()
    {
        localHealth = maxHealth; localState = EnemyState.Patrol;
        SnapToNavMesh(); DisableHitboxes();
        ApplySpeedAnim(0f);
        GoToNextWaypoint();
    }

    public override void OnNetworkSpawn()
    {
        isStandaloneMode = false;

        var nt = GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (nt != null) { nt.PositionThreshold = 0.001f; nt.RotAngleThreshold = 0.01f; nt.ScaleThreshold = 0.01f; }

        // ── Client-side: nhận sync animation từ Server ──
        // Base Layer: Speed float (Idle/Walk/Run)
        netSpeed.OnValueChanged   += (_, v) => ApplySpeedAnim(v);
        // Anhit Layer: trigger synced via hitCounter
        hitCounter.OnValueChanged += (_, _) => { if (anim != null) anim.SetTrigger(hitTrigger); };
        currentHealth.OnValueChanged += OnHealthNetChanged;
        currentState.OnValueChanged  += OnStateChanged;

        // Khởi tạo animation theo giá trị hiện tại
        ApplySpeedAnim(netSpeed.Value);

        if (IsServer)
        {
            currentHealth.Value = maxHealth;
            isEnraged.Value = false;
            SnapToNavMesh();
            DisableHitboxes();
            GoToNextWaypoint();
        }
        else
        {
            // Client KHÔNG điều khiển NavMeshAgent - NetworkTransform từ Server
            if (agent != null) agent.enabled = false;
        }
    }

    public override void OnNetworkDespawn()
    {
        netSpeed.OnValueChanged   -= (_, v) => ApplySpeedAnim(v);
        hitCounter.OnValueChanged -= (_, _) => { if (anim != null) anim.SetTrigger(hitTrigger); };
        currentHealth.OnValueChanged -= OnHealthNetChanged;
        currentState.OnValueChanged  -= OnStateChanged;
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
        DisableHitboxes();
        if (anim != null)
        {
            anim.ResetTrigger(atkLeftTrigger);
            anim.ResetTrigger(atkRightTrigger);
            anim.ResetTrigger(atkComboTrigger);
            anim.ResetTrigger(hitTrigger);
            anim.ResetTrigger(dieTrigger);

            for (int i = 1; i < anim.layerCount; i++)
            {
                anim.SetLayerWeight(i, 0f);
            }
            anim.Play("quai1Die", 0, 0f);
        }
    }

    private void OnHealthNetChanged(float oldVal, float newVal)
    {
        float diff = oldVal - newVal;
        if (diff > 0)
        {
            EnemyDamageEffectHelper.PlayDamageEffects(gameObject, diff);
        }
    }

    // ══════════════════════════════════════════════════════════
    //  UPDATE (chỉ Server / Standalone chạy AI logic)
    // ══════════════════════════════════════════════════════════
    private void Update()
    {
        // Enrage scale (mọi client)
        if (IsEnragedValue && !hasRoared)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * 1.15f, Time.deltaTime * 3f);
            if (transform.localScale.x >= 1.14f) hasRoared = true;
        }

        bool aiAuth = isStandaloneMode || (IsNetworkActive && IsServer);
        if (!aiAuth) return;

        if (AgentReady && !agent.isOnNavMesh) SnapToNavMesh();
        if (attackCooldownTimer > 0) attackCooldownTimer -= Time.deltaTime;
        if (dodgeTimer > 0) { dodgeTimer -= Time.deltaTime; if (dodgeTimer <= 0) isDodging = false; }

        detectionTimer -= Time.deltaTime;
        if (detectionTimer <= 0) { detectionTimer = DETECTION_INTERVAL; DetectPlayer(); }

        switch (CurrentStateValue)
        {
            case EnemyState.Patrol:  HandlePatrol();  break;
            case EnemyState.Chase:   HandleChase();   break;
            case EnemyState.Stagger: HandleStagger(); break;
            case EnemyState.Attack:  HandleAttack();  break;
        }
    }

    // ══════════════════════════════════════════════════════════
    //  PATROL  (Walk animation = Speed 0.5)
    // ══════════════════════════════════════════════════════════
    private void GoToNextWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0) { SetSpeedNet(0f); return; }
        if (waypoints.Length > 1)
        {
            int n; do { n = Random.Range(0, waypoints.Length); } while (n == currentWaypointIndex);
            currentWaypointIndex = n;
        }
        else currentWaypointIndex = 0;

        waitingAtWaypoint = false;
        if (waypoints[currentWaypointIndex] == null) { SetSpeedNet(0f); return; }

        if (AgentReady)
        {
            agent.isStopped = false;
            agent.speed = patrolWalkSpeed;
            agent.SetDestination(waypoints[currentWaypointIndex].position);
        }
        // Dùng 0.5 để phân biệt Walk với Run (1.0)
        SetSpeedNet(0.5f);
    }

    private void HandlePatrol()
    {
        if (waypoints == null || waypoints.Length == 0) { SetSpeedNet(0f); return; }

        if (waitingAtWaypoint)
        {
            // Đứng tại waypoint → Idle
            SetSpeedNet(0f);
            waypointWaitTimer -= Time.deltaTime;
            if (waypointWaitTimer <= 0)
            {
                if (Random.value <= patrolMoveChance) GoToNextWaypoint();
                else waypointWaitTimer = Random.Range(patrolWaitMin, patrolWaitMax);
            }
        }
        else
        {
            // Đang di chuyển đến waypoint
            // Chỉ set Walk animation khi thực sự có velocity (chống glide)
            bool moving = AgentReady && agent.velocity.magnitude > 0.15f;
            SetSpeedNet(moving ? 0.5f : 0f);

            if (AgentReady && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.3f)
            {
                if (AgentReady) agent.isStopped = true;
                SetSpeedNet(0f); // → Idle
                waitingAtWaypoint = true;
                waypointWaitTimer = Random.Range(patrolWaitMin, patrolWaitMax);
            }
        }
    }

    // ══════════════════════════════════════════════════════════
    //  CHASE  (Run animation = Speed 1.0)
    // ══════════════════════════════════════════════════════════
    private void HandleChase()
    {
        if (targetPlayer == null) { ReturnToPatrol(); return; }
        IPlayerHUDTarget ps = targetPlayer.GetComponentInParent<IPlayerHUDTarget>();
        if (ps != null && (ps.CurrentHealth <= 0 || ps.IsInvisible)) { targetPlayer = null; ReturnToPatrol(); return; }

        Vector3 ld = (targetPlayer.position - transform.position); ld.y = 0;
        if (ld.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(ld), Time.deltaTime * 15f);

        Vector3 flatEnemy = transform.position; flatEnemy.y = 0;
        Vector3 flatPlayer = targetPlayer.position; flatPlayer.y = 0;
        float dist = Vector3.Distance(flatEnemy, flatPlayer);

        if (dist <= attackRange)
        {
            if (AgentReady) agent.isStopped = true;
            SetSpeedNet(0f); // Dừng lại → Idle trước khi Attack
            if (attackCooldownTimer <= 0) ChangeState(EnemyState.Attack);
            return;
        }

        // Đuổi theo → Run
        float runSpd = IsEnragedValue ? chaseRunSpeed * 1.5f : chaseRunSpeed;
        if (AgentReady)
        {
            agent.isStopped = false;
            agent.speed = runSpd;
            agent.SetDestination(targetPlayer.position);
        }
        // Velocity check: chống glide
        bool actuallyMoving = AgentReady && agent.velocity.magnitude > 0.2f;
        SetSpeedNet(actuallyMoving ? 1f : 0f);
    }

    private void ReturnToPatrol()
    {
        targetPlayer = null;
        CurrentStateValue = EnemyState.Patrol;
        waitingAtWaypoint = false;
        waypointWaitTimer = 0f;
        GoToNextWaypoint();
    }

    // ══════════════════════════════════════════════════════════
    //  STAGGER
    // ══════════════════════════════════════════════════════════
    private void HandleStagger()
    {
        if (AgentReady) agent.isStopped = true;
        SetSpeedNet(0f);
        staggerTimer -= Time.deltaTime;
        if (staggerTimer <= 0)
        {
            if (IsEnragedValue && !hasRoared) hasRoared = true;
            if (targetPlayer != null) ChangeState(EnemyState.Chase);
            else ReturnToPatrol();
        }
    }

    // ══════════════════════════════════════════════════════════
    //  ATTACK
    // ══════════════════════════════════════════════════════════
    private void HandleAttack()
    {
        if (targetPlayer == null) { EndAttack(); return; }
        if (AgentReady) agent.isStopped = true;
        SetSpeedNet(0f); // Không di chuyển khi tấn công

        stateTimer -= Time.deltaTime;
        float elapsed = attackDuration - stateTimer;

        // Quay mặt về phía player trong 35% đầu của đòn
        if (elapsed < attackDuration * 0.35f)
        {
            Vector3 ld = (targetPlayer.position - transform.position); ld.y = 0;
            if (ld.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(ld), Time.deltaTime * 18f);
        }

        if (stateTimer <= 0) EndAttack();
    }

    private void EndAttack()
    {
        attackCooldownTimer = (CurrentHealthValue < maxHealth * 0.5f) ? 0.3f : 0.7f;
        DisableHitboxes();
        detectionTimer = 0f;
        if (targetPlayer != null) ChangeState(EnemyState.Chase);
        else ReturnToPatrol();
    }

    // ══════════════════════════════════════════════════════════
    //  DETECTION
    // ══════════════════════════════════════════════════════════
    private void DetectPlayer()
    {
        EnemyState s = CurrentStateValue;
        if (s == EnemyState.Dead || s == EnemyState.Stagger || s == EnemyState.Attack) return;

        int num = Physics.OverlapSphereNonAlloc(transform.position, sightRange, detectionResults, playerLayer);
        if (num == 0) num = FallbackDetect();

        bool found = false; Transform closest = null; float minD = float.MaxValue;
        Vector3 ep = eyeTransform != null ? eyeTransform.position : transform.position + Vector3.up * 1.5f;

        for (int i = 0; i < num; i++)
        {
            if (detectionResults[i] == null) continue;
            Transform pt = detectionResults[i].transform;
            IPlayerHUDTarget ps = pt.GetComponentInParent<IPlayerHUDTarget>();
            if (ps != null && (ps.CurrentHealth <= 0 || ps.IsInvisible)) continue;
            Vector3 center = pt.position + Vector3.up;
            float d = Vector3.Distance(ep, center);
            Vector3 dir = (center - ep).normalized;
            bool inFOV = Vector3.Angle(transform.forward, dir) < fieldOfView / 2f;
            if ((inFOV || pt == targetPlayer) && !Physics.Raycast(ep, dir, d, obstacleLayer))
            { if (d < minD) { minD = d; closest = pt; found = true; } }
        }

        if (found && closest != null)
        {
            targetPlayer = closest;
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
        foreach (var p in GameObject.FindGameObjectsWithTag("Player"))
        {
            if (c >= detectionResults.Length) break;
            if (Vector3.Distance(transform.position, p.transform.position) <= sightRange)
            { var col = p.GetComponent<Collider>(); if (col != null) detectionResults[c++] = col; }
        }
        if (c == 0 && IsNetworkActive && NetworkManager.Singleton != null)
        {
            foreach (var kvp in NetworkManager.Singleton.ConnectedClients)
            {
                if (c >= detectionResults.Length) break;
                var po = kvp.Value.PlayerObject;
                if (po == null || Vector3.Distance(transform.position, po.transform.position) > sightRange) continue;
                var col = po.GetComponent<Collider>(); if (col != null) detectionResults[c++] = col;
            }
        }
        return c;
    }

    // ══════════════════════════════════════════════════════════
    //  CHANGE STATE
    // ══════════════════════════════════════════════════════════
    private void ChangeState(EnemyState newState)
    {
        // Kết thúc attack state — dọn hitbox
        if (CurrentStateValue == EnemyState.Attack && newState != EnemyState.Attack)
            DisableHitboxes();

        CurrentStateValue = newState;

        switch (newState)
        {
            case EnemyState.Chase:
                waitingAtWaypoint = false;
                if (AgentReady) { agent.isStopped = false; agent.speed = IsEnragedValue ? chaseRunSpeed * 1.5f : chaseRunSpeed; }
                break;

            case EnemyState.Stagger:
                if (AgentReady) agent.isStopped = true;
                SetSpeedNet(0f);
                if (staggerTimer <= 0) staggerTimer = 0.6f;
                break;

            case EnemyState.Attack:
                if (AgentReady) agent.isStopped = true;
                SetSpeedNet(0f);
                hasDealtDamage1 = false; hasDealtDamage2 = false;
                // Chọn loại tấn công
                int chosen;
                if (IsEnragedValue && Random.value < 0.4f) { chosen = 2; attackDuration = 1.8f; }
                else { chosen = isNextAttackLeft ? 0 : 1; isNextAttackLeft = !isNextAttackLeft; attackDuration = 1.1f; }
                if (!isStandaloneMode) attackType.Value = chosen;
                stateTimer = attackDuration;
                PlayAttackAnim(chosen);
                if (!isStandaloneMode) PlayAttackClientRpc(chosen);
                break;

            case EnemyState.Dead:
                Die();
                break;
        }
    }

    // ══════════════════════════════════════════════════════════
    //  ANIMATION HELPERS
    // ══════════════════════════════════════════════════════════
    /// <summary>Áp dụng Speed float lên Animator — Base Layer. 0=Idle, 0.5=Walk, 1=Run</summary>
    private void ApplySpeedAnim(float speed)
    {
        if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null) return;
        anim.SetFloat(speedParam, speed);
    }

    /// <summary>Set speed từ Server (hoặc standalone). Chỉ ghi khi giá trị thay đổi.</summary>
    private void SetSpeedNet(float val)
    {
        if (isStandaloneMode) ApplySpeedAnim(val);
        else if (IsServer && !Mathf.Approximately(netSpeed.Value, val)) netSpeed.Value = val;
    }

    private void PlayAttackAnim(int type)
    {
        if (anim == null) return;
        anim.ResetTrigger(atkLeftTrigger);
        anim.ResetTrigger(atkRightTrigger);
        anim.ResetTrigger(atkComboTrigger);
        if (type == 0)      anim.SetTrigger(atkLeftTrigger);
        else if (type == 1) anim.SetTrigger(atkRightTrigger);
        else                anim.SetTrigger(atkComboTrigger);
    }

    [ClientRpc] private void PlayAttackClientRpc(int type) { if (!IsServer) PlayAttackAnim(type); }

    private void PlayRoarAnim() { if (anim == null) return; anim.ResetTrigger(atkComboTrigger); anim.SetTrigger(atkComboTrigger); }
    [ClientRpc] private void PlayRoarClientRpc() => PlayRoarAnim();

    // ══════════════════════════════════════════════════════════
    //  DAMAGE
    // ══════════════════════════════════════════════════════════
    private void DealConeDamage(float damage, float range, float angle, float knockback)
    {
        int num = Physics.OverlapSphereNonAlloc(transform.position, range, damageResults, playerLayer);
        if (num == 0)
        {
            int c = 0;
            foreach (var p in GameObject.FindGameObjectsWithTag("Player"))
            { if (c >= damageResults.Length) break; if (Vector3.Distance(transform.position, p.transform.position) <= range) { var col = p.GetComponent<Collider>(); if (col != null) damageResults[c++] = col; } }
            num = c;
        }
        Vector3 ep = eyeTransform != null ? eyeTransform.position : transform.position + Vector3.up * 1.5f;
        
        System.Collections.Generic.HashSet<IPlayerHUDTarget> hitTargets = new System.Collections.Generic.HashSet<IPlayerHUDTarget>();
        
        for (int i = 0; i < num; i++)
        {
            if (damageResults[i] == null) continue;
            Transform pl = damageResults[i].transform;
            
            IPlayerHUDTarget target = pl.GetComponentInParent<IPlayerHUDTarget>();
            if (target == null) continue;
            if (hitTargets.Contains(target)) continue;
            hitTargets.Add(target);
            
            Vector3 dir = (pl.position - transform.position).normalized;
            if (Vector3.Angle(transform.forward, dir) <= angle / 2f && !Physics.Raycast(ep, dir, Vector3.Distance(transform.position, pl.position), obstacleLayer))
            { Vector3 kb = dir; kb.y = 0; EnemyDamageHelper.DealDamage(pl, damage, kb.normalized * knockback); }
        }
    }

    public void TakeDamage(float damage)
    {
        if (!isStandaloneMode && (!IsServer || CurrentStateValue == EnemyState.Dead)) return;
        if (isStandaloneMode && CurrentStateValue == EnemyState.Dead) return;
        CurrentHealthValue -= damage;
        if (isStandaloneMode) EnemyDamageEffectHelper.PlayDamageEffects(gameObject, damage);
        if (!isStandaloneMode) hitCounter.Value++;
        else if (anim != null) anim.SetTrigger(hitTrigger);
        if (CurrentHealthValue <= 0) { ChangeState(EnemyState.Dead); return; }
        if (CurrentHealthValue <= maxHealth * 0.5f && !IsEnragedValue)
        {
            IsEnragedValue = true; staggerTimer = 1.2f;
            ChangeState(EnemyState.Stagger);
            if (!isStandaloneMode) PlayRoarClientRpc(); else PlayRoarAnim();
            return;
        }
        float now = Time.time; if (now - lastDamageTime > 3f) recentHitCount = 0; recentHitCount++; lastDamageTime = now;
        if ((damage >= 25f || recentHitCount >= 3) && CurrentStateValue != EnemyState.Stagger)
        { recentHitCount = 0; staggerTimer = 0.6f; ChangeState(EnemyState.Stagger); }
        else if (!isDodging && Random.value < 0.3f && CurrentStateValue == EnemyState.Chase) ExecuteDodge();
    }

    private void ExecuteDodge()
    {
        if (targetPlayer == null || !AgentReady) return;
        Vector3 tp = (targetPlayer.position - transform.position).normalized;
        Vector3 perp = new Vector3(-tp.z, 0, tp.x) * (Random.value < 0.5f ? 1f : -1f);
        NavMeshHit h;
        if (NavMesh.SamplePosition(transform.position + perp * 3f, out h, 3f, NavMesh.AllAreas))
        { isDodging = true; dodgeTimer = 0.35f; agent.isStopped = false; agent.speed = IsEnragedValue ? 14f : 10f; agent.SetDestination(h.position); }
    }

    // ══════════════════════════════════════════════════════════
    //  DEATH & DROPS
    // ══════════════════════════════════════════════════════════
    private void Die()
    {
        if (AgentReady) agent.isStopped = true;
        SetSpeedNet(0f);
        ApplyLocalDeathEffects();
        DropExperience(); DropItems();
        Invoke(nameof(DespawnEnemy), 2.5f);
    }

    private void DropExperience()
    {
        if (expGemPrefab == null) return;
        string uid = System.Guid.NewGuid().ToString();
        Transform sp = expDropPoint != null ? expDropPoint : transform;
        float d = 0.6f;
        Vector3[] pos = { sp.position + new Vector3(d,0.1f,d), sp.position + new Vector3(-d,0.1f,d), sp.position + new Vector3(d,0.1f,-d), sp.position + new Vector3(-d,0.1f,-d) };
        bool na = IsNetworkActive;
        foreach (var p in pos)
        {
            if (isStandaloneMode || !na) 
            { 
                var g = Instantiate(expGemPrefab, p, Quaternion.identity); 
                g.SetActive(true); 
                var gem = g.GetComponent<ExperienceGem>(); 
                if (gem != null) { gem.expAmount = expDropAmount; gem.DropGroupId = uid; } 
            }
            else if (IsServer) 
            { 
                var g = Instantiate(expGemPrefab, p, Quaternion.identity); 
                g.SetActive(true); 
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
        Transform sp = expDropPoint != null ? expDropPoint : transform;
        Vector3 pv = sp.position + Vector3.up * 0.2f; bool na = IsNetworkActive;
        if (isStandaloneMode || !na) Instantiate(repairItemPrefab, pv, Quaternion.identity);
        else if (IsServer) { var g = Instantiate(repairItemPrefab, pv, Quaternion.identity); var no = g.GetComponent<NetworkObject>(); if (no != null) no.Spawn(); }
    }

    private void DespawnEnemy()
    { if (isStandaloneMode) { Destroy(gameObject); return; } if (IsServer && IsSpawned) GetComponent<NetworkObject>().Despawn(); }

    // ══════════════════════════════════════════════════════════
    //  HITBOXES (Animation Events)
    // ══════════════════════════════════════════════════════════
    private void DisableHitboxes()
    {
        if (hammerHitbox      != null) hammerHitbox.SetActive(false);
        if (hammerHitboxLeft  != null) hammerHitboxLeft.SetActive(false);
        if (hammerHitboxRight != null) hammerHitboxRight.SetActive(false);
    }
    public void EnableWeaponHitbox()
    {
        if (hammerHitbox != null) hammerHitbox.SetActive(true);
        bool auth = isStandaloneMode || (IsNetworkActive && IsServer);
        if (auth && !hasDealtDamage1)
        {
            hasDealtDamage1 = true;
            DealConeDamage(15f, attackRange + 0.5f, 80f, 8f);
        }
    }
    public void DisableWeaponHitbox()  { DisableHitboxes(); hasDealtDamage1 = false; hasDealtDamage2 = false; }
    public void EnableLeftWeaponHitbox()
    {
        if (hammerHitboxLeft != null) hammerHitboxLeft.SetActive(true);
        bool auth = isStandaloneMode || (IsNetworkActive && IsServer);
        if (auth && !hasDealtDamage1)
        {
            hasDealtDamage1 = true;
            int aType = isStandaloneMode ? 0 : attackType.Value;
            if (aType == 2)
            {
                DealConeDamage(10f, attackRange + 0.5f, 80f, 6f);
            }
            else
            {
                DealConeDamage(15f, attackRange + 0.5f, 80f, 8f);
            }
        }
    }
    public void DisableLeftWeaponHitbox()  { if (hammerHitboxLeft  != null) hammerHitboxLeft.SetActive(false); hasDealtDamage1 = false; }
    public void EnableRightWeaponHitbox()
    {
        if (hammerHitboxRight != null) hammerHitboxRight.SetActive(true);
        bool auth = isStandaloneMode || (IsNetworkActive && IsServer);
        if (auth)
        {
            int aType = isStandaloneMode ? 0 : attackType.Value;
            if (aType == 2)
            {
                if (!hasDealtDamage2)
                {
                    hasDealtDamage2 = true;
                    DealConeDamage(25f, attackRange + 1.2f, 95f, 15f);
                }
            }
            else
            {
                if (!hasDealtDamage1)
                {
                    hasDealtDamage1 = true;
                    DealConeDamage(15f, attackRange + 0.5f, 80f, 8f);
                }
            }
        }
    }
    public void DisableRightWeaponHitbox() { if (hammerHitboxRight != null) hammerHitboxRight.SetActive(false); hasDealtDamage2 = false; }

    // ══════════════════════════════════════════════════════════
    //  UTILITIES
    // ══════════════════════════════════════════════════════════
    private void SnapToNavMesh()
    {
        if (agent == null || !agent.isActiveAndEnabled) return;
        if (!agent.isOnNavMesh)
        { NavMeshHit h; if (NavMesh.SamplePosition(transform.position, out h, 10f, NavMesh.AllAreas)) agent.Warp(h.position); }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 eye = eyeTransform != null ? eyeTransform.position : transform.position + Vector3.up * 1.5f;
        Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(eye, sightRange);
        Vector3 l = Quaternion.AngleAxis(-fieldOfView / 2f, Vector3.up) * transform.forward;
        Vector3 r = Quaternion.AngleAxis( fieldOfView / 2f, Vector3.up) * transform.forward;
        Gizmos.DrawRay(eye, l * sightRange); Gizmos.DrawRay(eye, r * sightRange);
        Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}