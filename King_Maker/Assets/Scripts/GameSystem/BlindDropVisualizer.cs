using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 탈락자 한 명의 공개 정보 (닉네임, 등급이 떨어진 과정)
public struct DoomedEntry
{
    public string nickname;
    public int oldGrade;
    public int newGrade;

    public DoomedEntry(string nickname, int oldGrade, int newGrade)
    {
        this.nickname = nickname;
        this.oldGrade = oldGrade;
        this.newGrade = newGrade;
    }
}

// 블라인드 드롭(시드 포커 단계)의 임시 연출 UI.
// 이미지 에셋 없이 코드가 Image / TMP 텍스트를 직접 만들어서 보여준다. (각 플레이어 PC에서만 동작)
//
// 연출 순서 (전체 시간을 비율로 나눈다)
//   1) 참가자 자리가 하나씩 나타난다      (0% ~ 20%)
//   2) 보상 카드가 놓이고 한 장씩 뒤집힌다 (20% ~ 60%)
//   3) 탈락자가 한 줄씩 발표된다           (60% ~ 100%)
//
// 사용법: PokerPanel 오브젝트에 이 스크립트를 붙이고,
//         BlindDropManager의 Seed Poker Visualizer 칸에 PokerPanel을 연결한다.
//         (패널이 꺼지면 연출도 자동으로 멈춘다)
public class BlindDropVisualizer : MonoBehaviour
{
    // 연출 단계 비율 (BlindDropManager의 하단 대사도 같은 비율을 쓴다)
    public const float SeatsEndRatio = 0.2f;
    public const float CardsEndRatio = 0.6f;

    [Header("색")]
    [SerializeField] private Color titleColor = Color.white;
    [SerializeField] private Color seatColor = new Color(0.85f, 0.88f, 0.95f, 0.95f);
    [SerializeField] private Color seatTextColor = new Color(0.1f, 0.1f, 0.15f, 1f);
    [SerializeField] private Color cardBackColor = new Color(0.15f, 0.25f, 0.55f, 1f);
    [SerializeField] private Color cardFaceColor = new Color(0.97f, 0.97f, 0.97f, 1f);
    [SerializeField] private Color cardValueColor = new Color(0.1f, 0.1f, 0.1f, 1f);
    [SerializeField] private Color doomedColor = new Color(1f, 0.35f, 0.35f, 1f);
    [SerializeField] private Color noDoomedColor = new Color(0.8f, 0.95f, 0.8f, 1f);

    [Header("크기 (1920 x 1080 기준)")]
    [SerializeField] private Vector2 seatSize = new Vector2(90f, 90f);
    [SerializeField] private float seatGap = 110f;
    [SerializeField] private Vector2 cardSize = new Vector2(150f, 210f);
    [SerializeField] private float cardGap = 190f;
    [SerializeField] private int maxDoomedLines = 4;

    [Header("세로 위치 (패널 위쪽에서 아래로 잰 거리)")]
    [SerializeField] private float titleY = 50f;
    [SerializeField] private float seatY = 160f;
    [SerializeField] private float cardY = 350f;
    [SerializeField] private float doomedY = 500f;
    [SerializeField] private float doomedLineHeight = 40f;

    private RectTransform container;
    private TMP_FontAsset fontAsset;

    private void Awake()
    {
        // 패널 안의 기존 텍스트(PokerText)와 같은 폰트를 쓴다 (한글 폰트가 깨지지 않도록)
        TMP_Text existing = GetComponentInChildren<TMP_Text>(true);

        if (existing != null)
            fontAsset = existing.font;
    }

    // =========================================================
    // 외부에서 호출
    // =========================================================

    public void Play(
        int participantCount,
        int rewardedCount,
        IList<int> cards,
        IList<DoomedEntry> doomed,
        float duration)
    {
        EnsureContainer();
        Clear();

        if (!isActiveAndEnabled)
            return;

        StartCoroutine(PlayRoutine(participantCount, rewardedCount, cards, doomed, duration));
    }

    // 연출을 멈추고 만들어 둔 UI를 지운다
    public void Clear()
    {
        StopAllCoroutines();

        if (container == null)
            return;

        for (int i = container.childCount - 1; i >= 0; i--)
            Destroy(container.GetChild(i).gameObject);
    }

    // =========================================================
    // 연출 진행
    // =========================================================

    private IEnumerator PlayRoutine(
        int participantCount,
        int rewardedCount,
        IList<int> cards,
        IList<DoomedEntry> doomed,
        float duration)
    {
        float seatsTime = duration * SeatsEndRatio;
        float cardsTime = duration * (CardsEndRatio - SeatsEndRatio);
        float doomedTime = duration * (1f - CardsEndRatio);

        TextMeshProUGUI title = CreateText(
            "Title", $"참가자 {participantCount}명", 52f, titleColor, 0f, titleY, 1200f, 70f);

        // 1) 참가자 자리 (누구인지는 표시하지 않는다)
        yield return SeatsRoutine(participantCount, seatsTime);

        // 2) 보상 카드 공개
        if (cards != null && cards.Count > 0)
        {
            title.text = "보상 카드 공개";
            yield return CardsRoutine(cards, cardsTime);
        }
        else
        {
            yield return new WaitForSeconds(cardsTime);
        }

        // 3) 결과 발표  ← 이 부분만 교체
        title.text = "블라인드 드롭 결과";
        yield return DoomedRoutine(rewardedCount, doomed, doomedTime);
    }

    private IEnumerator SeatsRoutine(int count, float time)
    {
        if (count <= 0)
        {
            yield return new WaitForSeconds(time);
            yield break;
        }

        float step = time * 0.7f / count;

        for (int i = 0; i < count; i++)
        {
            float x = (i - (count - 1) * 0.5f) * seatGap;

            Image seat = CreateBox("Seat" + i, seatColor, seatSize, x, seatY);
            CreateChildText(seat.rectTransform, "참가", 26f, seatTextColor);

            StartCoroutine(Pop(seat.rectTransform, 0.2f));

            yield return new WaitForSeconds(step);
        }

        yield return new WaitForSeconds(time * 0.3f);
    }

    private IEnumerator CardsRoutine(IList<int> cards, float time)
    {
        int count = cards.Count;

        Image[] cardImages = new Image[count];
        TMP_Text[] cardLabels = new TMP_Text[count];

        // 뒷면 카드를 차례로 놓는다
        float popStep = time * 0.25f / count;

        for (int i = 0; i < count; i++)
        {
            float x = (i - (count - 1) * 0.5f) * cardGap;

            Image card = CreateBox("Card" + i, cardBackColor, cardSize, x, cardY);
            cardImages[i] = card;
            cardLabels[i] = CreateChildText(card.rectTransform, "?", 80f, Color.white);

            StartCoroutine(Pop(card.rectTransform, 0.2f));

            yield return new WaitForSeconds(popStep);
        }

        // 한 장씩 뒤집어서 값을 공개한다
        float flipStep = time * 0.75f / count;
        const float flipTime = 0.4f;

        for (int i = 0; i < count; i++)
        {
            yield return Flip(cardImages[i], cardLabels[i], cards[i], flipTime);

            float rest = flipStep - flipTime;

            if (rest > 0f)
                yield return new WaitForSeconds(rest);
        }
    }

    private IEnumerator DoomedRoutine(int rewardedCount, IList<DoomedEntry> doomed, float time)
    {
        bool hasDoomed = doomed != null && doomed.Count > 0;
        int shown = hasDoomed ? Mathf.Min(doomed.Count, maxDoomedLines) : 0;
        float step = time / (shown + 1.5f);

        // 기본 정보: 보상 카드를 받은 인원 수 (항상 표시)
        string summary = rewardedCount > 0
            ? $"{rewardedCount}명이 보상 카드를 획득했습니다."
            : "보상 카드를 획득한 사람이 없습니다.";

        TextMeshProUGUI info = CreateText(
            "RewardInfo", summary, 38f, noDoomedColor, 0f, doomedY, 1300f, 50f);

        StartCoroutine(FadeIn(info, 0.4f));
        yield return new WaitForSeconds(step);

        // 탈락자 안내는 탈락자가 있을 때만
        for (int i = 0; i < shown; i++)
        {
            DoomedEntry entry = doomed[i];

            TextMeshProUGUI line = CreateText(
                "Doomed" + i,
                $"{entry.nickname} 님의 등급이 {entry.oldGrade}에서 {entry.newGrade}까지 하락하여 탈락합니다.",
                34f, doomedColor, 0f, doomedY + (i + 1.3f) * doomedLineHeight, 1300f, doomedLineHeight);

            StartCoroutine(FadeIn(line, 0.4f));

            yield return new WaitForSeconds(step);
        }

        if (hasDoomed && doomed.Count > shown)
        {
            TextMeshProUGUI more = CreateText(
                "DoomedMore",
                $"외 {doomed.Count - shown}명 탈락",
                30f, doomedColor, 0f, doomedY + (shown + 1.3f) * doomedLineHeight, 1300f, doomedLineHeight);

            StartCoroutine(FadeIn(more, 0.4f));
        }

        yield return new WaitForSeconds(step * 0.5f);
    }

    // =========================================================
    // 작은 애니메이션
    // =========================================================

    // 0 → 1 크기로 커지며 나타난다
    private IEnumerator Pop(RectTransform rect, float duration)
    {
        rect.localScale = Vector3.zero;

        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;

            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            rect.localScale = Vector3.one * k;

            yield return null;
        }

        rect.localScale = Vector3.one;
    }

    // 투명 → 불투명
    private IEnumerator FadeIn(TMP_Text text, float duration)
    {
        Color target = text.color;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;

            Color c = target;
            c.a = target.a * Mathf.Clamp01(t / duration);
            text.color = c;

            yield return null;
        }

        text.color = target;
    }

    // 카드 뒤집기: 가로로 접었다가 앞면으로 펼친다
    private IEnumerator Flip(Image card, TMP_Text label, int value, float duration)
    {
        RectTransform rect = card.rectTransform;
        float half = duration * 0.5f;

        float t = 0f;

        while (t < half)
        {
            t += Time.deltaTime;
            rect.localScale = new Vector3(1f - Mathf.Clamp01(t / half), 1f, 1f);
            yield return null;
        }

        // 앞면으로 교체
        card.color = cardFaceColor;
        label.text = value.ToString();
        label.color = cardValueColor;
        label.fontSize = 96f;

        t = 0f;

        while (t < half)
        {
            t += Time.deltaTime;
            rect.localScale = new Vector3(Mathf.Clamp01(t / half), 1f, 1f);
            yield return null;
        }

        rect.localScale = Vector3.one;
    }

    // =========================================================
    // UI 만들기 (코드로 직접 생성)
    // =========================================================

    // 연출용 UI를 담는 컨테이너. 기존 텍스트(PokerText)가 위에 보이도록 맨 뒤에 둔다.
    private void EnsureContainer()
    {
        if (container != null)
            return;

        GameObject go = new GameObject("VisualStage", typeof(RectTransform));

        container = go.GetComponent<RectTransform>();
        container.SetParent(transform, false);
        container.anchorMin = Vector2.zero;
        container.anchorMax = Vector2.one;
        container.offsetMin = Vector2.zero;
        container.offsetMax = Vector2.zero;
        container.SetAsFirstSibling();
    }

    // 위쪽 가운데 기준으로 배치되는 텍스트
    private TextMeshProUGUI CreateText(
        string objectName,
        string content,
        float fontSize,
        Color color,
        float x,
        float y,
        float width,
        float height)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(container, false);
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(x, -y);

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();

        if (fontAsset != null)
            text.font = fontAsset;

        text.text = content;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;

        return text;
    }

    // 위쪽 가운데 기준으로 배치되는 단색 사각형
    private Image CreateBox(string objectName, Color color, Vector2 size, float x, float y)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(container, false);
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = new Vector2(x, -y);

        Image image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        return image;
    }

    // 부모 사각형을 가득 채우는 텍스트 (카드 숫자, 자리 문구)
    private TextMeshProUGUI CreateChildText(
        RectTransform parent,
        string content,
        float fontSize,
        Color color)
    {
        GameObject go = new GameObject("Label", typeof(RectTransform));

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();

        if (fontAsset != null)
            text.font = fontAsset;

        text.text = content;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;

        return text;
    }
}