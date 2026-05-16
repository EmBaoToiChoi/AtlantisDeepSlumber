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

    [Header("Network State")]
    // Đồng bộ trạng thái từ Server xuống tất cả Client
    public NetworkVariable<EnemyState> currentState = new NetworkVariable<EnemyState>(
        EnemyState.Idle, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server
    );

    [Header("Components")]
    public NavMeshAgent agent;
    public Animator anim;
    public Transform eyeTransform; // Vị trí mắt của Enemy để bắn Raycast

    [Header("AI Settings")]
    public float sightRange = 15f;      // Tầm nhìn xa
    public float fieldOfView = 90f;     // Góc nhìn (độ)
    public float attackRange = 2f;      // Khoảng cách ra đòn
    public float walkRadius = 10f;      // Bán kính đi dạo ngẫu nhiên
    public float idleTimeMax = 4f;      // Thời gian đứng im tối đa
    
    [Header("Layers")]
    public LayerMask playerLayer;       // Layer của Player
    public LayerMask obstacleLayer;     // Layer của vật cản (tường, đất...)

    private Transform targetPlayer;
    private float stateTimer;
    private bool hasDestination;

    public override void OnNetworkSpawn()
    {
        // Lắng nghe sự thay đổi trạng thái để chạy Animation trên các Client
        currentState.OnValueChanged += OnStateChanged;

        if (IsServer)
        {
            currentHealth.Value = maxHealth;
            ChangeState(EnemyState.Idle);
        }
    }

    public override void OnNetworkDespawn()
    {
        currentState.OnValueChanged -= OnStateChanged;
    }

    private void Update()
    {
        // Chỉ Server mới được phép tính toán AI
        if (!IsServer) return;

        // Luôn kiểm tra xem có Player nào lọt vào tầm mắt không
        DetectPlayer();

        // Xử lý logic tùy theo trạng thái hiện tại
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
            case EnemyState.Attack:
                HandleAttack();
                break;
        }
    }

    #region AI Logic (Server Side)

    private void DetectPlayer()
    {
        // Nếu đang tấn công thì tạm không tìm mục tiêu mới cho đến khi đòn đánh xong (tùy logic game)
        if (currentState.Value == EnemyState.Attack) return;

        Collider[] playersInSight = Physics.OverlapSphere(transform.position, sightRange, playerLayer);
        bool playerFound = false;

        foreach (Collider p in playersInSight)
        {
            Transform potentialTarget = p.transform;
            Vector3 directionToTarget = (potentialTarget.position - eyeTransform.position).normalized;

            // Kiểm tra xem player có nằm trong góc nhìn (Field of View) không
            if (Vector3.Angle(transform.forward, directionToTarget) < fieldOfView / 2)
            {
                float distanceToTarget = Vector3.Distance(eyeTransform.position, potentialTarget.position);

                // Bắn Raycast từ mắt tới player xem có bị khuất tường không
                if (!Physics.Raycast(eyeTransform.position, directionToTarget, distanceToTarget, obstacleLayer))
                {
                    // Nhìn thấy Player -> Đổi sang trạng thái Run để dí
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

        // Nếu mất dấu mục tiêu khi đang chạy
        if (!playerFound && currentState.Value == EnemyState.Run)
        {
            targetPlayer = null;
            ChangeState(EnemyState.Idle);
        }
    }

    private void HandleIdle()
    {
        agent.isStopped = true;
        stateTimer -= Time.deltaTime;

        if (stateTimer <= 0)
        {
            // Random: 50% tiếp tục đứng im, 50% đi dạo
            int rand = Random.Range(0, 2);
            if (rand == 0)
            {
                ChangeState(EnemyState.Walk);
            }
            else
            {
                stateTimer = Random.Range(2f, idleTimeMax); // Reset timer đứng im
            }
        }
    }

    private void HandleWalk()
    {
        agent.isStopped = false;
        agent.speed = 2f; // Tốc độ đi bộ

        if (!hasDestination)
        {
            Vector3 randomDirection = Random.insideUnitSphere * walkRadius;
            randomDirection += transform.position;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomDirection, out hit, walkRadius, 1))
            {
                agent.SetDestination(hit.position);
                hasDestination = true;
            }
        }

        // Nếu đã đến nơi
        if (hasDestination && agent.remainingDistance <= agent.stoppingDistance)
        {
            hasDestination = false;
            ChangeState(EnemyState.Idle);
        }
    }

    private void HandleRun()
    {
        if (targetPlayer == null) return;

        agent.isStopped = false;
        agent.speed = 5f; // Tốc độ chạy
        agent.SetDestination(targetPlayer.position);

        // Kiểm tra khoảng cách để tấn công (Dùng khoảng cách thay vì Sphere Collider cho mượt)
        float distanceToPlayer = Vector3.Distance(transform.position, targetPlayer.position);
        if (distanceToPlayer <= attackRange)
        {
            ChangeState(EnemyState.Attack);
        }
    }

    private void HandleAttack()
    {
        agent.isStopped = true;
        transform.LookAt(new Vector3(targetPlayer.position.x, transform.position.y, targetPlayer.position.z));

        // Đợi hết Animation tấn công (giả sử mất 1.5s), sau đó kiểm tra lại
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, targetPlayer.position);
            if (distanceToPlayer <= attackRange)
            {
                // Vẫn ở gần thì đánh tiếp
                ChangeState(EnemyState.Attack); 
            }
            else
            {
                // Chạy theo tiếp
                ChangeState(EnemyState.Run);
            }
        }
    }

    public void TakeDamage(float damage)
    {
        if (!IsServer || currentState.Value == EnemyState.Dead) return;

        currentHealth.Value -= damage;
        if (currentHealth.Value <= 0)
        {
            ChangeState(EnemyState.Dead);
        }
    }

    private void Die()
    {
        // Thực hiện logic chết (ví dụ: biến mất hoặc để lại xác)
        // Ở đây tạm thời Despawn sau 2 giây để xem animation chết
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
        currentState.Value = newState;

        // Reset các biến liên quan khi chuyển trạng thái
        if (newState == EnemyState.Idle) stateTimer = Random.Range(2f, idleTimeMax);
        if (newState == EnemyState.Walk) hasDestination = false;
        if (newState == EnemyState.Attack) stateTimer = 1.5f; // Đặt bằng với thời gian clip attack của bạn
        if (newState == EnemyState.Dead) Die();
    }

    #endregion

    #region Client Animation Sync

    // Hàm này chạy trên TẤT CẢ các Client khi NetworkVariable currentState thay đổi
    private void OnStateChanged(EnemyState previousValue, EnemyState newValue)
    {
        // Reset tất cả trigger để tránh kẹt animation
        anim.ResetTrigger("Idle");
        anim.ResetTrigger("Walk");
        anim.ResetTrigger("Run");
        anim.ResetTrigger("Attack");

        // Kích hoạt animation tương ứng (Đảm bảo bạn có setup các Trigger này trong Animator)
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
            case EnemyState.Attack:
                anim.SetTrigger("Attack");
                break;
            case EnemyState.Dead:
                anim.SetTrigger("Die"); // Đảm bảo có trigger "Die" trong Animator
                break;
        }
    }

    #endregion

    // Vẽ vùng nhìn thấy trên Editor để dễ debug
    private void OnDrawGizmosSelected()
    {
        if (eyeTransform != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(eyeTransform.position, sightRange);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}