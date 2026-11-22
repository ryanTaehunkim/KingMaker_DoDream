using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 벤트 전용 UI (안내 문구 / 진행 게이지 / 이동 선택창).
// 씬에 미리 둘 필요 없이, VentInteractable이 처음 필요할 때 코드로 자동 생성한다.
public class VentUI : MonoBehaviour
{
    private static VentUI instance;
    private static TMP_FontAsset creationFont;

    // 이미 만들어져 있으면 그것을, 없으면 새로 만든다
    public static VentUI Get(TMP_FontAsset font)
    {
        if (instance == null)
        {
            creationFont = font;
            new GameObject("VentUI").AddComponent<VentUI>();
        }

        return instance;
    }

    // 만들지는 않고, 있으면 돌려준다 (숨길 때 사용)
    public static VentUI Existing => instance;

    private static readonly Key[] numberKeys =
    {
        Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
        Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9
    };

    private static readonly Color panelColor = new Color(0f, 0f, 0f, 0.65f);

    private Canvas canvas;
    private TMP_FontAsset font;

    // 안내 문구 + 게이지
    private object promptOwner;
    private GameObject promptRoot;
    private TextMeshProUGUI promptText;
    private GameObject gaugeRoot;
    private RectTransform gaugeFill;

    // 선택창
    private GameObject selectionRoot;
    private bool selecting;
    private int selectionCount;
    private Action<int> onSelect;
    private Action onCancel;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        // 한글이 깨지지 않도록 지정된 폰트, 없으면 씬의 기존 TMP 텍스트 폰트를 쓴다
        font = creationFont;

        if (font == null)
        {
            TMP_Text existing = FindFirstObjectByType<TMP_Text>(FindObjectsInactive.Include);

            if (existing != null)
                font = existing.font;
        }

        BuildCanvas();
        BuildPrompt();

        if (FindFirstObjectByType<EventSystem>() == null)
            Debug.LogWarning("[VentUI] 씬에 EventSystem이 없어 선택 버튼 클릭이 동작하지 않습니다. (숫자키는 동작)");
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        if (!selecting)
            return;

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            Cancel();
            return;
        }

        for (int i = 0; i < selectionCount && i < numberKeys.Length; i++)
        {
            if (keyboard[numberKeys[i]].wasPressedThisFrame)
            {
                Select(i);
                return;
            }
        }
    }

    // =========================================================
    // 안내 문구 / 게이지
    // =========================================================

    public void ShowPrompt(object owner, string text)
    {
        promptOwner = owner;
        promptText.text = text;
        promptRoot.SetActive(true);
    }

    // 문구를 켠 주인만 끌 수 있다 (변기 여러 개가 서로 끄지 않도록)
    public void HidePrompt(object owner)
    {
        if (promptOwner != owner)
            return;

        promptOwner = null;
        promptRoot.SetActive(false);
        gaugeRoot.SetActive(false);
    }

    public void SetGauge(float progress)
    {
        gaugeRoot.SetActive(true);
        gaugeFill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
    }

    // =========================================================
    // 이동 선택창
    // =========================================================

    public void ShowSelection(string title, IList<string> labels, Action<int> selectCallback, Action cancelCallback)
    {
        HideSelection();

        onSelect = selectCallback;
        onCancel = cancelCallback;
        selectionCount = labels.Count;

        const float rowHeight = 64f;
        const float gap = 10f;
        float height = 110f + (labels.Count + 1) * (rowHeight + gap) + 20f;

        Vector2 center = new Vector2(0.5f, 0.5f);
        Vector2 topCenter = new Vector2(0.5f, 1f);

        Image panel = CreateImage("Selection", canvas.transform, center, new Vector2(640f, height), Vector2.zero, panelColor);
        panel.raycastTarget = true;
        selectionRoot = panel.gameObject;

        CreateText(panel.transform, title, 38f, Color.white, topCenter, new Vector2(600f, 60f), new Vector2(0f, -45f));

        for (int i = 0; i < labels.Count; i++)
        {
            int index = i;
            float y = -(100f + rowHeight * 0.5f + i * (rowHeight + gap));

            CreateButton(panel.transform, $"{i + 1}.  {labels[i]}", topCenter,
                new Vector2(560f, rowHeight), new Vector2(0f, y), () => Select(index));
        }

        float cancelY = -(100f + rowHeight * 0.5f + labels.Count * (rowHeight + gap));

        CreateButton(panel.transform, "취소 (ESC)", topCenter,
            new Vector2(560f, rowHeight), new Vector2(0f, cancelY), Cancel);

        selecting = true;
    }

    public void HideSelection()
    {
        selecting = false;
        onSelect = null;
        onCancel = null;

        if (selectionRoot != null)
        {
            Destroy(selectionRoot);
            selectionRoot = null;
        }
    }

    private void Select(int index)
    {
        Action<int> callback = onSelect;
        HideSelection();
        callback?.Invoke(index);
    }

    private void Cancel()
    {
        Action callback = onCancel;
        HideSelection();
        callback?.Invoke();
    }

    // =========================================================
    // UI 만들기 (코드로 직접 생성)
    // =========================================================

    private void BuildCanvas()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();
    }

    private void BuildPrompt()
    {
        Vector2 center = new Vector2(0.5f, 0.5f);

        // 화면 중앙(조준선) 약간 아래
        Image bg = CreateImage("Prompt", canvas.transform, center, new Vector2(560f, 70f), new Vector2(0f, -150f), panelColor);
        promptRoot = bg.gameObject;

        promptText = CreateText(bg.transform, "", 34f, Color.white, center, new Vector2(560f, 70f), Vector2.zero);

        // 게이지 (배경 바 + 안쪽 채움 바)
        Image gaugeBack = CreateImage("Gauge", bg.transform, center, new Vector2(460f, 16f), new Vector2(0f, -55f),
            new Color(0.15f, 0.15f, 0.15f, 0.9f));
        gaugeRoot = gaugeBack.gameObject;

        Image fill = CreateImage("Fill", gaugeBack.transform, center, Vector2.zero, Vector2.zero,
            new Color(0.95f, 0.25f, 0.25f, 1f));

        gaugeFill = fill.rectTransform;
        gaugeFill.anchorMin = new Vector2(0f, 0f);
        gaugeFill.anchorMax = new Vector2(0f, 1f);
        gaugeFill.offsetMin = Vector2.zero;
        gaugeFill.offsetMax = Vector2.zero;

        gaugeRoot.SetActive(false);
        promptRoot.SetActive(false);
    }

    private RectTransform CreateRect(string objectName, Transform parent, Vector2 anchor, Vector2 size, Vector2 position)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        return rect;
    }

    private Image CreateImage(string objectName, Transform parent, Vector2 anchor, Vector2 size, Vector2 position, Color color)
    {
        RectTransform rect = CreateRect(objectName, parent, anchor, size, position);

        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        return image;
    }

    private TextMeshProUGUI CreateText(Transform parent, string content, float fontSize, Color color,
        Vector2 anchor, Vector2 size, Vector2 position)
    {
        RectTransform rect = CreateRect("Text", parent, anchor, size, position);

        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();

        if (font != null)
            text.font = font;

        text.text = content;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;

        return text;
    }

    private void CreateButton(Transform parent, string label, Vector2 anchor, Vector2 size, Vector2 position, Action onClick)
    {
        Image image = CreateImage("Button", parent, anchor, size, position, Color.white);
        image.raycastTarget = true;

        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.25f, 0.25f, 0.32f, 1f);
        colors.highlightedColor = new Color(0.45f, 0.45f, 0.58f, 1f);
        colors.pressedColor = new Color(0.65f, 0.65f, 0.8f, 1f);
        colors.selectedColor = colors.normalColor;
        button.colors = colors;

        button.onClick.AddListener(() => onClick());

        CreateText(image.transform, label, 30f, Color.white, new Vector2(0.5f, 0.5f), size, Vector2.zero);
    }
}