using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Gắn component này vào bất kỳ GameObject nào (UGUI Image, Text, Panel hoặc 3D Collider như NPC, Rương)
/// để khi rê chuột vào nó sẽ tự động kích hoạt con trỏ chuột Hover!
/// </summary>
public class CursorHoverTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Custom Cursor (Tùy chọn)")]
    [Tooltip("Nếu để trống, sẽ tự dùng con trỏ Hover chung của CursorManager")]
    public Texture2D customHoverCursor;
    public Vector2 customHotspot = Vector2.zero;

    // Khi chuột rê vào đối tượng UI (Canvas / UGUI)
    public void OnPointerEnter(PointerEventData eventData)
    {
        TriggerHover();
    }

    // Khi chuột rời khỏi đối tượng UI (Canvas / UGUI)
    public void OnPointerExit(PointerEventData eventData)
    {
        TriggerExit();
    }

    // Khi chuột rê vào Collider 3D/2D
    private void OnMouseEnter()
    {
        TriggerHover();
    }

    // Khi chuột rời khỏi Collider 3D/2D
    private void OnMouseExit()
    {
        TriggerExit();
    }

    private void TriggerHover()
    {
        if (CursorManager.Instance == null) return;

        if (customHoverCursor != null)
        {
            CursorManager.Instance.SetCustomCursor(customHoverCursor, customHotspot);
        }
        else
        {
            CursorManager.Instance.ApplyHoverCursor();
        }
    }

    private void TriggerExit()
    {
        if (CursorManager.Instance == null) return;

        if (customHoverCursor != null)
        {
            CursorManager.Instance.ResetCustomCursor();
        }
        else
        {
            CursorManager.Instance.ApplyDefaultCursor();
        }
    }

    private void OnDisable()
    {
        // Tránh tình trạng object bị ẩn/tắt khi chuột vẫn đang hover làm kẹt con trỏ
        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.ApplyDefaultCursor();
        }
    }
}
