using UnityEngine;

public class RollStateBehaviour : StateMachineBehaviour
{
    // OnStateExit is called when a transition ends and the state machine finishes evaluating this state
    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        LeoPlayer player = animator.GetComponentInParent<LeoPlayer>();
        if (player != null)
        {
            player.OnRollEnd();
        }
    }
}
