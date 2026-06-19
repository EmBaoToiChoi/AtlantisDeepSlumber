using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class CameraSwitcher : NetworkBehaviour
{
    [Header("Camera Switching")]
    public Camera topDownCamera; 
    public CanvasGroup fadePanel; // Gán Panel đen/trắng (nếu có) vào đây
    
    [Header("Scripts cần tắt/bật (Cực quan trọng)")]
    // HÃY KÉO SCRIPT ĐIỀU KHIỂN NHÂN VẬT (Movement, Controller) VÀO ĐÂY
    public MonoBehaviour[] scriptsToToggle; 

    [Header("Cấu hình")]
    public float transitionDuration = 2.0f; 

    private Camera mainCamera;
    private bool isTopDownActive = false;
    private bool isTransitioning = false;

    private Vector3 savedPos;
    private Quaternion savedRot;

    // Biến để lưu trạng thái xoay của nhân vật trước khi bay
    private Quaternion playerStartRot;

    void Start()
    {
        mainCamera = Camera.main;
        if (topDownCamera != null) topDownCamera.enabled = false;
        if (fadePanel != null) fadePanel.alpha = 0; 
    }

    void Update()
    {
        if (!IsOwner) return;
        if (Input.GetKeyDown(KeyCode.B) && !isTransitioning)
        {
            StartCoroutine(ToggleModeRoutine());
        }
    }

    IEnumerator ToggleModeRoutine()
    {
        isTransitioning = true;
        isTopDownActive = !isTopDownActive;

        // Kiểm tra an toàn cho MainCamera
        if (mainCamera == null) mainCamera = Camera.main;
        
        // KIỂM TRA QUAN TRỌNG: Nếu topDownCamera bị null thì thoát ngay
        if (topDownCamera == null)
        {
            Debug.LogError("Lỗi: Top Down Camera bị trống! Kiểm tra lại Inspector.");
            isTransitioning = false;
            yield break; // Thoát coroutine, không chạy tiếp dòng 51 nữa
        }

        Vector3 startPos = mainCamera.transform.position;
        Quaternion startRot = mainCamera.transform.rotation;

        if (isTopDownActive)
        {
            savedPos = startPos;
            savedRot = startRot;
            
            foreach (var s in scriptsToToggle) if (s != null) s.enabled = false;
            
            playerStartRot = transform.rotation;
            
            // Ở đây dòng 51 của bạn sẽ an toàn vì đã check null ở trên
            yield return StartCoroutine(MoveCamera(startPos, startRot, topDownCamera.transform.position, topDownCamera.transform.rotation));

            mainCamera.enabled = false;
            topDownCamera.enabled = true;
        }
        else
        {
            mainCamera.enabled = true;
            topDownCamera.enabled = false;

            yield return StartCoroutine(MoveCamera(topDownCamera.transform.position, topDownCamera.transform.rotation, savedPos, savedRot));

            transform.rotation = playerStartRot;
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