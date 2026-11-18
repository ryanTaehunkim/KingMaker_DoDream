using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// C3 화면: 카드를 클릭하면 서버에 뒤집기 요청. 상대가 뒤집은 카드도 똑같이 보인다.
public class CoopCardsMinigame : CoopMinigameBase
{
    private static readonly string[] Symbols = { "A", "K", "Q", "J", "7", "★", "9", "5" };
    private static readonly Color[] SymbolColors =
    {
        new Color(0.15f, 0.15f, 0.2f), new Color(0.85f, 0.2f, 0.25f), new Color(0.9f, 0.45f, 0.1f),
        new Color(0.2f, 0.5f, 0.3f), new Color(0.85f, 0.65f, 0.1f), new Color(0.3f, 0.35f, 0.85f),
        new Color(0.6f, 0.25f, 0.7f), new Color(0.2f, 0.6f, 0.65f),
    };
    private static readonly Color BackColor = new Color(0.45f, 0.15f, 0.2f);

    private readonly List<Image> cards = new List<Image>();
    private readonly List<TextMeshProUGUI> faces = new List<TextMeshProUGUI>();
    private int matchedPairs;

    public override string StatusText => cards.Count == 0 ? "상대를 기다리는 중..." : $"{matchedPairs} / {cards.Count / 2}쌍";

    protected override void OnCoopBegin()
    {
        MinigameUI.Text(Board, "Hint", "둘 중 누구든 카드를 뒤집을 수 있어요. 함께 기억하세요!", 24f,
            new Color(1f, 1f, 1f, 0.7f), new Vector2(0f, -215f), new Vector2(800f, 36f));
    }

    public override void OnServerMessage(CoopMsg msg)
    {
        switch (msg.kind)
        {
            case CoopCardsLogic.Layout: BuildCards(msg.a); break;
            case CoopCardsLogic.Reveal: Show(msg.a, msg.b); break;
            case CoopCardsLogic.Match:
                SetMatched(msg.a);
                SetMatched(msg.b);
                matchedPairs++;
                break;
            case CoopCardsLogic.Hide:
                Hide(msg.a);
                Hide(msg.b);
                break;
        }
    }

    private void BuildCards(int count)
    {
        int cols = Mathf.CeilToInt(count / 2f);
        const float w = 110f, h = 150f, gap = 18f;
        float startX = -(cols - 1) * (w + gap) * 0.5f;
        for (int i = 0; i < count; i++)
        {
            int idx = i;
            float x = startX + (i % cols) * (w + gap);
            float y = i < cols ? 85f : -85f;
            var btn = MinigameUI.Button(Board, $"Card{i}", "", BackColor, new Vector2(x, y), new Vector2(w, h), () => Click(idx));
            var img = (Image)btn.targetGraphic;
            var face = MinigameUI.Text(img.transform, "Face", "", 60f, Color.black, Vector2.zero, new Vector2(w, h));
            cards.Add(img);
            faces.Add(face);
        }
    }

    public void Click(int index)
    {
        if (IsFinished || index < 0 || index >= cards.Count) return;
        Send(new CoopMsg(CoopCardsLogic.Flip, index));
    }

    private void Show(int i, int kind)
    {
        if (i < 0 || i >= cards.Count) return;
        cards[i].color = Color.white;
        faces[i].text = Symbols[kind % Symbols.Length];
        faces[i].color = SymbolColors[kind % SymbolColors.Length];
    }

    private void Hide(int i)
    {
        if (i < 0 || i >= cards.Count) return;
        cards[i].color = BackColor;
        faces[i].text = "";
    }

    private void SetMatched(int i)
    {
        if (i < 0 || i >= cards.Count) return;
        cards[i].color = new Color(0.75f, 0.95f, 0.78f);
        cards[i].GetComponent<Button>().interactable = false;
    }

    public int CardCount => cards.Count;
}
