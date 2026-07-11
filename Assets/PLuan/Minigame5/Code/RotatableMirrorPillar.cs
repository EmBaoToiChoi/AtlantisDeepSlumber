using UnityEngine;
using Unity.Netcode;

public class RotatableMirrorPillar : NetworkBehaviour
{
    public enum RotationAxis { X, Y, Z }

    [Header("Cấu hình xoay")]
    [SerializeField] private Transform rotatePart; // Phần đầu trụ chứa gương cần xoay (để trống nếu xoay cả object)
    [SerializeField] private RotationAxis rotationAxis = RotationAxis.Y; // Trục xoay: mặc định là Y (quay tròn), đổi thành X hoặc Z nếu pivot mesh bị xoay ngược
    [SerializeField] private float rotationStepAngle = 45f; // Góc xoay mỗi lần nhấn F (ví dụ: 45 độ)
    [SerializeField] private float rotateSpeed = 5f; // Tốc độ xoay mượt mà (chuyển động quay)

    [Header("Cấu hình tương tác")]
    [SerializeField] private string playerTag = "Player"; // Tag dùng để nhận diện người chơi

    // Biến mạng đồng bộ góc xoay mục tiêu (Chỉ Server có quyền ghi)
    private NetworkVariable<float> m_TargetRotationY = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private bool m_PlayerInRange = false;
    private float m_CurrentRotationY = 0f;
    private float m_OriginalRotationX = 0f;
    private float m_OriginalRotationY = 0f;
    private float m_OriginalRotationZ = 0f;

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
        // 1. Xoay mượt mà đến góc xoay mục tiêu trên cả Server và Client
        m_CurrentRotationY = Mathf.MoveTowardsAngle(m_CurrentRotationY, m_TargetRotationY.Value, rotateSpeed * 100f * Time.deltaTime);
        rotatePart.localRotation = GetRotationForAxis(m_CurrentRotationY);

        // 2. Nhận lệnh nhấn F từ người chơi tại máy khách tương ứng
        if (m_PlayerInRange && Input.GetKeyDown(KeyCode.F))
        {
            RotatePillarServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RotatePillarServerRpc()
    {
        // Tính toán góc xoay tiếp theo (xoay vòng 360 độ)
        float nextRotation = (m_TargetRotationY.Value + rotationStepAngle) % 360f;
        m_TargetRotationY.Value = nextRotation;
        Debug.Log($"[RotatableMirrorPillar] Server ghi nhận yêu cầu xoay cột. Góc mới = {nextRotation} độ.");
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
                Debug.Log("[RotatableMirrorPillar] Người chơi bước vào vùng xoay gương. Nhấn 'F' để xoay.");
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
                Debug.Log("[RotatableMirrorPillar] Người chơi rời khỏi vùng xoay gương.");
            }
        }
    }
}
