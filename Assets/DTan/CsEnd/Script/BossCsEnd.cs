using UnityEngine;
using UnityEngine.Playables;

public class BossCsEnd : MonoBehaviour
{
    public Animator animator;
            public void DungLen()
    {
        animator.SetTrigger("DungLen");
    }
    public void BayLen()
    {
        animator.SetTrigger("BayLen");
    }
  

}
