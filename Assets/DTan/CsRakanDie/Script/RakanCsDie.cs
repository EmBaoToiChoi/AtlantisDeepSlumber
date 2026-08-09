using UnityEngine;
using UnityEngine.Playables;

public class RakanCsDie : MonoBehaviour
{
    public Animator animator;
    public void BienThan()
    {
        animator.SetTrigger("BienThan");
    }
    public void HtBienThan()
    {
        animator.SetTrigger("HtBienThan");
    }
    public void DungYen()
    {
        animator.SetTrigger("DungYen");
    }
    public void Di()
    {
        animator.SetTrigger("Di");
    }


}
