using Unity.Netcode;
using UnityEngine;

// 내 카메라의 Culling Mask에서 Ghost 레이어를 켜고 끈다. (로컬 화면 전용, 네트워크 동기화 불필요)
// 생존 중: Ghost 레이어를 안 보이게 / 사망 후: Ghost 레이어도 보이게
public class PlayerGhostVision : NetworkBehaviour
{
    private const string GhostLayerName = "Ghost";

    private PlayerStateList state;
    private Camera cam;
    private int ghostLayer = -1;
    private int originalMask;
    private bool subscribed;

    private void Awake()
    {
        state = GetComponent<PlayerStateList>();
        ghostLayer = LayerMask.NameToLayer(GhostLayerName);

        if (ghostLayer < 0)
            Debug.LogError("[PlayerGhostVision] 'Ghost' 레이어가 없습니다. Project Settings > Tags and Layers 를 확인하세요.");
    }

    public override void OnNetworkSpawn()
    {
        // 내 캐릭터일 때만 내 카메라를 조정한다
        if (!IsOwner) return;
        if (state == null || ghostLayer < 0) return;

        cam = Camera.main; // MainCamera 태그가 붙은 카메라 (PlayerControlRigid와 동일한 방식)
        if (cam == null)
        {
            Debug.LogWarning("[PlayerGhostVision] MainCamera를 찾지 못했습니다.");
            return;
        }

        originalMask = cam.cullingMask;

        state.isAlive.OnValueChanged += OnAliveChanged;
        subscribed = true;

        // 스폰 시점의 현재 상태를 바로 반영
        ApplyMask(state.isAlive.Value);
    }

    public override void OnNetworkDespawn()
    {
        if (subscribed && state != null)
        {
            state.isAlive.OnValueChanged -= OnAliveChanged;
            subscribed = false;
        }

        // 카메라가 아직 살아있다면 원래 마스크로 복구
        if (cam != null)
            cam.cullingMask = originalMask;
    }

    private void OnAliveChanged(bool oldValue, bool newValue)
    {
        ApplyMask(newValue);
    }

    private void ApplyMask(bool alive)
    {
        if (cam == null) return;

        int ghostBit = 1 << ghostLayer;

        if (state.isAlive.Value)
            cam.cullingMask = originalMask & ~ghostBit; // Ghost 숨김
        else
            cam.cullingMask = originalMask | ghostBit;  // Ghost 표시
    }
}