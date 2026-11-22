using UnityEngine;

// 카드 등급의 상한값과 화면 표시 규칙을 한곳에서 관리한다.
// 실제 등급 값(int)은 그대로 쓰고, "화면에 보여줄 때만" 이 함수로 변환한다.
public static class CardGradeUtil
{
    // 등급 상승 상한 (Q). K 달성은 별도 규칙으로 처리한다.
    public const int MaxGrade = 16;

    // 표시 규칙 (3PT당 1단계, 내림)
    //   1 ~ 10  : 숫자 그대로
    //   11 ~ 12 : 10
    //   13 ~ 15 : J
    //   16      : Q
    //   19 이상 : K (나중에 K 달성 처리용으로 미리 넣어 둠)
    public static string ToLabel(int grade)
    {
        if (grade >= 19) return "K";
        if (grade >= 16) return "Q";
        if (grade >= 13) return "J";
        if (grade >= 11) return "10";
        return grade.ToString();
    }
}
