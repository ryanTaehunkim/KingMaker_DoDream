using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// P10 금고 패턴 맞추기 (3차 기획서)
// 3x3 점 위에 2초간 보인 패턴 선을 기억했다가, 마우스를 누른 채 같은 순서로 따라 그린다.
public class SafePatternMinigame : MinigameBase
{
    private const float Spacing = 110f;
    private const float HitRadius = 38f;
    private static readonly Color DotColor = new Color(0.55f, 0.55f, 0.60f);
    private static readonly Color ShowColor = new Color(0.30f, 0.85f, 1f);

    private enum Phase { Showing, Input, Result }

    private int length;
    private float showTime;
    private bool reshow;
    private bool failOnWrong;

    private RectTransform pad;
    private readonly Vector2[] dotPos = new Vector2[9];
    private readonly Image[] dots = new Image[9];
    private readonly List<Image> lines = new List<Image>();
    private Image dragLine;
    private TextMeshProUGUI info;

    private int[] pattern;
    private readonly List<int> input = new List<int>();
    private Phase phase;
    private float phaseTimer;
    private bool drawing;

    public override string StatusText => phase == Phase.Showing ? "" : $"{input.Count} / {length}";

    protected override void OnBegin()
    {
        var s = DataAs<SafePatternMissionData>();
        length = s != null ? Mathf.Clamp(s.patternLength, 3, 9) : 5;
        showTime = s != null ? s.showTime : 2f;
        reshow = s == null || s.reshowOnWrong;
        failOnWrong = s != null && s.failOnWrong;

        var safe = MinigameUI.Image(Board, "Safe", new Color(0.18f, 0.20f, 0.24f), new Vector2(0f, 0f), new Vector2(420f, 400f));
        pad = MinigameUI.Image(safe.transform, "Pad", new Color(0.08f, 0.09f, 0.11f), new Vector2(0f, -10f), new Vector2(340f, 340f)).rectTransform;
        var lineLayer = MinigameUI.Rect(pad, "Lines", Vector2.zero, pad.sizeDelta);

        for (int i = 0; i < 9; i++)
        {
            dotPos[i] = new Vector2((i % 3 - 1) * Spacing, (1 - i / 3) * Spacing);
            dots[i] = MinigameUI.Image(pad, $"Dot{i}", DotColor, dotPos[i], new Vector2(34f, 34f));
        }
        for (int i = 0; i < 8; i++)
        {
            var l = MinigameUI.Line(lineLayer, $"Line{i}", ShowColor, 12f);
            l.gameObject.SetActive(false);
            lines.Add(l);
        }
        dragLine = MinigameUI.Line(lineLayer, "DragLine", new Color(1f, 1f, 1f, 0.5f), 10f);
        dragLine.gameObject.SetActive(false);

        info = MinigameUI.Text(Board, "Info", "", 26f, new Color(1f, 1f, 1f, 0.8f), new Vector2(0f, -225f), new Vector2(700f, 40f));

        pattern = MakePattern(length);
        StartShowing();
    }

    // 인접한 점(대각선 포함)으로만 이어지는 패턴 — 건너뛰는 선이 없어 따라 그리기 쉽다
    private static int[] MakePattern(int len)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            var result = new List<int> { Random.Range(0, 9) };
            while (result.Count < len)
            {
                int last = result[result.Count - 1];
                var options = new List<int>();
                for (int n = 0; n < 9; n++)
                {
                    if (result.Contains(n)) continue;
                    if (Mathf.Abs(n % 3 - last % 3) <= 1 && Mathf.Abs(n / 3 - last / 3) <= 1) options.Add(n);
                }
                if (options.Count == 0) break;
                result.Add(options[Random.Range(0, options.Count)]);
            }
            if (result.Count == len) return result.ToArray();
        }
        return new[] { 0, 1, 2, 5, 8 }; // 안전장치
    }

    private void StartShowing()
    {
        phase = Phase.Showing;
        phaseTimer = showTime;
        input.Clear();
        DrawPath(pattern, ShowColor);
        info.text = "패턴을 기억하세요";
    }

    private void StartInput()
    {
        phase = Phase.Input;
        input.Clear();
        DrawPath(System.Array.Empty<int>(), ShowColor);
        info.text = "마우스를 누른 채 같은 패턴을 그리세요";
    }

    private void DrawPath(IReadOnlyList<int> path, Color color)
    {
        for (int i = 0; i < 9; i++) dots[i].color = DotColor;
        foreach (int d in path) dots[d].color = color;
        for (int i = 0; i < lines.Count; i++)
        {
            bool on = i < path.Count - 1;
            lines[i].gameObject.SetActive(on);
            if (!on) continue;
            lines[i].color = color;
            MinigameUI.SetLine(lines[i], dotPos[path[i]], dotPos[path[i + 1]]);
        }
    }

    protected override void OnTick(float deltaTime)
    {
        if (phase == Phase.Showing || phase == Phase.Result)
        {
            phaseTimer -= deltaTime;
            if (phaseTimer > 0f) return;
            if (phase == Phase.Showing) StartInput();
            else if (reshow) StartShowing();
            else StartInput();
            return;
        }

        var mouse = Mouse.current;
        if (mouse == null || !MinigameUI.MouseLocal(pad, out var local)) return;

        if (mouse.leftButton.wasPressedThisFrame) { drawing = true; input.Clear(); }
        if (!drawing) return;

        if (mouse.leftButton.isPressed)
        {
            Touch(local);
            if (input.Count > 0)
            {
                dragLine.gameObject.SetActive(true);
                MinigameUI.SetLine(dragLine, dotPos[input[input.Count - 1]], local);
            }
        }
        else
        {
            drawing = false;
            Submit();
        }
    }

    // 좌표 기준 입력 (테스트용으로도 사용): 점 근처를 지나면 경로에 추가
    public void Touch(Vector2 local)
    {
        if (phase != Phase.Input) return;
        for (int i = 0; i < 9; i++)
        {
            if (input.Contains(i) || Vector2.Distance(local, dotPos[i]) > HitRadius) continue;
            input.Add(i);
            DrawPath(input, Color.white);
            return;
        }
    }

    public void Submit()
    {
        if (phase != Phase.Input || IsFinished) return;
        dragLine.gameObject.SetActive(false);
        if (input.Count == 0) return;

        bool ok = input.Count == pattern.Length;
        for (int i = 0; ok && i < pattern.Length; i++) ok = input[i] == pattern[i];

        if (ok)
        {
            DrawPath(input, MinigameUI.Good);
            info.text = "열렸습니다!";
            Finish(MinigameResult.Success);
            return;
        }

        DrawPath(input, MinigameUI.Bad);
        info.text = reshow ? "틀렸어요! 패턴을 다시 보여줍니다" : "틀렸어요! 다시 그리세요";
        if (failOnWrong) { Finish(MinigameResult.Fail); return; }
        phase = Phase.Result;
        phaseTimer = 0.7f;
    }

    // 테스트/디버그용
    public bool IsInputPhase => phase == Phase.Input;
    public IReadOnlyList<int> Pattern => pattern;
    public Vector2 DotPosition(int i) => dotPos[i];
}
