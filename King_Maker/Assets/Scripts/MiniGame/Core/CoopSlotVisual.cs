using UnityEngine;

// 협동 미션 자리의 켜짐/꺼짐 표시 (C1 라디오: 소리 + 불빛).
// MissionManager가 동기화하는 자리 상태를 읽기만 하므로 모든 클라이언트에서 똑같이 보인다.
// 같은 GameObject(또는 부모)의 MissionStation을 사용한다.
public class CoopSlotVisual : MonoBehaviour
{
    [Tooltip("자리 수행 전(켜짐)에만 보이는 오브젝트 (예: 라디오 불빛)")]
    [SerializeField] private GameObject activeVisual;
    [Tooltip("자리 수행 후(꺼짐)에만 보이는 오브젝트 (선택)")]
    [SerializeField] private GameObject doneVisual;
    [Tooltip("켜져 있는 동안 재생할 소리. 클립이 비어 있으면 라디오 잡음을 만들어 쓴다.")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private bool generateStaticIfEmpty = true;

    private MissionStation station;
    private bool? lastActive;

    private void Awake()
    {
        station = GetComponentInParent<MissionStation>();
        if (audioSource != null && audioSource.clip == null && generateStaticIfEmpty)
            audioSource.clip = MakeRadioStatic();
    }

    private void Update()
    {
        var manager = MissionManager.Instance;
        bool active = station != null && manager != null && manager.IsDayActive
                      && !manager.TryGetCoopSlot(station.StationId, out _, out float _);
        if (lastActive == active) return;
        lastActive = active;

        if (activeVisual != null) activeVisual.SetActive(active);
        if (doneVisual != null) doneVisual.SetActive(!active);
        if (audioSource != null)
        {
            if (active && !audioSource.isPlaying) audioSource.Play();
            else if (!active && audioSource.isPlaying) audioSource.Stop();
        }
    }

    // 라디오 느낌의 잡음 + 낮은 음 (2초 반복)
    private static AudioClip MakeRadioStatic()
    {
        const int rate = 22050;
        int length = rate * 2;
        var data = new float[length];
        var rng = new System.Random(7);
        float lp = 0f;
        for (int i = 0; i < length; i++)
        {
            float t = (float)i / rate;
            lp = Mathf.Lerp(lp, (float)(rng.NextDouble() * 2 - 1), 0.35f); // 거친 잡음을 부드럽게
            float melody = Mathf.Sin(2f * Mathf.PI * (t < 1f ? 220f : 262f) * t) * 0.25f;
            float wobble = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 3f * t);
            data[i] = (lp * 0.35f + melody * wobble) * 0.6f;
        }
        var clip = AudioClip.Create("RadioStatic", length, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
