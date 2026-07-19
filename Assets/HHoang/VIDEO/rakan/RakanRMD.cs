using UnityEngine;
using UnityEngine.Playables;

public class RakanRMD : MonoBehaviour
{
    public Animator animator;

    // Trigger Đi (Walk)
    public void Di()
    {
        animator.SetTrigger("Di");
    }

    // Trigger Đứng Yên (Idle)
    public void DungYen()
    {
        animator.SetTrigger("DungYen");
    }

    // Trigger Gật Đầu (Nod)
    public void GatDau()
    {
        animator.SetTrigger("GatDau");
    }

    // Trigger Nói Chuyện (Talk)
    // Lưu ý: Nếu trong Animator ông ghi đúng chữ "NoiChuye" chưa có chữ "n" thì phải xóa chữ "n" ở dòng dưới đi nhé.
    public void NoiChuyen()
    {
        animator.SetTrigger("NoiChuyen");
    }

    // Trigger Khoanh Tay (Cross Arms) - Cái này ông viết rồi nè
    public void KhoangTay()
    {
        animator.SetTrigger("KhoangTay");
    }
}