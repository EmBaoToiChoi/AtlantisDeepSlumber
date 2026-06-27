using UnityEngine;
using UnityEngine.Playables;

public class RakanCutscene : MonoBehaviour
{
    public Animator animator;
    public void TriggerStand()
    {
        animator.SetTrigger("Stand");
    }
    public void TriggerIdle()
    {
        animator.SetTrigger("Idle");
    }
    public void TriggerWalk()
    {
        animator.SetTrigger("Walk");
    }
    public void TriggerTalk()
    {
        animator.SetTrigger("Talk");
    }
    public void TriggerLook()
    {
        animator.SetTrigger("Look");
    }
    public void TriggerThank()
    {
        animator.SetTrigger("Thank");
    }
    public void TriggerNhinQua()
    {
        animator.SetTrigger("NhinQua");
    }

}
