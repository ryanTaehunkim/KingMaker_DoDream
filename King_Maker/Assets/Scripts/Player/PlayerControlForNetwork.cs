using Unity.Netcode;
using UnityEngine;
using Unity.Cinemachine;

[RequireComponent (typeof(CharacterController))]
public class PlayerControlForNetwork : NetworkBehaviour
{
    

    [Header("Move Settings")]
    [SerializeField] private float walkSpeed = 20f;
    [SerializeField] private float sprintSpeed = 35.0f;

    [Header("Jump / Gravity")]
    [SerializeField] private float jumpHeight = 6f; //점프 높이
    [SerializeField] private float gravity = -98f; //중력
    [SerializeField] private float jumpBufferTime = 0.15f;
    private float jumpBufferCounter;

    [Header("Look Settings")]
    [SerializeField] private float mouseSensitivity = 0.12f;
    [SerializeField] private float minPitch = -85f; //고개 최대 내림 각도
    [SerializeField] private float maxPitch = 85f; //고개 최대 올림 각도
    [SerializeField] private Transform headTransform; //돌릴 머리 위치
    private CinemachineCamera virtualCamera;
    [SerializeField] private Transform faceTransform;//실제 카메라 추적대상

    [Header("Interaction")]
    [SerializeField] private float interactionDistance = 9f;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private LayerMask interactMask;

    private bool canInteract;
    private GameObject currentInteractable;
    private GameObject holdItem;

    [Header("Attack&&knockback")]
    [SerializeField] private float knockbackDecay = 8f;
    private Vector3 knockbackVelocity;
    private PlayerCombat combat;

    private CharacterController characterController;
    private Vector3 currentVelocity;
    private float verticalVelocity;
    private float pitch;

    //뛰는 중인지 서버에 보내기 위한 변수
    private PlayerStateList playerState;

    public  Animator animator;
    private bool isGrounded;
    private bool isRunning;
    private static readonly int IsWalkHash = Animator.StringToHash("isWalk");
    private static readonly int IsRunHash = Animator.StringToHash("isRun");
    private static readonly int IsJumpHash = Animator.StringToHash("isJump");



    private void Awake()
    {
        characterController = GetComponent<CharacterController> ();
        virtualCamera = FindFirstObjectByType<CinemachineCamera> ();
        playerState = GetComponent<PlayerStateList>();
        combat = GetComponent<PlayerCombat>();
        animator = GetComponentInChildren<Animator>();

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
            virtualCamera.Follow = faceTransform;
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
        //사망시 추가 코드


        HandleLook();
        UpdateRunState();
        Move();
        Jump();
        
        DetectInteractable();
        TrytoInteract();
        PutDownObject();
        combat.TryAttack();

        ApplyMovement();
        UpdateAnimation();
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
        bool newRunningState = InputManager.sprintHeld && playerState.stamina.Value > 0f;
        

        if (isRunning != newRunningState)
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

    private void ApplyMovement()
    {
        Vector3 horizontal = currentVelocity + knockbackVelocity;
        Vector3 finalMove = horizontal + Vector3.up * verticalVelocity;
        characterController.Move(finalMove * Time.deltaTime);

        isGrounded = characterController.isGrounded;
        knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, knockbackDecay * Time.deltaTime);
        if (knockbackVelocity.sqrMagnitude < 0.01f) knockbackVelocity = Vector3.zero;
    }
    public void ApplyKnockback(Vector3 horizontal, float upward)
    {
        if (!IsOwner) return;
        horizontal.y = 0f;
        knockbackVelocity = horizontal;
        verticalVelocity = upward;
    }
    private void Jump()
    {
        bool isGrounded = characterController.isGrounded;

        if (InputManager.jumpPressed) jumpBufferCounter = jumpBufferTime;
        else jumpBufferCounter -= Time.deltaTime;

        if (isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        if (jumpBufferCounter>0f && isGrounded)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpBufferCounter = 0f;
        }
        

        verticalVelocity += gravity * Time.deltaTime;
    }

    private void UpdateAnimation()
    {
        Vector2 input = InputManager.moveDir;
        bool moving = input.sqrMagnitude > 0.01f;

        animator.SetBool(IsWalkHash, moving);
        animator.SetBool(IsRunHash, isRunning && moving);
        animator.SetBool(IsJumpHash, !isGrounded&& knockbackVelocity.sqrMagnitude < 0.01f);
    }
    private void DetectInteractable()
    {
        currentInteractable = null;
        canInteract = false;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        Debug.DrawRay(ray.origin, ray.direction * interactionDistance, Color.red);

        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance, interactMask, QueryTriggerInteraction.Ignore)
        && hit.collider.CompareTag("Interactable")) {
            Debug.Log("감지");
            canInteract = true;
            currentInteractable = hit.collider.gameObject;
                
        }
    }

    private void TrytoInteract()
    {
        if(canInteract && currentInteractable != null&&InputManager.interactPressed)
        {
            if(currentInteractable != null)
            {
                IInteractable interactobj = currentInteractable.GetComponentInParent<IInteractable>();
                if (interactobj != null) {
                    interactobj.Interact(playerState);
                    
                }
            }
            
        }

    }

    private void PutDownObject()
    {
        if (playerState.isHoldingItem.Value && InputManager.putDownPressed)
        {
            Debug.Log("놓기");
            ulong itemId = playerState.heldItemNetworkId.Value;

            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemId, out NetworkObject itemNetObj))
            {
                GrabInteractableObject grabScript = itemNetObj.GetComponent<GrabInteractableObject>();
                if (grabScript != null)
                {
                    grabScript.PutDown();
                }

            }
        }
    }


}
