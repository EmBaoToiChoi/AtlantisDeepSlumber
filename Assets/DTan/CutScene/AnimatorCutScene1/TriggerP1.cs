using UnityEngine;
using UnityEngine.Playables;

public class P1 : MonoBehaviour
{
    public Animator animator;

    public void TriggerStand()
    {
        animator.SetTrigger("Standing");
    }
}
