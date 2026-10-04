using TMPro;
using UnityEngine;
using UnityEngine.UI;

// P3 잭팟 숫자 맞추기: 랜덤 칸을 ▲/▼ 버튼으로 굴려 모든 칸을 목표 숫자로 맞춘다. (예: 7 9 9 → ▲ 2회)
public class JackpotMinigame : MinigameBase
{
    private int[] values;
    private bool[] adjustable;
    private TextMeshProUGUI[] digitTexts;
    private Button[] buttons;
    private int targetDigit;
    private float rollDuration;

    private int rollingSlot = -1;
    private int rollTarget;
    private float rollTimer;
    private float rollFlickerTimer;
    private float successDelay = -1f;

    public override string StatusText => $"목표: {new string((char)('0' + targetDigit), values.Length)}";

    protected override void OnBegin()
    {
        var settings = DataAs<JackpotMissionData>();
        int slotCount = settings != null ? settings.slotCount : 3;
        int randomCount = settings != null ? Mathf.Clamp(settings.randomSlotCount, 1, slotCount) : 1;
        targetDigit = settings != null ? settings.targetDigit : 9;
        rollDuration = settings != null ? settings.rollDuration : 0.3f;
        bool allowDown = settings == null || settings.allowDownButton;

        values = new int[slotCount];
        adjustable = new bool[slotCount];
        digitTexts = new TextMeshProUGUI[slotCount];
        var buttonList = new System.Collections.Generic.List<Button>();

        var frame = MinigameUI.Image(Board, "Machine", new Color(0.35f, 0.08f, 0.10f), new Vector2(0f, 10f),
            new Vector2(slotCount * 170f + 60f, 400f));
        MinigameUI.Text(frame.transform, "Title", "JACKPOT", 40f, MinigameUI.Gold, new Vector2(0f, 178f), new Vector2(400f, 44f))
            .fontStyle = FontStyles.Bold;

        for (int i = 0; i < slotCount; i++)
        {
            float x = (i - (slotCount - 1) * 0.5f) * 170f;
            adjustable[i] = i < randomCount;
            values[i] = adjustable[i] ? RandomNonTarget() : targetDigit;

            var slot = MinigameUI.Image(frame.transform, $"Slot{i}", MinigameUI.PanelColor, new Vector2(x, 0f), new Vector2(140f, 180f));
            digitTexts[i] = MinigameUI.Text(slot.transform, "Digit", values[i].ToString(), 130f, MinigameUI.TextColor, Vector2.zero, new Vector2(140f, 180f));
            digitTexts[i].fontStyle = FontStyles.Bold;

            if (!adjustable[i]) continue;
            int index = i;
            buttonList.Add(MinigameUI.Button(frame.transform, $"Up{i}", "▲", MinigameUI.ButtonColor, new Vector2(x, 125f),
                new Vector2(120f, 56f), () => Roll(index, +1)));
            if (allowDown)
            {
                buttonList.Add(MinigameUI.Button(frame.transform, $"Down{i}", "▼", MinigameUI.ButtonColor, new Vector2(x, -125f),
                    new Vector2(120f, 56f), () => Roll(index, -1)));
            }
        }
        buttons = buttonList.ToArray();
        RefreshColors();
    }

    private int RandomNonTarget()
    {
        int v = Random.Range(0, 9);
        return v >= targetDigit ? v + 1 : v;
    }

    private void Roll(int slot, int direction)
    {
        if (IsFinished || rollingSlot >= 0 || successDelay >= 0f) return;
        rollingSlot = slot;
        rollTarget = (values[slot] + direction + 10) % 10;
        rollTimer = rollDuration;
        rollFlickerTimer = 0f;
        SetButtonsInteractable(false);
    }

    protected override void OnTick(float deltaTime)
    {
        if (successDelay >= 0f)
        {
            successDelay -= deltaTime;
            if (successDelay < 0f) Finish(MinigameResult.Success);
            return;
        }

        if (rollingSlot < 0) return;

        rollTimer -= deltaTime;
        rollFlickerTimer -= deltaTime;
        if (rollFlickerTimer <= 0f)
        {
            rollFlickerTimer = 0.05f;
            digitTexts[rollingSlot].text = Random.Range(0, 10).ToString();
        }

        if (rollTimer > 0f) return;

        values[rollingSlot] = rollTarget;
        digitTexts[rollingSlot].text = rollTarget.ToString();
        rollingSlot = -1;
        RefreshColors();

        if (AllMatched())
        {
            // 맞춘 숫자를 잠깐 보여준 뒤 성공
            successDelay = 0.3f;
        }
        else
        {
            SetButtonsInteractable(true);
        }
    }

    private bool AllMatched()
    {
        foreach (int v in values) if (v != targetDigit) return false;
        return true;
    }

    private void RefreshColors()
    {
        for (int i = 0; i < values.Length; i++)
            digitTexts[i].color = values[i] == targetDigit ? MinigameUI.Gold : MinigameUI.TextColor;
    }

    private void SetButtonsInteractable(bool interactable)
    {
        foreach (var b in buttons) b.interactable = interactable;
    }
}
