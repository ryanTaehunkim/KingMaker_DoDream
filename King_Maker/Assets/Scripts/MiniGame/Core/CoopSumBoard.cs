using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;

// 협동 C2 산수 미션 보드: 목표 숫자를 정하고 숫자 패널을 맵에 흩어 놓는다. (서버 권한)
// - 일차가 시작되면 남아 있는 패널을 치우고 panelCount개를 새로 뿌린 뒤, 그중 두 개의 합을 목표로 정한다.
// - 목표 숫자는 NetworkVariable로 모든 클라이언트에 표시된다.
// - 판정은 MissionManager(SumItems)가 하고, 사용한 패널은 이 보드가 다시 뿌린다.
// 씬에 NetworkObject와 함께 배치한다.
public class CoopSumBoard : NetworkBehaviour
{
    private static readonly Dictionary<int, CoopSumBoard> registry = new Dictionary<int, CoopSumBoard>();

    [SerializeField] private CoopMissionData mission;
    [Tooltip("숫자별 패널 프리팹. 0번 = 숫자 1, 8번 = 숫자 9 (NetworkObject + MissionItem)")]
    [SerializeField] private GameObject[] panelPrefabs;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private int panelCount = 6;
    [SerializeField] private TextMeshPro targetLabel;

    private readonly NetworkVariable<int> netTarget = new NetworkVariable<int>(0);

    public int Target => netTarget.Value;

    public static bool TryGet(int missionId, out CoopSumBoard board) => registry.TryGetValue(missionId, out board) && board != null;

    public static bool TryGetTarget(int missionId, out int target)
    {
        target = 0;
        if (!TryGet(missionId, out var board) || !board.IsSpawned || board.Target <= 0) return false;
        target = board.Target;
        return true;
    }

    private void OnEnable()
    {
        if (mission != null) registry[mission.missionId] = this;
        MissionEvents.DayStarted += OnDayStarted;
    }

    private void OnDisable()
    {
        if (mission != null && registry.TryGetValue(mission.missionId, out var b) && b == this) registry.Remove(mission.missionId);
        MissionEvents.DayStarted -= OnDayStarted;
    }

    public override void OnNetworkSpawn()
    {
        netTarget.OnValueChanged += OnTargetChanged;
        OnTargetChanged(0, netTarget.Value);
        if (IsServer && netTarget.Value <= 0) ServerResetPanels();
    }

    public override void OnNetworkDespawn()
    {
        netTarget.OnValueChanged -= OnTargetChanged;
    }

    private void OnTargetChanged(int oldValue, int newValue)
    {
        if (targetLabel != null) targetLabel.text = newValue > 0 ? $"목표 숫자\n<size=160%>{newValue}</size>" : "목표 숫자\n?";
    }

    private void OnDayStarted(int day)
    {
        if (IsSpawned && IsServer) ServerResetPanels();
    }

    // 남은 패널(손에 든 것 제외)을 치우고 새로 뿌린 뒤 목표를 정한다
    public void ServerResetPanels()
    {
        if (!IsServer || mission == null || panelPrefabs == null || panelPrefabs.Length == 0) return;

        foreach (var item in FindObjectsByType<MissionItem>(FindObjectsSortMode.None))
        {
            if (item.ItemId != mission.sumItemId) continue;
            var grab = item.GetComponent<GrabInteractableObject>();
            if (grab != null && grab.IsSpawned && grab.isHeld.Value) continue;
            if (item.TryGetComponent(out NetworkObject no) && no.IsSpawned) no.Despawn(true);
        }

        var values = new List<int>();
        int count = Mathf.Max(2, panelCount);
        for (int i = 0; i < count; i++) values.Add(Random.Range(1, panelPrefabs.Length + 1));

        var points = new List<Transform>(spawnPoints ?? new Transform[0]);
        for (int i = 0; i < values.Count; i++)
        {
            Transform p = null;
            if (points.Count > 0)
            {
                int k = Random.Range(0, points.Count);
                p = points[k];
                points.RemoveAt(k); // 같은 자리에 겹치지 않게 (모자라면 랜덤 재사용)
            }
            ServerSpawnPanel(values[i], p);
        }

        // 서로 다른 두 패널의 합 → 반드시 만들 수 있는 목표
        int a = Random.Range(0, values.Count);
        int b = (a + Random.Range(1, values.Count)) % values.Count;
        netTarget.Value = values[a] + values[b];
        Debug.Log($"[CoopSumBoard] 패널 {string.Join(",", values)} → 목표 {netTarget.Value}");
    }

    public void ServerSpawnPanel(int value, Transform at = null)
    {
        if (!IsServer || value < 1 || value > panelPrefabs.Length || panelPrefabs[value - 1] == null) return;
        if (at == null && spawnPoints != null && spawnPoints.Length > 0) at = spawnPoints[Random.Range(0, spawnPoints.Length)];

        Vector3 pos = at != null ? at.position : transform.position + Vector3.up * 5f;
        var go = Instantiate(panelPrefabs[value - 1], pos, at != null ? at.rotation : Quaternion.identity);
        if (go.TryGetComponent(out NetworkObject no)) no.Spawn();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => registry.Clear();
}
