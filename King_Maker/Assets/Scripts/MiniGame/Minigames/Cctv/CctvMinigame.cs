using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// P4 CCTV 모니터 켜기: 꺼진 모니터에 깜빡이는 색과 같은 버튼을 일정 시간 눌러 유지한다.
// 버튼에서 손을 떼면 게이지가 초기화된다.
public class CctvMinigame : MinigameBase
{
    private static readonly Color[] Colors =
    {
        new Color(0.92f, 0.25f, 0.25f), new Color(0.30f, 0.80f, 0.40f), new Color(0.28f, 0.52f, 0.95f),
        new Color(0.96f, 0.85f, 0.25f), new Color(0.72f, 0.40f, 0.92f), new Color(0.97f, 0.58f, 0.20f)
    };
    private static readonly string[] ColorNames = { "빨강", "초록", "파랑", "노랑", "보라", "주황" };

    private Image[] buttons;
    private Image signal;
    private TextMeshProUGUI signalText;
    private Image screen;
    private Image gaugeFill;
    private TextMeshProUGUI feedback;

    private int buttonCount;
    private float holdTime;
    private float changeInterval;
    private bool wrongHoldFails;
    private float wrongTolerance;

    private int target;
    private int held = -1;
    private float holdProgress;
    private float wrongHeld;
    private float changeTimer;
    private float blinkTimer;
    private float feedbackTimer;
    private float successDelay = -1f;

    public override string StatusText => $"유지 {holdProgress:0.0} / {holdTime:0.0}초";

    protected override void OnBegin()
    {
        var s = DataAs<CctvMissionData>();
        buttonCount = Mathf.Clamp(s != null ? s.buttonCount : 4, 2, Colors.Length);
        holdTime = s != null ? s.holdTime : 3f;
        changeInterval = s != null ? s.colorChangeInterval : 0f;
        wrongHoldFails = s != null && s.wrongHoldFails;
        wrongTolerance = s != null ? s.wrongHoldTolerance : 0.5f;

        var monitor = MinigameUI.Image(Board, "Monitor", new Color(0.05f, 0.05f, 0.06f), new Vector2(0f, 100f), new Vector2(560f, 230f));
        screen = MinigameUI.Image(monitor.transform, "Screen", new Color(0.02f, 0.02f, 0.03f), Vector2.zero, new Vector2(520f, 190f));
        signal = MinigameUI.Image(screen.transform, "Signal", Color.white, new Vector2(0f, 20f), new Vector2(110f, 110f));
        signalText = MinigameUI.Text(screen.transform, "SignalText", "", 26f, MinigameUI.TextColor, new Vector2(0f, -70f), new Vector2(400f, 36f));

        MinigameUI.Gauge(Board, "HoldGauge", new Vector2(0f, -40f), new Vector2(560f, 22f), MinigameUI.SlotColor, MinigameUI.Good, out gaugeFill);
        feedback = MinigameUI.Text(Board, "Feedback", "", 28f, MinigameUI.Bad, new Vector2(0f, -80f), new Vector2(500f, 40f));

        buttons = new Image[buttonCount];
        float size = 110f, gap = 30f;
        float totalW = buttonCount * size + (buttonCount - 1) * gap;
        for (int i = 0; i < buttonCount; i++)
        {
            var pos = new Vector2(-totalW * 0.5f + size * 0.5f + i * (size + gap), -160f);
            var img = MinigameUI.Image(Board, $"Button{i}", Colors[i], pos, new Vector2(size, size), true);
            MinigameUI.Text(img.transform, "Name", ColorNames[i], 24f, Color.black, new Vector2(0f, -size * 0.5f - 18f), new Vector2(size, 30f))
                .color = MinigameUI.TextColor;
            AddHoldEvents(img.gameObject, i);
            buttons[i] = img;
        }

        PickTarget();
    }

    private void AddHoldEvents(GameObject go, int index)
    {
        var trigger = go.AddComponent<EventTrigger>();
        AddEntry(trigger, EventTriggerType.PointerDown, _ => OnPress(index));
        AddEntry(trigger, EventTriggerType.PointerUp, _ => OnRelease(index));
        AddEntry(trigger, EventTriggerType.PointerExit, _ => OnRelease(index));
    }

    private static void AddEntry(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(action);
        trigger.triggers.Add(entry);
    }

    private void OnPress(int index)
    {
        if (IsFinished || successDelay >= 0f) return;
        held = index;
        holdProgress = 0f;
        wrongHeld = 0f;
        buttons[index].transform.localScale = Vector3.one * 0.92f;
    }

    private void OnRelease(int index)
    {
        if (held != index) return;
        buttons[index].transform.localScale = Vector3.one;
        held = -1;
        if (successDelay < 0f) holdProgress = 0f;
    }

    private void PickTarget()
    {
        int next;
        do next = Random.Range(0, buttonCount);
        while (next == target && buttonCount > 1 && changeTimer > 0f);
        target = next;
        signal.color = Colors[target];
        signalText.text = $"신호: {ColorNames[target]}";
        holdProgress = 0f;
        changeTimer = changeInterval;
    }

    protected override void OnTick(float deltaTime)
    {
        if (successDelay >= 0f)
        {
            successDelay -= deltaTime;
            if (successDelay < 0f) Finish(MinigameResult.Success);
            return;
        }

        // 신호 깜빡임
        blinkTimer += deltaTime;
        signal.enabled = Mathf.Repeat(blinkTimer, 0.8f) < 0.55f;

        if (feedbackTimer > 0f)
        {
            feedbackTimer -= deltaTime;
            if (feedbackTimer <= 0f) feedback.text = "";
        }

        if (changeInterval > 0f)
        {
            changeTimer -= deltaTime;
            if (changeTimer <= 0f)
            {
                PickTarget();
                ShowFeedback("신호가 바뀌었습니다!");
            }
        }

        if (held == target)
        {
            holdProgress += deltaTime;
            if (holdProgress >= holdTime)
            {
                holdProgress = holdTime;
                TurnOnScreen();
            }
        }
        else if (held >= 0)
        {
            wrongHeld += deltaTime;
            if (wrongHeld >= wrongTolerance)
            {
                if (wrongHoldFails)
                {
                    Finish(MinigameResult.Fail);
                    return;
                }
                ShowFeedback("다른 버튼입니다");
                wrongHeld = float.MinValue; // 같은 홀드 동안 한 번만 알림
            }
        }

        MinigameUI.SetGauge(gaugeFill, holdProgress / holdTime);
    }

    private void TurnOnScreen()
    {
        screen.color = new Color(0.55f, 0.75f, 0.85f);
        signal.enabled = true;
        signal.color = new Color(1f, 1f, 1f, 0.9f);
        signalText.text = "REC ●";
        signalText.color = MinigameUI.Bad;
        successDelay = 0.35f;
    }

    private void ShowFeedback(string text)
    {
        feedback.text = text;
        feedbackTimer = 1f;
    }
}
