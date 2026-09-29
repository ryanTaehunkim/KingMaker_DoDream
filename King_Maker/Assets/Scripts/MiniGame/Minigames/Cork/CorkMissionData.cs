using UnityEngine;

[CreateAssetMenu(menuName = "KingMaker/Mission/Cork (P5)", fileName = "Mission_Cork")]
public class CorkMissionData : MissionData
{
    [Header("P5 코르크 병 따기")]
    [Tooltip("바가 왕복하는 속도 (초당 왕복 횟수)")]
    public float barSpeed = 0.7f;
    [Tooltip("다음 성공마다 속도 증가량")]
    public float speedUpPerHit = 0.15f;
    [Tooltip("목표 구간 폭 (트랙 전체 대비 비율)")]
    [Range(0.05f, 0.5f)] public float zoneWidth = 0.18f;
    [Tooltip("필요한 성공 횟수")]
    [Range(1, 5)] public int requiredHits = 2;
    [Tooltip("구간 밖에서 누르면 즉시 실패")]
    public bool failOnMiss = false;
    [Tooltip("빗나간 뒤 입력이 막히는 시간(초)")]
    public float missLockTime = 0.4f;
}
