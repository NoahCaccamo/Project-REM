using System.Collections;
using System.Collections.Generic;
using UnityEngine;


// https://youtu.be/fx7ryOc15Uk?si=HPibVm8HDYZZj5DT&t=2665 for hitpause
public class VFXControl : StateMachineBehaviour
{

    public float deparent = 1;
    public Animator myAnimator;
    public Transform vfxRoot;

    // OnStateEnter is called when a transition starts and the state machine starts to evaluate this state
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        myAnimator = animator;
    }

    // OnStateUpdate is called on each Update frame between OnStateEnter and OnStateExit callbacks
    override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (stateInfo.normalizedTime >= deparent) { Deparent(); }
        if (stateInfo.normalizedTime >= 1) { DestroySelf(); }
    }

    public void Deparent()
    {
        if (vfxRoot != null) { if (vfxRoot.parent != null && vfxRoot.parent != vfxRoot) { vfxRoot.SetParent(null); } }
    }

    // ideally use object pool instead of rapid creation and destruction
    public void DestroySelf()
    {
        if (vfxRoot != null) { Destroy(vfxRoot.gameObject); }
        else { Destroy(myAnimator.gameObject); }
    }
    // OnStateExit is called when a transition ends and the state machine finishes evaluating this state
    //override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    //{
    //    
    //}

    // OnStateMove is called right after Animator.OnAnimatorMove()
    //override public void OnStateMove(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    //{
    //    // Implement code that processes and affects root motion
    //}

    // OnStateIK is called right after Animator.OnAnimatorIK()
    //override public void OnStateIK(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    //{
    //    // Implement code that sets up animation IK (inverse kinematics)
    //}
}
