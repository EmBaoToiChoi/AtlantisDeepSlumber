using UnityEngine;
using UnityEngine.Playables;

public class RakanDNT : MonoBehaviour
{
    public Animator animator;
    public void KhoangTay()
    {
        animator.SetTrigger("KhoangTay");
    }
    public void DungYen()
    {
        animator.SetTrigger("DungYen");
    }
    public void Di()
    {
        animator.SetTrigger("Di");
    }
    public void SuyNgam()
    {
        animator.SetTrigger("SuyNgam");
    }
    public void GioHaiTay()
    {
        animator.SetTrigger("GioHaiTay");
    }
}
