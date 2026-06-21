using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class CameraSwitcher : NetworkBehaviour
{
    [Header("Cấu hình")]
    [Tooltip("Không cần kéo thả vào đây, code sẽ tự tìm object có tag 'TopDownCam' trong Map")]
    public Transform topDownTarget; 
    public float transitionDuration = 2.0f;
    
    [Header("Scripts cần tắt/bật")]
    public MonoBehaviour[] scriptsToToggle;

    private Camera mainCamera;
    private bool isTopDownActive = false;
    private bool isTransitioning = false;
    
    // Biến kiểm soát vùng
    private bool isInsideZone = false;

    private Vector3 originalPos;
    private Quaternion originalRot;

    void Start()
    {
        // Ưu tiên lấy Camera nằm bên trong Prefab của nhân vật trước để không bị tranh chấp giữa 4 người chơi
        mainCamera = GetComponentInChildren<Camera>();
        
        // Nếu không tìm thấy (ví dụ setup sai), mới dùng đến Camera.main dự phòng
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        // Tìm vị trí Camera TopDown trong Map
        FindTopDownTarget();
    }

    void Update()
    {
        if (!IsOwner) return;

        // Đề phòng trường hợp Map load chậm, nếu chưa có target thì sẽ tự động tìm lại
        if (topDownTarget == null)
        {
            FindTopDownTarget();
        }

        // Nhấn B khi nằm trong vùng hoặc đang ở chế độ TopDown thì được kích hoạt
        if (Input.GetKeyDown(KeyCode.B) && !isTransitioning && (isInsideZone || isTopDownActive))
        {
            if (topDownTarget != null)
            {
                StartCoroutine(ToggleCameraView());
            }
            else
            {
                Debug.LogError("Lỗi: Không tìm thấy vị trí Camera trên Map! Hãy kiểm tra lại Tag.");
            }
        }
    }

    // Hàm tự động quét toàn bộ Scene để tìm Object có Tag là "TopDownCam"
    private void FindTopDownTarget()
    {
        if (topDownTarget == null)
        {
            // Hàm này CÓ THỂ tìm được object làm con của object khác, miễn là object đó đang Active
            GameObject targetObj = GameObject.FindGameObjectWithTag("TopDownCam");
            if (targetObj != null)
            {
                topDownTarget = targetObj.transform;
            }
        }
    }

    // Kiểm tra vùng
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("CameraZone")) isInsideZone = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("CameraZone")) isInsideZone = false;
    }

    IEnumerator ToggleCameraView()
    {
        isTransitioning = true;
        isTopDownActive = !isTopDownActive;

        if (isTopDownActive)
        {
            // Lưu lại vị trí và góc quay hiện tại của camera nhân vật
            originalPos = mainCamera.transform.position;
            originalRot = mainCamera.transform.rotation;
            
            // Tắt các script di chuyển/bắn súng...
            foreach (var s in scriptsToToggle) if (s != null) s.enabled = false;

            // Bắt đầu bay cam lên vị trí cố định của Map
            yield return StartCoroutine(MoveCamera(mainCamera.transform.position, mainCamera.transform.rotation, 
                                               topDownTarget.position, topDownTarget.rotation));
        }
        else
        {
            // Bắt đầu bay cam từ vị trí trên Map về lại vị trí cá nhân đã lưu
            yield return StartCoroutine(MoveCamera(mainCamera.transform.position, mainCamera.transform.rotation, 
                                               originalPos, originalRot));
            
            // Bật lại các script
            foreach (var s in scriptsToToggle) if (s != null) s.enabled = true;
        }

        isTransitioning = false;
    }

    IEnumerator MoveCamera(Vector3 startP, Quaternion startR, Vector3 endP, Quaternion endR)
    {
        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / transitionDuration);
            mainCamera.transform.position = Vector3.Lerp(startP, endP, t);
            mainCamera.transform.rotation = Quaternion.Slerp(startR, endR, t);
            yield return null;
        }
        mainCamera.transform.position = endP;
        mainCamera.transform.rotation = endR;
    }
}