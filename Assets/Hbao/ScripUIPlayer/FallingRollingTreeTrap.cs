using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.AI;

/// <summary>
/// Script điều khiển Cây Ngã & Cây Lăn theo Trigger Box (Interactive Environment Trap).
/// 1. Player chạm Box 1 (fallTriggerBox) -> Cây ngã xuống đất.
/// 2. Player bị cây đè trúng -> Trừ 5 HP (duy nhất 1 lần) & dời vị trí Player ra bên cạnh an toàn để không bị đẩy chìm xuống map.
/// 3. Player chạm Box 2 (rollTriggerBox) -> Cây lăn về phía trước.
/// 4. Cây lăn chạm Tường (wallLayer / Tag Wall / Obstacle) -> Nằm yên tại chỗ, KHÔNG bị Destroy.
/// Đồng bộ hoàn hảo trên cả Standalone (Chơi đơn) và Netcode (Multiplayer).
/// </summary>
public class FallingRollingTreeTrap : NetworkBehaviour
{
    public enum TreeState { Standing, Falling, FallenOnGround, Rolling, StoppedAtWall }

    [Header("--- Component Cây ---")]
    [Tooltip("Transform phần thân cây (nếu để trống sẽ dùng chính GameObject này)")]
    public Transform treeMesh;
    
    [Tooltip("Collider gây sát thương của cây (isTrigger = true)")]
    public Collider treeDamageCollider;

    [Header("--- Box Triggers Kích Hoạt ---")]
    [Tooltip("Box 1: Khi Player đi vào vùng này -> Cây ngã xuống đất")]
    public Collider fallTriggerBox;

    [Tooltip("Box 2: Khi Player đi vào vùng này -> Cây bắt đầu lăn về phía trước")]
    public Collider rollTriggerBox;

    [Header("--- Cấu Hình Cây Ngã (Phase 1) ---")]
    [Tooltip("Góc ngã xuống đất (ví dụ 90, 0, 0)")]
    public Vector3 fallRotationAngle = new Vector3(90f, 0f, 0f);

    [Tooltip("Tốc độ ngã của cây")]
    public float fallSpeed = 4.0f;

    [Tooltip("Sát thương khi cây đè trúng Player (Trừ 5 HP duy nhất 1 lần)")]
    public float crushDamage = 5.0f;

    [Tooltip("Khoảng cách đẩy Player ra bên cạnh khi bị đè để không bị lọt/chìm map")]
    public float safeNudgeDistance = 2.2f;

    [Header("--- Cấu Hình Cây Lăn (Phase 2) ---")]
    [Tooltip("Hướng lăn về phía trước (mặc định theo hướng forward của cây)")]
    public Vector3 rollDirection = Vector3.forward;

    [Tooltip("Tốc độ di chuyển khi lăn")]
    public float rollMoveSpeed = 6.5f;

    [Tooltip("Tốc độ xoay tròn của thân cây khi lăn")]
    public float rollRotationSpeed = 450.0f;

    [Tooltip("Layer tường / chướng ngại vật để cây đụng vào sẽ nằm yên tại chỗ")]
    public LayerMask wallLayer;

    // Quản lý trạng thái Network & Standalone
    private NetworkVariable<TreeState> currentStateNet = new NetworkVariable<TreeState>(
        TreeState.Standing, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private TreeState localState = TreeState.Standing;
    private bool isStandaloneMode = false;

    private bool IsNetworkActive => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    private bool IsServerAuth => isStandaloneMode || (IsNetworkActive && IsServer);

    public TreeState CurrentState
    {
        get => isStandaloneMode ? localState : currentStateNet.Value;
        private set
        {
            if (isStandaloneMode) localState = value;
            else if (IsServer) currentStateNet.Value = value;
        }
    }

    private Quaternion targetFallRotation;
    private Quaternion initialRotation;
    private HashSet<GameObject> hitPlayersThisFall = new HashSet<GameObject>();

    private TriggerProxy fallProxy;
    private TriggerProxy rollProxy;
    private TriggerProxy damageProxy;

    private void Awake()
    {
        if (treeMesh == null) treeMesh = transform;
        initialRotation = treeMesh.rotation;
        targetFallRotation = initialRotation * Quaternion.Euler(fallRotationAngle);

        if (wallLayer == 0)
        {
            wallLayer = LayerMask.GetMask("Default", "Environment", "Wall", "Obstacle", "Ground");
        }
    }

    private void Start()
    {
        if (!IsNetworkActive)
        {
            isStandaloneMode = true;
        }

        EnsureValidTreeCollider();
        SetupTriggerProxies();
    }

    private void EnsureValidTreeCollider()
    {
        if (treeDamageCollider == null)
        {
            treeDamageCollider = treeMesh.GetComponent<Collider>();
        }

        // Nếu chưa gán Collider hoặc Collider nhỏ, tự tạo/chỉnh BoxCollider ôm trọn chiều dài cây
        if (treeDamageCollider == null)
        {
            BoxCollider autoBox = treeMesh.gameObject.AddComponent<BoxCollider>();
            autoBox.isTrigger = true;

            Renderer ren = treeMesh.GetComponentInChildren<Renderer>();
            if (ren != null)
            {
                autoBox.center = treeMesh.InverseTransformPoint(ren.bounds.center);
                autoBox.size = treeMesh.InverseTransformVector(ren.bounds.size);
                // Nới rộng một chút cho dễ đè trúng Player
                Vector3 sz = autoBox.size;
                sz.x = Mathf.Max(sz.x, 1.2f);
                sz.z = Mathf.Max(sz.z, 1.2f);
                autoBox.size = sz;
            }
            else
            {
                autoBox.size = new Vector3(1.5f, 10f, 1.5f);
                autoBox.center = new Vector3(0f, 5f, 0f);
            }
            treeDamageCollider = autoBox;
        }
    }

    public override void OnNetworkSpawn()
    {
        isStandaloneMode = false;
        currentStateNet.OnValueChanged += OnTreeStateChanged;
    }

    private void OnTreeStateChanged(TreeState oldState, TreeState newState)
    {
        if (newState == TreeState.FallenOnGround)
        {
            treeMesh.rotation = targetFallRotation;
        }
    }

    private void SetupTriggerProxies()
    {
        // 1. Box 1: Fall Trigger
        if (fallTriggerBox != null)
        {
            fallProxy = fallTriggerBox.gameObject.GetComponent<TriggerProxy>() ?? fallTriggerBox.gameObject.AddComponent<TriggerProxy>();
            fallProxy.onTriggerEnterAction = OnFallTriggerEntered;
        }

        // 2. Box 2: Roll Trigger
        if (rollTriggerBox != null)
        {
            rollProxy = rollTriggerBox.gameObject.GetComponent<TriggerProxy>() ?? rollTriggerBox.gameObject.AddComponent<TriggerProxy>();
            rollProxy.onTriggerEnterAction = OnRollTriggerEntered;
        }

        // 3. Damage Trigger trên Cây
        if (treeDamageCollider != null)
        {
            damageProxy = treeDamageCollider.gameObject.GetComponent<TriggerProxy>() ?? treeDamageCollider.gameObject.AddComponent<TriggerProxy>();
            damageProxy.onTriggerEnterAction = OnTreeDamageTriggerEntered;
            damageProxy.onTriggerStayAction = OnTreeDamageTriggerEntered;
        }
    }

    private void Update()
    {
        if (!IsServerAuth) return;

        switch (CurrentState)
        {
            case TreeState.Falling:
                UpdateTreeFalling();
                break;

            case TreeState.Rolling:
                UpdateTreeRolling();
                break;
        }
    }

    // --- PHASE 1: KÍCH HOẠT CÂY NGÃ ---
    private void OnFallTriggerEntered(Collider other)
    {
        if (!IsServerAuth) return;
        if (CurrentState != TreeState.Standing) return;

        if (IsPlayer(other.gameObject, out Transform playerTransform))
        {
            Debug.Log($"[FallingRollingTreeTrap] Player {playerTransform.name} chạm Box 1 -> Cây bắt đầu ngã!");
            hitPlayersThisFall.Clear();
            CurrentState = TreeState.Falling;
        }
    }

    private void UpdateTreeFalling()
    {
        treeMesh.rotation = Quaternion.RotateTowards(treeMesh.rotation, targetFallRotation, fallSpeed * 45f * Time.deltaTime);

        if (Quaternion.Angle(treeMesh.rotation, targetFallRotation) < 0.5f)
        {
            treeMesh.rotation = targetFallRotation;
            CurrentState = TreeState.FallenOnGround;
            Debug.Log("[FallingRollingTreeTrap] Cây đã ngã hoàn tất nằm trên mặt đất.");
        }
    }

    [Header("--- Cấu Hình Sát Thương Khi Lăn (Phase 2) ---")]
    [Tooltip("Thời gian giãn cách giữa các lần trừ 5 HP khi cây đang lăn liên tục (giây)")]
    public float rollingDamageTickInterval = 0.5f;

    private Dictionary<GameObject, float> nextRollingDamageTime = new Dictionary<GameObject, float>();

    // Sát thương khi cây ngã (duy nhất 1 lần) & khi cây lăn (liên tục -5 HP) + Đẩy Player an toàn chống xuyên tường
    private void OnTreeDamageTriggerEntered(Collider other)
    {
        if (!IsServerAuth) return;
        if (CurrentState != TreeState.Falling && CurrentState != TreeState.Rolling) return;

        if (IsPlayer(other.gameObject, out Transform playerTransform))
        {
            GameObject playerRoot = playerTransform.gameObject;

            // 1. PHASE 1: CÂY NGÃ -> Trừ 5 HP duy nhất 1 lần
            if (CurrentState == TreeState.Falling)
            {
                if (!hitPlayersThisFall.Contains(playerRoot))
                {
                    hitPlayersThisFall.Add(playerRoot);
                    EnemyDamageHelper.DealDamage(playerTransform, crushDamage, Vector3.zero);
                    Debug.Log($"[FallingRollingTreeTrap] Cây ngã đè trúng {playerRoot.name}! Trừ {crushDamage} HP duy nhất 1 lần.");
                    SafelyShiftPlayerAwayFromTree(playerTransform);
                }
            }
            // 2. PHASE 2: CÂY LĂN -> Đứng đơ liên tục trừ 5 HP theo nhịp 0.5s & đẩy tránh đụng tường
            else if (CurrentState == TreeState.Rolling)
            {
                float curTime = Time.time;
                if (!nextRollingDamageTime.TryGetValue(playerRoot, out float nextTime) || curTime >= nextTime)
                {
                    nextRollingDamageTime[playerRoot] = curTime + rollingDamageTickInterval;
                    EnemyDamageHelper.DealDamage(playerTransform, crushDamage, Vector3.zero);
                    Debug.Log($"[FallingRollingTreeTrap] Cây lăn trúng {playerRoot.name}! Trừ {crushDamage} HP (liên tục).");
                    SafelyShiftPlayerAwayFromTree(playerTransform);
                }
            }
        }
    }

    private void SafelyShiftPlayerAwayFromTree(Transform playerTransform)
    {
        if (playerTransform == null) return;

        // Tính hướng đẩy ngang né thân cây
        Vector3 treeToPlayer = playerTransform.position - treeMesh.position;
        treeToPlayer.y = 0;
        if (treeToPlayer.sqrMagnitude < 0.01f)
        {
            treeToPlayer = -treeMesh.forward;
        }
        Vector3 pushDir = treeToPlayer.normalized;

        // KIỂM TRA CHỐNG ĐẨY XUYÊN TƯỜNG (Anti-Wall Clipping Raycast)
        Vector3 rayOrigin = playerTransform.position + Vector3.up * 0.5f;
        if (Physics.Raycast(rayOrigin, pushDir, out RaycastHit wallHit, safeNudgeDistance + 0.6f, wallLayer, QueryTriggerInteraction.Ignore))
        {
            // Nếu hướng đẩy bị vướng Tường ở khúc cuối, đảo ngược hướng đẩy ra phía ngược lại hoặc đẩy lùi ra sau
            pushDir = -pushDir;
            if (Physics.Raycast(rayOrigin, pushDir, out RaycastHit altWallHit, safeNudgeDistance + 0.6f, wallLayer, QueryTriggerInteraction.Ignore))
            {
                // Nếu cả 2 phía đều vướng tường, đẩy vuông góc dọc theo hành lang
                pushDir = Vector3.Cross(pushDir, Vector3.up).normalized;
            }
        }

        Vector3 targetPos = playerTransform.position + pushDir * safeNudgeDistance;

        // ĐẢM BẢO 100%: Dùng NavMesh.Warp để Player giữ nguyên trên vùng NavMesh hợp lệ, KHÔNG BAO GIỜ bị xuyên tường
        var agent = playerTransform.GetComponentInParent<NavMeshAgent>() ?? playerTransform.GetComponentInChildren<NavMeshAgent>();
        if (agent != null && agent.enabled)
        {
            if (NavMesh.SamplePosition(targetPos, out NavMeshHit navHit, 4.0f, NavMesh.AllAreas))
            {
                agent.Warp(navHit.position);
                return;
            }
        }

        // Fallback dùng CharacterController
        var cc = playerTransform.GetComponentInParent<CharacterController>() ?? playerTransform.GetComponentInChildren<CharacterController>();
        if (cc != null && cc.enabled)
        {
            cc.Move(pushDir * safeNudgeDistance);
            return;
        }

        // Fallback dùng Rigidbody hoặc Transform
        var rb = playerTransform.GetComponentInParent<Rigidbody>() ?? playerTransform.GetComponentInChildren<Rigidbody>();
        if (rb != null)
        {
            rb.position = targetPos;
        }
        else
        {
            playerTransform.position = targetPos;
        }
    }

    // --- PHASE 2: KÍCH HOẠT CÂY LĂN ---
    private void OnRollTriggerEntered(Collider other)
    {
        if (!IsServerAuth) return;
        if (CurrentState != TreeState.FallenOnGround && CurrentState != TreeState.Falling) return;

        if (IsPlayer(other.gameObject, out Transform playerTransform))
        {
            Debug.Log($"[FallingRollingTreeTrap] Player {playerTransform.name} chạm Box 2 -> Cây bắt đầu lăn về phía trước!");
            treeMesh.rotation = targetFallRotation;
            CurrentState = TreeState.Rolling;
        }
    }

    private void UpdateTreeRolling()
    {
        Vector3 moveDir = transform.TransformDirection(rollDirection).normalized;
        if (moveDir.sqrMagnitude < 0.01f) moveDir = transform.forward;

        float moveDist = rollMoveSpeed * Time.deltaTime;

        // Kiểm tra xem cây lăn có chạm Tường / Chướng ngại vật phía trước không
        Vector3 rayOrigin = treeMesh.position + Vector3.up * 0.5f;
        if (Physics.SphereCast(rayOrigin, 0.8f, moveDir, out RaycastHit hit, moveDist + 0.3f, wallLayer, QueryTriggerInteraction.Ignore))
        {
            if (!hit.collider.CompareTag("Player") && !hit.collider.CompareTag("Enemy") && hit.collider.gameObject != gameObject)
            {
                Debug.Log($"[FallingRollingTreeTrap] Cây lăn đụng tường ({hit.collider.name})! Dừng lại nằm yên tại chỗ, KHÔNG destroy.");
                CurrentState = TreeState.StoppedAtWall;
                return;
            }
        }

        // Di chuyển cây lăn về phía trước & xoay tròn thân cây
        treeMesh.position += moveDir * moveDist;
        treeMesh.Rotate(Vector3.right * rollRotationSpeed * Time.deltaTime, Space.Self);
    }

    private bool IsPlayer(GameObject go, out Transform playerTransform)
    {
        playerTransform = null;
        if (go == null) return false;

        if (go.CompareTag("Player"))
        {
            playerTransform = go.transform;
            return true;
        }

        var p = go.GetComponentInParent<IPlayerHUDTarget>() ?? go.GetComponentInChildren<IPlayerHUDTarget>();
        if (p != null)
        {
            MonoBehaviour mono = p as MonoBehaviour;
            if (mono != null)
            {
                playerTransform = mono.transform;
                return true;
            }
        }

        return false;
    }

    // Component phụ hứng Trigger Callback gọn gàng
    private class TriggerProxy : MonoBehaviour
    {
        public System.Action<Collider> onTriggerEnterAction;
        public System.Action<Collider> onTriggerStayAction;

        private void OnTriggerEnter(Collider other)
        {
            onTriggerEnterAction?.Invoke(other);
        }

        private void OnTriggerStay(Collider other)
        {
            onTriggerStayAction?.Invoke(other);
        }
    }
}
