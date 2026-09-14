using Unity.Netcode;
using UnityEngine;
using Unity.Cinemachine;

[RequireComponent (typeof(CharacterController))]
public class PlayerControlForNetwork : NetworkBehaviour
{
    

    [Header("Move Settings")]
    [SerializeField] private float walkSpeed = 4.0f;
    [SerializeField] private float sprintSpeed = 7.0f;

    [Header("Jump / Gravity")]
    [SerializeField] private float jumpHeight = 1.2f; //점프 높이
    [SerializeField] private float gravity = -9.8f; //중력

    [Header("Look Settings")]
    [SerializeField] private float mouseSensitivity = 0.12f;
    [SerializeField] private float minPitch = -85f; //고개 최대 내림 각도
    [SerializeField] private float maxPitch = 85f; //고개 최대 올림 각도
    [SerializeField] private Transform headTransform; //돌릴 머리 위치
    [SerializeField] private CinemachineCamera virtualCamera;
    [SerializeField] private Transform cameraTransform;

    private CharacterController characterController;
    private Vector3 currentVelocity;
    private float verticalVelocity;
    private float pitch;

    private void Awake()
    {
        characterController = GetComponent<CharacterController> ();
        virtualCamera = FindFirstObjectByType<CinemachineCamera> ();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            return;
        }
        if(virtualCamera != null)
        {
            virtualCamera.Follow = cameraTransform;
        }
        CursorLock();
    }
    private void CursorLock()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (!IsOwner) return;

        HandleLook();
        Move();
        Jump();

        Vector3 finalMove = currentVelocity + Vector3.up * verticalVelocity;
        characterController.Move(finalMove * Time.deltaTime);
    }
    private void HandleLook()
    {
        Vector2 delta = InputManager.lookDelta * mouseSensitivity;
        transform.Rotate(Vector3.up * delta.x);

        pitch -= delta.y;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        if (headTransform != null)
        {
            headTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }

    private void Move()
    {
        Vector2 input = InputManager.moveDir;
        Vector3 desiredDir = (transform.right * input.x + transform.forward * input.y);
        desiredDir = Vector3.ClampMagnitude(desiredDir, 1f);

        float speed = Run();
        currentVelocity = desiredDir * speed;
    }

    private float Run()
    {
        return InputManager.sprintHeld ? sprintSpeed : walkSpeed;
    }

    private void Jump()
    {
        bool isGrounded = characterController.isGrounded;
        if (isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        if (InputManager.jumpPressed && isGrounded)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        verticalVelocity += gravity * Time.deltaTime;
    }
}
