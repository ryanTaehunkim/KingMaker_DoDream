using UnityEngine;

// 도박 미션(G1~G3) 공통 수치. 기획 조정은 이 에셋 하나만 바꾸면 된다.
[CreateAssetMenu(menuName = "KingMaker/Mission/Gamble Config", fileName = "GambleConfig")]
public class GambleConfig : ScriptableObject
{
    [Tooltip("일차별 최대 배팅 PT (0번 = 1일차). 범위를 넘는 일차는 마지막 값")]
    public int[] betLimitByDay = { 2, 3, 4 };
    [Tooltip("기본 성공 확률")]
    [Range(0f, 1f)] public float baseWinChance = 0.5f;
    [Tooltip("해당 일차 개인 미션 1개 클리어당 추가 확률")]
    [Range(0f, 0.1f)] public float winChancePerClear = 0.01f;
    [Range(0f, 1f)] public float maxWinChance = 0.9f;
    [Tooltip("배팅 후 남아야 하는 최소 등급 (배팅 가능 = 현재 등급 - 이 값). 1이면 져도 탈락하지 않는다.")]
    public int minGradeAfterLoss = 1;
    [Tooltip("일차당 1회만 가능 (승패와 관계없이)")]
    public bool oncePerDay = true;

    public int BetLimit(int day)
    {
        if (betLimitByDay == null || betLimitByDay.Length == 0) return 1;
        return betLimitByDay[Mathf.Clamp(day - 1, 0, betLimitByDay.Length - 1)];
    }

    public float WinChance(int personalClears) =>
        Mathf.Min(maxWinChance, baseWinChance + personalClears * winChancePerClear);

    // grade < 0 (오프라인 등 알 수 없음)이면 일차 한도만 적용
    public int MaxBet(int day, int grade)
    {
        int limit = BetLimit(day);
        return grade < 0 ? limit : Mathf.Min(limit, grade - minGradeAfterLoss);
    }
}
