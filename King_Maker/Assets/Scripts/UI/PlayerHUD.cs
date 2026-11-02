using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHUD : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Slider hpBar;
    [SerializeField] private Slider staminaBar;
    [SerializeField] private TMP_Text cardGradeText;
    [SerializeField] private TMP_Text roleLabelText;   // 본인 정체 (조커 / 시민)
    [SerializeField] private TMP_Text dayText;
    [SerializeField] private TMP_Text timeText;

    [Header("정체 표시 색")]
    [SerializeField] private Color citizenColor = new Color(0.45f, 0.85f, 0.55f);
    [SerializeField] private Color jokerColor = new Color(0.95f, 0.25f, 0.25f);
    [SerializeField] private Color unknownColor = new Color(0.8f, 0.8f, 0.8f);

    private PlayerStateList localPlayerState;
    private GameTimeManager gameTimeManager;

    private IEnumerator Start()
    {
        // NetworkManager와 Local Player가 생성될 때까지 기다림
        while (
            NetworkManager.Singleton == null ||
            NetworkManager.Singleton.LocalClient == null ||
            NetworkManager.Singleton.LocalClient.PlayerObject == null
        )
        {
            yield return null;
        }

        // 자신의 PlayerObject에서 PlayerStateList 가져오기
        localPlayerState =
            NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerStateList>();

        if (localPlayerState != null)
        {
            localPlayerState.health.OnValueChanged += OnHealthChanged;
            localPlayerState.stamina.OnValueChanged += OnStaminaChanged;
            localPlayerState.cardGrade.OnValueChanged += OnCardGradeChanged;
            localPlayerState.role.OnValueChanged += OnRoleChanged;

            // 처음 들어왔을 때 현재값 바로 표시
            UpdateHealth(localPlayerState.health.Value);
            UpdateStamina(localPlayerState.stamina.Value);
            UpdateCardGrade(localPlayerState.cardGrade.Value);
            UpdateRole(localPlayerState.role.Value);
        }

        // 게임 시간 관리자 찾기
        gameTimeManager = FindFirstObjectByType<GameTimeManager>();

        if (gameTimeManager != null)
        {
            gameTimeManager.currentDay.OnValueChanged += OnDayChanged;
            gameTimeManager.remainingTime.OnValueChanged += OnTimeChanged;

            UpdateDay(gameTimeManager.currentDay.Value);
            UpdateTime(gameTimeManager.remainingTime.Value);
        }
    }

    private void OnDestroy()
    {
        if (localPlayerState != null)
        {
            localPlayerState.health.OnValueChanged -= OnHealthChanged;
            localPlayerState.stamina.OnValueChanged -= OnStaminaChanged;
            localPlayerState.cardGrade.OnValueChanged -= OnCardGradeChanged;
            localPlayerState.role.OnValueChanged -= OnRoleChanged;
        }

        if (gameTimeManager != null)
        {
            gameTimeManager.currentDay.OnValueChanged -= OnDayChanged;
            gameTimeManager.remainingTime.OnValueChanged -= OnTimeChanged;
        }
    }

    private void OnHealthChanged(float oldValue, float newValue)
    {
        UpdateHealth(newValue);
    }

    private void OnStaminaChanged(float oldValue, float newValue)
    {
        UpdateStamina(newValue);
    }

    private void OnCardGradeChanged(int oldValue, int newValue)
    {
        UpdateCardGrade(newValue);
    }

    private void OnRoleChanged(Role oldValue, Role newValue)
    {
        UpdateRole(newValue);
    }

    private void OnDayChanged(int oldValue, int newValue)
    {
        UpdateDay(newValue);
    }

    private void OnTimeChanged(float oldValue, float newValue)
    {
        UpdateTime(newValue);
    }

    private void UpdateHealth(float value)
    {
        if (hpBar != null)
            hpBar.value = value;
    }

    private void UpdateStamina(float value)
    {
        if (staminaBar != null)
            staminaBar.value = value;
    }

    private void UpdateCardGrade(int value)
    {
        if (cardGradeText != null)
            cardGradeText.text = $"GRADE {value}";
    }

    // 본인 정체 표시 (역할 배정 전에는 "?")
    private void UpdateRole(Role role)
    {
        if (roleLabelText == null)
            return;

        switch (role)
        {
            case Role.Joker:
                roleLabelText.text = "정체: 조커";
                roleLabelText.color = jokerColor;
                break;

            case Role.Citizen:
                roleLabelText.text = "정체: 시민";
                roleLabelText.color = citizenColor;
                break;

            default:
                roleLabelText.text = "정체: ?";
                roleLabelText.color = unknownColor;
                break;
        }
    }

    private void UpdateDay(int value)
    {
        if (dayText != null)
            dayText.text = $"DAY {value}";
    }

    private void UpdateTime(float seconds)
    {
        if (timeText != null)
        {
            int minutes = Mathf.FloorToInt(seconds / 60f);
            int secs = Mathf.FloorToInt(seconds % 60f);

            timeText.text = $"{minutes:00}:{secs:00}";
        }
    }
}