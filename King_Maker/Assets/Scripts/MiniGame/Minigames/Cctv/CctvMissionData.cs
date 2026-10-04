using UnityEngine;

[CreateAssetMenu(menuName = "KingMaker/Mission/CCTV (P4)", fileName = "Mission_Cctv")]
public class CctvMissionData : MissionData
{
    [Header("P4 CCTV 모니터 켜기")]
    [Range(2, 6)] public int buttonCount = 4;
    [Tooltip("올바른 버튼을 누르고 있어야 하는 시간(초)")]
    public float holdTime = 3f;
    [Tooltip("목표 색이 바뀌는 간격(초). 0이면 바뀌지 않음")]
    public float colorChangeInterval = 0f;
    [Tooltip("틀린 버튼을 누르고 있으면 실패 처리 (기획 확인 필요 항목)")]
    public bool wrongHoldFails = false;
    [Tooltip("틀린 버튼을 이 시간 이상 누르면 오답으로 판정(초)")]
    public float wrongHoldTolerance = 0.5f;
}
