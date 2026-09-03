using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

/// <summary>
/// Quản lý con trỏ chuột trong toàn game (CursorManager).
/// Tự động đổi giữa Default Cursor (Cusor bth) và Hover Cursor (Cusor HOver) khi rê chuột vào Button/UI/Interactable.
/// Singleton + DontDestroyOnLoad tồn tại xuyên suốt mọi Scene (Menu, Lobby, Map).
/// </summary>
public class CursorManager : MonoBehaviour
{
    public static CursorManager Instance { get; private set; }

    [Header("Default Cursor (Chuột bình thường)")]
    [Tooltip("Kéo texture 'Cusor bth' vào đây")]
    public Texture2D defaultCursor;
    [Tooltip("Tọa độ điểm click (Hotspot). Mặc định là (0, 0)")]
    public Vector2 defaultHotspot = Vector2.zero;

    [Header("Hover Cursor (Chuột khi hover)")]
    [Tooltip("Kéo texture 'Cusor HOver' vào đây")]
    public Texture2D hoverCursor;
    [Tooltip("Tọa độ điểm click (Hotspot). Thường là đầu ngón tay chỉ (VD: 35, 2 với ảnh 64px hoặc 70, 4 với ảnh 128px)")]
    public Vector2 hoverHotspot = Vector2.zero;

    [Header("Cursor Settings")]
    [Tooltip("Chế độ hiển thị con trỏ: Auto (Hardware Cursor - mượt nhất, không trễ)")]
    public CursorMode cursorMode = CursorMode.Auto;

    [Header("Tự Động Nhận Diện Hover")]
    [Tooltip("Tự động chuyển chuột sang Hover khi rê qua Button, Toggle, Slider...")]
    public bool autoDetectUIHover = true;

    [Tooltip("Kiểm tra cả UI Toolkit (UIDocument trong PlayerHUD / Menu)")]
    public bool checkUIToolkit = true;

    [Header("3D Interactable Hover (Tuỳ chọn)")]
    [Tooltip("Bật nếu muốn khi rê chuột vào NPC, Rương... trong Scene 3D cũng đổi con trỏ")]
    public bool check3DInteractables = false;
    public LayerMask interactableLayers = ~0;
    public float maxRaycastDistance = 100f;

    private bool isHovering = false;
    private bool isCustomOverridden = false;

    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();
    private UIDocument[] cachedUIDocs;
    private float nextUIDocRefreshTime = 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        ApplyDefaultCursor();
    }

    private void Start()
    {
        ApplyDefaultCursor();
    }

    private void Update()
    {
        // Khi chuột bị khóa (ví dụ lúc đang điều khiển nhân vật đánh nhau hoặc xoay camera) thì bỏ qua
        if (Cursor.lockState == CursorLockMode.Locked || !Cursor.visible)
        {
            return;
        }

        if (isCustomOverridden) return;
        if (!autoDetectUIHover) return;

        bool shouldHover = CheckHoverConditions();

        if (shouldHover && !isHovering)
        {
            ApplyHoverCursor();
        }
        else if (!shouldHover && isHovering)
        {
            ApplyDefaultCursor();
        }
    }

    private bool CheckHoverConditions()
    {
        Vector2 mousePos = GetCurrentMousePosition();

        // 1. Kiểm tra UI UGUI (Canvas: Button, Slider, Toggle, Dropdown, Scrollbar...)
        if (EventSystem.current != null)
        {
            PointerEventData pointerData = new PointerEventData(EventSystem.current)
            {
                position = mousePos
            };

            uiRaycastResults.Clear();
            EventSystem.current.RaycastAll(pointerData, uiRaycastResults);

            for (int i = 0; i < uiRaycastResults.Count; i++)
            {
                var hit = uiRaycastResults[i];
                if (hit.gameObject == null) continue;

                // Kiểm tra Selectable của UGUI
                Selectable selectable = hit.gameObject.GetComponentInParent<Selectable>();
                if (selectable != null && selectable.interactable)
                {
                    return true;
                }

                // Kiểm tra component đánh dấu thủ công CursorHoverTarget
                if (hit.gameObject.GetComponentInParent<CursorHoverTarget>() != null)
                {
                    return true;
                }
            }
        }

        // 2. Kiểm tra UI Toolkit (UIDocument - PlayerHUD)
        if (checkUIToolkit)
        {
            UIDocument[] docs = GetActiveUIDocuments();
            if (docs != null)
            {
                for (int i = 0; i < docs.Length; i++)
                {
                    var doc = docs[i];
                    if (doc == null || !doc.isActiveAndEnabled || doc.rootVisualElement == null || doc.rootVisualElement.panel == null)
                        continue;

                    // UI Toolkit tọa độ (0,0) ở góc trên bên trái
                    Vector2 panelPos = new Vector2(mousePos.x, Screen.height - mousePos.y);

                    VisualElement picked = doc.rootVisualElement.panel.Pick(panelPos);
                    if (picked != null)
                    {
                        VisualElement curr = picked;
                        while (curr != null && curr != doc.rootVisualElement)
                        {
                            // Button UI Toolkit
                            if (curr is UnityEngine.UIElements.Button) return true;

                            // Class tương tác
                            if (curr.ClassListContains("unity-button") ||
                                curr.ClassListContains("clickable") ||
                                curr.ClassListContains("interactive") ||
                                curr.ClassListContains("cursor-hover"))
                            {
                                return true;
                            }

                            // Tên phần tử tương tác (upgrade button, item slot...)
                            string elemName = curr.name;
                            if (!string.IsNullOrEmpty(elemName))
                            {
                                if (elemName.StartsWith("btn-") || 
                                    elemName.StartsWith("slot-") || 
                                    elemName.Contains("button") || 
                                    elemName.Contains("upgrade"))
                                {
                                    return true;
                                }
                            }

                            curr = curr.parent;
                        }
                    }
                }
            }
        }

        // 3. Kiểm tra vật thể 3D tương tác (NPC, Rương, Cổng...) nếu bật
        if (check3DInteractables && Camera.main != null)
        {
            Ray ray = Camera.main.ScreenPointToRay(mousePos);
            if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance, interactableLayers))
            {
                if (hit.collider.GetComponentInParent<CursorHoverTarget>() != null ||
                    hit.collider.CompareTag("Interactable") ||
                    hit.collider.CompareTag("NPC"))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private UIDocument[] GetActiveUIDocuments()
    {
        if (Time.time >= nextUIDocRefreshTime || cachedUIDocs == null)
        {
            cachedUIDocs = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            nextUIDocRefreshTime = Time.time + 1.0f; // Cache 1 giây để tối ưu FPS
        }
        return cachedUIDocs;
    }

    private Vector2 GetCurrentMousePosition()
    {
        #if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Mouse.current != null)
        {
            return UnityEngine.InputSystem.Mouse.current.position.ReadValue();
        }
        #endif
        return Input.mousePosition;
    }

    /// <summary>
    /// Chuyển về con trỏ mặc định (Cusor bth)
    /// </summary>
    public void ApplyDefaultCursor()
    {
        isHovering = false;
        isCustomOverridden = false;
        if (defaultCursor != null)
        {
            Cursor.SetCursor(defaultCursor, defaultHotspot, cursorMode);
        }
        else
        {
            Cursor.SetCursor(null, Vector2.zero, cursorMode);
        }
    }

    /// <summary>
    /// Chuyển sang con trỏ Hover (Cusor HOver)
    /// </summary>
    public void ApplyHoverCursor()
    {
        isHovering = true;
        if (hoverCursor != null)
        {
            Cursor.SetCursor(hoverCursor, hoverHotspot, cursorMode);
        }
    }

    /// <summary>
    /// Đặt con trỏ chuột tùy biến tạm thời
    /// </summary>
    public void SetCustomCursor(Texture2D cursorTexture, Vector2 hotspot)
    {
        isCustomOverridden = true;
        Cursor.SetCursor(cursorTexture, hotspot, cursorMode);
    }

    /// <summary>
    /// Khôi phục lại chế độ chuột bình thường
    /// </summary>
    public void ResetCustomCursor()
    {
        isCustomOverridden = false;
        ApplyDefaultCursor();
    }
}
