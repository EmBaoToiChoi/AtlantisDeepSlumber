using UnityEngine;
using UnityEngine.Playables;

public class NgoiPlayer : MonoBehaviour
{
    public Animator animator;
    public void TriggerGatDauNgoi()
    {
        animator.SetTrigger("GatDauNgoi");
    }
    public void TriggerNgoi()
    {
        animator.SetTrigger("Ngoi");
    }


}
