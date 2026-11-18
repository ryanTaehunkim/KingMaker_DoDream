using UnityEngine;

[CreateAssetMenu(menuName = "KingMaker/Mission/Trash (P8)", fileName = "Mission_Trash")]
public class TrashMissionData : MissionData
{
    [Header("P8 청소 게임")]
    [Tooltip("치워야 할 쓰레기 개수")]
    [Range(2, 10)] public int trashCount = 5;
}
