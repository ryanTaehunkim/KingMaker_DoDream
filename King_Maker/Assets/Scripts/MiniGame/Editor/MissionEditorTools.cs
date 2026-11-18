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
            d.ruleText = "돌아가는 첫 번째 숫자가 9일 때 STOP(또는 스페이스)을 누르세요.";
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

    // 3차 기획서 추가 개인 미션 (이미 있으면 건너뜀) + P3 규칙 문구 갱신
    [MenuItem("KingMaker/Missions/Create 3rd Plan Mission Assets")]
    public static void Create3rdPlanAssets()
    {
        EnsureFolder(PrefabFolder);
        EnsureFolder(DataFolder);

        var keySeq = CreatePrefab<KeySequenceMinigame>("Minigame_KeySequence");
        var bulb = CreatePrefab<BulbMinigame>("Minigame_Bulb");

        CreateData<KeySequenceMissionData>("Mission_P6_KeySequence", keySeq, d =>
        {
            d.missionId = 106;
            d.displayName = "키보드 조작 게임";
            d.ruleText = "화면의 방향키를 순서대로 누르세요. 틀리면 처음부터!";
            d.gradeReward = 1;
            d.timeLimit = 15f;
            d.minClearTime = 1.5f;
        });
        CreateData<BulbMissionData>("Mission_P9_Bulb", bulb, d =>
        {
            d.missionId = 109;
            d.displayName = "전구 불 켜기";
            d.ruleText = "스페이스바를 연타해 꺼져가는 전구를 끝까지 밝히세요.";
            d.gradeReward = 2;
            d.timeLimit = 12f;
            d.minClearTime = 1.5f;
        });

        var neon = CreatePrefab<NeonWireMinigame>("Minigame_NeonWire");
        var trash = CreatePrefab<TrashMinigame>("Minigame_Trash");
        var safe = CreatePrefab<SafePatternMinigame>("Minigame_SafePattern");

        CreateData<NeonWireMissionData>("Mission_P7_NeonWire", neon, d =>
        {
            d.missionId = 107;
            d.displayName = "네온사인 선 연결";
            d.ruleText = "왼쪽 단자를 드래그해 같은 색 단자에 연결하세요.";
            d.gradeReward = 1;
            d.timeLimit = 15f;
            d.minClearTime = 2f;
        });
        CreateData<TrashMissionData>("Mission_P8_Trash", trash, d =>
        {
            d.missionId = 108;
            d.displayName = "청소 게임";
            d.ruleText = "책상 위 쓰레기를 끌어서 쓰레기통에 모두 넣으세요.";
            d.gradeReward = 1;
            d.timeLimit = 15f;
            d.minClearTime = 2f;
        });
        CreateData<SafePatternMissionData>("Mission_P10_SafePattern", safe, d =>
        {
            d.missionId = 110;
            d.displayName = "금고 패턴 맞추기";
            d.ruleText = "2초간 보이는 패턴을 기억했다가 그대로 따라 그리세요.";
            d.gradeReward = 1;
            d.timeLimit = 15f;
            d.minClearTime = 2.5f;
        });

        var jackpot = AssetDatabase.LoadAssetAtPath<JackpotMissionData>($"{DataFolder}/Mission_P3_Jackpot.asset");
        if (jackpot != null)
        {
            jackpot.ruleText = "돌아가는 첫 번째 숫자가 9일 때 STOP(또는 스페이스)을 누르세요.";
            jackpot.minClearTime = 0.5f;
            EditorUtility.SetDirty(jackpot);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[Mission] 3차 기획 미션 에셋 생성 완료 (P6~P10) + P3 규칙 갱신");
    }

    // 협동 미션 C1 라디오 끄기: 데이터 + 라디오 스테이션 프리팹 (이미 있으면 건너뜀)
    [MenuItem("KingMaker/Missions/Create Coop Mission Assets (C1)")]
    public static void CreateCoopAssets()
    {
        EnsureFolder(DataFolder);

        CreateData<CoopMissionData>("Mission_C1_Radio", null, d =>
        {
            d.missionId = 201;
            d.displayName = "라디오 끄기";
            d.ruleText = "맵 양 끝의 라디오를 5초 안에 둘 다 끄세요. (2인 협동)";
            d.type = MissionType.Coop;
            d.requiredPlayers = 2;
            d.gradeReward = 3;
            d.mode = CoopMode.SyncAction;
            d.syncWindow = 5f;
            d.resetDelay = 4f;
            d.failRetryCooldown = 3f;
            d.slotActionText = "라디오 끄기";
            d.slotDoneText = "꺼짐";
            d.joinedMessage = "라디오를 껐습니다! {0:0}초 안에 반대편 라디오도 꺼야 합니다.";
            d.failMessage = "시간 초과! 라디오가 다시 켜졌습니다.";
        });
        AssetDatabase.SaveAssets();

        MissionItemPrefabTools.CreateCoopRadioStation(false);
        AssetDatabase.SaveAssets();
        Debug.Log("[Mission] 협동 미션 C1 에셋 생성 완료 → Prefebs/MissionStations/Station_C1_Radio 2개를 맵 양 끝에 배치");
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
