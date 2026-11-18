using UnityEngine;

public enum AimPowerStyle { Billiards, Basketball }

// C4 2인 당구 / C5 미니 농구: A는 각도, B는 힘을 맡는다. 두 값이 모두 확정되면 서버가 결과를 계산.
[CreateAssetMenu(menuName = "KingMaker/Mission/Aim Power (C4, C5)", fileName = "Mission_AimPower")]
public class AimPowerMissionData : CoopMissionData
{
    [Header("C4 / C5 각도 + 힘")]
    public AimPowerStyle style = AimPowerStyle.Billiards;
    [Tooltip("성공에 필요한 골 수 (C4 = 1, C5 = 2)")]
    [Range(1, 5)] public int goalsNeeded = 1;
    [Tooltip("조준 가능 각도 ±(도). 0도 = 정면")]
    public float angleRange = 40f;
    [Tooltip("목표 각도와의 허용 오차(도)")]
    public float angleTolerance = 6f;
    [Tooltip("힘 정답 구간 폭 (0~1)")]
    [Range(0.05f, 0.6f)] public float powerWindow = 0.22f;
    [Tooltip("A: 초당 회전 각도")]
    public float aimSpeed = 45f;
    [Tooltip("B: 힘 게이지가 0→1→0 한 번 왕복하는 시간(초)")]
    public float powerCycle = 1.4f;
    [Tooltip("슛 결과를 보여주는 시간(초)")]
    public float shotShowTime = 1.0f;

    public override CoopGameLogic CreateLogic() => new AimPowerLogic();
}

// 역할 0 = 각도(A), 역할 1 = 힘(B)
// 메시지
//  서버→화면: 1 Setup(x=목표 각도, y=힘 중심, a=골, b=필요 골)  2 Aim(x=각도)  3 Locked(a=역할, x=값)  4 Shot(a=성공 1/0, x=각도, y=힘)
//  화면→서버: 2 Aim(x)  3 Lock(x)
public class AimPowerLogic : CoopGameLogic
{
    public const byte Setup = 1, Aim = 2, Lock = 3, Shot = 4;
    public const byte Locked = 3;

    private AimPowerMissionData d;
    private float target, powerCenter;
    private float angle, power;
    private bool angleLocked, powerLocked;
    private int goals;
    private float shotTimer;
    private bool lastHit;

    public float TargetAngle => target;
    public float PowerCenter => powerCenter;

    protected override void OnStart()
    {
        d = Data as AimPowerMissionData;
        NewTarget();
    }

    private void NewTarget()
    {
        float range = d != null ? d.angleRange : 40f;
        target = RandomRange(-range * 0.75f, range * 0.75f);
        powerCenter = RandomRange(0.3f, 0.8f);
        SendSetup();
    }

    private void SendSetup()
    {
        angleLocked = powerLocked = false;
        Host.Broadcast(new CoopMsg(Setup, goals, d != null ? d.goalsNeeded : 1, target, powerCenter));
    }

    protected override void OnInput(int role, CoopMsg msg)
    {
        if (shotTimer > 0f) return;
        float range = d != null ? d.angleRange : 40f;

        if (msg.kind == Aim && role == 0 && !angleLocked)
        {
            angle = Mathf.Clamp(msg.x, -range, range);
            Host.SendTo(1, new CoopMsg(Aim, 0, 0, angle));
            return;
        }
        if (msg.kind != Lock) return;

        if (role == 0 && !angleLocked)
        {
            angle = Mathf.Clamp(msg.x, -range, range);
            angleLocked = true;
        }
        else if (role == 1 && !powerLocked)
        {
            power = Mathf.Clamp01(msg.x);
            powerLocked = true;
        }
        else return;

        Host.Broadcast(new CoopMsg(Locked, role, 0, role == 0 ? angle : power));
        if (!angleLocked || !powerLocked) return;

        float tol = d != null ? d.angleTolerance : 6f;
        float win = d != null ? d.powerWindow : 0.22f;
        lastHit = Mathf.Abs(angle - target) <= tol && Mathf.Abs(power - powerCenter) <= win * 0.5f;
        if (lastHit) goals++;
        Host.Broadcast(new CoopMsg(Shot, lastHit ? 1 : 0, goals, angle, power));
        shotTimer = d != null ? d.shotShowTime : 1f;
    }

    // 필요한 골을 이미 넣었으면 연출 중에 시간이 끝나도 성공. 마지막 슛 연출 중이면 결과를 기다린다.
    protected override void OnTimeout()
    {
        if (goals >= (d != null ? d.goalsNeeded : 1)) End(MinigameResult.Success);
        else if (shotTimer <= 0f) End(MinigameResult.Fail);
    }

    protected override void OnTick(float dt)
    {
        if (shotTimer <= 0f) return;
        shotTimer -= dt;
        if (shotTimer > 0f) return;

        if (goals >= (d != null ? d.goalsNeeded : 1)) End(MinigameResult.Success);
        else if (lastHit) NewTarget();
        else SendSetup(); // 같은 목표로 다시
    }
}
