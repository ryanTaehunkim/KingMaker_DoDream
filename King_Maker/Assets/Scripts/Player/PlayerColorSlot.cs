using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerSlotId : NetworkBehaviour
{
    // 서버 전용: 사용 중인 번호 모음
    private static readonly HashSet<int> usedSlots = new HashSet<int>();

    // 서버만 쓰고 모두 읽음
    public NetworkVariable<int> slot = new NetworkVariable<int>(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    private SkinnedMeshRenderer bodyRenderer;
    [SerializeField] private Material[] colorMaterials;
    private void Awake()
    {
        bodyRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
    }
    public override void OnNetworkSpawn()
    {
        slot.OnValueChanged += OnSlotChanged;

        if (IsServer)
        {
            slot.Value = AcquireSlot();
            Debug.Log($"[Slot][Server] Client {OwnerClientId} 번호 배정 = {slot.Value}");
        }
        else
        {
            // 클라이언트: 스폰 시점에 이미 값이 들어와 있을 수 있음
            Debug.Log($"[Slot][Client] Client {OwnerClientId} 스폰 시 번호 = {slot.Value}");
        }
        ApplyColor(slot.Value);
    }

    public override void OnNetworkDespawn()
    {
        slot.OnValueChanged -= OnSlotChanged;

        if (IsServer && slot.Value >= 0)
        {
            usedSlots.Remove(slot.Value);
            Debug.Log($"[Slot][Server] Client {OwnerClientId} 번호 {slot.Value} 반납");
        }
    }

    private void OnSlotChanged(int oldValue, int newValue)
    {
        Debug.Log($"[Slot] Client {OwnerClientId} 번호 변경: {oldValue} -> {newValue}");
    }

    // 0부터 시작해서 비어 있는 가장 작은 번호를 반환
    private int AcquireSlot()
    {
        int i = 0;
        while (!usedSlots.Add(i))
            i++;
        return i;
    }
    private void ApplyColor(int index)
    {
        if (index < 0 || index >= colorMaterials.Length) return;
        bodyRenderer.sharedMaterial = colorMaterials[index];
    }
}