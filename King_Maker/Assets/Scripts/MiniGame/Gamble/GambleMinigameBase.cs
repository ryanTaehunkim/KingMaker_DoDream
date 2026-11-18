using TMPro;
using UnityEngine;

// 1인 도박 미니게임(G1, G2) 공통 흐름
// 서버 준비값 수신 → BetPanel로 배팅/선택 → 서버 결과 수신 → 연출(revealTime) → 종료
public abstract class GambleMinigameBase : MinigameBase
{
    protected BetPanel bet;
    protected TextMeshProUGUI info;
    protected int setupValue;
    protected int maxBet;

    private bool waitingResult;
    private bool resultReceived;
    private bool win;
    private float revealTimer;

    // 배팅을 보낸 뒤에는 결과가 올 때까지 / 연출 중에는 시간 초과로 끝내지 않는다
    protected override bool UseLocalTimeout => !waitingResult && !resultReceived;

    protected abstract string[] Choices { get; }
    protected abstract void BuildBoard();
    protected abstract void OnSetup(int setup);
    protected abstract void OnReveal(bool win, int outcome, int delta, float t01);

    public bool IsReady => bet != null;

    public override string StatusText => resultReceived ? "" : (bet == null ? "준비 중..." : waitingResult ? "결과 확인 중..." : $"최대 {maxBet} PT 배팅 가능");

    protected override void OnBegin()
    {
        BuildBoard();
        info = MinigameUI.Text(Board, "Info", "", 30f, MinigameUI.TextColor, new Vector2(0f, -215f), new Vector2(800f, 44f));
    }

    public void OnGambleSetup(int setup, int max)
    {
        if (IsFinished || bet != null) return;
        setupValue = setup;
        maxBet = max;
        OnSetup(setup);
        bet = BetPanel.Create(Board, new Vector2(0f, -88f), maxBet, Choices, "배팅", (b, c) =>
        {
            waitingResult = true;
            MissionManager.Instance?.SubmitGamble(b, c);
        });
    }

    public void OnGambleResult(bool won, int outcome, int delta)
    {
        if (IsFinished || resultReceived) return;
        waitingResult = false;
        resultReceived = true;
        win = won;
        revealTimer = 0f;
        float duration = Reveal;
        lastOutcome = outcome;
        lastDelta = delta;
        bet?.Root.gameObject.SetActive(false);
        OnReveal(won, outcome, delta, 0f);
        if (duration <= 0f) Finish(won ? MinigameResult.Success : MinigameResult.Fail);
    }

    private int lastOutcome, lastDelta;
    private float Reveal => DataAs<GambleMissionData>() != null ? DataAs<GambleMissionData>().revealTime : 1.5f;

    protected override void OnTick(float dt)
    {
        if (!resultReceived) return;
        revealTimer += dt;
        float t = Mathf.Clamp01(revealTimer / Mathf.Max(0.01f, Reveal));
        OnReveal(win, lastOutcome, lastDelta, t);
        if (t >= 1f) Finish(win ? MinigameResult.Success : MinigameResult.Fail);
    }

    // 테스트/디버그용
    public void DebugBet(int amount, int choice)
    {
        if (bet == null) return;
        bet.SetBet(amount);
        if (choice >= 0) bet.Choose(choice);
        bet.Submit();
    }
}
