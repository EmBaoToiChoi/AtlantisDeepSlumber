using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Custom Player Controller for Leo Assassin.
/// Inherits from SimplePlayerTest to maintain complete compatibility with enemy AI, HUD, and other gameplay systems,
/// while providing translation and overriding logic for Leo's specific animations.
/// </summary>
public class LeoPlayer : SimplePlayerTest
{
    [Header("Leo Custom Movement Triggers (Unarmed)")]
    public string idleUnarmed = "Idle";
    public string walkUnarmed = "Walk";
    public string runUnarmed = "run";
    public string walkBackwardUnarmed = "WalkBackward"; // Maps to Player 1 dilui

    [Header("Leo Custom Movement Triggers (Armed)")]
    public string idleArmed = "IdleArmed"; // Maps to IDLE camvukhi
    public string walkForwardArmed = "WalkForwardArmed"; // Maps to Ditoicovukhi
    public string walkBackwardArmed = "WalkBackwardArmed"; // Maps to Diluicovukhi
    public string runForwardArmed = "RunForwardArmed"; // Maps to Runtruoccovukhi
    public string runBackwardArmed = "RunBackwardArmed"; // Maps to Runluicovukhi

    [Header("Leo Custom Dodge & Action Triggers")]
    public string rollTrigger = "Lonmeo"; // Maps to Lonmeo
    public string pickTrigger = "PickingUp"; // Maps to Picking Up

    [Header("Leo Custom Attack Triggers")]
    public string punch1Trigger = "Punch1"; // Maps to Damtrai
    public string punch2Trigger = "Punch2"; // Maps to Damphai
    public string slash1Trigger = "Slash1"; // Maps to One Combo
    public string slash2Trigger = "Slash2"; // Maps to Dual Combo
    public string slash3Trigger = "Slash1"; // Fallback for 3rd step, defaults back to One Combo

    [Header("Leo Custom Death Triggers")]
    public string deathUnarmedTrigger = "Death"; // Maps to DIE khonvukhi
    public string deathArmedTrigger = "DeathArmed"; // Maps to Diecovukhi

    [Header("Leo Custom Hit Reaction Triggers")]
    public string getHitTrigger = "GetHit";
    public string getHit2Trigger = "GeiHit2";

    private void Awake()
    {
        // Initial setup for Leo Assassin stats (matching LeoAssassin)
        characterClassIndex = 0; 
        maxHealth = 85f;
        moveSpeed = 3.5f;
        runSpeedMultiplier = 3.5f;
        damageAmount = 25f;
        attackRange = 2f;
        cameraOffset = new Vector3(0f, 10f, -6.5f); 
        cameraSensitivity = 3f;  
        cameraPivotHeight = 3.5f; 

        if (anim == null)
        {
            anim = GetComponent<Animator>();
            if (anim == null)
                anim = GetComponentInChildren<Animator>(true);
        }
    }

    /// <summary>
    /// Translates base class generic animation names to Leo's custom animation triggers.
    /// </summary>
    private string TranslateAnimName(string animName)
    {
        int weapon = GetActiveWeaponIndex();
        bool isArmed = (weapon == 2);

        // Determine if moving backward
        float verticalInput = Input.GetAxis("Vertical");
        bool isMovingBackward = (verticalInput < -0.1f);

        switch (animName)
        {
            case "Idle":
                return isArmed ? idleArmed : idleUnarmed;
            case "Walk":
                if (isArmed)
                {
                    return isMovingBackward ? walkBackwardArmed : walkForwardArmed;
                }
                else
                {
                    return isMovingBackward ? walkBackwardUnarmed : walkUnarmed;
                }
            case "run":
                if (isArmed)
                {
                    return isMovingBackward ? runBackwardArmed : runForwardArmed;
                }
                else
                {
                    return isMovingBackward ? walkBackwardUnarmed : runUnarmed;
                }
            case "LonVong":
                return rollTrigger;
            case "Idle_Pick":
                return pickTrigger;
            case "Punch1":
                return punch1Trigger;
            case "Punch2":
                return punch2Trigger;
            case "Slash1":
                return slash1Trigger;
            case "Slash2":
                return slash2Trigger;
            case "Slash3":
                return slash3Trigger;
            case "GetHit":
                return getHitTrigger;
            case "GeiHit2":
                return getHit2Trigger;
            case "Death":
                return isArmed ? deathArmedTrigger : deathUnarmedTrigger;
            default:
                return animName;
        }
    }

    protected override bool IsActionAnimationName(string name)
    {
        return name == rollTrigger || 
               name == getHitTrigger || 
               name == getHit2Trigger || 
               name == pickTrigger || 
               name == deathUnarmedTrigger ||
               name == deathArmedTrigger ||
               name == punch1Trigger ||
               name == punch2Trigger ||
               name == slash1Trigger ||
               name == slash2Trigger ||
               name == slash3Trigger ||
               name == "LonVong" ||
               name == "GetHit" ||
               name == "GeiHit2" ||
               name == "Idle_Pick" ||
               name == "Death" ||
               name == "Punch1" ||
               name == "Punch2" ||
               name == "Slash1" ||
               name == "Slash2" ||
               name == "Slash3" ||
               (!string.IsNullOrEmpty(drawWeaponTrigger) && name == drawWeaponTrigger) ||
               (!string.IsNullOrEmpty(sheathWeaponTrigger) && name == sheathWeaponTrigger);
    }

    protected override bool IsAttackAnimationName(string name)
    {
        return name == punch1Trigger || 
               name == punch2Trigger || 
               name == slash1Trigger || 
               name == slash2Trigger ||
               name == slash3Trigger ||
               name == "Punch1" ||
               name == "Punch2" ||
               name == "Slash1" ||
               name == "Slash2" ||
               name == "Slash3";
    }

    protected override bool IsFullBodyActionAnimation(string name)
    {
        return name == rollTrigger || 
               name == getHitTrigger || 
               name == getHit2Trigger || 
               name == pickTrigger || 
               name == deathUnarmedTrigger ||
               name == deathArmedTrigger ||
               name == "LonVong" ||
               name == "GetHit" ||
               name == "GeiHit2" ||
               name == "Idle_Pick" ||
               name == "Death";
    }

    protected override bool IsAttackState(AnimatorStateInfo stateInfo)
    {
        return stateInfo.IsName(punch1Trigger) || 
               stateInfo.IsName(punch2Trigger) || 
               stateInfo.IsName(slash1Trigger) || 
               stateInfo.IsName(slash2Trigger) ||
               stateInfo.IsName(slash3Trigger) ||
               stateInfo.IsName("Punch1") ||
               stateInfo.IsName("Punch2") ||
               stateInfo.IsName("Slash1") ||
               stateInfo.IsName("Slash2") ||
               stateInfo.IsName("Slash3");
    }

    protected override void PlayAnimationLocal(string animName, float fadeTime)
    {
        if (anim == null)
        {
            anim = GetComponent<Animator>();
            if (anim == null)
                anim = GetComponentInChildren<Animator>(true);
        }

        if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null) return;

        string translatedName = TranslateAnimName(animName);

        // Avoid looping basic movement triggers every frame
        bool isLoopingAnim = translatedName == idleUnarmed || translatedName == walkUnarmed || translatedName == runUnarmed ||
                             translatedName == idleArmed || translatedName == walkForwardArmed || translatedName == walkBackwardArmed ||
                             translatedName == runForwardArmed || translatedName == runBackwardArmed || translatedName == walkBackwardUnarmed;
        if (isLoopingAnim && currentAnimState == translatedName) return;

        Debug.Log($"[LeoPlayer] Triggering Anim: '{translatedName}' (source: '{animName}')");

        // Reset basic movement and common action triggers
        anim.ResetTrigger(idleUnarmed);
        anim.ResetTrigger(walkUnarmed);
        anim.ResetTrigger(runUnarmed);
        anim.ResetTrigger(walkBackwardUnarmed);
        anim.ResetTrigger(idleArmed);
        anim.ResetTrigger(walkForwardArmed);
        anim.ResetTrigger(walkBackwardArmed);
        anim.ResetTrigger(runForwardArmed);
        anim.ResetTrigger(runBackwardArmed);
        anim.ResetTrigger(deathUnarmedTrigger);
        anim.ResetTrigger(deathArmedTrigger);
        anim.ResetTrigger(getHitTrigger);
        anim.ResetTrigger(getHit2Trigger);
        anim.ResetTrigger(rollTrigger);
        anim.ResetTrigger(pickTrigger);

        // Also reset base class triggers to avoid state conflicts
        anim.ResetTrigger("Idle");
        anim.ResetTrigger("Walk");
        anim.ResetTrigger("run");
        anim.ResetTrigger("Death");
        anim.ResetTrigger("GetHit");
        anim.ResetTrigger("GeiHit2");
        anim.ResetTrigger("LonVong");
        anim.ResetTrigger("Idle_Pick");

        // Reset attack/combo triggers
        if (IsActionAnimationName(translatedName))
        {
            anim.ResetTrigger(punch1Trigger);
            anim.ResetTrigger(punch2Trigger);
            anim.ResetTrigger(slash1Trigger);
            anim.ResetTrigger(slash2Trigger);
            anim.ResetTrigger(slash3Trigger);
            
            anim.ResetTrigger("Punch1");
            anim.ResetTrigger("Punch2");
            anim.ResetTrigger("Slash1");
            anim.ResetTrigger("Slash2");
            anim.ResetTrigger("Slash3");
            if (!string.IsNullOrEmpty(drawWeaponTrigger)) anim.ResetTrigger(drawWeaponTrigger);
            if (!string.IsNullOrEmpty(sheathWeaponTrigger)) anim.ResetTrigger(sheathWeaponTrigger);
        }

        anim.SetTrigger(translatedName);

        bool isMovingAttack = IsAttackAnimationName(translatedName) && !isRootedAttack;
        if (!isMovingAttack)
        {
            currentAnimState = translatedName;
        }
        lastTriggeredAnimName = translatedName;

        if (IsActionAnimationName(translatedName))
        {
            lastActionTriggerTime = Time.time;
        }

        if (IsFullBodyActionAnimation(translatedName))
        {
            ClearAttackLayer();
        }
    }
}
