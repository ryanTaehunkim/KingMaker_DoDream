using System;
using System.IO;
using UnityEngine;

// 밸런싱용 CSV 로그. 미션별 소요시간·성공률 측정에 사용한다. (서버에서만 기록)
// 저장 위치: Application.persistentDataPath/mission_log.csv
public static class MissionLogger
{
    private const string FileName = "mission_log.csv";
    private const string Header = "timestamp,day,clientId,missionId,missionName,reported,final,clientElapsed,serverElapsed";

    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    public static void Append(int day, ulong clientId, MissionData mission, MinigameResult reported,
        MinigameResult final, float clientElapsed, float serverElapsed)
    {
        try
        {
            bool writeHeader = !File.Exists(FilePath);
            // BOM 포함 UTF-8: 엑셀에서 한글이 깨지지 않도록
            using (var writer = new StreamWriter(FilePath, true, new System.Text.UTF8Encoding(true)))
            {
                if (writeHeader) writer.WriteLine(Header);
                writer.WriteLine(string.Join(",",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    day,
                    clientId,
                    mission.missionId,
                    mission.displayName?.Replace(",", " "),
                    reported,
                    final,
                    clientElapsed.ToString("0.00"),
                    serverElapsed.ToString("0.00")));
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[MissionLogger] 로그 기록 실패: {e.Message}");
        }
    }
}
