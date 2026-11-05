using System.Text;
using UnityEngine;

public static class LocalPlayerProfile
{
    public const int MaxLength = 12;
    private const string PrefKey = "KingMaker_Nickname";

    public static string Nickname { get; private set; } = PlayerPrefs.GetString(PrefKey, "");
    public static bool HasNickname => !string.IsNullOrWhiteSpace(Nickname);

    public static bool TrySet(string raw, out string error)
    {
        string clean = Sanitize(raw);
        if (string.IsNullOrEmpty(clean))
        {
            error = "닉네임을 입력하세요.";
            return false;
        }

        Nickname = clean;
        PlayerPrefs.SetString(PrefKey, clean);
        error = null;
        return true;
    }

    // 서버에서도 동일하게 사용하는 정제 함수
    public static string Sanitize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";

        var sb = new StringBuilder();
        //Trim 앞뒤 공백 제거
        foreach (char c in raw.Trim())
        {
            // 제어문자, TMP 리치텍스트 태그 주입(<, >) 차단
            if (char.IsControl(c) || c == '<' || c == '>') continue;
            sb.Append(c);
        }

        string result = sb.ToString().Trim();
        if (result.Length > MaxLength) result = result.Substring(0, MaxLength);
        return result;
    }
}