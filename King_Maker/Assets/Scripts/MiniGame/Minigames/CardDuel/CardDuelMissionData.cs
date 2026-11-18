using UnityEngine;

// G3 1대1 카드 대결: 두 명이 배팅하고(낮은 쪽 금액으로 통일) 각자 카드 1장. 높은 카드가 이긴다.
// 승률은 기본 50%에 두 사람의 개인 미션 클리어 수 차이 × 1%를 반영. 결과는 서버에서만 정한다.
[CreateAssetMenu(menuName = "KingMaker/Mission/Card Duel (G3)", fileName = "Mission_CardDuel")]
public class CardDuelMissionData : CoopMissionData
{
    [Header("G3 카드 대결")]
    public GambleConfig config;
    [Tooltip("카드 공개 후 창이 닫히기까지(초)")]
    public float revealTime = 2f;

    public override CoopGameLogic CreateLogic() => new CardDuelLogic();

    // 배팅할 등급이 있어야 참가 가능
    public override bool CanTakeRole(int role, int grade) =>
        grade < 0 || config == null || grade - config.minGradeAfterLoss >= 1;
}

// 메시지
//  서버→화면: 1 Setup(a=내 최대 배팅)  2 BetLocked(a=역할, b=금액)  3 Reveal(a=0번 카드, b=1번 카드, x=승자 역할, y=배팅액)
//  화면→서버: 1 Bet(a=금액)
public class CardDuelLogic : CoopGameLogic
{
    public const byte Setup = 1, BetLocked = 2, Reveal = 3;
    public const byte Bet = 1;

    private CardDuelMissionData d;
    private int[] maxBet;
    private int[] bets;
    private bool revealed;
    private float revealTimer;
    private int winner, finalBet;

    protected override void OnStart()
    {
        d = Data as CardDuelMissionData;
        maxBet = new int[Players];
        bets = new int[Players];
        for (int r = 0; r < Players; r++)
        {
            maxBet[r] = d != null && d.config != null ? d.config.MaxBet(Host.Day, Host.GetGrade(r)) : 2;
            Host.SendTo(r, new CoopMsg(Setup, maxBet[r]));
        }
    }

    protected override void OnInput(int role, CoopMsg msg)
    {
        if (msg.kind != Bet || revealed || bets[role] > 0 || maxBet[role] < 1) return;
        bets[role] = Mathf.Clamp(msg.a, 1, maxBet[role]);
        Host.Broadcast(new CoopMsg(BetLocked, role, bets[role]));

        for (int r = 0; r < Players; r++) if (bets[r] <= 0) return;

        finalBet = Mathf.Min(bets[0], bets[1]);
        var cfg = d != null ? d.config : null;
        float c0 = cfg != null ? cfg.WinChance(Host.GetPersonalClearCount(0)) : 0.5f;
        float c1 = cfg != null ? cfg.WinChance(Host.GetPersonalClearCount(1)) : 0.5f;
        float p0 = Mathf.Clamp(0.5f + (c0 - c1), 0.05f, 0.95f);
        winner = Rng.NextDouble() < p0 ? 0 : 1;

        int loserCard = Rng.Next(1, 13);          // A~Q
        int winnerCard = Rng.Next(loserCard + 1, 14);
        int card0 = winner == 0 ? winnerCard : loserCard;
        int card1 = winner == 1 ? winnerCard : loserCard;

        revealed = true;
        revealTimer = d != null ? d.revealTime : 2f;
        Host.Broadcast(new CoopMsg(Reveal, card0, card1, winner, finalBet));
    }

    protected override void OnTick(float dt)
    {
        if (!revealed) return;
        revealTimer -= dt;
        if (revealTimer > 0f) return;

        bool once = d == null || d.config == null || d.config.oncePerDay;
        for (int r = 0; r < Players; r++)
        {
            bool won = r == winner;
            RoleResults[r] = won ? MinigameResult.Success : MinigameResult.Fail;
            Deltas[r] = won ? finalBet : -finalBet;
            MarkCleared[r] = once;
        }
        EndCustom(MinigameResult.Success);
    }

    protected override void OnTimeout()
    {
        if (revealed) return; // 공개 연출 중이면 기다린다
        End(MinigameResult.Fail);
    }
}
