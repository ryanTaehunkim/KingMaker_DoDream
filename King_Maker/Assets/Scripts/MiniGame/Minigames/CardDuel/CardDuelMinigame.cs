using TMPro;
using UnityEngine;
using UnityEngine.UI;

// G3 화면: 배팅 → 상대도 배팅하면 두 카드 공개
public class CardDuelMinigame : CoopMinigameBase
{
    private BetPanel bet;
    private Image myCard, otherCard;
    private TextMeshProUGUI myText, otherText, info;
    private bool myLocked, otherLocked, revealed;

    public override string StatusText => revealed ? "" : bet == null ? "준비 중..." :
        $"나: {(myLocked ? "배팅 완료" : "배팅 중")}   상대: {(otherLocked ? "배팅 완료" : "배팅 중")}";

    protected override void OnCoopBegin()
    {
        MinigameUI.Text(Board, "Me", "나", 26f, MinigameUI.Gold, new Vector2(-150f, 190f), new Vector2(200f, 36f));
        MinigameUI.Text(Board, "Other", "상대", 26f, MinigameUI.TextColor, new Vector2(150f, 190f), new Vector2(200f, 36f));
        myCard = MinigameUI.Image(Board, "MyCard", new Color(0.45f, 0.15f, 0.2f), new Vector2(-150f, 85f), new Vector2(120f, 170f));
        otherCard = MinigameUI.Image(Board, "OtherCard", new Color(0.45f, 0.15f, 0.2f), new Vector2(150f, 85f), new Vector2(120f, 170f));
        myText = MinigameUI.Text(myCard.transform, "V", "?", 60f, Color.white, Vector2.zero, new Vector2(120f, 170f));
        otherText = MinigameUI.Text(otherCard.transform, "V", "?", 60f, Color.white, Vector2.zero, new Vector2(120f, 170f));
        MinigameUI.Text(Board, "Vs", "VS", 40f, MinigameUI.Bad, new Vector2(0f, 85f), new Vector2(100f, 60f));
        info = MinigameUI.Text(Board, "Info", "두 사람 중 낮은 배팅액으로 대결합니다", 24f, new Color(1f, 1f, 1f, 0.7f), new Vector2(0f, -215f), new Vector2(800f, 36f));
    }

    public override void OnServerMessage(CoopMsg msg)
    {
        switch (msg.kind)
        {
            case CardDuelLogic.Setup:
                if (bet != null) break;
                bet = BetPanel.Create(Board, new Vector2(0f, -95f), msg.a, null, "배팅",
                    (amount, _) => Send(new CoopMsg(CardDuelLogic.Bet, amount)));
                break;
            case CardDuelLogic.BetLocked:
                if (msg.a == Role) myLocked = true;
                else otherLocked = true;
                break;
            case CardDuelLogic.Reveal:
                revealed = true;
                bet?.Root.gameObject.SetActive(false);
                int mine = Role == 0 ? msg.a : msg.b;
                int theirs = Role == 0 ? msg.b : msg.a;
                ShowCard(myCard, myText, mine);
                ShowCard(otherCard, otherText, theirs);
                bool won = Mathf.RoundToInt(msg.x) == Role;
                int amount = Mathf.RoundToInt(msg.y);
                info.text = won ? $"승리! +{amount} PT" : $"패배... -{amount} PT";
                info.color = won ? MinigameUI.Good : MinigameUI.Bad;
                info.fontSize = 36f;
                break;
        }
    }

    private static void ShowCard(Image card, TextMeshProUGUI text, int value)
    {
        card.color = Color.white;
        text.color = new Color(0.15f, 0.15f, 0.2f);
        text.text = HighLowMinigame.CardName(value);
    }

    // 테스트용
    public void DebugBet(int amount)
    {
        if (bet == null) return;
        bet.SetBet(amount);
        bet.Submit();
    }
}
