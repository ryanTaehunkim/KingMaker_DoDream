using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); // 중복 개체는 자식(InputManager 등)까지 통째로 삭제
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void LeaveLobby()
    {
        Destroy(gameObject); // 로비에서 나갈 때 명시적으로 호출
    }
}