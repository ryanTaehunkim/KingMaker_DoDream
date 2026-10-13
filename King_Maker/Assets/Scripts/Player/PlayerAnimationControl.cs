using Unity.Netcode;
using UnityEngine;

public class PlayerAnimationControl : NetworkBehaviour
{
    private Animator animator;
    private PlayerControlRigid pc;

    private static readonly int IsMovingHash = Animator.StringToHash("isWalk");
    private static readonly int IsRunningHash = Animator.StringToHash("isRun");
    private static readonly int IsJumpingHash = Animator.StringToHash("isJump");

    private void Awake()
    {
        animator = GetComponent<Animator>();
        pc = GetComponent<PlayerControlRigid>();

    }
    private void Update()
    {
        if (!IsOwner) return; // 파라미터는 Owner만 세팅, 전파는 NetworkAnimator가 처리

        bool isMoving = InputManager.moveDir.sqrMagnitude > 0.01f;
        //bool isRunning = pc.isRunning; // 스태미나 조건은 PlayerControlRigid의 isRunning을 공개해서 쓰는 게 정확
        //bool isjumping = pc.isJumping;

        animator.SetBool(IsMovingHash, isMoving);
        //animator.SetBool(IsRunningHash, isRunning);
        //animator.SetBool(IsJumpingHash, isjumping);
    }
}