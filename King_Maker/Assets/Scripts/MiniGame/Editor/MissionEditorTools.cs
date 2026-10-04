using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 미션 파트 에디터 도구
// - Assign Station IDs: 열린 씬의 MissionStation에 고유 번호 부여
// - Create Default Mission Assets: 개인 미션 5종의 미니게임 프리팹과 MissionData 생성 (이미 있으면 건너뜀)
public static class MissionEditorTools
{
    public const string PrefabFolder = "Assets/Prefebs/Minigames";
    public const string DataFolder = "Assets/Data/Missions";

    [MenuItem("KingMaker/Missions/Assign Station IDs")]
    public static void AssignStationIds()
    {
        var stations = Object.FindObjectsByType<MissionStation>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var used = new HashSet<int>();
        var needId = new List<MissionStation>();

        foreach (var s in stations)
        {
            if (s.StationId > 0 && used.Add(s.StationId)) continue;
            needId.Add(s);
        }

        int next = 1;
        foreach (var s in needId)
        {
            while (used.Contains(next)) next++;
            Undo.RecordObject(s, "Assign Station ID");
            s.EditorSetStationId(next);
            used.Add(next);
            EditorUtility.SetDirty(s);
            EditorSceneManager.MarkSceneDirty(s.gameObject.scene);
        }
        Debug.Log($"[Mission] 스테이션 {stations.Length}개 확인, {needId.Count}개에 새 번호 부여");
    }

    [MenuItem("KingMaker/Missions/Create Default Mission Assets")]
    public static void CreateDefaultAssets()
    {
        EnsureFolder(PrefabFolder);
        EnsureFolder(DataFolder);

        var wine = CreatePrefab<WineGlassMinigame>("Minigame_WineGlass");
        var card = CreatePrefab<CardMatchMinigame>("Minigame_CardMatch");
        var jackpot = CreatePrefab<JackpotMinigame>("Minigame_Jackpot");
        var cctv = CreatePrefab<CctvMinigame>("Minigame_Cctv");
        var cork = CreatePrefab<CorkMinigame>("Minigame_Cork");

        CreateData<WineGlassMissionData>("Mission_P1_WineGlass", wine, d =>
        {
            d.missionId = 101;
            d.displayName = "와인잔 닦기";
            d.ruleText = "마우스를 누른 채 문질러 와인 얼룩을 지우세요.";
            d.timeLimit = 15f;
            d.minClearTime = 3f;
            d.requiredToolId = "cloth";
            d.requiredToolName = "행주";
        });
        CreateData<CardMatchMissionData>("Mission_P2_CardMatch", card, d =>
        {
            d.missionId = 102;
            d.displayName = "카드 짝 맞추기";
            d.ruleText = "카드를 두 장씩 뒤집어 같은 카드끼리 맞추세요.";
            d.timeLimit = 20f;
            d.minClearTime = 3f;
        });
        CreateData<JackpotMissionData>("Mission_P3_Jackpot", jackpot, d =>
        {
            d.missionId = 103;
            d.displayName = "잭팟 숫자 맞추기";
            d.ruleText = "▲▼ 버튼으로 모든 칸을 9로 맞추세요.";
            d.timeLimit = 10f;
            d.minClearTime = 0.5f;
        });
        CreateData<CctvMissionData>("Mission_P4_Cctv", cctv, d =>
        {
            d.missionId = 104;
            d.displayName = "CCTV 모니터 켜기";
            d.ruleText = "모니터에 깜빡이는 색과 같은 버튼을 3초간 누르고 계세요.";
            d.timeLimit = 12f;
            d.minClearTime = 2.5f;
        });
        CreateData<CorkMissionData>("Mission_P5_Cork", cork, d =>
        {
            d.missionId = 105;
            d.displayName = "코르크 병 따기";
            d.ruleText = "코르크가 초록 구간에 들어왔을 때 스페이스바를 누르세요.";
            d.timeLimit = 10f;
            d.minClearTime = 0.5f;
        });

        AssetDatabase.SaveAssets();
        Debug.Log("[Mission] 기본 미션 에셋 생성 완료");
    }

    private static MinigameBase CreatePrefab<T>(string name) where T : MinigameBase
    {
        string path = $"{PrefabFolder}/{name}.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing;

        var go = new GameObject(name, typeof(RectTransform));
        go.AddComponent<T>();
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab.GetComponent<T>();
    }

    private static void CreateData<T>(string name, MinigameBase prefab, System.Action<T> setup) where T : MissionData
    {
        string path = $"{DataFolder}/{name}.asset";
        if (AssetDatabase.LoadAssetAtPath<T>(path) != null) return;

        var data = ScriptableObject.CreateInstance<T>();
        setup(data);
        data.minigamePrefab = prefab;
        AssetDatabase.CreateAsset(data, path);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
