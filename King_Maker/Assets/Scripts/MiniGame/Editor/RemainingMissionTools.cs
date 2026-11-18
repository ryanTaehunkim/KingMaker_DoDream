using System;
using System.IO;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// 3차 기획서 남은 미션 일괄 생성: C2~C5(협동), G1~G3(도박), Q 전용
// 메뉴: KingMaker/Missions/Create Remaining Mission Assets (C2~C5, G1~G3, Q)
// 만드는 것 (이미 있으면 건너뜀)
//  Data/Missions: GambleConfig, Mission_C2~C5, Mission_G1~G3, Mission_Q_Shoot
//  Prefebs/Minigames: CoopCards, AimPower(C4/C5 공용), CardDuel, QShoot, Coin, HighLow
//  Prefebs/MissionItems: Item_NumberPanel_1~9 (C2 숫자 패널)
//  Prefebs/MissionStations: 각 미션 자리 스테이션 + C2_SumBoard
public static class RemainingMissionTools
{
    private const string ItemFolder = "Assets/Prefebs/MissionItems";
    private const string StationFolder = "Assets/Prefebs/MissionStations";
    private const string FontPath = "Assets/Font/PRETENDARD-MEDIUM SDF.asset";
    private const float U = 10f; // 1m ≈ 10유닛

    private static string Data(string name) => $"{MissionEditorTools.DataFolder}/{name}.asset";

    [MenuItem("KingMaker/Missions/Create Remaining Mission Assets (C2~C5, G1~G3, Q)")]
    public static void CreateAll()
    {
        EnsureFolder(MissionEditorTools.DataFolder);
        EnsureFolder(MissionEditorTools.PrefabFolder);
        EnsureFolder(ItemFolder);
        EnsureFolder(StationFolder);

        // ---- 도박 공통 수치 ----
        var config = AssetDatabase.LoadAssetAtPath<GambleConfig>(Data("GambleConfig"));
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<GambleConfig>();
            AssetDatabase.CreateAsset(config, Data("GambleConfig"));
        }

        // ---- 미니게임 프리팹 ----
        var cards = Prefab<CoopCardsMinigame>("Minigame_CoopCards");
        var aim = Prefab<AimPowerMinigame>("Minigame_AimPower");
        var duel = Prefab<CardDuelMinigame>("Minigame_CardDuel");
        var shoot = Prefab<QShootMinigame>("Minigame_QShoot");
        var coin = Prefab<CoinMinigame>("Minigame_Coin");
        var hilo = Prefab<HighLowMinigame>("Minigame_HighLow");

        // ---- 데이터 ----
        Make<CoopMissionData>("Mission_C2_Sum", null, d =>
        {
            Coop(d, 202, "산수 미션", "목표 숫자에 맞춰 각자 숫자 패널을 찾아와 두 자리에 올려 합을 맞추세요.", 3, MissionType.Coop);
            d.mode = CoopMode.SumItems;
            d.syncWindow = 60f;
            d.slotActionText = "숫자 패널 올려놓기";
            d.slotDoneText = "올려놓음";
            d.joinedMessage = "패널을 올렸습니다! {0:0}초 안에 상대도 패널을 올려야 합니다.";
            d.failMessage = "합이 맞지 않습니다!";
            d.sumItemId = "number_panel";
        });
        Make<CoopCardsMissionData>("Mission_C3_CoopCards", cards, d =>
        {
            Coop(d, 203, "카드 짝 맞추기(고급)", "5쌍(10장)을 둘이 함께 뒤집어 8초 안에 모두 맞추세요.", 2, MissionType.Coop);
            d.mode = CoopMode.SharedMinigame;
            d.timeLimit = 8f;
            d.pairs = 5;
        });
        Make<AimPowerMissionData>("Mission_C4_Billiards", aim, d =>
        {
            Coop(d, 204, "2인 당구", "A는 각도, B는 힘! 둘 다 맞아야 공이 구멍에 들어갑니다.", 2, MissionType.Coop);
            d.mode = CoopMode.SharedMinigame;
            d.timeLimit = 6f;
            d.style = AimPowerStyle.Billiards;
            d.goalsNeeded = 1;
        });
        Make<AimPowerMissionData>("Mission_C5_Basketball", aim, d =>
        {
            Coop(d, 205, "미니 농구", "A는 좌/우 각도, B는 Space로 힘! 8초 안에 2골을 넣으세요.", 2, MissionType.Coop);
            d.mode = CoopMode.SharedMinigame;
            d.timeLimit = 8f;
            d.style = AimPowerStyle.Basketball;
            d.goalsNeeded = 2;
            d.shotShowTime = 0.8f;
        });
        Make<GambleMissionData>("Mission_G1_Coin", coin, d =>
        {
            Gamble(d, 301, "동전 앞/뒤", "앞/뒤를 고르고 배팅하세요. 맞히면 +배팅액, 틀리면 -배팅액.");
            d.kind = GambleKind.Coin;
            d.config = config;
        });
        Make<GambleMissionData>("Mission_G2_HighLow", hilo, d =>
        {
            Gamble(d, 302, "하이-로우", "다음 카드가 더 높을지 낮을지 배팅하세요. 같으면 패배.");
            d.kind = GambleKind.HighLow;
            d.config = config;
        });
        Make<CardDuelMissionData>("Mission_G3_CardDuel", duel, d =>
        {
            Coop(d, 303, "1대1 카드 대결", "둘이 배팅하고 카드 1장씩! 높은 카드가 배팅액을 가져갑니다.", 0, MissionType.HighRisk);
            d.mode = CoopMode.SharedMinigame;
            d.timeLimit = 20f;
            d.syncWindow = 30f;
            d.config = config;
            d.slotActionText = "대결 참가";
            d.roleDeniedMessage = "배팅할 등급(PT)이 부족합니다.";
            d.suitBonusEligible = false;
        });
        Make<QShootMissionData>("Mission_Q_Shoot", shoot, d =>
        {
            Coop(d, 401, "Q 전용: 블라인드 사격", "Q는 가려진 화면으로 조준, 도우미는 말로 안내! 공 3개를 맞히면 K 달성.", 0, MissionType.HighRisk);
            d.mode = CoopMode.SharedMinigame;
            d.timeLimit = 30f;
            d.syncWindow = 30f;
            d.slotActionText = "참가";
            d.roleDeniedMessage = "Q 등급(16 PT 이상)만 이 자리에 앉을 수 있습니다.";
            d.suitBonusEligible = false;
        });
        AssetDatabase.SaveAssets();

        // ---- C2 숫자 패널 ----
        var panels = new GameObject[9];
        for (int v = 1; v <= 9; v++) panels[v - 1] = NumberPanel(v);

        // ---- 스테이션 ----
        Station("Station_C2_Sum_A", "Mission_C2_Sum", new Color(0.3f, 0.5f, 0.9f), -1, "A");
        Station("Station_C2_Sum_B", "Mission_C2_Sum", new Color(0.3f, 0.5f, 0.9f), -1, "B");
        SumBoard(panels);
        Station("Station_C3_Cards_A", "Mission_C3_CoopCards", new Color(0.7f, 0.3f, 0.35f), -1, "A");
        Station("Station_C3_Cards_B", "Mission_C3_CoopCards", new Color(0.7f, 0.3f, 0.35f), -1, "B");
        Station("Station_C4_Billiards_Angle", "Mission_C4_Billiards", new Color(0.15f, 0.5f, 0.25f), 0, "각도");
        Station("Station_C4_Billiards_Power", "Mission_C4_Billiards", new Color(0.15f, 0.5f, 0.25f), 1, "힘");
        Station("Station_C5_Basket_Angle", "Mission_C5_Basketball", new Color(0.85f, 0.45f, 0.15f), 0, "각도");
        Station("Station_C5_Basket_Power", "Mission_C5_Basketball", new Color(0.85f, 0.45f, 0.15f), 1, "힘");
        Station("Station_G1_Coin", "Mission_G1_Coin", new Color(0.85f, 0.7f, 0.2f), -1, null);
        Station("Station_G2_HighLow", "Mission_G2_HighLow", new Color(0.85f, 0.7f, 0.2f), -1, null);
        Station("Station_G3_Duel_A", "Mission_G3_CardDuel", new Color(0.6f, 0.2f, 0.6f), -1, null);
        Station("Station_G3_Duel_B", "Mission_G3_CardDuel", new Color(0.6f, 0.2f, 0.6f), -1, null);
        Station("Station_Q_Shooter", "Mission_Q_Shoot", new Color(0.9f, 0.2f, 0.2f), 0, "Q 자리");
        Station("Station_Q_Helper", "Mission_Q_Shoot", new Color(0.9f, 0.2f, 0.2f), 1, "도우미 자리");

        AssetDatabase.SaveAssets();
        Debug.Log("[Mission] 남은 미션(C2~C5, G1~G3, Q) 에셋 생성 완료");
    }

    // =====================================================================

    private static void Coop(CoopMissionData d, int id, string name, string rule, int reward, MissionType type)
    {
        d.missionId = id;
        d.displayName = name;
        d.ruleText = rule;
        d.type = type;
        d.requiredPlayers = 2;
        d.gradeReward = reward;
        d.minClearTime = 0f;
        d.failRetryCooldown = 3f;
        d.syncWindow = 20f;
        d.resetDelay = 3f;
        d.slotActionText = "참가";
        d.joinedMessage = "참가했습니다! {0:0}초 안에 상대가 다른 자리에 참가해야 시작합니다.";
        d.failMessage = "미션 실패!";
    }

    private static void Gamble(GambleMissionData d, int id, string name, string rule)
    {
        d.missionId = id;
        d.displayName = name;
        d.ruleText = rule;
        d.type = MissionType.HighRisk;
        d.gradeReward = 0;
        d.suitBonusEligible = false;
        d.timeLimit = 20f;
        d.minClearTime = 0f;
        d.failRetryCooldown = 3f;
    }

    private static MinigameBase Prefab<T>(string name) where T : MinigameBase
    {
        string path = $"{MissionEditorTools.PrefabFolder}/{name}.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing;
        var go = new GameObject(name, typeof(RectTransform));
        go.AddComponent<T>();
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab.GetComponent<T>();
    }

    private static void Make<T>(string name, MinigameBase prefab, Action<T> setup) where T : MissionData
    {
        if (AssetDatabase.LoadAssetAtPath<T>(Data(name)) != null) return;
        var d = ScriptableObject.CreateInstance<T>();
        setup(d);
        d.minigamePrefab = prefab;
        AssetDatabase.CreateAsset(d, Data(name));
    }

    // 숫자 패널: 얇은 판(0.35m) 양면에 숫자. 잡기(E)/내려놓기(Q) 가능
    private static GameObject NumberPanel(int value)
    {
        string path = $"{ItemFolder}/Item_NumberPanel_{value}.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        var root = new GameObject($"Item_NumberPanel_{value}");
        try
        {
            root.tag = "Interactable";
            var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = "Plate";
            Object.DestroyImmediate(plate.GetComponent<Collider>());
            plate.transform.SetParent(root.transform, false);
            plate.transform.localScale = new Vector3(0.35f, 0.35f, 0.04f) * U;
            plate.transform.localPosition = new Vector3(0f, 0.175f * U, 0f);
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.color = new Color(0.95f, 0.92f, 0.8f);
            AssetDatabase.CreateAsset(mat, $"{ItemFolder}/NumberPanel_Mat_{value}.mat");
            plate.GetComponent<Renderer>().sharedMaterial = mat;

            for (int side = 0; side < 2; side++)
            {
                var t = new GameObject(side == 0 ? "NumberFront" : "NumberBack");
                t.transform.SetParent(root.transform, false);
                t.transform.localPosition = new Vector3(0f, 0.175f * U, (side == 0 ? -0.021f : 0.021f) * U);
                t.transform.localRotation = side == 0 ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);
                var tmp = t.AddComponent<TextMeshPro>();
                tmp.text = value.ToString();
                tmp.fontSize = 2.2f * U;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = new Color(0.1f, 0.1f, 0.15f);
                tmp.rectTransform.sizeDelta = new Vector2(0.35f, 0.35f) * U;
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
                if (font != null) tmp.font = font;
            }

            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.175f, 0f) * U;
            box.size = new Vector3(0.35f, 0.35f, 0.08f) * U;

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.3f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkTransform>();
            root.AddComponent<NetworkRigidbody>();
            var grab = root.AddComponent<GrabInteractableObject>();
            grab.gripOffset = Vector3.zero;
            grab.gripEuler = Vector3.zero;
            grab.gripScale = Vector3.one;

            var item = root.AddComponent<MissionItem>();
            var so = new SerializedObject(item);
            so.FindProperty("itemId").stringValue = "number_panel";
            so.FindProperty("displayName").stringValue = $"숫자 패널 {value}";
            so.FindProperty("value").intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();

            SetLayer(root, "Interactable");
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);

            var contents = PrefabUtility.LoadPrefabContents(path);
            var gso = new SerializedObject(contents.GetComponent<GrabInteractableObject>());
            gso.FindProperty("heldItemPrefab").objectReferenceValue = prefab;
            gso.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(contents, path);
            PrefabUtility.UnloadPrefabContents(contents);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // 자리 스테이션: 받침대(0.6×1×0.6m) + 라벨. 모델이 정해지면 받침대만 교체하면 된다.
    private static void Station(string name, string missionAsset, Color color, int seat, string seatName)
    {
        string path = $"{StationFolder}/{name}.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

        var root = new GameObject(name);
        try
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Pedestal";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(0.6f, 1f, 0.6f) * U;
            body.transform.localPosition = new Vector3(0f, 0.5f * U, 0f);

            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.5f, 0f) * U;
            box.size = new Vector3(0.6f, 1f, 0.6f) * U;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(root.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 1.5f * U, 0f);
            var label = labelGo.AddComponent<TextMeshPro>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 1.2f * U;
            label.rectTransform.sizeDelta = new Vector2(2.4f, 0.8f) * U;
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font != null) label.font = font;

            root.tag = "Interactable";
            SetLayer(root, "Interactable");

            var station = root.AddComponent<MissionStation>();
            var so = new SerializedObject(station);
            so.FindProperty("mission").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MissionData>(Data(missionAsset));
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("indicator").objectReferenceValue = body.GetComponent<Renderer>();
            so.FindProperty("readyColor").colorValue = color;
            so.FindProperty("coopSeat").intValue = seat;
            so.FindProperty("seatName").stringValue = seatName ?? "";
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // C2 목표 숫자 보드 + 패널 생성 위치 6곳 (보드 기준 반경 6m)
    private static void SumBoard(GameObject[] panels)
    {
        string path = $"{StationFolder}/C2_SumBoard.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

        var root = new GameObject("C2_SumBoard");
        try
        {
            root.AddComponent<NetworkObject>();
            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Board";
            board.transform.SetParent(root.transform, false);
            board.transform.localScale = new Vector3(1.4f, 1f, 0.1f) * U;
            board.transform.localPosition = new Vector3(0f, 1.2f * U, 0f);

            var labelGo = new GameObject("TargetLabel");
            labelGo.transform.SetParent(root.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 1.2f * U, -0.06f * U);
            var label = labelGo.AddComponent<TextMeshPro>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 1.4f * U;
            label.color = new Color(0.1f, 0.1f, 0.15f);
            label.text = "목표 숫자\n?";
            label.rectTransform.sizeDelta = new Vector2(1.4f, 1f) * U;
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font != null) label.font = font;

            var points = new Transform[6];
            for (int i = 0; i < points.Length; i++)
            {
                var p = new GameObject($"PanelSpawn{i}").transform;
                p.SetParent(root.transform, false);
                float a = i * Mathf.PI * 2f / points.Length;
                p.localPosition = new Vector3(Mathf.Cos(a) * 6f * U, 0.5f * U, Mathf.Sin(a) * 6f * U);
                points[i] = p;
            }

            var sum = root.AddComponent<CoopSumBoard>();
            var so = new SerializedObject(sum);
            so.FindProperty("mission").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CoopMissionData>(Data("Mission_C2_Sum"));
            var arr = so.FindProperty("panelPrefabs");
            arr.arraySize = panels.Length;
            for (int i = 0; i < panels.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = panels[i];
            var sp = so.FindProperty("spawnPoints");
            sp.arraySize = points.Length;
            for (int i = 0; i < points.Length; i++) sp.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
            so.FindProperty("targetLabel").objectReferenceValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void SetLayer(GameObject go, string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer < 0) return;
        foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
