using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class Enemy2_Zombie : NetworkBehaviour
{
    public enum EnemyState
    {
        Idle,
        Walk,
        Run,
        Search,
        Stagger,
        Attack,
        Dead
    }

    [Header("Health Settings")]
    public float maxHealth = 100f;
    public NetworkVariable<float> currentHealth = new NetworkVariable<float>(
        100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<EnemyState> currentState = new NetworkVariable<EnemyState>(
        EnemyState.Idle, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Advanced AI Sync")]
    public NetworkVariable<int> hitCounter = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> attackType = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // ------------------------------------------------------------------
    //  Standalone Fallback
    // ------------------------------------------------------------------
    private float localHealth;
    private EnemyState localState = EnemyState.Idle;
    private bool isStandaloneMode = false;

    private bool IsNetworkActive =>
        NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

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

    [Header("Components")]
    public NavMeshAgent agent;
    public Animator anim;
    public Transform eyeTransform;

    [Header("Melee Hitbox Settings")]
    public GameObject clawHitbox;

    [Header("AI Settings")]
    public float sightRange = 12f;
    public float fieldOfView = 110f;
    public float attackRange = 1.8f;
    public float walkRadius = 8f;
    public float idleTimeMax = 5f;

    [Header("Layers")]
    public LayerMask playerLayer;
    public LayerMask obstacleLayer;

    private Transform targetPlayer;
    private float stateTimer;
    private bool hasDestination;

    private Vector3 lastKnownPlayerPosition;
    private float searchTimer;
    private float searchLookTimer;
    private float searchLookDirection = 1f;

    private float detectionTimer;
    private const float DETECTION_INTERVAL = 0.15f;

    private float staggerTimer;
    private float attackCooldownTimer;
    private float tacticalTimer;
    private int tacticalState = 0;

    private float lastDamageTime;
    private int recentHitCount;

    private bool hasDealtDamage;
    private float attackDuration;

    private readonly Collider[] detectionResults = new Collider[8];
    private readonly Collider[] damageResults = new Collider[8];

    private EnemyState clientLocalState = (EnemyState)(-1);
    private int framesSinceActive = 0;

    // ------------------------------------------------------------------
    //  Awake
    // ------------------------------------------------------------------
    private void Awake()
    {
        if (anim == null) { anim = GetComponent<Animator>(); if (anim == null) anim = GetComponentInChildren<Animator>(true); }
        var netAnim = GetComponent<Unity.Netcode.Components.NetworkAnimator>();
        if (netAnim != null)
        {
            if (anim == null) { Debug.LogError($"[{gameObject.name}] Không tìm thấy Animator!"); netAnim.enabled = false; }
            else if (anim.runtimeAnimatorController == null) { Debug.LogError($"[{gameObject.name}] Animator chưa gán Controller!"); netAnim.enabled = false; }
            else netAnim.Animator = anim;
        }
    }

    // ------------------------------------------------------------------
    //  Start - Standalone
    // ------------------------------------------------------------------
    private void Start()
    {
        if (!IsNetworkActive) { isStandaloneMode = true; InitStandalone(); }
    }

    private void InitStandalone()
    {
        Debug.Log($"[{gameObject.name}] Chạy ở chế độ STANDALONE.");
        localHealth = maxHealth; localState = EnemyState.Idle;
        stateTimer = Random.Range(2f, idleTimeMax);
        SnapToNavMesh();
        if (clawHitbox != null) clawHitbox.SetActive(false);
        SyncAnimationState(EnemyState.Idle);
    }

    // ------------------------------------------------------------------
    //  OnNetworkSpawn
    // ------------------------------------------------------------------
    public override void OnNetworkSpawn()
    {
        isStandaloneMode = false;
        currentState.OnValueChanged += OnStateChanged;
        OnStateChanged(currentState.Value, currentState.Value);
        var nt = GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (nt != null) { nt.PositionThreshold = 0.001f; nt.RotAngleThreshold = 0.01f; nt.ScaleThreshold = 0.01f; }
        if (IsServer) { currentHealth.Value = maxHealth; ChangeState(EnemyState.Idle); SnapToNavMesh(); }
        else // ---> THÊM ĐOẠN NÀY VÀO <---
        {
            // TẮT NavMeshAgent trên Client để NetworkTransform của Server thoải mái cập nhật vị trí
            if (agent != null)
            {
                agent.enabled = false;
            }
        }
        if (clawHitbox != null) clawHitbox.SetActive(false);
        hitCounter.OnValueChanged += (o, n) => { if (anim != null) { anim.ResetTrigger("Anhit"); anim.SetTrigger("Anhit"); } };
    }

    public override void OnNetworkDespawn() { currentState.OnValueChanged -= OnStateChanged; }

    // ------------------------------------------------------------------
    //  NavMesh Helper
    // ------------------------------------------------------------------
    private void SnapToNavMesh()
    {
        if (agent == null || !agent.isActiveAndEnabled) return;
        if (!agent.isOnNavMesh)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 10f, NavMesh.AllAreas))
            { agent.Warp(hit.position); Debug.Log($"[{gameObject.name}] Snap NavMesh tại {hit.position}"); }
            else Debug.LogWarning($"[{gameObject.name}] Không tìm thấy NavMesh trong 10m!");
        }
    }

    private bool AgentReady => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;

    // ------------------------------------------------------------------
    //  Update
    // ------------------------------------------------------------------
    private void Update()
    {
        if (anim != null && anim.isActiveAndEnabled && anim.runtimeAnimatorController != null)
        {
            framesSinceActive++;
            if (clientLocalState != CurrentStateValue)
            {
                if (SyncAnimationState(CurrentStateValue))
                {
                    clientLocalState = CurrentStateValue; // Cập nhật ngay lập tức, bỏ qua delay!
                }
            }
        }
        else framesSinceActive = 0;

        bool isAIAuthoritative = isStandaloneMode || (IsNetworkActive && IsServer);
        if (!isAIAuthoritative) return;

        if (agent != null && agent.isActiveAndEnabled && !agent.isOnNavMesh) SnapToNavMesh();
        if (attackCooldownTimer > 0) attackCooldownTimer -= Time.deltaTime;

        detectionTimer -= Time.deltaTime;
        if (detectionTimer <= 0) { detectionTimer = DETECTION_INTERVAL; DetectPlayer(); }

        switch (CurrentStateValue)
        {
            case EnemyState.Idle: HandleIdle(); break;
            case EnemyState.Walk: HandleWalk(); break;
            case EnemyState.Run: HandleRun(); break;
            case EnemyState.Search: HandleSearch(); break;
            case EnemyState.Stagger: HandleStagger(); break;
            case EnemyState.Attack: HandleAttack(); break;
        }
    }

    // ------------------------------------------------------------------
    //  AI Logic
    // ------------------------------------------------------------------
    private void DetectPlayer()
    {
        if (CurrentStateValue == EnemyState.Dead || CurrentStateValue == EnemyState.Stagger) return;
        if (CurrentStateValue == EnemyState.Attack) return;

        int numPlayers = Physics.OverlapSphereNonAlloc(transform.position, sightRange, detectionResults, playerLayer);
        if (numPlayers == 0)
        {
            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            int c = 0;
            foreach (var p in players)
            {
                if (c >= detectionResults.Length) break;
                if (Vector3.Distance(transform.position, p.transform.position) <= sightRange)
                { var col = p.GetComponent<Collider>(); if (col != null) detectionResults[c++] = col; }
            }
            numPlayers = c;
        }
        if (numPlayers == 0 && IsNetworkActive && NetworkManager.Singleton != null)
        {
            int c = 0;
            foreach (var kvp in NetworkManager.Singleton.ConnectedClients)
            {
                if (c >= detectionResults.Length) break;
                var po = kvp.Value.PlayerObject;
                if (po == null || Vector3.Distance(transform.position, po.transform.position) > sightRange) continue;
                var col = po.GetComponent<Collider>(); if (col != null) detectionResults[c++] = col;
            }
            numPlayers = c;
        }

        bool playerFound = false;
        Vector3 eyePos = eyeTransform != null ? eyeTransform.position : transform.position + Vector3.up * 1.5f;
        for (int i = 0; i < numPlayers; i++)
        {
            Collider p = detectionResults[i]; if (p == null) continue;
            Transform pt = p.transform;
            var ps = pt.GetComponentInParent<SimplePlayerTest>();
            if (ps != null && ps.CurrentHealth <= 0) continue;
            // Nâng tâm ngắm lên ngực Player (cao 1.0f)
            Vector3 targetCenterPos = pt.position + Vector3.up * 1.0f;
            Vector3 dir = (targetCenterPos - eyePos).normalized;

            if (Vector3.Angle(transform.forward, dir) < fieldOfView / 2)
            {
                float dist = Vector3.Distance(eyePos, targetCenterPos);
                if (!Physics.Raycast(eyePos, dir, dist, obstacleLayer))
                { targetPlayer = pt; playerFound = true; if (CurrentStateValue != EnemyState.Run) ChangeState(EnemyState.Run); break; }
            }
        }
        if (!playerFound && CurrentStateValue == EnemyState.Run)
        {
            if (targetPlayer != null) { lastKnownPlayerPosition = targetPlayer.position; targetPlayer = null; ChangeState(EnemyState.Search); }
            else ChangeState(EnemyState.Idle);
        }
    }

    private void HandleIdle()
    {
        if (AgentReady) agent.isStopped = true;
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0)
        {
            float r = Random.value;
            if (r < 0.4f) ChangeState(EnemyState.Walk);
            else if (r < 0.7f) ChangeState(EnemyState.Run);
            else stateTimer = Random.Range(1.5f, idleTimeMax);
        }
    }

    private void HandleWalk()
    {
        if (AgentReady) { agent.isStopped = false; agent.speed = 1.5f; }
        if (!hasDestination)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(Random.insideUnitSphere * walkRadius + transform.position, out hit, walkRadius, 1))
            { if (AgentReady) agent.SetDestination(hit.position); hasDestination = true; }
        }
        if (hasDestination && AgentReady && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f)
        {
            hasDestination = false;
            float r = Random.value;
            if (r < 0.4f) ChangeState(EnemyState.Idle); else if (r < 0.8f) ChangeState(EnemyState.Walk); else ChangeState(EnemyState.Run);
        }
    }

    private void HandleRun()
    {
        if (targetPlayer == null)
        {
            if (AgentReady) { agent.isStopped = false; agent.speed = 3.5f; }
            if (!hasDestination)
            {
                NavMeshHit hit;
                if (NavMesh.SamplePosition(Random.insideUnitSphere * walkRadius * 1.5f + transform.position, out hit, walkRadius * 1.5f, 1))
                { if (AgentReady) agent.SetDestination(hit.position); hasDestination = true; }
            }
            if (hasDestination && AgentReady && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f)
            { hasDestination = false; ChangeState(Random.value < 0.6f ? EnemyState.Idle : EnemyState.Walk); }
            return;
        }

        Vector3 lk = (targetPlayer.position - transform.position); lk.y = 0;
        if (lk != Vector3.zero) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lk), Time.deltaTime * 15f);

        bool isFrantic = CurrentHealthValue <= maxHealth * 0.4f;
        if (AgentReady) { agent.isStopped = false; agent.speed = isFrantic ? 6.5f : 4.5f; }

        float dist = Vector3.Distance(transform.position, targetPlayer.position);
        if (dist <= 6f)
        {
            tacticalTimer -= Time.deltaTime;
            if (tacticalTimer <= 0) { float r = Random.value; if (r < 0.5f) tacticalState = 0; else if (r < 0.75f) tacticalState = 1; else tacticalState = 2; tacticalTimer = Random.Range(1f, 2f); }
            if (tacticalState == 0) { if (AgentReady) agent.SetDestination(targetPlayer.position); }
            else
            {
                Vector3 tp = (targetPlayer.position - transform.position).normalized;
                Vector3 tg = new Vector3(-tp.z, 0, tp.x); float sd = (tacticalState == 1) ? 1f : -1f;
                Vector3 toff = targetPlayer.position - tp * 2.2f + tg * sd * 2.5f;
                NavMeshHit hit;
                if (NavMesh.SamplePosition(toff, out hit, 3.5f, NavMesh.AllAreas)) { if (AgentReady) agent.SetDestination(hit.position); }
                else { if (AgentReady) agent.SetDestination(targetPlayer.position); }
            }
        }
        else { if (AgentReady) agent.SetDestination(targetPlayer.position); }

        // ĐOẠN CODE ĐÚNG SAU KHI SỬA
        if (dist <= attackRange)
        {
            if (attackCooldownTimer <= 0)
            {
                ChangeState(EnemyState.Attack);
            }
            // Bỏ qua bước chuyển sang Idle. Quái sẽ giữ state Run và chạy bám đuôi Player!
        }
    }

    private void HandleSearch()
    {
        if (AgentReady) { agent.isStopped = false; agent.speed = 3.5f; agent.SetDestination(lastKnownPlayerPosition); }
        if (AgentReady && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
        {
            agent.isStopped = true; searchTimer -= Time.deltaTime;
            searchLookTimer -= Time.deltaTime; if (searchLookTimer <= 0) { searchLookDirection = -searchLookDirection; searchLookTimer = 0.6f; }
            transform.Rotate(Vector3.up, searchLookDirection * 150f * Time.deltaTime);
            if (searchTimer <= 0) ChangeState(EnemyState.Idle);
        }
    }

    private void HandleStagger()
    {
        if (AgentReady) agent.isStopped = true;
        staggerTimer -= Time.deltaTime;
        if (staggerTimer <= 0) ChangeState(targetPlayer != null ? EnemyState.Run : EnemyState.Idle);
    }

    private void HandleAttack()
    {
        if (targetPlayer == null) { ChangeState(EnemyState.Idle); return; }
        if (AgentReady) agent.isStopped = true;
        stateTimer -= Time.deltaTime;
        float elapsed = attackDuration - stateTimer;
        if (elapsed < attackDuration * 0.4f)
        {
            Vector3 lk = (targetPlayer.position - transform.position); lk.y = 0;
            if (lk != Vector3.zero) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lk), Time.deltaTime * 18f);
        }
        bool isFrantic = CurrentHealthValue <= maxHealth * 0.4f;
        float hitT = isFrantic ? 0.24f : 0.4f;
        if (elapsed >= hitT && !hasDealtDamage) { hasDealtDamage = true; DealConeDamage(isFrantic ? 15f : 12f, 2.0f, 90f, 4f); }
        if (stateTimer <= 0) { attackCooldownTimer = isFrantic ? 0.15f : 0.6f; ChangeState(EnemyState.Run); }
    }

    private void DealConeDamage(float damage, float range, float angle, float knockback)
    {
        int num = Physics.OverlapSphereNonAlloc(transform.position, range, damageResults, playerLayer);
        if (num == 0)
        {
            int c = 0;
            foreach (var p in GameObject.FindGameObjectsWithTag("Player"))
            {
                if (c >= damageResults.Length) break;
                if (Vector3.Distance(transform.position, p.transform.position) <= range)
                { var col = p.GetComponent<Collider>(); if (col != null) damageResults[c++] = col; }
            }
            num = c;
        }
        Vector3 eyePos = eyeTransform != null ? eyeTransform.position : transform.position + Vector3.up * 1.5f;
        for (int i = 0; i < num; i++)
        {
            Collider col = damageResults[i]; if (col == null) continue;
            Transform pl = col.transform; Vector3 dir = (pl.position - transform.position).normalized;
            if (Vector3.Angle(transform.forward, dir) <= angle / 2f)
            {
                float d = Vector3.Distance(transform.position, pl.position);
                if (!Physics.Raycast(eyePos, dir, d, obstacleLayer))
                {
                    var ps = pl.GetComponentInParent<SimplePlayerTest>();
                    if (ps != null) { ps.TakeDamage(damage); Vector3 kb = dir; kb.y = 0; kb.Normalize(); ps.ApplyKnockback(kb * knockback); }
                }
            }
        }
    }

    public void TakeDamage(float damage)
    {
        if (!isStandaloneMode && (!IsServer || CurrentStateValue == EnemyState.Dead)) return;
        if (isStandaloneMode && CurrentStateValue == EnemyState.Dead) return;

        CurrentHealthValue -= damage;
        if (!isStandaloneMode) hitCounter.Value++;
        else if (anim != null) { anim.ResetTrigger("Anhit"); anim.SetTrigger("Anhit"); }

        if (CurrentHealthValue <= 0) { ChangeState(EnemyState.Dead); return; }

        float now = Time.time;
        if (now - lastDamageTime > 3f) recentHitCount = 0;
        recentHitCount++; lastDamageTime = now;
        bool shouldStagger = (damage >= 20f || recentHitCount >= 3) && (CurrentStateValue != EnemyState.Stagger);
        if (shouldStagger) { recentHitCount = 0; staggerTimer = 0.5f; ChangeState(EnemyState.Stagger); }
    }

    private void Die()
    {
        if (AgentReady) agent.isStopped = true;
        Invoke(nameof(DespawnEnemy), 2f);
    }

    private void DespawnEnemy()
    {
        if (isStandaloneMode) { Destroy(gameObject); return; }
        if (IsServer && IsSpawned) GetComponent<NetworkObject>().Despawn();
    }

    private void ChangeState(EnemyState newState)
    {
        if (CurrentStateValue == EnemyState.Attack && newState != EnemyState.Attack && clawHitbox != null) clawHitbox.SetActive(false);
        CurrentStateValue = newState;
        if (newState == EnemyState.Idle) { stateTimer = Random.Range(2f, idleTimeMax); if (AgentReady) agent.isStopped = true; }
        if (newState == EnemyState.Walk) { hasDestination = false; if (AgentReady) agent.isStopped = false; }
        if (newState == EnemyState.Run) { if (AgentReady) agent.isStopped = false; }
        if (newState == EnemyState.Search) { searchTimer = 3.0f; searchLookTimer = 0f; if (AgentReady) agent.isStopped = false; }
        if (newState == EnemyState.Stagger) { if (AgentReady) agent.isStopped = true; if (staggerTimer <= 0) staggerTimer = 0.5f; }
        if (newState == EnemyState.Attack)
        {
            if (AgentReady) agent.isStopped = true;
            hasDealtDamage = false; if (!isStandaloneMode) attackType.Value = 0;
            bool isFrantic = CurrentHealthValue <= maxHealth * 0.4f;
            attackDuration = isFrantic ? 0.6f : 1.0f; stateTimer = attackDuration;
            PlayAttackAnimationLocal();
            if (!isStandaloneMode) PlayAttackAnimationClientRpc();
        }
        if (newState == EnemyState.Dead) Die();
    }

    private void OnStateChanged(EnemyState pv, EnemyState nv) { clientLocalState = (EnemyState)(-1); }

    private bool SyncAnimationState(EnemyState s)
    {
        if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null) return false;
        anim.ResetTrigger("Idle"); anim.ResetTrigger("Walk"); anim.ResetTrigger("Run"); anim.ResetTrigger("Anhit"); anim.ResetTrigger("Die");
        switch (s)
        {
            case EnemyState.Idle: anim.SetTrigger("Idle"); break;
            case EnemyState.Walk: anim.SetTrigger("Walk"); break;
            case EnemyState.Run: anim.SetTrigger("Run"); break;
            case EnemyState.Stagger: anim.SetTrigger("Anhit"); break;
            case EnemyState.Dead: anim.SetTrigger("Die"); break;
        }
        return true;
    }

    private void PlayAttackAnimationLocal() { if (anim == null) return; anim.ResetTrigger("Attack"); anim.SetTrigger("Attack"); }

    [ClientRpc] private void PlayAttackAnimationClientRpc() { PlayAttackAnimationLocal(); }

    public void EnableClawHitbox() { if (clawHitbox != null) clawHitbox.SetActive(true); }
    public void DisableClawHitbox() { if (clawHitbox != null) clawHitbox.SetActive(false); }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, sightRange);
        Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
