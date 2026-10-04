using System;
using UnityEngine;

// 모든 미니게임의 부모.
// 새 미니게임은 이 클래스를 상속해 OnBegin()에서 UI를 만들고, 판정 시 Finish()만 호출하면 된다.
// 입력 잠금, 제한시간, 서버 통신, 보상은 프레임워크(MinigameController / MissionManager)가 처리한다.
public abstract class MinigameBase : MonoBehaviour
{
    public event Action<MinigameResult> OnFinished;

    protected MissionData data;
    protected float elapsed;
    private bool finished;

    public MissionData Data => data;
    public float Elapsed => elapsed;
    public bool IsFinished => finished;
    public float TimeRemaining => data != null ? Mathf.Max(0f, data.timeLimit - elapsed) : 0f;

    // 컨트롤러 상단에 표시되는 진행 상황 문구 (예: "2 / 3쌍")
    public virtual string StatusText => string.Empty;

    // 미니게임 UI가 그려질 영역
    protected RectTransform Board => (RectTransform)transform;

    public void Begin(MissionData missionData)
    {
        data = missionData;
        elapsed = 0f;
        finished = false;
        OnBegin();
    }

    protected abstract void OnBegin();

    // 매 프레임 호출 (종료 후에는 호출되지 않음)
    protected virtual void OnTick(float deltaTime) { }

    private void Update()
    {
        if (finished || data == null) return;

        elapsed += Time.deltaTime;
        OnTick(Time.deltaTime);

        if (!finished && elapsed >= data.timeLimit)
        {
            Finish(MinigameResult.Fail);
        }
    }

    public void Cancel() => Finish(MinigameResult.Cancelled);

    protected void Finish(MinigameResult result)
    {
        if (finished) return;
        finished = true;
        OnFinished?.Invoke(result);
    }

    // 파생 클래스에서 설정 데이터를 꺼낼 때 사용. 타입이 다르면 null.
    protected T DataAs<T>() where T : MissionData => data as T;
}
