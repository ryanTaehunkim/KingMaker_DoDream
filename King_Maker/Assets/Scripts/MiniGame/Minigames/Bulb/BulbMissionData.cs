using UnityEngine;

[CreateAssetMenu(menuName = "KingMaker/Mission/Bulb (P9)", fileName = "Mission_Bulb")]
public class BulbMissionData : MissionData
{
    [Header("P9 전구 불 켜기")]
    [Tooltip("스페이스 1회당 차오르는 양 (0~1)")]
    [Range(0.01f, 0.3f)] public float chargePerPress = 0.07f;
    [Tooltip("초당 줄어드는 양 (0~1). 클수록 빨리 연타해야 한다")]
    [Range(0f, 1f)] public float drainPerSecond = 0.3f;
    [Tooltip("시작 밝기 (0~1)")]
    [Range(0f, 0.5f)] public float startCharge = 0.2f;
}
