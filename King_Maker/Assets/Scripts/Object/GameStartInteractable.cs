using TMPro;
using Unity.Netcode;
using UnityEngine;

// 대기 씬의 게임 시작 오브젝트. 호스트만 E로 시작할 수 있다.
// (씬 전환은 서버만 할 수 있으므로 IsServer인 호스트에서만 동작 = 서버 권한 유지)
public class GameStartInteractable : MonoBehaviour, IInteractable
{
    [Header("안내 문구")]
    [SerializeField] private float promptDistance = 9f;
    [SerializeField] private string hostPrompt = "[E] 게임 시작";
    [SerializeField] private string clientPrompt = "방장이 게임을 시작할 때까지 기다려 주세요.";
    [Tooltip("대기 씬에는 기존 TMP 텍스트가 없을 수 있으니 한글 TMP 폰트 에셋을 꼭 지정하세요.")]
    [SerializeField] private TMP_FontAsset uiFont;

    private void Awake()
    {
        // 자식 콜라이더가 태그 없이 시선을 가로채지 않도록 (변기와 같은 처리)
        gameObject.tag = "Interactable";

        foreach (Collider col in GetComponentsInChildren<Collider>(true))
            col.gameObject.tag = "Interactable";
    }

    private void Update()
    {
        NetworkManager nm = NetworkManager.Singleton;

        bool show =
            nm != null && nm.IsListening &&
            nm.LocalClient != null && nm.LocalClient.PlayerObject != null &&
            IsAimedAt();

        if (show)
        {
            string text = hostPrompt;

            if (!nm.IsServer)
            {
                text = clientPrompt;
            }
            else if (WaitingRoomManager.Instance != null)
            {
                string reason;

                if (!WaitingRoomManager.Instance.CanStart(out reason))
                    text = reason;
            }

            VentUI.Get(uiFont).ShowPrompt(this, text);
        }
        else if (VentUI.Existing != null)
        {
            VentUI.Existing.HidePrompt(this);
        }
    }

    // 플레이어가 E를 눌렀을 때 (PlayerControlForNetwork.TrytoInteract)
    public void Interact(PlayerStateList playerState)
    {
        if (playerState == null || !playerState.IsOwner)
            return;

        NetworkManager nm = NetworkManager.Singleton;

        // 호스트(서버)만 시작할 수 있다
        if (nm == null || !nm.IsServer)
            return;

        if (WaitingRoomManager.Instance == null)
        {
            Debug.LogError("[WaitingRoom] 씬에 WaitingRoomManager가 없습니다.");
            return;
        }

        WaitingRoomManager.Instance.ServerStartGame();
    }

    private bool IsAimedAt()
    {
        Camera cam = Camera.main;

        if (cam == null)
            return false;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);

        if (!Physics.Raycast(ray, out RaycastHit hit, promptDistance, ~0, QueryTriggerInteraction.Ignore))
            return false;

        return hit.collider.GetComponentInParent<GameStartInteractable>() == this;
    }
}