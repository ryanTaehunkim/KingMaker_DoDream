using UnityEngine;

[CreateAssetMenu(menuName = "KingMaker/Mission/Jackpot (P3)", fileName = "Mission_Jackpot")]
public class JackpotMissionData : MissionData
{
    [Header("P3 잭팟 숫자 맞추기")]
    [Range(1, 5)] public int slotCount = 3;
    [Tooltip("랜덤으로 시작하는 칸 수 (앞에서부터). 나머지 칸은 목표 숫자로 고정")]
    [Range(1, 5)] public int randomSlotCount = 1;
    [Tooltip("모든 칸을 이 숫자로 맞추면 성공 (999, 777 등)")]
    [Range(0, 9)] public int targetDigit = 9;
    [Tooltip("버튼 1회당 숫자가 굴러가는 연출 시간(초)")]
    public float rollDuration = 0.3f;
    [Tooltip("false면 ▲ 버튼만 제공 (난이도 상승)")]
    public bool allowDownButton = true;
}
