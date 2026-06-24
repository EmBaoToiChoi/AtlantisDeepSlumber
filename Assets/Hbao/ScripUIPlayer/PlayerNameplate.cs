using UnityEngine;
using UnityEngine.UIElements;

public class PlayerNameplate : MonoBehaviour
{
    [Tooltip("Chiều cao offset so với gốc của player để vẽ tên trên đầu (chỉ dùng khi component nằm ở root)")]
    [SerializeField] private float heightOffset = 2.4f;

    private VisualElement container;
    private Label nameLabel;
    private IPlayerHUDTarget playerTarget;
    private VisualTreeAsset nameplateAsset;
    private bool isInitialized = false;

    private void Awake()
    {
        playerTarget = GetComponentInParent<IPlayerHUDTarget>();
    }

    private void Start()
    {
        if (playerTarget == null) playerTarget = GetComponentInParent<IPlayerHUDTarget>();
        if (playerTarget == null) playerTarget = GetComponent<IPlayerHUDTarget>();

        // Nếu là local player (standalone hoặc owner mạng đã được spawn), ẩn nameplate và tắt script
        if (playerTarget != null && (playerTarget.IsStandaloneMode || (playerTarget.IsSpawned && playerTarget.IsOwner)))
        {
            if (container != null)
            {
                container.style.display = DisplayStyle.None;
            }
            enabled = false;
            return;
        }

        TryInitialize();
    }

    private void TryInitialize()
    {
        if (isInitialized) return;

        // Tải UXML từ thư mục Resources
        nameplateAsset = Resources.Load<VisualTreeAsset>("PlayerNameplate");
        if (nameplateAsset == null)
        {
            Debug.LogError("[PlayerNameplate] Không tìm thấy PlayerNameplate.uxml trong thư mục Resources!");
            return;
        }

        // Tải USS từ thư mục Resources
        StyleSheet nameplateStyle = Resources.Load<StyleSheet>("PlayerNameplate");

        // Tìm UIDocument chính của HUD game
        var hudController = FindAnyObjectByType<PlayerHUDController>();
        if (hudController != null)
        {
            var hudDoc = hudController.GetComponent<UIDocument>();
            if (hudDoc != null && hudDoc.rootVisualElement != null)
            {
                // Instantiate nameplate từ asset
                container = nameplateAsset.CloneTree().Q<VisualElement>("nameplate-container");
                if (container != null)
                {
                    // Áp dụng trực tiếp StyleSheet vào container
                    if (nameplateStyle != null)
                    {
                        container.styleSheets.Add(nameplateStyle);
                    }
                    else
                    {
                        Debug.LogWarning("[PlayerNameplate] Không tìm thấy PlayerNameplate.uss trong thư mục Resources!");
                    }

                    nameLabel = container.Q<Label>("player-name-label");
                    
                    // Thêm vào root visual element của HUD chính
                    hudDoc.rootVisualElement.Add(container);
                    
                    // Mặc định ẩn trước khi cập nhật vị trí
                    container.style.display = DisplayStyle.None;
                    isInitialized = true;
                    Debug.Log($"[PlayerNameplate] Khởi tạo thành công bảng tên cho {playerTarget?.DisplayName}");
                }
            }
        }
    }

    private void LateUpdate()
    {
        if (playerTarget == null)
        {
            playerTarget = GetComponentInParent<IPlayerHUDTarget>();
            if (playerTarget == null) playerTarget = GetComponent<IPlayerHUDTarget>();
        }
        if (playerTarget == null) return;

        // Chờ đến khi player target được spawn hoàn tất trên mạng (để IsOwner được cập nhật chính xác),
        // trừ phi ở chế độ Standalone.
        if (!playerTarget.IsStandaloneMode && !playerTarget.IsSpawned)
        {
            return;
        }

        // Bổ sung kiểm tra an toàn: Nếu là chính mình (local player) thì ẩn và tắt script
        if (playerTarget.IsStandaloneMode || playerTarget.IsOwner)
        {
            if (container != null)
            {
                container.style.display = DisplayStyle.None;
            }
            enabled = false;
            return;
        }

        // Nếu chưa khởi tạo (do HUD chưa ready lúc Start), thử khởi tạo lại
        if (!isInitialized || container == null || nameLabel == null)
        {
            TryInitialize();
            return;
        }

        // Cập nhật tên hiển thị
        nameLabel.text = playerTarget.DisplayName;

        // Định vị trên màn hình dựa theo vị trí 3D trên đầu nhân vật
        if (Camera.main != null)
        {
            Vector3 worldPos = transform.position;
            // Nếu component này nằm trực tiếp trên root, dùng root + heightOffset
            if (playerTarget != null && transform == playerTarget.transform)
            {
                worldPos += Vector3.up * heightOffset;
            }

            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

            // Kiểm tra xem vị trí có nằm trước camera không
            if (screenPos.z > 0)
            {
                var panel = container.panel;
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

    private void OnDestroy()
    {
        // Dọn dẹp VisualElement khỏi HUD khi nhân vật bị hủy
        if (container != null)
        {
            container.RemoveFromHierarchy();
        }
    }

    private void OnDisable()
    {
        if (container != null)
        {
            container.style.display = DisplayStyle.None;
        }
    }
    
    private void OnEnable()
    {
        if (container != null)
        {
            container.style.display = DisplayStyle.Flex;
        }
    }
}
