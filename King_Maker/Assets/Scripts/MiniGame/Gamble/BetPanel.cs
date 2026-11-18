using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 도박 미션 공통 배팅 UI (G1, G2, G3)
//  [-]  2 PT  [+]
//  [선택지 A] [선택지 B]     ← 선택지가 없으면 생략 (G3)
//  [   배팅   ]
public class BetPanel
{
    public RectTransform Root { get; private set; }
    public int Bet { get; private set; } = 1;
    public int Choice { get; private set; } = -1;
    public bool Locked { get; private set; }

    private int maxBet;
    private TextMeshProUGUI betText;
    private Button minus, plus, submit;
    private Button[] choiceButtons;
    private Action<int, int> onSubmit;
    private bool needsChoice;

    public static BetPanel Create(Transform parent, Vector2 pos, int maxBet, string[] choices, string submitLabel, Action<int, int> onSubmit)
    {
        var p = new BetPanel { onSubmit = onSubmit, maxBet = maxBet, needsChoice = choices != null && choices.Length > 0 };
        p.Root = MinigameUI.Rect(parent, "BetPanel", pos, new Vector2(560f, 230f));

        p.minus = MinigameUI.Button(p.Root, "Minus", "−", MinigameUI.ButtonColor, new Vector2(-150f, 70f), new Vector2(70f, 56f), () => p.SetBet(p.Bet - 1), 40f);
        p.betText = MinigameUI.Text(p.Root, "Bet", "", 38f, MinigameUI.Gold, new Vector2(0f, 70f), new Vector2(200f, 56f));
        p.plus = MinigameUI.Button(p.Root, "Plus", "+", MinigameUI.ButtonColor, new Vector2(150f, 70f), new Vector2(70f, 56f), () => p.SetBet(p.Bet + 1), 40f);

        if (p.needsChoice)
        {
            p.choiceButtons = new Button[choices.Length];
            float w = 520f / choices.Length;
            for (int i = 0; i < choices.Length; i++)
            {
                int idx = i;
                float x = -260f + w * (i + 0.5f);
                p.choiceButtons[i] = MinigameUI.Button(p.Root, $"Choice{i}", choices[i], MinigameUI.SlotColor, new Vector2(x, 0f), new Vector2(w - 16f, 58f), () => p.Choose(idx), 30f);
            }
        }

        p.submit = MinigameUI.Button(p.Root, "Submit", submitLabel, MinigameUI.ButtonColor, new Vector2(0f, -70f), new Vector2(260f, 58f), p.Submit, 32f);

        if (maxBet < 1)
        {
            p.betText.text = "배팅 불가";
            p.SetLocked(true);
        }
        else p.SetBet(1);
        p.Refresh();
        return p;
    }

    public void SetBet(int value)
    {
        if (Locked) return;
        Bet = Mathf.Clamp(value, 1, Mathf.Max(1, maxBet));
        betText.text = $"{Bet} PT";
        minus.interactable = Bet > 1;
        plus.interactable = Bet < maxBet;
    }

    public void Choose(int index)
    {
        if (Locked) return;
        Choice = index;
        Refresh();
    }

    public void Submit()
    {
        if (Locked || maxBet < 1 || (needsChoice && Choice < 0)) return;
        SetLocked(true);
        onSubmit?.Invoke(Bet, Choice);
    }

    public void SetLocked(bool locked)
    {
        Locked = locked;
        minus.interactable = !locked && Bet > 1;
        plus.interactable = !locked && Bet < maxBet;
        if (choiceButtons != null) foreach (var b in choiceButtons) b.interactable = !locked;
        Refresh();
    }

    private void Refresh()
    {
        if (choiceButtons != null)
            for (int i = 0; i < choiceButtons.Length; i++)
                ((Image)choiceButtons[i].targetGraphic).color = i == Choice ? MinigameUI.Gold * 0.85f : MinigameUI.SlotColor;
        submit.interactable = !Locked && maxBet >= 1 && (!needsChoice || Choice >= 0);
    }
}
