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
                // Dọn dẹp container cũ khỏi HUD (nếu có) trước khi tạo mới để tránh rò rỉ bộ nhớ
                if (container != null)
                {
                    container.RemoveFromHierarchy();
                }

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

        // Nếu chưa khởi tạo hoặc bị mất kết nối với Panel HUD (khi chuyển/tải lại HUD), thử khởi tạo lại
        if (!isInitialized || container == null || container.panel == null || nameLabel == null)
        {
            isInitialized = false;
            TryInitialize();
            return;
        }

        // TÍNH NĂNG: Tự động ẩn bảng tên khi đang trong trận chiến với MiniBoss, BossAI hoặc FinalBoss
        if (IsInBossCombat())
        {
            if (container != null && container.style.display != DisplayStyle.None)
            {
                container.style.display = DisplayStyle.None;
            }
            return;
        }

        // Cập nhật tên hiển thị
        nameLabel.text = playerTarget.DisplayName;

        // Định vị trên màn hình dựa theo vị trí 3D trên đầu nhân vật
        if (Camera.main != null)
        {
            Vector3 worldPos = transform.position;
            // Nếu component này nằm trực tiếp trên root, dùng root + heightOffset.
            // Hoặc nếu nằm ở child transform nhưng có vị trí Y cục bộ quá thấp (< 1.5m), ta bù thêm chiều cao.
            if (playerTarget != null && (transform == playerTarget.transform || transform.localPosition.y < 1.5f))
            {
                float offset = heightOffset;
                if (transform != playerTarget.transform)
                {
                    offset = Mathf.Max(0f, heightOffset - transform.localPosition.y);
                }
                worldPos += Vector3.up * offset;
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

    // ─── TỰ ĐỘNG KIỂM TRA TRẠNG THÁI GIAO TRANH VỚI BOSS ───
    private static MiniBossAI cachedMiniBoss;
    private static BossAI cachedBossAI;
    private static FinalBossAI cachedFinalBoss;
    private static float nextBossCheckTime = 0f;

    /// <summary>
    /// Kiểm tra xem người chơi có đang trong trận đánh với MiniBoss, BossAI (Silas), hoặc FinalBoss (King Atlantis) hay không.
    /// Tự động đồng bộ hoàn toàn qua NetworkVariables trên mọi máy Client & Host.
    /// </summary>
    public static bool IsInBossCombat()
    {
        float now = Time.time;
        if (now >= nextBossCheckTime)
        {
            nextBossCheckTime = now + 0.35f; // Tần suất quét 0.35s/lần để tiết kiệm hiệu năng

            if (cachedMiniBoss == null || !cachedMiniBoss.gameObject.activeInHierarchy || cachedMiniBoss.isClone)
            {
                var allMiniBosses = Object.FindObjectsByType<MiniBossAI>(FindObjectsSortMode.None);
                foreach (var b in allMiniBosses)
                {
                    if (b != null && b.gameObject.activeInHierarchy && !b.isClone)
                    {
                        cachedMiniBoss = b;
                        break;
                    }
                }
            }

            if (cachedBossAI == null || !cachedBossAI.gameObject.activeInHierarchy)
            {
                cachedBossAI = Object.FindFirstObjectByType<BossAI>();
            }

            if (cachedFinalBoss == null || !cachedFinalBoss.gameObject.activeInHierarchy)
            {
                cachedFinalBoss = Object.FindFirstObjectByType<FinalBossAI>();
            }
        }

        // 1. Kiểm tra trận đấu với MiniBoss (Rakan - Trùm Phụ và các phân thân) - Đồng bộ mạng qua NetworkVariable
        if (cachedMiniBoss != null && cachedMiniBoss.gameObject.activeInHierarchy)
        {
            if (cachedMiniBoss.IsBossActive && !cachedMiniBoss.AreAllBossesAndClonesDead())
            {
                return true;
            }
        }

        // 2. Kiểm tra trận đấu với BossAI (Silas) - Đồng bộ mạng qua isBossActive NetworkVariable
        if (cachedBossAI != null && cachedBossAI.gameObject.activeInHierarchy)
        {
            if (cachedBossAI.IsBossActive && !cachedBossAI.IsDead && cachedBossAI.ActualCurrentHealth > 0)
            {
                return true;
            }
        }

        // 3. Kiểm tra trận đấu với FinalBoss (King Atlantis) - Đồng bộ mạng qua isBossActive & currentState NetworkVariables
        if (cachedFinalBoss != null && cachedFinalBoss.gameObject.activeInHierarchy)
        {
            if (cachedFinalBoss.IsBossActive && !cachedFinalBoss.IsDead && 
                cachedFinalBoss.CurrentStateValue != FinalBossAI.FinalBossState.Sitting && 
                cachedFinalBoss.ActualCurrentHealth > 0)
            {
                return true;
            }
        }

        return false;
    }
}
