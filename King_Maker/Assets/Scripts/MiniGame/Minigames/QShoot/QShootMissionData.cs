using UnityEngine;

// Q 전용 미션: Q(16 PT 이상)는 화면이 가려진 채 조준·발사만 하고, 도우미가 Q의 조준점과 공을 보며 음성으로 안내한다.
// 공 3개를 맞히면 성공 → Q는 K(19 PT) 달성. 자리 0 = Q, 자리 1 = 도우미 (MissionStation.coopSeat)
[CreateAssetMenu(menuName = "KingMaker/Mission/Q Shoot (Q 전용)", fileName = "Mission_QShoot")]
public class QShootMissionData : CoopMissionData
{
    [Header("Q 전용 미션")]
    [Tooltip("Q 자리에 앉을 수 있는 최소 등급")]
    public int qMinGrade = 16;
    [Tooltip("성공 시 Q가 도달하는 등급 (K)")]
    public int kGrade = 19;
    [Tooltip("도우미 보상 (기획 미정 → 0)")]
    public int helperReward = 0;
    [Range(1, 6)] public int targetCount = 3;
    [Tooltip("맞았다고 인정하는 반경 (UI 픽셀)")]
    public float hitRadius = 45f;
    [Tooltip("공이 나타나는 영역 크기 (UI 픽셀)")]
    public Vector2 area = new Vector2(700f, 320f);
    [Tooltip("발사 간격(초)")]
    public float fireCooldown = 0.35f;

    public override CoopGameLogic CreateLogic() => new QShootLogic();

    public override bool CanTakeRole(int role, int grade) => role != 0 || grade < 0 || grade >= qMinGrade;
}

// 역할 0 = Q(사수), 역할 1 = 도우미
// 메시지
//  서버→화면: 1 Target(a=번호, x, y) [도우미만]  2 Aim(x, y) [도우미만]  3 Hit(a=번호, b=맞힌 수)  4 Miss(x, y)
//  화면→서버: 2 Aim(x, y)  5 Fire(x, y)
public class QShootLogic : CoopGameLogic
{
    public const byte Target = 1, Aim = 2, Hit = 3, Miss = 4, Fire = 5;

    private QShootMissionData d;
    private Vector2[] targets;
    private bool[] hit;
    private int hits;
    private float fireTimer;

    public Vector2 TargetPos(int i) => targets[i];

    protected override void OnStart()
    {
        d = Data as QShootMissionData;
        int count = d != null ? d.targetCount : 3;
        Vector2 area = d != null ? d.area : new Vector2(700f, 320f);
        float r = d != null ? d.hitRadius : 45f;

        targets = new Vector2[count];
        hit = new bool[count];
        for (int i = 0; i < count; i++)
        {
            Vector2 p = Vector2.zero;
            for (int guard = 0; guard < 40; guard++)
            {
                p = new Vector2(RandomRange(-area.x * 0.5f + r, area.x * 0.5f - r), RandomRange(-area.y * 0.5f + r, area.y * 0.5f - r));
                bool ok = true;
                for (int j = 0; j < i; j++) if (Vector2.Distance(p, targets[j]) < r * 3f) ok = false;
                if (ok) break;
            }
            targets[i] = p;
            Host.SendTo(1, new CoopMsg(Target, i, 0, p.x, p.y));
        }
    }

    protected override void OnInput(int role, CoopMsg msg)
    {
        if (role != 0) return;
        if (msg.kind == Aim)
        {
            Host.SendTo(1, new CoopMsg(Aim, 0, 0, msg.x, msg.y));
            return;
        }
        if (msg.kind != Fire || fireTimer > 0f) return;
        fireTimer = d != null ? d.fireCooldown : 0.35f;

        var shot = new Vector2(msg.x, msg.y);
        float radius = d != null ? d.hitRadius : 45f;
        for (int i = 0; i < targets.Length; i++)
        {
            if (hit[i] || Vector2.Distance(shot, targets[i]) > radius) continue;
            hit[i] = true;
            hits++;
            Host.Broadcast(new CoopMsg(Hit, i, hits));
            if (hits >= targets.Length) Succeed();
            return;
        }
        Host.Broadcast(new CoopMsg(Miss, 0, 0, msg.x, msg.y));
    }

    protected override void OnTick(float dt)
    {
        if (fireTimer > 0f) fireTimer -= dt;
    }

    private void Succeed()
    {
        int qGrade = Host.GetGrade(0);
        int k = d != null ? d.kGrade : 19;
        RoleResults[0] = RoleResults[1] = MinigameResult.Success;
        Deltas[0] = qGrade >= 0 ? Mathf.Max(0, k - qGrade) : (d != null ? d.gradeReward : 3);
        Deltas[1] = d != null ? d.helperReward : 0;
        MarkCleared[0] = MarkCleared[1] = true;
        EndCustom(MinigameResult.Success);
    }
}
