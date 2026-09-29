using UnityEngine;

// 미션 1개의 설정값. 난이도 파라미터가 필요한 미니게임은 이 클래스를 상속한 전용 데이터를 쓴다.
[CreateAssetMenu(menuName = "KingMaker/Mission/MissionData", fileName = "MissionData")]
public class MissionData : ScriptableObject
{
    [Header("기본 정보")]
    [Tooltip("미션 고유 번호. PlayerStateList.clearedMissionIds에 저장된다.")]
    public int missionId;
    public string displayName;
    [Tooltip("5초 안에 이해할 수 있는 한 줄 규칙")]
    [TextArea] public string ruleText;
    public MissionType type = MissionType.Personal;
    [Tooltip("공용 미션 여부. 일차 내 공용 미션을 전부 클리어하면 등급 분포가 공개된다.")]
    public bool isShared;

    [Header("조건")]
    [Tooltip("비어 있으면 도구 불필요")]
    public string requiredToolId;
    [Tooltip("안내 문구에 표시할 도구 이름")]
    public string requiredToolName;
    [Tooltip("협동 미션은 2")]
    public int requiredPlayers = 1;

    [Header("보상")]
    [Tooltip("성공 시 등급 변화량")]
    public int gradeReward = 1;
    [Tooltip("메인 문양 배율 적용 대상 여부 (실제 배율 계산은 등급 시스템 담당)")]
    public bool suitBonusEligible = true;

    [Header("시간")]
    [Tooltip("제한시간(초)")]
    public float timeLimit = 15f;
    [Tooltip("서버 검증용 최소 소요시간(초). 이보다 빨리 성공 보고가 오면 실패 처리")]
    public float minClearTime = 2f;
    public bool allowRetryOnFail = true;
    [Tooltip("실패 후 재도전까지 대기 시간(초)")]
    public float failRetryCooldown = 3f;

    [Header("시야")]
    [Tooltip("미니게임 중 주변 화면 어둡기 (몰입 = 위험)")]
    [Range(0f, 1f)] public float surroundDim = 0.6f;

    [Header("페널티")]
    public GradePenalty[] penalties;

    [Header("미니게임")]
    public MinigameBase minigamePrefab;

    public bool RequiresTool => !string.IsNullOrEmpty(requiredToolId);
}
