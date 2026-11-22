using System.Collections;
using System.Collections.Generic;
using EZPZGames.FunctionalBathroom;
using TMPro;
using Unity.Netcode;
using UnityEngine;

// 변기 벤트: 조커 전용. 변기를 바라보고 E를 길게 누르면 이동할 변기를 고를 수 있다.
// - 씬에 있는 VentInteractable끼리 자동으로 서로 연결된다. (Inspector에서 쌍을 연결할 필요 없음)
// - 이동 흐름: E 길게 누르기(게이지) → 목적지 선택 → 물 내리는 소리 + 암전 → 이동 → 화면 밝아짐
// - 이동은 플레이어 본인 PC에서만 처리한다. (기존 PlayerControlForNetwork.TeleportTo 사용, 오너 권한 이동)
// - 물 내리는 애니메이션/소리는 같은 오브젝트의 ToiletController.Flush()를 사용한다.
public class VentInteractable : MonoBehaviour, IInteractable
{
    private const int NoChoice = -2;
    private const int Cancelled = -1;

    // 씬에 있는 모든 벤트 (OnEnable/OnDisable에서 자동 등록/해제)
    private static readonly List<VentInteractable> allVents = new List<VentInteractable>();

    // 이 PC에서 벤트 이동이 진행 중인지 (동시에 두 번 실행 방지)
    private static bool isBusy;

    [Header("표시 이름")]
    [Tooltip("선택창에 보일 이름. 비워두면 오브젝트 이름(Toilet, Toilet (1) ...)을 쓴다. 씬의 변기마다 다르게 입력하세요.")]
    [SerializeField] private string ventName;

    [Header("이동 설정")]
    [SerializeField] private float holdDuration = 2f;       // E를 누르고 있어야 하는 시간
    [Tooltip("안내 문구가 뜨는 최대 거리. 플레이어의 Interaction Distance(9)와 같거나 작게.")]
    [SerializeField] private float promptDistance = 9f;
    [Tooltip("누르거나 선택하는 동안 이 거리보다 멀어지면 취소. (이 게임은 10유닛=1m 스케일)")]
    [SerializeField] private float maxHoldDistance = 15f;

    [Header("도착 위치")]
    [Tooltip("이 변기로 이동해 왔을 때 서 있을 위치. 파란 화살표(Z)가 변기 바깥쪽을 향하게 둘 것. 비우면 변기 앞쪽으로 자동 계산.")]
    [SerializeField] private Transform exitPoint;
    [SerializeField] private float fallbackExitDistance = 10f;

    [Header("암전")]
    [SerializeField] private float fadeOutDuration = 0.5f;
    [SerializeField] private float blackHoldDuration = 0.3f;
    [SerializeField] private float fadeInDuration = 0.5f;

    [Header("물 내리는 연출")]
    [Tooltip("비워두면 같은 오브젝트의 ToiletController를 자동으로 사용")]
    [SerializeField] private ToiletController toiletController;

    [Header("UI")]
    [Tooltip("한글이 깨질 때만 지정. 비워두면 씬의 기존 TMP 텍스트 폰트를 사용")]
    [SerializeField] private TMP_FontAsset uiFont;
    [SerializeField] private string promptMessage = "[E] 길게 눌러 벤트 사용";
    [SerializeField] private string holdingMessage = "E를 계속 누르세요";
    [SerializeField] private string selectTitle = "이동할 변기를 선택하세요";

    // 이 변기가 지금 벤트 루틴을 실행 중인지 (오브젝트가 사라질 때 isBusy를 되돌리기 위함)
    private bool runningHere;

    private string DisplayName => string.IsNullOrWhiteSpace(ventName) ? gameObject.name : ventName;
    private VentUI UI => VentUI.Get(uiFont);

    // 에디터에서 Domain Reload를 꺼둔 경우 static 값이 남는 것을 방지
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        allVents.Clear();
        isBusy = false;
    }

    private void Awake()
    {
        if (toiletController == null)
            toiletController = GetComponent<ToiletController>();
    }

    private void OnEnable()
    {
        if (!allVents.Contains(this))
            allVents.Add(this);
    }

    private void OnDisable()
    {
        allVents.Remove(this);

        // 이동 도중 오브젝트가 사라져도 다른 변기가 영원히 막히지 않도록
        if (runningHere)
        {
            runningHere = false;
            isBusy = false;
        }
    }

    // 변기를 바라보고 있으면 안내 문구를 띄운다 (E가 실제로 동작하는 상황과 같은 조건)
    private void Update()
    {
        // 이동 진행 중에는 루틴이 문구와 게이지를 직접 관리한다
        if (isBusy)
            return;

        PlayerStateList player = GetLocalPlayer();

        if (player != null && CanUse(player) && IsAimedAt())
        {
            UI.ShowPrompt(this, promptMessage);
        }
        else if (VentUI.Existing != null)
        {
            VentUI.Existing.HidePrompt(this);
        }
    }

    // 플레이어가 E를 "눌렀을 때" 한 번 호출된다. (PlayerControlForNetwork.TrytoInteract)
    public void Interact(PlayerStateList playerState)
    {
        if (isBusy)
            return;

        if (!CanUse(playerState))
            return;

        if (allVents.Count < 2)
        {
            Debug.LogWarning("[Vent] 이동할 다른 변기가 없습니다.");
            return;
        }

        StartCoroutine(VentRoutine(playerState));
    }

    private static PlayerStateList GetLocalPlayer()
    {
        NetworkManager nm = NetworkManager.Singleton;

        if (nm == null || nm.LocalClient == null || nm.LocalClient.PlayerObject == null)
            return null;

        return nm.LocalClient.PlayerObject.GetComponent<PlayerStateList>();
    }

    // 메인 카메라 정중앙 시선이 이 변기에 닿는지
    private bool IsAimedAt()
    {
        Camera cam = Camera.main;

        if (cam == null)
            return false;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);

        if (!Physics.Raycast(ray, out RaycastHit hit, promptDistance, ~0, QueryTriggerInteraction.Ignore))
            return false;

        return hit.collider.GetComponentInParent<VentInteractable>() == this;
    }

    // 사용 자격: 내 캐릭터 + 생존 + 조커 + 이동 잠금 아님 + 게임 결과 안 나옴
    private bool CanUse(PlayerStateList player)
    {
        if (player == null || !player.IsOwner || !player.IsSpawned)
            return false;

        if (!player.isAlive.Value)
            return false;

        // role은 오너만 읽을 수 있는데, 항상 본인 PC에서 호출되므로 문제 없음
        if (player.role.Value != Role.Joker)
            return false;

        PlayerMovementLock movementLock = player.GetComponent<PlayerMovementLock>();
        if (movementLock != null && movementLock.IsLocked.Value)
            return false;

        if (GameCheckManager.Instance != null &&
            GameCheckManager.Instance.result.Value != GameResult.None)
            return false;

        return true;
    }

    // 누르는 동안, 선택창이 떠 있는 동안 계속 확인할 조건
    private bool CanContinue(PlayerStateList player)
    {
        if (!CanUse(player))
            return false;

        return Vector3.Distance(player.transform.position, transform.position) <= maxHoldDistance;
    }

    private IEnumerator VentRoutine(PlayerStateList player)
    {
        isBusy = true;
        runningHere = true;

        VentUI ui = UI;

        // 1) E를 holdDuration 동안 계속 누르고 있어야 한다 (InputManager.interactIsHeld)
        ui.ShowPrompt(this, holdingMessage);
        ui.SetGauge(0f);

        float held = 0f;

        while (held < holdDuration)
        {
            if (!InputManager.interactIsHeld || !CanContinue(player))
            {
                Finish(ui);
                yield break; // 손을 떼거나 조건이 깨지면 취소
            }

            held += Time.deltaTime;
            ui.SetGauge(held / holdDuration);

            yield return null;
        }

        ui.HidePrompt(this);

        // 2) 목적지 선택
        List<VentInteractable> candidates = GetOtherVents();
        PlayerControlForNetwork control = player.GetComponent<PlayerControlForNetwork>();

        if (candidates.Count == 0 || control == null)
        {
            Debug.LogWarning("[Vent] 목적지 또는 PlayerControlForNetwork를 찾지 못했습니다.");
            Finish(ui);
            yield break;
        }

        // 선택하는 동안 이동/공격 입력을 막고(버튼 클릭이 공격으로 나가지 않게) 커서를 푼다
        InputManager.DeactivatePlayerControls();
        SetCursorLocked(false);

        List<string> labels = new List<string>();
        foreach (VentInteractable vent in candidates)
            labels.Add(vent.DisplayName);

        int choice = NoChoice;

        ui.ShowSelection(selectTitle, labels, index => choice = index, () => choice = Cancelled);

        while (choice == NoChoice)
        {
            // 선택 중에 사망, 잠금, 게임 종료 등이 생기면 취소
            if (!CanContinue(player))
            {
                choice = Cancelled;
                break;
            }

            yield return null;
        }

        ui.HideSelection();
        SetCursorLocked(true);

        if (choice < 0 || choice >= candidates.Count)
        {
            InputManager.ActivatePlayerControls();
            Finish(ui);
            yield break;
        }

        VentInteractable destination = candidates[choice];

        // 3) 물 내리는 연출 + 암전 → 이동 → 밝힘 (입력은 계속 잠긴 상태)
        PlayFlush();

        bool moved = false;
        bool done = false;

        FadeManager fade = FadeManager.Instance;

        if (fade != null)
        {
            // 화면이 완전히 검어진 순간(onBlack)에 이동한다
            fade.FadeOutIn(
                fadeOutDuration,
                blackHoldDuration,
                fadeInDuration,
                () =>
                {
                    moved = true;
                    destination.MovePlayerHere(control);
                },
                () => done = true);

            // 다른 연출(FadeManager 다른 호출)이 끼어들어 콜백이 안 오는 경우를 대비한 시간 제한
            float limit = fadeOutDuration + blackHoldDuration + fadeInDuration + 1f;
            float t = 0f;

            while (!done && t < limit)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        // FadeManager가 없거나 연출이 끊겨서 이동이 안 됐다면 직접 이동
        if (!moved)
            destination.MovePlayerHere(control);

        InputManager.ActivatePlayerControls();
        Finish(ui);
    }

    private void Finish(VentUI ui)
    {
        if (ui != null)
        {
            ui.HidePrompt(this);
            ui.HideSelection();
        }

        runningHere = false;
        isBusy = false;
    }

    // 나를 제외한 변기 목록 (이름순으로 정렬해서 항상 같은 순서로 보이게 한다)
    private List<VentInteractable> GetOtherVents()
    {
        List<VentInteractable> list = new List<VentInteractable>();

        foreach (VentInteractable vent in allVents)
        {
            if (vent != null && vent != this)
                list.Add(vent);
        }

        list.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.Ordinal));

        return list;
    }

    private void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    // "이 변기"의 출구로 플레이어를 이동시킨다
    private void MovePlayerHere(PlayerControlForNetwork control)
    {
        Vector3 position;
        Quaternion rotation;

        if (exitPoint != null)
        {
            position = exitPoint.position;
            rotation = exitPoint.rotation;
        }
        else
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            forward.Normalize();

            position = transform.position + forward * fallbackExitDistance;
            rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        control.TeleportTo(position, rotation);
    }

    // 물 내리는 애니메이션 + 소리 (출발하는 변기에서 재생)
    private void PlayFlush()
    {
        if (toiletController == null)
            return;

        // 애니메이션 끝 이벤트가 빠져 있어 isFlushing이 true로 남아도 다시 재생되도록 먼저 초기화
        toiletController.CancelFlush();
        toiletController.Flush();
    }
}