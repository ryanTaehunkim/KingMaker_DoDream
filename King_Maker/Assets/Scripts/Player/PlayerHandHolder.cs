using Unity.Netcode;
using UnityEngine;

public class PlayerHandHolder : NetworkBehaviour
{
    [SerializeField] private Transform handSocket;

    private PlayerStateList state;
    private Transform heldItem;
    private ulong cachedItemId;
    GrabInteractableObject obj;

    private void Awake()
    {
        state = GetComponent<PlayerStateList>();
        
    }

    private void LateUpdate()
    {
        if (!state.isHoldingItem.Value)
        {
            heldItem = null;
            cachedItemId = 0;
            return;
        }

        ulong id = state.heldItemNetworkId.Value;

        // 아이템이 아직 스폰 안 된 클라이언트(늦게 도착)도 있으니 캐시 + 재탐색
        if (heldItem == null || cachedItemId != id)
        {
            if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(id, out var obj))
            {
                heldItem = obj.transform;
                cachedItemId = id;
            }
            else
            {
                heldItem = null;
                return;
            }
        }
        obj = heldItem.GetComponent<GrabInteractableObject>();
        if (obj == null) return;
        Vector3 graboffset = obj.gripOffset;
        //heldItem.SetPositionAndRotation(handSocket.position, handSocket.rotation);
        Vector3 worldPos = handSocket.TransformPoint(graboffset);
        Quaternion worldRotation = handSocket.rotation * Quaternion.Euler(obj.gripEuler);

        heldItem.SetPositionAndRotation(worldPos, worldRotation);
    }
}