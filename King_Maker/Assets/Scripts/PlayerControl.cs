using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerControl : MonoBehaviour
{
    [Header("References")]
    [Tooltip("플레이어 자식으로 있는 카메라 Transform")]
    [SerializeField] private Transform cameraTransform;

    [Header("Move Settings")]
    [SerializeField] private float walkSpeed = 4.0f;
    [SerializeField] private float sprintSpeed = 7.0f;

    [Header("Jump / Gravity")]
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -19.6f;

    [Header("Look Settings")]
    [SerializeField] private float mouseSensitivity = 0.12f;
    [SerializeField] private float minPitch = -85f;
    [SerializeField] private float maxPitch = 85f;

    private CharacterController controller;
    private Vector3 currentVelocity;
    private float verticalVelocity;
    private float pitch;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void OnEnable()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        HandleLook();
        Move();
        Jump();

        // Move()가 만든 수평 속도 + Jump()가 만든 수직 속도를 합쳐서 실제로 이동시킴
        Vector3 finalMove = currentVelocity + Vector3.up * verticalVelocity;
        controller.Move(finalMove * Time.deltaTime);
    }

    private void HandleLook()
    {
        // InputManager.lookDelta : 마우스 델타 (x = 좌우, y = 상하)
        Vector2 delta = InputManager.lookDelta * mouseSensitivity;

        // 좌우 - 몸통(Player) 회전
        transform.Rotate(Vector3.up * delta.x);

        // 상하 - 카메라만 회전 (클램프)
        pitch -= delta.y;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        if (cameraTransform != null)
        {
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }

    private void Move()
    {
        // InputManager.moveDir : WASD 입력 (x = 좌우, y = 앞뒤)
        Vector2 input = InputManager.moveDir;
        Vector3 desiredDir = (transform.right * input.x + transform.forward * input.y);
        desiredDir = Vector3.ClampMagnitude(desiredDir, 1f);

        float speed = Run();

        // 가속/감속 없이 입력 방향 * 속도로 매 프레임 즉시 덮어씀
        currentVelocity = desiredDir * speed;
    }

    private float Run()
    {
        return InputManager.sprintHeld ? sprintSpeed : walkSpeed;
    }


    private void Jump()
    {
        bool isGrounded = controller.isGrounded;
        if (isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f; // 지면에 붙어있도록
        }

        if (InputManager.jumpPressed && isGrounded)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        verticalVelocity += gravity * Time.deltaTime;
    }
}