using UnityEngine;

/// <summary>
/// Helper script to programmatically add the "OnRollEnd" AnimationEvent to read-only roll clips
/// (like "LonVong" or "Lonmeo") in the Animator Controller at runtime.
/// Since FBX animations are read-only, this resolves the issue without modifying the source asset files.
/// Attach this script to the GameObject containing the Animator component (or the Player parent GameObject).
/// </summary>
public class RollAnimationEventHelper : MonoBehaviour
{
    private LeoPlayer player;

    private void Start()
    {
        // Find LeoPlayer on this GameObject or in parents
        player = GetComponentInParent<LeoPlayer>();
        if (player == null)
        {
            player = GetComponentInChildren<LeoPlayer>(true);
        }

        Animator anim = GetComponent<Animator>();
        if (anim == null)
        {
            anim = GetComponentInChildren<Animator>(true);
        }

        if (anim != null && anim.runtimeAnimatorController != null)
        {
            // Iterate over all animation clips inside the Animator Controller
            foreach (var clip in anim.runtimeAnimatorController.animationClips)
            {
                string clipNameLower = clip.name.ToLower();
                if (clipNameLower == "lonvong" || clipNameLower == "lonmeo" || 
                    clipNameLower.Contains("lonvong") || clipNameLower.Contains("lonmeo"))
                {
                    // Check if the OnRollEnd event is already present
                    bool eventExists = false;
                    if (clip.events != null)
                    {
                        foreach (var ev in clip.events)
                        {
                            if (ev.functionName == "OnRollEnd")
                            {
                                eventExists = true;
                                break;
                            }
                        }
                    }

                    if (!eventExists)
                    {
                        AnimationEvent rollEndEvent = new AnimationEvent();
                        rollEndEvent.time = clip.length * 0.95f; // Fire near the end of the clip (95% duration)
                        rollEndEvent.functionName = "OnRollEnd";
                        
                        clip.AddEvent(rollEndEvent);
                        Debug.Log($"[RollAnimationEventHelper] Automatically added 'OnRollEnd' event to read-only clip: {clip.name} ({clip.length} seconds)");
                    }
                }
            }
        }
        else
        {
            Debug.LogWarning("[RollAnimationEventHelper] Animator or RuntimeAnimatorController is missing. Cannot add events.");
        }
    }

    /// <summary>
    /// Event receiver called by the Animation Event.
    /// Forwards the roll end callback to the LeoPlayer instance.
    /// </summary>
    public void OnRollEnd()
    {
        if (player == null)
        {
            player = GetComponentInParent<LeoPlayer>();
            if (player == null)
            {
                player = GetComponentInChildren<LeoPlayer>(true);
            }
        }

        if (player != null)
        {
            player.OnRollEnd();
            Debug.Log("[RollAnimationEventHelper] Forwarded OnRollEnd callback to LeoPlayer.");
        }
        else
        {
            Debug.LogWarning("[RollAnimationEventHelper] Received OnRollEnd event, but LeoPlayer component is not found.");
        }
    }
}
