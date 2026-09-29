using System;
using UnityEngine;

// 미션 파트가 다른 시스템(등급, 힌트, HUD, 사운드)에 알리는 이벤트 모음.
// [서버] 이벤트는 서버(호스트)에서만, [로컬] 이벤트는 해당 클라이언트에서만 발생한다.
public static class MissionEvents
{
    // [서버] 미션 시작 허가됨 (clientId, station) — 위치 공개 페널티 등 HUD 연동용
    public static event Action<ulong, MissionStation> MissionStarted;

    // [서버] 미션 종료 (clientId, station, 최종 결과, 서버 측정 소요시간)
    public static event Action<ulong, MissionStation, MinigameResult, float> MissionFinished;

    // [서버] 등급 변화 요청 (clientId, 변화량, 메인 문양 배율 적용 대상 여부, 미션)
    // 실제 반올림·상한·문양 배율 계산은 등급 시스템이 담당한다.
    public static event Action<ulong, int, bool, MissionData> GradeChangeRequested;

    // [서버] 해당 일차 개인 미션 전부 클리어 (clientId, day) → 힌트 시스템
    public static event Action<ulong, int> AllPersonalMissionsCleared;

    // [서버] 해당 일차 공용 미션 전부 클리어 (day) → 등급 분포 공개
    public static event Action<int> AllSharedMissionsCleared;

    // [서버+로컬] 일차 시작/종료 (day)
    public static event Action<int> DayStarted;
    public static event Action<int> DayEnded;

    // [로컬] 내 미니게임 화면이 열림/닫힘
    public static event Action<MissionData> LocalMinigameOpened;
    public static event Action<MissionData, MinigameResult> LocalMinigameClosed;

    // [로컬] 서버가 확정한 내 미션 결과 (미션, 최종 결과, 등급 변화량)
    public static event Action<MissionData, MinigameResult, int> LocalResultConfirmed;

    // [로컬] 내가 오늘 개인 미션을 전부 클리어함 (day)
    public static event Action<int> LocalAllPersonalMissionsCleared;

    internal static void RaiseMissionStarted(ulong clientId, MissionStation station) => MissionStarted?.Invoke(clientId, station);
    internal static void RaiseMissionFinished(ulong clientId, MissionStation station, MinigameResult result, float elapsed) => MissionFinished?.Invoke(clientId, station, result, elapsed);
    internal static void RaiseGradeChangeRequested(ulong clientId, int amount, bool suitEligible, MissionData data) => GradeChangeRequested?.Invoke(clientId, amount, suitEligible, data);
    internal static void RaiseAllPersonalMissionsCleared(ulong clientId, int day) => AllPersonalMissionsCleared?.Invoke(clientId, day);
    internal static void RaiseAllSharedMissionsCleared(int day) => AllSharedMissionsCleared?.Invoke(day);
    internal static void RaiseDayStarted(int day) => DayStarted?.Invoke(day);
    internal static void RaiseDayEnded(int day) => DayEnded?.Invoke(day);
    internal static void RaiseLocalMinigameOpened(MissionData data) => LocalMinigameOpened?.Invoke(data);
    internal static void RaiseLocalMinigameClosed(MissionData data, MinigameResult result) => LocalMinigameClosed?.Invoke(data, result);
    internal static void RaiseLocalResultConfirmed(MissionData data, MinigameResult result, int gradeDelta) => LocalResultConfirmed?.Invoke(data, result, gradeDelta);
    internal static void RaiseLocalAllPersonalMissionsCleared(int day) => LocalAllPersonalMissionsCleared?.Invoke(day);

    // Enter Play Mode에서 도메인 리로드를 끈 경우에도 이전 구독이 남지 않도록 초기화
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        MissionStarted = null;
        MissionFinished = null;
        GradeChangeRequested = null;
        AllPersonalMissionsCleared = null;
        AllSharedMissionsCleared = null;
        DayStarted = null;
        DayEnded = null;
        LocalMinigameOpened = null;
        LocalMinigameClosed = null;
        LocalResultConfirmed = null;
        LocalAllPersonalMissionsCleared = null;
    }
}
