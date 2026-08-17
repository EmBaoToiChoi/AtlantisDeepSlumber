using UnityEngine;
using UnityEngine.Playables;

public class Rakan_CungDien : MonoBehaviour
{
    public Animator animator;
    public void Di()
    {
        animator.SetTrigger("Di");
    }
    public void DungYen()
    {
        animator.SetTrigger("DungYen");
    }
    public void KinhChao()
    {
        animator.SetTrigger("KinhChao");
    }
    public void KhoangTay()
    {
        animator.SetTrigger("KhoangTay");
    }
    public void BieuCam()
    {
        animator.SetTrigger("BieuCam");
    }
    public void ChuyenVuKhi()
    {
        animator.SetTrigger("ChuyenVuKhi");
    }

}
