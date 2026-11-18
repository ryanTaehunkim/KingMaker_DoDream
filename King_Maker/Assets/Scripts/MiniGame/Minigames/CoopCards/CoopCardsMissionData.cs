using UnityEngine;

// C3 카드 짝 맞추기(고급): 두 명이 같은 카드판을 보고 함께 뒤집는다. 뒤집기 상태는 서버가 관리.
[CreateAssetMenu(menuName = "KingMaker/Mission/Coop Cards (C3)", fileName = "Mission_CoopCards")]
public class CoopCardsMissionData : CoopMissionData
{
    [Header("C3 카드 짝 맞추기(고급)")]
    [Range(2, 8)] public int pairs = 5;
    [Tooltip("틀린 두 장을 다시 덮기까지 시간(초)")]
    public float mismatchShowTime = 0.6f;

    public override CoopGameLogic CreateLogic() => new CoopCardsLogic();
}

// 메시지
//  서버→화면: 1 Layout(a=장수)  2 Reveal(a=번호, b=그림)  3 Match(a, b)  4 Hide(a, b)
//  화면→서버: 1 Flip(a=번호)
public class CoopCardsLogic : CoopGameLogic
{
    public const byte Layout = 1, Reveal = 2, Match = 3, Hide = 4;
    public const byte Flip = 1;

    private int[] kinds;
    private bool[] matched;
    private int first = -1, second = -1;
    private float hideTimer;
    private int matchedPairs;
    private int pairs;

    protected override void OnStart()
    {
        var d = Data as CoopCardsMissionData;
        pairs = d != null ? d.pairs : 5;
        kinds = new int[pairs * 2];
        for (int i = 0; i < kinds.Length; i++) kinds[i] = i / 2;
        for (int i = kinds.Length - 1; i > 0; i--)
        {
            int j = Rng.Next(i + 1);
            (kinds[i], kinds[j]) = (kinds[j], kinds[i]);
        }
        matched = new bool[kinds.Length];
        Host.Broadcast(new CoopMsg(Layout, kinds.Length));
    }

    protected override void OnInput(int role, CoopMsg msg)
    {
        if (msg.kind != Flip) return;
        int i = msg.a;
        if (hideTimer > 0f || i < 0 || i >= kinds.Length || matched[i] || i == first) return;

        Host.Broadcast(new CoopMsg(Reveal, i, kinds[i]));
        if (first < 0)
        {
            first = i;
            return;
        }

        second = i;
        if (kinds[first] == kinds[second])
        {
            matched[first] = matched[second] = true;
            Host.Broadcast(new CoopMsg(Match, first, second));
            first = second = -1;
            if (++matchedPairs >= pairs) End(MinigameResult.Success);
        }
        else
        {
            var d = Data as CoopCardsMissionData;
            hideTimer = d != null ? d.mismatchShowTime : 0.6f;
        }
    }

    protected override void OnTick(float dt)
    {
        if (hideTimer <= 0f) return;
        hideTimer -= dt;
        if (hideTimer > 0f) return;
        Host.Broadcast(new CoopMsg(Hide, first, second));
        first = second = -1;
    }

    // 테스트용
    public int KindOf(int index) => kinds[index];
}
