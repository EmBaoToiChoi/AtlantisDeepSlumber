using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// AI Skeleton - Đệ triệu hồi của Player
/// Có khả năng chạy theo Player, phát hiện và tấn công 5 Enemy.
/// Đồng bộ hóa mạng hoàn chỉnh và hỗ trợ thanh máu UI generic.
/// </summary>
public class Skeleton : NetworkBehaviour
{
    public enum State { Spawn, Follow, Chase, Stagger, Dead }

    [Header("Health")]
    public float maxHealth = 100f;
    public NetworkVariable<float> currentHealth = new NetworkVariable<float>(
        100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Network Sync Vars")]
    public NetworkVariable<float> netSpeed = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> hitCounter = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<NetworkObjectReference> summonerRef = new NetworkVariable<NetworkObjectReference>(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private float localHealth;
    public bool isStandaloneMode = false;
    private State localState = State.Spawn;

    private bool IsNetworkActive => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

    public float CurrentHealthValue
    {
        get => isStandaloneMode ? localHealth : currentHealth.Value;
        set { if (isStandaloneMode) localHealth = value; else currentHealth.Value = value; }
    }

    public float ActualCurrentHealth => CurrentHealthValue;

    public State currentState
    {
        get => isStandaloneMode ? localState : localState; // We'll handle state locally on Host/Server
        set => localState = value;
    }

    [Header("Components")]
    public NavMeshAgent agent;
    public Animator anim;
    public Rigidbody rb;

    [Header("AI follow/chase Settings")]
    public float sightRange = 12f;
    public float attackRange = 1.8f;
    public float followDistance = 2.5f;
    public float followSpeed = 4f;
    public float chaseSpeed = 5.5f;
    public float attackCooldown = 1.5f;
    public float spawnDuration = 1.5f;
    public float despawnDelay = 2.5f;

    [Header("Layers & Targets")]
    public LayerMask enemyLayer;
    public string speedParam = "Speed";
    public string hitTrigger = "Hit";
    public string dieTrigger = "Die";
    public string atkTrigger = "Attack";
    public string spawnTrigger = "Spawn";

    [Header("Weapon Hitbox settings")]
    public GameObject weaponHitbox;
    public Transform attackCheckPoint;
    public float attackRadius = 1.2f;
    public float attackDamage = 15f;
    public float knockbackForce = 5f;

    private Transform targetEnemy;
    private Transform localSummoner;
    private float stateTimer;
    private float attackCooldownTimer;
    private float detectionTimer;
    private const float DETECTION_INTERVAL = 0.2f;
    private bool isHitboxActive = false;

    private readonly Collider[] detectionResults = new Collider[8];
    private readonly Collider[] damageResults = new Collider[8];
    private System.Collections.Generic.List<Transform> hitTargetsThisAttack = new System.Collections.Generic.List<Transform>();

    private bool AgentReady => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;
    private bool IsServerOrStandalone => isStandaloneMode || (IsNetworkActive && IsServer);

    private void Awake()
    {
        if (anim == null) anim = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (rb == null) rb = GetComponent<Rigidbody>();

        // Thiết lập chống đẩy quái/player vật lý
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        if (agent != null)
        {
            agent.obstacleAvoidanceType = (UnityEngine.AI.ObstacleAvoidanceType)0;
        }
    }

    private void Start()
    {
        if (!IsNetworkActive)
        {
            isStandaloneMode = true;
            InitStandalone();
        }
    }

    private void InitStandalone()
    {
        localHealth = maxHealth;
        currentState = State.Spawn;
        stateTimer = spawnDuration;
        if (anim != null) anim.SetTrigger(spawnTrigger);
        SnapToNavMesh();
        if (weaponHitbox != null) weaponHitbox.SetActive(false);
    }

    public override void OnNetworkSpawn()
    {
        isStandaloneMode = false;

        var nt = GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (nt != null)
        {
            nt.PositionThreshold = 0.001f;
            nt.RotAngleThreshold = 0.01f;
            nt.ScaleThreshold = 0.01f;
        }

        netSpeed.OnValueChanged += (_, v) => ApplySpeedAnim(v);
        hitCounter.OnValueChanged += (_, _) => { if (anim != null) anim.SetTrigger(hitTrigger); };

        ApplySpeedAnim(netSpeed.Value);

        if (IsServer)
        {
            currentHealth.Value = maxHealth;
            currentState = State.Spawn;
            stateTimer = spawnDuration;
            PlaySpawnClientRpc();
            SnapToNavMesh();
            if (weaponHitbox != null) weaponHitbox.SetActive(false);
        }
        else
        {
            if (agent != null) agent.enabled = false;
        }
    }

    public override void OnNetworkDespawn()
    {
        netSpeed.OnValueChanged -= (_, v) => ApplySpeedAnim(v);
        hitCounter.OnValueChanged -= (_, _) => { if (anim != null) anim.SetTrigger(hitTrigger); };
    }

    public void SetSummoner(Transform summoner)
    {
        if (IsServerOrStandalone)
        {
            if (isStandaloneMode)
            {
                localSummoner = summoner;
            }
            else
            {
                if (summoner.TryGetComponent<NetworkObject>(out var netObj))
                {
                    summonerRef.Value = netObj;
                }
            }
        }
    }

    private Transform GetSummonerTransform()
    {
        if (isStandaloneMode)
        {
            if (localSummoner == null)
            {
                var player = FindObjectOfType<SimplePlayerTest>();
                if (player != null) localSummoner = player.transform;
            }
            return localSummoner;
        }
        else
        {
            if (summonerRef.Value.TryGet(out NetworkObject netObj))
            {
                return netObj.transform;
            }
            return null;
        }
    }

    private void Update()
    {
        if (!IsServerOrStandalone) return;

        if (AgentReady && !agent.isOnNavMesh) SnapToNavMesh();

        if (attackCooldownTimer > 0) attackCooldownTimer -= Time.deltaTime;

        if (currentState == State.Spawn)
        {
            if (AgentReady) agent.isStopped = true;
            SetSpeedNet(0f);
            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f)
            {
                currentState = State.Follow;
            }
            return;
        }

        if (currentState == State.Dead) return;

        if (currentState == State.Stagger)
        {
            if (AgentReady) agent.isStopped = true;
            SetSpeedNet(0f);
            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f)
            {
                currentState = State.Follow;
            }
            return;
        }

        // Dò quét kẻ thù liên tục
        detectionTimer -= Time.deltaTime;
        if (detectionTimer <= 0f)
        {
            detectionTimer = DETECTION_INTERVAL;
            DetectEnemy();
        }

        // Hành vi AI
        if (targetEnemy != null)
        {
            HandleChaseAndAttack();
        }
        else
        {
            HandleFollowSummoner();
        }

        // Xử lý bật hitbox gây sát thương
        if (isHitboxActive)
        {
            CheckHitboxOverlap();
        }
    }

    private void HandleFollowSummoner()
    {
        Transform summoner = GetSummonerTransform();
        if (summoner == null)
        {
            if (AgentReady) agent.isStopped = true;
            SetSpeedNet(0f);
            return;
        }

        float dist = Vector3.Distance(transform.position, summoner.position);
        if (dist > followDistance)
        {
            if (AgentReady)
            {
                agent.isStopped = false;
                agent.speed = followSpeed;
                agent.SetDestination(summoner.position);
            }
            // Di chuyển với speed 1f (Run) khi chạy theo người triệu hồi
            SetSpeedNet(AgentReady && agent.velocity.magnitude > 0.15f ? 1f : 0f);
        }
        else
        {
            if (AgentReady) agent.isStopped = true;
            SetSpeedNet(0f);
        }
    }

    private void HandleChaseAndAttack()
    {
        if (targetEnemy == null || IsEnemyDead(targetEnemy))
        {
            targetEnemy = null;
            return;
        }

        float dist = Vector3.Distance(transform.position, targetEnemy.position);
        if (dist <= attackRange)
        {
            if (AgentReady) agent.isStopped = true;
            SetSpeedNet(0f);

            if (attackCooldownTimer <= 0f)
            {
                TriggerAttack();
            }
        }
        else
        {
            if (AgentReady)
            {
                agent.isStopped = false;
                agent.speed = chaseSpeed;
                agent.SetDestination(targetEnemy.position);
            }
            SetSpeedNet(AgentReady && agent.velocity.magnitude > 0.2f ? 1f : 0f);
        }
    }

    private void TriggerAttack()
    {
        attackCooldownTimer = attackCooldown;
        
        // Quay mặt về hướng quái
        Vector3 dir = (targetEnemy.position - transform.position);
        dir.y = 0;
        if (dir.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(dir);
        }

        if (isStandaloneMode)
        {
            if (anim != null) anim.SetTrigger(atkTrigger);
        }
        else
        {
            PlayAttackClientRpc();
        }
    }

    private void DetectEnemy()
    {
        int num = Physics.OverlapSphereNonAlloc(transform.position, sightRange, detectionResults, enemyLayer);
        float minD = float.MaxValue;
        Transform closest = null;

        for (int i = 0; i < num; i++)
        {
            Collider col = detectionResults[i];
            if (col == null) continue;

            if (!IsEnemyComponent(col)) continue;
            if (IsEnemyDead(col.transform)) continue;

            float d = Vector3.Distance(transform.position, col.transform.position);
            if (d < minD)
            {
                minD = d;
                closest = col.transform;
            }
        }

        targetEnemy = closest;
    }

    private bool IsEnemyComponent(Collider col)
    {
        return col.GetComponentInParent<Enemy1_DapBua>() != null ||
               col.GetComponentInParent<Enemy2_Zombie>() != null ||
               col.GetComponentInParent<Enemy3_Buaa>() != null ||
               col.GetComponentInParent<Enemy4_Bongtoi>() != null ||
               col.GetComponentInParent<Enemy5_PhuThuy>() != null;
    }

    private bool IsEnemyDead(Transform t)
    {
        var e1 = t.GetComponentInParent<Enemy1_DapBua>();
        if (e1 != null) return e1.currentState.Value == Enemy1_DapBua.EnemyState.Dead;
        var e2 = t.GetComponentInParent<Enemy2_Zombie>();
        if (e2 != null) return e2.currentState.Value == Enemy2_Zombie.EnemyState.Dead;
        var e3 = t.GetComponentInParent<Enemy3_Buaa>();
        if (e3 != null) return e3.currentState.Value == Enemy3_Buaa.EnemyState.Dead;
        var e4 = t.GetComponentInParent<Enemy4_Bongtoi>();
        if (e4 != null) return e4.currentState.Value == Enemy4_Bongtoi.EnemyState.Dead;
        var e5 = t.GetComponentInParent<Enemy5_PhuThuy>();
        if (e5 != null) return e5.currentState.Value == Enemy5_PhuThuy.EnemyState.Dead;
        return true;
    }

    // ══════════════════════════════════════════════════════════
    //  DAMAGE / STAGGER / DEATH
    // ══════════════════════════════════════════════════════════
    public void TakeDamage(float damage)
    {
        if (!IsServerOrStandalone) return;
        if (currentState == State.Dead) return;

        CurrentHealthValue -= damage;

        if (!isStandaloneMode)
        {
            hitCounter.Value++;
        }
        else
        {
            if (anim != null) anim.SetTrigger(hitTrigger);
        }

        if (CurrentHealthValue <= 0f)
        {
            currentState = State.Dead;
            Die();
            return;
        }

        currentState = State.Stagger;
        stateTimer = 0.6f;
    }

    public void ApplyKnockback(Vector3 force)
    {
        if (AgentReady && force.sqrMagnitude > 0.01f)
        {
            agent.Move(force * Time.deltaTime);
        }
    }

    private void Die()
    {
        if (AgentReady) agent.isStopped = true;
        SetSpeedNet(0f);
        
        if (anim != null)
        {
            anim.ResetTrigger(dieTrigger);
            anim.SetTrigger(dieTrigger);
        }

        DisableWeaponHitbox();

        // Tắt colliders để tránh cản trở
        var cols = GetComponentsInChildren<Collider>();
        foreach (var c in cols) c.enabled = false;

        Invoke(nameof(DespawnSkeleton), despawnDelay);
    }

    private void DespawnSkeleton()
    {
        if (isStandaloneMode)
        {
            Destroy(gameObject);
        }
        else if (IsServer && IsSpawned)
        {
            GetComponent<NetworkObject>().Despawn();
        }
    }

    // ══════════════════════════════════════════════════════════
    //  HITBOX OVERLAP SYSTEM (Animation Event targets)
    // ══════════════════════════════════════════════════════════
    public void EnableWeaponHitbox()
    {
        isHitboxActive = true;
        hitTargetsThisAttack.Clear();
        if (weaponHitbox != null) weaponHitbox.SetActive(true);
    }

    public void DisableWeaponHitbox()
    {
        isHitboxActive = false;
        if (weaponHitbox != null) weaponHitbox.SetActive(false);
    }

    private void CheckHitboxOverlap()
    {
        Vector3 checkPos = attackCheckPoint != null ? attackCheckPoint.position : transform.position + transform.forward * attackRange + Vector3.up * 1f;
        int num = Physics.OverlapSphereNonAlloc(checkPos, attackRadius, damageResults, enemyLayer);

        for (int i = 0; i < num; i++)
        {
            Collider col = damageResults[i];
            if (col == null) continue;

            Transform targetRoot = col.transform;
            if (hitTargetsThisAttack.Contains(targetRoot)) continue;

            if (DealDamageToEnemy(col))
            {
                hitTargetsThisAttack.Add(targetRoot);
            }
        }
    }

    private bool DealDamageToEnemy(Collider col)
    {
        Vector3 knockbackDir = (col.transform.position - transform.position).normalized;
        knockbackDir.y = 0;
        Vector3 kbForce = knockbackDir * knockbackForce;

        var e1 = col.GetComponentInParent<Enemy1_DapBua>();
        if (e1 != null) { e1.TakeDamage(attackDamage); return true; }

        var e2 = col.GetComponentInParent<Enemy2_Zombie>();
        if (e2 != null) { e2.TakeDamage(attackDamage); return true; }

        var e3 = col.GetComponentInParent<Enemy3_Buaa>();
        if (e3 != null) { e3.TakeDamage(attackDamage); return true; }

        var e4 = col.GetComponentInParent<Enemy4_Bongtoi>();
        if (e4 != null) { e4.TakeDamage(attackDamage); return true; }

        var e5 = col.GetComponentInParent<Enemy5_PhuThuy>();
        if (e5 != null) { e5.TakeDamage(attackDamage); return true; }

        return false;
    }

    // ══════════════════════════════════════════════════════════
    //  NETWORKING / ANIMATION SYNC
    // ══════════════════════════════════════════════════════════
    private void ApplySpeedAnim(float speed)
    {
        if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null) return;
        anim.SetFloat(speedParam, speed);
    }

    private void SetSpeedNet(float val)
    {
        if (isStandaloneMode) ApplySpeedAnim(val);
        else if (IsServer && !Mathf.Approximately(netSpeed.Value, val)) netSpeed.Value = val;
    }

    [ClientRpc] private void PlaySpawnClientRpc() { if (anim != null) anim.SetTrigger(spawnTrigger); }
    [ClientRpc] private void PlayAttackClientRpc() { if (anim != null) anim.SetTrigger(atkTrigger); }

    private void SnapToNavMesh()
    {
        if (agent == null || !agent.isActiveAndEnabled) return;
        if (!agent.isOnNavMesh)
        {
            NavMeshHit h;
            if (NavMesh.SamplePosition(transform.position, out h, 10f, NavMesh.AllAreas))
            {
                agent.Warp(h.position);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Vector3 checkPos = attackCheckPoint != null ? attackCheckPoint.position : transform.position + transform.forward * attackRange + Vector3.up * 1f;
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(checkPos, attackRadius);
    }
}
