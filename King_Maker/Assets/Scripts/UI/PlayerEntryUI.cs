using TMPro;
using UnityEngine;

public class PlayerEntryUI : MonoBehaviour
{
    [SerializeField] private TMP_Text playerNameText;
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

    // 익명 순위판 표시용
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