using UnityEngine;

[ExecuteAlways]
public class DoubleDoorController : MonoBehaviour
{
    [Header("Cánh cửa")]
    [SerializeField] private Transform leftWing; // Cánh cửa bên trái
    [SerializeField] private Transform rightWing; // Cánh cửa bên phải

    [Header("Cấu hình xoay")]
    [SerializeField] private Vector3 leftOpenRotationOffset = new Vector3(0, -90f, 0); // Độ lệch xoay khi mở của cánh trái (thường âm quanh Y)
    [SerializeField] private Vector3 rightOpenRotationOffset = new Vector3(0, 90f, 0); // Độ lệch xoay khi mở của cánh phải (thường dương quanh Y)
    [SerializeField] private float openSpeed = 2f; // Tốc độ mở cửa mượt mà

    private Quaternion m_LeftClosedRot;
    private Quaternion m_LeftOpenRot;
    private Quaternion m_RightClosedRot;
    private Quaternion m_RightOpenRot;

    private bool m_IsOpen = false;
    private bool m_Initialized = false;

    private void InitRotations()
    {
        if (m_Initialized) return;

        // Lưu giữ góc đóng ban đầu được thiết lập trong scene
        if (leftWing != null)
        {
            m_LeftClosedRot = leftWing.localRotation;
            m_LeftOpenRot = m_LeftClosedRot * Quaternion.Euler(leftOpenRotationOffset);
        }
        if (rightWing != null)
        {
            m_RightClosedRot = rightWing.localRotation;
            m_RightOpenRot = m_RightClosedRot * Quaternion.Euler(rightOpenRotationOffset);
        }
        
        m_Initialized = true;
    }

    void Start()
    {
        InitRotations();
    }

    void OnEnable()
    {
        m_Initialized = false; // Reset để nhận diện lại góc xoay khi bật/tắt component
    }

    void Update()
    {
        // Đảm bảo khởi tạo các góc xoay gốc
        InitRotations();

        // Trong Edit Mode, nếu người chơi thay đổi Vector cấu hình góc mở trong Inspector, cập nhật lại góc mục tiêu tương ứng
        if (!Application.isPlaying)
        {
            if (leftWing != null) m_LeftOpenRot = m_LeftClosedRot * Quaternion.Euler(leftOpenRotationOffset);
            if (rightWing != null) m_RightOpenRot = m_RightClosedRot * Quaternion.Euler(rightOpenRotationOffset);
        }

        // Nội suy mượt mà góc xoay của 2 cánh cửa
        if (leftWing != null)
        {
            Quaternion targetRot = m_IsOpen ? m_LeftOpenRot : m_LeftClosedRot;
            leftWing.localRotation = Quaternion.Slerp(leftWing.localRotation, targetRot, Time.deltaTime * openSpeed);
        }

        if (rightWing != null)
        {
            Quaternion targetRot = m_IsOpen ? m_RightOpenRot : m_RightClosedRot;
            rightWing.localRotation = Quaternion.Slerp(rightWing.localRotation, targetRot, Time.deltaTime * openSpeed);
        }
    }

    [ContextMenu("Open Door")]
    public void OpenDoor()
    {
        m_IsOpen = true;
        Debug.Log("[DoubleDoorController] Lệnh MỞ cửa được kích hoạt.");

        #if UnityEditor
        if (!Application.isPlaying)
        {
            // Buộc Unity Editor cập nhật khung hình để thấy hiệu ứng xoay cửa trong Scene view
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
        }
        #endif
    }

    [ContextMenu("Close Door")]
    public void CloseDoor()
    {
        m_IsOpen = false;
        Debug.Log("[DoubleDoorController] Lệnh ĐÓNG cửa được kích hoạt.");

        #if UnityEditor
        if (!Application.isPlaying)
        {
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
        }
        #endif
    }

    // Nhận trạng thái trực tiếp từ Unity Event của Trụ Final (True = Mở, False = Đóng)
    public void SetDoorState(bool open)
    {
        m_IsOpen = open;
    }
}
