using Unity.Netcode;
using UnityEngine;

public class GameTimeManager : NetworkBehaviour
{
    private const float DAY_DURATION = 300f; // 5분

    public NetworkVariable<int> currentDay =
        new NetworkVariable<int>(
            1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public NetworkVariable<float> remainingTime =
        new NetworkVariable<float>(
            DAY_DURATION,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            currentDay.Value = 1;
            remainingTime.Value = DAY_DURATION;
        }
    }

    private void Update()
    {
        if (!IsServer)
            return;

        remainingTime.Value -= Time.deltaTime;

        if (remainingTime.Value <= 0f)
        {
            remainingTime.Value = 0f;

            if (currentDay.Value < 4)
            {
                currentDay.Value++;
                remainingTime.Value = DAY_DURATION;
            }
            else
            {
                // DAY 4 종료
                // 여기서 게임 결과 처리 연결
            }
        }
    }
}