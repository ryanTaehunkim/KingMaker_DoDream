
using Unity.Netcode;
using UnityEngine;

public class GameTimeManager : NetworkBehaviour
{
    // 진행 단계
    public const int PhaseWaiting = 0;
    public const int PhaseDay1Field = 1;
    public const int PhaseDay1Drop = 2;
    public const int PhaseDay2Field = 3;
    public const int PhaseDay2Drop = 4;
    public const int PhaseDay3Field = 5;
    public const int PhaseDay4Final = 6;
    public const int PhaseFinished = 7;

    // 총 18분
    public const float GameDuration = 1080f;

    // 현재 일차
    public NetworkVariable<int> currentDay =
        new NetworkVariable<int>(
            1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    // 현재 단계가 끝날 때까지 남은 시간
    public NetworkVariable<float> remainingTime =
        new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    // 게임 시작 이후 경과 시간
    public NetworkVariable<float> elapsedTime =
        new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    // 현재 진행 단계
    public NetworkVariable<int> currentPhase =
        new NetworkVariable<int>(
            PhaseWaiting,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    // 타이머 실행 상태
    public NetworkVariable<bool> timerRunning =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;

        currentDay.Value = 1;
        remainingTime.Value = 0f;
        elapsedTime.Value = 0f;
        currentPhase.Value = PhaseWaiting;
        timerRunning.Value = false;
    }

    // GameFlowManager에서 게임이 시작될 때 호출
    public void StartGameTimer()
    {
        if (!IsServer || !IsSpawned)
            return;

        if (timerRunning.Value)
            return;

        elapsedTime.Value = 0f;
        currentDay.Value = 1;
        currentPhase.Value = PhaseDay1Field;
        remainingTime.Value = 210f;

        timerRunning.Value = true;

        Debug.Log("[GameTime] 1일차 필드 시작");
    }

    // 게임 결과가 결정되었을 때 호출
    public void FinishGameTimer()
    {
        if (!IsServer)
            return;

        timerRunning.Value = false;
        currentPhase.Value = PhaseFinished;
        remainingTime.Value = 0f;
    }

    private void Update()
    {
        if (!IsServer || !timerRunning.Value)
            return;

        elapsedTime.Value = Mathf.Min(
            elapsedTime.Value + Time.deltaTime,
            GameDuration
        );

        float elapsed = elapsedTime.Value;

        currentDay.Value = GetDay(elapsed);
        currentPhase.Value = GetPhase(elapsed);

        float phaseEnd = GetPhaseEndTime(
            currentPhase.Value
        );

        remainingTime.Value = Mathf.Max(
            0f,
            phaseEnd - elapsed
        );

        if (elapsed >= GameDuration)
        {
            FinishGameTimer();
            Debug.Log("[GameTime] 18분 경과");
        }
    }

    private int GetDay(float elapsed)
    {
        if (elapsed < 300f)
            return 1;

        if (elapsed < 600f)
            return 2;

        if (elapsed < 840f)
            return 3;

        return 4;
    }

    private int GetPhase(float elapsed)
    {
        if (elapsed < 210f)
            return PhaseDay1Field;

        if (elapsed < 300f)
            return PhaseDay1Drop;

        if (elapsed < 510f)
            return PhaseDay2Field;

        if (elapsed < 600f)
            return PhaseDay2Drop;

        if (elapsed < 840f)
            return PhaseDay3Field;

        if (elapsed < GameDuration)
            return PhaseDay4Final;

        return PhaseFinished;
    }

    private float GetPhaseEndTime(int phase)
    {
        switch (phase)
        {
            case PhaseDay1Field:
                return 210f;

            case PhaseDay1Drop:
                return 300f;

            case PhaseDay2Field:
                return 510f;

            case PhaseDay2Drop:
                return 600f;

            case PhaseDay3Field:
                return 840f;

            case PhaseDay4Final:
                return GameDuration;

            default:
                return elapsedTime.Value;
        }
    }
}
