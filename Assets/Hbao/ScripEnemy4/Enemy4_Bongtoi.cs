using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Enemy 4 - Bóng Tối
/// Animator Float "Speed": 0=Idle, 0.5=Walk (patrol), 1=Run (chase)
/// Attack triggers: qual44attack, qual4combo
/// </summary>
public class Enemy4_Bongtoi : NetworkBehaviour
{
    public enum EnemyState { Patrol, Chase, Stagger, Attack, Dead }

    [Header("Health")]
    public float maxHealth = 120f;
    public NetworkVariable<float> currentHealth = new NetworkVariable<float>(120f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
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
    public float ActualCurrentHealth => isStandaloneMode ? localHealth : currentHealth.Value;

    [Header("Components")]
    public NavMeshAgent agent;
    public Animator anim;
    public Transform eyeTransform;

    [Header("Hitboxes")]
    public GameObject clawHitbox;
    public GameObject weaponHitbox;

    [Header("Patrol Waypoints (3 điểm hình tam giác)")]
    public Transform[] waypoints = new Transform[3];

    [Header("Drops")]
    public GameObject expGemPrefab;
    public float expDropAmount = 30f;
    public Transform expDropPoint;
    public GameObject repairItemPrefab;
    [Range(0f, 1f)] public float repairItemDropChance = 0.35f;

    [Header("AI Settings")]
    public float sightRange = 13f;
    public float fieldOfView = 100f;
    public float attackRange = 2.2f;
    public float patrolWalkSpeed = 2.5f;
    public float chaseRunSpeed   = 5.5f;
    public float patrolWaitMin = 1f;
    public float patrolWaitMax = 4f;
    [Range(0f, 1f)] public float patrolMoveChance = 0.7f;

    [Header("Layers")]
    public LayerMask playerLayer;
    public LayerMask obstacleLayer;

    [Header("Animator Parameter Names")]
    [Tooltip("Float: 0=Idle, 0.5=Walk, 1=Run — Base Layer")]
    public string speedParam   = "Speed";        // Float  — Base Layer
    public string hitTrigger   = "Qual4Anhit";   // Trigger — Anhit Layer
    public string dieTrigger   = "Qual4Dle";     // Trigger — Base Layer
    public string atk1Trigger  = "qual44attack"; // Trigger — Attack Layer
    public string atk2Trigger  = "qual4combo";   // Trigger — Attack Layer

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
    private bool wasEnraged;
    private MaterialPropertyBlock propBlock;
    public Renderer[] modelRenderers;

    private readonly Collider[] detectionResults = new Collider[8];
    private readonly Collider[] damageResults    = new Collider[8];
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
        SnapToNavMesh(); DisableHitboxes();
        ApplySpeedAnim(0f); GoToNextWaypoint();
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
        if (IsServer) { currentHealth.Value = maxHealth; SnapToNavMesh(); DisableHitboxes(); GoToNextWaypoint(); }
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
        DisableHitboxes();
        if (anim != null)
        {
            anim.ResetTrigger(atk1Trigger);
            anim.ResetTrigger(atk2Trigger);
            anim.ResetTrigger(hitTrigger);
            anim.ResetTrigger(dieTrigger);

            for (int i = 1; i < anim.layerCount; i++)
            {
                anim.SetLayerWeight(i, 0f);
            }
            anim.Play("Quai4Die", 0, 0f);
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
        if (enraged) { wasEnraged = true; float pp = Mathf.PingPong(Time.time * 3f, 1f); Color c = Color.Lerp(Color.white, new Color(0.5f, 0f, 0.8f), pp); foreach (var r in modelRenderers) { if (r != null) { r.GetPropertyBlock(propBlock); propBlock.SetColor("_Color", c); r.SetPropertyBlock(propBlock); } } }
        else if (wasEnraged) { wasEnraged = false; foreach (var r in modelRenderers) { if (r != null) { r.GetPropertyBlock(propBlock); propBlock.SetColor("_Color", Color.white); r.SetPropertyBlock(propBlock); } } }
    }

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
        
        if (dist <= attackRange && attackCooldownTimer <= 0)
        {
            if (AgentReady) agent.isStopped = true;
            SetSpeedNet(0f);
            ChangeState(EnemyState.Attack);
            return;
        }
        
        bool enraged = CurrentHealthValue < maxHealth * 0.5f;
        if (AgentReady)
        {
            agent.isStopped = false;
            agent.speed = enraged ? chaseRunSpeed * 1.3f : chaseRunSpeed;
            agent.SetDestination(targetPlayer.position);
        }
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
        if (elapsed < attackDuration * 0.35f) { Vector3 ld = (targetPlayer.position - transform.position); ld.y = 0; if (ld.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(ld), Time.deltaTime * 18f); }
        if (stateTimer <= 0) EndAttack();
    }

    private void EndAttack()
    {
        attackCooldownTimer = (CurrentHealthValue < maxHealth * 0.5f) ? 0.3f : 0.6f;
        DisableHitboxes(); detectionTimer = 0f;
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
        if (CurrentStateValue == EnemyState.Attack && newState != EnemyState.Attack) { DisableHitboxes(); }
        CurrentStateValue = newState;
        switch (newState)
        {
            case EnemyState.Chase:   waitingAtWaypoint = false; if (AgentReady) { agent.isStopped = false; agent.speed = chaseRunSpeed; } break;
            case EnemyState.Stagger: if (AgentReady) agent.isStopped = true; SetSpeedNet(0f); if (staggerTimer <= 0) staggerTimer = 0.55f; break;
            case EnemyState.Attack:
                if (AgentReady) agent.isStopped = true; SetSpeedNet(0f);
                hasDealtDamage = false; float hp = CurrentHealthValue / maxHealth;
                int chosen; float dur; if (hp < 0.5f) { chosen = 1; dur = 1.7f; } else { chosen = 0; dur = 1.0f; }
                if (!isStandaloneMode) attackType.Value = chosen;
                attackDuration = dur; stateTimer = dur;
                PlayAttackAnimLocal(chosen);
                if (!isStandaloneMode) PlayAttackClientRpc(chosen);
                break;
            case EnemyState.Dead: Die(); break;
        }
    }

    private void ApplySpeedAnim(float speed) { if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null) return; anim.SetFloat(speedParam, speed); }
    private void SetSpeedNet(float val) { if (isStandaloneMode) ApplySpeedAnim(val); else if (IsServer && !Mathf.Approximately(netSpeed.Value, val)) netSpeed.Value = val; }
    private void PlayAttackAnimLocal(int type) { if (anim == null) return; anim.ResetTrigger(atk1Trigger); anim.ResetTrigger(atk2Trigger); if (type == 0) anim.SetTrigger(atk1Trigger); else anim.SetTrigger(atk2Trigger); }
    [ClientRpc] private void PlayAttackClientRpc(int type) { if (!IsServer) PlayAttackAnimLocal(type); }

    private void DealConeDamage(float damage, float range, float angle, float knockback)
    {
        int num = Physics.OverlapSphereNonAlloc(transform.position, range, damageResults, playerLayer);
        if (num == 0) { int c = 0; foreach (var p in GameObject.FindGameObjectsWithTag("Player")) { if (c >= damageResults.Length) break; if (Vector3.Distance(transform.position, p.transform.position) <= range) { var col = p.GetComponent<Collider>(); if (col != null) damageResults[c++] = col; } } num = c; }
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
        if (!isStandaloneMode) hitCounter.Value++; else if (anim != null) anim.SetTrigger(hitTrigger);
        if (CurrentHealthValue <= 0) { ChangeState(EnemyState.Dead); return; }
        float now = Time.time; if (now - lastDamageTime > 3f) recentHitCount = 0; recentHitCount++; lastDamageTime = now;
        if ((damage >= 25f || recentHitCount >= 3) && CurrentStateValue != EnemyState.Stagger) { recentHitCount = 0; staggerTimer = 0.55f; ChangeState(EnemyState.Stagger); }
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
        Debug.Log($"[Enemy4_Bongtoi] Bị choáng (Skill Q Arthur) trong {duration}s");
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
    private void DisableHitboxes() { if (clawHitbox != null) clawHitbox.SetActive(false); if (weaponHitbox != null) weaponHitbox.SetActive(false); }
    public void EnableClawHitbox()
    {
        if (clawHitbox != null) clawHitbox.SetActive(true);
        bool auth = isStandaloneMode || (IsNetworkActive && IsServer);
        if (auth && !hasDealtDamage)
        {
            hasDealtDamage = true;
            DealConeDamage(15f, attackRange + 0.5f, 80f, 2.0f);
        }
    }
    public void DisableClawHitbox()  { if (clawHitbox   != null) clawHitbox.SetActive(false); hasDealtDamage = false; }
    public void EnableWeaponHitbox()
    {
        if (weaponHitbox != null) weaponHitbox.SetActive(true);
        bool auth = isStandaloneMode || (IsNetworkActive && IsServer);
        if (auth && !hasDealtDamage)
        {
            hasDealtDamage = true;
            DealConeDamage(20f, attackRange + 1f, 90f, 2.5f);
        }
    }
    public void DisableWeaponHitbox(){ if (weaponHitbox != null) weaponHitbox.SetActive(false); hasDealtDamage = false; }
}