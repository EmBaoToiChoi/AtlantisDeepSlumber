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

    private Vector3 currentVelocity;
    private float targetPitch = 0f;
    private float targetYaw = 0f;
    private float currentRoll = 0f;

    private float freeLookYaw = 0f;
    private float freeLookPitch = 0f;
    private float currentFreeLookYaw = 0f;
    private float currentFreeLookPitch = 0f;
    private float freeLookDistanceMultiplier = 1f;
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
        
        // 2 giây đầu: Chim tự động bay về phía trước và tăng tốc dần từ 30% đến 100% tốc độ
        if (age < 2f)
        {
            float launchSpeed = flySpeed * Mathf.Lerp(0.3f, 1.0f, age / 2f);
            transform.position += autoFlyDirection * launchSpeed * Time.deltaTime;
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

            // Chỉ người chơi Elena (chủ sở hữu con chim này) mới có quyền điều khiển
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
            freeLookPitch = Mathf.Clamp(freeLookPitch, -60f, 60f);

            // Giảm độ nghiêng cánh về 0
            currentRoll = Mathf.Lerp(currentRoll, 0f, Time.deltaTime * rollSpeed);
        }
        else
        {
            // Smoothly reset free look camera angles
            if (freeLookYaw != 0f || freeLookPitch != 0f)
            {
                freeLookYaw = Mathf.Lerp(freeLookYaw, 0f, Time.deltaTime * 5f);
                freeLookPitch = Mathf.Lerp(freeLookPitch, 0f, Time.deltaTime * 5f);
                if (Mathf.Abs(freeLookYaw) < 0.01f) freeLookYaw = 0f;
                if (Mathf.Abs(freeLookPitch) < 0.01f) freeLookPitch = 0f;
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
        transform.position += currentVelocity * Time.deltaTime;

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

        if (age < 2f && playerCamStartPos != Vector3.zero)
        {
            // Tăng dần khoảng cách camera lên 1.5 lần để zoom rộng tầm nhìn cất cánh
            freeLookDistanceMultiplier = Mathf.Lerp(freeLookDistanceMultiplier, 1.5f, Time.deltaTime * 5f);

            // Sử dụng SmoothStep để chuyển đổi camera mượt mà, tránh robotic
            float t = Mathf.SmoothStep(0f, 1f, age / 2f);
            
            // Hướng cơ bản không roll của chim tại thời điểm bắt đầu
            Quaternion baseRot = Quaternion.Euler(0f, transform.rotation.eulerAngles.y, 0f);
            
            // Vị trí camera góc nhìn thứ 3 của chim (Dùng baseRot không roll để tránh camera bị nghiêng khi cất cánh)
            Vector3 birdCamPos = transform.position + baseRot * (cameraOffset * freeLookDistanceMultiplier);
            cam.transform.position = Vector3.Lerp(playerCamStartPos, birdCamPos, t);
            
            // Hướng nhìn chuyển tiếp mượt mà từ góc nhìn cũ của player sang hướng nhìn tập trung vào con chim
            Vector3 lookTarget = transform.position + transform.up * 0.2f;
            Quaternion lookAtBirdRot = Quaternion.LookRotation(lookTarget - cam.transform.position);
            cam.transform.rotation = Quaternion.Slerp(playerCamStartRot, lookAtBirdRot, t);

            // Cập nhật FOV rộng dần ra tạo cảm giác tốc độ và không gian rộng
            cam.fieldOfView = Mathf.Lerp(playerCamStartFOV, targetFOV, t);

            // Thêm hiệu ứng rung camera nhẹ (camera shake) trong 0.5s đầu khi chim tung cánh cất cánh
            if (age < 0.5f)
            {
                float shakeStrength = 0.05f * (1f - (age / 0.5f));
                Vector3 shakeOffset = new Vector3(
                    Random.Range(-1f, 1f),
                    Random.Range(-1f, 1f),
                    Random.Range(-1f, 1f)
                ).normalized * shakeStrength;
                cam.transform.position += shakeOffset;
            }
        }
        else
        {
            // Duy trì FOV khi điều khiển chim bay
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.deltaTime * 5f);

            // Sử dụng hướng bay (Yaw/Pitch) không có Roll làm gốc cho camera để giữ đường chân trời thẳng, không bị nghiêng lệch
            Quaternion baseRot = Quaternion.Euler(targetPitch, targetYaw, 0f);

            // Nếu đang giữ Ctrl xoay camera tự do xung quanh chim
            bool ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (ctrlHeld || freeLookYaw != 0f || freeLookPitch != 0f || Mathf.Abs(currentFreeLookYaw) > 0.01f || Mathf.Abs(currentFreeLookPitch) > 0.01f)
            {
                // Tăng khoảng cách camera (zoom out) trong chế độ Free Look để không bị vướng model con chim
                freeLookDistanceMultiplier = Mathf.Lerp(freeLookDistanceMultiplier, 2.0f, Time.deltaTime * 5f);

                // Nội suy góc quay camera tự do để tránh việc camera cắt xuyên qua model khi xoay chuột nhanh
                currentFreeLookYaw = Mathf.Lerp(currentFreeLookYaw, freeLookYaw, Time.deltaTime * cameraSmoothSpeed);
                currentFreeLookPitch = Mathf.Lerp(currentFreeLookPitch, freeLookPitch, Time.deltaTime * cameraSmoothSpeed);

                Quaternion freeLookRot = Quaternion.Euler(currentFreeLookPitch, currentFreeLookYaw, 0f);
                Quaternion finalCamRot = baseRot * freeLookRot;
                
                // Vị trí camera xoay quanh chim trên mặt cầu hoàn hảo (khoảng cách luôn cố định bằng cameraOffset * multiplier)
                Vector3 targetPos = transform.position + finalCamRot * (cameraOffset * freeLookDistanceMultiplier);
                cam.transform.position = targetPos;
                
                // Camera luôn hướng nhìn vào chim (Slerp mượt mà)
                Vector3 lookTarget = transform.position + transform.up * 0.2f;
                Quaternion targetRot = Quaternion.LookRotation(lookTarget - cam.transform.position);
                cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, targetRot, Time.deltaTime * cameraSmoothSpeed);
            }
            else
            {
                // Trả khoảng cách camera về mặc định khi bay (zoom rộng 1.5 lần)
                freeLookDistanceMultiplier = Mathf.Lerp(freeLookDistanceMultiplier, 1.5f, Time.deltaTime * 5f);

                currentFreeLookYaw = 0f;
                currentFreeLookPitch = 0f;

                // Góc nhìn thứ 3 bình thường bám theo sau chim
                Vector3 targetPos = transform.position + baseRot * (cameraOffset * freeLookDistanceMultiplier);
                cam.transform.position = Vector3.Lerp(cam.transform.position, targetPos, Time.deltaTime * cameraSmoothSpeed);

                Vector3 lookTarget = transform.position + transform.up * 0.2f;
                Quaternion targetRot = Quaternion.LookRotation(lookTarget - cam.transform.position);
                cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, targetRot, Time.deltaTime * cameraSmoothSpeed);
            }
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
