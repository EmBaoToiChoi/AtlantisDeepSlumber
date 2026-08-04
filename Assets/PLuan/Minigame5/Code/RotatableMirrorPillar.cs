using UnityEngine;
using Unity.Netcode;
using System.Reflection;

public class RotatableMirrorPillar : NetworkBehaviour
{
    public enum RotationAxis { X, Y, Z }
    public enum MirrorForwardDirection
    {
        Forward,  // +Z
        Back,     // -Z
        Right,    // +X
        Left,     // -X
        Up,       // +Y
        Down      // -Y
    }

    [Header("Cấu hình xoay")]
    [SerializeField] private Transform rotatePart; // Phần đầu trụ chứa gương cần xoay (để trống nếu xoay cả object)
    [SerializeField] private RotationAxis rotationAxis = RotationAxis.Y; // Trục xoay: mặc định là Y (quay tròn), đổi thành X hoặc Z nếu pivot mesh bị xoay ngược
    [SerializeField] private float rotationStepAngle = 45f; // Góc xoay mỗi lần nhấn F (ví dụ: 45 độ)
    [SerializeField] private float rotateSpeed = 5f; // Tốc độ xoay mượt mà (chuyển động quay)

    [Header("Cấu hình tương tác xoay tự do")]
    [SerializeField] private string playerTag = "Player"; // Tag dùng để nhận diện người chơi
    [SerializeField] private float keyboardRotateSpeed = 90f; // Tốc độ xoay bằng phím A/D (độ/giây)
    [SerializeField] private float mouseRotateSpeed = 5f; // Tốc độ xoay (độ nhạy) khi rê chuột ngang

    [Header("Cấu hình Camera khi xoay")]
    [SerializeField] private bool useCustomCamera = true;
    [SerializeField] private float camDistance = 8f;
    [SerializeField] private float camHeight = 6f;
    [SerializeField] private float lookAheadDistance = 5f;
    [SerializeField] private float transitionDuration = 0.6f;

    [Header("Cấu hình điều khiển Camera tự do (Play Mode)")]
    [SerializeField] private float zoomSpeed = 8f; // Tốc độ zoom camera bằng cuộn chuột
    [SerializeField] private float minCamDistance = 3f; // Khoảng cách zoom tối thiểu
    [SerializeField] private float maxCamDistance = 30f; // Khoảng cách zoom tối đa (giúp nhìn bao quát phòng)
    [SerializeField] private float orbitSensitivity = 3f; // Tốc độ xoay camera bằng chuột phải

    [Header("Cấu hình hướng mặt gương / tia sáng")]
    [SerializeField] private MirrorForwardDirection mirrorForward = MirrorForwardDirection.Forward; // Trục local của rotatePart mà gương hướng mặt ra (tia laser bắn ra)
    [SerializeField] private float cameraAngleOffset = 0f; // Độ lệch góc camera so với hướng mặt gương (nếu muốn camera lệch sang bên góc nghiêng dễ nhìn hơn)

    // Biến mạng đồng bộ góc xoay mục tiêu (Chỉ Server có quyền ghi)
    private NetworkVariable<float> m_TargetRotationY = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private bool m_PlayerInRange = false;
    private bool m_IsControlling = false; // Trạng thái người chơi đang nắm giữ/xoay gương này
    private float m_CurrentRotationY = 0f;

    // Public properties để bạn dễ dàng gọi từ Code UI riêng của bạn sau này
    public bool IsPlayerInRange => m_PlayerInRange;
    public bool IsControllingMirror => m_IsControlling;
    private float m_OriginalRotationX = 0f;
    private float m_OriginalRotationY = 0f;
    private float m_OriginalRotationZ = 0f;

    // Camera variables
    private MonoBehaviour m_LocalPlayerScript = null;
    private bool m_TransitioningIn = false;
    private bool m_TransitioningOut = false;
    private float m_TransitionTimer = 0f;
    private Vector3 m_StartCameraPos;
    private Quaternion m_StartCameraRot;
    private Camera m_MainCamera;

    // Biến camera động điều khiển trong play mode
    private float m_DynamicCamDistance;
    private float m_CameraOrbitYawOffset = 0f;
    private float m_CameraOrbitPitchOffset = 0f;

    // Helper lấy hướng mặt gương tương ứng với trục local được thiết lập
    private Vector3 GetMirrorForward()
    {
        Transform t = rotatePart != null ? rotatePart : transform;
        
        switch (mirrorForward)
        {
            case MirrorForwardDirection.Back:
                return -t.forward;
            case MirrorForwardDirection.Right:
                return t.right;
            case MirrorForwardDirection.Left:
                return -t.right;
            case MirrorForwardDirection.Up:
                return t.up;
            case MirrorForwardDirection.Down:
                return -t.up;
            case MirrorForwardDirection.Forward:
            default:
                return t.forward;
        }
    }

    // Helper tạo Quaternion xoay dựa trên trục được chọn và GIỮ NGUYÊN 2 trục còn lại
    private Quaternion GetRotationForAxis(float angle)
    {
        switch (rotationAxis)
        {
            case RotationAxis.X: 
                return Quaternion.Euler(angle, m_OriginalRotationY, m_OriginalRotationZ);
            case RotationAxis.Z: 
                return Quaternion.Euler(m_OriginalRotationX, m_OriginalRotationY, angle);
            case RotationAxis.Y:
            default:
                return Quaternion.Euler(m_OriginalRotationX, angle, m_OriginalRotationZ);
        }
    }

    private void InitOriginalRotations()
    {
        if (rotatePart != null)
        {
            m_OriginalRotationX = rotatePart.localEulerAngles.x;
            m_OriginalRotationY = rotatePart.localEulerAngles.y;
            m_OriginalRotationZ = rotatePart.localEulerAngles.z;
        }
    }

    void Start()
    {
        if (rotatePart == null)
        {
            rotatePart = transform;
        }

        // Lưu góc xoay thiết lập trong Editor của cả 3 trục
        InitOriginalRotations();

        // Lấy góc xoay ban đầu của riêng trục hoạt động
        m_CurrentRotationY = rotationAxis == RotationAxis.X ? m_OriginalRotationX :
                             rotationAxis == RotationAxis.Z ? m_OriginalRotationZ :
                                                              m_OriginalRotationY;

        // Đồng bộ góc xoay ban đầu nếu đối tượng đã được sinh ra mạng
        if (IsSpawned)
        {
            m_CurrentRotationY = m_TargetRotationY.Value;
            rotatePart.localRotation = GetRotationForAxis(m_CurrentRotationY);
        }
    }

    public override void OnNetworkSpawn()
    {
        m_TargetRotationY.OnValueChanged += OnRotationValueChanged;

        // Đảm bảo lấy lại góc xoay ban đầu của Editor phòng trường hợp Start chưa chạy
        InitOriginalRotations();

        // Đồng bộ góc xoay hiện tại khớp với Server khi mới vào game
        m_CurrentRotationY = m_TargetRotationY.Value;
        rotatePart.localRotation = GetRotationForAxis(m_CurrentRotationY);
    }

    public override void OnNetworkDespawn()
    {
        m_TargetRotationY.OnValueChanged -= OnRotationValueChanged;
    }

    private void OnRotationValueChanged(float previousValue, float newValue)
    {
        // Góc xoay mạng thay đổi sẽ tự động chạy trong Update() trên mọi máy khách
    }

    void Update()
    {
        // 1. Tương tác bật/tắt chế độ điều khiển bằng phím F
        if (m_PlayerInRange && Input.GetKeyDown(KeyCode.F))
        {
            if (!m_IsControlling)
            {
                m_IsControlling = true;
                Debug.Log($"[RotatableMirrorPillar] Bắt đầu điều khiển trụ.");
                
                // Khởi tạo các giá trị camera động từ mặc định cấu hình
                m_DynamicCamDistance = camDistance;
                m_CameraOrbitYawOffset = 0f;
                m_CameraOrbitPitchOffset = 0f;

                if (m_LocalPlayerScript != null)
                {
                    ResetPlayerMovementAndAnimations();
                    SetPlayerField("enableCameraFollow", false);
                    m_LocalPlayerScript.enabled = false;

                    if (useCustomCamera)
                    {
                        InitMainCamera();
                        if (m_MainCamera != null)
                        {
                            m_StartCameraPos = m_MainCamera.transform.position;
                            m_StartCameraRot = m_MainCamera.transform.rotation;
                        }
                        m_TransitionTimer = 0f;
                        m_TransitioningIn = true;
                        m_TransitioningOut = false;
                    }
                }
            }
            else
            {
                m_IsControlling = false;
                Debug.Log($"[RotatableMirrorPillar] Thoát điều khiển trụ.");

                if (m_LocalPlayerScript != null)
                {
                    m_LocalPlayerScript.enabled = true;
                    SetPlayerField("enableCameraFollow", false);

                    if (useCustomCamera && m_MainCamera != null)
                    {
                        float camEulerY = m_MainCamera.transform.eulerAngles.y;
                        float newYaw = (360f - camEulerY) % 360f;
                        SetPlayerField("currentYaw", newYaw);
                        SetPlayerField("targetYaw", newYaw);

                        m_StartCameraPos = m_MainCamera.transform.position;
                        m_StartCameraRot = m_MainCamera.transform.rotation;
                        m_TransitionTimer = 0f;
                        m_TransitioningOut = true;
                        m_TransitioningIn = false;
                    }
                    else
                    {
                        SetPlayerField("enableCameraFollow", true);
                        m_LocalPlayerScript = null;
                    }
                }
            }
        }

        // 2. Nếu đang trong chế độ điều khiển: người chơi tự xoay cục bộ theo thời gian thực (không có độ trễ)
        if (m_IsControlling)
        {
            // A. Zoom camera bằng nút cuộn chuột (Scroll Wheel)
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                m_DynamicCamDistance = Mathf.Clamp(m_DynamicCamDistance - scroll * zoomSpeed, minCamDistance, maxCamDistance);
            }

            // B. Xoay/Tilt camera tự do bằng cách Giữ Chuột Phải và rê chuột
            if (Input.GetMouseButton(1))
            {
                m_CameraOrbitYawOffset += Input.GetAxis("Mouse X") * orbitSensitivity;
                m_CameraOrbitPitchOffset = Mathf.Clamp(m_CameraOrbitPitchOffset - Input.GetAxis("Mouse Y") * orbitSensitivity * 0.5f, -15f, 20f); // Giới hạn góc tilt để tránh xuyên đất/trần quá mức
            }

            // C. Xoay gương bằng phím A/D (hoặc rê chuột trái/di chuột khi KHÔNG giữ chuột phải)
            float input = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) input -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) input += 1f;

            // Rê chuột bình thường (không bấm chuột phải) sẽ xoay gương
            if (!Input.GetMouseButton(1))
            {
                input += Input.GetAxis("Mouse X") * mouseRotateSpeed;
            }

            if (Mathf.Abs(input) > 0.01f)
            {
                // Cập nhật góc xoay hiện tại cục bộ
                m_CurrentRotationY = (m_CurrentRotationY + input * keyboardRotateSpeed * Time.deltaTime) % 360f;
                rotatePart.localRotation = GetRotationForAxis(m_CurrentRotationY);

                // Gửi góc xoay mới lên Server để đồng bộ cho các Client khác (Chỉ chạy khi đã Spawn mạng)
                if (IsSpawned)
                {
                    UpdateRotationServerRpc(m_CurrentRotationY);
                }
            }
        }
        else
        {
            // 3. Nếu KHÔNG trong chế độ điều khiển: xoay mượt mà đồng bộ theo góc mục tiêu từ mạng
            // (Chỉ đồng bộ khi đã Spawn mạng, nếu chạy offline thì giữ nguyên góc hiện tại)
            if (IsSpawned)
            {
                m_CurrentRotationY = Mathf.MoveTowardsAngle(m_CurrentRotationY, m_TargetRotationY.Value, rotateSpeed * 100f * Time.deltaTime);
            }
            rotatePart.localRotation = GetRotationForAxis(m_CurrentRotationY);
        }
    }

    void LateUpdate()
    {
        if (!useCustomCamera || m_LocalPlayerScript == null) return;

        InitMainCamera();
        if (m_MainCamera == null) return;

        Transform t = rotatePart != null ? rotatePart : transform;

        // Tính hướng mặt gương và hướng camera nhìn theo (kèm offset xoay tự do của người chơi)
        Vector3 mirrorForwardDir = GetMirrorForward();
        Vector3 lookDir = Quaternion.AngleAxis(cameraAngleOffset + m_CameraOrbitYawOffset, t.up) * mirrorForwardDir;
        lookDir.Normalize();

        // Tính toán vị trí camera mục tiêu dựa vào khoảng cách zoom và độ cao đã tilt
        float currentHeight = camHeight + m_CameraOrbitPitchOffset;
        Vector3 targetCamPos = t.position - lookDir * m_DynamicCamDistance + Vector3.up * currentHeight;
        Vector3 rayPivot = t.position + Vector3.up * (currentHeight * 0.5f);
        targetCamPos = GetClampedCameraPosition(rayPivot, targetCamPos);
        
        Vector3 lookTarget = t.position + lookDir * lookAheadDistance;
        Quaternion targetCamRot = Quaternion.LookRotation(lookTarget - targetCamPos);

        if (m_TransitioningIn)
        {
            m_TransitionTimer += Time.deltaTime;
            float tVal = Mathf.Clamp01(m_TransitionTimer / transitionDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, tVal);

            m_MainCamera.transform.position = Vector3.Lerp(m_StartCameraPos, targetCamPos, smoothT);
            m_MainCamera.transform.rotation = Quaternion.Slerp(m_StartCameraRot, targetCamRot, smoothT);

            if (tVal >= 1f)
            {
                m_TransitioningIn = false;
            }
        }
        else if (m_TransitioningOut)
        {
            m_TransitionTimer += Time.deltaTime;
            float tVal = Mathf.Clamp01(m_TransitionTimer / transitionDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, tVal);

            // Compute the player's normal camera position and rotation dynamically
            float p_Yaw = GetPlayerField<float>("currentYaw", 0f);
            float p_Pitch = GetPlayerField<float>("currentPitch", 0f);
            float p_Dist = GetPlayerField<float>("cameraDistance", 14f);
            float p_Pivot = GetPlayerField<float>("cameraPivotHeight", 1.0f);
            float p_Shoulder = GetPlayerField<float>("currentShoulderOffset", 0f);

            float yawRad = p_Yaw * Mathf.Deg2Rad;
            float pitchRad = p_Pitch * Mathf.Deg2Rad;

            Vector3 rotatedOffset = new Vector3(
                p_Dist * Mathf.Cos(pitchRad) * Mathf.Sin(yawRad),
                p_Dist * Mathf.Sin(pitchRad),
                -p_Dist * Mathf.Cos(pitchRad) * Mathf.Cos(yawRad)
            );

            Vector3 camRight = new Vector3(Mathf.Cos(yawRad), 0f, Mathf.Sin(yawRad));
            Vector3 pivotPosition = (m_LocalPlayerScript.transform.position + Vector3.up * p_Pivot) + camRight * p_Shoulder;
            Vector3 playerCamPos = pivotPosition + rotatedOffset;

            // Apply camera collision check for player camera
            playerCamPos = GetClampedCameraPosition(pivotPosition, playerCamPos);
            Quaternion playerCamRot = Quaternion.LookRotation(pivotPosition - playerCamPos);

            m_MainCamera.transform.position = Vector3.Lerp(m_StartCameraPos, playerCamPos, smoothT);
            m_MainCamera.transform.rotation = Quaternion.Slerp(m_StartCameraRot, playerCamRot, smoothT);

            if (tVal >= 1f)
            {
                m_TransitioningOut = false;
                SetPlayerField("enableCameraFollow", true);
                m_LocalPlayerScript = null;
            }
        }
        else if (m_IsControlling)
        {
            // Khóa camera theo các biến vị trí/xoay đã tính toán
            m_MainCamera.transform.position = targetCamPos;
            m_MainCamera.transform.rotation = targetCamRot;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void UpdateRotationServerRpc(float angle)
    {
        m_TargetRotationY.Value = angle;
    }

    // Phát hiện người chơi đi vào phạm vi tương tác (Yêu cầu Trụ có gắn Trigger Collider)
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            NetworkObject netObj = other.GetComponentInParent<NetworkObject>();
            // Chỉ cho phép Local Player (người chơi trên máy này) tương tác
            if (netObj == null || netObj.IsLocalPlayer)
            {
                m_PlayerInRange = true;
                
                // Find player script
                m_LocalPlayerScript = other.GetComponentInParent<MonoBehaviour>();
                if (m_LocalPlayerScript != null)
                {
                    var type = m_LocalPlayerScript.GetType();
                    var enableCamField = type.GetField("enableCameraFollow", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (enableCamField == null)
                    {
                        var rootObj = netObj != null ? netObj.gameObject : other.transform.root.gameObject;
                        var components = rootObj.GetComponentsInChildren<MonoBehaviour>();
                        foreach (var comp in components)
                        {
                            if (comp.GetType().GetField("enableCameraFollow", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance) != null)
                            {
                                m_LocalPlayerScript = comp;
                                break;
                            }
                        }
                    }
                }
                Debug.Log("[RotatableMirrorPillar] Người chơi bước vào vùng xoay gương. Nhấn 'F' để tương tác.");
            }
        }
    }

    // Phát hiện người chơi rời đi
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            NetworkObject netObj = other.GetComponentInParent<NetworkObject>();
            if (netObj == null || netObj.IsLocalPlayer)
            {
                m_PlayerInRange = false;
                if (m_IsControlling)
                {
                    m_IsControlling = false;
                    if (m_LocalPlayerScript != null)
                    {
                        m_LocalPlayerScript.enabled = true;
                        SetPlayerField("enableCameraFollow", false);

                        InitMainCamera();
                        if (m_MainCamera != null)
                        {
                            float camEulerY = m_MainCamera.transform.eulerAngles.y;
                            float newYaw = (360f - camEulerY) % 360f;
                            SetPlayerField("currentYaw", newYaw);
                            SetPlayerField("targetYaw", newYaw);

                            m_StartCameraPos = m_MainCamera.transform.position;
                            m_StartCameraRot = m_MainCamera.transform.rotation;
                            m_TransitionTimer = 0f;
                            m_TransitioningOut = true;
                            m_TransitioningIn = false;
                        }
                        else
                        {
                            SetPlayerField("enableCameraFollow", true);
                            m_LocalPlayerScript = null;
                        }
                    }
                }
                else
                {
                    if (!m_TransitioningOut)
                    {
                        m_LocalPlayerScript = null;
                    }
                }
                Debug.Log("[RotatableMirrorPillar] Người chơi rời khỏi vùng xoay gương.");
            }
        }
    }

    private void InitMainCamera()
    {
        if (m_MainCamera == null)
        {
            m_MainCamera = Camera.main;
            if (m_MainCamera == null)
            {
                m_MainCamera = FindObjectOfType<Camera>();
            }
        }
    }

    private void ResetPlayerMovementAndAnimations()
    {
        if (m_LocalPlayerScript == null) return;

        var rb = m_LocalPlayerScript.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        var anim = m_LocalPlayerScript.GetComponentInChildren<Animator>();
        if (anim != null)
        {
            foreach (var param in anim.parameters)
            {
                if (param.type == AnimatorControllerParameterType.Float)
                {
                    string pName = param.name.ToLower();
                    if (pName == "speed" || pName == "magnitude" || pName == "movex" || pName == "movez" || pName == "inputx" || pName == "inputz")
                    {
                        anim.SetFloat(param.name, 0f);
                    }
                }
                else if (param.type == AnimatorControllerParameterType.Bool)
                {
                    string pName = param.name.ToLower();
                    if (pName == "ismoving" || pName == "moving" || pName == "isrunning" || pName == "running")
                    {
                        anim.SetBool(param.name, false);
                    }
                }
            }
            anim.Play("Idle");
        }
    }

    private void SetPlayerField(string fieldName, object value)
    {
        if (m_LocalPlayerScript == null) return;
        System.Type type = m_LocalPlayerScript.GetType();
        var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(m_LocalPlayerScript, value);
        }
    }

    private T GetPlayerField<T>(string fieldName, T defaultValue = default)
    {
        if (m_LocalPlayerScript == null) return defaultValue;
        System.Type type = m_LocalPlayerScript.GetType();
        var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null)
        {
            try
            {
                return (T)field.GetValue(m_LocalPlayerScript);
            }
            catch
            {
                return defaultValue;
            }
        }
        return defaultValue;
    }

    private Vector3 GetClampedCameraPosition(Vector3 pivot, Vector3 targetPos)
    {
        float collisionSafetyDistance = 0.4f;
        int cameraLayerMask = ~LayerMask.GetMask("Player", "Ignore Raycast", "UI", "Water");
        Vector3 rayDirection = targetPos - pivot;
        float maxRayDistance = rayDirection.magnitude;

        RaycastHit[] hits = Physics.SphereCastAll(pivot, 0.2f, rayDirection.normalized, maxRayDistance, cameraLayerMask, QueryTriggerInteraction.Ignore);
        
        RaycastHit closestHit = default;
        bool hasHit = false;
        float minDistance = float.MaxValue;

        foreach (var hit in hits)
        {
            if (hit.collider == null || hit.collider.isTrigger)
                continue;

            if (hit.transform.IsChildOf(transform) || hit.transform == transform)
                continue;

            if (hit.distance < 0.2f)
                continue;

            if (hit.distance < minDistance)
            {
                minDistance = hit.distance;
                closestHit = hit;
                hasHit = true;
            }
        }

        if (hasHit)
        {
            float clampedDistance = Mathf.Max(1.5f, closestHit.distance - collisionSafetyDistance);
            return pivot + rayDirection.normalized * clampedDistance;
        }
        return targetPos;
    }

    private void OnDrawGizmosSelected()
    {
        Transform t = rotatePart != null ? rotatePart : transform;

        // Calculate direction based on settings
        Vector3 mirrorForwardDir = GetMirrorForward();
        Vector3 lookDir = Quaternion.AngleAxis(cameraAngleOffset, t.up) * mirrorForwardDir;
        lookDir.Normalize();

        Vector3 targetCamPos = t.position - lookDir * camDistance + Vector3.up * camHeight;
        Vector3 lookTarget = t.position + lookDir * lookAheadDistance;

        // Draw mirror forward direction (Cyan ray)
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(t.position, mirrorForwardDir * 4f);

        // Draw camera position sphere
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(targetCamPos, 0.4f);

        // Draw connection line
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(targetCamPos, t.position);

        // Draw camera frustum
        if (lookTarget != targetCamPos)
        {
            Gizmos.color = Color.green;
            Matrix4x4 tempMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(targetCamPos, Quaternion.LookRotation(lookTarget - targetCamPos), Vector3.one);
            Gizmos.DrawFrustum(Vector3.zero, 40f, 3f, 0.3f, 1.7f);
            Gizmos.matrix = tempMatrix;
        }
    }
}
