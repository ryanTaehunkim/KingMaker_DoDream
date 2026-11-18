using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// P9 전구 불 켜기 (3차 기획서)
// 깜빡이며 꺼져가는 전구를 스페이스바 연타로 밝혀 게이지를 끝까지 채운다.
public class BulbMinigame : MinigameBase
{
    private static readonly Color OffColor = new Color(0.25f, 0.24f, 0.20f);
    private static readonly Color OnColor = new Color(1f, 0.92f, 0.55f);

    private float charge;
    private float perPress;
    private float drain;

    private Image bulb;
    private Image glow;
    private Image gaugeFill;
    private float flickerTimer;
    private float flicker = 1f;
    private float successDelay = -1f;

    public override string StatusText => $"{Mathf.RoundToInt(charge * 100f)}%";

    protected override void OnBegin()
    {
        var s = DataAs<BulbMissionData>();
        perPress = s != null ? s.chargePerPress : 0.07f;
        drain = s != null ? s.drainPerSecond : 0.3f;
        charge = s != null ? s.startCharge : 0.2f;

        glow = MinigameUI.Image(Board, "Glow", new Color(1f, 0.9f, 0.5f, 0f), new Vector2(0f, 70f), new Vector2(320f, 320f));
        bulb = MinigameUI.Image(Board, "Bulb", OffColor, new Vector2(0f, 80f), new Vector2(150f, 170f));
        MinigameUI.Image(Board, "Base", new Color(0.55f, 0.55f, 0.58f), new Vector2(0f, -25f), new Vector2(80f, 50f));

        MinigameUI.Gauge(Board, "Gauge", new Vector2(0f, -110f), new Vector2(600f, 36f), MinigameUI.SlotColor, OnColor, out gaugeFill);
        MinigameUI.Text(Board, "Key", "[ SPACE ] 연타!", 30f, new Color(1f, 1f, 1f, 0.75f), new Vector2(0f, -170f), new Vector2(400f, 44f));
        Refresh();
    }

    protected override void OnTick(float deltaTime)
    {
        if (successDelay >= 0f)
        {
            successDelay -= deltaTime;
            if (successDelay < 0f) Finish(MinigameResult.Success);
            return;
        }

        var kb = Keyboard.current;
        if (kb != null && kb.spaceKey.wasPressedThisFrame) Press();
        if (IsFinished || successDelay >= 0f) return;

        charge = Mathf.Max(0f, charge - drain * deltaTime);

        // 어두울수록 심하게 깜빡임
        flickerTimer -= deltaTime;
        if (flickerTimer <= 0f)
        {
            flickerTimer = Random.Range(0.04f, 0.15f);
            flicker = Random.value < (1f - charge) * 0.5f ? Random.Range(0.2f, 0.6f) : 1f;
        }
        Refresh();
    }

    // 스페이스 1회
    public void Press()
    {
        if (IsFinished || successDelay >= 0f) return;
        charge = Mathf.Min(1f, charge + perPress);
        if (charge >= 1f)
        {
            flicker = 1f;
            Refresh();
            successDelay = 0.4f;
        }
    }

    private void Refresh()
    {
        float b = charge * flicker;
        bulb.color = Color.Lerp(OffColor, OnColor, b);
        glow.color = new Color(1f, 0.9f, 0.5f, b * 0.45f);
        MinigameUI.SetGauge(gaugeFill, charge);
    }
}
