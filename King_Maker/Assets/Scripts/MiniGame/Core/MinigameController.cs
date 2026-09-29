using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// 현재 열린 미니게임 1개를 관리한다. (클라이언트 로컬)
// - 1인칭 입력 잠금/해제, 마우스 커서 전환
// - 주변 시야 제한(화면 어둡게), 제한시간 표시
// - ESC / 피격 시 즉시 취소
public class MinigameController : MonoBehaviour
{
    public static MinigameController Instance { get; private set; }
    public static bool IsBusy => Instance != null && Instance.isOpen;

    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private int sortingOrder = 500;
    [SerializeField] private Vector2 panelSize = new Vector2(980f, 700f);
    [Tooltip("성공/실패 문구를 보여준 뒤 창을 닫기까지 걸리는 시간")]
    [SerializeField] private float resultDisplayTime = 0.9f;

    private Canvas canvas;
    private GameObject windowRoot;
    private Image dim;
    private RectTransform content;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI ruleText;
    private TextMeshProUGUI statusText;
    private TextMeshProUGUI timerText;
    private Image timerFill;
    private GameObject resultRoot;
    private TextMeshProUGUI resultText;
    private TextMeshProUGUI toastText;

    private MinigameBase current;
    private MissionData currentData;
    private Action<MinigameResult, float> onResult;
    private PlayerStateList watchedPlayer;
    private MinigameCancelReason cancelReason;
    private bool isOpen;
    private float toastHideTime;
    private CursorLockMode prevLockMode;
    private bool prevCursorVisible;

    public MissionData CurrentMission => isOpen ? currentData : null;

    public static MinigameController GetOrCreate()
    {
        if (Instance != null) return Instance;
        var found = FindFirstObjectByType<MinigameController>();
        if (found != null) return found;
        return new GameObject("MinigameController").AddComponent<MinigameController>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        MinigameUI.Font = font != null ? font : TMP_Settings.defaultFontAsset;
        BuildUI();
        windowRoot.SetActive(false);
        toastText.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        if (isOpen) SetPlayerInputLocked(false);
        UnwatchPlayer();
        Instance = null;
    }

    // 미니게임을 연다. onResult는 미니게임이 끝나는 즉시(결과 문구 표시 전) 호출된다.
    public bool Open(MissionData data, PlayerStateList player, Action<MinigameResult, float> onResult)
    {
        if (isOpen || data == null) return false;
        if (data.minigamePrefab == null)
        {
            Debug.LogError($"[Minigame] {data.name}: minigamePrefab이 지정되지 않았습니다.");
            return false;
        }

        currentData = data;
        this.onResult = onResult;
        cancelReason = MinigameCancelReason.None;

        current = Instantiate(data.minigamePrefab, content);
        var rt = current.transform as RectTransform;
        if (rt == null) rt = current.gameObject.AddComponent<RectTransform>();
        MinigameUI.Stretch(rt);
        current.OnFinished += HandleFinished;

        dim.color = new Color(0f, 0f, 0f, data.surroundDim);
        titleText.text = data.displayName;
        ruleText.text = data.ruleText;
        resultRoot.SetActive(false);
        windowRoot.SetActive(true);
        isOpen = true;

        EnsureEventSystem();
        SetPlayerInputLocked(true);
        WatchPlayer(player);

        MissionEvents.RaiseLocalMinigameOpened(data);
        current.Begin(data);
        return true;
    }

    public void CancelCurrent(MinigameCancelReason reason)
    {
        if (!isOpen || current == null || current.IsFinished) return;
        cancelReason = reason;
        current.Cancel();
    }

    public void ShowToast(string message, float duration = 2.2f)
    {
        toastText.text = message;
        toastText.gameObject.SetActive(true);
        toastHideTime = Time.unscaledTime + duration;
    }

    private void Update()
    {
        if (toastText.gameObject.activeSelf && Time.unscaledTime >= toastHideTime)
        {
            toastText.gameObject.SetActive(false);
        }

        if (!isOpen || current == null || current.IsFinished) return;

        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            CancelCurrent(MinigameCancelReason.Escape);
            return;
        }

        float limit = Mathf.Max(0.01f, currentData.timeLimit);
        float remain = current.TimeRemaining;
        MinigameUI.SetGauge(timerFill, remain / limit);
        timerFill.color = remain <= 3f ? MinigameUI.Bad : MinigameUI.Gold;
        timerText.text = remain.ToString("0.0");
        statusText.text = current.StatusText;
    }

    private void HandleFinished(MinigameResult result)
    {
        float elapsed = current.Elapsed;
        var callback = onResult;
        onResult = null;
        callback?.Invoke(result, elapsed);

        if (result == MinigameResult.Cancelled)
        {
            string msg = CancelMessage(cancelReason);
            Close(result);
            if (msg != null) ShowToast(msg);
        }
        else
        {
            StartCoroutine(ShowResultThenClose(result));
        }
    }

    private IEnumerator ShowResultThenClose(MinigameResult result)
    {
        resultRoot.SetActive(true);
        resultText.text = result == MinigameResult.Success ? "성공!" : "실패...";
        resultText.color = result == MinigameResult.Success ? MinigameUI.Good : MinigameUI.Bad;
        yield return new WaitForSecondsRealtime(resultDisplayTime);
        if (isOpen) Close(result);
    }

    private void Close(MinigameResult result)
    {
        if (!isOpen) return;
        isOpen = false;

        StopAllCoroutines();
        if (current != null)
        {
            current.OnFinished -= HandleFinished;
            Destroy(current.gameObject);
            current = null;
        }
        windowRoot.SetActive(false);
        UnwatchPlayer();
        SetPlayerInputLocked(false);

        var data = currentData;
        currentData = null;
        MissionEvents.RaiseLocalMinigameClosed(data, result);
    }

    private static string CancelMessage(MinigameCancelReason reason)
    {
        switch (reason)
        {
            case MinigameCancelReason.Damaged: return "공격을 받아 미션이 중단되었습니다.";
            case MinigameCancelReason.DayEnded: return "일차가 종료되어 미션이 중단되었습니다.";
            case MinigameCancelReason.Server: return "미션이 중단되었습니다.";
            default: return null;
        }
    }

    // ---------- 입력 / 피격 감시 ----------

    private void SetPlayerInputLocked(bool locked)
    {
        if (locked)
        {
            prevLockMode = Cursor.lockState;
            prevCursorVisible = Cursor.visible;
            InputManager.DeactivatePlayerControls();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            InputManager.ActivatePlayerControls();
            Cursor.lockState = prevLockMode;
            Cursor.visible = prevCursorVisible;
        }
    }

    private void WatchPlayer(PlayerStateList player)
    {
        UnwatchPlayer();
        watchedPlayer = player;
        if (watchedPlayer != null) watchedPlayer.health.OnValueChanged += OnWatchedHealthChanged;
    }

    private void UnwatchPlayer()
    {
        if (watchedPlayer != null) watchedPlayer.health.OnValueChanged -= OnWatchedHealthChanged;
        watchedPlayer = null;
    }

    private void OnWatchedHealthChanged(float oldValue, float newValue)
    {
        if (newValue < oldValue) CancelCurrent(MinigameCancelReason.Damaged);
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        DontDestroyOnLoad(go);
    }

    // ---------- UI 구성 ----------

    private void BuildUI()
    {
        var canvasGo = new GameObject("MinigameCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        var root = MinigameUI.Rect(canvasGo.transform, "Window", Vector2.zero, Vector2.zero);
        MinigameUI.Stretch(root);
        windowRoot = root.gameObject;

        // 주변 시야 제한 + 뒤쪽 클릭 차단
        dim = MinigameUI.Image(root, "Dim", new Color(0f, 0f, 0f, 0.6f), Vector2.zero, Vector2.zero, true);
        MinigameUI.Stretch(dim.rectTransform);

        var panel = MinigameUI.Image(root, "Panel", MinigameUI.PanelColor, Vector2.zero, panelSize, true).rectTransform;
        float top = panelSize.y * 0.5f;
        float width = panelSize.x;

        titleText = MinigameUI.Text(panel, "Title", "", 44f, MinigameUI.Gold, new Vector2(0f, top - 44f), new Vector2(width - 60f, 60f));
        titleText.fontStyle = FontStyles.Bold;
        ruleText = MinigameUI.Text(panel, "Rule", "", 26f, MinigameUI.TextColor, new Vector2(0f, top - 100f), new Vector2(width - 80f, 50f));

        MinigameUI.Gauge(panel, "Timer", new Vector2(-40f, top - 145f), new Vector2(width - 160f, 16f),
            MinigameUI.SlotColor, MinigameUI.Gold, out timerFill);
        timerText = MinigameUI.Text(panel, "TimerText", "", 26f, MinigameUI.TextColor,
            new Vector2(width * 0.5f - 60f, top - 145f), new Vector2(80f, 36f));

        content = MinigameUI.Rect(panel, "Content", new Vector2(0f, -20f), new Vector2(width - 60f, panelSize.y - 260f));

        statusText = MinigameUI.Text(panel, "Status", "", 28f, MinigameUI.TextColor,
            new Vector2(0f, -top + 50f), new Vector2(width - 300f, 40f));
        MinigameUI.Text(panel, "Hint", "ESC  취소", 22f, new Color(1f, 1f, 1f, 0.5f),
            new Vector2(width * 0.5f - 90f, -top + 30f), new Vector2(160f, 30f), TextAlignmentOptions.Right);

        var result = MinigameUI.Image(panel, "Result", new Color(0f, 0f, 0f, 0.75f), Vector2.zero, Vector2.zero, true);
        MinigameUI.Stretch(result.rectTransform);
        resultRoot = result.gameObject;
        resultText = MinigameUI.Text(result.transform, "ResultText", "", 96f, MinigameUI.Good, Vector2.zero, new Vector2(width, 160f));
        resultText.fontStyle = FontStyles.Bold;

        toastText = MinigameUI.Text(canvasGo.transform, "Toast", "", 32f, MinigameUI.TextColor,
            new Vector2(0f, -300f), new Vector2(1200f, 60f));
        toastText.outlineWidth = 0.2f;
        toastText.outlineColor = Color.black;
    }
}
