using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class Enemy1_DapBua : NetworkBehaviour
{
    public enum EnemyState
    {
        Idle,
        Walk,
        Run,
        Search,    // Trạng thái săn tìm player tại vị trí cuối cùng
        Stagger,   // Trạng thái bị khựng / choáng khi nhận sát thương lớn
        Attack,
        Dead
    }

    [Header("Health Settings")]
    public float maxHealth = 100f;
    public NetworkVariable<float> currentHealth = new NetworkVariable<float>(
        100f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<EnemyState> currentState = new NetworkVariable<EnemyState>(
        EnemyState.Idle,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<bool> isEnraged = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Header("Advanced AI Sync")]
    public NetworkVariable<int> hitCounter = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> attackType = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // ------------------------------------------------------------------
    //  Biến fallback cho Standalone (không Netcode)
    // ------------------------------------------------------------------
    private float localHealth;
    private EnemyState localState = EnemyState.Idle;
    private bool localIsEnraged = false;
    private bool isStandaloneMode = false;

    /// <summary>true nếu NetworkManager đang chạy và lắng nghe.</summary>
    private bool IsNetworkActive =>
        NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

    // Trạng thái đọc hợp nhất (standalone hoặc network)
    private EnemyState CurrentStateValue
    {
        get => isStandaloneMode ? localState : currentState.Value;
        set
        {
            if (isStandaloneMode) localState = value;
            else currentState.Value = value;
        }
    }

    private float CurrentHealthValue
    {
        get => isStandaloneMode ? localHealth : currentHealth.Value;
        set
        {
            if (isStandaloneMode) localHealth = value;
            else currentHealth.Value = value;
        }
    }

    private bool IsEnragedValue
    {
        get => isStandaloneMode ? localIsEnraged : isEnraged.Value;
        set
        {
            if (isStandaloneMode) localIsEnraged = value;
            else isEnraged.Value = value;
        }
    }

    private bool isNextAttackLeft = true;

    [Header("Components")]
    public NavMeshAgent agent;
    public Animator anim;
    public Transform eyeTransform;

    [Header("Melee Hitbox Settings")]
    public GameObject hammerHitbox;
    public GameObject hammerHitboxLeft;
    public GameObject hammerHitboxRight;

    [Header("AI Settings")]
    public float sightRange = 15f;
    public float fieldOfView = 90f;
    public float attackRange = 2f;
    public float walkRadius = 10f;
    public float idleTimeMax = 4f;

    [Header("Layers")]
    public LayerMask playerLayer;
    public LayerMask obstacleLayer;

    [Header("Pro AI Combat Settings")]
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
    private bool hasRoared = false;

    private float attackCooldownTimer;
    private float tacticalTimer;
    private int tacticalState = 0;

    private bool isDodging = false;
    private float dodgeTimer;

    private float lastDamageTime;
    private int recentHitCount;

    private bool hasDealtDamage1;
    private bool hasDealtDamage2;
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
        if (anim == null)
        {
            anim = GetComponent<Animator>();
            if (anim == null)
                anim = GetComponentInChildren<Animator>(true);
        }

        var netAnim = GetComponent<Unity.Netcode.Components.NetworkAnimator>();
        if (netAnim != null)
        {
            if (anim == null)
            {
                Debug.LogError($"[{gameObject.name}] Không tìm thấy Animator! Đang vô hiệu hóa NetworkAnimator.");
                netAnim.enabled = false;
            }
            else if (anim.runtimeAnimatorController == null)
            {
                Debug.LogError($"[{gameObject.name}] Animator chưa được gán Controller! Đang vô hiệu hóa NetworkAnimator.");
                netAnim.enabled = false;
            }
            else
            {
                netAnim.Animator = anim;
            }
        }
    }

    // ------------------------------------------------------------------
    //  Start - Khởi tạo Standalone khi không có Netcode
    // ------------------------------------------------------------------
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
        Debug.Log($"[{gameObject.name}] Chạy ở chế độ STANDALONE.");
        localHealth = maxHealth;
        localIsEnraged = false;
        localState = EnemyState.Idle;
        stateTimer = Random.Range(2f, idleTimeMax);

        // Snap vào NavMesh
        SnapToNavMesh();

        // Tắt hitbox
        if (hammerHitbox != null) hammerHitbox.SetActive(false);
        if (hammerHitboxLeft != null) hammerHitboxLeft.SetActive(false);
        if (hammerHitboxRight != null) hammerHitboxRight.SetActive(false);

        // Khởi animation
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

        var netTransform = GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (netTransform != null)
        {
            netTransform.PositionThreshold = 0.001f;
            netTransform.RotAngleThreshold = 0.01f;
            netTransform.ScaleThreshold = 0.01f;
        }

        if (IsServer)
        {
            currentHealth.Value = maxHealth;
            isEnraged.Value = false;
            ChangeState(EnemyState.Idle);
            SnapToNavMesh();
        }
        else // ---> THÊM ĐOẠN NÀY VÀO <---
        {
            // TẮT NavMeshAgent trên Client để NetworkTransform của Server thoải mái cập nhật vị trí
            if (agent != null)
            {
                agent.enabled = false;
            }
        }

        if (hammerHitbox != null) hammerHitbox.SetActive(false);
        if (hammerHitboxLeft != null) hammerHitboxLeft.SetActive(false);
        if (hammerHitboxRight != null) hammerHitboxRight.SetActive(false);

        hitCounter.OnValueChanged += (oldVal, newVal) =>
        {
            if (anim != null)
            {
                if (CurrentStateValue == EnemyState.Stagger && IsEnragedValue && !hasRoared)
                    return;
                anim.SetTrigger("Hit");
            }
        };
    }

    public override void OnNetworkDespawn()
    {
        currentState.OnValueChanged -= OnStateChanged;
    }

    // ------------------------------------------------------------------
    //  Tiện ích NavMesh
    // ------------------------------------------------------------------
    private void SnapToNavMesh()
    {
        if (agent == null || !agent.isActiveAndEnabled) return;
        if (!agent.isOnNavMesh)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 10f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
                Debug.Log($"[{gameObject.name}] Đã snap vào NavMesh tại {hit.position}");
            }
            else
            {
                Debug.LogWarning($"[{gameObject.name}] KHÔNG TÌM THẤY NavMesh trong phạm vi 10m! Kiểm tra NavMesh Bake.");
            }
        }
    }

    private bool AgentReady => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;

    // ------------------------------------------------------------------
    //  Update
    // ------------------------------------------------------------------
    private void Update()
    {
        // Đồng bộ scale khi phẫn nộ (chạy trên tất cả client)
        if (IsEnragedValue && !hasRoared)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * 1.15f, Time.deltaTime * 3f);
            if (transform.localScale.x >= 1.14f)
                hasRoared = true;
        }

        // Đồng bộ animation (client side)
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
        else
        {
            framesSinceActive = 0;
        }

        // Xác định quyền AI: standalone → luôn tính; Netcode → chỉ Server
        bool isAIAuthoritative = isStandaloneMode || (IsNetworkActive && IsServer);
        if (!isAIAuthoritative) return;

        // Snap lại NavMesh nếu bị đẩy ra ngoài
        if (agent != null && agent.isActiveAndEnabled && !agent.isOnNavMesh)
            SnapToNavMesh();

        if (attackCooldownTimer > 0) attackCooldownTimer -= Time.deltaTime;

        detectionTimer -= Time.deltaTime;
        if (detectionTimer <= 0)
        {
            detectionTimer = DETECTION_INTERVAL;
            DetectPlayer();
        }

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

        // Fallback: tìm theo Tag "Player"
        if (numPlayers == 0)
        {
            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            int count = 0;
            foreach (var p in players)
            {
                if (count >= detectionResults.Length) break;
                if (Vector3.Distance(transform.position, p.transform.position) <= sightRange)
                {
                    Collider col = p.GetComponent<Collider>();
                    if (col != null) detectionResults[count++] = col;
                }
            }
            numPlayers = count;
        }

        // Fallback Netcode: tìm qua ConnectedClients
        if (numPlayers == 0 && IsNetworkActive && NetworkManager.Singleton != null)
        {
            int count = 0;
            foreach (var kvp in NetworkManager.Singleton.ConnectedClients)
            {
                if (count >= detectionResults.Length) break;
                var playerObj = kvp.Value.PlayerObject;
                if (playerObj == null) continue;
                if (Vector3.Distance(transform.position, playerObj.transform.position) > sightRange) continue;
                var col = playerObj.GetComponent<Collider>();
                if (col != null) detectionResults[count++] = col;
            }
            numPlayers = count;
        }

        bool playerFound = false;
        Transform closestPlayer = null;
        float minDistance = float.MaxValue;
        Vector3 eyePos = eyeTransform != null ? eyeTransform.position : transform.position + Vector3.up * 1.5f;

        for (int i = 0; i < numPlayers; i++)
        {
            Collider p = detectionResults[i];
            if (p == null) continue;
            Transform potentialTarget = p.transform;

            // 1. TÍNH NĂNG MỚI: Bỏ qua Player nếu họ đã chết
            SimplePlayerTest playerScript = potentialTarget.GetComponentInParent<SimplePlayerTest>();
            if (playerScript != null && playerScript.CurrentHealth <= 0) continue;

            Vector3 targetCenterPos = potentialTarget.position + Vector3.up * 1.0f;
            float distanceToTarget = Vector3.Distance(eyePos, targetCenterPos);
            Vector3 directionToTarget = (targetCenterPos - eyePos).normalized;

            // 2. FIX LỖI TRƯỢT BĂNG: Nằm trong góc FOV HOẶC đang là mục tiêu hiện tại.
            // Điều này giúp quái xoay 360 độ vẫn bám theo mục tiêu cũ, không bị mất dấu khi Player lách qua sườn!
            bool inFOV = Vector3.Angle(transform.forward, directionToTarget) < (fieldOfView / 2f);
            bool isCurrentTarget = (targetPlayer == potentialTarget);

            if (inFOV || isCurrentTarget)
            {
                // Bắn tia kiểm tra vật cản
                if (!Physics.Raycast(eyePos, directionToTarget, distanceToTarget, obstacleLayer))
                {
                    // 3. TÍNH NĂNG MỚI: Luôn ưu tiên khóa mục tiêu vào Player đứng gần nhất
                    if (distanceToTarget < minDistance)
                    {
                        minDistance = distanceToTarget;
                        closestPlayer = potentialTarget;
                        playerFound = true;
                    }
                }
            }
        }

        // 4. Áp dụng mục tiêu gần nhất tìm được
        if (playerFound && closestPlayer != null)
        {
            targetPlayer = closestPlayer;
            if (CurrentStateValue != EnemyState.Run)
            {
                ChangeState(EnemyState.Run);
            }
        }
        else if (CurrentStateValue == EnemyState.Run)
        {
            if (targetPlayer != null)
            {
                lastKnownPlayerPosition = targetPlayer.position;
                targetPlayer = null;
                ChangeState(EnemyState.Search);
            }
            else
            {
                ChangeState(EnemyState.Idle);
            }
        }
    }

    private void HandleIdle()
    {
        if (AgentReady) agent.isStopped = true;
        stateTimer -= Time.deltaTime;

        if (stateTimer <= 0)
        {
            float rand = Random.value;
            if (rand < 0.4f) ChangeState(EnemyState.Walk);
            else if (rand < 0.7f) ChangeState(EnemyState.Run);
            else stateTimer = Random.Range(1.5f, idleTimeMax);
        }
    }

    private void HandleWalk()
    {
        if (AgentReady)
        {
            agent.isStopped = false;
            agent.speed = 2.0f;
        }

        if (!hasDestination)
        {
            Vector3 randomDirection = Random.insideUnitSphere * walkRadius + transform.position;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomDirection, out hit, walkRadius, 1))
            {
                if (AgentReady) agent.SetDestination(hit.position);
                hasDestination = true;
            }
        }

        if (hasDestination && AgentReady && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f)
        {
            hasDestination = false;
            float rand = Random.value;
            if (rand < 0.4f) ChangeState(EnemyState.Idle);
            else if (rand < 0.8f) ChangeState(EnemyState.Walk);
            else ChangeState(EnemyState.Run);
        }
    }

    private void HandleRun()
    {
        // TÍNH NĂNG MỚI: Quay về trạng thái tuần tra thông minh nếu mục tiêu đang đuổi bị chết
        if (targetPlayer != null)
        {
            SimplePlayerTest ps = targetPlayer.GetComponentInParent<SimplePlayerTest>();
            if (ps != null && ps.CurrentHealth <= 0)
            {
                targetPlayer = null;
                ChangeState(EnemyState.Idle);
                return;
            }
        }
        if (targetPlayer == null)
        {
            if (AgentReady) { agent.isStopped = false; agent.speed = 4.0f; }
            if (!hasDestination)
            {
                Vector3 randomDirection = Random.insideUnitSphere * walkRadius * 1.5f + transform.position;
                NavMeshHit hit;
                if (NavMesh.SamplePosition(randomDirection, out hit, walkRadius * 1.5f, 1))
                {
                    if (AgentReady) agent.SetDestination(hit.position);
                    hasDestination = true;
                }
            }
            if (hasDestination && AgentReady && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                hasDestination = false;
                ChangeState(Random.value < 0.6f ? EnemyState.Idle : EnemyState.Walk);
            }
            return;
        }

        Vector3 lookDir = (targetPlayer.position - transform.position); lookDir.y = 0;
        if (lookDir != Vector3.zero)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 15f);

        if (isDodging)
        {
            dodgeTimer -= Time.deltaTime;
            if (dodgeTimer <= 0)
            {
                isDodging = false;
                if (AgentReady) agent.speed = IsEnragedValue ? 7.5f : 5f;
            }
            return;
        }

        if (AgentReady) { agent.isStopped = false; agent.speed = IsEnragedValue ? 7.5f : 5f; }

        float distanceToPlayer = Vector3.Distance(transform.position, targetPlayer.position);

        if (distanceToPlayer <= 8f)
        {
            tacticalTimer -= Time.deltaTime;
            if (tacticalTimer <= 0)
            {
                float rand = Random.value;
                if (rand < 0.5f) tacticalState = 0;
                else if (rand < 0.75f) tacticalState = 1;
                else tacticalState = 2;
                tacticalTimer = Random.Range(1.2f, 2.2f);
            }

            if (tacticalState == 0)
            {
                if (AgentReady) agent.SetDestination(targetPlayer.position);
            }
            else
            {
                Vector3 toPlayer = (targetPlayer.position - transform.position).normalized;
                Vector3 tangent = new Vector3(-toPlayer.z, 0, toPlayer.x);
                float sideDir = (tacticalState == 1) ? 1f : -1f;
                Vector3 targetOffset = targetPlayer.position - toPlayer * 2.5f + tangent * sideDir * 3f;
                NavMeshHit hit;
                if (NavMesh.SamplePosition(targetOffset, out hit, 4f, NavMesh.AllAreas))
                { if (AgentReady) agent.SetDestination(hit.position); }
                else
                { if (AgentReady) agent.SetDestination(targetPlayer.position); }
            }
        }
        else
        {
            if (AgentReady) agent.SetDestination(targetPlayer.position);
        }

        // ĐOẠN CODE ĐÚNG SAU KHI SỬA
        if (distanceToPlayer <= attackRange)
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
        if (AgentReady) { agent.isStopped = false; agent.speed = IsEnragedValue ? 5.5f : 4f; agent.SetDestination(lastKnownPlayerPosition); }

        if (AgentReady && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
        {
            agent.isStopped = true;
            searchTimer -= Time.deltaTime;
            searchLookTimer -= Time.deltaTime;
            if (searchLookTimer <= 0) { searchLookDirection = -searchLookDirection; searchLookTimer = 0.8f; }
            transform.Rotate(Vector3.up, searchLookDirection * 120f * Time.deltaTime);
            if (searchTimer <= 0) ChangeState(EnemyState.Idle);
        }
    }

    private void HandleStagger()
    {
        if (AgentReady) agent.isStopped = true;
        staggerTimer -= Time.deltaTime;
        if (staggerTimer <= 0)
        {
            if (IsEnragedValue && !hasRoared) hasRoared = true;
            ChangeState(targetPlayer != null ? EnemyState.Run : EnemyState.Idle);
        }
    }

    private void HandleAttack()
    {
        if (targetPlayer == null) { ChangeState(EnemyState.Idle); return; }
        if (AgentReady) agent.isStopped = true;

        stateTimer -= Time.deltaTime;
        float elapsed = attackDuration - stateTimer;

        if (elapsed < attackDuration * 0.35f)
        {
            Vector3 lookDir = (targetPlayer.position - transform.position); lookDir.y = 0;
            if (lookDir != Vector3.zero)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 18f);
        }

        if (attackType.Value == 2)
        {
            if (elapsed >= 0.5f && !hasDealtDamage1) { hasDealtDamage1 = true; DealConeDamage(10f, attackRange + 0.5f, 80f, 6f); }
            if (elapsed >= 1.2f && !hasDealtDamage2) { hasDealtDamage2 = true; DealConeDamage(25f, attackRange + 1.2f, 95f, 15f); }
        }
        else
        {
            if (elapsed >= 0.5f && !hasDealtDamage1) { hasDealtDamage1 = true; DealConeDamage(15f, attackRange + 0.5f, 80f, 8f); }
        }

        if (stateTimer <= 0)
        {
            attackCooldownTimer = IsEnragedValue ? 0.4f : 0.8f;
            ChangeState(EnemyState.Run);
        }
    }

    private void DealConeDamage(float damage, float range, float angle, float knockback)
    {
        int numPlayers = Physics.OverlapSphereNonAlloc(transform.position, range, damageResults, playerLayer);
        if (numPlayers == 0)
        {
            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            int count = 0;
            foreach (var p in players)
            {
                if (count >= damageResults.Length) break;
                if (Vector3.Distance(transform.position, p.transform.position) <= range)
                {
                    Collider col = p.GetComponent<Collider>();
                    if (col != null) damageResults[count++] = col;
                }
            }
            numPlayers = count;
        }

        Vector3 eyePos = eyeTransform != null ? eyeTransform.position : transform.position + Vector3.up * 1.5f;
        for (int i = 0; i < numPlayers; i++)
        {
            Collider col = damageResults[i];
            if (col == null) continue;
            Transform player = col.transform;
            Vector3 dirToPlayer = (player.position - transform.position).normalized;
            if (Vector3.Angle(transform.forward, dirToPlayer) <= angle / 2f)
            {
                float dist = Vector3.Distance(transform.position, player.position);
                if (!Physics.Raycast(eyePos, dirToPlayer, dist, obstacleLayer))
                {
                    SimplePlayerTest playerScript = player.GetComponentInParent<SimplePlayerTest>();
                    if (playerScript != null)
                    {
                        playerScript.TakeDamage(damage);
                        Vector3 kbDir = dirToPlayer; kbDir.y = 0; kbDir.Normalize();
                        playerScript.ApplyKnockback(kbDir * knockback);
                    }
                }
            }
        }
    }

    public void TakeDamage(float damage)
    {
        // Standalone: cho phép bất kỳ ai gọi
        if (!isStandaloneMode && (!IsServer || CurrentStateValue == EnemyState.Dead)) return;
        if (isStandaloneMode && CurrentStateValue == EnemyState.Dead) return;

        CurrentHealthValue -= damage;
        if (!isStandaloneMode) hitCounter.Value++;
        else PlayHitAnimation();

        if (CurrentHealthValue <= 0) { ChangeState(EnemyState.Dead); return; }

        if (CurrentHealthValue <= maxHealth * 0.5f && !IsEnragedValue)
        {
            IsEnragedValue = true;
            staggerTimer = 1.2f;
            ChangeState(EnemyState.Stagger);
            if (!isStandaloneMode) PlayRoarAnimationClientRpc();
            else PlayRoarAnimation();
            return;
        }

        float now = Time.time;
        if (now - lastDamageTime > 3f) recentHitCount = 0;
        recentHitCount++;
        lastDamageTime = now;

        bool shouldStagger = (damage >= 25f || recentHitCount >= 3) && (CurrentStateValue != EnemyState.Stagger);
        if (shouldStagger)
        {
            recentHitCount = 0;
            staggerTimer = 0.6f;
            ChangeState(EnemyState.Stagger);
        }
        else
        {
            if (!isDodging && Random.value < 0.3f && (CurrentStateValue == EnemyState.Run || CurrentStateValue == EnemyState.Walk))
                ExecuteDodge();
        }
    }

    private void PlayHitAnimation()
    {
        if (anim != null && CurrentStateValue != EnemyState.Stagger)
            anim.SetTrigger("Hit");
    }

    private void ExecuteDodge()
    {
        if (targetPlayer == null) return;
        Vector3 toPlayer = (targetPlayer.position - transform.position).normalized;
        Vector3 perpendicular = new Vector3(-toPlayer.z, 0, toPlayer.x);
        if (Random.value < 0.5f) perpendicular = -perpendicular;
        Vector3 dodgeTarget = transform.position + perpendicular * 3f;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(dodgeTarget, out hit, 3f, NavMesh.AllAreas))
        {
            isDodging = true;
            dodgeTimer = 0.35f;
            if (AgentReady) { agent.isStopped = false; agent.speed = IsEnragedValue ? 14f : 10f; agent.SetDestination(hit.position); }
        }
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
        if (CurrentStateValue == EnemyState.Attack && newState != EnemyState.Attack)
            DisableWeaponHitbox();

        CurrentStateValue = newState;

        if (newState == EnemyState.Idle) { stateTimer = Random.Range(2f, idleTimeMax); if (AgentReady) agent.isStopped = true; }
        if (newState == EnemyState.Walk) { hasDestination = false; if (AgentReady) agent.isStopped = false; }
        if (newState == EnemyState.Run) { isDodging = false; if (AgentReady) agent.isStopped = false; }
        if (newState == EnemyState.Search) { searchTimer = 3.0f; searchLookTimer = 0f; if (AgentReady) agent.isStopped = false; }
        if (newState == EnemyState.Stagger) { if (AgentReady) agent.isStopped = true; if (staggerTimer <= 0) staggerTimer = 0.6f; }
        if (newState == EnemyState.Attack)
        {
            if (AgentReady) agent.isStopped = true;
            hasDealtDamage1 = false; hasDealtDamage2 = false;
            if (IsEnragedValue && Random.value < 0.4f)
            { if (!isStandaloneMode) attackType.Value = 2; attackDuration = 1.8f; }
            else
            {
                int type = isNextAttackLeft ? 0 : 1;
                if (!isStandaloneMode) attackType.Value = type;
                isNextAttackLeft = !isNextAttackLeft;
                attackDuration = 1.1f;
            }
            stateTimer = attackDuration;
            if (!isStandaloneMode) PlayAttackAnimationClientRpc(!isStandaloneMode && attackType.Value == 2 ? 2 : (isNextAttackLeft ? 1 : 0));
            else PlayAttackAnimation(isNextAttackLeft ? 0 : 1);
        }
        if (newState == EnemyState.Dead) Die();
    }

    // ------------------------------------------------------------------
    //  Animation Sync
    // ------------------------------------------------------------------
    private void OnStateChanged(EnemyState previousValue, EnemyState newValue)
    {
        clientLocalState = (EnemyState)(-1);
    }

    private bool SyncAnimationState(EnemyState newState)
    {
        if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null) return false;
        anim.ResetTrigger("Idle"); anim.ResetTrigger("Walk"); anim.ResetTrigger("Run"); anim.ResetTrigger("Hit");
        switch (newState)
        {
            case EnemyState.Idle: anim.SetTrigger("Idle"); break;
            case EnemyState.Walk: anim.SetTrigger("Walk"); break;
            case EnemyState.Search: anim.SetTrigger("Run"); break;
            case EnemyState.Run: anim.SetTrigger("Run"); break;
            case EnemyState.Stagger:
                if (IsEnragedValue && !hasRoared) { anim.ResetTrigger("Combo"); anim.SetTrigger("Combo"); }
                else anim.SetTrigger("Hit");
                break;
            case EnemyState.Dead: anim.SetTrigger("Die"); break;
        }
        return true;
    }

    private void PlayAttackAnimation(int type)
    {
        if (anim == null) return;
        anim.ResetTrigger("AttLeft"); anim.ResetTrigger("quai1Attackphai"); anim.ResetTrigger("Combo");
        if (type == 0) anim.SetTrigger("AttLeft");
        else if (type == 1) anim.SetTrigger("quai1Attackphai");
        else anim.SetTrigger("Combo");
    }

    private void PlayRoarAnimation()
    {
        if (anim == null) return;
        anim.ResetTrigger("Hit"); anim.ResetTrigger("Combo");
        anim.SetTrigger("Combo");
    }

    [ClientRpc]
    private void PlayAttackAnimationClientRpc(int type)
    {
        PlayAttackAnimation(type);
    }

    [ClientRpc]
    private void PlayRoarAnimationClientRpc()
    {
        PlayRoarAnimation();
    }

    // ------------------------------------------------------------------
    //  Hitbox Control (Animation Events)
    // ------------------------------------------------------------------
    public void EnableWeaponHitbox() { if (hammerHitbox != null) hammerHitbox.SetActive(true); EnableLeftWeaponHitbox(); EnableRightWeaponHitbox(); }
    public void DisableWeaponHitbox() { if (hammerHitbox != null) hammerHitbox.SetActive(false); DisableLeftWeaponHitbox(); DisableRightWeaponHitbox(); }
    public void EnableLeftWeaponHitbox() { if (hammerHitboxLeft != null) hammerHitboxLeft.SetActive(true); }
    public void DisableLeftWeaponHitbox() { if (hammerHitboxLeft != null) hammerHitboxLeft.SetActive(false); }
    public void EnableRightWeaponHitbox() { if (hammerHitboxRight != null) hammerHitboxRight.SetActive(true); }
    public void DisableRightWeaponHitbox() { if (hammerHitboxRight != null) hammerHitboxRight.SetActive(false); }

    private void OnDrawGizmosSelected()
    {
        if (eyeTransform != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(eyeTransform.position, sightRange);
            Vector3 leftLimit = Quaternion.AngleAxis(-fieldOfView / 2f, Vector3.up) * transform.forward;
            Vector3 rightLimit = Quaternion.AngleAxis(fieldOfView / 2f, Vector3.up) * transform.forward;
            Gizmos.DrawRay(eyeTransform.position, leftLimit * sightRange);
            Gizmos.DrawRay(eyeTransform.position, rightLimit * sightRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}