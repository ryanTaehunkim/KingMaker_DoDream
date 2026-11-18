using UnityEngine;

[CreateAssetMenu(menuName = "KingMaker/Mission/Safe Pattern (P10)", fileName = "Mission_SafePattern")]
public class SafePatternMissionData : MissionData
{
    [Header("P10 금고 패턴 맞추기")]
    [Tooltip("패턴 점 개수 (3x3 격자 기준)")]
    [Range(3, 9)] public int patternLength = 5;
    [Tooltip("패턴을 보여주는 시간(초)")]
    public float showTime = 2f;
    [Tooltip("틀리면 패턴을 다시 보여줄지. false면 같은 패턴을 기억으로 재입력")]
    public bool reshowOnWrong = true;
    [Tooltip("true면 한 번 틀릴 때 바로 실패")]
    public bool failOnWrong = false;
}
