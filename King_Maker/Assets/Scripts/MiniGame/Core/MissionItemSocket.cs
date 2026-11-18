using Unity.Netcode;
using UnityEngine;

// 아이템을 올려놓으면 미션이 활성화되는 자리 (테이블 위, 레버 구멍 등).
// - 트리거 콜라이더(IsTrigger) 안에 맞는 아이템을 내려놓으면(Q) 자동으로 장착된다.
// - 아이템을 든 채 연결된 MissionStation에 E를 눌러도 장착된다.
// - 장착 여부는 서버가 판정하고 NetworkVariable로 모든 클라이언트에 동기화한다.
// - resetOnNewDay가 켜져 있으면 다음 일차에 비활성으로 돌아가고 아이템을 다시 맵에 생성한다.
// 같은 GameObject(또는 부모)에 NetworkObject가 있어야 온라인에서 동기화된다. 없으면 오프라인 테스트로 동작.
[RequireComponent(typeof(Collider))]
public class MissionItemSocket : NetworkBehaviour
{
    [Header("받는 아이템")]
    [SerializeField] private string acceptedItemId;
    [SerializeField] private string acceptedItemName;

    [Header("표시")]
    [Tooltip("장착되면 켜지는 모델 (예: 테이블 위 와인잔, 기계에 꽂힌 레버)")]
    [SerializeField] private GameObject placedVisual;
    [Tooltip("장착 전에만 보이는 안내 표시 (선택)")]
    [SerializeField] private GameObject emptyVisual;

    [Header("일차 리셋")]
    [SerializeField] private bool resetOnNewDay = true;
    [Tooltip("리셋 시 다시 생성할 아이템 프리팹 (NetworkObject + MissionItem). 비우면 리셋하지 않는다.")]
    [SerializeField] private GameObject itemPrefab;
    [Tooltip("아이템 재생성 위치 후보. 랜덤으로 1곳 선택")]
    [SerializeField] private Transform[] respawnPoints;

    private readonly NetworkVariable<bool> netActivated = new NetworkVariable<bool>(false);
    private bool offlineActivated;

    public bool IsOnline => IsSpawned;
    private bool IsAuthority => !IsOnline || IsServer;
    public bool IsActivated => IsOnline ? netActivated.Value : offlineActivated;
    public string AcceptedItemId => acceptedItemId;
    public string AcceptedItemName => string.IsNullOrEmpty(acceptedItemName) ? acceptedItemId : acceptedItemName;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnEnable()
    {
        MissionEvents.DayStarted += OnDayStarted;
        ApplyVisual(IsActivated);
    }

    private void OnDisable()
    {
        MissionEvents.DayStarted -= OnDayStarted;
    }

    public override void OnNetworkSpawn()
    {
        netActivated.OnValueChanged += OnActivatedChanged;
        ApplyVisual(netActivated.Value);
    }

    public override void OnNetworkDespawn()
    {
        netActivated.OnValueChanged -= OnActivatedChanged;
    }

    private void OnActivatedChanged(bool oldValue, bool newValue) => ApplyVisual(newValue);

    private void ApplyVisual(bool activated)
    {
        if (placedVisual != null) placedVisual.SetActive(activated);
        if (emptyVisual != null) emptyVisual.SetActive(!activated);
    }

    public bool Accepts(MissionItem item) => item != null && item.ItemId == acceptedItemId;

    // =====================================================================
    // 1) 바닥에 내려놓은(Q) 아이템이 트리거 안에 들어온 경우 — 서버에서만 판정
    // =====================================================================

    private void OnTriggerEnter(Collider other) => TryConsumeDropped(other);
    private void OnTriggerStay(Collider other) => TryConsumeDropped(other);

    private void TryConsumeDropped(Collider other)
    {
        if (!IsAuthority || IsActivated) return;

        var item = other.GetComponentInParent<MissionItem>();
        if (!Accepts(item)) return;

        // 손에 들고 있는 아이템(트리거 상태)은 내려놓기 전까지 무시
        var grab = item.GetComponent<GrabInteractableObject>();
        if (grab != null && grab.isHeld.Value) return;

        ServerConsume(item, null);
    }

    // =====================================================================
    // 2) 아이템을 든 채 스테이션에 E — 클라이언트 요청 → 서버 검증
    // =====================================================================

    // 로컬 플레이어가 지금 이 소켓에 맞는 아이템을 들고 있는지 (안내 문구용)
    public bool IsHoldingAcceptedItem(PlayerStateList player)
    {
        var item = GetHeldItem(player);
        return Accepts(item);
    }

    public void RequestPlaceFromHand(PlayerStateList player)
    {
        if (IsActivated || player == null) return;

        if (IsOnline) PlaceFromHandRpc();
        else
        {
            var item = GetHeldItem(player);
            if (Accepts(item)) ServerConsume(item, player);
        }
    }

    [Rpc(SendTo.Server)]
    private void PlaceFromHandRpc(RpcParams rpcParams = default)
    {
        if (IsActivated) return;

        ulong sender = rpcParams.Receive.SenderClientId;
        if (!NetworkManager.ConnectedClients.TryGetValue(sender, out var client) || client.PlayerObject == null) return;
        if (!client.PlayerObject.TryGetComponent(out PlayerStateList player)) return;

        var item = GetHeldItem(player);
        if (!Accepts(item)) return;

        ServerConsume(item, player);
    }

    private static MissionItem GetHeldItem(PlayerStateList player)
    {
        if (player == null || !player.IsSpawned || !player.isHoldingItem.Value) return null;
        var spawn = NetworkManager.Singleton != null ? NetworkManager.Singleton.SpawnManager : null;
        if (spawn == null || !spawn.SpawnedObjects.TryGetValue(player.heldItemNetworkId.Value, out var obj)) return null;
        return obj.GetComponent<MissionItem>();
    }

    // =====================================================================
    // 서버: 아이템 소모 + 활성화
    // =====================================================================

    private void ServerConsume(MissionItem item, PlayerStateList holder)
    {
        if (IsActivated || item == null) return;

        if (holder != null && holder.IsSpawned)
        {
            holder.isHoldingItem.Value = false;
            holder.heldItemNetworkId.Value = 0;
        }

        if (item.TryGetComponent(out NetworkObject netObj) && netObj.IsSpawned) netObj.Despawn(true);
        else Destroy(item.gameObject);

        SetActivated(true);
        Debug.Log($"[MissionItemSocket] {name}: {AcceptedItemName} 장착 → 미션 활성화");
    }

    private void SetActivated(bool value)
    {
        if (IsOnline) netActivated.Value = value;
        else
        {
            offlineActivated = value;
            ApplyVisual(value);
        }
    }

    private void OnDayStarted(int day)
    {
        if (!IsAuthority || !resetOnNewDay || !IsActivated || day <= 1) return;

        if (itemPrefab == null)
        {
            Debug.LogWarning($"[MissionItemSocket] {name}: itemPrefab이 없어 일차 리셋을 건너뜁니다 (계속 활성 상태).", this);
            return;
        }

        SetActivated(false);
        SpawnItem();
    }

    private void SpawnItem()
    {
        Vector3 pos = transform.position + Vector3.up * 0.3f;
        Quaternion rot = Quaternion.identity;
        if (respawnPoints != null && respawnPoints.Length > 0)
        {
            var p = respawnPoints[Random.Range(0, respawnPoints.Length)];
            if (p != null)
            {
                pos = p.position;
                rot = p.rotation;
            }
        }

        var go = Instantiate(itemPrefab, pos, rot);
        if (IsOnline && go.TryGetComponent(out NetworkObject netObj)) netObj.Spawn();
    }
}
