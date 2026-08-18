using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

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

    [Header("Góc đóng ban đầu (Tự động ghi nhận)")]
    [SerializeField] private bool hasSavedClosedRotations = false;
    [SerializeField] private Quaternion defaultLeftClosedRot = Quaternion.identity;
    [SerializeField] private Quaternion defaultRightClosedRot = Quaternion.identity;

    [Header("Xem trước trong Editor (0 = Đóng, 1 = Mở)")]
    [Range(0f, 1f)]
    [SerializeField] private float previewProgress = 0f;

    private bool m_IsOpen = false;
    private float m_CurrentOpenProgress = 0f;

    public bool IsOpen => m_IsOpen;
    public float CurrentProgress => m_CurrentOpenProgress;

    /// <summary>
    /// Ghi nhận góc quay hiện tại của 2 cánh cửa làm góc ĐÓNG chuẩn
    /// </summary>
    public void SaveCurrentAsClosedRotation()
    {
        if (leftWing != null) defaultLeftClosedRot = leftWing.localRotation;
        if (rightWing != null) defaultRightClosedRot = rightWing.localRotation;
        hasSavedClosedRotations = true;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(this);
        }
#endif
    }

    /// <summary>
    /// Reset 2 cánh cửa về góc ĐÓNG chuẩn ban đầu
    /// </summary>
    public void ResetToClosedRotation()
    {
        EnsureClosedRotationsInitialized();
        if (leftWing != null) leftWing.localRotation = defaultLeftClosedRot;
        if (rightWing != null) rightWing.localRotation = defaultRightClosedRot;
        m_CurrentOpenProgress = 0f;
        previewProgress = 0f;
        m_IsOpen = false;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(this);
            SceneView.RepaintAll();
        }
#endif
    }

    private void EnsureClosedRotationsInitialized()
    {
        if (!hasSavedClosedRotations)
        {
            SaveCurrentAsClosedRotation();
        }
    }

    private void Awake()
    {
        EnsureClosedRotationsInitialized();
    }

    private void Start()
    {
        EnsureClosedRotationsInitialized();
        if (Application.isPlaying)
        {
            // Bắt đầu game ở vị trí đóng hoặc theo trạng thái m_IsOpen
            m_CurrentOpenProgress = m_IsOpen ? 1f : 0f;
            ApplyRotationByProgress(m_CurrentOpenProgress);
        }
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            EnsureClosedRotationsInitialized();
            ApplyRotationByProgress(previewProgress);
        }
    }

    /// <summary>
    /// Áp dụng góc xoay theo tiến trình từ 0.0 (Đóng) đến 1.0 (Mở)
    /// </summary>
    public void ApplyRotationByProgress(float progress)
    {
        EnsureClosedRotationsInitialized();
        progress = Mathf.Clamp01(progress);
        m_CurrentOpenProgress = progress;

        if (leftWing != null)
        {
            Quaternion leftOpenRot = defaultLeftClosedRot * Quaternion.Euler(leftOpenRotationOffset);
            leftWing.localRotation = Quaternion.Slerp(defaultLeftClosedRot, leftOpenRot, progress);
        }

        if (rightWing != null)
        {
            Quaternion rightOpenRot = defaultRightClosedRot * Quaternion.Euler(rightOpenRotationOffset);
            rightWing.localRotation = Quaternion.Slerp(defaultRightClosedRot, rightOpenRot, progress);
        }
    }

    private void Update()
    {
        if (Application.isPlaying)
        {
            float targetProgress = m_IsOpen ? 1f : 0f;
            if (!Mathf.Approximately(m_CurrentOpenProgress, targetProgress))
            {
                m_CurrentOpenProgress = Mathf.MoveTowards(m_CurrentOpenProgress, targetProgress, Time.deltaTime * openSpeed);
                ApplyRotationByProgress(m_CurrentOpenProgress);
            }
        }
    }

    [ContextMenu("Open Door")]
    public void OpenDoor()
    {
        m_IsOpen = true;
        Debug.Log("[DoubleDoorController] Lệnh MỞ cửa được kích hoạt.");

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            StartEditorAnimation(1f);
        }
#endif
    }

    [ContextMenu("Close Door")]
    public void CloseDoor()
    {
        m_IsOpen = false;
        Debug.Log("[DoubleDoorController] Lệnh ĐÓNG cửa được kích hoạt.");

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            StartEditorAnimation(0f);
        }
#endif
    }

    // Nhận trạng thái trực tiếp từ Unity Event của Trụ Final (True = Mở, False = Đóng)
    public void SetDoorState(bool open)
    {
        if (open) OpenDoor();
        else CloseDoor();
    }

#if UNITY_EDITOR
    private double m_LastEditorTime;
    private float m_EditorTargetProgress;
    private bool m_IsAnimatingInEditor = false;

    public void StartEditorAnimation(float target)
    {
        EnsureClosedRotationsInitialized();
        m_EditorTargetProgress = target;
        m_LastEditorTime = EditorApplication.timeSinceStartup;

        if (!m_IsAnimatingInEditor)
        {
            m_IsAnimatingInEditor = true;
            EditorApplication.update += HandleEditorUpdate;
        }
    }

    private void HandleEditorUpdate()
    {
        if (this == null || Application.isPlaying)
        {
            EditorApplication.update -= HandleEditorUpdate;
            m_IsAnimatingInEditor = false;
            return;
        }

        double currentTime = EditorApplication.timeSinceStartup;
        float dt = (float)(currentTime - m_LastEditorTime);
        m_LastEditorTime = currentTime;

        if (dt > 0.1f) dt = 0.1f;

        m_CurrentOpenProgress = Mathf.MoveTowards(m_CurrentOpenProgress, m_EditorTargetProgress, dt * openSpeed);
        previewProgress = m_CurrentOpenProgress;
        ApplyRotationByProgress(m_CurrentOpenProgress);

        EditorUtility.SetDirty(this);
        SceneView.RepaintAll();

        if (Mathf.Approximately(m_CurrentOpenProgress, m_EditorTargetProgress))
        {
            EditorApplication.update -= HandleEditorUpdate;
            m_IsAnimatingInEditor = false;
        }
    }

    private void OnDisable()
    {
        if (!Application.isPlaying && m_IsAnimatingInEditor)
        {
            EditorApplication.update -= HandleEditorUpdate;
            m_IsAnimatingInEditor = false;
        }
    }
#endif
}
