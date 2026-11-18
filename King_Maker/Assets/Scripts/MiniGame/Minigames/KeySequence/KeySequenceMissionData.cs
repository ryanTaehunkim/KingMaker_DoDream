using UnityEngine;

[CreateAssetMenu(menuName = "KingMaker/Mission/Key Sequence (P6)", fileName = "Mission_KeySequence")]
public class KeySequenceMissionData : MissionData
{
    [Header("P6 키보드 조작 게임")]
    [Tooltip("한 번에 입력할 방향키 개수")]
    [Range(3, 10)] public int sequenceLength = 6;
    [Tooltip("몇 줄을 연속으로 맞춰야 하는지")]
    [Range(1, 3)] public int rounds = 1;
    [Tooltip("틀렸을 때 true면 새 순서로 다시, false면 같은 순서 처음부터")]
    public bool reshuffleOnWrong = false;
    [Tooltip("틀린 뒤 입력이 막히는 시간(초)")]
    public float wrongLockTime = 0.4f;
    [Tooltip("true면 한 번 틀릴 때 바로 실패")]
    public bool failOnWrong = false;
}
