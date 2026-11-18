using UnityEngine;

public enum GambleKind
{
    Coin,   // G1 동전 앞/뒤 (choice 0 = 앞, 1 = 뒤)
    HighLow // G2 하이-로우 (choice 1 = High, 0 = Low)
}

// 1인 도박 미션(G1, G2) 설정. 결과는 서버에서만 정한다.
[CreateAssetMenu(menuName = "KingMaker/Mission/Gamble (G1, G2)", fileName = "Mission_Gamble")]
public class GambleMissionData : MissionData
{
    [Header("도박")]
    public GambleKind kind;
    public GambleConfig config;
    [Tooltip("결과 연출 시간(초). 이후 미니게임 창이 닫힌다")]
    public float revealTime = 1.6f;
}
