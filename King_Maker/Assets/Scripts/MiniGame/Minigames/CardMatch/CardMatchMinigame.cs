using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// P2 카드 짝 맞추기: 카드를 두 장씩 뒤집어 같은 카드끼리 맞춘다.
public class CardMatchMinigame : MinigameBase
{
    private static readonly string[] Faces = { "A", "K", "Q", "J", "7", "★" };
    private static readonly Color[] FaceColors =
    {
        new Color(0.95f, 0.30f, 0.30f), new Color(0.30f, 0.60f, 0.95f), new Color(0.35f, 0.85f, 0.45f),
        new Color(0.96f, 0.78f, 0.30f), new Color(0.75f, 0.45f, 0.95f), new Color(0.95f, 0.60f, 0.25f)
    };
    private static readonly Color BackColor = new Color(0.45f, 0.12f, 0.16f);
    private static readonly Color FrontColor = new Color(0.95f, 0.93f, 0.88f);

    private class Card
    {
        public int face;
        public bool faceUp;
        public bool matched;
        public Button button;
        public Image image;
        public TextMeshProUGUI label;
    }

    private readonly List<Card> cards = new List<Card>();
    private Card first;
    private Card second;
    private float hideTimer;   // 틀린 두 장을 다시 덮기까지 남은 시간
    private float previewTimer;
    private int matchedPairs;
    private int pairCount;
    private float mismatchRevealTime;

    public override string StatusText => $"{matchedPairs} / {pairCount} 쌍";

    protected override void OnBegin()
    {
        var settings = DataAs<CardMatchMissionData>();
        pairCount = settings != null ? settings.pairCount : 3;
        pairCount = Mathf.Clamp(pairCount, 1, Faces.Length);
        mismatchRevealTime = settings != null ? settings.mismatchRevealTime : 0.6f;
        previewTimer = settings != null ? settings.previewTime : 1f;

        var faces = new List<int>();
        for (int i = 0; i < pairCount; i++) { faces.Add(i); faces.Add(i); }
        for (int i = faces.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (faces[i], faces[j]) = (faces[j], faces[i]);
        }

        int count = faces.Count;
        int columns = count <= 6 ? 3 : 4;
        if (count == 4) columns = 2;
        int rows = Mathf.CeilToInt(count / (float)columns);
        var cardSize = new Vector2(140f, 190f);
        float gap = 24f;
        // 카드가 많으면 영역에 맞게 축소
        float fullW = columns * cardSize.x + (columns - 1) * gap;
        float fullH = rows * cardSize.y + (rows - 1) * gap;
        var area = Board.rect.size;
        if (area.x <= 0f || area.y <= 0f) area = new Vector2(fullW, fullH);
        float scale = Mathf.Min(1f, area.x / fullW, area.y / fullH);
        cardSize *= scale;
        gap *= scale;
        float totalW = fullW * scale;
        float totalH = fullH * scale;

        for (int i = 0; i < count; i++)
        {
            int col = i % columns;
            int row = i / columns;
            var pos = new Vector2(-totalW * 0.5f + cardSize.x * 0.5f + col * (cardSize.x + gap),
                                   totalH * 0.5f - cardSize.y * 0.5f - row * (cardSize.y + gap));
            var card = new Card { face = faces[i] };
            card.button = MinigameUI.Button(Board, $"Card{i}", "", BackColor, pos, cardSize, () => OnCardClicked(card));
            card.image = (Image)card.button.targetGraphic;
            card.label = MinigameUI.Text(card.button.transform, "Face", "", 80f * scale, Color.black, Vector2.zero, cardSize);
            card.label.fontStyle = FontStyles.Bold;
            cards.Add(card);
        }

        bool preview = previewTimer > 0f;
        foreach (var c in cards) SetFaceUp(c, preview);
    }

    protected override void OnTick(float deltaTime)
    {
        if (previewTimer > 0f)
        {
            previewTimer -= deltaTime;
            if (previewTimer <= 0f)
            {
                foreach (var c in cards) SetFaceUp(c, false);
            }
            return;
        }

        if (hideTimer > 0f)
        {
            hideTimer -= deltaTime;
            if (hideTimer <= 0f)
            {
                SetFaceUp(first, false);
                SetFaceUp(second, false);
                first = second = null;
            }
        }
    }

    private void OnCardClicked(Card card)
    {
        if (IsFinished || previewTimer > 0f || hideTimer > 0f) return;
        if (card.matched || card.faceUp) return;

        SetFaceUp(card, true);

        if (first == null)
        {
            first = card;
            return;
        }

        second = card;
        if (first.face == second.face)
        {
            first.matched = second.matched = true;
            first.image.color = second.image.color = new Color(0.80f, 0.95f, 0.80f);
            first = second = null;
            matchedPairs++;
            if (matchedPairs >= pairCount) Finish(MinigameResult.Success);
        }
        else
        {
            hideTimer = mismatchRevealTime;
        }
    }

    private static void SetFaceUp(Card card, bool faceUp)
    {
        card.faceUp = faceUp;
        if (card.matched) return;
        card.image.color = faceUp ? FrontColor : BackColor;
        card.label.text = faceUp ? Faces[card.face] : "";
        card.label.color = FaceColors[card.face];
    }
}
