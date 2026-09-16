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

    [Header("Interaction")]
    [SerializeField] private float interactionDistance = 3f;
    [SerializeField] private Camera playerCamera;
    private bool canInteract;
    private GameObject currentInteractable;

    private CharacterController characterController;
    private Vector3 currentVelocity;
    private float verticalVelocity;
    private float pitch;

    //뛰는 중인지 서버에 보내기 위한 변수
    private PlayerStateList playerState; 
    private bool isRunning;

    private Transform handTransform;

    private void Awake()
    {
        characterController = GetComponent<CharacterController> ();
        virtualCamera = FindFirstObjectByType<CinemachineCamera> ();
        playerState = GetComponent<PlayerStateList>();

        GameObject camearObj = GameObject.FindGameObjectWithTag("MainCamera");
        if(camearObj != null)
        {
            playerCamera = camearObj.GetComponent<Camera> ();
        }
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
        Debug.Log(playerState.stamina.Value.ToString());
        HandleLook();
        UpdateRunState();
        Move();
        Jump();
        DetectInteractable();
        TrytoInteract();

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
        return isRunning ? sprintSpeed : walkSpeed;
    }

    private void UpdateRunState()
    {
        bool newRunningState = InputManager.sprintHeld && playerState.stamina.Value < 5f;


        if(isRunning != newRunningState)
        {
            isRunning = newRunningState;
            SetRunningServerRpc(isRunning);
        }
    }

    [ServerRpc]
    private void SetRunningServerRpc(bool running)
    {
        playerState.SetRunning(running);
       
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

    private void DetectInteractable()
    {
        currentInteractable = null;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        Debug.DrawRay(ray.origin, ray.direction * interactionDistance, Color.red);

        if(Physics.Raycast(ray,out RaycastHit hit, interactionDistance)){
            
            if (hit.collider.CompareTag("Interactable"))
            {
                canInteract = true;
                currentInteractable = hit.collider.gameObject;
                Debug.Log("상호작용 가능: " + currentInteractable.name);

            }
            else
            {
                canInteract = false;
                currentInteractable = null;
            }
        }
    }

    private void TrytoInteract()
    {
        if(canInteract && currentInteractable != null&&InputManager.interactPressed)
        {
            if(currentInteractable != null)
            {
                IInteractable interactobj = currentInteractable.GetComponent<IInteractable>();
                if (interactobj != null) {
                    interactobj.Interact(playerState);
                }
            }
            
        }
    }

    public Transform GetHandTransform()
    {
        return handTransform;
    }
}
