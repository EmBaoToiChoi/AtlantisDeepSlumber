using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Script managing the Mini Boss HUD health bar using UI Toolkit (UXML + USS).
/// Displays main boss health bar with yellow lag drain, hit shake, and hit flash.
/// Also displays 2 clone health bars when MiniBoss summons its shadow clones at 50% HP.
/// UI appears when player activates the Mini Boss trigger box, and hides only when
/// BOTH the Main Boss AND all clones are dead.
/// </summary>
public class MiniBossHealthBar : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Reference to the main MiniBossAI component (auto-discovered if empty)")]
    public MiniBossAI boss;
    
    [Tooltip("Reference to the UIDocument containing the layout")]
    public UIDocument uiDocument;

    // Main boss UI elements
    private VisualElement rootContainer;
    private VisualElement progressBar;
    private VisualElement yellowBar;
    private Label nameLabel;
    private Label hpTextLabel;

    // Clone sub-container & UI elements
    private VisualElement clonesSubContainer;

    private VisualElement clone1ProgressBar;
    private VisualElement clone1YellowBar;
    private Label clone1NameLabel;
    private Label clone1HpTextLabel;

    private VisualElement clone2ProgressBar;
    private VisualElement clone2YellowBar;
    private Label clone2NameLabel;
    private Label clone2HpTextLabel;

    // Clone AI references (auto-discovered)
    private MiniBossAI clone1AI;
    private MiniBossAI clone2AI;
    private bool clonesDiscovered = false;
    private float cloneScanTimer = 0f;

    // Lag bar tracking for main boss
    private float displayedHealth = -1f;
    private float yellowHealth = -1f;
    private float yellowDrainDelay = 0.5f;
    private float yellowDrainTimer = 0f;

    // Lag bar tracking for clone 1
    private float clone1DisplayedHealth = -1f;
    private float clone1YellowHealth = -1f;
    private float clone1YellowDrainTimer = 0f;

    // Lag bar tracking for clone 2
    private float clone2DisplayedHealth = -1f;
    private float clone2YellowHealth = -1f;
    private float clone2YellowDrainTimer = 0f;

    private float shakeTimer = 0f;
    private float flashTimer = 0f;

    private void OnEnable()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }

        QueryVisualElements();
        
        if (boss == null)
        {
            FindMainBoss();
        }

        InitBossHealthAndName();
    }

    private void QueryVisualElements()
    {
        if (uiDocument != null && uiDocument.rootVisualElement != null)
        {
            var root = uiDocument.rootVisualElement;
            rootContainer = root.Q<VisualElement>("miniboss-hud-container");
            progressBar = root.Q<VisualElement>("miniboss-hp-progress-bar");
            yellowBar = root.Q<VisualElement>("miniboss-hp-yellow-bar");
            nameLabel = root.Q<Label>("miniboss-name");
            hpTextLabel = root.Q<Label>("miniboss-hp-text");

            // Clone sub-container
            clonesSubContainer = root.Q<VisualElement>("clones-sub-container");

            // Clone 1
            clone1ProgressBar = root.Q<VisualElement>("clone1-hp-progress-bar");
            clone1YellowBar = root.Q<VisualElement>("clone1-hp-yellow-bar");
            clone1NameLabel = root.Q<Label>("clone1-name");
            clone1HpTextLabel = root.Q<Label>("clone1-hp-text");

            // Clone 2
            clone2ProgressBar = root.Q<VisualElement>("clone2-hp-progress-bar");
            clone2YellowBar = root.Q<VisualElement>("clone2-hp-yellow-bar");
            clone2NameLabel = root.Q<Label>("clone2-name");
            clone2HpTextLabel = root.Q<Label>("clone2-hp-text");
        }
    }

    private void FindMainBoss()
    {
        var allBosses = FindObjectsByType<MiniBossAI>(FindObjectsSortMode.None);
        foreach (var b in allBosses)
        {
            if (b != null && b.gameObject.activeInHierarchy && b.enabled && !b.isClone)
            {
                boss = b;
                break;
            }
        }
    }

    private void DiscoverClones()
    {
        clone1AI = null;
        clone2AI = null;
        var allBosses = FindObjectsByType<MiniBossAI>(FindObjectsSortMode.None);
        foreach (var b in allBosses)
        {
            if (b != null && b.isClone && b.gameObject.activeInHierarchy)
            {
                if (clone1AI == null)
                {
                    clone1AI = b;
                }
                else if (clone2AI == null)
                {
                    clone2AI = b;
                    break;
                }
            }
        }

        if (clone1AI != null && clone2AI != null)
        {
            clonesDiscovered = true;
        }
    }

    private void InitBossHealthAndName()
    {
        // Ẩn HUD mặc định cho tới khi Player đi vào Trigger Box kích hoạt Boss (IsBossActive == true)
        if (boss == null || !boss.IsBossActive || boss.IsDead)
        {
            if (rootContainer != null) rootContainer.style.display = DisplayStyle.None;
            return;
        }

        if (rootContainer != null)
        {
            rootContainer.style.display = DisplayStyle.Flex;
        }

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

        // Ẩn thanh máu phân thân mặc định
        if (clonesSubContainer != null)
        {
            clonesSubContainer.style.display = DisplayStyle.None;
        }
    }

    public void HideUI()
    {
        if (rootContainer != null)
        {
            rootContainer.style.display = DisplayStyle.None;
        }
    }

    /// <summary>
    /// Kiểm tra cả MiniBoss chính VÀ tất cả Phân thân đã chết hết chưa.
    /// Chỉ khi TẤT CẢ đều chết/null mới trả về true → ẩn UI.
    /// </summary>
    private bool AreAllBossesAndClonesDead()
    {
        // Boss chính còn sống → chưa ẩn UI
        if (boss != null && boss.gameObject.activeInHierarchy && !boss.IsDead && boss.ActualCurrentHealth > 0)
        {
            return false;
        }

        // Nếu chưa triệu hồi phân thân → chỉ cần boss chính chết là ẩn UI
        if (!clonesDiscovered && clone1AI == null && clone2AI == null)
        {
            return true;
        }

        // Clone 1 còn sống → chưa ẩn UI
        if (clone1AI != null && clone1AI.gameObject.activeInHierarchy && !clone1AI.IsDead && clone1AI.ActualCurrentHealth > 0)
        {
            return false;
        }

        // Clone 2 còn sống → chưa ẩn UI
        if (clone2AI != null && clone2AI.gameObject.activeInHierarchy && !clone2AI.IsDead && clone2AI.ActualCurrentHealth > 0)
        {
            return false;
        }

        return true;
    }

    private void Update()
    {
        // 1. Check if Final Boss HUD is active to prevent UI overlap
        var finalBoss = FindFirstObjectByType<FinalBossAI>();
        bool isFinalBossActive = finalBoss != null && 
            finalBoss.gameObject.activeInHierarchy && 
            finalBoss.CurrentStateValue != FinalBossAI.FinalBossState.Sitting && 
            !finalBoss.IsDead;

        if (isFinalBossActive)
        {
            HideUI();
            return;
        }

        // 2. Discover main boss
        if (boss == null || !boss.gameObject.activeInHierarchy || boss.isClone)
        {
            FindMainBoss();
        }

        // 3. CHỈ HIỂN THỊ HUD khi Main Boss tồn tại, active qua trigger box (IsBossActive == true)
        if (boss == null || !boss.IsBossActive)
        {
            HideUI();
            return;
        }

        // 4. CHỈ ẨN UI KHI CẢ BOSS LẪN 2 PHÂN THÂN ĐỀU ĐÃ CHẾT
        if (AreAllBossesAndClonesDead())
        {
            HideUI();
            return;
        }

        // Show HUD container if boss trigger box was activated and boss/clones are still alive
        if (rootContainer != null && rootContainer.style.display == DisplayStyle.None)
        {
            rootContainer.style.display = DisplayStyle.Flex;
        }

        // Update shake timers
        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.deltaTime;
            if (shakeTimer <= 0f && rootContainer != null)
            {
                rootContainer.RemoveFromClassList("shake");
            }
        }

        // Update flash timers
        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0f && progressBar != null)
            {
                progressBar.RemoveFromClassList("flash");
            }
        }

        // Main Boss Health
        UpdateMainHealthAnimation(boss);

        // Clone Discovery (quét mỗi 0.5 giây sau khi boss triệu hồi xong)
        if (!clonesDiscovered && boss != null && boss.IsBossActive)
        {
            cloneScanTimer -= Time.deltaTime;
            if (cloneScanTimer <= 0f)
            {
                cloneScanTimer = 0.5f;
                DiscoverClones();
            }
        }

        // Clone Health Bars
        UpdateCloneHealthBars();
    }

    private void UpdateCloneHealthBars()
    {
        if (clonesSubContainer == null) return;

        // Chỉ hiển thị thanh máu phân thân khi đã triệu hồi và tìm thấy ít nhất 1 phân thân
        bool hasAnyClone = (clone1AI != null && clone1AI.gameObject.activeInHierarchy) ||
                           (clone2AI != null && clone2AI.gameObject.activeInHierarchy);

        if (!hasAnyClone)
        {
            if (clonesSubContainer.style.display != DisplayStyle.None)
                clonesSubContainer.style.display = DisplayStyle.None;
            return;
        }

        if (clonesSubContainer.style.display != DisplayStyle.Flex)
            clonesSubContainer.style.display = DisplayStyle.Flex;

        // Update Clone 1
        UpdateSingleCloneBar(clone1AI, clone1ProgressBar, clone1YellowBar, clone1NameLabel, clone1HpTextLabel,
            ref clone1DisplayedHealth, ref clone1YellowHealth, ref clone1YellowDrainTimer, "Phân Thân 1");

        // Update Clone 2
        UpdateSingleCloneBar(clone2AI, clone2ProgressBar, clone2YellowBar, clone2NameLabel, clone2HpTextLabel,
            ref clone2DisplayedHealth, ref clone2YellowHealth, ref clone2YellowDrainTimer, "Phân Thân 2");
    }

    private void UpdateSingleCloneBar(MiniBossAI cloneAI, VisualElement cloneProgress, VisualElement cloneYellow,
        Label cloneName, Label cloneHpText,
        ref float cloneDisplayed, ref float cloneYellowHp, ref float cloneYellowTimer,
        string defaultName)
    {
        if (cloneProgress == null || cloneYellow == null) return;

        // Phân thân đã chết hoặc bị hủy
        if (cloneAI == null || !cloneAI.gameObject.activeInHierarchy || cloneAI.IsDead || cloneAI.ActualCurrentHealth <= 0)
        {
            cloneProgress.style.width = Length.Percent(0);
            cloneYellow.style.width = Length.Percent(0);
            if (cloneHpText != null) cloneHpText.text = "0 (Đã hạ)";
            if (cloneName != null) cloneName.text = defaultName + " ☠";
            return;
        }

        float maxHp = cloneAI.maxHealth;
        if (maxHp <= 0f) maxHp = 225f;

        float actualHp = cloneAI.ActualCurrentHealth;

        if (cloneDisplayed < 0f)
        {
            cloneDisplayed = actualHp;
            cloneYellowHp = actualHp;
        }

        if (cloneName != null) cloneName.text = defaultName;

        cloneDisplayed = actualHp;

        float percent = Mathf.Clamp01(cloneDisplayed / maxHp) * 100f;
        cloneProgress.style.width = Length.Percent(percent);

        if (cloneHpText != null) cloneHpText.text = $"{(int)Mathf.Max(0, cloneDisplayed)} / {(int)maxHp}";

        // Yellow lag bar
        if (actualHp < cloneYellowHp)
        {
            cloneYellowTimer += Time.deltaTime;
            if (cloneYellowTimer >= yellowDrainDelay)
            {
                cloneYellowHp = Mathf.MoveTowards(cloneYellowHp, actualHp, maxHp * 0.35f * Time.deltaTime);
            }
        }
        else
        {
            cloneYellowHp = actualHp;
            cloneYellowTimer = 0f;
        }

        float yellowPercent = Mathf.Clamp01(cloneYellowHp / maxHp) * 100f;
        cloneYellow.style.width = Length.Percent(yellowPercent);
    }

    private void UpdateMainHealthAnimation(MiniBossAI targetBoss)
    {
        if (progressBar == null || yellowBar == null || nameLabel == null || hpTextLabel == null)
        {
            QueryVisualElements();
            return;
        }

        if (targetBoss == null || targetBoss.IsDead || targetBoss.ActualCurrentHealth <= 0)
        {
            progressBar.style.width = Length.Percent(0);
            yellowBar.style.width = Length.Percent(0);
            hpTextLabel.text = "0 (Đã hạ)";
            return;
        }

        float maxHp = targetBoss.maxHealth;
        if (maxHp <= 0f) maxHp = 500f;

        float actualHp = targetBoss.ActualCurrentHealth;

        if (displayedHealth < 0f)
        {
            displayedHealth = actualHp;
            yellowHealth = actualHp;
        }

        // Trigger shake & flash on health reduction
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

        // Update main progress bar
        float percent = Mathf.Clamp01(displayedHealth / maxHp) * 100f;
        progressBar.style.width = Length.Percent(percent);

        // Update health text
        hpTextLabel.text = $"{(int)Mathf.Max(0, displayedHealth)} / {(int)maxHp}";

        // Update yellow lag bar
        if (actualHp < yellowHealth)
        {
            yellowDrainTimer += Time.deltaTime;
            if (yellowDrainTimer >= yellowDrainDelay)
            {
                yellowHealth = Mathf.MoveTowards(yellowHealth, actualHp, maxHp * 0.35f * Time.deltaTime);
            }
        }
        else
        {
            yellowHealth = actualHp;
            yellowDrainTimer = 0f;
        }

        float yellowPercent = Mathf.Clamp01(yellowHealth / maxHp) * 100f;
        yellowBar.style.width = Length.Percent(yellowPercent);
    }

    private void UpdateHPBars(float current, float max)
    {
        if (max <= 0) return;
        float percent = Mathf.Clamp01(current / max) * 100f;
        if (progressBar != null) progressBar.style.width = Length.Percent(percent);
        if (yellowBar != null) yellowBar.style.width = Length.Percent(percent);
        if (hpTextLabel != null) hpTextLabel.text = $"{(int)current} / {(int)max}";
    }
}
