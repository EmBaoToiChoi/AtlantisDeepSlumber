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

        if (mainCamera == null) mainCamera = Camera.main;

        Vector3 startPos = mainCamera.transform.position;
        Quaternion startRot = mainCamera.transform.rotation;

        if (isTopDownActive)
        {
            // --- MỞ TOP DOWN ---
            savedPos = startPos;
            savedRot = startRot;
            
            // 1. TẮT TẤT CẢ SCRIPT (Bao gồm script di chuyển nhân vật)
            foreach (var s in scriptsToToggle) if (s != null) s.enabled = false;
            
            // 2. Lưu lại hướng xoay hiện tại của nhân vật để không bị lỗi trục
            playerStartRot = transform.rotation;
            
            // 3. Bay camera (Đã tắt xoay nhân vật nên nó sẽ đứng yên)
            yield return StartCoroutine(MoveCamera(startPos, startRot, topDownCamera.transform.position, topDownCamera.transform.rotation));

            mainCamera.enabled = false;
            topDownCamera.enabled = true;
        }
        else
        {
            // --- QUAY LẠI ---
            mainCamera.enabled = true;
            topDownCamera.enabled = false;

            // 1. Bay camera về vị trí cũ
            yield return StartCoroutine(MoveCamera(topDownCamera.transform.position, topDownCamera.transform.rotation, savedPos, savedRot));

            // 2. Chốt lại góc xoay của nhân vật trước khi bật script điều khiển
            transform.rotation = playerStartRot;

            // 3. BẬT LẠI TẤT CẢ SCRIPT
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