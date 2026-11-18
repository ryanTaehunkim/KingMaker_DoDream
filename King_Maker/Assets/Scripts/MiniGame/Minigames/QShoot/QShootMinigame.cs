using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Q 전용 미션 화면
//  Q(역할 0): 화면이 가려져 공이 보이지 않는다. 마우스로 조준점을 움직이고 클릭으로 발사.
//  도우미(역할 1): 공 위치와 Q의 조준점이 보인다. 음성으로 "왼쪽, 조금 위" 식으로 안내.
public class QShootMinigame : CoopMinigameBase
{
    private QShootMissionData d;
    private RectTransform area;
    private RectTransform crosshair;
    private readonly Dictionary<int, Image> targets = new Dictionary<int, Image>();
    private TextMeshProUGUI info, flash;
    private int hits, total = 3;
    private float aimSendTimer;
    private Vector2 lastSent = new Vector2(float.NaN, float.NaN);
    private float flashTimer;

    public bool IsShooter => Role == 0;
    public override string StatusText => $"맞힌 공 {hits} / {total}";

    protected override void OnCoopBegin()
    {
        d = DataAs<QShootMissionData>();
        total = d != null ? d.targetCount : 3;
        Vector2 size = d != null ? d.area : new Vector2(700f, 320f);

        // Q는 완전히 가려진 화면, 도우미는 사격장
        var bg = MinigameUI.Image(Board, "Area", IsShooter ? Color.black : new Color(0.16f, 0.2f, 0.26f), new Vector2(0f, 30f), size);
        area = bg.rectTransform;

        crosshair = MinigameUI.Rect(area, "Crosshair", Vector2.zero, new Vector2(40f, 40f));
        var cc = IsShooter ? new Color(1f, 1f, 1f, 0.9f) : new Color(1f, 0.3f, 0.3f, 0.95f);
        MinigameUI.Image(crosshair, "H", cc, Vector2.zero, new Vector2(40f, 4f));
        MinigameUI.Image(crosshair, "V", cc, Vector2.zero, new Vector2(4f, 40f));

        info = MinigameUI.Text(Board, "Info",
            IsShooter ? "화면이 가려져 있습니다. 도우미의 안내에 따라 마우스로 조준하고 클릭하세요." : "공 위치를 보고 Q에게 말로 안내하세요. (빨간 십자 = Q의 조준점)",
            24f, MinigameUI.Gold, new Vector2(0f, -170f), new Vector2(880f, 60f));
        flash = MinigameUI.Text(Board, "Flash", "", 44f, MinigameUI.Good, new Vector2(0f, 30f), new Vector2(400f, 70f));
        flash.fontStyle = FontStyles.Bold;
    }

    public override void OnServerMessage(CoopMsg msg)
    {
        switch (msg.kind)
        {
            case QShootLogic.Target:
                if (!IsShooter && !targets.ContainsKey(msg.a))
                {
                    float r = d != null ? d.hitRadius : 45f;
                    var t = MinigameUI.Image(area, $"Ball{msg.a}", new Color(0.95f, 0.8f, 0.25f), new Vector2(msg.x, msg.y), new Vector2(r * 2f, r * 2f));
                    t.transform.SetSiblingIndex(0);
                    targets[msg.a] = t;
                }
                break;
            case QShootLogic.Aim:
                if (!IsShooter) crosshair.anchoredPosition = new Vector2(msg.x, msg.y);
                break;
            case QShootLogic.Hit:
                hits = msg.b;
                if (targets.TryGetValue(msg.a, out var img)) img.color = new Color(0.35f, 0.85f, 0.45f, 0.4f);
                Flash("명중!", MinigameUI.Good);
                break;
            case QShootLogic.Miss:
                Flash("빗나감", MinigameUI.Bad);
                break;
        }
    }

    private void Flash(string text, Color color)
    {
        flash.text = text;
        flash.color = color;
        flashTimer = 0.6f;
    }

    protected override void OnTick(float dt)
    {
        if (flashTimer > 0f && (flashTimer -= dt) <= 0f) flash.text = "";
        if (!IsShooter) return;

        var mouse = Mouse.current;
        if (mouse == null || !MinigameUI.MouseLocal(area, out var local)) return;
        Vector2 half = area.sizeDelta * 0.5f;
        local = new Vector2(Mathf.Clamp(local.x, -half.x, half.x), Mathf.Clamp(local.y, -half.y, half.y));
        crosshair.anchoredPosition = local;

        aimSendTimer -= dt;
        if (aimSendTimer <= 0f && local != lastSent)
        {
            aimSendTimer = 0.06f;
            lastSent = local;
            Send(new CoopMsg(QShootLogic.Aim, 0, 0, local.x, local.y));
        }
        if (mouse.leftButton.wasPressedThisFrame) Fire(local);
    }

    public void Fire(Vector2 local)
    {
        if (!IsShooter || IsFinished) return;
        Send(new CoopMsg(QShootLogic.Fire, 0, 0, local.x, local.y));
    }
}
