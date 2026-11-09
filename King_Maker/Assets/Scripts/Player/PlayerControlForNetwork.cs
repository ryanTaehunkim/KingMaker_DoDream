using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using Unity.Cinemachine;

[RequireComponent(typeof(CharacterController))]
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

    //이동 제한 (블라인드 드롭 등)
    private PlayerMovementLock movementLock;
    private bool IsLockedNow => movementLock != null && movementLock.IsLocked.Value;

    //순간이동용
    private NetworkTransform networkTransform;

    public Animator animator;
    private bool isGrounded;
    private bool isJumping;
    private bool isRunning;
    [HideInInspector] public bool isMoving;
    private static readonly int IsWalkHash = Animator.StringToHash("isWalk");
    private static readonly int IsRunHash = Animator.StringToHash("isRun");
    private static readonly int IsJumpHash = Animator.StringToHash("isJump");



    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        virtualCamera = FindFirstObjectByType<CinemachineCamera>();
        playerState = GetComponent<PlayerStateList>();
        combat = GetComponent<PlayerCombat>();
        animator = GetComponentInChildren<Animator>();
        movementLock = GetComponent<PlayerMovementLock>();
        networkTransform = GetComponent<NetworkTransform>();

        GameObject camearObj = GameObject.FindGameObjectWithTag("MainCamera");
        if (camearObj != null)
        {
            playerCamera = camearObj.GetComponent<Camera>();
        }
    }

    public override void OnNetworkSpawn()
    {
        // 스폰 직후 CharacterController가 옛 위치를 들고 있지 않도록 현재 위치를 다시 읽게 한다.
        ResyncCharacterController();

        if (!IsOwner)
        {

            return;
        }
        if (virtualCamera != null)
        {
            virtualCamera.Follow = faceTransform;
        }
        CursorLock();

        // 원격 클라이언트: 서버가 정해준 스폰 위치로 이동을 요청한다.
        // (호스트는 스폰 위치에서 바로 생성되므로 필요 없음)
        if (!IsServer)
        {
            RequestSpawnPlacementRpc();
        }
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


        HandleLook();   // 잠겨도 고개는 움직임

        // 블라인드 드롭 등으로 잠긴 동안: 이동/점프/공격/상호작용 입력은 무시
        if (IsLockedNow)
        {
            UpdateLocked();
            return;
        }

        UpdateRunState();
        Move();
        Jump();

        DetectInteractable();
        TrytoInteract();
        PutDownObject();
        combat.TryAttack();

        ApplyMovement();
        UpdateAnimation();
        Debug.Log(isMoving);
    }

    // 잠긴 동안: 입력으로 생긴 움직임을 멈추고 중력만 적용한다.
    private void UpdateLocked()
    {
        currentVelocity = Vector3.zero;
        knockbackVelocity = Vector3.zero;
        jumpBufferCounter = 0f;

        // 달리던 중에 잠기면 서버 스태미나 계산도 멈춰야 함
        if (isRunning)
        {
            isRunning = false;
            SetRunningServerRpc(false);
        }

        // 중력만 적용 (바닥에 붙어 있도록)
        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
            isJumping = false;
        }
        verticalVelocity += gravity * Time.deltaTime;

        ApplyMovement();

        animator.SetBool(IsWalkHash, false);
        animator.SetBool(IsRunHash, false);
        animator.SetBool(IsJumpHash, false);
    }

    private void HandleLook()
    {
        // 커서가 풀려 있으면(버튼을 누르는 중) 시점도 돌리지 않는다
        if (Cursor.lockState != CursorLockMode.Locked) return;

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
        if (horizontal.magnitude < 0.01f && !isJumping) isMoving = false;
        else isMoving = true;
    }
    public void ApplyKnockback(Vector3 horizontal, float upward)
    {
        if (!IsOwner) return;
        horizontal.y = 0f;
        knockbackVelocity = horizontal;
        verticalVelocity = upward;
    }

    // =========================================================
    // 순간이동 (CharacterController 전용)
    // =========================================================
    // CharacterController는 transform을 직접 옮기면 다음 Move에서 옛 위치로 되돌아간다.
    // 그래서 CharacterController를 잠깐 끈 상태에서 위치를 바꾼 뒤 다시 켠다.
    // GameFlowManager(필드 <-> 드롭 이동)와 PlayerSpawnManager(스폰)가 이 함수를 사용한다.
    public void TeleportTo(Vector3 position, Quaternion rotation)
    {
        if (!IsOwner) return;

        // 남아있는 움직임 제거
        currentVelocity = Vector3.zero;
        knockbackVelocity = Vector3.zero;
        verticalVelocity = 0f;
        jumpBufferCounter = 0f;

        bool wasEnabled = characterController.enabled;
        characterController.enabled = false;

        if (networkTransform != null)
            networkTransform.Teleport(position, rotation, transform.localScale);
        else
            transform.SetPositionAndRotation(position, rotation);

        characterController.enabled = wasEnabled;
        Physics.SyncTransforms();

        Debug.Log($"[Teleport] ClientId={OwnerClientId} -> {position}");
    }

    // 서버가 해당 플레이어(오너)에게 순간이동을 지시할 때 사용
    [Rpc(SendTo.SpecifiedInParams)]
    public void TeleportOwnerRpc(Vector3 position, Quaternion rotation, RpcParams rpcParams = default)
    {
        TeleportTo(position, rotation);
    }

    // 원격 클라이언트가 스폰 직후 "내 스폰 위치가 어디인지" 서버에 묻는다.
    [Rpc(SendTo.Server)]
    private void RequestSpawnPlacementRpc(RpcParams rpcParams = default)
    {
        if (PlayerSpawnManager.Instance == null) return;

        ulong sender = rpcParams.Receive.SenderClientId;

        if (!PlayerSpawnManager.Instance.TryGetSpawnPoint(sender, out Vector3 position, out Quaternion rotation))
            return;

        TeleportOwnerRpc(position, rotation, RpcTarget.Single(sender, RpcTargetUse.Temp));
    }

    private void ResyncCharacterController()
    {
        if (characterController == null) return;

        characterController.enabled = false;
        characterController.enabled = true;
        Physics.SyncTransforms();
    }

    private void Jump()
    {
        bool isGrounded = characterController.isGrounded;

        if (InputManager.jumpPressed) jumpBufferCounter = jumpBufferTime;
        else jumpBufferCounter -= Time.deltaTime;

        if (isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
            isJumping = false;
        }

        if (jumpBufferCounter > 0f && isGrounded)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpBufferCounter = 0f;
            isJumping = true;
        }


        verticalVelocity += gravity * Time.deltaTime;
    }

    private void UpdateAnimation()
    {
        Vector2 input = InputManager.moveDir;
        bool moving = input.sqrMagnitude > 0.01f;

        animator.SetBool(IsWalkHash, moving);
        animator.SetBool(IsRunHash, isRunning && moving);
        animator.SetBool(IsJumpHash, isJumping);
    }
    private void DetectInteractable()
    {
        currentInteractable = null;
        canInteract = false;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        Debug.DrawRay(ray.origin, ray.direction * interactionDistance, Color.red);

        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance, interactMask, QueryTriggerInteraction.Ignore)
        && hit.collider.CompareTag("Interactable"))
        {
            Debug.Log("감지");
            canInteract = true;
            currentInteractable = hit.collider.gameObject;

        }
    }

    private void TrytoInteract()
    {
        if (canInteract && currentInteractable != null && InputManager.interactPressed)
        {
            if (currentInteractable != null)
            {
                IInteractable interactobj = currentInteractable.GetComponentInParent<IInteractable>();
                if (interactobj != null)
                {
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