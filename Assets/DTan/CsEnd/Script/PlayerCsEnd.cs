using UnityEngine;
using UnityEngine.Playables;

public class PlayerCsEnd : MonoBehaviour
{
    public Animator animator;
    public void DungYen()
    {
        animator.SetTrigger("DungYen");
    }
    public void NhinLen()
    {
        animator.SetTrigger("NhinLen");
    }
    public void CatKiem()
    {
        animator.SetTrigger("CatKiem");
    }
    public void Chay()
    {
        animator.SetTrigger("Chay");
    }

}
