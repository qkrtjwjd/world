using UnityEngine;

/// <summary>
/// S#21C 「루가 천천히 고개를 위로 올려본다」 — 결계가 숲 전체를 감싼 부감 오버레이 컷의 <b>자리 표시 그림</b>.
///
/// <para>
/// 사용자 결정(2026-09-30): 강화된 결계가 숲 전체를 덮은 모습을 한 장으로 보여 주고, 본편에서 돌 세 곳을 암시한다 —
/// 서쪽 호수 · 북쪽 고목 · 동쪽 회전목마(기계숲). 방위는 A-13-1 · E 의 원형 결계 배치를 따른다.
/// 이름도 표시도 붙이지 않는다 — 설명하지 않는다(CLAUDE.md §10). 결계 중심(마을 · 세라)은 흐리게만 둔다 —
/// 세라의 위치를 계산하게 하지 않는다(정본 1301).
/// </para>
///
/// <para>
/// 움직임은 3프레임 반복뿐이다(F-3-9 오버레이 — 트윈 · 셰이더 금지). 황금 안개가 천천히 흐르고, 그 틈으로
/// 호수 물결이 일렁이고, 고목이 흔들리고, 회전목마가 돈다.
/// </para>
///
/// ⚠ F-3-9(문단 280)은 부감용 <b>배경</b>을 막는다. 이 컷은 장소 배경이 아니라 한 장짜리 오버레이이며,
///   사용자 결정으로 예외를 둔다 — D S#21C · F-3-9 오버레이 목록 반영은 원고 작업에서 한다.
///   그림이 오면 <see cref="ForestBarrierDirector.domeOverlayFrames"/> 에 꽂으면 이 생성기는 쓰지 않는다.
/// </summary>
public static class ForestDomeOverlay
{
    public const int W = 384, H = 216;   // F-3-9 오버레이 규격 한 종

    // 팔레트 — Assets/Art/_palette/무채색낙원.gpl
    static readonly Color32 F_cream  = new Color32(249, 242, 228, 255);
    static readonly Color32 F_light  = new Color32(253, 208, 112, 255);
    static readonly Color32 F_orange = new Color32(232, 141,  50, 255);
    static readonly Color32 F_brown  = new Color32(109,  84,  43, 255);
    static readonly Color32 R_gray   = new Color32(146, 147, 141, 255);
    static readonly Color32 R_dust   = new Color32( 55,  52,  32, 255);
    static readonly Color32 R_green  = new Color32( 65,  86,  65, 255);
    static readonly Color32 R_teal   = new Color32( 38,  64,  60, 255);
    static readonly Color32 R_dark   = new Color32(  9,  20,  25, 255);

    const int CX = W / 2, CY = H / 2, R = 98;

    public static Sprite[] Build(int frames = 3)
    {
        var result = new Sprite[frames];
        for (int f = 0; f < frames; f++)
        {
            var px = new Color32[W * H];
            DrawLand(px);
            DrawVillage(px);
            DrawLake(px, f);
            DrawOldTree(px, f);
            DrawCarousel(px, f, frames);
            DrawVeil(px, f, frames);
            DrawRim(px);
            DrawFrame(px);

            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "S21 DomeOverlay " + f };
            tex.SetPixels32(px);
            tex.Apply();
            result[f] = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 32f);
        }
        return result;
    }

    // ── 땅 — 결계 밖은 어둡게, 안은 숲 ─────────────────────────────────────
    static void DrawLand(Color32[] px)
    {
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float d = Dist(x, y, CX, CY);
                Color32 c;
                if (d > R)
                {
                    c = Hash(x, y, 3) < 0.04f ? R_dust : R_dark;
                }
                else
                {
                    // 불규칙한 수관 — 두 겹 값 노이즈를 3단으로 끊는다(격자 무늬가 나지 않게)
                    float n = Noise(x / 7f, y / 7f, 1) * 0.65f + Noise(x / 2.5f, y / 2.5f, 2) * 0.35f;
                    c = n < 0.38f ? R_dark : n < 0.58f ? R_teal : R_green;
                    if (n > 0.72f && Hash(x, y, 4) < 0.35f) c = Mix(R_green, F_cream, 0.2f);   // 수관 꼭대기 빛
                }
                px[y * W + x] = c;
            }
    }

    // ── 중심 마을 — 흐리게. 강조하지 않는다 ─────────────────────────────────
    static void DrawVillage(Color32[] px)
    {
        for (int i = 0; i < 9; i++)
        {
            int rx = CX - 10 + (int)(Hash(i, 7, 5) * 20);
            int ry = CY - 7 + (int)(Hash(i, 9, 5) * 14);
            for (int y = 0; y < 2; y++)
                for (int x = 0; x < 3; x++)
                    Blend(px, rx + x, ry + y, R_gray, 0.35f);
        }
    }

    // ── 서 — 호수. 물결이 일렁인다 ──────────────────────────────────────────
    static void DrawLake(Color32[] px, int f)
    {
        int lx = CX - 58, ly = CY + 6, rx = 23, ry = 14;
        for (int y = ly - ry - 1; y <= ly + ry + 1; y++)
            for (int x = lx - rx - 1; x <= lx + rx + 1; x++)
            {
                float e = Sq((x - lx) / (float)rx) + Sq((y - ly) / (float)ry);
                if (e > 1.08f) continue;
                if (e > 0.9f) { Set(px, x, y, R_dark); continue; }
                Color32 c = Mix(R_teal, R_gray, 0.28f);
                // 물결 — 짧은 가로줄이 프레임마다 옆으로 밀린다
                if ((y - ly) % 4 == 0 && ((x * 1 + y * 3 + f * 3) % 11) < 3) c = Mix(c, F_cream, 0.55f);
                Set(px, x, y, c);
            }
    }

    // ── 북 — 고목. 크고 늙은 수관이 흔들리고, 뿌리가 숲 바닥으로 뻗는다 ──
    static void DrawOldTree(Color32[] px, int f)
    {
        int tx = CX + 2, ty = CY + 60, r = 22;
        int sway = f == 1 ? 1 : 0;
        // 뿌리 — 네 갈래 이상, 굽어 뻗는다
        for (int k = 0; k < 7; k++)
        {
            float ang = k * (Mathf.PI * 2f / 7f) + 0.4f;
            for (int i = r - 4; i < r + 13; i++)
            {
                float bend = Mathf.Sin(i * 0.35f + k) * 1.8f;
                int x = tx + Mathf.RoundToInt(Mathf.Cos(ang) * i - Mathf.Sin(ang) * bend);
                int y = ty + Mathf.RoundToInt(Mathf.Sin(ang) * i + Mathf.Cos(ang) * bend);
                Color32 rc = i < r + 8 ? F_brown : R_dust;
                Set(px, x, y, rc);
                if (i < r + 6) { Set(px, x + 1, y, rc); Set(px, x, y + 1, R_dark); }   // 굵은 뿌리 · 아래 그늘
            }
        }
        // 그림자
        for (int y = -r; y <= r; y++)
            for (int x = -r; x <= r; x++)
                if (x * x + y * y <= r * r) Blend(px, tx + x + 4, ty + y - 4, R_dark, 0.75f);
        // 수관 — 가장자리가 울퉁불퉁하다
        for (int y = -r - 3; y <= r + 3; y++)
            for (int x = -r - 3; x <= r + 3; x++)
            {
                float edge = r + (Noise((Mathf.Atan2(y, x) + 4f) * 2.2f, 0.5f, 11) - 0.5f) * 7f;
                float d = Mathf.Sqrt(x * x + y * y);
                if (d > edge) continue;
                float n = Noise((x + 60) / 4f, (y + 60) / 4f, 12);
                // 주변 숲보다 한 톤 밝고 누렇다 — 오래된 나무라 잎빛이 다르다. 테두리는 두 겹으로 굵게.
                Color32 c = d > edge - 1.2f ? R_dark
                          : d > edge - 2.4f ? Mix(R_green, R_dark, 0.5f)
                          : n < 0.35f ? Mix(R_green, R_dust, 0.3f)
                          : n < 0.68f ? Mix(R_green, F_light, 0.2f)
                          : Mix(R_green, F_light, 0.42f);
                Set(px, tx + x + sway, ty + y, c);
            }
    }

    // ── 동 — 회전목마(기계숲). 천천히 돈다 ─────────────────────────────────
    static void DrawCarousel(Color32[] px, int f, int frames)
    {
        int cx = CX + 60, cy = CY - 2;
        const int deck = 16, roof = 11;
        // 6갈래 지붕 · 목마 12마리라 한 주기가 60° / 30° — 3프레임이면 10° 씩 돌려 둘 다 끊김 없이 잇는다
        float rot = f * (30f / frames) * Mathf.Deg2Rad;

        // 그림자 · 원판(바닥)
        for (int y = -deck - 2; y <= deck + 2; y++)
            for (int x = -deck - 2; x <= deck + 2; x++)
            {
                float d = Mathf.Sqrt(x * x + y * y);
                if (d <= deck + 0.5f) Blend(px, cx + x + 3, cy + y - 3, R_dark, 0.6f);
            }
        for (int y = -deck; y <= deck; y++)
            for (int x = -deck; x <= deck; x++)
            {
                float d = Mathf.Sqrt(x * x + y * y);
                if (d > deck + 0.5f) continue;
                Set(px, cx + x, cy + y, d > deck - 0.8f ? R_dark : Mix(F_brown, R_dust, 0.35f));
            }
        // 목마 — 원판 가장자리에 2×2 로, 지붕 밖으로 보인다
        for (int k = 0; k < 12; k++)
        {
            float a = k * (Mathf.PI * 2f / 12f) + rot;
            int hx = cx + Mathf.RoundToInt(Mathf.Cos(a) * (deck - 2.5f));
            int hy = cy + Mathf.RoundToInt(Mathf.Sin(a) * (deck - 2.5f));
            Color32 hc = k % 2 == 0 ? Mix(F_cream, R_dust, 0.2f) : Mix(F_orange, R_dust, 0.3f);
            for (int y = 0; y < 2; y++) for (int x = 0; x < 2; x++) Set(px, hx + x - 1, hy + y - 1, hc);
        }
        // 지붕 — 6갈래 줄무늬 · 물결 테두리 · 꼭대기 장식
        for (int y = -roof - 2; y <= roof + 2; y++)
            for (int x = -roof - 2; x <= roof + 2; x++)
            {
                float d = Mathf.Sqrt(x * x + y * y);
                float ang = Mathf.Atan2(y, x) - rot * 2f;
                float scallop = roof + (Mathf.Cos(ang * 12f) > 0f ? 1f : 0f);
                if (d > scallop + 0.3f) continue;
                Color32 c;
                if (d > roof - 0.6f) c = R_dark;
                else if (d < 1.5f) c = F_light;
                else
                {
                    int seg = Mathf.FloorToInt(Mathf.Repeat(ang, Mathf.PI * 2f) / (Mathf.PI / 3f));
                    c = seg % 2 == 0 ? F_orange : F_cream;
                    c = Mix(c, R_dust, 0.22f);                  // 오래 멈춰 있던 색 — 빛바랬다
                }
                Set(px, cx + x, cy + y, c);
            }
    }

    // ── 결계의 황금 안개 — 천천히 흐르고, 그 틈으로 세 곳이 비친다 ──────────
    static void DrawVeil(Color32[] px, int f, int frames)
    {
        float phase = f * (Mathf.PI * 2f / frames);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float d = Dist(x, y, CX, CY);
                if (d > R) continue;
                // 비스듬히 흐르는 띠 × 얼룩 — 절반쯤은 걷혀 있어야 틈이 생긴다
                float band = Mathf.Sin((x * 0.9f + y * 0.55f) / 15f - phase) * 0.5f + 0.5f;
                float blot = Noise(x / 16f + f * 0.35f, y / 12f, 21);
                float v = band * 0.6f + blot * 0.55f;
                v += Mathf.Clamp01((d - R * 0.7f) / (R * 0.3f)) * 0.4f;   // 가장자리로 갈수록 짙다 — 돔의 곡면
                float a = v > 1.0f ? 0.42f : v > 0.82f ? 0.26f : v > 0.66f ? 0.13f : 0f;
                if (a > 0f && a < 0.2f && ((x + y) & 1) == 0) a = 0f;       // 가장 옅은 단은 체크 디더
                if (a > 0f) Blend(px, x, y, F_light, a);
            }
    }

    // ── 돔의 가장자리 ───────────────────────────────────────────────────────
    static void DrawRim(Color32[] px)
    {
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float d = Dist(x, y, CX, CY);
                if (d > R - 1.5f && d <= R + 0.5f) px[y * W + x] = F_light;
                else if (d > R + 0.5f && d <= R + 1.5f) px[y * W + x] = F_cream;
                else if (d > R + 1.5f && d <= R + 3.5f) Blend(px, x, y, F_light, 0.35f);
            }
    }

    // ── 공용 테두리(F-3-9 「테두리 프레임 1종」) 자리 — 프레임 그림이 오면 그쪽을 쓴다 ─
    static void DrawFrame(Color32[] px)
    {
        for (int x = 0; x < W; x++) { px[x] = R_dark; px[(H - 1) * W + x] = R_dark; px[W + x] = F_brown; px[(H - 2) * W + x] = F_brown; }
        for (int y = 0; y < H; y++) { px[y * W] = R_dark; px[y * W + W - 1] = R_dark; px[y * W + 1] = F_brown; px[y * W + W - 2] = F_brown; }
    }

    // ── 도구 ────────────────────────────────────────────────────────────────
    static float Dist(int x, int y, int cx, int cy) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
    static float Sq(float v) => v * v;

    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 144665);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
        }
    }

    static Color32 Mix(Color32 a, Color32 b, float t) => Color32.Lerp(a, b, t);

    static float Noise(float x, float y, int seed)
    {
        int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
        float fx = x - ix, fy = y - iy;
        fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
        float a = Hash(ix, iy, seed), b = Hash(ix + 1, iy, seed), c = Hash(ix, iy + 1, seed), d = Hash(ix + 1, iy + 1, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    static void Set(Color32[] px, int x, int y, Color32 c)
    {
        if (x < 0 || y < 0 || x >= W || y >= H) return;
        px[y * W + x] = c;
    }

    static void Blend(Color32[] px, int x, int y, Color32 c, float a)
    {
        if (x < 0 || y < 0 || x >= W || y >= H) return;
        px[y * W + x] = Color32.Lerp(px[y * W + x], c, a);
    }
}
