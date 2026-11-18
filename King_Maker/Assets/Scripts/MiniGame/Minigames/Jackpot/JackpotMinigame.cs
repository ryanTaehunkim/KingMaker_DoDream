using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// P3 잭팟 숫자 맞추기 (3차 기획서)
// 첫 번째 숫자가 계속 회전한다. 상단 STOP 버튼(또는 스페이스)으로 멈춰 목표 숫자(9)에 맞춘다.
// 2·3번째 칸은 목표 숫자로 고정. 회전 칸이 여러 개면 앞에서부터 하나씩 멈춘다.
public class JackpotMinigame : MinigameBase
{
    private int[] values;
    private TextMeshProUGUI[] digitTexts;
    private Image[] slotImages;
    private Button stopButton;
    private TextMeshProUGUI feedback;

    private int targetDigit;
    private int spinCount;
    private float spinInterval;
    private float stopPause;
    private bool failOnMiss;

    private int currentSlot;      // 지금 회전 중인 칸
    private float spinTimer;
    private float pauseTimer = -1f;
    private bool pauseIsSuccess;

    public override string StatusText => $"목표: {new string((char)('0' + targetDigit), values.Length)}";

    protected override void OnBegin()
    {
        var s = DataAs<JackpotMissionData>();
        int slotCount = s != null ? s.slotCount : 3;
        spinCount = s != null ? Mathf.Clamp(s.randomSlotCount, 1, slotCount) : 1;
        targetDigit = s != null ? s.targetDigit : 9;
        spinInterval = s != null ? Mathf.Max(0.03f, s.spinInterval) : 0.12f;
        stopPause = s != null ? s.stopPause : 0.6f;
        failOnMiss = s != null && s.failOnMiss;

        values = new int[slotCount];
        digitTexts = new TextMeshProUGUI[slotCount];
        slotImages = new Image[slotCount];

        var frame = MinigameUI.Image(Board, "Machine", new Color(0.35f, 0.08f, 0.10f), new Vector2(0f, -25f),
            new Vector2(slotCount * 170f + 60f, 370f));
        MinigameUI.Text(frame.transform, "Title", "JACKPOT", 40f, MinigameUI.Gold, new Vector2(0f, 155f), new Vector2(400f, 44f))
            .fontStyle = FontStyles.Bold;

        // 상단 STOP 버튼
        stopButton = MinigameUI.Button(frame.transform, "Stop", "STOP", MinigameUI.Bad, new Vector2(0f, 100f),
            new Vector2(220f, 60f), Stop, 36f);

        for (int i = 0; i < slotCount; i++)
        {
            float x = (i - (slotCount - 1) * 0.5f) * 170f;
            values[i] = i < spinCount ? Random.Range(0, 10) : targetDigit;

            slotImages[i] = MinigameUI.Image(frame.transform, $"Slot{i}", MinigameUI.PanelColor, new Vector2(x, -35f), new Vector2(140f, 160f));
            digitTexts[i] = MinigameUI.Text(slotImages[i].transform, "Digit", values[i].ToString(), 130f, MinigameUI.TextColor,
                Vector2.zero, new Vector2(140f, 180f));
            digitTexts[i].fontStyle = FontStyles.Bold;
        }

        MinigameUI.Text(frame.transform, "Hint", "[ SPACE ] 또는 STOP", 24f, new Color(1f, 1f, 1f, 0.6f),
            new Vector2(0f, -150f), new Vector2(400f, 36f));
        feedback = MinigameUI.Text(Board, "Feedback", "", 34f, MinigameUI.Good, new Vector2(0f, -250f), new Vector2(400f, 50f));

        currentSlot = 0;
        RefreshColors();
    }

    protected override void OnTick(float deltaTime)
    {
        if (pauseTimer >= 0f)
        {
            pauseTimer -= deltaTime;
            if (pauseTimer >= 0f) return;

            if (pauseIsSuccess) { Finish(MinigameResult.Success); return; }
            feedback.text = "";
            stopButton.interactable = true;
            RefreshColors();
        }

        spinTimer -= deltaTime;
        if (spinTimer <= 0f)
        {
            spinTimer = spinInterval;
            values[currentSlot] = (values[currentSlot] + 1) % 10;
            digitTexts[currentSlot].text = values[currentSlot].ToString();
        }

        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame) Stop();
    }

    // STOP 1회 판정
    public void Stop()
    {
        if (IsFinished || pauseTimer >= 0f) return;

        stopButton.interactable = false;
        if (values[currentSlot] == targetDigit)
        {
            RefreshColors();
            currentSlot++;
            if (currentSlot >= spinCount)
            {
                feedback.text = "JACKPOT!";
                feedback.color = MinigameUI.Gold;
                pauseIsSuccess = true;
                pauseTimer = 0.4f;
                return;
            }
            feedback.text = "좋아요!";
            feedback.color = MinigameUI.Good;
            pauseIsSuccess = false;
            pauseTimer = 0.25f;
        }
        else
        {
            digitTexts[currentSlot].color = MinigameUI.Bad;
            feedback.text = "아깝다!";
            feedback.color = MinigameUI.Bad;
            if (failOnMiss) { Finish(MinigameResult.Fail); return; }
            pauseIsSuccess = false;
            pauseTimer = stopPause;
        }
    }

    private void RefreshColors()
    {
        for (int i = 0; i < values.Length; i++)
        {
            bool locked = i >= spinCount || i < currentSlot || (i == currentSlot && values[i] == targetDigit);
            digitTexts[i].color = locked && values[i] == targetDigit ? MinigameUI.Gold : MinigameUI.TextColor;
        }
    }
}
