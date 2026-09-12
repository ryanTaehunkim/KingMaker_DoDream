using UnityEngine;
using Unity.Netcode;
using Unity.Cinemachine;

[RequireComponent(typeof(CharacterController))]
public class PlayerControl : NetworkBehaviour
{
    [Header("References")]
    [Tooltip("플레이어 자식으로 있는 카메라 기준점")]
    [SerializeField] private Transform cameraTransform;

    [Tooltip("플레이어 자식으로 있는 Head Transform")]
    [SerializeField] private Transform headTransform;

    [Header("Move Settings")]
    [SerializeField] private float walkSpeed = 4.0f;
    [SerializeField] private float sprintSpeed = 7.0f;

    [Header("Jump / Gravity")]
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -9.81f;

    [Header("Look Settings")]
    [SerializeField] private float mouseSensitivity = 0.12f;
    [SerializeField] private float minPitch = -85f;
    [SerializeField] private float maxPitch = 85f;

    private CharacterController controller;

    private Vector3 currentVelocity;
    private float verticalVelocity;
    private float pitch;

    private CinemachineCamera cinemachineCamera;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    public override void OnNetworkSpawn()
    {
        // 내가 조종하는 Player가 아니면 카메라를 설정하지 않음
        if (!IsOwner)
            return;

        SetupLocalCamera();
    }

    private void OnEnable()
    {
        if (!IsOwner)
            return;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        // 내 Player만 입력을 처리
        if (!IsOwner)
            return;

        HandleLook();
        Move();
        Jump();

        Vector3 finalMove =
            currentVelocity +
            Vector3.up * verticalVelocity;

        controller.Move(finalMove * Time.deltaTime);
    }

    private void SetupLocalCamera()
    {
        // 씬에 있는 CinemachineCamera 찾기
        cinemachineCamera =
            FindFirstObjectByType<CinemachineCamera>();

        if (cinemachineCamera == null)
        {
            Debug.LogWarning(
                "PlayerControl: 씬에서 CinemachineCamera를 찾을 수 없습니다."
            );

            return;
        }

        // Head Transform이 지정되어 있는지 확인
        if (headTransform == null)
        {
            Debug.LogWarning(
                "PlayerControl: Head Transform이 지정되지 않았습니다."
            );

            return;
        }

        // 내 Player의 Head를 CinemachineCamera의 Tracking Target으로 설정
        cinemachineCamera.Target.TrackingTarget = headTransform;

        Debug.Log(
            $"PlayerControl: Local Camera Target 설정 완료 → {headTransform.name}"
        );
    }

    private void HandleLook()
    {
        Vector2 delta =
            InputManager.lookDelta *
            mouseSensitivity;

        // 좌우 회전
        transform.Rotate(
            Vector3.up * delta.x
        );

        // 상하 회전
        pitch -= delta.y;

        pitch = Mathf.Clamp(
            pitch,
            minPitch,
            maxPitch
        );

        if (headTransform != null)
        {
            headTransform.localRotation =
                Quaternion.Euler(
                    pitch,
                    0f,
                    0f
                );
        }
    }

    private void Move()
    {
        Vector2 input =
            InputManager.moveDir;

        Vector3 desiredDir =
            transform.right * input.x +
            transform.forward * input.y;

        desiredDir =
            Vector3.ClampMagnitude(
                desiredDir,
                1f
            );

        float speed = Run();

        currentVelocity =
            desiredDir * speed;
    }

    private float Run()
    {
        return InputManager.sprintHeld
            ? sprintSpeed
            : walkSpeed;
    }

    private void Jump()
    {
        bool isGrounded =
            controller.isGrounded;

        if (isGrounded &&
            verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        if (InputManager.jumpPressed &&
            isGrounded)
        {
            verticalVelocity =
                Mathf.Sqrt(
                    jumpHeight *
                    -2f *
                    gravity
                );
        }

        verticalVelocity +=
            gravity *
            Time.deltaTime;
    }
}