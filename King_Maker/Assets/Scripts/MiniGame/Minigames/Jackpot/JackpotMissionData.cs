using UnityEngine;

[CreateAssetMenu(menuName = "KingMaker/Mission/Jackpot (P3)", fileName = "Mission_Jackpot")]
public class JackpotMissionData : MissionData
{
    [Header("P3 잭팟 숫자 맞추기 (3차 기획: 회전 중 STOP)")]
    [Range(1, 5)] public int slotCount = 3;
    [Tooltip("회전하는 칸 수 (앞에서부터). 나머지 칸은 목표 숫자로 고정")]
    [Range(1, 5)] public int randomSlotCount = 1;
    [Tooltip("모든 칸을 이 숫자로 맞추면 성공 (999, 777 등)")]
    [Range(0, 9)] public int targetDigit = 9;
    [Tooltip("숫자 하나가 바뀌는 간격(초). 작을수록 빠르고 어렵다")]
    public float spinInterval = 0.12f;
    [Tooltip("STOP 후 결과를 보여주는 시간(초). 틀리면 이 시간 뒤 다시 회전")]
    public float stopPause = 0.6f;
    [Tooltip("true면 한 번 틀릴 때 바로 실패")]
    public bool failOnMiss = false;
}
