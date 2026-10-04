using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

// 미션 테스트 씬 전용 디버그 키 (서버/호스트 또는 오프라인에서만 동작)
// F1: 다음 일차 시작 / F2: 일차 종료(컷오프) / F3: 모든 플레이어에게 테스트 도구 지급 / F4: 내 미니게임 피격 취소 테스트
public class MissionDebugKeys : MonoBehaviour
{
    [SerializeField] private string[] testTools = { "cloth" };

    private void Update()
    {
        var keyboard = Keyboard.current;
        var manager = MissionManager.Instance;
        if (keyboard == null || manager == null || !manager.IsAuthority) return;

        if (keyboard.f1Key.wasPressedThisFrame)
        {
            manager.ServerStartDay(manager.CurrentDay + 1);
            Toast($"{manager.CurrentDay}일차 시작");
        }
        if (keyboard.f2Key.wasPressedThisFrame)
        {
            manager.ServerEndDay();
            Toast($"{manager.CurrentDay}일차 종료");
        }
        if (keyboard.f3Key.wasPressedThisFrame)
        {
            GiveTools();
            Toast("테스트 도구 지급");
        }
        if (keyboard.f4Key.wasPressedThisFrame)
        {
            MinigameController.Instance?.CancelCurrent(MinigameCancelReason.Damaged);
        }
    }

    public void GiveTools()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening)
        {
            foreach (var client in nm.ConnectedClientsList)
            {
                if (client.PlayerObject != null) Give(client.PlayerObject.gameObject);
            }
        }
        else
        {
            var ps = FindFirstObjectByType<PlayerStateList>();
            Give(ps != null ? ps.gameObject : gameObject);
        }
    }

    private void Give(GameObject player)
    {
        if (!player.TryGetComponent<MissionToolInventory>(out var inv)) inv = player.AddComponent<MissionToolInventory>();
        foreach (var t in testTools) inv.AddTool(t);
    }

    private static void Toast(string msg) => MinigameController.GetOrCreate().ShowToast(msg);
}
