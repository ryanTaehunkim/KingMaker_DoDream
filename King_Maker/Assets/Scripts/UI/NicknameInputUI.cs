using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NicknameInputUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField nicknameInput;
    [SerializeField] private Button confirmButton;
    [SerializeField] private TextMeshProUGUI feedbackText;

    private void Awake()
    {
        nicknameInput.characterLimit = LocalPlayerProfile.MaxLength;
        confirmButton.onClick.AddListener(OnConfirm);

        // 이전에 저장한 닉네임이 있으면 미리 채워 둠 (확정은 다시 눌러야 함)
        if (LocalPlayerProfile.HasNickname)
            nicknameInput.text = LocalPlayerProfile.Nickname;

        if (feedbackText != null) feedbackText.text = "";
    }

    private void OnConfirm()
    {
        if (LocalPlayerProfile.TrySet(nicknameInput.text, out string error))
        {
            nicknameInput.text = LocalPlayerProfile.Nickname; // 정제된 값으로 갱신
            if (feedbackText != null) feedbackText.text = $"닉네임 확정: {LocalPlayerProfile.Nickname}";
        }
        else
        {
            if (feedbackText != null) feedbackText.text = error;
        }
    }
}