using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class Enemy5_PhuThuy : NetworkBehaviour
{
    public enum EnemyState
    {
        Idle,
        Walk,
        Run,
        Search,    // Tìm kiếm Player tại tọa độ cuối cùng
        Stagger,   // Bị khựng / choáng khi nhận sát thương lớn
        Attack,
        Dead
    }

    [Header("Health Settings")]
    public float maxHealth = 90f; // Phù Thủy (Mage) có máu yếu hơn quái cận chiến
    public NetworkVariable<float> currentHealth = new NetworkVariable<float>(
        90f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<EnemyState> currentState = new NetworkVariable<EnemyState>(
        EnemyState.Idle, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server
    );

    [Header("Advanced AI Multiplayer Sync")]
    // Đồng bộ hit để mọi máy khách chơi hoạt ảnh dính đòn khựng lại
    public NetworkVariable<int> hitCounter = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Components")]
    public NavMeshAgent agent;
    public Animator anim;
    public Transform eyeTransform;      // Điểm bắn tia Raycast quét tầm nhìn
    public Transform staffTipTransform; // Điểm xuất phát của quả cầu phép (Staff/Hand tip)
    public Renderer[] modelRenderers;

    [Header("Spell Settings")]
    public GameObject spellProjectilePrefab; // Prefab Quả cầu phép đồng bộ mạng
    public float spellSpeed = 12f;
    public float spellDamage = 20f;

    [Header("AI Vision & Ranged Combat Settings")]
    public float sightRange = 18f;        // Tầm nhìn xa của pháp sư
    public float fieldOfView = 110f;      // Góc nhìn rộng
    public float maxAttackRange = 13f;    // Tầm đánh phép xa tối đa
    public float minAttackRange = 5.5f;   // Cự ly an toàn tối thiểu (nếu Player dưới tầm này, quái sẽ thoái lui kiting)
    public float walkRadius = 9f;         // Bán kính tuần tra dạo chơi
    public float idleTimeMax = 4.5f;      // Thời gian đứng yên tối đa

    [Header("Layers")]
    public LayerMask playerLayer;         // Layer của Player
    public LayerMask obstacleLayer;       // Layer của tường / địa hình cản tia Raycast

    [Header("Animator Parameter/Trigger Names")]
    public string idleTriggerName = "quai4IDLE";       // Khớp với trạng thái cam quai4IDLE ở Base Layer
    public string walkTriggerName = "quai5walk";       // Khớp với quai5walk
    public string runTriggerName = "quai5Run";         // Khớp với quai5Run
    public string hitTriggerName = "Quai5Anhit";       // Trigger ở layer Anhit
    public string dieTriggerName = "Quai5Die";         // Trigger ở base layer
    public string attackTriggerName = "quai5Attack";   // Trigger ở layer Attack

    // Điều khiển hành vi AI (Server Side)
    private Transform targetPlayer;
    private float stateTimer;
    private bool hasDestination;

    private Vector3 lastKnownPlayerPosition;
    private float searchTimer;
    private float searchLookTimer;
    private float searchLookDirection = 1f;

    private float detectionTimer;
    private const float DETECTION_INTERVAL = 0.15f; // Quét Player 0.15s một lần tối ưu hóa hiệu năng CPU

    private float staggerTimer;
    private float attackCooldownTimer;
    private float retreatTimer; // Hồi chiêu chạy lùi giữ cự ly

    private bool isBlinking = false; // Phản xạ lướt né dịch chuyển ma thuật
    private float blinkTimer;

    private float lastDamageTime;
    private int recentHitCount;

    private bool hasCastSpell;
    private float attackDuration = 1.2f; // Thời gian thực thi hoạt ảnh chưởng phép

    public override void OnNetworkSpawn()
    {
        // Đồng bộ hóa Animation trên các máy Client khi biến mạng thay đổi
        currentState.OnValueChanged += OnStateChanged;

        if (IsServer)
        {
            currentHealth.Value = maxHealth;
            ChangeState(EnemyState.Idle);
        }

        // Lắng nghe hitCounter để chơi hoạt ảnh ăn đòn Quai5Anhit trên mọi Client
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
    }

    private void Update()
    {
        // Chỉ Server mới xử lý bộ não AI
        if (!IsServer) return;

        // Giảm hồi chiêu đòn đánh và thoái lui
        if (attackCooldownTimer > 0) attackCooldownTimer -= Time.deltaTime;
        if (retreatTimer > 0) retreatTimer -= Time.deltaTime;

        // Xử lý Lướt dịch chuyển né tránh (Blink Dodge)
        if (isBlinking)
        {
            blinkTimer -= Time.deltaTime;
            if (blinkTimer <= 0)
            {
                isBlinking = false;
                UpdateAgentSpeed();
            }
            return;
        }

        // Dò quét tìm Player theo chu kỳ tối ưu CPU
        detectionTimer -= Time.deltaTime;
        if (detectionTimer <= 0)
        {
            detectionTimer = DETECTION_INTERVAL;
            DetectPlayer();
        }

        // Máy trạng thái AI
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

            SimplePlayerTest playerScript = playerTrans.GetComponentInParent<SimplePlayerTest>();
            if (playerScript != null && playerScript.currentHealth.Value <= 0) continue;

            Vector3 direction = (playerTrans.position - eyeTransform.position).normalized;

            // Kiểm tra FOV quét góc
            if (Vector3.Angle(transform.forward, direction) < fieldOfView / 2f)
            {
                float distance = Vector3.Distance(eyeTransform.position, playerTrans.position);

                // Bắn Raycast cản địa hình
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
        if (!found && currentState.Value == EnemyState.Run && targetPlayer != null)
        {
            lastKnownPlayerPosition = targetPlayer.position;
            targetPlayer = null;
            ChangeState(EnemyState.Search);
        }
    }

    private void HandleIdle()
    {
        if (agent.isActiveAndEnabled) agent.isStopped = true;
        stateTimer -= Time.deltaTime;

        if (stateTimer <= 0)
        {
            // Luân phiên ngẫu nhiên trạng thái tuần tra
            float rand = Random.value;
            if (rand < 0.4f)
            {
                ChangeState(EnemyState.Walk);
            }
            else if (rand < 0.7f)
            {
                ChangeState(EnemyState.Run);
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
            agent.speed = 1.8f; // Đi chậm tuần tra
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
            float rand = Random.value;
            if (rand < 0.5f) ChangeState(EnemyState.Idle);
            else if (rand < 0.8f) ChangeState(EnemyState.Run);
            else hasDestination = false;
        }
    }

    private void HandleRun()
    {
        // 1. Trường hợp tuần tra tự do không có mục tiêu Player
        if (targetPlayer == null)
        {
            if (agent.isActiveAndEnabled)
            {
                agent.isStopped = false;
                agent.speed = 3.6f;
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
                ChangeState(Random.value < 0.6f ? EnemyState.Idle : EnemyState.Walk);
            }
            return;
        }

        // 2. Trường hợp Đuổi / Kiting Player (Ranged Combat Chase Mode)
        float distance = Vector3.Distance(transform.position, targetPlayer.position);

        // A. CƠ CHẾ KITING / THOÁI LUI SIÊU THÔNG MINH CỦA PHÁP SƯ:
        // Nếu Player áp sát quá gần (< 5.5m), quái lập tức quay đầu chạy lùi để giữ khoảng cách chưởng phép an toàn
        if (distance < minAttackRange)
        {
            if (agent.isActiveAndEnabled)
            {
                agent.isStopped = false;
                agent.speed = 5.5f; // Chạy lùi giữ khoảng cách rất nhanh
            }

            // Tính toán điểm chạy lùi đối lập hướng Player đứng
            Vector3 awayDirection = (transform.position - targetPlayer.position).normalized;
            Vector3 retreatPos = transform.position + awayDirection * 4.5f;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(retreatPos, out hit, 4.5f, NavMesh.AllAreas))
            {
                if (agent.isActiveAndEnabled) agent.SetDestination(hit.position);
            }
            else
            {
                // Nếu bị kẹt góc tường, chạy dạt chéo vuông góc trái hoặc phải
                Vector3 tangent = new Vector3(-awayDirection.z, 0, awayDirection.x);
                Vector3 alternatePos = transform.position + tangent * (Random.value < 0.5f ? 4f : -4f);
                if (NavMesh.SamplePosition(alternatePos, out hit, 4f, NavMesh.AllAreas))
                {
                    if (agent.isActiveAndEnabled) agent.SetDestination(hit.position);
                }
            }
            return;
        }

        // B. CỰ LY BẮN PHÉP LÝ TƯỞNG (Từ 5.5m đến 13m):
        // Dừng lại quay mặt về Player và chưởng phép!
        if (distance >= minAttackRange && distance <= maxAttackRange)
        {
            if (agent.isActiveAndEnabled) agent.isStopped = true;

            // Xoay hướng trực tiếp mặt đối diện Player
            Vector3 lookDir = (targetPlayer.position - transform.position);
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 12f);
            }

            if (attackCooldownTimer <= 0)
            {
                ChangeState(EnemyState.Attack);
            }
            return;
        }

        // C. CỰ LY QUÁ XA (> 13m):
        // Chạy tiếp cận lại gần cho tới khi vào tầm chưởng phép
        if (distance > maxAttackRange)
        {
            if (agent.isActiveAndEnabled)
            {
                agent.isStopped = false;
                UpdateAgentSpeed();
                agent.SetDestination(targetPlayer.position);
            }
        }
    }

    private void HandleSearch()
    {
        if (agent.isActiveAndEnabled)
        {
            agent.isStopped = false;
            agent.speed = 3.2f;
            agent.SetDestination(lastKnownPlayerPosition);
        }

        if (agent.isActiveAndEnabled && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
        {
            agent.isStopped = true;
            searchTimer -= Time.deltaTime;

            // Xoay đầu tìm kiếm
            searchLookTimer -= Time.deltaTime;
            if (searchLookTimer <= 0)
            {
                searchLookDirection = -searchLookDirection;
                searchLookTimer = 0.7f;
            }

            transform.Rotate(Vector3.up, searchLookDirection * 120f * Time.deltaTime);

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

        // Quay mặt đối diện Player trong quá trình tụ lực chưởng
        Vector3 lookDir = (targetPlayer.position - transform.position);
        lookDir.y = 0;
        if (lookDir != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 10f);
        }

        // TẠO QUẢ CẦU PHÉP PHÓNG ĐI (Khớp nhịp chưởng phép ở giây 0.45s)
        if (elapsed >= 0.45f && !hasCastSpell)
        {
            hasCastSpell = true;
            LaunchSpellBall();
        }

        if (stateTimer <= 0)
        {
            // Hồi đòn chưởng: Máu thấp chưởng dồn dập (hồi 0.4s), bình thường hồi 0.9s
            attackCooldownTimer = (currentHealth.Value < maxHealth * 0.5f) ? 0.4f : 0.9f;
            ChangeState(EnemyState.Run);
        }
    }

    private void LaunchSpellBall()
    {
        if (targetPlayer == null) return;

        Vector3 spawnPoint = staffTipTransform != null ? staffTipTransform.position : transform.position + transform.forward * 1.2f + Vector3.up * 1.2f;
        Vector3 shootDirection = (targetPlayer.position + Vector3.up * 1.0f - spawnPoint).normalized;

        if (spellProjectilePrefab != null)
        {
            // Khởi tạo quả cầu phép đồng bộ qua mạng
            GameObject proj = Instantiate(spellProjectilePrefab, spawnPoint, Quaternion.LookRotation(shootDirection));
            
            // Nếu quả cầu phép có thành phần đẩy lực vật lý Rigidbody
            Rigidbody rb = proj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = shootDirection * spellSpeed;
            }

            // Kích hoạt Spawn qua Netcode trên Server để hiển thị quả cầu bay trên Client
            NetworkObject netObj = proj.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn(true);
            }
        }
        else
        {
            // FALLBACK CHUYÊN NGHIỆP: Nếu chưa có prefab quả cầu phép, tự động gây sát thương tầm xa bằng tia chưởng phép
            Debug.LogWarning("Chưa gán spellProjectilePrefab cho Phù Thủy! Đang kích hoạt chế độ Fallback chưởng phép tia quét.");
            
            // Vẽ hiệu ứng raycast mô phỏng
            if (Physics.Raycast(spawnPoint, shootDirection, out RaycastHit hit, maxAttackRange + 2f))
            {
                SimplePlayerTest playerScript = hit.collider.GetComponentInParent<SimplePlayerTest>();
                if (playerScript != null)
                {
                    playerScript.TakeDamage(spellDamage);
                    playerScript.ApplyKnockback(shootDirection * 5f);
                }
            }
        }
    }

    public void TakeDamage(float damage)
    {
        if (!IsServer || currentState.Value == EnemyState.Dead) return;

        currentHealth.Value -= damage;
        hitCounter.Value++; // Tăng biến mạng đồng bộ hiệu ứng khựng ăn đòn trên client

        if (currentHealth.Value <= 0)
        {
            ChangeState(EnemyState.Dead);
            return;
        }

        // Phân tích sát thương dồn dập
        float now = Time.time;
        if (now - lastDamageTime > 3f)
        {
            recentHitCount = 0;
        }
        recentHitCount++;
        lastDamageTime = now;

        // Bị dính đòn mạnh >= 22 HP hoặc bị ăn 3 hit dồn dập -> Bị khựng choáng ngắt chưởng phép
        bool shouldStagger = (damage >= 22f || recentHitCount >= 3) && (currentState.Value != EnemyState.Stagger);

        if (shouldStagger)
        {
            recentHitCount = 0;
            staggerTimer = 0.5f;
            ChangeState(EnemyState.Stagger);
        }
        else
        {
            // Phản xạ Dịch Chuyển / Lướt né ma thuật (Blink Dodge Reflex) ngẫu nhiên 35% khi bị bắn lúc di chuyển
            if (!isBlinking && Random.value < 0.35f && (currentState.Value == EnemyState.Run || currentState.Value == EnemyState.Walk))
            {
                ExecuteBlinkDodge();
            }
        }
    }

    private void ExecuteBlinkDodge()
    {
        if (targetPlayer == null) return;

        Vector3 toPlayer = (targetPlayer.position - transform.position).normalized;
        Vector3 perpendicular = new Vector3(-toPlayer.z, 0, toPlayer.x);
        
        // Ngẫu nhiên chọn né bên trái hay bên phải
        if (Random.value < 0.5f) perpendicular = -perpendicular;

        // Quãng đường lướt né xa 3.2m
        Vector3 blinkTarget = transform.position + perpendicular * 3.2f;
        
        NavMeshHit hit;
        if (NavMesh.SamplePosition(blinkTarget, out hit, 3.2f, NavMesh.AllAreas))
        {
            isBlinking = true;
            blinkTimer = 0.25f; // Di chuyển dịch chuyển tức thời trong 0.25s
            if (agent.isActiveAndEnabled)
            {
                agent.isStopped = false;
                agent.speed = 16f; // Tốc chạy cực lớn tạo hiệu ứng biến mất và xuất hiện cực ngầu
                agent.SetDestination(hit.position);
            }
        }
    }

    private void UpdateAgentSpeed()
    {
        if (!agent.isActiveAndEnabled) return;

        // Khi máu yếu < 50% chạy kiting giữ cự ly nhanh hơn biểu thị hoảng sợ cuồng bạo
        if (currentHealth.Value < maxHealth * 0.5f)
        {
            agent.speed = 4.8f;
        }
        else
        {
            agent.speed = 3.8f;
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
        
        // Despawn Enemy qua mạng sau 2 giây chơi hoạt ảnh chết
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
            isBlinking = false;
            hasDestination = false;
            if (agent.isActiveAndEnabled) agent.isStopped = false;
        }
        if (newState == EnemyState.Search)
        {
            searchTimer = 3.5f;
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
            hasCastSpell = false;
            stateTimer = attackDuration;

            // Kích hoạt animation chưởng trên client
            if (anim != null)
            {
                anim.ResetTrigger(attackTriggerName);
                anim.SetTrigger(attackTriggerName);
            }
        }
        if (newState == EnemyState.Dead) Die();
    }

    #endregion

    #region Client Animation Synchronization

    // Đồng bộ trigger chuyển động trên toàn bộ máy client khi currentState đổi
    private void OnStateChanged(EnemyState previousValue, EnemyState newValue)
    {
        if (anim == null) return;

        // Reset toàn bộ trigger di chuyển và trạng thái đặc biệt
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
                anim.SetTrigger(hitTriggerName); // Chơi hoạt ảnh Quai5Anhit
                break;
            case EnemyState.Dead:
                anim.SetTrigger(dieTriggerName); // Chơi hoạt ảnh Quai5Die
                break;
        }
    }

    #endregion

    // Vẽ công cụ hỗ trợ căn khoảng cách trực quan trên Unity Scene Editor
    private void OnDrawGizmosSelected()
    {
        if (eyeTransform != null)
        {
            // Tầm mắt phát hiện Player (Màu Tím Phép Thuật)
            Gizmos.color = new Color(0.85f, 0.1f, 0.95f, 0.4f);
            Gizmos.DrawWireSphere(eyeTransform.position, sightRange);

            // Góc FOV
            Vector3 leftRay = Quaternion.AngleAxis(-fieldOfView / 2f, Vector3.up) * transform.forward;
            Vector3 rightRay = Quaternion.AngleAxis(fieldOfView / 2f, Vector3.up) * transform.forward;
            Gizmos.DrawRay(eyeTransform.position, leftRay * sightRange);
            Gizmos.DrawRay(eyeTransform.position, rightRay * sightRange);

            // Vành cự ly thoái lui giữ khoảng cách kiting (Màu Vàng Hổ Phách Cảnh Báo)
            Gizmos.color = new Color(1.0f, 0.64f, 0.0f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, minAttackRange);

            // Vành cự ly tầm chưởng phép xa tối đa (Màu Xanh Cyan Phép Thuật)
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, maxAttackRange);
        }
    }
}
