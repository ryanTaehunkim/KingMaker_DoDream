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
    [SerializeField] private TMP_Text dayText;
    [SerializeField] private TMP_Text timeText;

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

            // 처음 들어왔을 때 현재값 바로 표시
            UpdateHealth(localPlayerState.health.Value);
            UpdateStamina(localPlayerState.stamina.Value);
            UpdateCardGrade(localPlayerState.cardGrade.Value);
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

    private void OnDayChanged(int oldValue, int newValue)
    {
        UpdateDay(newValue);
    }

    private void OnTimeChanged(float oldValue, float newValue)
    {
        UpdateTime(newValue);
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