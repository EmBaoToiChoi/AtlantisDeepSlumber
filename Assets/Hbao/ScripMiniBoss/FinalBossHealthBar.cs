using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Script managing the Final Boss HUD health bar using UI Toolkit (UXML + USS).
/// Matches FinalBossAI, including yellow draining lag bar, hit shake, and hit flash effects.
/// </summary>
public class FinalBossHealthBar : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Reference to the FinalBossAI component (auto-discovered if empty)")]
    public FinalBossAI boss;
    
    [Tooltip("Reference to the UIDocument containing the layout")]
    public UIDocument uiDocument;

    private VisualElement rootContainer;
    private VisualElement progressBar;
    private VisualElement yellowBar;
    private Label nameLabel;
    private Label hpTextLabel;

    private float displayedHealth = -1f;
    private float yellowHealth = -1f;
    private float yellowDrainDelay = 0.5f;
    private float yellowDrainTimer = 0f;

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
            boss = FindFirstObjectByType<FinalBossAI>();
        }

        InitBossHealthAndName();
    }

    private void QueryVisualElements()
    {
        if (uiDocument != null && uiDocument.rootVisualElement != null)
        {
            var root = uiDocument.rootVisualElement;
            rootContainer = root.Q<VisualElement>("finalboss-hud-container");
            progressBar = root.Q<VisualElement>("finalboss-hp-progress-bar");
            yellowBar = root.Q<VisualElement>("finalboss-hp-yellow-bar");
            nameLabel = root.Q<Label>("finalboss-name");
            hpTextLabel = root.Q<Label>("finalboss-hp-text");
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
            rootContainer.style.display = boss.IsHUDVisible ? DisplayStyle.Flex : DisplayStyle.None;
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

    private void Update()
    {
        if (boss == null)
        {
            boss = FindFirstObjectByType<FinalBossAI>();
            if (boss == null)
            {
                if (rootContainer != null) rootContainer.style.display = DisplayStyle.None;
                return;
            }
            InitBossHealthAndName();
        }

        // Hide UI if boss is dead or HUD is not visible
        if (boss.IsDead || !boss.IsHUDVisible)
        {
            if (rootContainer != null && rootContainer.style.display != DisplayStyle.None)
            {
                rootContainer.style.display = DisplayStyle.None;
            }
            return;
        }

        // Show UI if active and alive
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

        UpdateHealthAnimation();
    }

    private void UpdateHealthAnimation()
    {
        if (boss == null) return;

        if (progressBar == null || yellowBar == null || nameLabel == null || hpTextLabel == null)
        {
            QueryVisualElements();
            return;
        }

        float maxHp = boss.maxHealth;
        if (maxHp <= 0f) maxHp = 1200f;

        float actualHp = boss.ActualCurrentHealth;

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
                shakeTimer = 0.25f;
            }

            if (progressBar != null)
            {
                progressBar.RemoveFromClassList("flash");
                progressBar.AddToClassList("flash");
                flashTimer = 0.18f;
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
                // Smoothly drain the yellow bar
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
