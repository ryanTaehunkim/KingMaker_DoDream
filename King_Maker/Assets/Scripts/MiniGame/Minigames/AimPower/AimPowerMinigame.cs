using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// C4 2인 당구 / C5 미니 농구 화면
//  역할 0(A): ←/→(A/D)로 조준선을 돌리고 Space로 각도 확정
//  역할 1(B): 왕복하는 힘 게이지를 Space로 멈춰 힘 확정 (초록 구간이 정답)
//  둘 다 확정되면 서버가 판정 → 공이 날아가는 연출
public class AimPowerMinigame : CoopMinigameBase
{
    private const float Dist = 220f;
    private static readonly Vector2 Origin = new Vector2(0f, -80f);

    private AimPowerMissionData d;
    private RectTransform ball, goal;
    private Image aimLine, powerFill, powerZone;
    private RectTransform powerBar;
    private TextMeshProUGUI roleText, stateText, resultText;

    private float target, powerCenter;
    private float angle, power, powerT;
    private bool angleLocked, powerLocked, waitingSetup = true;
    private int goals, needed = 1;
    private float aimSendTimer;
    private float lastSentAngle = float.NaN;

    private bool shooting;
    private float shotTimer, shotAngle, shotPower;
    private bool shotHit;

    public override string StatusText => $"골 {goals} / {needed}";

    private static Vector2 Dir(float deg) => new Vector2(Mathf.Sin(deg * Mathf.Deg2Rad), Mathf.Cos(deg * Mathf.Deg2Rad));

    protected override void OnCoopBegin()
    {
        d = DataAs<AimPowerMissionData>();
        bool hoop = d != null && d.style == AimPowerStyle.Basketball;

        var field = MinigameUI.Image(Board, "Field", hoop ? new Color(0.55f, 0.36f, 0.2f) : new Color(0.1f, 0.42f, 0.22f),
            new Vector2(0f, 40f), new Vector2(760f, 300f));
        field.transform.SetAsFirstSibling();

        goal = MinigameUI.Image(Board, "Goal", hoop ? new Color(0.95f, 0.35f, 0.15f) : Color.black, Origin, hoop ? new Vector2(70f, 18f) : new Vector2(46f, 46f)).rectTransform;
        aimLine = MinigameUI.Line(Board, "AimLine", new Color(1f, 1f, 1f, 0.55f), 6f);
        ball = MinigameUI.Image(Board, "Ball", hoop ? new Color(0.95f, 0.55f, 0.15f) : Color.white, Origin, new Vector2(30f, 30f)).rectTransform;

        powerBar = MinigameUI.Gauge(Board, "Power", new Vector2(0f, -140f), new Vector2(460f, 22f), MinigameUI.SlotColor, MinigameUI.Gold, out powerFill);
        powerZone = MinigameUI.Image(powerBar, "Zone", new Color(0.35f, 0.85f, 0.45f, 0.55f), Vector2.zero, new Vector2(10f, 22f));

        roleText = MinigameUI.Text(Board, "Role", Role == 0 ? "당신: 각도 담당 (←/→ 조준, Space 확정)" : "당신: 힘 담당 (Space로 초록 구간에 멈추기)",
            26f, MinigameUI.Gold, new Vector2(0f, -185f), new Vector2(860f, 36f));
        stateText = MinigameUI.Text(Board, "State", "", 22f, new Color(1f, 1f, 1f, 0.75f), new Vector2(0f, -212f), new Vector2(860f, 30f));
        // 골대(가운데 위쪽)를 가리지 않게 왼쪽 위 구석에 표시
        resultText = MinigameUI.Text(Board, "Result", "", 40f, MinigameUI.Good, new Vector2(-265f, 150f), new Vector2(220f, 60f));
        resultText.fontStyle = FontStyles.Bold;

        Refresh();
    }

    public override void OnServerMessage(CoopMsg msg)
    {
        switch (msg.kind)
        {
            case AimPowerLogic.Setup:
                goals = msg.a;
                needed = msg.b;
                target = msg.x;
                powerCenter = msg.y;
                angleLocked = powerLocked = false;
                waitingSetup = false;
                shooting = false;
                resultText.text = "";
                ball.anchoredPosition = Origin;
                goal.anchoredPosition = Origin + Dir(target) * Dist;
                float w = d != null ? d.powerWindow : 0.22f;
                powerZone.rectTransform.anchoredPosition = new Vector2((powerCenter - 0.5f) * powerBar.sizeDelta.x, 0f);
                powerZone.rectTransform.sizeDelta = new Vector2(w * powerBar.sizeDelta.x, powerBar.sizeDelta.y);
                break;
            case AimPowerLogic.Aim:
                if (Role != 0) angle = msg.x;
                break;
            case AimPowerLogic.Locked:
                if (msg.a == 0) { angleLocked = true; angle = msg.x; }
                else { powerLocked = true; power = msg.x; }
                break;
            case AimPowerLogic.Shot:
                shooting = true;
                shotTimer = 0f;
                shotHit = msg.a == 1;
                goals = msg.b;
                shotAngle = msg.x;
                shotPower = msg.y;
                break;
        }
        Refresh();
    }

    protected override void OnTick(float dt)
    {
        if (shooting)
        {
            shotTimer += dt;
            float dur = (d != null ? d.shotShowTime : 1f) * 0.7f;
            float t = Mathf.Clamp01(shotTimer / dur);
            float reach = Dist * Mathf.Clamp(shotPower / Mathf.Max(0.05f, powerCenter), 0.3f, 1.45f);
            ball.anchoredPosition = Origin + Dir(shotAngle) * reach * t;
            if (t >= 1f)
            {
                resultText.text = shotHit ? "골인!" : "빗나감!";
                resultText.color = shotHit ? MinigameUI.Good : MinigameUI.Bad;
            }
            return;
        }
        if (waitingSetup) return;

        var kb = Keyboard.current;
        if (Role == 0 && !angleLocked)
        {
            float range = d != null ? d.angleRange : 40f;
            float speed = d != null ? d.aimSpeed : 45f;
            float dir = 0f;
            if (kb != null && (kb.leftArrowKey.isPressed || kb.aKey.isPressed)) dir -= 1f;
            if (kb != null && (kb.rightArrowKey.isPressed || kb.dKey.isPressed)) dir += 1f;
            angle = Mathf.Clamp(angle + dir * speed * dt, -range, range);

            aimSendTimer -= dt;
            if (aimSendTimer <= 0f && !Mathf.Approximately(angle, lastSentAngle))
            {
                aimSendTimer = 0.05f;
                lastSentAngle = angle;
                Send(new CoopMsg(AimPowerLogic.Aim, 0, 0, angle));
            }
            if (kb != null && kb.spaceKey.wasPressedThisFrame) LockAngle();
        }
        else if (Role == 1 && !powerLocked)
        {
            float cycle = d != null ? d.powerCycle : 1.4f;
            powerT += dt / Mathf.Max(0.2f, cycle);
            power = Mathf.PingPong(powerT * 2f, 1f);
            if (kb != null && kb.spaceKey.wasPressedThisFrame) LockPower();
        }
        Refresh();
    }

    public void LockAngle()
    {
        if (Role != 0 || angleLocked || waitingSetup || shooting) return;
        Send(new CoopMsg(AimPowerLogic.Lock, 0, 0, angle));
    }

    public void LockPower()
    {
        if (Role != 1 || powerLocked || waitingSetup || shooting) return;
        Send(new CoopMsg(AimPowerLogic.Lock, 0, 0, power));
    }

    // 테스트용
    public void DebugSetAngle(float a) { if (Role == 0) angle = a; }
    public void DebugSetPower(float p) { if (Role == 1) power = p; }
    public float Target => target;
    public float PowerCenter => powerCenter;

    private void Refresh()
    {
        if (aimLine == null) return;
        MinigameUI.SetLine(aimLine, Origin, Origin + Dir(angle) * 160f);
        aimLine.color = angleLocked ? MinigameUI.Gold : new Color(1f, 1f, 1f, 0.55f);
        MinigameUI.SetGauge(powerFill, power);
        powerFill.color = powerLocked ? MinigameUI.Gold : new Color(0.95f, 0.85f, 0.5f);

        if (waitingSetup) stateText.text = "준비 중...";
        else if (shooting) stateText.text = "";
        else
            stateText.text = $"각도: {(angleLocked ? "확정" : "조준 중")}   ·   힘: {(powerLocked ? "확정" : "조절 중")}";
    }
}
