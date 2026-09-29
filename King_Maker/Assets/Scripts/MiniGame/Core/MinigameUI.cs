using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 미니게임 UI를 코드로 만들기 위한 헬퍼.
// 아트 리소스가 들어오기 전까지 단색 도형 + 텍스트로 구성하고, 이후 프리팹에서 교체한다.
public static class MinigameUI
{
    public static TMP_FontAsset Font;

    public static readonly Color PanelColor = new Color(0.10f, 0.09f, 0.12f, 0.97f);
    public static readonly Color SlotColor = new Color(0.20f, 0.18f, 0.24f, 1f);
    public static readonly Color ButtonColor = new Color(0.32f, 0.28f, 0.40f, 1f);
    public static readonly Color Gold = new Color(0.96f, 0.78f, 0.30f, 1f);
    public static readonly Color Good = new Color(0.35f, 0.85f, 0.45f, 1f);
    public static readonly Color Bad = new Color(0.95f, 0.30f, 0.30f, 1f);
    public static readonly Color TextColor = new Color(0.95f, 0.94f, 0.90f, 1f);

    public static RectTransform Rect(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    public static void Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    public static Image Image(Transform parent, string name, Color color, Vector2 pos, Vector2 size, bool raycast = false)
    {
        var rt = Rect(parent, name, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    public static TextMeshProUGUI Text(Transform parent, string name, string text, float fontSize, Color color,
        Vector2 pos, Vector2 size, TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        var rt = Rect(parent, name, pos, size);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (Font != null) tmp.font = Font;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        return tmp;
    }

    public static Button Button(Transform parent, string name, string label, Color color, Vector2 pos, Vector2 size,
        UnityAction onClick, float fontSize = 34f)
    {
        var img = Image(parent, name, color, pos, size, true);
        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        // 스페이스/엔터가 선택된 버튼을 누르지 않도록 내비게이션 비활성화
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        var colors = btn.colors;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.8f);
        btn.colors = colors;
        if (onClick != null) btn.onClick.AddListener(onClick);

        if (!string.IsNullOrEmpty(label))
        {
            var text = Text(img.transform, "Label", label, fontSize, TextColor, Vector2.zero, size);
            Stretch(text.rectTransform);
        }
        return btn;
    }

    // 좌측 기준으로 채워지는 게이지. SetGauge()로 0~1 값을 반영한다.
    public static RectTransform Gauge(Transform parent, string name, Vector2 pos, Vector2 size, Color back, Color fill, out Image fillImage)
    {
        var bg = Image(parent, name, back, pos, size);
        fillImage = Image(bg.transform, "Fill", fill, Vector2.zero, Vector2.zero);
        var frt = fillImage.rectTransform;
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = new Vector2(0f, 1f);
        frt.offsetMin = frt.offsetMax = Vector2.zero;
        return bg.rectTransform;
    }

    public static void SetGauge(Image fillImage, float value01)
    {
        var rt = fillImage.rectTransform;
        rt.anchorMax = new Vector2(Mathf.Clamp01(value01), 1f);
    }
}
