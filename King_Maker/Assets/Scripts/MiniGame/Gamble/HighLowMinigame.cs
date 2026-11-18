using TMPro;
using UnityEngine;
using UnityEngine.UI;

// G2 하이-로우: 첫 카드(A~K)를 보고 다음 카드가 더 높을지/낮을지 배팅. 같은 숫자는 패배.
public class HighLowMinigame : GambleMinigameBase
{
    private TextMeshProUGUI firstText, nextText;
    private Image nextCard;

    protected override string[] Choices => new[] { "LOW ▼", "HIGH ▲" }; // 0 = Low, 1 = High

    public static string CardName(int v)
    {
        switch (v)
        {
            case 1: return "A";
            case 11: return "J";
            case 12: return "Q";
            case 13: return "K";
            default: return v.ToString();
        }
    }

    protected override void BuildBoard()
    {
        var first = MinigameUI.Image(Board, "First", Color.white, new Vector2(-110f, 125f), new Vector2(120f, 170f));
        firstText = MinigameUI.Text(first.transform, "Value", "?", 60f, new Color(0.15f, 0.15f, 0.2f), Vector2.zero, new Vector2(120f, 170f));
        nextCard = MinigameUI.Image(Board, "Next", new Color(0.45f, 0.15f, 0.2f), new Vector2(110f, 125f), new Vector2(120f, 170f));
        nextText = MinigameUI.Text(nextCard.transform, "Value", "?", 60f, Color.white, Vector2.zero, new Vector2(120f, 170f));
        MinigameUI.Text(Board, "Vs", "→", 48f, MinigameUI.TextColor, new Vector2(0f, 125f), new Vector2(80f, 80f));
    }

    protected override void OnSetup(int setup)
    {
        firstText.text = CardName(setup);
        info.text = "다음 카드가 더 높을까요, 낮을까요? (같으면 패배)";
    }

    protected override void OnReveal(bool win, int outcome, int delta, float t)
    {
        if (t < 0.5f)
        {
            nextCard.rectTransform.localScale = new Vector3(Mathf.Abs(Mathf.Cos(t * 12f)), 1f, 1f);
            return;
        }
        nextCard.rectTransform.localScale = Vector3.one;
        nextCard.color = Color.white;
        nextText.color = new Color(0.15f, 0.15f, 0.2f);
        nextText.text = CardName(outcome);
        info.text = win ? $"적중! +{delta} PT" : $"빗나감... {delta} PT";
        info.color = win ? MinigameUI.Good : MinigameUI.Bad;
    }
}
