
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerStateListUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject scoreboardPanel;
    [SerializeField] private Transform content;
    [SerializeField] private PlayerEntryUI playerEntryPrefab;

    private readonly List<PlayerEntryUI> entries =
        new List<PlayerEntryUI>();

    private AnonymousScoreboardManager scoreboardManager;

    private IEnumerator Start()
    {
        // 네트워크 매니저와 점수판 관리자가 준비될 때까지 대기
        while (NetworkManager.Singleton == null)
        {
            yield return null;
        }

        while (scoreboardManager == null ||
               !scoreboardManager.IsSpawned)
        {
            scoreboardManager =
                FindFirstObjectByType<AnonymousScoreboardManager>();

            yield return null;
        }

        scoreboardManager.OnLocalBoardChanged += RefreshList;

        // 시작할 때 점수판 숨김
        RefreshList();
    }

    private void OnDestroy()
    {
        if (scoreboardManager != null)
        {
            scoreboardManager.OnLocalBoardChanged -= RefreshList;
        }

        ClearEntries();
    }

    private void RefreshList()
    {
        ClearEntries();

        if (scoreboardManager == null)
            return;

        // 공개 상태가 아니면 패널 전체를 숨김
        if (!scoreboardManager.LocalBoardVisible)
        {
            if (scoreboardPanel != null)
                scoreboardPanel.SetActive(false);

            return;
        }

        // 공개 상태일 때만 패널 표시
        if (scoreboardPanel != null)
            scoreboardPanel.SetActive(true);

        IReadOnlyList<int> grades =
            scoreboardManager.LocalAnonymousGrades;

        for (int i = 0; i < grades.Count; i++)
        {
            PlayerEntryUI anonymousEntry =
                Instantiate(playerEntryPrefab, content);

            anonymousEntry.SetAnonymousData(
                i + 1,
                grades[i]
            );

            entries.Add(anonymousEntry);
        }
    }

    private void ClearEntries()
    {
        foreach (PlayerEntryUI entry in entries)
        {
            if (entry != null)
                Destroy(entry.gameObject);
        }

        entries.Clear();
    }
}
