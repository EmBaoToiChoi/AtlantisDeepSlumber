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

}
