using UnityEngine;
using UnityEngine.Playables;


public class Player_CungDien : MonoBehaviour
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
    public void NoiChuyen()
    {
        animator.SetTrigger("NoiChuyen");
    }
    public void RutVuKhi()
    {
        animator.SetTrigger("RutVuKhi");
    }
    public void RutCung()
    {
        animator.SetTrigger("RutCung");
    }

}
