using UnityEngine;
using UnityEngine.InputSystem;


// 다른 스크립트는 이 클래스의 static 필드를 그대로 읽어서 쓰면 됨
// Script Execution Order에서 가장 먼저 실행되도록 설정해야 한다.

public class InputManager : MonoBehaviour
{
    private static PlayerInput playerInput;

    // 이동
    public static Vector2 moveDir;
    private InputAction moveAction;

    // 시점 
    public static Vector2 lookDelta;
    private InputAction lookAction;

    // 점프 
    public static bool jumpPressed;
    private InputAction jumpAction;

    // 달리기 
    public static bool sprintHeld;
    private InputAction sprintAction;

    //공격
    public static bool attackPressed;
    private InputAction attackAction;

    //상호작용(미션)
    public static bool interactPressed;
    private InputAction interactAction;

    

    private void Awake()
    {
    

        
        playerInput = GetComponent<PlayerInput>();
        
        

        moveAction = playerInput.actions["Move"];
        lookAction = playerInput.actions["Look"];
        jumpAction = playerInput.actions["Jump"];
        sprintAction = playerInput.actions["Sprint"];
        attackAction = playerInput.actions["Attack"];
        interactAction = playerInput.actions["Interact"];


    }

    private void Update()
    {
        moveDir = moveAction.ReadValue<Vector2>();
        lookDelta = lookAction.ReadValue<Vector2>();

        jumpPressed = jumpAction.WasPressedThisFrame();
        sprintHeld = sprintAction.IsPressed();

        attackPressed = attackAction.WasPressedThisFrame();
        interactPressed = interactAction.WasPressedThisFrame();

    }

    public static void ActivatePlayerControls()
    {
        playerInput.currentActionMap.Enable();
    }

    public static void DeactivatePlayerControls()
    {
        playerInput.currentActionMap.Disable();
    }
}