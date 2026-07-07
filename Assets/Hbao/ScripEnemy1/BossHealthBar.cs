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

    private VisualElement rootContainer;
    private VisualElement progressBar;
    private VisualElement yellowBar;
    private Label nameLabel;
    private Label hpTextLabel;

    private float displayedHealth = -1f;
    private float yellowHealth = -1f;
    private float yellowDrainDelay = 0.5f; // Thời gian chờ trước khi thanh vàng tụt xuống
    private float yellowDrainTimer = 0f;

    private void OnEnable()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
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
        if (uiDocument != null && uiDocument.rootVisualElement != null)
        {
            var root = uiDocument.rootVisualElement;
            rootContainer = root.Q<VisualElement>("boss-hud-container");
            progressBar = root.Q<VisualElement>("boss-hp-progress-bar");
            yellowBar = root.Q<VisualElement>("boss-hp-yellow-bar");
            nameLabel = root.Q<Label>("boss-name");
            hpTextLabel = root.Q<Label>("boss-hp-text");
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

        if (rootContainer != null) rootContainer.style.display = DisplayStyle.Flex;

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

        float curHp = boss.ActualCurrentHealth;
        float maxHp = boss.maxHealth;

        displayedHealth = curHp;
        yellowHealth = curHp;

        UpdateHPBars(curHp, maxHp);
    }

    private void Update()
    {
        if (boss == null)
        {
            // Thử tìm lại Boss nếu chưa gán
            boss = FindFirstObjectByType<BossAI>();
            if (boss == null)
            {
                if (rootContainer != null) rootContainer.style.display = DisplayStyle.None;
                return;
            }
            InitBossHealthAndName();
        }

        // Ẩn thanh máu khi Boss đã chết
        if (boss.IsDead)
        {
            if (rootContainer != null) rootContainer.style.display = DisplayStyle.None;
            return;
        }

        // Hiện thanh máu nếu Boss còn sống
        if (rootContainer != null && rootContainer.style.display == DisplayStyle.None)
        {
            rootContainer.style.display = DisplayStyle.Flex;
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
