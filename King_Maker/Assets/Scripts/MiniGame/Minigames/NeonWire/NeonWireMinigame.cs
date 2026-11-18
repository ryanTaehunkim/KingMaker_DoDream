using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// P7 네온사인 선 연결 (3차 기획서)
// 왼쪽 단자에서 마우스를 드래그해 같은 색의 오른쪽 단자에 놓는다. 모든 색을 연결하면 성공.
public class NeonWireMinigame : MinigameBase
{
    private static readonly Color[] Palette =
    {
        new Color(1f, 0.25f, 0.35f),   // 빨강
        new Color(0.25f, 0.6f, 1f),    // 파랑
        new Color(1f, 0.85f, 0.2f),    // 노랑
        new Color(0.3f, 1f, 0.45f),    // 초록
        new Color(1f, 0.35f, 0.95f),   // 분홍
        new Color(0.3f, 1f, 1f),       // 하늘
    };

    private const float LeftX = -260f;
    private const float RightX = 260f;
    private const float GrabRadius = 40f;

    private int count;
    private bool failOnWrong;

    private Vector2[] leftPos;
    private Vector2[] rightPos;
    private int[] rightColor;        // 오른쪽 i번 단자의 색 번호
    private bool[] connected;        // 색 번호 기준
    private Image[] fixedLines;      // 색 번호 기준
    private Image dragLine;
    private int dragColor = -1;
    private RectTransform area;
    private TextMeshProUGUI feedback;
    private float feedbackTimer;
    private int connectedCount;

    public override string StatusText => $"{connectedCount} / {count}";

    protected override void OnBegin()
    {
        var s = DataAs<NeonWireMissionData>();
        count = s != null ? Mathf.Clamp(s.wireCount, 2, Palette.Length) : 4;
        failOnWrong = s != null && s.failOnWrong;

        var box = MinigameUI.Image(Board, "Box", new Color(0.08f, 0.08f, 0.10f), new Vector2(0f, 0f), new Vector2(700f, 380f));
        area = box.rectTransform;
        MinigameUI.Text(area, "Title", "NEON", 30f, MinigameUI.Gold, new Vector2(0f, 165f), new Vector2(300f, 40f)).fontStyle = FontStyles.Bold;

        leftPos = new Vector2[count];
        rightPos = new Vector2[count];
        rightColor = new int[count];
        connected = new bool[count];
        fixedLines = new Image[count];

        // 오른쪽 색 순서 섞기 (왼쪽과 완전히 같지 않게)
        for (int i = 0; i < count; i++) rightColor[i] = i;
        do Shuffle(rightColor); while (IsIdentity(rightColor));

        // 선을 단자보다 먼저 만들어 단자 아래에 그려지게
        var wires = MinigameUI.Rect(area, "Wires", Vector2.zero, area.sizeDelta);

        float span = Mathf.Min(280f, (count - 1) * 75f);
        for (int i = 0; i < count; i++)
        {
            float y = span * 0.5f - (count > 1 ? span * i / (count - 1) : 0f) - 10f;
            leftPos[i] = new Vector2(LeftX, y);
            rightPos[i] = new Vector2(RightX, y);
            Terminal(area, $"L{i}", leftPos[i], Palette[i]);
            Terminal(area, $"R{i}", rightPos[i], Palette[rightColor[i]]);
            fixedLines[i] = MinigameUI.Line(wires, $"Wire{i}", Palette[i], 14f);
            fixedLines[i].gameObject.SetActive(false);
        }

        dragLine = MinigameUI.Line(wires, "DragWire", Color.white, 14f);
        dragLine.gameObject.SetActive(false);

        MinigameUI.Text(Board, "Hint", "왼쪽 단자를 드래그해 같은 색에 연결하세요", 24f, new Color(1f, 1f, 1f, 0.7f),
            new Vector2(0f, -215f), new Vector2(700f, 36f));
        feedback = MinigameUI.Text(area, "Feedback", "", 30f, MinigameUI.Bad, new Vector2(0f, -165f), new Vector2(400f, 40f));
    }

    private static void Terminal(Transform parent, string name, Vector2 pos, Color color)
    {
        var outer = MinigameUI.Image(parent, name, new Color(0.25f, 0.25f, 0.28f), pos, new Vector2(56f, 56f));
        MinigameUI.Image(outer.transform, "Core", color, Vector2.zero, new Vector2(36f, 36f));
    }

    protected override void OnTick(float deltaTime)
    {
        if (feedbackTimer > 0f && (feedbackTimer -= deltaTime) <= 0f) feedback.text = "";

        var mouse = Mouse.current;
        if (mouse == null || !MinigameUI.MouseLocal(area, out var local)) return;

        if (mouse.leftButton.wasPressedThisFrame) BeginDrag(local);
        if (dragColor < 0) return;

        if (mouse.leftButton.isPressed) MinigameUI.SetLine(dragLine, leftPos[dragColor], local);
        else EndDrag(local);
    }

    // 좌표 기준 입력 (테스트용으로도 사용)
    public void BeginDrag(Vector2 local)
    {
        if (IsFinished) return;
        for (int i = 0; i < count; i++)
        {
            if (connected[i] || Vector2.Distance(local, leftPos[i]) > GrabRadius) continue;
            dragColor = i;
            dragLine.color = Palette[i];
            dragLine.gameObject.SetActive(true);
            MinigameUI.SetLine(dragLine, leftPos[i], local);
            return;
        }
    }

    public void EndDrag(Vector2 local)
    {
        if (dragColor < 0 || IsFinished) return;
        int color = dragColor;
        dragColor = -1;
        dragLine.gameObject.SetActive(false);

        for (int r = 0; r < count; r++)
        {
            if (Vector2.Distance(local, rightPos[r]) > GrabRadius) continue;
            if (rightColor[r] == color)
            {
                connected[color] = true;
                connectedCount++;
                fixedLines[color].gameObject.SetActive(true);
                MinigameUI.SetLine(fixedLines[color], leftPos[color], rightPos[r]);
                if (connectedCount >= count) Finish(MinigameResult.Success);
            }
            else
            {
                feedback.text = "색이 달라요!";
                feedbackTimer = 0.6f;
                if (failOnWrong) Finish(MinigameResult.Fail);
            }
            return;
        }
    }

    // 테스트/디버그용: 왼쪽 i번 색의 정답 오른쪽 위치
    public Vector2 LeftTerminal(int color) => leftPos[color];
    public Vector2 RightTerminalOf(int color) => rightPos[System.Array.IndexOf(rightColor, color)];
    public int WireCount => count;

    private static void Shuffle(int[] a)
    {
        for (int i = a.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (a[i], a[j]) = (a[j], a[i]);
        }
    }

    private static bool IsIdentity(int[] a)
    {
        for (int i = 0; i < a.Length; i++) if (a[i] != i) return false;
        return true;
    }
}
