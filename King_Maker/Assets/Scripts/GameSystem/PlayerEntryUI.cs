using TMPro;
using UnityEngine;

public class PlayerEntryUI : MonoBehaviour
{
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text gradeText;


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


    public void SetGrade(int grade)
    {
        gradeText.text = $"GRADE {grade}";
    }


    public void HideGrade()
    {
        gradeText.text = "GRADE ?";
    }
}