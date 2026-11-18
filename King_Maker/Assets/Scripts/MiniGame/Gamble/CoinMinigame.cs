using TMPro;
using UnityEngine;
using UnityEngine.UI;

// G1 동전 앞/뒤: 앞/뒤를 고르고 배팅 → 동전이 돌다가 서버가 정한 면으로 멈춘다
public class CoinMinigame : GambleMinigameBase
{
    private static readonly string[] Faces = { "앞", "뒤" };

    private RectTransform coin;
    private Image coinImage;
    private TextMeshProUGUI coinText;

    protected override string[] Choices => new[] { "앞면", "뒷면" };

    protected override void BuildBoard()
    {
        coinImage = MinigameUI.Image(Board, "Coin", MinigameUI.Gold, new Vector2(0f, 125f), new Vector2(150f, 150f));
        coin = coinImage.rectTransform;
        coinText = MinigameUI.Text(coin, "Face", "?", 64f, new Color(0.3f, 0.22f, 0.05f), Vector2.zero, new Vector2(150f, 150f));
        coinText.fontStyle = FontStyles.Bold;
    }

    protected override void OnSetup(int setup) { }

    protected override void OnReveal(bool win, int outcome, int delta, float t)
    {
        // 앞쪽 75% 동안 회전(가로 폭이 줄었다 늘었다), 이후 결과 면 표시
        if (t < 0.75f)
        {
            float spin = Mathf.Cos(t * 40f);
            coin.localScale = new Vector3(Mathf.Abs(spin), 1f, 1f);
            coinText.text = spin >= 0f ? Faces[0] : Faces[1];
            info.text = "";
            return;
        }
        coin.localScale = Vector3.one;
        coinText.text = Faces[Mathf.Clamp(outcome, 0, 1)];
        info.text = win ? $"맞혔습니다! +{delta} PT" : $"틀렸습니다... {delta} PT";
        info.color = win ? MinigameUI.Good : MinigameUI.Bad;
    }
}
