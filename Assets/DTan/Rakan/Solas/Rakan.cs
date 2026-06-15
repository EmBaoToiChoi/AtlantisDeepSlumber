using UnityEngine;
using UnityEngine.Playables;
public class Rakan : MonoBehaviour
{
    public Animator animator;
    public void TriggerPraying()
    {
        animator.SetTrigger("Quy");
    }
    public void SetTriggerQuy()
    {
        animator.SetTrigger("QuyThem");
    }
    public void SetTriggerStand()
    {
        animator.SetTrigger("STA");
    }
    public void SetTriggerNHQ()
    {
        animator.SetTrigger("NHQ");
    }
    public void SetTriggerTHR()
    {
        animator.SetTrigger("THR");
    }
    public void SetTriggerTK()
    {
        animator.SetTrigger("TK");
    }
    public void SetTriggerID()
    {
        animator.SetTrigger("ID");
    }

}
