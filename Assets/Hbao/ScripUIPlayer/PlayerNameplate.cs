using UnityEngine;
using UnityEngine.UIElements;

public class PlayerNameplate : MonoBehaviour
{
    [Tooltip("Chiều cao offset so với gốc của player để vẽ tên trên đầu")]
    [SerializeField] private float heightOffset = 2.2f;

    private UIDocument uiDocument;
    private VisualElement container;
    private Label nameLabel;
    private IPlayerHUDTarget playerTarget;

    private void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
        playerTarget = GetComponentInParent<IPlayerHUDTarget>();
    }

    private void Start()
    {
        if (uiDocument != null && uiDocument.rootVisualElement != null)
        {
            container = uiDocument.rootVisualElement.Q<VisualElement>("nameplate-container");
            nameLabel = uiDocument.rootVisualElement.Q<Label>("player-name-label");
        }

        if (playerTarget == null)
        {
            Debug.LogWarning("[PlayerNameplate] Không tìm thấy script Player triển khai IPlayerHUDTarget ở lớp cha!");
        }
    }

    private void LateUpdate()
    {
        if (playerTarget == null || container == null || nameLabel == null) return;

        // Cập nhật tên hiển thị
        nameLabel.text = playerTarget.DisplayName;

        // Định vị trên màn hình dựa theo vị trí 3D trên đầu nhân vật
        if (Camera.main != null)
        {
            Vector3 worldPos = playerTarget.transform.position + Vector3.up * heightOffset;
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

            // Kiểm tra xem vị trí có nằm trước camera không
            if (screenPos.z > 0)
            {
                var panel = uiDocument.rootVisualElement.panel;
                if (panel != null)
                {
                    // Chuyển đổi tọa độ Screen sang Panel UI Toolkit
                    Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
                    
                    container.style.left = panelPos.x;
                    container.style.top = panelPos.y;
                    container.style.display = DisplayStyle.Flex;
                }
            }
            else
            {
                container.style.display = DisplayStyle.None;
            }
        }
        else
        {
            container.style.display = DisplayStyle.None;
        }
    }
}
