
using Unity.Netcode;
using UnityEngine;

public class PlayerMovementLock : NetworkBehaviour
{
    [Header("미니게임 중 비활성화할 컴포넌트")]
    [SerializeField] private Behaviour[] behavioursToDisable;

    private Rigidbody rb;
    private PlayerControlRigid playerControl;

    private bool[] originalEnabledStates;
    private bool originalIsKinematic;
    private bool originalUseGravity;

    public NetworkVariable<bool> IsLocked =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public bool IsGameplayLocked => IsLocked.Value;

    public override void OnNetworkSpawn()
    {
        rb = GetComponent<Rigidbody>();
        playerControl = GetComponent<PlayerControlRigid>();

        if (behavioursToDisable != null)
        {
            originalEnabledStates =
                new bool[behavioursToDisable.Length];

            for (int i = 0; i < behavioursToDisable.Length; i++)
            {
                if (behavioursToDisable[i] != null)
                {
                    originalEnabledStates[i] =
                        behavioursToDisable[i].enabled;
                }
            }
        }

        if (rb != null)
        {
            originalIsKinematic = rb.isKinematic;
            originalUseGravity = rb.useGravity;
        }

        IsLocked.OnValueChanged += OnLockChanged;

        ApplyLock(IsLocked.Value);
    }

    public override void OnNetworkDespawn()
    {
        IsLocked.OnValueChanged -= OnLockChanged;

        // 오브젝트가 사라질 때 원래 물리 설정 복구
        if (rb != null)
        {
            rb.isKinematic = originalIsKinematic;
            rb.useGravity = originalUseGravity;
        }
    }

    // 서버에서만 호출
    public void SetLockedServer(bool locked)
    {
        if (!IsServer)
            return;

        IsLocked.Value = locked;
        ApplyLock(locked);
    }

    private void OnLockChanged(bool previous, bool current)
    {
        ApplyLock(current);
    }

    private void ApplyLock(bool locked)
    {
        // 내 캐릭터의 행동 컴포넌트 제한
        if (IsOwner && behavioursToDisable != null)
        {
            for (int i = 0; i < behavioursToDisable.Length; i++)
            {
                Behaviour behaviour = behavioursToDisable[i];

                if (behaviour == null)
                    continue;

                // PlayerControlRigid는 비활성화하지 않음.
                // 그래야 미니게임 중에도 마우스 시선 이동이 가능함.
                if (behaviour == playerControl)
                    continue;

                if (locked)
                {
                    behaviour.enabled = false;
                }
                else if (originalEnabledStates != null)
                {
                    behaviour.enabled = originalEnabledStates[i];
                }
            }
        }

        if (rb == null)
            return;

        if (locked)
        {
            // Rigidbody 자체를 고정하지 않고 중력만 정지
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            rb.useGravity = false;

            // isKinematic은 변경하지 않음
            // PlayerControlRigid의 카메라 입력은 계속 작동함
        }
        else
        {
            rb.isKinematic = originalIsKinematic;
            rb.useGravity = originalUseGravity;

            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }
}
