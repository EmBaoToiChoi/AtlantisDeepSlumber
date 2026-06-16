using UnityEngine;
using UnityEngine.Playables;

public class RunSignal : MonoBehaviour
{
    public Animator animator;
    public void SetTriggerRun()
    {
        animator.SetTrigger("RUN");
    }

}
