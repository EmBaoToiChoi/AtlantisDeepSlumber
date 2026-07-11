using UnityEngine;
using UnityEngine.Playables;

public class PlayerRun : MonoBehaviour
{
        public Animator animator;
        public void TriggerDi()
    {
        animator.SetTrigger("Di");
    }
    public void TriggerGatDau()
    {
        animator.SetTrigger("GatDau");
    }
    public void TriggerIdle()
    {
        animator.SetTrigger("DungYen");
    }
    public void TriggerNoiChuyen()
    {
        animator.SetTrigger("NoiChuyen");
    }

}
