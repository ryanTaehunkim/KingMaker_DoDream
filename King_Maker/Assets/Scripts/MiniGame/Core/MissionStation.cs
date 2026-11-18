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

    [Header("활성화 조건 (3차 기획)")]
    [Tooltip("비어 있으면 바로 수행 가능. 지정하면 해당 소켓에 아이템을 올려놓아야 미션이 열린다.")]
    [SerializeField] private MissionItemSocket activationSocket;

    [Header("표시")]
    [SerializeField] private TextMeshPro label;
    [SerializeField] private Renderer indicator;
    [SerializeField] private Color readyColor = new Color(0.96f, 0.78f, 0.30f);
    [SerializeField] private Color clearedColor = new Color(0.35f, 0.85f, 0.45f);
    [SerializeField] private Color inactiveColor = new Color(0.4f, 0.4f, 0.4f);
    [SerializeField] private Color coopWaitingColor = new Color(0.35f, 0.75f, 1f);

    private float nextRefreshTime;
    private MaterialPropertyBlock block;

    [Header("협동 자리")]
    [Tooltip("공유 미니게임에서 이 자리의 역할 번호 (예: Q 전용 0 = Q, 1 = 도우미). -1이면 참가 순서")]
    [SerializeField] private int coopSeat = -1;
    [Tooltip("라벨에 붙는 자리 이름 (예: Q 자리, 도우미 자리)")]
    [SerializeField] private string seatName;

    public int CoopSeat => coopSeat;
    private string Title => string.IsNullOrEmpty(seatName) ? mission.displayName : $"{mission.displayName} · {seatName}";

    public MissionData Mission => mission;
    public int StationId => stationId;
    public MissionItemSocket ActivationSocket => activationSocket;
    public bool IsActivated => activationSocket == null || activationSocket.IsActivated;

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

        // 아직 아이템이 장착되지 않은 스테이션: 들고 있으면 장착, 아니면 안내
        if (!IsActivated)
        {
            if (activationSocket.IsHoldingAcceptedItem(playerState))
                activationSocket.RequestPlaceFromHand(playerState);
            else
                MinigameController.GetOrCreate().ShowToast($"{activationSocket.AcceptedItemName}을(를) 찾아 올려놓아야 합니다.");
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
        else if (!IsActivated)
        {
            color = inactiveColor;
            text = $"{mission.displayName}\n<size=70%>{activationSocket.AcceptedItemName} 필요 — 찾아서 올려놓기</size>";
        }
        else if (mission is CoopMissionData coop && manager.TryGetCoopSlot(stationId, out var slot, out float remaining))
        {
            // 협동: 이 자리는 이미 참가/수행됨 (누가 했든 모두에게 보임)
            string state;
            switch (slot.phase)
            {
                case MissionManager.CoopSlotPhase.Done:
                    color = clearedColor;
                    state = "성공!";
                    break;
                case MissionManager.CoopSlotPhase.Playing:
                    color = coopWaitingColor;
                    state = "진행 중";
                    break;
                default:
                    color = coopWaitingColor;
                    state = coop.mode == CoopMode.SyncAction ? $"{coop.slotDoneText} — {remaining:0.0}초 안에 반대편도!"
                          : coop.mode == CoopMode.SumItems ? $"패널 {slot.value} 올려놓음 — 상대 대기 {remaining:0}초"
                          : $"참가함 — 상대 대기 {remaining:0}초";
                    break;
            }
            text = $"{Title}\n<size=70%>{state}</size>";
        }
        else if (cleared)
        {
            color = clearedColor;
            text = $"{Title}\n<size=70%>오늘은 완료</size>";
        }
        else if (mission is CoopMissionData readyCoop)
        {
            color = readyColor;
            string kind = readyCoop.type == MissionType.Coop ? "인 협동" : "인";
            string extra = readyCoop.mode == CoopMode.SumItems && CoopSumBoard.TryGetTarget(readyCoop.missionId, out int target)
                ? $"\n목표 숫자 {target}" : "";
            text = $"{Title}\n<size=70%>[E] {readyCoop.slotActionText} ({readyCoop.RequiredSlots}{kind}){extra}</size>";
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
