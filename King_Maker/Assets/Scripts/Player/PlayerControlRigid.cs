using Unity.Netcode;
using UnityEngine;
using Unity.Cinemachine;

public class PlayerControlRigid : NetworkBehaviour
{


    [Header("Move Settings")]
    [SerializeField] private float walkSpeed = 4.0f;
    [SerializeField] private float sprintSpeed = 7.0f;

    [Header("Jump / Gravity")]
    [SerializeField] private float jumpHeight = 1.2f; //점프 높이
    [SerializeField] private float gravity = -9.8f; //중력
    [SerializeField] private float jumpCooldown = 0.1f;

    [Header("Ground Check")]
    [SerializeField] private Transform[] groundCheckPoints;
    [SerializeField] private float groundRayStartUp = 0.2f;
    [SerializeField] private float groundRayLength = 0.25f;
    [SerializeField] private LayerMask groundMask;


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
    private GameObject holdItem;

    private Rigidbody rb;
    private Collider feetCollider;
    private Vector3 horizontalVelocity;
    private float verticalVelocity;
    private float speed;
    private float pitch;
    //점프
    private bool isGrounded;
    private Vector3 groundNormal = Vector3.up;
    private bool jumpRequested;
    private float lastJumpTime = -1f;
    private bool Grounded() => isGrounded;
    //공격
    [Header("Knockback")]
    private PlayerCombat combat;
    [SerializeField] private float knockbackDecay = 8f; //클수록 빨리 멈춤
    private Vector3 knockbackVelocity;

    //뛰는 중인지 서버에 보내기 위한 변수
    private PlayerStateList playerState;
    private bool isRunning;



    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        virtualCamera = FindFirstObjectByType<CinemachineCamera>();
        playerState = GetComponent<PlayerStateList>();
        combat = GetComponent<PlayerCombat>();

        GameObject camearObj = GameObject.FindGameObjectWithTag("MainCamera");
        if (camearObj != null)
        {
            playerCamera = camearObj.GetComponent<Camera>();
        }
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            return;
        }
        if (virtualCamera != null)
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
        InputMove();
        InputJump();
        DetectInteractable();
        TrytoInteract();
        PutDownObject();
        combat.TryAttack();
      
    }
    private void FixedUpdate()
    {
        if (!IsOwner) return;

        CheckGround();
        Move();
        Jump();
        ApplyGravity();
    }

    private void ApplyGravity()
    {
        rb.AddForce(Vector3.up * gravity, ForceMode.Acceleration);
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

    private void InputMove()
    {
        Vector2 input = InputManager.moveDir;
        horizontalVelocity = (transform.right * input.x + transform.forward * input.y);
        horizontalVelocity = Vector3.ClampMagnitude(horizontalVelocity, 1f);
    }
    private void Move()
    {

        Vector3 v = rb.linearVelocity;
        
        Vector3 target = horizontalVelocity * speed + knockbackVelocity;
        rb.linearVelocity = new Vector3(target.x, v.y, target.z);

        knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, knockbackDecay * Time.fixedDeltaTime);
        if (knockbackVelocity.sqrMagnitude < 0.01f) knockbackVelocity = Vector3.zero;


    }

 

    private void UpdateRunState()
    {
        bool newRunningState = InputManager.sprintHeld && playerState.stamina.Value > 5f;


        if (isRunning != newRunningState)
        {
            isRunning = newRunningState;
            SetRunningServerRpc(isRunning);
        }
        if (isRunning)
        {
            speed = sprintSpeed;
        }
        else
        {
            speed = walkSpeed;
        }
    }

    [ServerRpc]
    private void SetRunningServerRpc(bool running)
    {
        playerState.SetRunning(running);

    }


    private void InputJump()
    {
        if (InputManager.jumpPressed)
            jumpRequested = true;
    }

    private void CheckGround()
    {
        if (Time.time - lastJumpTime < jumpCooldown){
            isGrounded = false;
            return;
        }


        isGrounded = false;
        groundNormal = Vector3.up;

        foreach (Transform p in groundCheckPoints)
        {
            Vector3 origin = p.position + Vector3.up * groundRayStartUp;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, groundRayLength,
                groundMask, QueryTriggerInteraction.Ignore))
            {
                isGrounded = true;
                groundNormal = hit.normal;
                return;
            }
        }


    }
    private void Jump()
    {
        if (!jumpRequested) return;
        jumpRequested = false;

        if (!isGrounded) return;

        float jumpVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        Vector3 v = rb.linearVelocity;
        rb.linearVelocity = new Vector3(v.x, jumpVelocity, v.z);

        lastJumpTime = Time.time;
        isGrounded = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheckPoints == null) return;
        Gizmos.color = Color.green;
        foreach (Transform p in groundCheckPoints)
        {
            if (p == null) continue;
            Vector3 origin = p.position + Vector3.up * groundRayStartUp;
            Gizmos.DrawLine(origin, origin + Vector3.down * groundRayLength);
            Gizmos.DrawWireSphere(origin, 0.02f);
        }
    }

    private void DetectInteractable()
    {
        currentInteractable = null;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        Debug.DrawRay(ray.origin, ray.direction * interactionDistance, Color.red);

        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance))
        {

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
        else
        {
            canInteract = false;
            currentInteractable = null;
        }
    }

    private void TrytoInteract()
    {
        if (canInteract && currentInteractable != null && InputManager.interactPressed)
        {
            if (currentInteractable != null)
            {
                IInteractable interactobj = currentInteractable.GetComponent<IInteractable>();
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
    public void ApplyKnockback(Vector3 horizontal, float upward)
    {
        if (!IsOwner) return;
        horizontal.y = 0f;
        knockbackVelocity = horizontal;

        Vector3 v = rb.linearVelocity;
        rb.linearVelocity = new Vector3(v.x, upward, v.z);

        lastJumpTime = Time.time;
        isGrounded = false;
    }

}
