using UnityEngine;

[CreateAssetMenu(menuName = "KingMaker/Mission/Neon Wire (P7)", fileName = "Mission_NeonWire")]
public class NeonWireMissionData : MissionData
{
    [Header("P7 네온사인 선 연결")]
    [Tooltip("연결할 전선 색 개수")]
    [Range(2, 6)] public int wireCount = 4;
    [Tooltip("true면 다른 색에 연결할 때 바로 실패")]
    public bool failOnWrong = false;
}
