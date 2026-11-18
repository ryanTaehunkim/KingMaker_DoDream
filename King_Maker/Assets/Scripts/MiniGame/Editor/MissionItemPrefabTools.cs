using System.IO;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

// 3차 기획서 개인 미션을 Assets/Objects/IsTrigger 모델로 프리팹화하는 도구.
// 메뉴: KingMaker/Missions/Create IsTrigger Mission Prefabs
//
// 만드는 것 (이미 있으면 건너뜀)
//  [아이템]  Prefebs/MissionItems/Item_CardDeck, Item_JackPotLever  (+ 기존 Prefebs/WineGlass에 MissionItem 추가)
//            → 맵에 놓고 E로 줍고 Q로 내려놓는 오브젝트
//  [스테이션] Prefebs/MissionStations/Station_P1~P5
//            → P1~P3: 아이템을 올려놓으면 활성화 (MissionItemSocket)
//            → P4(CCTV 마우스), P5(와인병): 바로 상호작용 (아이템 불필요)
// 생성 후 씬에 스테이션을 배치하고 KingMaker/Missions/Assign Station IDs를 실행할 것.
public static class MissionItemPrefabTools
{
    private const string ModelFolder = "Assets/Objects/IsTrigger";
    private const string ItemFolder = "Assets/Prefebs/MissionItems";
    private const string StationFolder = "Assets/Prefebs/MissionStations";
    private const string WineGlassItemPath = "Assets/Prefebs/WineGlass.prefab";
    private const string FontPath = "Assets/Font/PRETENDARD-MEDIUM SDF.asset";

    // 맵 단위: 1m ≈ 10유닛 (캐릭터 키 ≈ 19유닛, 테이블 높이 ≈ 8유닛)
    private const float U = 10f;

    private struct ItemDef
    {
        public string model, id, name;
        public float size; // 가장 긴 변 길이(유닛)
    }

    private static readonly ItemDef WineGlass = new ItemDef { model = "WineGlass", id = "wine_glass", name = "와인잔", size = 0.285f * U };
    private static readonly ItemDef CardDeck = new ItemDef { model = "CardDeck", id = "card_deck", name = "카드 덱", size = 0.12f * U };
    private static readonly ItemDef JackpotLever = new ItemDef { model = "JackPotLever", id = "jackpot_lever", name = "잭팟 레버", size = 0.45f * U };

    [MenuItem("KingMaker/Missions/Create IsTrigger Mission Prefabs")]
    public static void CreateAll() => Build(false);

    [MenuItem("KingMaker/Missions/Rebuild IsTrigger Mission Prefabs (overwrite)")]
    public static void RebuildAll()
    {
        Build(true);
    }

    private static bool overwrite;

    private static void Build(bool overwriteExisting)
    {
        overwrite = overwriteExisting;
        EnsureFolder(ItemFolder);
        EnsureFolder(StationFolder);

        // ---- 아이템 ----
        var wineItem = SetupExistingWineGlass();
        var deckItem = CreateItemPrefab(CardDeck);
        var leverItem = CreateItemPrefab(JackpotLever);

        // ---- 스테이션 ----
        CreateSocketStation("Station_P1_WineGlass", "Mission_P1_WineGlass", WineGlass, wineItem);
        CreateSocketStation("Station_P2_CardMatch", "Mission_P2_CardMatch", CardDeck, deckItem);
        CreateSocketStation("Station_P3_Jackpot", "Mission_P3_Jackpot", JackpotLever, leverItem);
        CreateDirectStation("Station_P4_Cctv", "Mission_P4_Cctv", "ComputerMouse", 0.25f * U);
        CreateDirectStation("Station_P5_Cork", "Mission_P5_Cork", "WineBottle", 0.35f * U);

        AssetDatabase.SaveAssets();
        Debug.Log("[Mission] IsTrigger 미션 프리팹 생성 완료 → 씬에 배치 후 'Assign Station IDs' 실행");
    }

    // =====================================================================
    // 아이템
    // =====================================================================

    // 기존 와인잔(잡기 가능)에 MissionItem만 추가
    private static GameObject SetupExistingWineGlass()
    {
        var root = PrefabUtility.LoadPrefabContents(WineGlassItemPath);
        if (root == null)
        {
            Debug.LogWarning($"[Mission] {WineGlassItemPath} 없음 → 와인잔 아이템 새로 생성");
            return CreateItemPrefab(WineGlass);
        }
        try
        {
            if (!root.TryGetComponent(out MissionItem item)) item = root.AddComponent<MissionItem>();
            SetMissionItem(item, WineGlass);
            PrefabUtility.SaveAsPrefabAsset(root, WineGlassItemPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        return AssetDatabase.LoadAssetAtPath<GameObject>(WineGlassItemPath);
    }

    private static GameObject CreateItemPrefab(ItemDef def)
    {
        string path = $"{ItemFolder}/Item_{def.model}.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null && !overwrite) return existing;

        var root = new GameObject($"Item_{def.model}");
        try
        {
            root.tag = "Interactable";
            SetLayer(root, "Interactable");

            var model = InstantiateModel(def.model, root.transform, def.size);
            if (model == null) return null;

            var bounds = LocalBounds(root);
            var box = root.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = bounds.size;

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkTransform>();
            root.AddComponent<NetworkRigidbody>();
            var grab = root.AddComponent<GrabInteractableObject>();
            grab.gripOffset = Vector3.zero;
            grab.gripEuler = Vector3.zero;
            grab.gripScale = Vector3.one; // 잡을 때 localScale로 그대로 쓰이므로 0이면 안 보임

            SetMissionItem(root.AddComponent<MissionItem>(), def);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);

            // 잡을 때 복제할 프리팹 = 자기 자신
            var contents = PrefabUtility.LoadPrefabContents(path);
            var so = new SerializedObject(contents.GetComponent<GrabInteractableObject>());
            so.FindProperty("heldItemPrefab").objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(contents, path);
            PrefabUtility.UnloadPrefabContents(contents);

            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void SetMissionItem(MissionItem item, ItemDef def)
    {
        var so = new SerializedObject(item);
        so.FindProperty("itemId").stringValue = def.id;
        so.FindProperty("displayName").stringValue = def.name;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // =====================================================================
    // 스테이션
    // =====================================================================

    // 아이템을 올려놓아야 열리는 스테이션 (P1~P3)
    //  Station_X (NetworkObject, MissionStation, BoxCollider=E키 대상)
    //   ├ DropZone (BoxCollider IsTrigger, MissionItemSocket) — Q로 떨어뜨린 아이템 감지
    //   │  └ Placed (모델, 장착 전 비활성)
    //   └ Label (TextMeshPro)
    private static void CreateSocketStation(string name, string missionAsset, ItemDef def, GameObject itemPrefab)
    {
        string path = $"{StationFolder}/{name}.prefab";
        if (!overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

        var root = new GameObject(name);
        try
        {
            root.AddComponent<NetworkObject>();

            var dropZone = new GameObject("DropZone");
            dropZone.transform.SetParent(root.transform, false);
            var trigger = dropZone.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.25f, 0f) * U;
            trigger.size = new Vector3(0.7f, 0.5f, 0.7f) * U;

            var placed = InstantiateModel(def.model, dropZone.transform, def.size);
            if (placed != null)
            {
                placed.name = "Placed";
                placed.SetActive(false);
            }

            var socket = dropZone.AddComponent<MissionItemSocket>();
            var sso = new SerializedObject(socket);
            sso.FindProperty("acceptedItemId").stringValue = def.id;
            sso.FindProperty("acceptedItemName").stringValue = def.name;
            sso.FindProperty("placedVisual").objectReferenceValue = placed;
            sso.FindProperty("itemPrefab").objectReferenceValue = itemPrefab;
            sso.ApplyModifiedPropertiesWithoutUndo();

            // E키 레이캐스트 대상 (레이캐스트는 트리거를 무시하므로 별도 콜라이더)
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.15f, 0f) * U;
            box.size = new Vector3(0.5f, 0.3f, 0.5f) * U;

            AddStation(root, missionAsset, socket, 0.8f * U);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // 바로 상호작용하는 스테이션 (P4 CCTV 마우스, P5 와인병)
    private static void CreateDirectStation(string name, string missionAsset, string modelName, float size)
    {
        string path = $"{StationFolder}/{name}.prefab";
        if (!overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

        var root = new GameObject(name);
        try
        {
            var model = InstantiateModel(modelName, root.transform, size);
            if (model == null) return;

            var bounds = LocalBounds(root);
            var box = root.AddComponent<BoxCollider>();
            box.center = bounds.center;
            // 작은 오브젝트도 조준하기 쉽게 최소 크기 보장
            box.size = Vector3.Max(bounds.size, new Vector3(0.25f, 0.25f, 0.25f) * U);

            AddStation(root, missionAsset, null, bounds.max.y + 0.35f * U);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // =====================================================================
    // 협동 C1 라디오 (라디오 모델이 없어 컴퓨터 스피커 모델로 대체)
    //  Station_C1_Radio (MissionStation, BoxCollider, AudioSource, CoopSlotVisual)
    //   ├ Model
    //   ├ OnLight (켜져 있는 동안만 활성)
    //   └ Label
    // =====================================================================

    public const string RadioModelPath = "Assets/Objects/Computer_Asset/Сomputer devices/Prefabs/Speakers.prefab";

    public static void CreateCoopRadioStation(bool overwriteExisting)
    {
        overwrite = overwriteExisting;
        EnsureFolder(StationFolder);

        const string name = "Station_C1_Radio";
        string path = $"{StationFolder}/{name}.prefab";
        if (!overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

        var root = new GameObject(name);
        try
        {
            var model = InstantiateModelAt(RadioModelPath, root.transform, 0.4f * U);
            if (model == null) return;

            var bounds = LocalBounds(root);
            var box = root.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = Vector3.Max(bounds.size, new Vector3(0.3f, 0.3f, 0.3f) * U);

            var lightGo = new GameObject("OnLight");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, bounds.max.y + 0.05f * U, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.4f, 1f, 0.5f);
            light.range = 1.2f * U;
            light.intensity = 3f;

            var audio = root.AddComponent<AudioSource>();
            audio.loop = true;
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            audio.rolloffMode = AudioRolloffMode.Linear;
            audio.minDistance = 1f * U;
            audio.maxDistance = 8f * U;
            audio.volume = 0.35f;

            var visual = root.AddComponent<CoopSlotVisual>();
            var vso = new SerializedObject(visual);
            vso.FindProperty("activeVisual").objectReferenceValue = lightGo;
            vso.FindProperty("audioSource").objectReferenceValue = audio;
            vso.ApplyModifiedPropertiesWithoutUndo();

            AddStation(root, "Mission_C1_Radio", null, bounds.max.y + 0.35f * U);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void AddStation(GameObject root, string missionAsset, MissionItemSocket socket, float labelHeight)
    {
        root.tag = "Interactable";
        SetLayer(root, "Interactable");

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(root.transform, false);
        labelGo.transform.localPosition = new Vector3(0f, labelHeight, 0f);
        var label = labelGo.AddComponent<TextMeshPro>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 1.2f * U;
        label.rectTransform.sizeDelta = new Vector2(2f, 0.6f) * U;
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font != null) label.font = font;

        var station = root.AddComponent<MissionStation>();
        var mission = AssetDatabase.LoadAssetAtPath<MissionData>($"{MissionEditorTools.DataFolder}/{missionAsset}.asset");
        if (mission == null) Debug.LogWarning($"[Mission] {missionAsset}.asset 없음 → 'Create Default Mission Assets' 먼저 실행");

        var so = new SerializedObject(station);
        so.FindProperty("mission").objectReferenceValue = mission;
        so.FindProperty("activationSocket").objectReferenceValue = socket;
        so.FindProperty("label").objectReferenceValue = label;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // =====================================================================
    // 공통
    // =====================================================================

    // fbx를 자식으로 넣고 가장 긴 변이 size(유닛)가 되도록 크기 조정, 바닥을 y=0에 맞춤
    private static GameObject InstantiateModel(string modelName, Transform parent, float size)
        => InstantiateModelAt($"{ModelFolder}/{modelName}.fbx", parent, size);

    private static GameObject InstantiateModelAt(string assetPath, Transform parent, float size)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (asset == null)
        {
            Debug.LogError($"[Mission] 모델 없음: {assetPath}");
            return null;
        }

        var model = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        model.name = "Model";
        model.transform.SetParent(parent, false);
        model.transform.localPosition = Vector3.zero;

        // 모델에 딸려 온 콜라이더는 제거 (루트 콜라이더 하나로 통일)
        foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

        var b = WorldBounds(model);
        float longest = Mathf.Max(b.size.x, b.size.y, b.size.z);
        if (longest > 1e-5f) model.transform.localScale *= size / longest;

        b = WorldBounds(model);
        model.transform.position += new Vector3(parent.position.x - b.center.x, parent.position.y - b.min.y, parent.position.z - b.center.z);

        SetLayer(model, LayerMask.LayerToName(parent.gameObject.layer));
        return model;
    }

    private static Bounds WorldBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.1f);
        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    private static Bounds LocalBounds(GameObject root)
    {
        var w = WorldBounds(root);
        return new Bounds(root.transform.InverseTransformPoint(w.center), w.size);
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
