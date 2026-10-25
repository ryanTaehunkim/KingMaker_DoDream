using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
public class FadeManager : MonoBehaviour
{
    public static FadeManager Instance { get; private set; }

    [SerializeField] private Image fadeImage;
    private Coroutine fadeRoutine;

    public bool IsFading { get; private set; }


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        SetAlpha(0f);
    }
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }


    private void SetAlpha(float a)
    {
        Color c = fadeImage.color;
        c.a = a;
        fadeImage.color = c;
    }
    public void FadeOut(float duration, Action onComplete = null)
    {
        StartFade(1f,duration, onComplete);
    }
    public void FadeIn(float duration, Action onComplete = null)
    {
        StartFade(0f,duration, onComplete);
    }
    public void FadeOutIn(float outduration, float holdDuration, float inDuration, Action onBlack = null, Action onComplete = null)
    {
        StopCurrent();
        fadeRoutine = StartCoroutine(FadeOutInRoutine(outduration, holdDuration, inDuration, onBlack, onComplete));
    }

    private void StartFade(float alpha, float duration, Action onComplete)
    {
        StopCurrent();
        fadeRoutine = StartCoroutine(FadeRoutine(alpha, duration, onComplete));
    }

    private void StopCurrent()
    {
        if(fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }
        IsFading = false;
    }

    private IEnumerator FadeRoutine(float alpha, float duration, Action onComplete)
    {
        IsFading = true;

        float startAlpha = fadeImage.color.a;
        float t = 0f;

        if (duration > 0f)
        {
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Lerp(startAlpha, alpha, t / duration));
                yield return null;
            }
        }

        SetAlpha(alpha);
        IsFading = false;
        fadeRoutine = null;

        onComplete?.Invoke();

    }
    private IEnumerator FadeOutInRoutine(float outDuration, float holdDuration, float inDuration, Action onBlack, Action onComplete)
    {
        yield return FadeRoutine(1f, outDuration, null);

        onBlack?.Invoke();

        if (holdDuration > 0f)
            yield return new WaitForSecondsRealtime(holdDuration);

        yield return FadeRoutine(0f, inDuration, null);

        onComplete?.Invoke();
    }
}
