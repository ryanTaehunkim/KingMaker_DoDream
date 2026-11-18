using UnityEngine;

public enum CoopMode
{
    SyncAction,     // 여러 자리를 제한시간 안에 각각 수행 (C1 라디오 끄기)
    SumItems,       // 각 자리에 숫자 패널을 올려 합이 목표와 같으면 성공 (C2 산수)
    SharedMinigame  // 자리가 다 차면 두 명이 같은 미니게임을 함께 플레이 (C3~C5, G3, Q 전용)
}

// 협동(2인) 미션 설정. 같은 데이터를 가진 MissionStation 여러 개가 각각 "자리"가 된다.
// 서버가 자리별 입력을 모아 판정하고, 참여자 전원에게 결과를 보낸다.
[CreateAssetMenu(menuName = "KingMaker/Mission/Coop Mission", fileName = "Mission_Coop")]
public class CoopMissionData : MissionData
{
    [Header("협동")]
    public CoopMode mode = CoopMode.SyncAction;
    [Tooltip("SyncAction: 첫 자리 수행 후 나머지 자리를 수행해야 하는 시간(초)\n" +
             "SumItems/SharedMinigame: 첫 참가자가 상대를 기다리는 시간(초)")]
    public float syncWindow = 5f;
    [Tooltip("성공 후 자리가 원래대로 돌아오기까지 걸리는 시간(초). 다른 조가 이어서 할 수 있다.")]
    public float resetDelay = 4f;

    [Header("문구")]
    [Tooltip("스테이션 라벨의 행동 이름 (예: 라디오 끄기)")]
    public string slotActionText = "참가하기";
    [Tooltip("수행된 자리의 상태 (예: 꺼짐)")]
    public string slotDoneText = "완료";
    [Tooltip("자리를 수행한 플레이어에게 보이는 안내. {0} = 남은 초")]
    public string joinedMessage = "{0:0}초 안에 상대가 와야 합니다!";
    [Tooltip("시간 안에 다 못했을 때 안내")]
    public string failMessage = "시간 초과! 처음부터 다시 해야 합니다.";
    [Tooltip("자리 조건(CanTakeRole)을 만족하지 못했을 때 안내")]
    public string roleDeniedMessage = "";

    [Header("SumItems (C2)")]
    [Tooltip("자리에 올릴 수 있는 아이템 id")]
    public string sumItemId = "number_panel";

    public int RequiredSlots => Mathf.Max(2, requiredPlayers);

    // SharedMinigame: 서버 판정 로직 생성 (파생 데이터에서 구현)
    public virtual CoopGameLogic CreateLogic() => null;

    // 이 역할(자리)에 참가할 수 있는지. grade = 서버가 아는 등급 (오프라인이면 -1)
    public virtual bool CanTakeRole(int role, int grade) => true;

    protected virtual void OnValidate()
    {
        if (requiredPlayers < 2) requiredPlayers = 2;
    }
}
