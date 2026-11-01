using TMPro;
using UnityEngine;

public class PlayerEntryUI : MonoBehaviour
{
    // 왼쪽 텍스트 (프리팹 기준: 이름/순위 자리)
    [SerializeField] private TMP_Text playerNameText;
    // 오른쪽 텍스트 (프리팹 기준: 등급 자리)
    [SerializeField] private TMP_Text gradeText;

    // 본인의 정보 또는 기존 플레이어 정보 표시용
    public void SetData(
        string playerName,
        bool showGrade,
        int grade)
    {
        playerNameText.text = playerName;

        if (showGrade)
        {
            gradeText.text = $"GRADE {grade}";
        }
        else
        {
            gradeText.text = "GRADE ?";
        }
    }

    // 익명 정보판 표시용: "GRADE 3  -  2명"
    public void SetGradeCountData(int grade, int count)
    {
        playerNameText.text = $"GRADE {grade}";
        gradeText.text = $"{count}명";
    }

    // (이전 방식) 순위 + 등급 표시. 지금은 사용하지 않음
    public void SetAnonymousData(int rank, int grade)
    {
        playerNameText.text = $"RANK {rank}";
        gradeText.text = $"GRADE {grade}";
    }

    public void SetGrade(int grade)
    {
        gradeText.text = $"GRADE {grade}";
    }

    public void HideGrade()
    {
        gradeText.text = "GRADE ?";
    }
}