using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// 화면 하단 대사창을 관리한다. (네트워크와 무관, 각 플레이어 PC에서 따로 동작)
//
// 사용 예시
//   DialogueManager.Instance.ShowLine("안녕하세요", 3f);   // 3초 뒤 자동으로 사라짐
//   DialogueManager.Instance.ShowLine("계속 표시");          // 기본 시간(defaultDuration) 뒤 사라짐
//   DialogueManager.Instance.ShowLine("직접 지울 때까지", 0f); // 0이면 ClearLine 전까지 유지
//   DialogueManager.Instance.ShowLines(lines, 2f);          // 여러 줄을 2초 간격으로 차례로 출력
//   DialogueManager.Instance.ClearLine();                   // 지금 화면의 대사만 지움
//   DialogueManager.Instance.ClearHistory();                // 저장해둔 대사 기록 삭제
//   DialogueManager.Instance.ClearAll();                    // 화면 + 기록 전부 삭제
//
// 주의: 이 스크립트는 "항상 켜져 있는" 오브젝트에 붙이고,
//       dialoguePanel은 그 자식(또는 다른 오브젝트)으로 연결하세요.
//       패널 자신에게 붙이면 패널이 꺼질 때 코루틴이 같이 멈춥니다.
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TMP_Text dialogueText;

    [Header("Settings")]
    [SerializeField] private float defaultDuration = 3f;

    // 출력했던 대사 기록 (ClearHistory로 삭제)
    private readonly List<string> history = new List<string>();
    public IReadOnlyList<string> History => history;

    private Coroutine currentRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("[DialogueManager] 중복된 DialogueManager가 있습니다.");
            return;
        }

        Instance = this;
        HideVisual();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // 한 줄 출력. duration < 0 이면 defaultDuration, 0이면 직접 지울 때까지 유지
    public void ShowLine(string text, float duration = -1f)
    {
        StopCurrent();

        if (duration < 0f)
            duration = defaultDuration;

        currentRoutine = StartCoroutine(LineRoutine(text, duration));
    }

    // 여러 줄을 차례로 출력
    public void ShowLines(IList<string> lines, float secondsPerLine = -1f)
    {
        if (lines == null || lines.Count == 0)
            return;

        StopCurrent();

        if (secondsPerLine <= 0f)
            secondsPerLine = defaultDuration;

        // 원본 배열이 바뀌어도 안전하도록 복사
        string[] copy = new string[lines.Count];
        lines.CopyTo(copy, 0);

        currentRoutine = StartCoroutine(LinesRoutine(copy, secondsPerLine));
    }

    // 지금 화면의 대사만 지움 (기록은 유지)
    public void ClearLine()
    {
        StopCurrent();
        HideVisual();
    }

    // 저장된 대사 기록만 삭제
    public void ClearHistory()
    {
        history.Clear();
    }

    // 화면 + 기록 전부 삭제
    public void ClearAll()
    {
        ClearLine();
        ClearHistory();
    }

    // -----------------------------------------------------

    private IEnumerator LineRoutine(string text, float duration)
    {
        Display(text);

        if (duration > 0f)
        {
            yield return new WaitForSeconds(duration);
            HideVisual();
        }

        currentRoutine = null;
    }

    private IEnumerator LinesRoutine(string[] lines, float secondsPerLine)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            Display(lines[i]);
            yield return new WaitForSeconds(secondsPerLine);
        }

        HideVisual();
        currentRoutine = null;
    }

    private void Display(string text)
    {
        history.Add(text);

        if (dialogueText != null)
            dialogueText.text = text;

        if (dialoguePanel != null)
            dialoguePanel.SetActive(true);
    }

    private void HideVisual()
    {
        if (dialogueText != null)
            dialogueText.text = "";

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);
    }

    private void StopCurrent()
    {
        if (currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
            currentRoutine = null;
        }
    }
}