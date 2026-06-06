using UnityEngine;
using UnityEngine.Playables;

public class P1 : MonoBehaviour
{
    public Animator animator;

    public void TriggerStand()
    {
        animator.SetTrigger("Standing");
    }
    public void TriggerTalk()
    {
        animator.SetTrigger("Talk1");
    }
    public void TriggerIdle()
    {
        animator.SetTrigger("Idle");
    }
    public void TriggerWalk()
    {
        animator.SetTrigger("Walk");
    }
    public void TriggerLookBehide()
    {
        animator.SetTrigger("LookBehide");
    }
}
