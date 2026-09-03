using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Script quản lý HUD thanh máu Boss trên màn hình sử dụng UI Toolkit (UXML + USS).
/// Có các hiệu ứng giảm máu chậm (vệt vàng) và hiển thị chỉ số HP dạng chữ.
/// </summary>
public class BossHealthBar : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Kéo thả đối tượng Boss AI vào đây (nếu để trống, script sẽ tự tìm kiếm BossAI trong scene)")]
    public BossAI boss;
    
    [Tooltip("Kéo thả UIDocument chứa file BossHealthBar.uxml vào đây")]
    public UIDocument uiDocument;

    [Header("Settings")]
    [Tooltip("Danh hiệu/phụ đề hiển thị phía trên bên trái thanh máu Boss (ví dụ: TEMPLE GUARDIAN)")]
    public string bossTitle = "TEMPLE GUARDIAN";

    private VisualElement rootContainer;
    private VisualElement progressBar;
    private VisualElement yellowBar;
    private Label nameLabel;
    private Label titleLabel;
    private Label hpTextLabel;

    private float displayedHealth = -1f;
    private float yellowHealth = -1f;
    private float yellowDrainDelay = 0.5f; // Thời gian chờ trước khi thanh vàng tụt xuống
    private float yellowDrainTimer = 0f;

    private float shakeTimer = 0f;
    private float flashTimer = 0f;

    private void Awake()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null) uiDocument = gameObject.AddComponent<UIDocument>();
        }

        if (uiDocument != null && uiDocument.visualTreeAsset == null)
        {
            uiDocument.visualTreeAsset = Resources.Load<VisualTreeAsset>("BossHealthBar");
        }
    }

    private void OnEnable()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null) uiDocument = gameObject.AddComponent<UIDocument>();
        }

        if (uiDocument != null && uiDocument.visualTreeAsset == null)
        {
            uiDocument.visualTreeAsset = Resources.Load<VisualTreeAsset>("BossHealthBar");
        }

        QueryVisualElements();
        
        // Tự động tìm BossAI nếu chưa được gán
        if (boss == null)
        {
            boss = FindFirstObjectByType<BossAI>();
        }

        InitBossHealthAndName();
    }

    private void QueryVisualElements()
    {
        if (uiDocument != null)
        {
            if (uiDocument.visualTreeAsset == null)
            {
                uiDocument.visualTreeAsset = Resources.Load<VisualTreeAsset>("BossHealthBar");
            }
            if (uiDocument.rootVisualElement != null)
            {
                var root = uiDocument.rootVisualElement;
                rootContainer = root.Q<VisualElement>("boss-hud-container");
                progressBar = root.Q<VisualElement>("boss-hp-progress-bar");
                yellowBar = root.Q<VisualElement>("boss-hp-yellow-bar");
                nameLabel = root.Q<Label>("boss-name");
                titleLabel = root.Q<Label>("boss-title");
                hpTextLabel = root.Q<Label>("boss-hp-text");
            }
        }
    }

    private void InitBossHealthAndName()
    {
        if (boss == null)
        {
            // Ẩn thanh máu nếu không tìm thấy Boss
            if (rootContainer != null) rootContainer.style.display = DisplayStyle.None;
            return;
        }

        if (rootContainer != null)
        {
            rootContainer.style.display = boss.IsBossActive ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // Đặt tên hiển thị cho Boss
        string bossName = boss.gameObject.name;
        if (bossName.Contains("(Clone)"))
        {
            bossName = bossName.Replace("(Clone)", "").Trim();
        }

        if (nameLabel != null)
        {
            nameLabel.text = bossName;
        }

        if (titleLabel != null && !string.IsNullOrEmpty(bossTitle))
        {
            titleLabel.text = bossTitle;
        }

        float curHp = boss.ActualCurrentHealth;
        float maxHp = boss.maxHealth;

        displayedHealth = curHp;
        yellowHealth = curHp;

        UpdateHPBars(curHp, maxHp);
    }

    public void HideUI()
    {
        if (rootContainer != null)
        {
            rootContainer.style.display = DisplayStyle.None;
        }
    }

    private void Update()
    {
        if (boss == null || !boss.gameObject.activeInHierarchy || !boss.enabled)
        {
            HideUI();
            return;
        }

        // Kiểm tra nếu MiniBoss (Rakan) đang hoạt động
        var miniBoss = FindFirstObjectByType<MiniBossAI>();
        bool isMiniBossActive = miniBoss != null && miniBoss.gameObject.activeInHierarchy && miniBoss.IsBossActive && !miniBoss.IsDead;

        // Kiểm tra nếu Boss cuối đang hoạt động (không ở Sitting và chưa chết)
        var finalBoss = FindFirstObjectByType<FinalBossAI>();
        bool isFinalBossActive = finalBoss != null && 
            finalBoss.gameObject.activeInHierarchy && 
            finalBoss.CurrentStateValue != FinalBossAI.FinalBossState.Sitting && 
            !finalBoss.IsDead;

        // Ẩn thanh máu Silas nếu Silas đã chết/chưa kích hoạt, hoặc khi MiniBoss/FinalBoss đã vào trận
        if (boss.IsDead || !boss.IsBossActive || boss.ActualCurrentHealth <= 0 || isMiniBossActive || isFinalBossActive)
        {
            HideUI();
            return;
        }

        // Hiện thanh máu nếu Boss còn sống và đã kích hoạt
        if (rootContainer != null && rootContainer.style.display == DisplayStyle.None)
        {
            rootContainer.style.display = DisplayStyle.Flex;
        }

        // CHỐNG CHỒNG UI: Force ẩn triệt để mọi phần tử VisualElement và component của Trùm Phụ (MiniBoss)
        var allUIDocs = FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var doc in allUIDocs)
        {
            if (doc != null && doc.rootVisualElement != null)
            {
                var mbHud = doc.rootVisualElement.Q<VisualElement>("miniboss-hud-container");
                if (mbHud != null && mbHud.style.display != DisplayStyle.None)
                {
                    mbHud.style.display = DisplayStyle.None;
                }
            }
        }

        var allMiniBossHUDs = FindObjectsByType<MiniBossHealthBar>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var hud in allMiniBossHUDs)
        {
            if (hud != null)
            {
                hud.HideUI();
            }
        }

        // Cập nhật bộ đếm thời gian hiệu ứng và loại bỏ class sau khi chạy xong
        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.deltaTime;
            if (shakeTimer <= 0f && rootContainer != null)
            {
                rootContainer.RemoveFromClassList("shake");
            }
        }

        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0f && progressBar != null)
            {
                progressBar.RemoveFromClassList("flash");
            }
        }

        UpdateHealthAnimation();
    }

    private void UpdateHealthAnimation()
    {
        if (boss == null) return;

        // Query lại nếu VisualElement bị null
        if (progressBar == null || yellowBar == null || nameLabel == null || hpTextLabel == null)
        {
            QueryVisualElements();
            return;
        }

        float maxHp = boss.maxHealth;
        if (maxHp <= 0f) maxHp = 1000f;

        float actualHp = boss.ActualCurrentHealth;

        if (displayedHealth < 0f)
        {
            displayedHealth = actualHp;
            yellowHealth = actualHp;
        }

        // Kích hoạt hiệu ứng rung lắc (shake) và chớp đỏ (flash) khi Boss bị mất máu
        if (actualHp < displayedHealth && displayedHealth > 0f)
        {
            if (rootContainer != null)
            {
                rootContainer.RemoveFromClassList("shake");
                rootContainer.AddToClassList("shake");
                shakeTimer = 0.22f;
            }

            if (progressBar != null)
            {
                progressBar.RemoveFromClassList("flash");
                progressBar.AddToClassList("flash");
                flashTimer = 0.2f;
            }
        }

        displayedHealth = actualHp;

        // Cập nhật thanh máu chính (màu đỏ)
        float percent = Mathf.Clamp01(displayedHealth / maxHp) * 100f;
        progressBar.style.width = Length.Percent(percent);

        // Hiển thị số máu dạng chữ
        hpTextLabel.text = $"{(int)Mathf.Max(0, displayedHealth)} / {(int)maxHp}";

        // Xử lý hiệu ứng rút máu thanh vàng (lag bar)
        if (actualHp < yellowHealth)
        {
            if (yellowDrainTimer <= 0f || actualHp < displayedHealth)
            {
                yellowDrainTimer = yellowDrainDelay;
            }
        }
        else if (actualHp > yellowHealth)
        {
            yellowHealth = actualHp;
        }

        if (yellowDrainTimer > 0f)
        {
            yellowDrainTimer -= Time.deltaTime;
        }
        else
        {
            // Tốc độ tụt của thanh vàng tỉ lệ với lượng máu tối đa
            float drainSpeed = maxHp * 0.35f;
            yellowHealth = Mathf.MoveTowards(yellowHealth, actualHp, drainSpeed * Time.deltaTime);
        }

        float yellowPercent = Mathf.Clamp01(yellowHealth / maxHp) * 100f;
        yellowBar.style.width = Length.Percent(yellowPercent);
    }

    private void UpdateHPBars(float curHp, float maxHp)
    {
        if (maxHp <= 0f) maxHp = 1000f;
        float percent = Mathf.Clamp01(curHp / maxHp) * 100f;

        if (progressBar != null) progressBar.style.width = Length.Percent(percent);
        if (yellowBar != null) yellowBar.style.width = Length.Percent(percent);
        if (hpTextLabel != null) hpTextLabel.text = $"{(int)Mathf.Max(0, curHp)} / {(int)maxHp}";
    }
}
