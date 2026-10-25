using Unity.Netcode;
using UnityEngine;

// 플레이어의 이동/점프/공격/상호작용을 잠그는 "상태값"만 관리한다.
// 실제로 멈추는 동작은 PlayerControlRigid가 IsLocked 값을 읽어서 처리한다.
// (고개 돌리기는 잠금 중에도 PlayerControlRigid.HandleLook이 계속 동작)
public class PlayerMovementLock : NetworkBehaviour
{
    public NetworkVariable<bool> IsLocked =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public bool IsGameplayLocked => IsLocked.Value;

    // 서버에서만 호출 (GameFlowManager 등)
    public void SetLockedServer(bool locked)
    {
        if (!IsServer)
            return;

        IsLocked.Value = locked;
    }
}