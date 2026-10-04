using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// P5 코르크 병 따기: 움직이는 코르크가 목표 구간 안에 들어왔을 때 스페이스바를 누른다.
public class CorkMinigame : MinigameBase
{
    private const float TrackWidth = 760f;

    private RectTransform marker;
    private RectTransform zone;
    private Image zoneImage;
    private Image[] pips;
    private TextMeshProUGUI feedback;

    private float speed;
    private float speedUp;
    private float zoneWidth;
    private int requiredHits;
    private bool failOnMiss;
    private float missLockTime;

    private float phase;        // 0~1 왕복 위상
    private float zoneCenter;   // 0~1
    private int hits;
    private float lockTimer;
    private float feedbackTimer;

    public override string StatusText => $"{hits} / {requiredHits}";

    protected override void OnBegin()
    {
        var s = DataAs<CorkMissionData>();
        speed = s != null ? s.barSpeed : 0.7f;
        speedUp = s != null ? s.speedUpPerHit : 0.15f;
        zoneWidth = s != null ? s.zoneWidth : 0.18f;
        requiredHits = s != null ? s.requiredHits : 2;
        failOnMiss = s != null && s.failOnMiss;
        missLockTime = s != null ? s.missLockTime : 0.4f;

        // 병 (단순 도형)
        var bottle = MinigameUI.Image(Board, "Bottle", new Color(0.16f, 0.35f, 0.20f), new Vector2(0f, 70f), new Vector2(90f, 120f));
        MinigameUI.Image(bottle.transform, "Neck", new Color(0.16f, 0.35f, 0.20f), new Vector2(0f, 75f), new Vector2(40f, 50f));
        MinigameUI.Image(bottle.transform, "Cork", new Color(0.72f, 0.52f, 0.30f), new Vector2(0f, 110f), new Vector2(34f, 30f));

        var track = MinigameUI.Image(Board, "Track", MinigameUI.SlotColor, new Vector2(0f, -40f), new Vector2(TrackWidth, 70f));
        zoneImage = MinigameUI.Image(track.transform, "Zone", new Color(0.35f, 0.85f, 0.45f, 0.55f), Vector2.zero, new Vector2(TrackWidth * zoneWidth, 70f));
        zone = zoneImage.rectTransform;
        marker = MinigameUI.Image(track.transform, "Marker", new Color(0.72f, 0.52f, 0.30f), Vector2.zero, new Vector2(22f, 96f)).rectTransform;

        MinigameUI.Text(Board, "Key", "[ SPACE ]", 30f, new Color(1f, 1f, 1f, 0.7f), new Vector2(0f, -120f), new Vector2(400f, 44f));
        feedback = MinigameUI.Text(Board, "Feedback", "", 34f, MinigameUI.Good, new Vector2(260f, 100f), new Vector2(400f, 50f));

        pips = new Image[requiredHits];
        for (int i = 0; i < requiredHits; i++)
        {
            float x = (i - (requiredHits - 1) * 0.5f) * 44f;
            pips[i] = MinigameUI.Image(Board, $"Pip{i}", MinigameUI.SlotColor, new Vector2(x, -180f), new Vector2(30f, 30f));
        }

        phase = Random.value;
        MoveZone();
    }

    protected override void OnTick(float deltaTime)
    {
        phase = (phase + deltaTime * speed) % 1f;
        float t = Mathf.PingPong(phase * 2f, 1f);
        marker.anchoredPosition = new Vector2((t - 0.5f) * TrackWidth, 0f);

        if (feedbackTimer > 0f)
        {
            feedbackTimer -= deltaTime;
            if (feedbackTimer <= 0f) feedback.text = "";
        }

        if (lockTimer > 0f)
        {
            lockTimer -= deltaTime;
            return;
        }

        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame) Press();
    }

    // 스페이스 입력 1회 판정
    public void Press()
    {
        if (IsFinished || lockTimer > 0f) return;

        float t = Mathf.PingPong(phase * 2f, 1f);
        if (Mathf.Abs(t - zoneCenter) <= zoneWidth * 0.5f)
        {
            pips[hits].color = MinigameUI.Good;
            hits++;
            ShowFeedback("좋아요!", MinigameUI.Good);
            if (hits >= requiredHits)
            {
                Finish(MinigameResult.Success);
                return;
            }
            speed += speedUp;
            MoveZone();
        }
        else
        {
            ShowFeedback("빗나감", MinigameUI.Bad);
            if (failOnMiss)
            {
                Finish(MinigameResult.Fail);
                return;
            }
            lockTimer = missLockTime;
        }
    }

    private void MoveZone()
    {
        float half = zoneWidth * 0.5f;
        float newCenter;
        // 직전 위치와 너무 가깝지 않게
        int guard = 0;
        do
        {
            newCenter = Random.Range(half + 0.05f, 1f - half - 0.05f);
        } while (Mathf.Abs(newCenter - zoneCenter) < 0.2f && ++guard < 10);
        zoneCenter = newCenter;
        zone.anchoredPosition = new Vector2((zoneCenter - 0.5f) * TrackWidth, 0f);
    }

    private void ShowFeedback(string text, Color color)
    {
        feedback.text = text;
        feedback.color = color;
        feedbackTimer = 0.5f;
    }
}
