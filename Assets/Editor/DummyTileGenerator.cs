// 배경 타일 더미 생성기 — 실제 도트가 나오기 전 배선용 (사용자 규격서 8장 「배경」)
//
// 규격: 32×32 · PPU 32 · Point · 피벗 **Center**(Grid 셀 1×1 과 맞는다).
//   *"통짜 그림 말고 32×32 타일 조각으로 그려"* · *"한 구역당 타일 12~20종"*
//   *"집이랑 마을이랑 숲은 타일셋 따로 그려야 돼"*
//
// 구역당 12종. 이름은 쓰임새로 붙여 두었으니 실제 도트가 오면 **같은 이름으로 덮으면 끝난다.**
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class DummyTileGenerator
{
    const string Root = "Assets/Art/Tiles";
    const int W = 32, H = 32;

    enum Pat { Solid, Checker, StripeH, StripeV, Dots, Border,
               HalfTop, HalfBottom, Corner, Cross, Diag, Noise }

    class Area
    {
        public string Folder;      // Assets/Art/Tiles/<Folder>/_dummy
        public string Prefix;
        public float BaseTone;     // 구역 기준 명도 — 구역끼리 벌려 둔다 (규격서 4장)
        public string[] Names;     // 12종
    }

    static readonly Area[] AREAS =
    {
        new Area { Folder = "House", Prefix = "house", BaseTone = 0.55f, Names = new[]
        { "floor_wood", "floor_wood_alt", "wall", "wall_top", "baseboard", "door_frame",
          "window", "rug", "stair", "ceiling_edge", "corner", "shadow" } },

        new Area { Folder = "Village", Prefix = "village", BaseTone = 0.70f, Names = new[]
        { "ground_dirt", "ground_stone", "path", "grass", "wall_brick", "roof",
          "fence", "well_edge", "step", "planter", "corner", "shadow" } },

        new Area { Folder = "Forest", Prefix = "forest", BaseTone = 0.40f, Names = new[]
        { "ground_soil", "ground_moss", "grass_tall", "path", "rock", "root",
          "water", "water_edge", "log", "bush", "corner", "shadow" } },
    };

    [MenuItem("Tools/도트/배경 타일 더미 생성")]
    public static void Run()
    {
        string outDir = System.Environment.GetEnvironmentVariable("DUMMY_OUT");
        if (string.IsNullOrEmpty(outDir)) outDir = Path.GetTempPath();

        var log = new StringBuilder();
        int fails = 0;
        var made = new List<string>();

        try
        {
            foreach (var a in AREAS)
            {
                string dir = string.Format("{0}/{1}/_dummy", Root, a.Folder);
                Directory.CreateDirectory(dir);
                log.AppendLine(string.Format("[{0}] 기준 명도 {1:0.00} · {2}종",
                    a.Folder, a.BaseTone, a.Names.Length));

                for (int i = 0; i < a.Names.Length; i++)
                {
                    // 패턴은 12종을 한 바퀴 돌리고, 명도는 타일마다 조금씩 어긋나게 준다.
                    var pat = (Pat)(i % 12);
                    float tone = Mathf.Clamp01(a.BaseTone + (i - 5.5f) * 0.035f);
                    string path = string.Format("{0}/{1}_{2}.png", dir, a.Prefix, a.Names[i]);
                    var tex = Draw(pat, tone);
                    File.WriteAllBytes(path, tex.EncodeToPNG());
                    Object.DestroyImmediate(tex);
                    made.Add(path);
                    log.AppendLine(string.Format("    {0,-24} {1,-10} 명도 {2:0.00}",
                        a.Names[i], pat, tone));
                }
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }
        catch (System.Exception e)
        {
            fails++;
            log.AppendLine("EXCEPTION: " + e);
        }

        log.AppendLine();
        log.AppendLine("=== 규격 검증 (" + made.Count + "장) ===");
        int okCount = 0;
        foreach (var p in made)
        {
            int r = Verify(p, log);
            fails += r;
            if (r == 0) okCount++;
        }
        log.AppendLine(string.Format("  OK {0} / {1}", okCount, made.Count));

        log.AppendLine();
        log.AppendLine("fails = " + fails);
        File.WriteAllText(Path.Combine(outDir, "dummy_tile.txt"), log.ToString(),
                          new UTF8Encoding(false));
        Debug.Log("[DummyTileGenerator] fails=" + fails);

        if (Application.isBatchMode) EditorApplication.Exit(fails == 0 ? 0 : 1);
    }

    /// <summary>타일은 사방이 이어져야 하므로 캔버스를 꽉 채운다(알파 0 픽셀 없음).</summary>
    static Texture2D Draw(Pat pat, float tone)
    {
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        var px = new Color32[W * H];

        byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(tone) * 255f);
        byte b = (byte)Mathf.RoundToInt(Mathf.Clamp01(tone - 0.12f) * 255f);
        var ca = new Color32(a, a, a, 255);
        var cb = new Color32(b, b, b, 255);

        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                bool alt;
                switch (pat)
                {
                    case Pat.Checker:    alt = ((x / 8) + (y / 8)) % 2 == 1; break;
                    case Pat.StripeH:    alt = (y / 4) % 2 == 1; break;
                    case Pat.StripeV:    alt = (x / 4) % 2 == 1; break;
                    case Pat.Dots:       alt = (x % 8 < 3) && (y % 8 < 3); break;
                    case Pat.Border:     alt = x < 2 || y < 2 || x >= W - 2 || y >= H - 2; break;
                    case Pat.HalfTop:    alt = y >= H / 2; break;
                    case Pat.HalfBottom: alt = y < H / 2; break;
                    case Pat.Corner:     alt = (x < H / 2) == (y < H / 2); break;
                    case Pat.Cross:      alt = (x >= 13 && x < 19) || (y >= 13 && y < 19); break;
                    case Pat.Diag:       alt = ((x + y) / 5) % 2 == 1; break;
                    case Pat.Noise:      alt = ((x * 7 + y * 13) % 11) < 4; break;
                    default:             alt = false; break;      // Solid
                }
                px[y * W + x] = alt ? cb : ca;
            }

        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    static int Verify(string path, StringBuilder log)
    {
        if (!File.Exists(path)) { log.AppendLine("  FAIL " + path + " — 파일 없음"); return 1; }

        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(File.ReadAllBytes(path));
        int w = tex.width, h = tex.height, opaque = 0;
        foreach (var p in tex.GetPixels32()) if (p.a == 255) opaque++;
        Object.DestroyImmediate(tex);

        var spr = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        float ppu = spr != null ? spr.pixelsPerUnit : -1f;
        float pivX = (spr != null && spr.rect.width > 0) ? spr.pivot.x / spr.rect.width : -1f;
        float pivY = (spr != null && spr.rect.height > 0) ? spr.pivot.y / spr.rect.height : -1f;

        // 타일은 캔버스를 꽉 채워야 이어 붙였을 때 틈이 안 생긴다
        bool ok = (w == W) && (h == H) && (opaque == W * H)
                  && Mathf.Approximately(ppu, 32f)
                  && Mathf.Abs(pivX - 0.5f) < 0.001f && Mathf.Abs(pivY - 0.5f) < 0.001f;

        if (!ok)
            log.AppendLine(string.Format(
                "  FAIL {0}  캔버스 {1}x{2}  불투명 {3}/{4}  PPU {5}  피벗 {6:0.00},{7:0.00}",
                Path.GetFileName(path), w, h, opaque, W * H, ppu, pivX, pivY));
        return ok ? 0 : 1;
    }
}
