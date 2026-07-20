using UnityEngine;
using UnityEngine.Playables;

public class ZomRMD : MonoBehaviour
{
    public Animator animator;

    // Trigger Đi (Walk)
    public void Di1()
    {
        animator.SetTrigger("di1");
    }

    // Trigger Đứng Yên (Idle)
    public void Di2()
    {
        animator.SetTrigger("di2");
    }

    // Trigger Gật Đầu (Nod)
    public void ve()
    {
        animator.SetTrigger("ve");
    }
}