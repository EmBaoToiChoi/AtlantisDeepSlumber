using Unity.Netcode;
using UnityEngine;

public class SeagullController : NetworkBehaviour
{
    public static SeagullController ActiveSeagull { get; private set; }

    [Header("Flight Settings")]
    public float flySpeed = 12f;
    public float rotationSpeed = 120f;
    public float pitchSpeed = 90f;
    public float duration = 15f; // Thời gian tồn tại của hải âu (giây)

    [Header("Camera Settings")]
    [Tooltip("Góc nhìn thứ ba (Third Person) của chim. Ví dụ: X=0, Y=2.5, Z=-8")]
    public Vector3 cameraOffset = new Vector3(0f, 2.5f, -8f);
    public float cameraSmoothSpeed = 10f;

    [Tooltip("Bù đắp góc xoay Y của mô hình visual để đầu chim hướng về phía trước (trục Z). Ví dụ: 90, -90, 180.")]
    public float modelRotationYOffset = 90f;

    [Tooltip("FOV (Góc nhìn camera) khi đang bay chim. Mặc định 80 giúp góc rộng và thoáng hơn.")]
    public float targetFOV = 80f;

    [Tooltip("Bù đắp góc xoay Y khi chim bay về (không còn dùng, nên để 0).")]
    public float returnRotationYOffset = 0f;

    [Header("Animation Settings")]
    public Animator anim;
    public string idleAnimName = "Idle";
    public string flyAnimName = "Fly";

    private float age = 0f;
    private bool isControlled = false;
    private Vector3 autoFlyDirection;

    private bool localIsReturning = false;
    public bool IsReturning => isStandaloneMode ? localIsReturning : isReturningNet.Value;
    public float TimeRemaining => IsReturning ? 0f : Mathf.Max(duration - age, 0f);

    private Vector3 playerCamStartPos;
    private Quaternion playerCamStartRot;
    private float playerCamStartFOV;
    private float playerCamStartNearClip = 0.3f;

    [Header("Smooth Flight Settings")]
    [Tooltip("Gia tốc tăng tốc/giảm tốc của chim")]
    public float acceleration = 5f;
    [Tooltip("Độ nhạy xoay mượt mà")]
    public float smoothTurnSpeed = 5f;
    [Tooltip("Góc nghiêng tối đa khi rẽ trái/phải")]
    public float maxRollAngle = 30f;
    [Tooltip("Tốc độ nghiêng cánh")]
    public float rollSpeed = 5f;

    [Header("Collision & Wall Sliding Settings")]
    [Tooltip("Bán kính kiểm tra va chạm của chim (m). Mặc định 0.4m")]
    public float collisionRadius = 0.4f;
    [Tooltip("Layer của tường và mặt đất cần cản/trượt")]
    public LayerMask obstacleMask = ~0;

    [Header("Speed Boost Trail & Visual VFX Settings")]
    [Tooltip("Prefab VFX vệt sáng bứt tốc độ phía sau chim (nếu để trống sẽ tự động dùng Seagull_Speed_Trail).")]
    public GameObject speedTrailVfxPrefab;
    [Tooltip("Offset vị trí tương đối phía sau chim.")]
    public Vector3 speedVfxOffset = new Vector3(0f, 0.2f, -0.6f);
    [Tooltip("Tỷ lệ scale của VFX vệt sáng bứt tốc.")]
    public float speedVfxScale = 1.3f;
    [Tooltip("Tùy chọn tự động đổi màu mô hình chim thành trắng tinh tế.")]
    public bool makeBirdPureWhite = true;

    private GameObject activeSpeedVfxInstance;
    private static GameObject defaultSpeedTrailPrefab;

    private Vector3 currentVelocity;
    private float targetPitch = 0f;
    private float targetYaw = 0f;
    private float currentRoll = 0f;

    private float freeLookYaw = 0f;
    private float freeLookPitch = 0f;
    private float currentFreeLookYaw = 0f;
    private float currentFreeLookPitch = 0f;
    private float freeLookDistanceMultiplier = 1f;
    private float defaultPitch = 17.35f;
    private float baseDistance = 8.38f;
    private bool childrenRotated = false;

    public bool isStandaloneMode => NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;
    
    // Đồng bộ trạng thái bay/đứng yên qua mạng để hiển thị animation chính xác trên mọi client
    private NetworkVariable<bool> isMovingNet = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Đồng bộ trạng thái quay về phía Elena
    private NetworkVariable<bool> isReturningNet = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private void Awake()
    {
        // Khóa trọng lực và va chạm trên tất cả Rigidbody và Collider của chim (kể cả các object con) để tránh đẩy đè nhân vật lúc Instantiate
        var rigidbodies = GetComponentsInChildren<Rigidbody>();
        foreach (var rb in rigidbodies)
        {
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        var colliders = GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            col.isTrigger = true;
        }
    }

    private void OnEnable()
    {
        MakeBirdWhite();
        AttachSpeedTrailVfx();
    }

    private void Start()
    {
        if (isStandaloneMode)
        {
            InitializeSeagull();
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (!isStandaloneMode)
        {
            InitializeSeagull();
        }
    }

    private void InitializeSeagull()
    {
        returnRotationYOffset = 0f; // Force offset to 0 because we rotate the children directly to face forward

        if (!childrenRotated)
        {
            childrenRotated = true;
            foreach (Transform child in transform)
            {
                child.localRotation = Quaternion.Euler(0f, modelRotationYOffset, 0f) * child.localRotation;
            }
        }

        ActiveSeagull = this;
        age = 0f;
        isControlled = false;
        localIsReturning = false;
        freeLookDistanceMultiplier = 1.5f;

        // Tự động sửa chữa cameraOffset nếu giá trị trong Inspector của prefab bị sai lệch (quá ngắn hoặc bằng 0)
        if (cameraOffset.magnitude < 1.0f || cameraOffset.z > -1.0f)
        {
            cameraOffset = new Vector3(0f, 2.5f, -8f);
        }

        baseDistance = cameraOffset.magnitude;
        defaultPitch = Mathf.Atan2(cameraOffset.y, Mathf.Abs(cameraOffset.z)) * Mathf.Rad2Deg;

        // Khởi tạo góc quay tự do ban đầu bằng góc nghiêng mặc định
        freeLookYaw = 0f;
        freeLookPitch = defaultPitch;
        currentFreeLookYaw = 0f;
        currentFreeLookPitch = defaultPitch;

        if (Camera.main != null)
        {
            playerCamStartPos = Camera.main.transform.position;
            playerCamStartRot = Camera.main.transform.rotation;
            playerCamStartFOV = Camera.main.fieldOfView;
            playerCamStartNearClip = Camera.main.nearClipPlane;
            Camera.main.nearClipPlane = 0.05f; // Giảm Near Clip Plane để tránh vướng vào model
        }
        
        // Khóa va chạm vật lý đã được thực hiện sớm trong Awake() để tránh đẩy đè nhân vật lúc Instantiate

        transform.SetParent(null, true);

        // Hướng bay chếch lên khoảng 15 độ để tạo đà bay lên, tránh cắm đầu xuống đất
        Vector3 forwardXZ = transform.forward;
        forwardXZ.y = 0f;
        if (forwardXZ == Vector3.zero) forwardXZ = Vector3.forward;
        forwardXZ.Normalize();
        autoFlyDirection = forwardXZ + Vector3.up * 0.25f;
        autoFlyDirection.Normalize();
        
        transform.rotation = Quaternion.LookRotation(autoFlyDirection) * Quaternion.Euler(0f, returnRotationYOffset, 0f);

        if (anim == null)
        {
            anim = GetComponentInChildren<Animator>();
        }

        if (anim != null && !string.IsNullOrEmpty(flyAnimName))
        {
            anim.Play(flyAnimName);
        }

        MakeBirdWhite();
        AttachSpeedTrailVfx();

        if (IsServer)
        {
            isMovingNet.Value = true;
            isReturningNet.Value = false;
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        if (ActiveSeagull == this)
        {
            ActiveSeagull = null;
        }
    }

    private void OnDestroy()
    {
        if (ActiveSeagull == this)
        {
            ActiveSeagull = null;
        }
        if (activeSpeedVfxInstance != null)
        {
            Destroy(activeSpeedVfxInstance);
            activeSpeedVfxInstance = null;
        }
    }

    private void MakeBirdWhite()
    {
        if (!makeBirdPureWhite) return;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var rend in renderers)
        {
            if (rend == null) continue;
            if (rend is TrailRenderer || rend is ParticleSystemRenderer) continue;

            Material[] mats = rend.materials;
            foreach (var mat in mats)
            {
                if (mat == null) continue;
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
                if (mat.HasProperty("_Tint")) mat.SetColor("_Tint", Color.white);
                if (mat.HasProperty("_MainTex") && mat.mainTexture != null)
                {
                    mat.color = Color.white;
                }
            }
        }
    }

    private void AttachSpeedTrailVfx()
    {
        if (activeSpeedVfxInstance != null) return;

        GameObject prefabToUse = speedTrailVfxPrefab;
        if (prefabToUse == null)
        {
            if (defaultSpeedTrailPrefab == null)
            {
                defaultSpeedTrailPrefab = Resources.Load<GameObject>("VFX/Seagull_Speed_Trail");
                if (defaultSpeedTrailPrefab == null)
                {
                    defaultSpeedTrailPrefab = Resources.Load<GameObject>("Par_LightShoot_Trails");
                    if (defaultSpeedTrailPrefab == null)
                    {
                        defaultSpeedTrailPrefab = Resources.Load<GameObject>("Par_BlueShoot_Trails");
                    }
                }
            }
            prefabToUse = defaultSpeedTrailPrefab;
        }

        if (prefabToUse != null)
        {
            activeSpeedVfxInstance = Instantiate(prefabToUse, transform);
            
            Transform capsuleChild = activeSpeedVfxInstance.transform.Find("Capsule");
            if (capsuleChild != null)
            {
                capsuleChild.gameObject.SetActive(false);
            }

            activeSpeedVfxInstance.transform.localPosition = speedVfxOffset;
            activeSpeedVfxInstance.transform.localRotation = Quaternion.identity;
            activeSpeedVfxInstance.transform.localScale = Vector3.one * Mathf.Max(0.2f, speedVfxScale);

            ParticleSystem[] psList = activeSpeedVfxInstance.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in psList)
            {
                if (ps != null)
                {
                    var main = ps.main;
                    main.loop = true;
                    if (!ps.isPlaying) ps.Play();
                }
            }
        }

        CreateWingtipSpeedTrails();
    }

    private void CreateWingtipSpeedTrails()
    {
        Transform[] wingTips = GetComponentsInChildren<Transform>(true);
        Transform leftWing = null;
        Transform rightWing = null;
        Transform tail = null;

        foreach (var t in wingTips)
        {
            if (t == null) continue;
            string n = t.name.ToLower();
            if (n.Contains("wing") && (n.Contains("l") || n.Contains("left"))) leftWing = t;
            else if (n.Contains("wing") && (n.Contains("r") || n.Contains("right"))) rightWing = t;
            else if (n.Contains("tail")) tail = t;
        }

        Transform[] targets = new Transform[] { leftWing, rightWing, tail };
        foreach (var target in targets)
        {
            Transform parentToUse = target != null ? target : transform;
            GameObject trailObj = new GameObject("SpeedTrailRibbon");
            trailObj.transform.SetParent(parentToUse, false);
            trailObj.transform.localPosition = target == null ? Vector3.back * 0.4f : Vector3.zero;

            TrailRenderer tr = trailObj.AddComponent<TrailRenderer>();
            tr.time = 0.6f;
            tr.startWidth = 0.25f;
            tr.endWidth = 0.0f;
            tr.autodestruct = false;

            Material trailMat = new Material(Shader.Find("Sprites/Default"));
            tr.material = trailMat;

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(new Color(0.5f, 0.85f, 1.0f), 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.85f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            tr.colorGradient = gradient;
        }
    }

    private void Update()
    {
        age += Time.deltaTime;

        // Tự động quay đầu bay về Elena khi hết thời gian
        if (!IsReturning && age >= duration)
        {
            if (isStandaloneMode || IsServer)
            {
                StartReturning();
            }
        }

        if (IsReturning)
        {
            // Ngắt kết nối camera & giải phóng điều khiển người chơi ngay lập tức trên máy khách này
            if (ActiveSeagull == this)
            {
                ActiveSeagull = null;
            }

            // Chỉ Server (Online) hoặc Local Client (Standalone) mới thực hiện di chuyển chim về phía vai Elena
            if (isStandaloneMode || IsServer)
            {
                MoveTowardsElena();
            }

            // Luôn chạy animation bay khi đang quay về
            UpdateAnimationState();
            return;
        }
        
        // 2 giây đầu: Chim tự động bay thẳng lên về phía trước và tăng tốc dần
        if (age < 2f)
        {
            float launchSpeed = flySpeed * Mathf.Lerp(0.3f, 1.0f, age / 2f);
            Vector3 rawMove = autoFlyDirection * launchSpeed * Time.deltaTime;
            Vector3 slideMove = CalculateCollisionSlide(transform.position, rawMove);
            transform.position += slideMove;
            if (autoFlyDirection != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(autoFlyDirection) * Quaternion.Euler(0f, returnRotationYOffset, 0f);
            }
        }
        else
        {
            if (!isControlled)
            {
                isControlled = true;
                StartControl();
            }

            // Người chơi Elena (chủ sở hữu con chim này) mới có quyền điều khiển sau khi kết thúc 2s cất cánh
            if (isStandaloneMode || IsOwner)
            {
                HandleOwnerControl();
            }
        }

        // Cập nhật hoạt ảnh dựa trên di chuyển trên toàn bộ máy khách
        UpdateAnimationState();
    }

    private void LateUpdate()
    {
        if (ActiveSeagull == this)
        {
            UpdateCamera(Camera.main);
        }
    }

    private void StartControl()
    {
        Vector3 euler = transform.rotation.eulerAngles;
        targetYaw = euler.y;
        targetPitch = euler.x;
        if (targetPitch > 180f) targetPitch -= 360f;
        currentRoll = 0f;
        currentVelocity = transform.forward * flySpeed;
    }

    private void HandleOwnerControl()
    {
        // 1. Nhận input từ chuột
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        // Kiểm tra xem có đang giữ phím Ctrl để xoay camera tự do không
        bool ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

        if (ctrlHeld)
        {
            // Trong chế độ Free Look: Xoay camera bằng chuột, chim bay thẳng hướng cũ
            freeLookYaw += mouseX * rotationSpeed * Time.deltaTime;
            freeLookPitch -= mouseY * pitchSpeed * Time.deltaTime;
            freeLookPitch = Mathf.Clamp(freeLookPitch, -80f, 80f); // Tăng giới hạn lên 80 độ để dễ nhìn từ dưới lên

            // Giảm độ nghiêng cánh về 0
            currentRoll = Mathf.Lerp(currentRoll, 0f, Time.deltaTime * rollSpeed);
        }
        else
        {
            // Smoothly reset free look camera angles
            if (freeLookYaw != 0f || freeLookPitch != defaultPitch)
            {
                freeLookYaw = Mathf.Lerp(freeLookYaw, 0f, Time.deltaTime * 5f);
                freeLookPitch = Mathf.Lerp(freeLookPitch, defaultPitch, Time.deltaTime * 5f);
                if (Mathf.Abs(freeLookYaw) < 0.01f) freeLookYaw = 0f;
                if (Mathf.Abs(freeLookPitch - defaultPitch) < 0.01f) freeLookPitch = defaultPitch;
            }

            // Điều khiển hướng bay bằng chuột và bàn phím (A/D)
            float yawInput = mouseX + Input.GetAxis("Horizontal") * 0.5f;
            targetYaw += yawInput * rotationSpeed * Time.deltaTime;
            targetPitch -= mouseY * pitchSpeed * Time.deltaTime;
            targetPitch = Mathf.Clamp(targetPitch, -80f, 80f); // Giới hạn ngửa/cúi

            // Tính góc nghiêng (Roll) tự động khi rẽ trái/phải
            float turnInput = yawInput;
            float targetRoll = -turnInput * maxRollAngle;
            currentRoll = Mathf.Lerp(currentRoll, targetRoll, Time.deltaTime * rollSpeed);
        }

        // Áp dụng góc xoay mượt mà vào Rotation của chim
        Quaternion finalRotation = Quaternion.Euler(targetPitch, targetYaw, currentRoll);
        transform.rotation = Quaternion.Slerp(transform.rotation, finalRotation, Time.deltaTime * smoothTurnSpeed);

        // 2. Chim tự động bay thẳng về phía trước theo hướng mũi chim
        Vector3 targetVel = transform.forward * flySpeed;

        // Lerp vận tốc hiện tại tới vận tốc mục tiêu (tạo gia tốc quán tính)
        currentVelocity = Vector3.Lerp(currentVelocity, targetVel, Time.deltaTime * acceleration);
        
        // Tính toán di chuyển kèm cản va chạm va trượt tường (Wall Sliding)
        Vector3 rawMove = currentVelocity * Time.deltaTime;
        Vector3 slideMove = CalculateCollisionSlide(transform.position, rawMove);
        transform.position += slideMove;

        // Cập nhật trạng thái bay sang server (chim luôn bay)
        bool movingState = true;
        if (movingState != isMovingNet.Value)
        {
            UpdateMovingStateServerRpc(movingState);
        }

        // Ấn Q lần nữa để thu hồi quay về vai Elena sớm
        if (Input.GetKeyDown(KeyCode.Q))
        {
            if (isStandaloneMode)
            {
                StartReturning();
            }
            else
            {
                RequestStartReturningServerRpc();
            }
        }
    }

    /// <summary>
    /// Kiểm tra va chạm bằng SphereCast, Raycast & Ground Check để trượt mượt mà trên cả Tường và Mặt Đất Terrain
    /// </summary>
    private Vector3 CalculateCollisionSlide(Vector3 position, Vector3 moveStep)
    {
        float moveDistance = moveStep.magnitude;
        if (moveDistance < 0.0001f) return Vector3.zero;

        Vector3 moveDirection = moveStep / moveDistance;
        Vector3 finalMove = moveStep;
        bool hitSomething = false;

        // 1. Kiểm tra va chạm hướng di chuyển bằng SphereCast
        if (Physics.SphereCast(position, collisionRadius, moveDirection, out RaycastHit hit, moveDistance + 0.05f, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            if (!hit.transform.IsChildOf(transform) && hit.transform != transform)
            {
                hitSomething = true;
                finalMove = ProcessHitSlide(moveDirection, moveDistance, hit);
            }
        }

        // 2. Dự phòng bằng Raycast (đặc biệt đối với TerrainCollider khi SphereCast bị xịt do chèn mép)
        if (!hitSomething && Physics.Raycast(position, moveDirection, out RaycastHit rayHit, moveDistance + collisionRadius + 0.1f, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            if (!rayHit.transform.IsChildOf(transform) && rayHit.transform != transform)
            {
                hitSomething = true;
                finalMove = ProcessHitSlide(moveDirection, moveDistance, rayHit);
            }
        }

        Vector3 targetPosition = position + finalMove;

        // 3. CHỐNG ĐÂM XUYÊN ĐẤT TERRAIN: Bắn tia từ trên cao xuống để xác định độ cao mặt đất chính xác
        Vector3 skyOrigin = new Vector3(targetPosition.x, targetPosition.y + 5f, targetPosition.z);
        if (Physics.Raycast(skyOrigin, Vector3.down, out RaycastHit groundHit, 25f, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            if (!groundHit.transform.IsChildOf(transform) && groundHit.transform != transform)
            {
                float minSafeY = groundHit.point.y + collisionRadius;
                if (targetPosition.y < minSafeY)
                {
                    targetPosition.y = minSafeY;

                    // Nếu đang đâm hướng xuống đất, nghiêng véc-tơ vận tốc trượt theo độ dốc của Terrain
                    if (currentVelocity.y < 0f)
                    {
                        currentVelocity = Vector3.ProjectOnPlane(currentVelocity, groundHit.normal);
                        if (currentVelocity.y < 0f) currentVelocity.y = 0f;
                    }
                }
            }
        }

        return targetPosition - position;
    }

    private Vector3 ProcessHitSlide(Vector3 moveDirection, float moveDistance, RaycastHit hit)
    {
        // Tính hướng trượt song song với bề mặt va chạm (ProjectOnPlane)
        Vector3 slideDirection = Vector3.ProjectOnPlane(moveDirection, hit.normal).normalized;

        // Triệt tiêu thành phần vận tốc đâm cắm vào bề mặt
        currentVelocity = Vector3.ProjectOnPlane(currentVelocity, hit.normal);

        float safeDistance = Mathf.Max(0f, hit.distance - 0.02f);
        Vector3 safeMove = moveDirection * Mathf.Min(moveDistance, safeDistance);

        float remainingDistance = moveDistance - safeDistance;
        if (remainingDistance > 0f)
        {
            Vector3 slideMove = slideDirection * remainingDistance;
            return safeMove + slideMove;
        }
        return safeMove;
    }

    [ServerRpc(RequireOwnership = false)]
    private void UpdateMovingStateServerRpc(bool isMoving)
    {
        isMovingNet.Value = isMoving;
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestStartReturningServerRpc()
    {
        StartReturning();
    }

    private void StartReturning()
    {
        if (isStandaloneMode)
        {
            localIsReturning = true;
            ActiveSeagull = null;
        }
        else if (IsServer)
        {
            isReturningNet.Value = true;
            isMovingNet.Value = true;
            
            if (NetworkObject.IsSpawned)
            {
                NetworkObject.RemoveOwnership();
            }
            
            StartReturningClientRpc();
        }
    }

    [ClientRpc]
    private void StartReturningClientRpc()
    {
        ActiveSeagull = null;
        if (anim != null && !string.IsNullOrEmpty(flyAnimName))
        {
            anim.Play(flyAnimName);
        }
    }

    private Transform FindOwnerPlayerTransform()
    {
        // Tìm ElenaPlayer thuộc OwnerClientId trong chế độ online
        var players = FindObjectsOfType<ElenaPlayer>();
        foreach (var player in players)
        {
            if (!isStandaloneMode && player.OwnerClientId == OwnerClientId)
            {
                return player.transform;
            }
        }
        
        // Chế độ chơi đơn
        var standalonePlayer = FindObjectOfType<ElenaPlayer>();
        if (standalonePlayer != null)
        {
            return standalonePlayer.transform;
        }
        return null;
    }

    private void MoveTowardsElena()
    {
        Transform ownerTrans = FindOwnerPlayerTransform();
        if (ownerTrans != null)
        {
            var elena = ownerTrans.GetComponent<ElenaPlayer>();
            Transform socket = elena != null && elena.seagullShoulderSocket != null ? elena.seagullShoulderSocket : ownerTrans;
            Vector3 targetPos = socket.position;
            Vector3 direction = (targetPos - transform.position);
            float distance = direction.magnitude;

            if (distance < 0.4f)
            {
                // Đến nơi: hiển thị lại chim trên vai và hủy con chim điều khiển này
                if (isStandaloneMode)
                {
                    if (elena != null)
                    {
                        elena.localIsSeagullOnShoulder = true;
                        elena.localIsQSkillActive = false;
                        elena.StartQSkillCooldown();
                    }
                    Destroy(gameObject);
                }
                else if (IsServer)
                {
                    if (elena != null)
                    {
                        elena.isSeagullOnShoulder.Value = true;
                    }
                    if (NetworkObject.IsSpawned)
                    {
                        NetworkObject.Despawn(true);
                    }
                }
            }
            else
            {
                direction.Normalize();
                transform.position += direction * flySpeed * Time.deltaTime;
                if (direction != Vector3.zero)
                {
                    Quaternion lookRot = Quaternion.LookRotation(direction) * Quaternion.Euler(0f, returnRotationYOffset, 0f);
                    transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * 10f);
                }
            }
        }
        else
        {
            // Không tìm thấy chủ nhân, hủy luôn
            if (isStandaloneMode)
            {
                Destroy(gameObject);
            }
            else if (NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn(true);
            }
        }
    }

    private void UpdateAnimationState()
    {
        if (anim == null) return;

        bool shouldFly = isMovingNet.Value || age < 2f || IsReturning;
        if (shouldFly)
        {
            if (!string.IsNullOrEmpty(flyAnimName) && !anim.GetCurrentAnimatorStateInfo(0).IsName(flyAnimName))
            {
                anim.Play(flyAnimName);
            }
        }
        else
        {
            if (!string.IsNullOrEmpty(idleAnimName) && !anim.GetCurrentAnimatorStateInfo(0).IsName(idleAnimName))
            {
                anim.Play(idleAnimName);
            }
        }
    }

    public void UpdateCamera(Camera cam)
    {
        if (cam == null) return;

        Vector3 lookTarget = transform.position + transform.up * 0.2f;

        // Duy trì FOV khi điều khiển chim bay
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.deltaTime * 5f);

        // Góc xoay gốc của camera: Owner lấy từ targetPitch/targetYaw, Non-Owner lấy từ rotation hiện tại của chim (đã sync qua network)
        Quaternion baseRot;
        if (isControlled && (isStandaloneMode || IsOwner))
        {
            baseRot = Quaternion.Euler(targetPitch, targetYaw, 0f);
        }
        else
        {
            Vector3 birdEuler = transform.rotation.eulerAngles;
            float birdPitch = birdEuler.x > 180f ? birdEuler.x - 360f : birdEuler.x;
            float birdYaw = birdEuler.y;
            baseRot = Quaternion.Euler(birdPitch, birdYaw, 0f);
        }

        // Nếu đang giữ Ctrl xoay camera tự do xung quanh chim (chỉ áp dụng cho owner)
        bool ctrlHeld = (isStandaloneMode || IsOwner) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl));
        if (ctrlHeld || freeLookYaw != 0f || freeLookPitch != 0f || Mathf.Abs(currentFreeLookYaw) > 0.01f || Mathf.Abs(currentFreeLookPitch - defaultPitch) > 0.01f)
        {
            // Tăng khoảng cách camera (zoom out) trong chế độ Free Look để không bị vướng model con chim
            freeLookDistanceMultiplier = Mathf.Lerp(freeLookDistanceMultiplier, 2.0f, Time.deltaTime * 5f);

            // Nội suy góc quay camera tự do để tránh việc camera cắt xuyên qua model khi xoay chuột nhanh
            currentFreeLookYaw = Mathf.Lerp(currentFreeLookYaw, freeLookYaw, Time.deltaTime * cameraSmoothSpeed);
            currentFreeLookPitch = Mathf.Lerp(currentFreeLookPitch, freeLookPitch, Time.deltaTime * cameraSmoothSpeed);

            Quaternion freeLookRot = Quaternion.Euler(currentFreeLookPitch, currentFreeLookYaw, 0f);
            Quaternion finalCamRot = baseRot * freeLookRot;
            
            // Vị trí camera xoay quanh chim trên mặt cầu hoàn hảo (khoảng cách luôn cố định tính từ tâm lookTarget)
            Vector3 targetPos = lookTarget + finalCamRot * new Vector3(0f, 0f, -baseDistance) * freeLookDistanceMultiplier;
            cam.transform.position = Vector3.Lerp(cam.transform.position, targetPos, Time.deltaTime * cameraSmoothSpeed);
            
            // Camera luôn hướng nhìn vào chim (Slerp mượt mà)
            Quaternion targetRot = Quaternion.LookRotation(lookTarget - cam.transform.position);
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, targetRot, Time.deltaTime * cameraSmoothSpeed);
        }
        else
        {
            // Trả khoảng cách camera về mặc định khi bay (zoom rộng 1.5 lần)
            freeLookDistanceMultiplier = Mathf.Lerp(freeLookDistanceMultiplier, 1.5f, Time.deltaTime * 5f);

            freeLookYaw = 0f;
            freeLookPitch = defaultPitch;
            currentFreeLookYaw = Mathf.Lerp(currentFreeLookYaw, 0f, Time.deltaTime * cameraSmoothSpeed);
            currentFreeLookPitch = Mathf.Lerp(currentFreeLookPitch, defaultPitch, Time.deltaTime * cameraSmoothSpeed);

            // Góc nhìn thứ 3 bình thường bám theo sau chim
            Quaternion freeLookRot = Quaternion.Euler(currentFreeLookPitch, currentFreeLookYaw, 0f);
            Quaternion finalCamRot = baseRot * freeLookRot;

            Vector3 targetPos = lookTarget + finalCamRot * new Vector3(0f, 0f, -baseDistance) * freeLookDistanceMultiplier;

            // Ở những khung hình đầu tiên (vừa ấn Q), chuyển vị trí camera nhanh để focus ngay vào con chim theo góc nhìn Elena
            float moveSpeed = age < 0.3f ? 30f : cameraSmoothSpeed;
            cam.transform.position = Vector3.Lerp(cam.transform.position, targetPos, Time.deltaTime * moveSpeed);

            Quaternion targetRot = Quaternion.LookRotation(lookTarget - cam.transform.position);
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, targetRot, Time.deltaTime * moveSpeed);
        }
    }

    private void OnDisable()
    {
        RestoreCamera();
    }

    private void RestoreCamera()
    {
        if (Camera.main != null)
        {
            if (playerCamStartFOV != 0f) Camera.main.fieldOfView = playerCamStartFOV;
            if (playerCamStartNearClip != 0f) Camera.main.nearClipPlane = playerCamStartNearClip;
        }
    }
}
