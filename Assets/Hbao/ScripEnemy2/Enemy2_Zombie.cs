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
        Search,    // Trạng thái săn tìm player tại vị trí cuối cùng khi mất dấu
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

    [Header("Advanced AI Sync")]
    // Đồng bộ hit để mọi client đều thấy hoạt ảnh dính đòn "Anhit"
    public NetworkVariable<int> hitCounter = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    // Đồng bộ kiểu tấn công (0: Cào cơ bản)
    public NetworkVariable<int> attackType = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Components")]
    public NavMeshAgent agent;
    public Animator anim;
    public Transform eyeTransform; // Vị trí mắt để dò quét Player bằng Raycast
    
    [Header("Melee Hitbox Settings")]
    public GameObject clawHitbox; // Kéo GameObject vùng đánh (box collider ở tay/vuốt) vào đây

    [Header("AI Settings")]
    public float sightRange = 12f;      // Tầm nhìn xa của Zombie
    public float fieldOfView = 110f;    // Góc nhìn rộng của Zombie
    public float attackRange = 1.8f;    // Khoảng cách cào cơ bản
    public float walkRadius = 8f;       // Bán kính đi dạo tuần tra ngẫu nhiên
    public float idleTimeMax = 5f;      // Thời gian đứng im tối đa
    
    [Header("Layers")]
    public LayerMask playerLayer;       // Layer của Player
    public LayerMask obstacleLayer;     // Layer của vật cản

    [Header("Zombie Special Combat")]
    private Transform targetPlayer;
    private float stateTimer;
    private bool hasDestination;

    // Biến điều khiển Pro AI
    private Vector3 lastKnownPlayerPosition;
    private float searchTimer;
    private float searchLookTimer;
    private float searchLookDirection = 1f;
    
    private float detectionTimer;
    private const float DETECTION_INTERVAL = 0.15f; // Quét 0.15s một lần để tiết kiệm CPU tối đa

    private float staggerTimer;
    private float attackCooldownTimer;
    private float tacticalTimer;
    private int tacticalState = 0; // 0: Lao trực diện, 1: Đi vòng hông trái, 2: Đi vòng hông phải

    private float lastDamageTime;
    private int recentHitCount;

    private bool hasDealtDamage;
    private float attackDuration;

    // Bộ đệm tránh phân bổ rác (GC Alloc) khi quét va chạm
    private readonly Collider[] detectionResults = new Collider[8];
    private readonly Collider[] damageResults = new Collider[8];

    public override void OnNetworkSpawn()
    {
        // Lắng nghe sự thay đổi trạng thái để đồng bộ hoạt ảnh trên các Client
        currentState.OnValueChanged += OnStateChanged;

        // Khởi tạo hoạt ảnh ban đầu khớp với trạng thái hiện tại (đặc biệt cho người chơi vào sau)
        OnStateChanged(currentState.Value, currentState.Value);

        // Tối ưu hóa đồng bộ chuyển động siêu nhỏ cho mọi Client cùng quan sát
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
            ChangeState(EnemyState.Idle);
        }

        // Đảm bảo hitbox vuốt ban đầu được tắt
        if (clawHitbox != null)
        {
            clawHitbox.SetActive(false);
        }

        // Đồng bộ dính đòn (Hit) chạy hoạt ảnh "Anhit" cho toàn bộ Client
        hitCounter.OnValueChanged += (oldVal, newVal) => {
            if (anim != null) 
            {
                anim.ResetTrigger("Anhit");
                anim.SetTrigger("Anhit");
            }
        };
    }

    public override void OnNetworkDespawn()
    {
        currentState.OnValueChanged -= OnStateChanged;
    }

    private void Update()
    {
        // Chỉ Server mới được phép tính toán AI
        if (!IsServer) return;

        // Giảm thời gian hồi đòn đánh
        if (attackCooldownTimer > 0) attackCooldownTimer -= Time.deltaTime;

        // Quét Player theo chu kỳ tối ưu thay vì mỗi frame giúp tăng hiệu năng đáng kể
        detectionTimer -= Time.deltaTime;
        if (detectionTimer <= 0)
        {
            detectionTimer = DETECTION_INTERVAL;
            DetectPlayer();
        }

        // Xử lý logic AI tùy theo trạng thái hiện tại
        switch (currentState.Value)
        {
            case EnemyState.Idle:
                HandleIdle();
                break;
            case EnemyState.Walk:
                HandleWalk();
                break;
            case EnemyState.Run:
                HandleRun();
                break;
            case EnemyState.Search:
                HandleSearch();
                break;
            case EnemyState.Stagger:
                HandleStagger();
                break;
            case EnemyState.Attack:
                HandleAttack();
                break;
        }
    }

    #region AI Logic (Server Side)

    private void DetectPlayer()
    {
        // Nếu đã chết hoặc đang bị stagger thì tạm dừng phát hiện
        if (currentState.Value == EnemyState.Dead || currentState.Value == EnemyState.Stagger) return;

        // Nếu đang trong đòn tấn công thì không đổi mục tiêu
        if (currentState.Value == EnemyState.Attack) return;

        int numPlayers = Physics.OverlapSphereNonAlloc(transform.position, sightRange, detectionResults, playerLayer);
        bool playerFound = false;

        for (int i = 0; i < numPlayers; i++)
        {
            Collider p = detectionResults[i];
            if (p == null) continue;
            Transform potentialTarget = p.transform;

            // Bỏ qua nếu Player đã chết
            SimplePlayerTest playerScript = potentialTarget.GetComponentInParent<SimplePlayerTest>();
            if (playerScript != null && playerScript.currentHealth.Value <= 0)
            {
                continue;
            }

            Vector3 directionToTarget = (potentialTarget.position - eyeTransform.position).normalized;

            // Kiểm tra xem player có nằm trong góc nhìn rộng (Field of View) của Zombie không
            if (Vector3.Angle(transform.forward, directionToTarget) < fieldOfView / 2)
            {
                float distanceToTarget = Vector3.Distance(eyeTransform.position, potentialTarget.position);

                // Bắn Raycast từ mắt tới player xem có bị khuất vật cản không
                if (!Physics.Raycast(eyeTransform.position, directionToTarget, distanceToTarget, obstacleLayer))
                {
                    targetPlayer = potentialTarget;
                    playerFound = true;
                    if (currentState.Value != EnemyState.Run)
                    {
                        ChangeState(EnemyState.Run);
                    }
                    break; 
                }
            }
        }

        // Nếu mất dấu mục tiêu khi đang chạy đuổi
        if (!playerFound && currentState.Value == EnemyState.Run)
        {
            if (targetPlayer != null)
            {
                // Chuyển sang trạng thái Tìm kiếm thông minh tại vị trí mất dấu cuối cùng
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
        if (agent.isActiveAndEnabled) agent.isStopped = true;
        stateTimer -= Time.deltaTime;

        if (stateTimer <= 0)
        {
            // Đi tuần ngẫu nhiên (50% tiếp tục đứng im nghỉ ngơi, 50% đi dạo chơi)
            int rand = Random.Range(0, 2);
            if (rand == 0)
            {
                ChangeState(EnemyState.Walk);
            }
            else
            {
                stateTimer = Random.Range(2f, idleTimeMax); // Reset đứng im
            }
        }
    }

    private void HandleWalk()
    {
        if (agent.isActiveAndEnabled)
        {
            agent.isStopped = false;
            agent.speed = 1.5f; // Đi dạo lờ đờ
        }

        if (!hasDestination)
        {
            Vector3 randomDirection = Random.insideUnitSphere * walkRadius;
            randomDirection += transform.position;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomDirection, out hit, walkRadius, 1))
            {
                if (agent.isActiveAndEnabled) agent.SetDestination(hit.position);
                hasDestination = true;
            }
        }

        // Nếu đã đến vị trí tuần tra
        if (hasDestination && agent.isActiveAndEnabled && agent.remainingDistance <= agent.stoppingDistance)
        {
            hasDestination = false;
            ChangeState(EnemyState.Idle);
        }
    }

    private void HandleRun()
    {
        if (targetPlayer == null)
        {
            ChangeState(EnemyState.Idle);
            return;
        }

        // Kiểm tra xem máu có dưới 40% không để kích hoạt TẤN CÔNG ĐIÊN CUỒNG
        bool isFrantic = currentHealth.Value <= maxHealth * 0.4f;

        if (agent.isActiveAndEnabled)
        {
            agent.isStopped = false;
            agent.speed = isFrantic ? 6.5f : 4.5f; // Chạy điên cuồng nhanh hơn khi máu thấp!
        }

        float distanceToPlayer = Vector3.Distance(transform.position, targetPlayer.position);

        // Chiến thuật di chuyển vòng sườn (Circle Chase) để tránh đòn bắn trực diện của Player
        if (distanceToPlayer <= 6f)
        {
            tacticalTimer -= Time.deltaTime;
            if (tacticalTimer <= 0)
            {
                // Chọn cách tiếp cận ngẫu nhiên (50% chạy thẳng, 50% đi vòng sườn trái/phải)
                float rand = Random.value;
                if (rand < 0.5f) tacticalState = 0;
                else if (rand < 0.75f) tacticalState = 1;
                else tacticalState = 2;

                tacticalTimer = Random.Range(1f, 2f);
            }

            if (tacticalState == 0)
            {
                if (agent.isActiveAndEnabled) agent.SetDestination(targetPlayer.position);
            }
            else
            {
                // Di chuyển xiên vòng: Tính toán hướng tiếp tuyến vuông góc với Player
                Vector3 toPlayer = (targetPlayer.position - transform.position).normalized;
                Vector3 tangent = new Vector3(-toPlayer.z, 0, toPlayer.x);
                float sideDir = (tacticalState == 1) ? 1f : -1f;

                // Điểm đích bo sườn
                Vector3 targetOffset = targetPlayer.position - toPlayer * 2.2f + tangent * sideDir * 2.5f;
                
                NavMeshHit hit;
                if (NavMesh.SamplePosition(targetOffset, out hit, 3.5f, NavMesh.AllAreas))
                {
                    if (agent.isActiveAndEnabled) agent.SetDestination(hit.position);
                }
                else
                {
                    if (agent.isActiveAndEnabled) agent.SetDestination(targetPlayer.position);
                }
            }
        }
        else
        {
            // Ở xa thì đuổi trực tiếp
            if (agent.isActiveAndEnabled) agent.SetDestination(targetPlayer.position);
        }

        // Vào tầm cào cơ bản cận chiến
        if (distanceToPlayer <= attackRange && attackCooldownTimer <= 0)
        {
            ChangeState(EnemyState.Attack);
        }
    }

    private void HandleSearch()
    {
        if (agent.isActiveAndEnabled)
        {
            agent.isStopped = false;
            agent.speed = 3.5f;
            agent.SetDestination(lastKnownPlayerPosition);
        }

        // Đã đến vị trí cuối cùng nhìn thấy Player
        if (agent.isActiveAndEnabled && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
        {
            agent.isStopped = true;
            searchTimer -= Time.deltaTime;

            // Xoay người lục lọi tìm kiếm qua lại
            searchLookTimer -= Time.deltaTime;
            if (searchLookTimer <= 0)
            {
                searchLookDirection = -searchLookDirection;
                searchLookTimer = 0.6f;
            }

            transform.Rotate(Vector3.up, searchLookDirection * 150f * Time.deltaTime);

            if (searchTimer <= 0)
            {
                ChangeState(EnemyState.Idle);
            }
        }
    }

    private void HandleStagger()
    {
        if (agent.isActiveAndEnabled) agent.isStopped = true;
        staggerTimer -= Time.deltaTime;

        if (staggerTimer <= 0)
        {
            if (targetPlayer != null)
            {
                ChangeState(EnemyState.Run);
            }
            else
            {
                ChangeState(EnemyState.Idle);
            }
        }
    }

    private void HandleAttack()
    {
        if (targetPlayer == null)
        {
            ChangeState(EnemyState.Idle);
            return;
        }

        if (agent.isActiveAndEnabled) agent.isStopped = true;

        stateTimer -= Time.deltaTime;
        float elapsed = attackDuration - stateTimer;

        // Khóa góc xoay hướng đòn cào ở 40% thời gian hoạt ảnh giúp Player dễ lướt tránh
        if (elapsed < attackDuration * 0.4f)
        {
            Vector3 lookDir = (targetPlayer.position - transform.position);
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 10f);
            }
        }

        // Máu thấp cào nhanh hơn (0.24s so với 0.4s) và gây sát thương nhiều hơn (15 so với 12)
        bool isFrantic = currentHealth.Value <= maxHealth * 0.4f;
        float hitPointTime = isFrantic ? 0.24f : 0.4f;

        // Gây sát thương cào móng vuốt
        if (elapsed >= hitPointTime && !hasDealtDamage)
        {
            hasDealtDamage = true;
            float damageDealt = isFrantic ? 15f : 12f;
            DealConeDamage(damageDealt, 2.0f, 90f, 4f); // Sát thương cào diện rộng, đẩy lùi nhẹ
        }

        // Đòn đánh hoàn thành
        if (stateTimer <= 0)
        {
            // Giãn cách hồi đòn đánh: máu thấp chỉ hồi 0.15s (gần như liên tục), bình thường hồi 0.6s
            attackCooldownTimer = isFrantic ? 0.15f : 0.6f;
            ChangeState(EnemyState.Run);
        }
    }

    private void DealConeDamage(float damage, float range, float angle, float knockback)
    {
        int numPlayers = Physics.OverlapSphereNonAlloc(transform.position, range, damageResults, playerLayer);
        for (int i = 0; i < numPlayers; i++)
        {
            Collider col = damageResults[i];
            if (col == null) continue;
            Transform player = col.transform;
            Vector3 dirToPlayer = (player.position - transform.position).normalized;

            // Kiểm tra góc hình nón phía trước mặt của Zombie
            if (Vector3.Angle(transform.forward, dirToPlayer) <= angle / 2f)
            {
                float distanceToPlayer = Vector3.Distance(transform.position, player.position);
                // Đảm bảo không cào xuyên qua tường
                if (!Physics.Raycast(eyeTransform.position, dirToPlayer, distanceToPlayer, obstacleLayer))
                {
                    SimplePlayerTest playerScript = player.GetComponentInParent<SimplePlayerTest>();
                    if (playerScript != null)
                    {
                        // Gây sát thương lên Player
                        playerScript.TakeDamage(damage);
                        
                        // Đẩy lùi Player mượt mà
                        Vector3 knockbackDir = dirToPlayer;
                        knockbackDir.y = 0;
                        knockbackDir.Normalize();
                        playerScript.ApplyKnockback(knockbackDir * knockback);
                    }
                }
            }
        }
    }

    public void TakeDamage(float damage)
    {
        if (!IsServer || currentState.Value == EnemyState.Dead) return;

        currentHealth.Value -= damage;
        hitCounter.Value++; // Tăng counter đồng bộ client chạy trigger dính đòn "Anhit"

        if (currentHealth.Value <= 0)
        {
            ChangeState(EnemyState.Dead);
            return;
        }

        // Trạng thái khựng choáng khi bị đánh dồn dập
        float now = Time.time;
        if (now - lastDamageTime > 3f)
        {
            recentHitCount = 0;
        }
        recentHitCount++;
        lastDamageTime = now;

        // Bị dính đòn >= 20 HP hoặc dính 3 hit liên tiếp -> Khựng lại chơi hoạt ảnh Anhit
        bool shouldStagger = (damage >= 20f || recentHitCount >= 3) && (currentState.Value != EnemyState.Stagger);

        if (shouldStagger)
        {
            recentHitCount = 0;
            staggerTimer = 0.5f; // Choáng khựng 0.5s cắt đứt đòn cào của Zombie
            ChangeState(EnemyState.Stagger);
        }
    }

    private void Die()
    {
        if (agent.isActiveAndEnabled) agent.isStopped = true;
        
        // Hủy quái sau 2 giây chơi hoạt ảnh chết
        Invoke(nameof(DespawnEnemy), 2f);
    }

    private void DespawnEnemy()
    {
        if (IsServer && IsSpawned)
        {
            GetComponent<NetworkObject>().Despawn();
        }
    }

    private void ChangeState(EnemyState newState)
    {
        // Tự động tắt hitbox vuốt nếu trạng thái chuyển từ Attack sang trạng thái khác (tránh lỗi kẹt hitbox khi bị khựng/chết)
        if (currentState.Value == EnemyState.Attack && newState != EnemyState.Attack)
        {
            DisableClawHitbox();
        }

        currentState.Value = newState;

        // Reset các biến liên quan khi chuyển trạng thái
        if (newState == EnemyState.Idle) 
        {
            stateTimer = Random.Range(2f, idleTimeMax);
            if (agent.isActiveAndEnabled) agent.isStopped = true;
        }
        if (newState == EnemyState.Walk) 
        {
            hasDestination = false;
            if (agent.isActiveAndEnabled) agent.isStopped = false;
        }
        if (newState == EnemyState.Run) 
        {
            if (agent.isActiveAndEnabled) agent.isStopped = false;
        }
        if (newState == EnemyState.Search)
        {
            searchTimer = 3.0f; // Đi tuần kiếm tìm trong 3 giây
            searchLookTimer = 0f;
            if (agent.isActiveAndEnabled) agent.isStopped = false;
        }
        if (newState == EnemyState.Stagger)
        {
            if (agent.isActiveAndEnabled) agent.isStopped = true;
            if (staggerTimer <= 0) staggerTimer = 0.5f;
        }
        if (newState == EnemyState.Attack)
        {
            if (agent.isActiveAndEnabled) agent.isStopped = true;
            
            hasDealtDamage = false;
            attackType.Value = 0; // Luôn dùng đòn cào cơ bản

            // Khi máu thấp, đòn đánh cào nhanh điên cuồng hơn (hoạt ảnh rút ngắn còn 0.6s thay vì 1.0s)
            bool isFrantic = currentHealth.Value <= maxHealth * 0.4f;
            attackDuration = isFrantic ? 0.6f : 1.0f;

            stateTimer = attackDuration;

            // Kích hoạt animation đồng bộ qua ClientRpc
            PlayAttackAnimationClientRpc();
        }
        if (newState == EnemyState.Dead) Die();
    }

    #endregion

    #region Client Animation Sync

    private void OnStateChanged(EnemyState previousValue, EnemyState newValue)
    {
        if (anim == null) return;

        // Reset các trigger cũ để tránh kẹt hoạt ảnh
        anim.ResetTrigger("Idle");
        anim.ResetTrigger("Walk");
        anim.ResetTrigger("Run");
        anim.ResetTrigger("Anhit");
        anim.ResetTrigger("Die");

        switch (newValue)
        {
            case EnemyState.Idle:
                anim.SetTrigger("Idle"); 
                break;
            case EnemyState.Walk:
                anim.SetTrigger("Walk"); 
                break;
            case EnemyState.Run:
                anim.SetTrigger("Run");   
                break;
            case EnemyState.Stagger:
                anim.SetTrigger("Anhit"); // Dính đòn choáng khựng đồng bộ trigger "Anhit"
                break;
            case EnemyState.Dead:
                anim.SetTrigger("Die"); // Đồng bộ trigger Die hoạt ảnh chết (quai2Die)
                break;
        }
    }

    private void OnAttackTypeChanged(int oldVal, int newVal)
    {
        if (anim == null) return;
        
        anim.ResetTrigger("Attack"); 

        // Kích hoạt duy nhất trigger "Attack" để chơi hoạt ảnh cào cơ bản (quai2-attackcoban)
        anim.SetTrigger("Attack"); 
    }

    [ClientRpc]
    private void PlayAttackAnimationClientRpc()
    {
        if (anim == null) return;
        
        anim.ResetTrigger("Attack"); 
        anim.SetTrigger("Attack"); 
    }

    #endregion

    #region Animation Events & Hitbox Control

    // Hàm này được gọi từ Animation Event tại frame cào để bật Box Collider vuốt
    public void EnableClawHitbox()
    {
        if (clawHitbox != null)
        {
            clawHitbox.SetActive(true);
        }
    }

    // Hàm này được gọi từ Animation Event tại frame cào xong hoặc kết thúc đòn đánh để tắt Box Collider
    public void DisableClawHitbox()
    {
        if (clawHitbox != null)
        {
            clawHitbox.SetActive(false);
        }
    }

    #endregion

    // Vẽ visual debug các vùng quét trên màn hình Scene Editor giúp căn cự ly siêu tốt
    private void OnDrawGizmosSelected()
    {
        if (eyeTransform != null)
        {
            // Vùng nhìn thấy của Zombie (Vàng)
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(eyeTransform.position, sightRange);

            // Hai hướng giới hạn góc nhìn FOV (Vàng)
            Vector3 leftLimit = Quaternion.AngleAxis(-fieldOfView / 2f, Vector3.up) * transform.forward;
            Vector3 rightLimit = Quaternion.AngleAxis(fieldOfView / 2f, Vector3.up) * transform.forward;
            Gizmos.DrawRay(eyeTransform.position, leftLimit * sightRange);
            Gizmos.DrawRay(eyeTransform.position, rightLimit * sightRange);

            // Vùng cào móng vuốt cơ bản (Đỏ)
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
            
            // Vẽ góc nêm quét sát thương hình nón khi đang cào
            if (currentState.Value == EnemyState.Attack)
            {
                float currentAngle = 90f;
                float currentRange = 2.0f;
                Vector3 attackLeft = Quaternion.AngleAxis(-currentAngle / 2f, Vector3.up) * transform.forward;
                Vector3 attackRight = Quaternion.AngleAxis(currentAngle / 2f, Vector3.up) * transform.forward;
                Gizmos.DrawRay(transform.position, attackLeft * currentRange);
                Gizmos.DrawRay(transform.position, attackRight * currentRange);
            }
        }
    }
}
