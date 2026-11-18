using UnityEngine;

public class PunchAttackState : StateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        var combat = animator.GetComponent<PunchCombat>();
        if (combat != null)
            combat.BeginAttack();
    }
}
