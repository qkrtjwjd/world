// 그림자 토끼 더미 생성기 — 실제 도트가 나오기 전 배선용 (사용자 규격서 2장·8장)
//
// 규격: 16×16 캔버스 · 앉은 자세 1장 · **완전 실루엣**(눈·코·털 같은 내부 디테일 없음)
//       색은 데모에서 검정과 짙은 회색 2단계만. 스프라이트는 하나고 색만 갈아끼운다.
//       키는 **10px 과 13px 중 미정** — 규격서 10장이 "둘 다 그려보고 같이 정하자"고 했다.
//       윤곽선 버전도 하나 만들어 두되 데모에선 안 쓰일 수 있다.
//
// 피벗은 발끝 아래 2px = 2/16 = 0.125. PixelArtImportPostprocessor 가 캔버스 세로를 읽어
// 자동으로 잡아 준다(32×48 이면 2/48 그대로).
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class DummyRabbitGenerator
{
    const string OutDir = "Assets/Art/Characters/Rabbit/_dummy";
    const int W = 16, H = 16;
    const int FootMargin = 2;          // 규격서 2장 — 발끝 아래 여백

    class Spec
    {
        public string Id;
        public int SilHeight;          // 실루엣 높이(px)
        public bool Outline;           // true = 속을 비우고 윤곽선만
        public string Note;
    }

    static readonly Spec[] Specs =
    {
        new Spec { Id = "rabbit_sit_10",  SilHeight = 10, Note = "루 키(40px)의 1/4" },
        new Spec { Id = "rabbit_sit_13",  SilHeight = 13, Note = "루 키(40px)의 1/3" },
        new Spec { Id = "rabbit_outline", SilHeight = 13, Outline = true,
                   Note = "윤곽선 버전. 데모에선 안 쓰일 수 있다" },
    };

    [MenuItem("Tools/도트/그림자 토끼 더미 생성")]
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
                string path = string.Format("{0}/{1}.png", OutDir, sp.Id);
                var tex = Draw(sp);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                made.Add(path);
                log.AppendLine(string.Format("  {0,-16} 실루엣 {1}px{2} — {3}",
                    sp.Id, sp.SilHeight, sp.Outline ? " (윤곽선)" : "", sp.Note));
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }
        catch (System.Exception e)
        {
            fails++;
            log.AppendLine("EXCEPTION: " + e);
        }

        // 있음을 센다 — 파일 존재가 아니라 픽셀과 임포트 설정을 판정한다
        log.AppendLine();
        log.AppendLine("=== 규격 검증 (" + made.Count + "장) ===");
        for (int i = 0; i < made.Count; i++) fails += Verify(made[i], Specs[i].SilHeight, log);

        log.AppendLine();
        log.AppendLine("fails = " + fails);
        File.WriteAllText(Path.Combine(outDir, "dummy_rabbit.txt"), log.ToString(),
                          new UTF8Encoding(false));
        Debug.Log("[DummyRabbitGenerator] fails=" + fails);

        if (Application.isBatchMode) EditorApplication.Exit(fails == 0 ? 0 : 1);
    }

    /// <summary>앉은 토끼 실루엣. 몸통 + 머리 + 귀 2개. 내부 디테일은 넣지 않는다.</summary>
    static Texture2D Draw(Spec sp)
    {
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        var px = new Color32[W * H];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 0);

        // 데모 색은 검정·짙은 회색 2단계. 여기서는 짙은 회색으로 굽고
        // 실제 단계 전환은 SpriteRenderer.color 가 맡는다.
        var col = new Color32(40, 40, 46, 255);

        int baseY = FootMargin;
        int cx = W / 2;

        // 높이 배분: 귀 = 전체의 약 1/3, 머리 1/3, 몸통 1/3
        int earH = Mathf.Max(2, sp.SilHeight / 3);
        int headH = Mathf.Max(3, (sp.SilHeight - earH) / 2);
        int bodyH = sp.SilHeight - earH - headH;

        // 몸통 (앉아 있으므로 아래가 넓다)
        Rect(px, cx - 4, baseY, 8, bodyH, col);
        // 머리
        Rect(px, cx - 3, baseY + bodyH, 6, headH, col);
        // 귀 2개 — 사이를 1px 비워 실루엣이 뭉치지 않게 한다
        int earY = baseY + bodyH + headH;
        Rect(px, cx - 3, earY, 2, earH, col);
        Rect(px, cx + 1, earY, 2, earH, col);

        if (sp.Outline) Hollow(px, col);

        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    /// <summary>사방이 모두 채워진 픽셀을 지워 윤곽선만 남긴다.</summary>
    static void Hollow(Color32[] px, Color32 col)
    {
        var src = (Color32[])px.Clone();
        for (int y = 1; y < H - 1; y++)
            for (int x = 1; x < W - 1; x++)
            {
                if (src[y * W + x].a == 0) continue;
                bool edge = src[(y + 1) * W + x].a == 0 || src[(y - 1) * W + x].a == 0
                         || src[y * W + x + 1].a == 0 || src[y * W + x - 1].a == 0;
                if (!edge) px[y * W + x] = new Color32(0, 0, 0, 0);
            }
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

    static int Verify(string path, int silHeight, StringBuilder log)
    {
        if (!File.Exists(path)) { log.AppendLine("  FAIL " + path + " — 파일 없음"); return 1; }

        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(File.ReadAllBytes(path));
        int w = tex.width, h = tex.height;
        int minY = int.MaxValue, maxY = int.MinValue;
        var px = tex.GetPixels32();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (px[y * w + x].a > 0) { if (y < minY) minY = y; if (y > maxY) maxY = y; }
        Object.DestroyImmediate(tex);

        int sil = (minY > maxY) ? 0 : maxY - minY + 1;
        int margin = (minY > maxY) ? -1 : minY;

        var spr = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        float ppu = spr != null ? spr.pixelsPerUnit : -1f;
        float piv = (spr != null && spr.rect.height > 0) ? spr.pivot.y / spr.rect.height : -1f;

        bool ok = (w == W) && (h == H) && (sil == silHeight) && (margin == FootMargin)
                  && Mathf.Approximately(ppu, 32f)
                  && Mathf.Abs(piv - FootMargin / (float)H) < 0.001f;

        log.AppendLine(string.Format(
            "  {0} {1,-20} 캔버스 {2}x{3}(기대 {4}x{5})  실루엣 {6}px(기대 {7})  바닥여백 {8}(기대 {9})  PPU {10}(기대 32)  피벗 {11:0.000}(기대 {12:0.000})",
            ok ? "OK  " : "FAIL", Path.GetFileName(path), w, h, W, H,
            sil, silHeight, margin, FootMargin, ppu, piv, FootMargin / (float)H));
        return ok ? 0 : 1;
    }
}
