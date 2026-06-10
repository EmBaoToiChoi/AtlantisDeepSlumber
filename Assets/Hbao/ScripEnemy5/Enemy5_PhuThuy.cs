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
    public float ActualCurrentHealth => isStandaloneMode ? localHealth : currentHealth.Value;

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
    public float spellDamage = 20f;

    [Header("Drops")]
    public GameObject expGemPrefab;
    public float expDropAmount = 25f;
    public Transform expDropPoint;
    public GameObject repairItemPrefab;
    [Range(0f, 1f)] public float repairItemDropChance = 0.3f;

    [Header("AI Settings")]
    public float sightRange     = 18f;
    public float fieldOfView    = 110f;
    public float maxAttackRange = 13f;
    public float minAttackRange = 5.5f;  // Kiting: thoái lui nếu Player gần hơn
    public float patrolWalkSpeed = 2.5f;
    public float chaseRunSpeed   = 3.8f;
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

    private readonly Collider[] detectionResults = new Collider[8];
    private bool AgentReady => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;

    private void Awake()
    {
        propBlock = new MaterialPropertyBlock();
        if (anim == null) anim = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);
        var na = GetComponent<Unity.Netcode.Components.NetworkAnimator>();
        if (na != null) { if (anim == null || anim.runtimeAnimatorController == null) na.enabled = false; else na.Animator = anim; }
    }

    private void Start() { if (!IsNetworkActive) { isStandaloneMode = true; InitStandalone(); } }

    private void InitStandalone()
    {
        localHealth = maxHealth; localState = EnemyState.Patrol;
        SnapToNavMesh(); ApplySpeedAnim(0f); GoToNextWaypoint();
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
        if (IsServer) { currentHealth.Value = maxHealth; SnapToNavMesh(); GoToNextWaypoint(); }
        else { if (agent != null) agent.enabled = false; }
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
        if (AgentReady && !agent.isOnNavMesh) SnapToNavMesh();
        if (attackCooldownTimer > 0) attackCooldownTimer -= Time.deltaTime;
        if (blinkTimer > 0) { blinkTimer -= Time.deltaTime; if (blinkTimer <= 0) isBlinking = false; }
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

    private void UpdateEnrageVisuals()
    {
        if (modelRenderers == null || modelRenderers.Length == 0 || propBlock == null) return;
        bool enraged = CurrentHealthValue < maxHealth * 0.5f;
        if (enraged) { wasEnraged = true; float pp = Mathf.PingPong(Time.time * 3f, 1f); Color c = Color.Lerp(Color.white, new Color(0f, 0.6f, 1f), pp); foreach (var r in modelRenderers) { if (r != null) { r.GetPropertyBlock(propBlock); propBlock.SetColor("_Color", c); r.SetPropertyBlock(propBlock); } } }
        else if (wasEnraged) { wasEnraged = false; foreach (var r in modelRenderers) { if (r != null) { r.GetPropertyBlock(propBlock); propBlock.SetColor("_Color", Color.white); r.SetPropertyBlock(propBlock); } } }
    }

    // ── Patrol (Walk) ──
    private void GoToNextWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0) { SetSpeedNet(0f); return; }
        if (waypoints.Length > 1) { int n; do { n = Random.Range(0, waypoints.Length); } while (n == currentWaypointIndex); currentWaypointIndex = n; }
        else currentWaypointIndex = 0;
        waitingAtWaypoint = false;
        if (waypoints[currentWaypointIndex] == null) { SetSpeedNet(0f); return; }
        if (AgentReady) { agent.isStopped = false; agent.speed = patrolWalkSpeed; agent.SetDestination(waypoints[currentWaypointIndex].position); }
        SetSpeedNet(0.5f);
    }

    private void HandlePatrol()
    {
        if (waypoints == null || waypoints.Length == 0) { SetSpeedNet(0f); return; }
        if (waitingAtWaypoint) { SetSpeedNet(0f); waypointWaitTimer -= Time.deltaTime; if (waypointWaitTimer <= 0) { if (Random.value <= patrolMoveChance) GoToNextWaypoint(); else waypointWaitTimer = Random.Range(patrolWaitMin, patrolWaitMax); } }
        else { SetSpeedNet(AgentReady && agent.velocity.magnitude > 0.15f ? 0.5f : 0f); if (AgentReady && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.3f) { agent.isStopped = true; SetSpeedNet(0f); waitingAtWaypoint = true; waypointWaitTimer = Random.Range(patrolWaitMin, patrolWaitMax); } }
    }

    // ── Chase (Kiting) ──
    private void HandleChase()
    {
        if (targetPlayer == null) { ReturnToPatrol(); return; }
        IPlayerHUDTarget ps = targetPlayer.GetComponentInParent<IPlayerHUDTarget>();
        Skeleton sk = targetPlayer.GetComponentInParent<Skeleton>();
        bool isTargetDead = (ps != null && (ps.CurrentHealth <= 0 || ps.IsInvisible)) || (sk != null && sk.CurrentHealthValue <= 0);
        if ((ps == null && sk == null) || isTargetDead) { targetPlayer = null; ReturnToPatrol(); return; }
        Vector3 ld = (targetPlayer.position - transform.position); ld.y = 0;
        if (ld.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(ld), Time.deltaTime * 15f);
        Vector3 flatEnemy = transform.position; flatEnemy.y = 0;
        Vector3 flatPlayer = targetPlayer.position; flatPlayer.y = 0;
        float dist = Vector3.Distance(flatEnemy, flatPlayer);
        float spd = CurrentHealthValue < maxHealth * 0.5f ? chaseRunSpeed * 1.3f : chaseRunSpeed;

        // A. QUÁ GẦN: Thoái lui kiting
        if (dist < minAttackRange)
        {
            Vector3 away = (transform.position - targetPlayer.position).normalized;
            Vector3 rp = transform.position + away * 4.5f;
            NavMeshHit h;
            if (NavMesh.SamplePosition(rp, out h, 4.5f, NavMesh.AllAreas)) { if (AgentReady) { agent.isStopped = false; agent.speed = spd + 1.5f; agent.SetDestination(h.position); } }
            else { Vector3 perp = new Vector3(-away.z, 0, away.x); Vector3 alt = transform.position + perp * (Random.value < 0.5f ? 4f : -4f); NavMeshHit h2; if (NavMesh.SamplePosition(alt, out h2, 4f, NavMesh.AllAreas) && AgentReady) agent.SetDestination(h2.position); }
            SetSpeedNet(AgentReady && agent.velocity.magnitude > 0.2f ? 1f : 0f);
            return;
        }

        // B. TẦM LÝ TƯỞNG: Đứng tấn công
        if (dist >= minAttackRange && dist <= maxAttackRange)
        { if (AgentReady) agent.isStopped = true; SetSpeedNet(0f); if (attackCooldownTimer <= 0) ChangeState(EnemyState.Attack); return; }

        // C. QUÁ XA: Tiến lại
        if (AgentReady) { agent.isStopped = false; agent.speed = spd; agent.SetDestination(targetPlayer.position); }
        SetSpeedNet(AgentReady && agent.velocity.magnitude > 0.2f ? 1f : 0f);
    }

    private void ReturnToPatrol() { targetPlayer = null; CurrentStateValue = EnemyState.Patrol; waitingAtWaypoint = false; waypointWaitTimer = 0f; GoToNextWaypoint(); }
    private void HandleStagger() { if (AgentReady) agent.isStopped = true; SetSpeedNet(0f); staggerTimer -= Time.deltaTime; if (staggerTimer <= 0) { if (targetPlayer != null) ChangeState(EnemyState.Chase); else ReturnToPatrol(); } }

    private void HandleAttack()
    {
        if (targetPlayer == null) { EndAttack(); return; }
        if (AgentReady) agent.isStopped = true; SetSpeedNet(0f);
        stateTimer -= Time.deltaTime;
        float elapsed = attackDuration - stateTimer;
        if (elapsed < attackDuration * 0.5f) { Vector3 ld = (targetPlayer.position - transform.position); ld.y = 0; if (ld.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(ld), Time.deltaTime * 18f); }
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
        int num = Physics.OverlapSphereNonAlloc(transform.position, sightRange, detectionResults, playerLayer);
        if (num == 0) num = FallbackDetect();
        bool found = false; Transform closest = null; float minD = float.MaxValue;
        Vector3 ep = eyeTransform != null ? eyeTransform.position : transform.position + Vector3.up * 1.5f;
        for (int i = 0; i < num; i++) { if (detectionResults[i] == null) continue; Transform pt = detectionResults[i].transform; IPlayerHUDTarget ps = pt.GetComponentInParent<IPlayerHUDTarget>(); Skeleton sk = pt.GetComponentInParent<Skeleton>(); bool isTargetDead = (ps != null && (ps.CurrentHealth <= 0 || ps.IsInvisible)) || (sk != null && sk.CurrentHealthValue <= 0); if ((ps == null && sk == null) || isTargetDead) continue; Vector3 center = pt.position + Vector3.up; float d = Vector3.Distance(ep, center); Vector3 dir = (center - ep).normalized; bool inFOV = Vector3.Angle(transform.forward, dir) < fieldOfView / 2f; if ((inFOV || pt == targetPlayer) && !Physics.Raycast(ep, dir, d, obstacleLayer)) { if (d < minD) { minD = d; closest = pt; found = true; } } }
        if (found && closest != null) { targetPlayer = closest; if (s != EnemyState.Chase) ChangeState(EnemyState.Chase); } else if (s == EnemyState.Chase) ReturnToPatrol();
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
        if (CurrentStateValue == EnemyState.Attack && newState != EnemyState.Attack)
        { /* Attack layer returns to empty state on its own */ }
        CurrentStateValue = newState;
        switch (newState)
        {
            case EnemyState.Chase:   waitingAtWaypoint = false; isBlinking = false; if (AgentReady) agent.isStopped = false; break;
            case EnemyState.Stagger: if (AgentReady) agent.isStopped = true; SetSpeedNet(0f); if (staggerTimer <= 0) staggerTimer = 0.5f; break;
            case EnemyState.Attack:
                if (AgentReady) agent.isStopped = true; SetSpeedNet(0f);
                hasCastSpell = false; stateTimer = attackDuration;
                PlayAttackAnimLocal();
                if (!isStandaloneMode) PlayAttackClientRpc();
                break;
            case EnemyState.Dead: Die(); break;
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
            if (spellBall != null)
            {
                spellBall.damage = spellDamage;
                spellBall.knockback = 5f;
            }
            var no = proj.GetComponent<NetworkObject>(); if (no != null && !isStandaloneMode) no.Spawn(true);
        }
        else
        {
            if (Physics.Raycast(spawnPt, dir, out RaycastHit hit, maxAttackRange + 2f))
            { EnemyDamageHelper.DealDamage(hit.collider.transform, spellDamage, dir * 5f); }
        }
    }

    public void TakeDamage(float damage)
    {
        if (!isStandaloneMode && (!IsServer || CurrentStateValue == EnemyState.Dead)) return;
        if (isStandaloneMode && CurrentStateValue == EnemyState.Dead) return;
        CurrentHealthValue -= damage;
        if (isStandaloneMode) EnemyDamageEffectHelper.PlayDamageEffects(gameObject, damage);
        if (!isStandaloneMode) hitCounter.Value++; else if (anim != null) anim.SetTrigger(hitTrigger);
        if (CurrentHealthValue <= 0) { ChangeState(EnemyState.Dead); return; }
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
}