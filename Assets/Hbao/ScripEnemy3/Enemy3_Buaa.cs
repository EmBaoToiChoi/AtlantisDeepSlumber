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
    public enum EnemyState { Patrol, Chase, Stagger, Attack, Dead }

    [Header("Health")]
    public float maxHealth = 150f;
    public NetworkVariable<float> currentHealth = new NetworkVariable<float>(150f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
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

    [Header("Components")]
    public NavMeshAgent agent;
    public Animator anim;
    public Transform eyeTransform;

    [Header("Melee Hitbox")]
    public GameObject hammerHitbox;

    [Header("Patrol Waypoints (3 điểm hình tam giác)")]
    public Transform[] waypoints = new Transform[3];

    [Header("Drops")]
    public GameObject expGemPrefab;
    public float expDropAmount = 35f;
    public Transform expDropPoint;
    public GameObject repairItemPrefab;
    [Range(0f, 1f)] public float repairItemDropChance = 0.35f;

    [Header("AI Settings")]
    public float sightRange = 14f;
    public float fieldOfView = 95f;
    public float attackRange = 2.5f;
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
    private float attackCooldownTimer;
    private float attackDuration;
    private float stateTimer;
    private bool hasDealtDamage1, hasDealtDamage2;
    private float lastDamageTime;
    private int recentHitCount;
    private bool isFrenzied, isEnraged;
    public Renderer[] modelRenderers;

    private readonly Collider[] detectionResults = new Collider[8];
    private readonly Collider[] damageResults    = new Collider[8];
    private bool AgentReady => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;

    private void Awake()
    {
        if (anim == null) anim = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);
        var na = GetComponent<Unity.Netcode.Components.NetworkAnimator>();
        if (na != null) { if (anim == null || anim.runtimeAnimatorController == null) na.enabled = false; else na.Animator = anim; }
    }

    private void Start() { if (!IsNetworkActive) { isStandaloneMode = true; InitStandalone(); } }

    private void InitStandalone()
    {
        localHealth = maxHealth; localState = EnemyState.Patrol;
        SnapToNavMesh(); if (hammerHitbox != null) hammerHitbox.SetActive(false);
        ApplySpeedAnim(0f); GoToNextWaypoint();
    }

    public override void OnNetworkSpawn()
    {
        isStandaloneMode = false;
        var nt = GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (nt != null) { nt.PositionThreshold = 0.001f; nt.RotAngleThreshold = 0.01f; nt.ScaleThreshold = 0.01f; }
        netSpeed.OnValueChanged       += (_, v) => ApplySpeedAnim(v);
        hitCounter.OnValueChanged     += (_, _) => { if (anim != null) anim.SetTrigger(hitTrigger); };
        ApplySpeedAnim(netSpeed.Value);
        if (IsServer) { currentHealth.Value = maxHealth; SnapToNavMesh(); if (hammerHitbox != null) hammerHitbox.SetActive(false); GoToNextWaypoint(); }
        else { if (agent != null) agent.enabled = false; }
    }

    private void Update()
    {
        UpdateEnrageVisuals();
        bool aiAuth = isStandaloneMode || (IsNetworkActive && IsServer);
        if (!aiAuth) return;
        if (AgentReady && !agent.isOnNavMesh) SnapToNavMesh();
        if (attackCooldownTimer > 0) attackCooldownTimer -= Time.deltaTime;
        float hp = CurrentHealthValue / maxHealth;
        isFrenzied = hp <= 0.3f; isEnraged = hp <= 0.6f && !isFrenzied;
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
        if (modelRenderers == null || modelRenderers.Length == 0) return;
        if (isFrenzied) { float pp = Mathf.PingPong(Time.time * 4f, 1f); Color c = Color.Lerp(Color.white, new Color(1f, 0.25f, 0f), pp); foreach (var r in modelRenderers) { if (r != null && r.material != null) r.material.color = c; } }
        else if (isEnraged) { Color c = new Color(1f, 0.75f, 0.5f); foreach (var r in modelRenderers) { if (r != null && r.material != null) r.material.color = c; } }
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
        SimplePlayerTest ps = targetPlayer.GetComponentInParent<SimplePlayerTest>();
        if (ps != null && ps.CurrentHealth <= 0) { targetPlayer = null; ReturnToPatrol(); return; }
        Vector3 ld = (targetPlayer.position - transform.position); ld.y = 0;
        if (ld.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(ld), Time.deltaTime * 15f);
        float dist = Vector3.Distance(transform.position, targetPlayer.position);
        if (dist <= attackRange) { if (AgentReady) agent.isStopped = true; SetSpeedNet(0f); if (attackCooldownTimer <= 0) ChangeState(EnemyState.Attack); return; }
        float spd = isFrenzied ? chaseRunSpeed * 1.5f : (isEnraged ? chaseRunSpeed * 1.2f : chaseRunSpeed);
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
        int aType = isStandaloneMode ? 0 : attackType.Value;
        if (elapsed < attackDuration * 0.35f) { Vector3 ld = (targetPlayer.position - transform.position); ld.y = 0; if (ld.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(ld), Time.deltaTime * 18f); }
        if (aType == 2) { if (elapsed >= 0.6f && !hasDealtDamage1) { hasDealtDamage1 = true; DealConeDamage(30f, attackRange + 1.5f, 110f, 18f); } }
        else if (aType == 1) { if (elapsed >= 0.4f && !hasDealtDamage1) { hasDealtDamage1 = true; DealConeDamage(15f, attackRange + 1f, 90f, 8f); } if (elapsed >= 1.0f && !hasDealtDamage2) { hasDealtDamage2 = true; DealConeDamage(20f, attackRange + 1f, 90f, 10f); } }
        else { if (elapsed >= 0.5f && !hasDealtDamage1) { hasDealtDamage1 = true; DealConeDamage(18f, attackRange + 0.5f, 80f, 8f); } }
        if (stateTimer <= 0) EndAttack();
    }

    private void EndAttack()
    {
        attackCooldownTimer = (CurrentHealthValue < maxHealth * 0.5f) ? 0.3f : 0.7f;
        if (hammerHitbox != null) hammerHitbox.SetActive(false); detectionTimer = 0f;
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
        for (int i = 0; i < num; i++) { if (detectionResults[i] == null) continue; Transform pt = detectionResults[i].transform; SimplePlayerTest ps = pt.GetComponentInParent<SimplePlayerTest>(); if (ps != null && ps.CurrentHealth <= 0) continue; Vector3 center = pt.position + Vector3.up; float d = Vector3.Distance(ep, center); Vector3 dir = (center - ep).normalized; bool inFOV = Vector3.Angle(transform.forward, dir) < fieldOfView / 2f; if ((inFOV || pt == targetPlayer) && !Physics.Raycast(ep, dir, d, obstacleLayer)) { if (d < minD) { minD = d; closest = pt; found = true; } } }
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
        if (CurrentStateValue == EnemyState.Attack && newState != EnemyState.Attack) { if (hammerHitbox != null) hammerHitbox.SetActive(false); }
        CurrentStateValue = newState;
        switch (newState)
        {
            case EnemyState.Chase:   waitingAtWaypoint = false; if (AgentReady) { agent.isStopped = false; agent.speed = chaseRunSpeed; } break;
            case EnemyState.Stagger: if (AgentReady) agent.isStopped = true; SetSpeedNet(0f); if (staggerTimer <= 0) staggerTimer = 0.55f; break;
            case EnemyState.Attack:
                if (AgentReady) agent.isStopped = true; SetSpeedNet(0f);
                hasDealtDamage1 = false; hasDealtDamage2 = false;
                float hp = CurrentHealthValue / maxHealth; int chosen; float dur;
                if (hp <= 0.4f) { chosen = 2; dur = 1.9f; } else if (hp <= 0.7f) { chosen = 1; dur = 1.6f; } else { chosen = 0; dur = 1.0f; }
                if (!isStandaloneMode) attackType.Value = chosen;
                attackDuration = dur; stateTimer = dur;
                if (!isStandaloneMode) PlayAttackClientRpc(chosen); else PlayAttackAnimLocal(chosen);
                break;
            case EnemyState.Dead: Die(); break;
        }
    }

    private void ApplySpeedAnim(float speed) { if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null) return; anim.SetFloat(speedParam, speed); }
    private void SetSpeedNet(float val) { if (isStandaloneMode) ApplySpeedAnim(val); else if (IsServer && !Mathf.Approximately(netSpeed.Value, val)) netSpeed.Value = val; }
    private void PlayAttackAnimLocal(int type) { if (anim == null) return; anim.ResetTrigger(atk1Trigger); anim.ResetTrigger(atk2Trigger); anim.ResetTrigger(atk3Trigger); if (type == 0) anim.SetTrigger(atk1Trigger); else if (type == 1) anim.SetTrigger(atk2Trigger); else anim.SetTrigger(atk3Trigger); }
    [ClientRpc] private void PlayAttackClientRpc(int type) => PlayAttackAnimLocal(type);

    private void DealConeDamage(float damage, float range, float angle, float knockback)
    {
        int num = Physics.OverlapSphereNonAlloc(transform.position, range, damageResults, playerLayer);
        if (num == 0) { int c = 0; foreach (var p in GameObject.FindGameObjectsWithTag("Player")) { if (c >= damageResults.Length) break; if (Vector3.Distance(transform.position, p.transform.position) <= range) { var col = p.GetComponent<Collider>(); if (col != null) damageResults[c++] = col; } } num = c; }
        Vector3 ep = eyeTransform != null ? eyeTransform.position : transform.position + Vector3.up * 1.5f;
        for (int i = 0; i < num; i++) { if (damageResults[i] == null) continue; Transform pl = damageResults[i].transform; Vector3 dir = (pl.position - transform.position).normalized; if (Vector3.Angle(transform.forward, dir) <= angle / 2f && !Physics.Raycast(ep, dir, Vector3.Distance(transform.position, pl.position), obstacleLayer)) { var ps = pl.GetComponentInParent<SimplePlayerTest>(); if (ps != null) { ps.TakeDamage(damage); Vector3 kb = dir; kb.y = 0; ps.ApplyKnockback(kb.normalized * knockback); } } }
    }

    public void TakeDamage(float damage)
    {
        if (!isStandaloneMode && (!IsServer || CurrentStateValue == EnemyState.Dead)) return;
        if (isStandaloneMode && CurrentStateValue == EnemyState.Dead) return;
        CurrentHealthValue -= damage;
        if (!isStandaloneMode) hitCounter.Value++; else if (anim != null) anim.SetTrigger(hitTrigger);
        if (CurrentHealthValue <= 0) { ChangeState(EnemyState.Dead); return; }
        float now = Time.time; if (now - lastDamageTime > 3f) recentHitCount = 0; recentHitCount++; lastDamageTime = now;
        if ((damage >= 30f || recentHitCount >= 3) && CurrentStateValue != EnemyState.Stagger) { recentHitCount = 0; staggerTimer = 0.55f; ChangeState(EnemyState.Stagger); }
    }

    private void Die()
    {
        if (AgentReady) agent.isStopped = true; SetSpeedNet(0f);
        if (anim != null) { anim.ResetTrigger(dieTrigger); anim.SetTrigger(dieTrigger); }
        if (hammerHitbox != null) hammerHitbox.SetActive(false);
        DropExperience(); DropItems(); Invoke(nameof(DespawnEnemy), 2.5f);
    }

    private void DropExperience()
    {
        if (expGemPrefab == null) return;
        string uid = System.Guid.NewGuid().ToString(); Transform sp = expDropPoint != null ? expDropPoint : transform; float d = 0.6f;
        Vector3[] pos = { sp.position + new Vector3(d,0.1f,d), sp.position + new Vector3(-d,0.1f,d), sp.position + new Vector3(d,0.1f,-d), sp.position + new Vector3(-d,0.1f,-d) };
        bool na = IsNetworkActive;
        foreach (var p in pos) { if (isStandaloneMode || !na) { var g = Instantiate(expGemPrefab, p, Quaternion.identity); var gem = g.GetComponent<ExperienceGem>(); if (gem != null) { gem.expAmount = expDropAmount; gem.DropGroupId = uid; } } else if (IsServer) { var g = Instantiate(expGemPrefab, p, Quaternion.identity); var gem = g.GetComponent<ExperienceGem>(); if (gem != null) { gem.expAmount = expDropAmount; gem.DropGroupId = uid; } var no = g.GetComponent<NetworkObject>(); if (no != null) no.Spawn(); } }
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
    public void EnableWeaponHitbox()  { if (hammerHitbox != null) hammerHitbox.SetActive(true); }
    public void DisableWeaponHitbox() { if (hammerHitbox != null) hammerHitbox.SetActive(false); }
}