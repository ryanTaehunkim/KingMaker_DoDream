using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// P8 청소 게임 (3차 기획서)
// 책상 위에 흩어진 쓰레기를 마우스로 끌어 오른쪽 쓰레기통에 넣는다. 전부 넣으면 성공.
public class TrashMinigame : MinigameBase
{
    private static readonly Color[] TrashColors =
    {
        new Color(0.85f, 0.85f, 0.80f), // 종이
        new Color(0.55f, 0.75f, 0.90f), // 페트병
        new Color(0.80f, 0.55f, 0.35f), // 상자
        new Color(0.70f, 0.20f, 0.25f), // 캔
        new Color(0.45f, 0.65f, 0.35f), // 병
    };

    private RectTransform area;
    private RectTransform bin;
    private readonly List<RectTransform> trash = new List<RectTransform>();
    private RectTransform dragging;
    private Vector2 dragOffset;
    private int total;
    private int cleaned;

    public override string StatusText => $"{cleaned} / {total}";

    protected override void OnBegin()
    {
        var s = DataAs<TrashMissionData>();
        total = s != null ? s.trashCount : 5;

        // 책상
        area = MinigameUI.Image(Board, "Desk", new Color(0.36f, 0.24f, 0.16f), new Vector2(0f, 0f), new Vector2(760f, 380f)).rectTransform;

        // 쓰레기통 (오른쪽)
        bin = MinigameUI.Image(area, "Bin", new Color(0.22f, 0.26f, 0.24f), new Vector2(270f, -20f), new Vector2(170f, 220f)).rectTransform;
        MinigameUI.Image(bin, "Lid", new Color(0.30f, 0.35f, 0.32f), new Vector2(0f, 115f), new Vector2(190f, 24f));
        MinigameUI.Text(bin, "Label", "TRASH", 26f, new Color(1f, 1f, 1f, 0.6f), Vector2.zero, new Vector2(170f, 40f));

        // 쓰레기: 책상 왼쪽 영역에 겹치지 않게 흩뿌림
        var placed = new List<Vector2>();
        for (int i = 0; i < total; i++)
        {
            Vector2 pos;
            int guard = 0;
            do pos = new Vector2(Random.Range(-330f, 130f), Random.Range(-150f, 140f));
            while (TooClose(pos, placed, 85f) && ++guard < 30);
            placed.Add(pos);

            var size = new Vector2(Random.Range(50f, 80f), Random.Range(45f, 75f));
            var img = MinigameUI.Image(area, $"Trash{i}", TrashColors[i % TrashColors.Length], pos, size);
            img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-35f, 35f));
            trash.Add(img.rectTransform);
        }

        MinigameUI.Text(Board, "Hint", "쓰레기를 끌어서 쓰레기통에 넣으세요", 24f, new Color(1f, 1f, 1f, 0.7f),
            new Vector2(0f, -215f), new Vector2(700f, 36f));
    }

    private static bool TooClose(Vector2 p, List<Vector2> others, float min)
    {
        foreach (var o in others) if (Vector2.Distance(p, o) < min) return true;
        return false;
    }

    protected override void OnTick(float deltaTime)
    {
        var mouse = Mouse.current;
        if (mouse == null || !MinigameUI.MouseLocal(area, out var local)) return;

        if (mouse.leftButton.wasPressedThisFrame) Grab(local);
        if (dragging == null) return;

        if (mouse.leftButton.isPressed) dragging.anchoredPosition = local + dragOffset;
        else Release(local);
    }

    // 좌표 기준 입력 (테스트용으로도 사용)
    public void Grab(Vector2 local)
    {
        if (IsFinished) return;
        // 위에 그려진(나중에 만든) 것부터 검사
        for (int i = trash.Count - 1; i >= 0; i--)
        {
            var t = trash[i];
            if (!t.gameObject.activeSelf) continue;
            if (Vector2.Distance(local, t.anchoredPosition) > Mathf.Max(t.sizeDelta.x, t.sizeDelta.y) * 0.6f) continue;
            dragging = t;
            dragOffset = t.anchoredPosition - local;
            t.SetAsLastSibling();
            return;
        }
    }

    public void Release(Vector2 local)
    {
        if (dragging == null || IsFinished) return;
        var t = dragging;
        dragging = null;

        Vector2 center = t.anchoredPosition;
        Rect binRect = new Rect(bin.anchoredPosition - bin.sizeDelta * 0.5f, bin.sizeDelta);
        binRect.yMax += 40f; // 뚜껑 위로 살짝 넘겨도 인정
        if (!binRect.Contains(center))
        {
            // 책상 밖으로 나가지 않게
            float hw = area.sizeDelta.x * 0.5f - 30f, hh = area.sizeDelta.y * 0.5f - 30f;
            t.anchoredPosition = new Vector2(Mathf.Clamp(center.x, -hw, hw), Mathf.Clamp(center.y, -hh, hh));
            return;
        }

        t.gameObject.SetActive(false);
        cleaned++;
        if (cleaned >= total) Finish(MinigameResult.Success);
    }

    // 테스트/디버그용
    public IReadOnlyList<RectTransform> Trash => trash;
    public Vector2 BinCenter => bin.anchoredPosition;
}
