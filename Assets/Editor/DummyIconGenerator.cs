// 아이템 아이콘 더미 생성기 — 실제 도트가 나오기 전 배선용 (사용자 규격서 8장)
//
// 규격: 32×32 · PPU 32 · Point · 피벗 **Center**. 인벤토리와 거래창이 같은 그림을 쓴다.
// `Assets/Art/Icons/` 아래에 놓으면 PixelArtImportPostprocessor 가 아이콘 프리셋을 자동 적용한다.
//
// 캐릭터 더미(DummyCharSpriteGenerator)와 다른 점:
//   · 피벗이 Center 라 "바닥 여백" 개념이 없다 → 검증 기준이 다르다
//   · UI Image 로 그려져 color 틴트를 받지 않는다 → 명도를 아이콘마다 직접 준다
//     규격서 4장의 「명도 겹침 금지」에 맞춰 같은 모양끼리는 명도를 벌려 둔다
//   · 채도를 주는 것은 붉은 결정 하나뿐이다 (규격서 팔레트의 강조 1칸)
//
// 실제 도트가 오면 **같은 파일명으로 덮어쓰면 끝난다.**
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class DummyIconGenerator
{
    const string OutDir = "Assets/Art/Icons/_dummy";
    const int W = 32, H = 32;          // 규격서 8장 — 아이템 아이콘은 전부 32×32

    enum Shape { Key, Gem, Orb, Seed, Bottle, Cube, Blob, Cup, Paper, Cloth, Blade, Box, Flower }

    class Spec
    {
        public string Asset;    // Assets/Resources/Items/<Asset>.asset
        public string Id;       // 파일명 (소문자 언더바 — 규격서 7장)
        public Shape Form;
        public float Tone;      // 0~1 명도
        public bool Accent;     // 규격서 팔레트의 강조색 1칸. 붉은 결정만이다
    }

    // (Form, Tone) 짝이 겹치지 않게 잡았다 — 모양이 같으면 명도로, 명도가 같으면 모양으로 갈린다.
    static readonly Spec[] Specs =
    {
        new Spec { Asset = "AtticKey",       Id = "attic_key",        Form = Shape.Key,    Tone = 0.45f },
        new Spec { Asset = "FrontDoorKey",   Id = "front_door_key",   Form = Shape.Key,    Tone = 0.75f },
        new Spec { Asset = "RedCrystal",     Id = "red_crystal",      Form = Shape.Gem,    Tone = 0.85f, Accent = true },
        new Spec { Asset = "BlackOrb",       Id = "black_orb",        Form = Shape.Orb,    Tone = 0.25f },
        new Spec { Asset = "DriedSeedAlive", Id = "dried_seed_alive", Form = Shape.Seed,   Tone = 0.65f },
        new Spec { Asset = "DriedSeedDead",  Id = "dried_seed_dead",  Form = Shape.Seed,   Tone = 0.35f },
        new Spec { Asset = "BottledWater",   Id = "bottled_water",    Form = Shape.Bottle, Tone = 0.70f },
        new Spec { Asset = "SugarCube",      Id = "sugar_cube",       Form = Shape.Cube,   Tone = 0.90f },
        new Spec { Asset = "Marshmallow",    Id = "marshmallow",      Form = Shape.Cube,   Tone = 0.75f },
        new Spec { Asset = "BreadDough",     Id = "bread_dough",      Form = Shape.Blob,   Tone = 0.55f },
        new Spec { Asset = "Espresso",       Id = "espresso",         Form = Shape.Cup,    Tone = 0.30f },
        new Spec { Asset = "hotchocolate",   Id = "hotchocolate",     Form = Shape.Cup,    Tone = 0.50f },
        new Spec { Asset = "M_hotch",        Id = "m_hotch",          Form = Shape.Cup,    Tone = 0.70f },
        new Spec { Asset = "KuruNote",       Id = "kuru_note",        Form = Shape.Paper,  Tone = 0.80f },
        new Spec { Asset = "Coat",           Id = "coat",             Form = Shape.Cloth,  Tone = 0.40f },
        new Spec { Asset = "dagger",         Id = "dagger",           Form = Shape.Blade,  Tone = 0.60f },
        new Spec { Asset = "radio",          Id = "radio",            Form = Shape.Box,    Tone = 0.50f },
        new Spec { Asset = "Anemone",        Id = "anemone",          Form = Shape.Flower, Tone = 0.65f },
    };

    [MenuItem("Tools/도트/아이템 아이콘 더미 생성")]
    public static void Run()
    {
        string outDir = System.Environment.GetEnvironmentVariable("DUMMY_OUT");
        if (string.IsNullOrEmpty(outDir)) outDir = Path.GetTempPath();

        var log = new StringBuilder();
        int fails = 0;
        var made = new List<string>();

        try
        {
            Directory.CreateDirectory(OutDir);
            foreach (var sp in Specs)
            {
                string path = string.Format("{0}/{1}_32.png", OutDir, sp.Id);
                var tex = Draw(sp);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                made.Add(path);
                log.AppendLine(string.Format("  {0,-20} {1,-7} 명도 {2:0.00}{3}",
                    sp.Id, sp.Form, sp.Tone, sp.Accent ? "  (강조색)" : ""));
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }
        catch (System.Exception e)
        {
            fails++;
            log.AppendLine("EXCEPTION: " + e);
        }

        // 있음을 센다 — 파일 존재가 아니라 실제 픽셀과 임포트 설정을 판정한다
        log.AppendLine();
        log.AppendLine("=== 규격 검증 (" + made.Count + "장) ===");
        foreach (var p in made) fails += Verify(p, log);

        log.AppendLine();
        log.AppendLine("fails = " + fails);
        File.WriteAllText(Path.Combine(outDir, "dummy_icon.txt"), log.ToString(),
                          new UTF8Encoding(false));
        Debug.Log("[DummyIconGenerator] fails=" + fails);

        if (Application.isBatchMode) EditorApplication.Exit(fails == 0 ? 0 : 1);
    }

    // ── 그리기 ────────────────────────────────────────────────────────────
    static Texture2D Draw(Spec sp)
    {
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        var px = new Color32[W * H];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 0);

        byte v = (byte)Mathf.RoundToInt(sp.Tone * 255f);
        // 강조색만 채도를 준다. 나머지는 무채색 (규격서 4장)
        var col = sp.Accent
            ? new Color32(v, (byte)(v * 0.20f), (byte)(v * 0.25f), 255)
            : new Color32(v, v, v, 255);
        var dim = new Color32((byte)(v * 0.6f), (byte)(v * 0.6f), (byte)(v * 0.6f), 255);

        int cx = W / 2, cy = H / 2;

        switch (sp.Form)
        {
            case Shape.Key:                                  // 손잡이 고리 + 자루 + 이빨
                Ring(px, cx - 6, cy + 2, 8, col);
                Rect(px, cx - 2, cy - 9, 3, 12, col);
                Rect(px, cx + 1, cy - 9, 4, 2, col);
                Rect(px, cx + 1, cy - 5, 3, 2, col);
                break;

            case Shape.Gem:                                  // 마름모
                Diamond(px, cx, cy, 9, col);
                Rect(px, cx - 2, cy + 1, 2, 3, dim);
                break;

            case Shape.Orb:                                  // 원
                Disc(px, cx, cy, 8, col);
                break;

            case Shape.Seed:                                 // 작은 타원 + 꼭지
                Disc(px, cx, cy - 1, 5, col);
                Rect(px, cx - 1, cy + 4, 2, 3, dim);
                break;

            case Shape.Bottle:                               // 병목 + 몸통
                Rect(px, cx - 2, cy + 5, 4, 5, col);
                Rect(px, cx - 6, cy - 10, 12, 15, col);
                Rect(px, cx - 4, cy - 8, 8, 7, dim);         // 안쪽 물
                break;

            case Shape.Cube:                                 // 정육면체
                Rect(px, cx - 7, cy - 7, 14, 14, col);
                Rect(px, cx - 7, cy + 3, 14, 4, dim);
                break;

            case Shape.Blob:                                 // 반죽 덩어리
                Disc(px, cx, cy - 2, 9, col);
                Rect(px, cx - 9, cy - 8, 18, 4, col);
                break;

            case Shape.Cup:                                  // 컵 + 손잡이
                Rect(px, cx - 6, cy - 8, 12, 13, col);
                Rect(px, cx - 4, cy + 2, 8, 3, dim);         // 안쪽 음료
                Ring(px, cx + 8, cy - 2, 5, col);
                break;

            case Shape.Paper:                                // 접힌 종이
                Rect(px, cx - 7, cy - 9, 14, 18, col);
                Rect(px, cx - 5, cy + 3, 10, 2, dim);
                Rect(px, cx - 5, cy - 1, 10, 2, dim);
                Rect(px, cx - 5, cy - 5, 7, 2, dim);
                break;

            case Shape.Cloth:                                // 코트 (어깨 + 몸통)
                Rect(px, cx - 8, cy + 4, 16, 5, col);
                Rect(px, cx - 6, cy - 10, 12, 14, col);
                Rect(px, cx - 1, cy - 10, 2, 14, dim);       // 앞섶
                break;

            case Shape.Blade:                                // 단검 (날 + 코등이 + 자루)
                Rect(px, cx - 2, cy - 2, 4, 12, col);
                Rect(px, cx - 6, cy - 4, 12, 2, col);
                Rect(px, cx - 1, cy - 11, 2, 7, dim);
                break;

            case Shape.Box:                                  // 라디오 (본체 + 다이얼 + 안테나)
                Rect(px, cx - 9, cy - 6, 18, 12, col);
                Disc(px, cx + 4, cy, 3, dim);
                Rect(px, cx - 7, cy - 3, 7, 6, dim);
                Rect(px, cx + 7, cy + 6, 2, 6, col);
                break;

            case Shape.Flower:                               // 꽃잎 4 + 꽃심 + 줄기
                Disc(px, cx, cy + 6, 4, col);
                Disc(px, cx - 6, cy + 1, 4, col);
                Disc(px, cx + 6, cy + 1, 4, col);
                Disc(px, cx, cy - 4, 4, col);
                Disc(px, cx, cy + 1, 3, dim);
                Rect(px, cx - 1, cy - 12, 2, 8, dim);
                break;
        }

        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    static void Rect(Color32[] px, int x0, int y0, int w, int h, Color32 c)
    {
        for (int y = y0; y < y0 + h; y++)
        {
            if (y < 0 || y >= H) continue;
            for (int x = x0; x < x0 + w; x++)
            {
                if (x < 0 || x >= W) continue;
                px[y * W + x] = c;
            }
        }
    }

    static void Disc(Color32[] px, int cx, int cy, int r, Color32 c)
    {
        for (int y = cy - r; y <= cy + r; y++)
        {
            if (y < 0 || y >= H) continue;
            for (int x = cx - r; x <= cx + r; x++)
            {
                if (x < 0 || x >= W) continue;
                int dx = x - cx, dy = y - cy;
                if (dx * dx + dy * dy <= r * r) px[y * W + x] = c;
            }
        }
    }

    /// <summary>속이 빈 원. 두께 2px.</summary>
    static void Ring(Color32[] px, int cx, int cy, int r, Color32 c)
    {
        for (int y = cy - r; y <= cy + r; y++)
        {
            if (y < 0 || y >= H) continue;
            for (int x = cx - r; x <= cx + r; x++)
            {
                if (x < 0 || x >= W) continue;
                int dx = x - cx, dy = y - cy;
                int d = dx * dx + dy * dy;
                if (d <= r * r && d >= (r - 2) * (r - 2)) px[y * W + x] = c;
            }
        }
    }

    static void Diamond(Color32[] px, int cx, int cy, int r, Color32 c)
    {
        for (int y = cy - r; y <= cy + r; y++)
        {
            if (y < 0 || y >= H) continue;
            for (int x = cx - r; x <= cx + r; x++)
            {
                if (x < 0 || x >= W) continue;
                if (Mathf.Abs(x - cx) + Mathf.Abs(y - cy) <= r) px[y * W + x] = c;
            }
        }
    }

    // ── 검증 ──────────────────────────────────────────────────────────────
    /// <summary>PNG 를 다시 읽어 캔버스·내용을 픽셀로 판정하고, 임포트된 PPU·피벗까지 본다.
    /// 아이콘은 피벗이 Center 라 캐릭터의 「바닥 여백」 판정을 쓰지 않는다.</summary>
    static int Verify(string path, StringBuilder log)
    {
        if (!File.Exists(path)) { log.AppendLine("  FAIL " + path + " — 파일 없음"); return 1; }

        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(File.ReadAllBytes(path));
        int w = tex.width, h = tex.height;
        int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
        int solid = 0;
        var px = tex.GetPixels32();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (px[y * w + x].a > 0)
                {
                    solid++;
                    if (x < minX) minX = x; if (x > maxX) maxX = x;
                    if (y < minY) minY = y; if (y > maxY) maxY = y;
                }
        Object.DestroyImmediate(tex);

        var spr = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        float ppu = spr != null ? spr.pixelsPerUnit : -1f;
        float pivX = (spr != null && spr.rect.width > 0) ? spr.pivot.x / spr.rect.width : -1f;
        float pivY = (spr != null && spr.rect.height > 0) ? spr.pivot.y / spr.rect.height : -1f;

        bool inside = solid > 0 && minX >= 0 && maxX < W && minY >= 0 && maxY < H;
        bool ok = (w == W) && (h == H) && (solid > 0) && inside
                  && Mathf.Approximately(ppu, 32f)
                  && Mathf.Abs(pivX - 0.5f) < 0.001f && Mathf.Abs(pivY - 0.5f) < 0.001f;

        log.AppendLine(string.Format(
            "  {0} {1,-24} 캔버스 {2}x{3}(기대 {4}x{5})  채운픽셀 {6,4}  바운딩 {7}~{8},{9}~{10}  PPU {11}(기대 32)  피벗 {12:0.00},{13:0.00}(기대 0.50,0.50)",
            ok ? "OK  " : "FAIL", Path.GetFileName(path), w, h, W, H, solid,
            solid > 0 ? minX : -1, solid > 0 ? maxX : -1, solid > 0 ? minY : -1, solid > 0 ? maxY : -1,
            ppu, pivX, pivY));
        return ok ? 0 : 1;
    }
}
