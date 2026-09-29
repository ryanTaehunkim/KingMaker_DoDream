using System.Collections.Generic;

// 개인/공용 미션 전부 클리어 여부를 판정한다. (서버 전용, MissionManager가 소유)
// 판정 대상은 현재 씬에 배치된 스테이션의 미션들이다.
public class MissionProgressTracker
{
    private readonly HashSet<ulong> personalNotified = new HashSet<ulong>();
    private bool sharedNotified;

    public void ResetDay()
    {
        personalNotified.Clear();
        sharedNotified = false;
    }

    // 이 플레이어가 개인 미션을 전부 클리어했고 아직 알리지 않았다면 true
    public bool CheckAllPersonalCleared(ulong clientId, HashSet<int> clearedIds)
    {
        if (personalNotified.Contains(clientId) || clearedIds == null) return false;

        bool any = false;
        foreach (var station in MissionStation.All.Values)
        {
            var m = station.Mission;
            if (m == null || m.type != MissionType.Personal || m.isShared) continue;
            any = true;
            if (!clearedIds.Contains(m.missionId)) return false;
        }
        if (!any) return false;

        personalNotified.Add(clientId);
        return true;
    }

    // 공용 미션이 (누가 했든) 전부 클리어되었고 아직 알리지 않았다면 true
    public bool CheckAllSharedCleared(IEnumerable<HashSet<int>> allClearedSets)
    {
        if (sharedNotified) return false;

        var union = new HashSet<int>();
        foreach (var set in allClearedSets) union.UnionWith(set);

        bool any = false;
        foreach (var station in MissionStation.All.Values)
        {
            var m = station.Mission;
            if (m == null || !m.isShared) continue;
            any = true;
            if (!union.Contains(m.missionId)) return false;
        }
        if (!any) return false;

        sharedNotified = true;
        return true;
    }
}
