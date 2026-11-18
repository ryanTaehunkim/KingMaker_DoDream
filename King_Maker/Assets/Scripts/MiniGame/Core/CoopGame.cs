using System;
using Unity.Netcode;
using UnityEngine;

// 2인 공유 화면 미니게임(C3~C5, G3, Q 전용) 공통 구조
// - CoopGameLogic: 서버에서만 돌아가는 판정 로직. 입력을 받아 상태를 바꾸고 메시지를 보낸다.
// - CoopMinigameBase: 클라이언트 화면. 입력을 서버로 보내고, 서버 메시지를 받아 그리기만 한다.
// - 둘 사이의 메시지는 CoopMsg 하나로 통일 (kind + 정수 2개 + 실수 2개). 의미는 게임마다 정한다.

public struct CoopMsg : INetworkSerializable
{
    public byte kind;
    public int a;
    public int b;
    public float x;
    public float y;

    public CoopMsg(byte kind, int a = 0, int b = 0, float x = 0f, float y = 0f)
    {
        this.kind = kind;
        this.a = a;
        this.b = b;
        this.x = x;
        this.y = y;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref kind);
        serializer.SerializeValue(ref a);
        serializer.SerializeValue(ref b);
        serializer.SerializeValue(ref x);
        serializer.SerializeValue(ref y);
    }

    public override string ToString() => $"[{kind}] a={a} b={b} x={x:0.##} y={y:0.##}";
}

// 서버 로직이 MissionManager에게 요청할 수 있는 것
public interface ICoopHost
{
    int Day { get; }
    int GetGrade(int role);
    int GetPersonalClearCount(int role);
    void Broadcast(CoopMsg msg);
    void SendTo(int role, CoopMsg msg);
}

// 서버 전용 판정 로직. CoopMissionData.CreateLogic()이 만든다.
public abstract class CoopGameLogic
{
    protected ICoopHost Host { get; private set; }
    protected CoopMissionData Data { get; private set; }
    protected System.Random Rng { get; private set; }
    protected int Players { get; private set; }

    public float Elapsed { get; private set; }
    public bool Ended { get; private set; }
    public MinigameResult Result { get; private set; }

    // 역할별 최종 결과와 등급 변화량 (End 이후 MissionManager가 읽어 반영)
    public MinigameResult[] RoleResults { get; private set; }
    public int[] Deltas { get; private set; }
    // 역할별로 "오늘 클리어"로 기록할지 (G3: 승패와 관계없이 1회)
    public bool[] MarkCleared { get; private set; }

    public void Init(ICoopHost host, CoopMissionData data, int players, int seed)
    {
        Host = host;
        Data = data;
        Players = players;
        Rng = new System.Random(seed);
        RoleResults = new MinigameResult[players];
        Deltas = new int[players];
        MarkCleared = new bool[players];
        OnStart();
    }

    public void Tick(float dt)
    {
        if (Ended) return;
        Elapsed += dt;
        OnTick(dt);
        if (!Ended && Elapsed >= Data.timeLimit) OnTimeout();
    }

    public void Input(int role, CoopMsg msg)
    {
        if (!Ended) OnInput(role, msg);
    }

    protected abstract void OnStart();
    protected abstract void OnInput(int role, CoopMsg msg);
    protected virtual void OnTick(float dt) { }
    protected virtual void OnTimeout() => End(MinigameResult.Fail);

    // 참가자 이탈 등으로 중단
    public void Abort() => End(MinigameResult.Cancelled);

    // 모두 같은 결과. 성공이면 기본 보상(gradeReward)과 클리어 기록.
    protected void End(MinigameResult result)
    {
        if (Ended) return;
        for (int r = 0; r < Players; r++)
        {
            RoleResults[r] = result;
            Deltas[r] = result == MinigameResult.Success ? Data.gradeReward : 0;
            MarkCleared[r] = result == MinigameResult.Success;
        }
        Result = result;
        Ended = true;
    }

    // 역할별로 결과를 직접 정할 때 (G3 승/패, Q 보상 등). RoleResults/Deltas/MarkCleared를 먼저 채운 뒤 호출.
    protected void EndCustom(MinigameResult overall)
    {
        if (Ended) return;
        Result = overall;
        Ended = true;
    }

    protected float RandomRange(float min, float max) => min + (float)Rng.NextDouble() * (max - min);
}

// 클라이언트 화면 부모
public abstract class CoopMinigameBase : MinigameBase
{
    internal static int PendingRole;

    public int Role { get; private set; }
    public bool IsServerEnded { get; private set; }

    protected override bool UseLocalTimeout => false; // 끝은 서버가 알려준다

    protected sealed override void OnBegin()
    {
        Role = PendingRole;
        OnCoopBegin();
    }

    protected abstract void OnCoopBegin();

    // 서버 → 화면
    public abstract void OnServerMessage(CoopMsg msg);

    // 화면 → 서버
    protected void Send(CoopMsg msg) => MissionManager.Instance?.SendCoopInput(msg);

    internal void OnServerEnded(MinigameResult result)
    {
        IsServerEnded = true;
        Finish(result);
    }
}
