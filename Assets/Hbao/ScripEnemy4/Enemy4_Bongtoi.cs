using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class Enemy4_Bongtoi : NetworkBehaviour
{
    public enum EnemyState
    {
        Idle,
        Walk,
        Run,
        Search,    // Tìm kiếm Player tại vị trí cuối cùng được phát hiện
        Stagger,   // Bị khựng / choáng khi nhận sát thương lớn
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

    [Header("Advanced AI Multiplayer Sync")]
    // Đồng bộ trigger hit để mọi client thấy hiệu ứng trúng đòn khựng lại
    public NetworkVariable<int> hitCounter = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    // Đồng bộ kiểu tấn công (0: Attack 1, 1: Attack Combo)
    public NetworkVariable<int> attackType = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Components")]
    public NavMeshAgent agent;
    public Animator anim;
    public Transform eyeTransform;    // Điểm bắn Raycast dò Player (ví dụ: ở đầu/mắt quái)
    public Renderer[] modelRenderers; // Thay đổi sắc thái để tăng hiệu ứng hình ảnh khi yếu máu hoặc cuồng nộ

    [Header("AI Vision & Combat Settings")]
    public float sightRange = 15f;      // Tầm nhìn xa phát hiện Player
    public float fieldOfView = 100f;    // Góc nhìn (độ)
    public float attackRange = 2f;      // Tầm đánh cận chiến
    public float walkRadius = 9f;       // Bán kính đi dạo ngẫu nhiên
    public float idleTimeMax = 4f;      // Thời gian đứng im nghỉ ngơi tối đa

    [Header("Layers")]
    public LayerMask playerLayer;       // Layer của Player
    public LayerMask obstacleLayer;     // Layer của tường / vật cản địa hình

    [Header("Animator Parameter/Trigger Names")]
    public string idleTriggerName = "qual4IDLE";
    public string walkTriggerName = "Qual4walk";
    public string runTriggerName = "qual4RUn";
    public string hitTriggerName = "Qual4Anhit";   // Trigger cho layer Anhit
    public string dieTriggerName = "Qual4Dle";     // Trigger cho layer Base (Chết)
    public string attack1TriggerName = "qual44attack"; // Trigger Attack 1
    public string attackComboTriggerName = "qual4combo"; // Trigger Attack Combo

    // Quản lý trạng thái AI (Chỉ tính toán và xử lý trên Server)
    private Transform targetPlayer;
    private float stateTimer;
    private bool hasDestination;

    private Vector3 lastKnownPlayerPosition;
    private float searchTimer;
    private float searchLookTimer;
    private float searchLookDirection = 1f;

    private float detectionTimer;
    private const float DETECTION_INTERVAL = 0.15f; // Quét 0.15s một lần tiết kiệm tài nguyên CPU

    private float staggerTimer;
    private float attackCooldownTimer;
    private float tacticalTimer;
    private int tacticalState = 0; // 0: Đuổi thẳng, 1: Né bo sườn trái, 2: Né bo sườn phải

    private bool isDodging = false;
    private float dodgeTimer;

    private float lastDamageTime;
    private int recentHitCount;

    private bool hasDealtDamage;
    private float attackDuration;

    public override void OnNetworkSpawn()
    {
        // Đăng ký đồng bộ hóa Animation trên Client khi biến mạng thay đổi
        currentState.OnValueChanged += OnStateChanged;
        attackType.OnValueChanged += OnAttackTypeChanged;

        if (IsServer)
        {
            currentHealth.Value = maxHealth;
            ChangeState(EnemyState.Idle);
        }

        // Đăng ký hiệu ứng dính đòn (Hit/Anhit) khi hitCounter tăng
        hitCounter.OnValueChanged += (oldVal, newVal) =>
        {
            if (anim != null)
            {
                anim.ResetTrigger(hitTriggerName);
                anim.SetTrigger(hitTriggerName);
            }
        };
    }

    public override void OnNetworkDespawn()
    {
        currentState.OnValueChanged -= OnStateChanged;
        attackType.OnValueChanged -= OnAttackTypeChanged;
    }

    private void Update()
    {
        // Chỉ Server mới có quyền tính toán và ra quyết định AI
        if (!IsServer) return;

        // Giảm thời gian hồi chiêu
        if (attackCooldownTimer > 0) attackCooldownTimer -= Time.deltaTime;

        // Xử lý né tránh lướt ngang (Dodge)
        if (isDodging)
        {
            dodgeTimer -= Time.deltaTime;
            if (dodgeTimer <= 0)
            {
                isDodging = false;
                UpdateAgentSpeed();
            }
            return;
        }

        // Quét tìm Player theo chu kỳ tối ưu CPU
        detectionTimer -= Time.deltaTime;
        if (detectionTimer <= 0)
        {
            detectionTimer = DETECTION_INTERVAL;
            DetectPlayer();
        }

        // Xử lý máy trạng thái
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

    #region AI Server Core Logic

    private void DetectPlayer()
    {
        if (currentState.Value == EnemyState.Dead || currentState.Value == EnemyState.Stagger) return;
        if (currentState.Value == EnemyState.Attack) return;

        Collider[] players = Physics.OverlapSphere(transform.position, sightRange, playerLayer);
        bool found = false;

        foreach (Collider col in players)
        {
            Transform playerTrans = col.transform;

            // Bỏ qua nếu Player đã chết
            SimplePlayerTest playerScript = playerTrans.GetComponentInParent<SimplePlayerTest>();
            if (playerScript != null && playerScript.currentHealth.Value <= 0) continue;

            Vector3 direction = (playerTrans.position - eyeTransform.position).normalized;

            // Kiểm tra góc FOV
            if (Vector3.Angle(transform.forward, direction) < fieldOfView / 2f)
            {
                float distance = Vector3.Distance(eyeTransform.position, playerTrans.position);

                // Bắn Raycast kiểm tra vật cản (tránh nhìn xuyên tường)
                if (!Physics.Raycast(eyeTransform.position, direction, distance, obstacleLayer))
                {
                    targetPlayer = playerTrans;
                    found = true;
                    if (currentState.Value != EnemyState.Run)
                    {
                        ChangeState(EnemyState.Run);
                    }
                    break;
                }
            }
        }

        // Mất dấu -> Chuyển sang tìm kiếm
        if (!found && currentState.Value == EnemyState.Run)
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
        if (agent.isActiveAndEnabled) agent.isStopped = true;
        stateTimer -= Time.deltaTime;

        if (stateTimer <= 0)
        {
            // Luân phiên ngẫu nhiên 3 trạng thái khi đứng yên hết thời gian:
            // 40% Đi dạo (Walk), 30% Đứng tiếp (Idle), 30% Chạy tuần tra nhanh (Run)
            float rand = Random.value;
            if (rand < 0.4f)
            {
                ChangeState(EnemyState.Walk);
            }
            else if (rand < 0.7f)
            {
                ChangeState(EnemyState.Run);
                // Tạo điểm tuần tra xa hơn một chút khi chạy
                hasDestination = false;
            }
            else
            {
                stateTimer = Random.Range(1.5f, idleTimeMax);
            }
        }
    }

    private void HandleWalk()
    {
        if (agent.isActiveAndEnabled)
        {
            agent.isStopped = false;
            agent.speed = 2.0f; // Đi chậm tuần tra
        }

        if (!hasDestination)
        {
            Vector3 targetPos = GetRandomNavMeshPoint(walkRadius);
            if (targetPos != Vector3.zero)
            {
                if (agent.isActiveAndEnabled) agent.SetDestination(targetPos);
                hasDestination = true;
            }
        }

        if (hasDestination && agent.isActiveAndEnabled && agent.remainingDistance <= agent.stoppingDistance)
        {
            hasDestination = false;
            // 50% đứng im (Idle), 50% chuyển sang chạy dạo (Run) hoặc đi tiếp
            float rand = Random.value;
            if (rand < 0.5f) ChangeState(EnemyState.Idle);
            else if (rand < 0.8f) ChangeState(EnemyState.Run);
            else hasDestination = false; // đi tiếp điểm mới
        }
    }

    private void HandleRun()
    {
        // 1. Trường hợp tuần tra tự do (không có mục tiêu Player)
        if (targetPlayer == null)
        {
            if (agent.isActiveAndEnabled)
            {
                agent.isStopped = false;
                agent.speed = 4.0f; // Tốc độ chạy tuần tra
            }

            if (!hasDestination)
            {
                Vector3 targetPos = GetRandomNavMeshPoint(walkRadius * 1.5f);
                if (targetPos != Vector3.zero)
                {
                    if (agent.isActiveAndEnabled) agent.SetDestination(targetPos);
                    hasDestination = true;
                }
            }

            if (hasDestination && agent.isActiveAndEnabled && agent.remainingDistance <= agent.stoppingDistance)
            {
                hasDestination = false;
                // Kết thúc chạy tuần tra -> 60% chuyển sang Idle nghỉ ngơi, 40% chuyển sang đi bộ dạo
                ChangeState(Random.value < 0.6f ? EnemyState.Idle : EnemyState.Walk);
            }
            return;
        }

        // 2. Trường hợp Đuổi theo Player (Chase mode)
        if (agent.isActiveAndEnabled)
        {
            agent.isStopped = false;
            UpdateAgentSpeed();
        }

        float distance = Vector3.Distance(transform.position, targetPlayer.position);

        // Kỹ thuật chiến thuật cao (Flanking AI):
        // Khi tiến sát Player (cự ly <= 6m), AI sẽ chạy chếch xiên bo sườn thay vì lao thẳng
        if (distance <= 6f)
        {
            tacticalTimer -= Time.deltaTime;
            if (tacticalTimer <= 0)
            {
                float rand = Random.value;
                if (rand < 0.35f) tacticalState = 0;      // Đuổi trực diện
                else if (rand < 0.68f) tacticalState = 1; // Bo sườn trái
                else tacticalState = 2;                  // Bo sườn phải

                tacticalTimer = Random.Range(0.8f, 1.8f);
            }

            if (tacticalState == 0)
            {
                if (agent.isActiveAndEnabled) agent.SetDestination(targetPlayer.position);
            }
            else
            {
                Vector3 toPlayer = (targetPlayer.position - transform.position).normalized;
                Vector3 tangent = new Vector3(-toPlayer.z, 0, toPlayer.x);
                float sideDir = (tacticalState == 1) ? 1f : -1f;

                // Điểm bo sườn chếch xiên một góc đẹp mắt
                Vector3 targetOffset = targetPlayer.position - toPlayer * 1.5f + tangent * sideDir * 2.5f;
                
                NavMeshHit hit;
                if (NavMesh.SamplePosition(targetOffset, out hit, 3f, NavMesh.AllAreas))
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
            // Cự ly xa -> Chạy đuổi trực diện
            if (agent.isActiveAndEnabled) agent.SetDestination(targetPlayer.position);
        }

        // Vào tầm đánh cận chiến và hết thời gian hồi chiêu
        if (distance <= attackRange && attackCooldownTimer <= 0)
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

        if (agent.isActiveAndEnabled && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
        {
            agent.isStopped = true;
            searchTimer -= Time.deltaTime;

            // Xoay đầu láo liên tìm kiếm
            searchLookTimer -= Time.deltaTime;
            if (searchLookTimer <= 0)
            {
                searchLookDirection = -searchLookDirection;
                searchLookTimer = 0.7f;
            }

            transform.Rotate(Vector3.up, searchLookDirection * 130f * Time.deltaTime);

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

        // Khóa hướng xoay ở 30% đầu của hoạt ảnh chuẩn bị đánh để tăng độ chính xác,
        // Sau đó người chơi có thể lướt hoặc di chuyển né sang sườn
        if (elapsed < attackDuration * 0.3f)
        {
            Vector3 lookDir = (targetPlayer.position - transform.position);
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 11f);
            }
        }

        // Tính toán gây sát thương diện rộng hình nón (khớp hoàn hảo với nhịp hạ tay của animation)
        // Attack Combo (2 hit vung tay đập liên hoàn) hoặc Attack 1
        if (attackType.Value == 1) // Combo (Dưới 50% HP)
        {
            // Nhịp vung 1: ở 0.5s
            if (elapsed >= 0.5f && !hasDealtDamage)
            {
                hasDealtDamage = true;
                DealConeDamage(14f, attackRange + 0.4f, 85f, 6f);
            }
            // Nhịp vung Slam mạnh 2: ở 1.1s
            if (elapsed >= 1.1f && hasDealtDamage)
            {
                hasDealtDamage = false; // Mượn biến để đánh dấu hit 2
                DealConeDamage(26f, attackRange + 1.1f, 95f, 13f);
            }
        }
        else // Attack 1 (Thanh máu đầy 100%)
        {
            if (elapsed >= 0.45f && !hasDealtDamage)
            {
                hasDealtDamage = true;
                DealConeDamage(16f, attackRange + 0.5f, 80f, 8f);
            }
        }

        if (stateTimer <= 0)
        {
            // Thời gian hồi chiêu: bình thường hồi 0.7s, khi máu thấp hồi siêu tốc 0.3s cực kỳ thông minh hung hãn
            attackCooldownTimer = (currentHealth.Value < maxHealth * 0.5f) ? 0.3f : 0.7f;
            ChangeState(EnemyState.Run);
        }
    }

    private void DealConeDamage(float damage, float range, float angle, float knockback)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, range, playerLayer);
        foreach (var col in hits)
        {
            Transform player = col.transform;
            Vector3 dir = (player.position - transform.position).normalized;

            // Kiểm tra góc nêm hình nón phía trước mặt quái
            if (Vector3.Angle(transform.forward, dir) <= angle / 2f)
            {
                float dist = Vector3.Distance(transform.position, player.position);
                // Tránh cào xuyên tường cản
                if (!Physics.Raycast(eyeTransform.position, dir, dist, obstacleLayer))
                {
                    SimplePlayerTest playerScript = player.GetComponentInParent<SimplePlayerTest>();
                    if (playerScript != null)
                    {
                        playerScript.TakeDamage(damage);
                        
                        // Đẩy lùi Player mượt mà bằng vật lý
                        Vector3 kbDir = dir;
                        kbDir.y = 0;
                        kbDir.Normalize();
                        playerScript.ApplyKnockback(kbDir * knockback);
                    }
                }
            }
        }
    }

    public void TakeDamage(float damage)
    {
        if (!IsServer || currentState.Value == EnemyState.Dead) return;

        currentHealth.Value -= damage;
        hitCounter.Value++; // Tăng biến mạng để đồng bộ trigger Anhit cho tất cả các Client

        if (currentHealth.Value <= 0)
        {
            ChangeState(EnemyState.Dead);
            return;
        }

        // Tính toán sát thương nhận dồn để khựng choáng
        float now = Time.time;
        if (now - lastDamageTime > 3f)
        {
            recentHitCount = 0;
        }
        recentHitCount++;
        lastDamageTime = now;

        // Nếu nhận sát thương lớn >= 25 HP hoặc bị đánh liên tiếp 3 hit -> Khựng stagger ăn hit
        bool shouldStagger = (damage >= 25f || recentHitCount >= 3) && (currentState.Value != EnemyState.Stagger);

        if (shouldStagger)
        {
            recentHitCount = 0;
            staggerTimer = 0.55f;
            ChangeState(EnemyState.Stagger);
        }
        else
        {
            // Phản xạ Lướt né tránh (Dodge Reflex) ngẫu nhiên 30% khi bị người chơi tấn công lúc đang di chuyển
            if (!isDodging && Random.value < 0.3f && (currentState.Value == EnemyState.Run || currentState.Value == EnemyState.Walk))
            {
                ExecuteDodge();
            }
        }
    }

    private void ExecuteDodge()
    {
        if (targetPlayer == null) return;

        Vector3 toPlayer = (targetPlayer.position - transform.position).normalized;
        Vector3 perpendicular = new Vector3(-toPlayer.z, 0, toPlayer.x);
        
        // Ngẫu nhiên chọn né bên trái hay bên phải
        if (Random.value < 0.5f) perpendicular = -perpendicular;

        Vector3 dodgeTarget = transform.position + perpendicular * 3.0f;
        
        NavMeshHit hit;
        if (NavMesh.SamplePosition(dodgeTarget, out hit, 3f, NavMesh.AllAreas))
        {
            isDodging = true;
            dodgeTimer = 0.35f; // Lướt nhanh trong 0.35 giây
            if (agent.isActiveAndEnabled)
            {
                agent.isStopped = false;
                agent.speed = (currentHealth.Value < maxHealth * 0.5f) ? 15f : 11f; // Chạy lướt siêu tốc độ khi máu yếu
                agent.SetDestination(hit.position);
            }
        }
    }

    private void UpdateAgentSpeed()
    {
        if (!agent.isActiveAndEnabled) return;

        // Chạy nhanh hơn khi dưới 50% HP biểu thị cuồng bạo
        if (currentHealth.Value < maxHealth * 0.5f)
        {
            agent.speed = 6.2f;
        }
        else
        {
            agent.speed = 4.8f;
        }
    }

    private Vector3 GetRandomNavMeshPoint(float radius)
    {
        Vector3 randomDirection = Random.insideUnitSphere * radius;
        randomDirection += transform.position;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, radius, 1))
        {
            return hit.position;
        }
        return Vector3.zero;
    }

    private void Die()
    {
        if (agent.isActiveAndEnabled) agent.isStopped = true;
        // Tắt va chạm hoặc thực hiện các hiệu ứng chết khác ở đây nếu cần
        
        // Hủy đối tượng qua mạng sau 2 giây chơi hoàn tất hoạt ảnh chết
        Invoke(nameof(DespawnEnemy), 2.0f);
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
        currentState.Value = newState;

        if (newState == EnemyState.Idle) 
        {
            stateTimer = Random.Range(1.5f, idleTimeMax);
            if (agent.isActiveAndEnabled) agent.isStopped = true;
        }
        if (newState == EnemyState.Walk) 
        {
            hasDestination = false;
            if (agent.isActiveAndEnabled) agent.isStopped = false;
        }
        if (newState == EnemyState.Run) 
        {
            isDodging = false;
            hasDestination = false;
            if (agent.isActiveAndEnabled) agent.isStopped = false;
        }
        if (newState == EnemyState.Search)
        {
            searchTimer = 3.5f; // Tìm kiếm trong 3.5 giây
            searchLookTimer = 0f;
            if (agent.isActiveAndEnabled) agent.isStopped = false;
        }
        if (newState == EnemyState.Stagger)
        {
            if (agent.isActiveAndEnabled) agent.isStopped = true;
            if (staggerTimer <= 0) staggerTimer = 0.55f;
        }
        if (newState == EnemyState.Attack)
        {
            hasDealtDamage = false;

            float hpPercent = currentHealth.Value / maxHealth;

            // QUYẾT ĐỊNH ĐÒN ĐÁNH THEO LƯỢNG MÁU HP:
            // 1. Khi còn đủ 100% HP -> Chắc chắn sử dụng Attack 1 (qual44attack)
            // 2. Khi HP dưới 50% -> Chắc chắn sử dụng Attack Combo (qual4combo)
            // 3. Khi HP ở khoảng giữa (50% <= HP < 100%) -> Ưu tiên Attack 1 hoặc xoay vòng ngẫu nhiên
            if (hpPercent >= 1.0f)
            {
                attackType.Value = 0; // Attack 1
                attackDuration = 1.0f; // Khớp thời lượng anim qual44attack
            }
            else if (hpPercent < 0.5f)
            {
                attackType.Value = 1; // Attack Combo
                attackDuration = 1.7f; // Khớp thời lượng anim qual4combo
            }
            else
            {
                // Khoảng giữa 50% - 99%: sử dụng đòn vung đơn Attack 1 làm mặc định chiến đấu
                attackType.Value = 0; 
                attackDuration = 1.0f;
            }

            stateTimer = attackDuration;

            // Client vung tay tấn công đồng bộ ngay lập tức
            OnAttackTypeChanged(0, attackType.Value);
        }
        if (newState == EnemyState.Dead) Die();
    }

    #endregion

    #region Client Animation Synchronization

    // Hàm chạy trên toàn bộ máy Client khi currentState đổi để đồng bộ Animator chuyển động
    private void OnStateChanged(EnemyState previousValue, EnemyState newValue)
    {
        if (anim == null) return;

        // Reset toàn bộ trigger cũ để tránh xung đột chuyển động
        anim.ResetTrigger(idleTriggerName);
        anim.ResetTrigger(walkTriggerName);
        anim.ResetTrigger(runTriggerName);
        anim.ResetTrigger(hitTriggerName);
        anim.ResetTrigger(dieTriggerName);

        switch (newValue)
        {
            case EnemyState.Idle:
                anim.SetTrigger(idleTriggerName); 
                break;
            case EnemyState.Walk:
                anim.SetTrigger(walkTriggerName); 
                break;
            case EnemyState.Run:
                anim.SetTrigger(runTriggerName);   
                break;
            case EnemyState.Stagger:
                anim.SetTrigger(hitTriggerName); // Chạy hoạt ảnh Qual4Anhit
                break;
            case EnemyState.Dead:
                anim.SetTrigger(dieTriggerName); // Chạy hoạt ảnh Qual4Dle
                break;
        }
    }

    // Hàm chạy trên toàn bộ client khi đổi đòn đánh để đồng bộ hoạt ảnh vung tay đấm/chém
    private void OnAttackTypeChanged(int oldVal, int newVal)
    {
        if (anim == null) return;

        anim.ResetTrigger(attack1TriggerName);
        anim.ResetTrigger(attackComboTriggerName);

        if (newVal == 0)
        {
            anim.SetTrigger(attack1TriggerName); // Kích hoạt qual44attack
        }
        else if (newVal == 1)
        {
            anim.SetTrigger(attackComboTriggerName); // Kích hoạt qual4combo
        }
    }

    #endregion

    // Hiển thị công cụ trực quan hóa tầm nhìn trên Unity Scene Editor hỗ trợ lập trình viên
    private void OnDrawGizmosSelected()
    {
        if (eyeTransform != null)
        {
            // Tầm quét Player (Màu Tím huyền bí)
            Gizmos.color = new Color(0.6f, 0.2f, 0.8f, 0.4f);
            Gizmos.DrawWireSphere(eyeTransform.position, sightRange);

            // Góc nhìn FOV
            Vector3 leftRay = Quaternion.AngleAxis(-fieldOfView / 2f, Vector3.up) * transform.forward;
            Vector3 rightRay = Quaternion.AngleAxis(fieldOfView / 2f, Vector3.up) * transform.forward;
            Gizmos.DrawRay(eyeTransform.position, leftRay * sightRange);
            Gizmos.DrawRay(eyeTransform.position, rightRay * sightRange);

            // Tầm đòn đánh cận chiến (Màu Đỏ)
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}
