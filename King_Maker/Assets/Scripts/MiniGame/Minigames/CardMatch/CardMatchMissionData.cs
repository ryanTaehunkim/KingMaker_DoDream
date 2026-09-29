using UnityEngine;

[CreateAssetMenu(menuName = "KingMaker/Mission/CardMatch (P2)", fileName = "Mission_CardMatch")]
public class CardMatchMissionData : MissionData
{
    [Header("P2 카드 짝 맞추기")]
    [Tooltip("카드 쌍 개수 (카드 수 = 쌍 × 2)")]
    [Range(2, 6)] public int pairCount = 3;
    [Tooltip("시작 시 앞면을 전부 보여주는 시간(초). 0이면 보여주지 않음")]
    public float previewTime = 1f;
    [Tooltip("짝이 틀렸을 때 두 카드를 보여주는 시간(초)")]
    public float mismatchRevealTime = 0.6f;
}
