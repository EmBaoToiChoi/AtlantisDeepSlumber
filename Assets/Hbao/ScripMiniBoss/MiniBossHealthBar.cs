using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Script managing the Mini Boss HUD health bar using UI Toolkit (UXML + USS).
/// Includes main boss health bar, 2 clone sub-health bars with yellow lag drain,
/// hit shake, and hit flash effects.
/// UI only disappears when ALL 3 (Main Boss + 2 Clones) are dead.
/// </summary>
public class MiniBossHealthBar : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Reference to the main MiniBossAI component (auto-discovered if empty)")]
    public MiniBossAI boss;
    
    [Tooltip("Reference to the UIDocument containing the layout")]
    public UIDocument uiDocument;

    private VisualElement rootContainer;
    private VisualElement progressBar;
    private VisualElement yellowBar;
    private Label nameLabel;
    private Label hpTextLabel;

    // 2 Clone Sub-Bar Visual Elements
    private VisualElement clonesSubContainer;
    
    private VisualElement clone1Wrapper;
    private VisualElement clone1ProgressBar;
    private VisualElement clone1YellowBar;
    private Label clone1HpTextLabel;

    private VisualElement clone2Wrapper;
    private VisualElement clone2ProgressBar;
    private VisualElement clone2YellowBar;
    private Label clone2HpTextLabel;

    // Lag bar tracking for main boss
    private float displayedHealth = -1f;
    private float yellowHealth = -1f;
    private float yellowDrainDelay = 0.5f;
    private float yellowDrainTimer = 0f;

    // Lag bar tracking for clone 1 & clone 2
    private float clone1DisplayedHp = -1f;
    private float clone1YellowHp = -1f;
    private float clone1YellowDrainTimer = 0f;

    private float clone2DisplayedHp = -1f;
    private float clone2YellowHp = -1f;
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
            boss = FindFirstObjectByType<MiniBossAI>();
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

            clonesSubContainer = root.Q<VisualElement>("clones-sub-container");

            clone1Wrapper = root.Q<VisualElement>("clone1-wrapper");
            clone1ProgressBar = root.Q<VisualElement>("clone1-hp-progress-bar");
            clone1YellowBar = root.Q<VisualElement>("clone1-hp-yellow-bar");
            clone1HpTextLabel = root.Q<Label>("clone1-hp-text");

            clone2Wrapper = root.Q<VisualElement>("clone2-wrapper");
            clone2ProgressBar = root.Q<VisualElement>("clone2-hp-progress-bar");
            clone2YellowBar = root.Q<VisualElement>("clone2-hp-yellow-bar");
            clone2HpTextLabel = root.Q<Label>("clone2-hp-text");
        }
    }

    private void InitBossHealthAndName()
    {
        if (boss == null)
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

        // 2. Discover all Mini Boss instances (Main Boss + Clones)
        var allBosses = FindObjectsByType<MiniBossAI>(FindObjectsSortMode.None);
        MiniBossAI mainBoss = null;
        List<MiniBossAI> activeClones = new List<MiniBossAI>();

        foreach (var b in allBosses)
        {
            if (b == null || !b.gameObject.activeInHierarchy || !b.enabled) continue;
            if (b.IsDead) continue;

            if (b.isClone)
            {
                activeClones.Add(b);
            }
            else if (mainBoss == null)
            {
                mainBoss = b;
            }
        }

        if (mainBoss != null) boss = mainBoss;

        // 3. UI only disappears when ALL 3 (Main Boss + Clones) are dead / inactive
        bool anyAlive = (mainBoss != null && !mainBoss.IsDead && mainBoss.ActualCurrentHealth > 0) || (activeClones.Count > 0);

        if (!anyAlive)
        {
            HideUI();
            return;
        }

        // Show HUD container if any target is alive
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

        UpdateMainHealthAnimation(mainBoss);
        UpdateClonesHealthAnimation(activeClones);
    }

    private void UpdateMainHealthAnimation(MiniBossAI targetBoss)
    {
        if (progressBar == null || yellowBar == null || nameLabel == null || hpTextLabel == null)
        {
            QueryVisualElements();
            return;
        }

        if (targetBoss == null)
        {
            progressBar.style.width = Length.Percent(0);
            yellowBar.style.width = Length.Percent(0);
            hpTextLabel.text = "0 / 0 (Đã hạ)";
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

    private void UpdateClonesHealthAnimation(List<MiniBossAI> activeClones)
    {
        if (clonesSubContainer == null)
        {
            QueryVisualElements();
            if (clonesSubContainer == null) return;
        }

        if (activeClones.Count == 0)
        {
            clonesSubContainer.style.display = DisplayStyle.None;
            return;
        }

        clonesSubContainer.style.display = DisplayStyle.Flex;

        // --- Clone 1 ---
        if (activeClones.Count >= 1 && activeClones[0] != null && !activeClones[0].IsDead)
        {
            if (clone1Wrapper != null) clone1Wrapper.style.display = DisplayStyle.Flex;
            UpdateSubBar(activeClones[0], ref clone1DisplayedHp, ref clone1YellowHp, ref clone1YellowDrainTimer, clone1ProgressBar, clone1YellowBar, clone1HpTextLabel);
        }
        else
        {
            if (clone1Wrapper != null) clone1Wrapper.style.display = DisplayStyle.None;
        }

        // --- Clone 2 ---
        if (activeClones.Count >= 2 && activeClones[1] != null && !activeClones[1].IsDead)
        {
            if (clone2Wrapper != null) clone2Wrapper.style.display = DisplayStyle.Flex;
            UpdateSubBar(activeClones[1], ref clone2DisplayedHp, ref clone2YellowHp, ref clone2YellowDrainTimer, clone2ProgressBar, clone2YellowBar, clone2HpTextLabel);
        }
        else
        {
            if (clone2Wrapper != null) clone2Wrapper.style.display = DisplayStyle.None;
        }
    }

    private void UpdateSubBar(MiniBossAI clone, ref float dispHp, ref float yellowHp, ref float drainTimer, VisualElement progBar, VisualElement yelBar, Label txtLabel)
    {
        if (clone == null || progBar == null || yelBar == null || txtLabel == null) return;

        float maxHp = clone.maxHealth;
        if (maxHp <= 0f) maxHp = 225f;
        float actualHp = clone.ActualCurrentHealth;

        if (dispHp < 0f)
        {
            dispHp = actualHp;
            yellowHp = actualHp;
        }

        dispHp = actualHp;

        float percent = Mathf.Clamp01(dispHp / maxHp) * 100f;
        progBar.style.width = Length.Percent(percent);
        txtLabel.text = $"{(int)Mathf.Max(0, dispHp)} / {(int)maxHp}";

        if (actualHp < yellowHp)
        {
            drainTimer += Time.deltaTime;
            if (drainTimer >= yellowDrainDelay)
            {
                yellowHp = Mathf.MoveTowards(yellowHp, actualHp, maxHp * 0.45f * Time.deltaTime);
            }
        }
        else
        {
            yellowHp = actualHp;
            drainTimer = 0f;
        }

        float yellowPercent = Mathf.Clamp01(yellowHp / maxHp) * 100f;
        yelBar.style.width = Length.Percent(yellowPercent);
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
