using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class CameraSwitcher : NetworkBehaviour
{
    [Header("Cấu hình")]
    public Transform topDownTarget; // Kéo một Empty Object đặt ở góc nhìn Top-Down vào đây
    public float transitionDuration = 2.0f;
    
    [Header("Scripts cần tắt/bật")]
    public MonoBehaviour[] scriptsToToggle;

    private Camera mainCamera;
    private bool isTopDownActive = false;
    private bool isTransitioning = false;

    // Lưu trạng thái cũ của Camera để khi bay về thì nó về đúng vị trí cũ
    private Vector3 originalPos;
    private Quaternion originalRot;

    void Start()
    {
        mainCamera = Camera.main;
    }

    void Update()
    {
        if (!IsOwner) return;
        if (Input.GetKeyDown(KeyCode.B) && !isTransitioning)
        {
            StartCoroutine(ToggleCameraView());
        }
    }

    IEnumerator ToggleCameraView()
    {
        isTransitioning = true;
        isTopDownActive = !isTopDownActive;

        if (isTopDownActive)
        {
            // Lưu vị trí hiện tại của cam chính trước khi di chuyển
            originalPos = mainCamera.transform.position;
            originalRot = mainCamera.transform.rotation;

            // Tắt script điều khiển nhân vật
            foreach (var s in scriptsToToggle) if (s != null) s.enabled = false;

            // Di chuyển cam chính đến vị trí topDownTarget
            yield return StartCoroutine(MoveCamera(mainCamera.transform.position, mainCamera.transform.rotation, 
                                                   topDownTarget.position, topDownTarget.rotation));
        }
        else
        {
            // Di chuyển cam chính về vị trí cũ
            yield return StartCoroutine(MoveCamera(mainCamera.transform.position, mainCamera.transform.rotation, 
                                                   originalPos, originalRot));

            // Bật lại script điều khiển
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