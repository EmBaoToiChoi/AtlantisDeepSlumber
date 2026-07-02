using UnityEngine;
using UnityEngine.Playables;

public class RakanMeCung : MonoBehaviour
{
    public Animator animator;
    public void KhoangTay()
    {
        animator.SetTrigger("KhoangTay");
    }
    public void GioHaiTay()
    {
        animator.SetTrigger("GioHaiTay");
    }
    public void NhinTheo()
    {
        animator.SetTrigger("NhinTheo");
    }
    public void NhinTheoDungYen()
    {
        animator.SetTrigger("NhinTheoDungYen");
    }

}
