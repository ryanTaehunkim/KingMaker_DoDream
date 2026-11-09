using System.Collections.Generic;
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

    [System.Serializable]
    public class DayEntry
    {
        [Tooltip("체크 해제하면 이 일차는 게임에서 완전히 제외됩니다")]
        public bool enabled = true;
        [Tooltip("필드 탐색/미션 시간(초)")]
        public float fieldDuration = 210f;
        [Tooltip("블라인드 드롭 시간(초). 0이면 드롭 없이 다음 일차로 넘어감")]
        public float dropDuration = 90f;
    }

    [Header("일차별 시간 (항목 1개 = 하루, 체크된 항목만 게임에 포함)")]
    [SerializeField]
    private DayEntry[] days =
    {
        new DayEntry { fieldDuration = 210f, dropDuration = 90f },
        new DayEntry { fieldDuration = 210f, dropDuration = 90f },
        new DayEntry { fieldDuration = 240f, dropDuration = 0f  },
        new DayEntry { fieldDuration = 240f, dropDuration = 0f  },
    };

    // 런타임에 만들어지는 단계 목록
    private struct PhaseInfo
    {
        public int phase;
        public int day;
        public float duration;
    }

    private readonly List<PhaseInfo> phaseList = new List<PhaseInfo>();
    private float totalDuration;
    private int activeDayCount;

    public float GameDuration => totalDuration;
    public int TotalDays => activeDayCount;

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

    private void Awake()
    {
        BuildPhaseList();
    }

    // 켜진 일차만 모아 1, 2, 3...으로 번호를 다시 매기고 단계 목록을 만든다
    private void BuildPhaseList()
    {
        phaseList.Clear();
        totalDuration = 0f;
        activeDayCount = 0;

        if (days == null) return;

        // 켜져 있고 필드 시간이 있는 일차만 모은다
        var active = new List<DayEntry>();
        foreach (var d in days)
        {
            if (d != null && d.enabled && d.fieldDuration > 0f)
                active.Add(d);
        }

        activeDayCount = active.Count;

        for (int i = 0; i < active.Count; i++)
        {
            int dayNumber = i + 1;
            bool isLast = i == active.Count - 1;
            DayEntry entry = active[i];

            int fieldPhase = isLast ? PhaseDay4Final : PhaseDay1Field + i * 2;

            phaseList.Add(new PhaseInfo
            {
                phase = fieldPhase,
                day = dayNumber,
                duration = entry.fieldDuration
            });
            totalDuration += entry.fieldDuration;

            // 마지막 일차는 드롭을 만들지 않는다
            if (!isLast && entry.dropDuration > 0f)
            {
                phaseList.Add(new PhaseInfo
                {
                    phase = PhaseDay1Drop + i * 2,
                    day = dayNumber,
                    duration = entry.dropDuration
                });
                totalDuration += entry.dropDuration;
            }
        }
    }

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

        BuildPhaseList(); // 인스펙터 값 변경 반영

        if (phaseList.Count == 0)
        {
            Debug.LogError("[GameTime] 켜진 일차가 없습니다. days에서 일차를 추가하거나 Enabled를 체크해 주세요.");
            return;
        }

        elapsedTime.Value = 0f;
        currentDay.Value = phaseList[0].day;
        currentPhase.Value = phaseList[0].phase;
        remainingTime.Value = phaseList[0].duration;

        timerRunning.Value = true;

        Debug.Log($"[GameTime] 게임 타이머 시작 (총 {activeDayCount}일, {totalDuration}초)");
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

        elapsedTime.Value = Mathf.Min(elapsedTime.Value + Time.deltaTime, totalDuration);
        float elapsed = elapsedTime.Value;

        int index = FindPhaseIndex(elapsed, out float phaseEnd);

        if (index >= 0)
        {
            currentDay.Value = phaseList[index].day;
            currentPhase.Value = phaseList[index].phase;
            remainingTime.Value = Mathf.Max(0f, phaseEnd - elapsed);
        }

        if (elapsed >= totalDuration)
        {
            FinishGameTimer();
            Debug.Log("[GameTime] 전체 시간 종료");
        }
    }

    // elapsed 시점이 속한 단계 인덱스와 그 단계가 끝나는 시각. 범위를 넘으면 -1
    private int FindPhaseIndex(float elapsed, out float endTime)
    {
        float t = 0f;
        for (int i = 0; i < phaseList.Count; i++)
        {
            t += phaseList[i].duration;
            if (elapsed < t)
            {
                endTime = t;
                return i;
            }
        }
        endTime = t;
        return -1;
    }
}