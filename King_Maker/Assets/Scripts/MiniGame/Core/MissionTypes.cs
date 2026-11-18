using System;

// 미션(미니게임) 파트 공통 타입 모음

public enum MissionType
{
    Personal,    // 개인 미션
    Coop,        // 협동 미션 (2인)
    HighRisk,    // 고위험/특수 미션
    Competitive  // 경쟁 미션
}

public enum MinigameResult
{
    Success,
    Fail,
    Cancelled
}

// 미니게임이 취소된 이유 (안내 문구용)
public enum MinigameCancelReason
{
    None,
    Escape,     // ESC 입력
    Damaged,    // 피격
    DayEnded,   // 일차 종료(컷오프)
    Server      // 서버 강제 종료
}

// 서버가 미션 시작을 거부한 이유
public enum MissionDenyReason
{
    None,
    InvalidStation,
    DayNotActive,
    AlreadyCleared,
    MissingTool,
    Occupied,
    OnCooldown,
    AlreadyInMission,
    NotEnoughPlayers,
    TooFar,
    NotActivated,      // 아이템을 아직 올려놓지 않음 (MissionItemSocket)
    CoopSlotTaken,     // 협동: 이 자리는 이미 누군가 수행함
    CoopAlreadyJoined, // 협동: 이미 다른 자리를 맡고 있음 (한 명이 두 자리 불가)
    NotEnoughGrade,    // 도박: 배팅할 등급이 부족
    NeedItem,          // 협동 C2: 숫자 패널을 들고 있어야 함
    RoleNotAllowed     // 자리 조건 불충족 (예: Q 전용 자리)
}

public enum MissionPenaltyType
{
    None,
    NoiseOnComplete,      // 미션 완료 시 주변에 효과음
    RevealPositionOnStart,// 미션 수행 중 위치 일시 공개
    CctvRecord            // CCTV 기록
}

// 등급 구간별 페널티 (minGrade ~ maxGrade 구간의 플레이어에게 적용)
[Serializable]
public struct GradePenalty
{
    public int minGrade;
    public int maxGrade;
    public MissionPenaltyType penalty;
    public float radius;   // 소음 반경
    public float duration; // 위치 공개 시간
}
