using UnityEngine;
using UnityEngine.Playables;

public class TimelineSignalReceiver : MonoBehaviour
{
    public Animator animator;

    public void TriggerIdle()
    {
        animator.SetTrigger("IdleTrigger");
    }
        public void TriggerTalk()
    {
        animator.SetTrigger("TalkTrigger");
    }
    public void TriggerDW()
    {
        animator.SetTrigger("DW");
    }
    public void TriggerFR()
    {
        animator.SetTrigger("FR");
    }
}
