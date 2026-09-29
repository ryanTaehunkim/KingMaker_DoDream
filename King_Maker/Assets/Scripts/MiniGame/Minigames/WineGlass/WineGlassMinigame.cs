using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// P1 와인잔 닦기: 마우스를 누른 채 문질러 잔 표면의 와인 얼룩을 지운다.
// 얼룩은 텍스처 알파 마스크로 그리고, 픽셀이 "깨끗해지는 순간"에만 카운트를 줄여
// 매 프레임 전체 픽셀을 검사하지 않고 진행률을 계산한다.
public class WineGlassMinigame : MinigameBase
{
    private const int TexSize = 256;
    private const byte CleanThreshold = 25;
    private const float DisplaySize = 400f;

    private static readonly Color32 StainColor = new Color32(115, 12, 32, 255);

    private Texture2D glassTex;
    private Texture2D stainTex;
    private Color32[] stainPixels;
    private RawImage stainImage;
    private RectTransform sponge;

    private int initialDirty;
    private int dirty;
    private float brushRadiusPx;
    private float scrubStrength;
    private float requiredRatio;

    private bool stroking;
    private Vector2 lastPx;
    private bool texDirty;

    private float CleanRatio => initialDirty > 0 ? 1f - dirty / (float)initialDirty : 1f;
    public override string StatusText => $"깨끗함 {Mathf.FloorToInt(CleanRatio * 100f)}%";

    protected override void OnBegin()
    {
        var s = DataAs<WineGlassMissionData>();
        int stainCount = s != null ? s.stainCount : 4;
        Vector2 stainRadius = s != null ? s.stainRadius : new Vector2(0.07f, 0.11f);
        brushRadiusPx = (s != null ? s.brushRadius : 0.08f) * TexSize;
        scrubStrength = s != null ? s.scrubStrength : 0.3f;
        requiredRatio = s != null ? s.requiredCleanRatio : 0.95f;

        glassTex = CreateGlassTexture();
        stainTex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        stainPixels = new Color32[TexSize * TexSize];
        PaintStains(stainCount, stainRadius);
        stainTex.SetPixels32(stainPixels);
        stainTex.Apply(false);

        CreateRawImage("Glass", glassTex);
        stainImage = CreateRawImage("Stains", stainTex);
        stainImage.raycastTarget = true; // 뒤쪽 클릭 차단

        sponge = MinigameUI.Image(Board, "Sponge", new Color(1f, 0.95f, 0.6f, 0.35f), Vector2.zero,
            Vector2.one * (brushRadiusPx * 2f / TexSize * DisplaySize)).rectTransform;
        sponge.gameObject.SetActive(false);
    }

    private RawImage CreateRawImage(string name, Texture tex)
    {
        var rt = MinigameUI.Rect(Board, name, Vector2.zero, new Vector2(DisplaySize, DisplaySize));
        var img = rt.gameObject.AddComponent<RawImage>();
        img.texture = tex;
        img.raycastTarget = false;
        return img;
    }

    private void HandleScrub()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        var rt = stainImage.rectTransform;
        bool inside = RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, mouse.position.ReadValue(), null, out var local)
                      && rt.rect.Contains(local);

        sponge.gameObject.SetActive(inside);
        if (inside) sponge.anchoredPosition = local;

        if (!inside || !mouse.leftButton.isPressed)
        {
            stroking = false;
            FlushTexture();
            return;
        }

        var px = (local - rt.rect.min) / rt.rect.size * TexSize;
        if (!stroking)
        {
            stroking = true;
            lastPx = px;
        }
        else
        {
            // 움직인 거리만큼 브러시를 찍는다 (가만히 누르고 있으면 지워지지 않음)
            float dist = Vector2.Distance(lastPx, px);
            if (dist >= 1f)
            {
                int steps = Mathf.CeilToInt(dist / (brushRadiusPx * 0.35f));
                for (int i = 1; i <= steps; i++) Stamp(Vector2.Lerp(lastPx, px, i / (float)steps));
                lastPx = px;
            }
        }

        FlushTexture();
    }

    protected override void OnTick(float deltaTime)
    {
        HandleScrub();
        if (CleanRatio >= requiredRatio) Finish(MinigameResult.Success);
    }

    private void Stamp(Vector2 center)
    {
        int r = Mathf.CeilToInt(brushRadiusPx);
        int cx = Mathf.RoundToInt(center.x);
        int cy = Mathf.RoundToInt(center.y);
        float strength = scrubStrength * 255f;

        for (int y = Mathf.Max(0, cy - r); y <= Mathf.Min(TexSize - 1, cy + r); y++)
        {
            for (int x = Mathf.Max(0, cx - r); x <= Mathf.Min(TexSize - 1, cx + r); x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                if (d > brushRadiusPx) continue;

                int i = y * TexSize + x;
                byte a = stainPixels[i].a;
                if (a == 0) continue;

                float falloff = 1f - d / brushRadiusPx * 0.5f;
                int na = Mathf.Max(0, a - Mathf.CeilToInt(strength * falloff));
                if (a > CleanThreshold && na <= CleanThreshold) dirty--;
                stainPixels[i].a = (byte)na;
                texDirty = true;
            }
        }
    }

    private void FlushTexture()
    {
        if (!texDirty) return;
        texDirty = false;
        stainTex.SetPixels32(stainPixels);
        stainTex.Apply(false);
    }

    // ---------- 텍스처 생성 ----------

    // 잔 모양: 볼(원) + 다리 + 받침
    private static bool InBowl(float u, float v) => ((u - 0.5f) * (u - 0.5f)) / (0.30f * 0.30f) + ((v - 0.62f) * (v - 0.62f)) / (0.28f * 0.28f) <= 1f && v <= 0.86f;
    private static bool InStem(float u, float v) => Mathf.Abs(u - 0.5f) <= 0.022f && v >= 0.10f && v <= 0.36f;
    private static bool InBase(float u, float v) => ((u - 0.5f) * (u - 0.5f)) / (0.20f * 0.20f) + ((v - 0.09f) * (v - 0.09f)) / (0.035f * 0.035f) <= 1f;

    private static Texture2D CreateGlassTexture()
    {
        var tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[TexSize * TexSize];
        for (int y = 0; y < TexSize; y++)
        {
            for (int x = 0; x < TexSize; x++)
            {
                float u = (x + 0.5f) / TexSize;
                float v = (y + 0.5f) / TexSize;
                Color32 c = new Color32(0, 0, 0, 0);
                if (InBowl(u, v))
                {
                    // 가장자리일수록 밝게 (유리 느낌)
                    float e = ((u - 0.5f) * (u - 0.5f)) / 0.09f + ((v - 0.62f) * (v - 0.62f)) / 0.0784f;
                    byte a = (byte)Mathf.Lerp(70, 200, e * e);
                    c = new Color32(200, 225, 240, a);
                    if (u > 0.30f && u < 0.36f && v > 0.52f && v < 0.78f) c = new Color32(255, 255, 255, 170); // 하이라이트
                }
                else if (InStem(u, v) || InBase(u, v))
                {
                    c = new Color32(200, 225, 240, 170);
                }
                pixels[y * TexSize + x] = c;
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply(false);
        return tex;
    }

    private void PaintStains(int count, Vector2 radiusRange)
    {
        for (int i = 0; i < stainPixels.Length; i++)
        {
            stainPixels[i] = StainColor;
            stainPixels[i].a = 0;
        }

        for (int n = 0; n < count; n++)
        {
            // 볼 안쪽에 얼룩 중심 배치
            Vector2 c;
            int guard = 0;
            do c = new Vector2(Random.Range(0.25f, 0.75f), Random.Range(0.40f, 0.80f));
            while (!InBowl(c.x, c.y) && ++guard < 20);

            float radius = Random.Range(radiusRange.x, radiusRange.y);
            float seed = Random.Range(0f, 100f);
            int minX = Mathf.Max(0, Mathf.FloorToInt((c.x - radius * 1.4f) * TexSize));
            int maxX = Mathf.Min(TexSize - 1, Mathf.CeilToInt((c.x + radius * 1.4f) * TexSize));
            int minY = Mathf.Max(0, Mathf.FloorToInt((c.y - radius * 1.4f) * TexSize));
            int maxY = Mathf.Min(TexSize - 1, Mathf.CeilToInt((c.y + radius * 1.4f) * TexSize));

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float u = (x + 0.5f) / TexSize;
                    float v = (y + 0.5f) / TexSize;
                    if (!InBowl(u, v)) continue;

                    // 펄린 노이즈로 가장자리를 울퉁불퉁하게
                    float noise = Mathf.PerlinNoise(seed + u * 12f, seed + v * 12f);
                    float r = radius * (0.75f + noise * 0.5f);
                    float d = Vector2.Distance(new Vector2(u, v), c);
                    if (d > r) continue;

                    byte a = (byte)Mathf.Lerp(235, 150, d / r);
                    int idx = y * TexSize + x;
                    if (a > stainPixels[idx].a) stainPixels[idx].a = a;
                }
            }
        }

        dirty = 0;
        foreach (var p in stainPixels) if (p.a > CleanThreshold) dirty++;
        initialDirty = dirty;
    }

    private void OnDestroy()
    {
        if (glassTex != null) Destroy(glassTex);
        if (stainTex != null) Destroy(stainTex);
    }
}
