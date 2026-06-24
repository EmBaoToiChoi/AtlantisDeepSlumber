using UnityEngine;
using UnityEngine.Playables;


public class RakanChiTay : MonoBehaviour
{
            public Animator animator;
            public void TriggerChiTay()
            {
                animator.SetTrigger("ChiTay");
            }

}
