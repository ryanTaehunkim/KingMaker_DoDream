using Unity.Netcode;
using UnityEngine;

// 1인 도박 미션(G1 동전, G2 하이-로우) — 서버 권한 판정
// 흐름: 시작 허가 → 서버가 준비값(G2 첫 카드)과 최대 배팅을 보냄 → 플레이어가 배팅/선택 →
//       서버가 확률(기본 50% + 개인 미션 클리어 수 × 1%)로 승패를 정하고 등급 반영 → 결과 연출 후 창 닫힘
// 난수와 등급 변경은 서버에서만 일어난다. 클라이언트는 연출만 한다.
public partial class MissionManager
{
    // =====================================================================
    // 클라이언트 API
    // =====================================================================

    // GambleMinigameBase가 배팅을 확정할 때 호출
    public void SubmitGamble(int bet, int choice)
    {
        if (activeStation == null) return;
        int stationId = activeStation.StationId;
        if (IsOnline) GambleBetRpc(stationId, bet, choice);
        else ServerResolveGamble(OfflineClientId, stationId, bet, choice);
    }

    private static GambleMinigameBase CurrentGamble =>
        MinigameController.Instance != null ? MinigameController.Instance.Current as GambleMinigameBase : null;

    private void OnGambleSetup(int stationId, int setup, int maxBet) => CurrentGamble?.OnGambleSetup(setup, maxBet);

    private void OnGambleResult(int stationId, bool win, int outcome, int delta) => CurrentGamble?.OnGambleResult(win, outcome, delta);

    // =====================================================================
    // RPC
    // =====================================================================

    [Rpc(SendTo.SpecifiedInParams)]
    private void GambleSetupRpc(int stationId, int setup, int maxBet, RpcParams rpcParams) => OnGambleSetup(stationId, setup, maxBet);

    [Rpc(SendTo.Server)]
    private void GambleBetRpc(int stationId, int bet, int choice, RpcParams rpcParams = default) =>
        ServerResolveGamble(rpcParams.Receive.SenderClientId, stationId, bet, choice);

    [Rpc(SendTo.SpecifiedInParams)]
    private void GambleResultRpc(int stationId, bool win, int outcome, int delta, RpcParams rpcParams) => OnGambleResult(stationId, win, outcome, delta);

    // =====================================================================
    // 서버
    // =====================================================================

    private int ServerMaxBet(ulong clientId, GambleConfig config)
    {
        if (config == null) return 1;
        return config.MaxBet(CurrentDay, ServerGetGrade(clientId));
    }

    private void ServerPrepareGamble(Session session, GambleMissionData mission)
    {
        session.gambleMaxBet = ServerMaxBet(session.clientId, mission.config);
        // G2: 첫 카드는 2~Q(12) — 어느 쪽을 골라도 이길 수 있는 카드만
        session.gambleSetup = mission.kind == GambleKind.HighLow ? Random.Range(2, 13) : 0;
        session.gambleResolved = false;
    }

    private void ServerSendGambleSetup(ulong clientId, int stationId)
    {
        if (!sessionsByClient.TryGetValue(clientId, out var session) || session.station.StationId != stationId) return;
        if (!(session.station.Mission is GambleMissionData)) return;

        if (IsOnline) GambleSetupRpc(stationId, session.gambleSetup, session.gambleMaxBet, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        else OnGambleSetup(stationId, session.gambleSetup, session.gambleMaxBet);
    }

    private void ServerResolveGamble(ulong clientId, int stationId, int bet, int choice)
    {
        if (!sessionsByClient.TryGetValue(clientId, out var session) || session.station.StationId != stationId) return;
        if (!(session.station.Mission is GambleMissionData mission) || session.gambleResolved) return;
        if (session.gambleMaxBet < 1) return;

        bet = Mathf.Clamp(bet, 1, session.gambleMaxBet);
        choice = Mathf.Clamp(choice, 0, 1);

        float chance = mission.config != null ? mission.config.WinChance(ServerPersonalClearCount(clientId)) : 0.5f;
        bool win = Random.value < chance;

        int outcome;
        if (mission.kind == GambleKind.Coin)
        {
            outcome = win ? choice : 1 - choice;
        }
        else
        {
            int first = session.gambleSetup;
            bool high = choice == 1;
            // 이기면 고른 방향의 카드, 지면 반대 방향 또는 같은 카드
            if (win) outcome = high ? Random.Range(first + 1, 14) : Random.Range(1, first);
            else outcome = high ? Random.Range(1, first + 1) : Random.Range(first, 14);
        }

        int delta = win ? bet : -bet;
        ServerApplyGradeDelta(clientId, delta, mission);

        session.gambleResolved = true;
        session.gambleWin = win;
        session.gambleDelta = delta;
        Debug.Log($"[MissionManager] 도박 {mission.displayName}: client {clientId}, 배팅 {bet}, 선택 {choice}, 확률 {chance:P0}, 결과 {outcome} → {(win ? "승" : "패")} ({delta:+0;-0})");

        if (IsOnline) GambleResultRpc(stationId, win, outcome, delta, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        else OnGambleResult(stationId, win, outcome, delta);
    }

    // ServerHandleResult에서 호출 (세션은 이미 종료됨)
    private void ServerFinishGamble(ulong clientId, Session session, GambleMissionData mission,
        MinigameResult reported, float clientElapsed, float serverElapsed)
    {
        MinigameResult final;
        int delta = 0;
        if (session.gambleResolved)
        {
            final = session.gambleWin ? MinigameResult.Success : MinigameResult.Fail;
            delta = session.gambleDelta;
            if (mission.config == null || mission.config.oncePerDay) MarkCleared(clientId, mission.missionId);
            else retryAvailableAt[(clientId, mission.missionId)] = Now + mission.failRetryCooldown;
        }
        else
        {
            // 배팅 전에 닫음/시간 초과 → 등급 변화 없음
            final = reported == MinigameResult.Cancelled ? MinigameResult.Cancelled : MinigameResult.Fail;
            if (final == MinigameResult.Fail) retryAvailableAt[(clientId, mission.missionId)] = Now + mission.failRetryCooldown;
        }

        if (writeCsvLog) MissionLogger.Append(CurrentDay, clientId, mission, reported, final, clientElapsed, serverElapsed);
        MissionEvents.RaiseMissionFinished(clientId, session.station, final, serverElapsed);

        int stationId = session.station.StationId;
        if (IsOnline) ResultConfirmedRpc(stationId, final, delta, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        else OnResultConfirmed(stationId, final, delta);
    }
}
