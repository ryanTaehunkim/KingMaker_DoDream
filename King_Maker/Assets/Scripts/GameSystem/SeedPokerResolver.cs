using System;
using System.Collections.Generic;
using System.Linq;

public enum SeedPokerOutcome
{
    Won,        // 시드카드 획득
    Void,       // 동점 꽝 (보상/페널티 없음)
    Penalized   // 참가했지만 획득 실패 → 페널티
}

public readonly struct SeedPokerParticipant
{
    public readonly ulong clientId;
    public readonly int grade;

    public SeedPokerParticipant(ulong clientId, int grade)
    {
        this.clientId = clientId;
        this.grade = grade;
    }
}

public readonly struct SeedPokerPlayerResult
{
    public readonly ulong clientId;
    public readonly SeedPokerOutcome outcome;
    public readonly int oldGrade;
    public readonly int newGrade;
    public readonly int cardValue;      // Won일 때만 의미 있음 (상한 보정 전 카드 값)
    public readonly int gradeDelta;     // newGrade - oldGrade (상한 보정 후 실제 변화량)

    public SeedPokerPlayerResult(
        ulong clientId, SeedPokerOutcome outcome,
        int oldGrade, int newGrade, int cardValue)
    {
        this.clientId = clientId;
        this.outcome = outcome;
        this.oldGrade = oldGrade;
        this.newGrade = newGrade;
        this.cardValue = cardValue;
        this.gradeDelta = newGrade - oldGrade;
    }
}

public static class SeedPokerResolver
{
    /// <param name="participants">참가를 선택한 생존자 (id, 현재 등급)</param>
    /// <param name="seedCards">이번 일차에 뽑힌 시드카드 (순서 무관)</param>
    /// <param name="day">현재 일차</param>
    /// <param name="penaltyPerDay">페널티 배수. 페널티 = day * penaltyPerDay</param>
    /// <param name="maxGradeFromSeedPoker">카드 획득으로 도달 가능한 최대 등급 (16)</param>
    /// <returns>참가자 전원의 결과 (입력 순서 유지). newGrade는 하한 보정 없음 (1 미만 가능)</returns>
    public static List<SeedPokerPlayerResult> Resolve(
        IReadOnlyList<SeedPokerParticipant> participants,
        IReadOnlyList<int> seedCards,
        int day,
        int penaltyPerDay,
        int maxGradeFromSeedPoker)
    {
        var results = new List<SeedPokerPlayerResult>(participants.Count);
        if (participants.Count == 0)
            return results;

        // 1) 동점 그룹 전원 꽝
        var voidIds = new HashSet<ulong>(
            participants
                .GroupBy(p => p.grade)
                .Where(g => g.Count() >= 2)
                .SelectMany(g => g.Select(p => p.clientId)));

        // 2) 유효 참가자: 등급 오름차순
        var valid = participants
            .Where(p => !voidIds.Contains(p.clientId))
            .OrderBy(p => p.grade)
            .ToList();

        // 3) 분배 인원 = min(카드 수, 유효 인원). 카드는 큰 것부터 사용, 작은 카드는 소멸
        int winnerCount = Math.Min(seedCards.Count, valid.Count);
        var cardsDesc = seedCards.OrderByDescending(c => c).Take(winnerCount).ToList();

        var winnerCards = new Dictionary<ulong, int>();
        for (int i = 0; i < winnerCount; i++)
            winnerCards[valid[i].clientId] = cardsDesc[i];

        int penalty = day * penaltyPerDay;

        // 4) 결과 조립 (입력 순서 유지)
        foreach (var p in participants)
        {
            if (voidIds.Contains(p.clientId))
            {
                results.Add(new SeedPokerPlayerResult(
                    p.clientId, SeedPokerOutcome.Void, p.grade, p.grade, 0));
            }
            else if (winnerCards.TryGetValue(p.clientId, out int card))
            {
                int newGrade = Math.Min(p.grade + card, maxGradeFromSeedPoker);
                // 이미 상한 이상이면 내려가지 않도록 (참가 차단이 있어도 방어)
                newGrade = Math.Max(newGrade, p.grade);
                results.Add(new SeedPokerPlayerResult(
                    p.clientId, SeedPokerOutcome.Won, p.grade, newGrade, card));
            }
            else
            {
                results.Add(new SeedPokerPlayerResult(
                    p.clientId, SeedPokerOutcome.Penalized,
                    p.grade, p.grade - penalty, 0));
            }
        }

        return results;
    }
}