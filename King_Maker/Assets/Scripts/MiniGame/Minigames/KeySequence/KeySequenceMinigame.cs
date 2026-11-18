using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// P6 키보드 조작 게임 (3차 기획서)
// 화면에 나타난 방향키를 순서대로 입력한다. 틀리면 처음부터.
public class KeySequenceMinigame : MinigameBase
{
    private static readonly string[] ArrowGlyphs = { "↑", "↓", "←", "→" };

    private int length;
    private int rounds;
    private bool reshuffle;
    private float wrongLock;
    private bool failOnWrong;

    private int[] sequence;
    private Image[] boxes;
    private TextMeshProUGUI[] glyphs;
    private TextMeshProUGUI feedback;

    private int index;
    private int round;
    private float lockTimer;
    private float feedbackTimer;

    public override string StatusText => rounds > 1 ? $"{round + 1} / {rounds}줄" : $"{index} / {length}";

    protected override void OnBegin()
    {
        var s = DataAs<KeySequenceMissionData>();
        length = s != null ? s.sequenceLength : 6;
        rounds = s != null ? s.rounds : 1;
        reshuffle = s != null && s.reshuffleOnWrong;
        wrongLock = s != null ? s.wrongLockTime : 0.4f;
        failOnWrong = s != null && s.failOnWrong;

        var screen = MinigameUI.Image(Board, "Screen", new Color(0.05f, 0.08f, 0.14f), new Vector2(0f, 20f),
            new Vector2(Mathf.Max(600f, length * 110f + 80f), 260f));
        MinigameUI.Text(screen.transform, "Title", "ARCADE", 34f, MinigameUI.Gold, new Vector2(0f, 95f), new Vector2(400f, 44f))
            .fontStyle = FontStyles.Bold;

        boxes = new Image[length];
        glyphs = new TextMeshProUGUI[length];
        for (int i = 0; i < length; i++)
        {
            float x = (i - (length - 1) * 0.5f) * 110f;
            boxes[i] = MinigameUI.Image(screen.transform, $"Key{i}", MinigameUI.SlotColor, new Vector2(x, -10f), new Vector2(90f, 90f));
            glyphs[i] = MinigameUI.Text(boxes[i].transform, "Glyph", "", 64f, MinigameUI.TextColor, Vector2.zero, new Vector2(90f, 90f));
            glyphs[i].fontStyle = FontStyles.Bold;
        }

        MinigameUI.Text(Board, "Hint", "방향키를 순서대로 누르세요", 26f, new Color(1f, 1f, 1f, 0.7f),
            new Vector2(0f, -150f), new Vector2(600f, 40f));
        feedback = MinigameUI.Text(Board, "Feedback", "", 34f, MinigameUI.Good, new Vector2(0f, -200f), new Vector2(400f, 50f));

        NewSequence();
    }

    private void NewSequence()
    {
        sequence = new int[length];
        for (int i = 0; i < length; i++)
        {
            // 같은 키가 3번 연속 나오지 않게
            int v;
            do v = Random.Range(0, 4);
            while (i >= 2 && v == sequence[i - 1] && v == sequence[i - 2]);
            sequence[i] = v;
            glyphs[i].text = ArrowGlyphs[v];
        }
        ResetProgress();
    }

    private void ResetProgress()
    {
        index = 0;
        for (int i = 0; i < length; i++)
        {
            boxes[i].color = MinigameUI.SlotColor;
            glyphs[i].color = MinigameUI.TextColor;
        }
        Highlight();
    }

    private void Highlight()
    {
        if (index < length) boxes[index].color = MinigameUI.ButtonColor;
    }

    protected override void OnTick(float deltaTime)
    {
        if (feedbackTimer > 0f)
        {
            feedbackTimer -= deltaTime;
            if (feedbackTimer <= 0f) feedback.text = "";
        }

        if (lockTimer > 0f)
        {
            lockTimer -= deltaTime;
            if (lockTimer <= 0f)
            {
                if (reshuffle) NewSequence();
                else ResetProgress();
            }
            return;
        }

        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.upArrowKey.wasPressedThisFrame) Press(0);
        else if (kb.downArrowKey.wasPressedThisFrame) Press(1);
        else if (kb.leftArrowKey.wasPressedThisFrame) Press(2);
        else if (kb.rightArrowKey.wasPressedThisFrame) Press(3);
    }

    // 방향 입력 1회 (0:↑ 1:↓ 2:← 3:→)
    public void Press(int direction)
    {
        if (IsFinished || lockTimer > 0f) return;

        if (sequence[index] == direction)
        {
            boxes[index].color = MinigameUI.Good;
            index++;
            if (index < length) { Highlight(); return; }

            round++;
            if (round >= rounds) { Finish(MinigameResult.Success); return; }
            ShowFeedback("다음 줄!", MinigameUI.Good);
            NewSequence();
        }
        else
        {
            boxes[index].color = MinigameUI.Bad;
            ShowFeedback("틀렸어요! 처음부터", MinigameUI.Bad);
            if (failOnWrong) { Finish(MinigameResult.Fail); return; }
            lockTimer = wrongLock;
        }
    }

    private void ShowFeedback(string text, Color color)
    {
        feedback.text = text;
        feedback.color = color;
        feedbackTimer = 0.6f;
    }
}
