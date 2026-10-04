using System.Collections.Generic;
using TMPro;
using UnityEngine;

// 맵에 배치되는 미션 상호작용 오브젝트.
// PlayerControlForNetwork가 "Interactable" 태그 + IInteractable로 E키 상호작용을 전달한다.
// 점유·쿨다운·클리어 판정은 서버(MissionManager)가 하고, 스테이션은 요청과 표시만 담당한다.
[RequireComponent(typeof(Collider))]
public class MissionStation : MonoBehaviour, IInteractable
{
    private static readonly Dictionary<int, MissionStation> registry = new Dictionary<int, MissionStation>();
    public static IReadOnlyDictionary<int, MissionStation> All => registry;
    public static bool TryGet(int stationId, out MissionStation station) => registry.TryGetValue(stationId, out station);

    [SerializeField] private MissionData mission;
    [Tooltip("씬 안에서 고유한 번호. 모든 클라이언트에서 같아야 한다. (메뉴: KingMaker/Missions/Assign Station IDs)")]
    [SerializeField] private int stationId;

    [Header("표시")]
    [SerializeField] private TextMeshPro label;
    [SerializeField] private Renderer indicator;
    [SerializeField] private Color readyColor = new Color(0.96f, 0.78f, 0.30f);
    [SerializeField] private Color clearedColor = new Color(0.35f, 0.85f, 0.45f);
    [SerializeField] private Color inactiveColor = new Color(0.4f, 0.4f, 0.4f);

    private float nextRefreshTime;
    private MaterialPropertyBlock block;

    public MissionData Mission => mission;
    public int StationId => stationId;

    private void Reset()
    {
        gameObject.tag = "Interactable";
    }

    private void OnEnable()
    {
        if (registry.TryGetValue(stationId, out var other) && other != null && other != this)
        {
            Debug.LogError($"[MissionStation] stationId {stationId} 중복: {other.name}, {name}", this);
            return;
        }
        registry[stationId] = this;
    }

    private void OnDisable()
    {
        if (registry.TryGetValue(stationId, out var s) && s == this) registry.Remove(stationId);
    }

    public void Interact(PlayerStateList playerState)
    {
        if (MissionManager.Instance == null)
        {
            Debug.LogWarning("[MissionStation] 씬에 MissionManager가 없습니다.");
            return;
        }
        MissionManager.Instance.RequestStart(this, playerState);
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefreshTime) return;
        nextRefreshTime = Time.unscaledTime + 0.25f;
        RefreshDisplay();
    }

    private void LateUpdate()
    {
        // 라벨이 로컬 카메라를 바라보게
        if (label == null) return;
        var cam = Camera.main;
        if (cam != null) label.transform.rotation = Quaternion.LookRotation(label.transform.position - cam.transform.position);
    }

    private void RefreshDisplay()
    {
        if (mission == null) return;
        var manager = MissionManager.Instance;
        bool dayActive = manager != null && manager.IsDayActive;
        bool cleared = manager != null && manager.IsClearedLocally(mission.missionId);

        Color color;
        string text;
        if (!dayActive)
        {
            color = inactiveColor;
            text = $"{mission.displayName}\n<size=70%>대기 중</size>";
        }
        else if (cleared)
        {
            color = clearedColor;
            text = $"{mission.displayName}\n<size=70%>오늘은 완료</size>";
        }
        else
        {
            color = readyColor;
            text = mission.RequiresTool
                ? $"{mission.displayName}\n<size=70%>[E] 시작 ({mission.requiredToolName} 필요)</size>"
                : $"{mission.displayName}\n<size=70%>[E] 시작</size>";
        }

        if (label != null)
        {
            label.text = text;
            label.color = color;
        }
        if (indicator != null)
        {
            block ??= new MaterialPropertyBlock();
            indicator.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            indicator.SetPropertyBlock(block);
        }
    }

#if UNITY_EDITOR
    public void EditorSetStationId(int id) => stationId = id;
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => registry.Clear();
}
