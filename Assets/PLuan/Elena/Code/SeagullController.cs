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
    [Tooltip("Góc nhìn thứ nhất (First Person) của chim. Ví dụ: X=0, Y=0.2, Z=0.1")]
    public Vector3 cameraOffset = new Vector3(0f, 0.2f, 0.1f);

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
        ActiveSeagull = this;
        age = 0f;
        isControlled = false;
        localIsReturning = false;
        
        // Khóa trọng lực và va chạm để tránh rơi hoặc bị đẩy lệch vị trí
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false;
            rb.isKinematic = true;
        }
        var col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        transform.SetParent(null, true);

        // Hướng bay chếch lên khoảng 15 độ để tạo đà bay lên, tránh cắm đầu xuống đất
        Vector3 forwardXZ = transform.forward;
        forwardXZ.y = 0f;
        if (forwardXZ == Vector3.zero) forwardXZ = Vector3.forward;
        forwardXZ.Normalize();
        autoFlyDirection = forwardXZ + Vector3.up * 0.25f;
        autoFlyDirection.Normalize();
        
        transform.rotation = Quaternion.LookRotation(autoFlyDirection);
        
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
        
        // 2 giây đầu: Chim tự động bay về phía trước
        if (age < 2f)
        {
            transform.position += autoFlyDirection * flySpeed * Time.deltaTime;
            if (autoFlyDirection != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(autoFlyDirection);
            }
        }
        else
        {
            if (!isControlled)
            {
                isControlled = true;
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

    private void HandleOwnerControl()
    {
        // 1. Xoay chim bằng chuột
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        // Quay Yaw (Xoay Trái/Phải theo World Space)
        transform.Rotate(Vector3.up * mouseX * rotationSpeed * Time.deltaTime, Space.World);

        // Quay Pitch (Ngửa/Cúi theo Local Space)
        transform.Rotate(Vector3.left * mouseY * pitchSpeed * Time.deltaTime, Space.Self);

        // Giới hạn góc ngửa/cúi của chim tránh bị quay ngược đầu (Clamp pitch)
        Vector3 rot = transform.eulerAngles;
        float pitch = rot.x > 180f ? rot.x - 360f : rot.x;
        pitch = Mathf.Clamp(pitch, -80f, 80f);
        transform.eulerAngles = new Vector3(pitch, rot.y, 0f); // Khóa Roll = 0 để bay thẳng thăng bằng

        // 2. Di chuyển chim bằng phím WASD
        float moveH = Input.GetAxis("Horizontal");
        float moveV = Input.GetAxis("Vertical");
        Vector3 moveInput = new Vector3(moveH, 0f, moveV);

        bool isMoving = moveInput.magnitude > 0.01f;
        if (isMoving)
        {
            Vector3 flyDirection = transform.forward * moveV + transform.right * moveH;
            transform.position += flyDirection.normalized * flySpeed * Time.deltaTime;
        }

        // Cập nhật trạng thái bay sang server
        if (isMoving != isMovingNet.Value)
        {
            UpdateMovingStateServerRpc(isMoving);
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
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 10f);
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

        // Góc nhìn thứ nhất (First Person) - Camera đặt tại vị trí đầu của chim và xoay theo chim
        Vector3 targetPos = transform.TransformPoint(cameraOffset);
        cam.transform.position = targetPos;
        cam.transform.rotation = transform.rotation;
    }
}
