using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class CameraSwitcher : NetworkBehaviour
{
    [Header("Cấu hình")]
    public Transform topDownTarget; 
    public float transitionDuration = 2.0f;
    
    [Header("Scripts cần tắt/bật")]
    public MonoBehaviour[] scriptsToToggle;

    private Camera mainCamera;
    private bool isTopDownActive = false;
    private bool isTransitioning = false;
    
    // [THÊM MỚI] Biến kiểm soát vùng
    private bool isInsideZone = false;

    private Vector3 originalPos;
    private Quaternion originalRot;

    void Start()
    {
        mainCamera = Camera.main;
    }

    void Update()
    {
        if (!IsOwner) return;

        // [SỬA] Thêm điều kiện (isInsideZone || isTopDownActive)
        // Nghĩa là: Phải trong vùng mới được bật, HOẶC nếu đang ở TopDown rồi thì phải cho nhấn B để quay về
        if (Input.GetKeyDown(KeyCode.B) && !isTransitioning && (isInsideZone || isTopDownActive))
        {
            StartCoroutine(ToggleCameraView());
        }
    }

    // [THÊM MỚI] Kiểm tra vùng
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
            originalPos = mainCamera.transform.position;
            originalRot = mainCamera.transform.rotation;
            foreach (var s in scriptsToToggle) if (s != null) s.enabled = false;

            yield return StartCoroutine(MoveCamera(mainCamera.transform.position, mainCamera.transform.rotation, 
                                               topDownTarget.position, topDownTarget.rotation));
        }
        else
        {
            yield return StartCoroutine(MoveCamera(mainCamera.transform.position, mainCamera.transform.rotation, 
                                               originalPos, originalRot));
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