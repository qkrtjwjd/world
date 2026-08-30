// 필드 UI 프레임 더미 생성기 — 실제 도트가 나오기 전 배선용 (사용자 규격서 8장 「UI」)
//
// 규격서: *"UI는 층이 달라서 색 빠지는 효과를 안 받아. 도트로 그리되 월드랑 픽셀 크기
//          같아 보이게 32 기준은 지켜줘."*
//
// ⚠ UI 만 PPU 가 100 이다. 캔버스가 referencePixelsPerUnit 100 · 기준 해상도 640×360 이라
//   (CLAUDE.md §11) 스프라이트도 PPU 100 이어야 **텍스처 1픽셀 = 화면 1픽셀**이 된다.
//   Assets/Art/ 아래에 두면 후처리기가 PPU 32 를 붙여 크기가 어긋나므로,
//   기존 UI 자산과 같은 Assets/Images/UI/ 에 두고 임포트 설정을 코드가 직접 지정한다.
//
// ⚠ 기존 ui_frame.png 는 filterMode 가 Bilinear 다. 도트가 뭉개지므로 새 것은 Point 로 만든다.
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class DummyUiSpriteGenerator
{
    const string OutDir = "Assets/Images/UI/_dummy";
    const int BORDER = 6;          // 9슬라이스 테두리(px)

    class Spec
    {
        public string Id;
        public int Size;
        public float Fill;         // 안쪽 명도
        public float Edge;         // 테두리 명도
        public float Alpha;        // 안쪽 알파 — 대사창은 뒤가 비쳐야 한다
        public bool Slice;         // 9슬라이스로 쓸 것인지
        public string Note;
    }

    static readonly Spec[] Specs =
    {
        new Spec { Id = "ui_panel",        Size = 32, Fill = 0.16f, Edge = 0.62f, Alpha = 0.86f, Slice = true,
                   Note = "대화창·목표·저장 패널 공용" },
        new Spec { Id = "ui_panel_light",  Size = 32, Fill = 0.34f, Edge = 0.78f, Alpha = 0.90f, Slice = true,
                   Note = "밝은 변형" },
        new Spec { Id = "ui_namebox",      Size = 24, Fill = 0.24f, Edge = 0.70f, Alpha = 1.00f, Slice = true,
                   Note = "이름창 배경 — ⚠ 지금 Image 슬롯이 없어 붙일 곳부터 만들어야 한다" },
        new Spec { Id = "ui_button",       Size = 24, Fill = 0.28f, Edge = 0.72f, Alpha = 1.00f, Slice = true,
                   Note = "버튼 기본" },
        new Spec { Id = "ui_button_press", Size = 24, Fill = 0.18f, Edge = 0.52f, Alpha = 1.00f, Slice = true,
                   Note = "버튼 눌림" },
        new Spec { Id = "ui_slot",         Size = 24, Fill = 0.20f, Edge = 0.56f, Alpha = 1.00f, Slice = true,
                   Note = "저장 슬롯 칸" },
        new Spec { Id = "ui_note",         Size = 32, Fill = 0.88f, Edge = 0.66f, Alpha = 1.00f, Slice = true,
                   Note = "쪽지 전용 창 — 종이색" },
        new Spec { Id = "ui_prompt",       Size = 16, Fill = 0.14f, Edge = 0.80f, Alpha = 0.92f, Slice = true,
                   Note = "상호작용 표시 — ⚠ 지금 Image 슬롯이 없다" },
    };

    [MenuItem("Tools/도트/필드 UI 더미 생성")]
    public static void Run()
    {
        string outDir = System.Environment.GetEnvironmentVariable("DUMMY_OUT");
        if (string.IsNullOrEmpty(outDir)) outDir = Path.GetTempPath();

        var log = new StringBuilder();
        int fails = 0;
        var made = new List<Spec>();

        try
        {
            Directory.CreateDirectory(OutDir);
            foreach (var sp in Specs)
            {
                string path = OutDir + "/" + sp.Id + ".png";
                var tex = Draw(sp);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                made.Add(sp);
                log.AppendLine(string.Format("  {0,-18} {1}x{1}  안쪽 {2:0.00}(a {3:0.00}) 테두리 {4:0.00} — {5}",
                    sp.Id, sp.Size, sp.Fill, sp.Alpha, sp.Edge, sp.Note));
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // 이 폴더는 후처리기 대상이 아니므로 임포트 설정을 직접 지정한다.
            foreach (var sp in Specs) Import(sp);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }
        catch (System.Exception e)
        {
            fails++;
            log.AppendLine("EXCEPTION: " + e);
        }

        log.AppendLine();
        log.AppendLine("=== 규격 검증 (" + made.Count + "장) ===");
        foreach (var sp in made) fails += Verify(sp, log);

        log.AppendLine();
        log.AppendLine("fails = " + fails);
        File.WriteAllText(Path.Combine(outDir, "dummy_ui.txt"), log.ToString(),
                          new UTF8Encoding(false));
        Debug.Log("[DummyUiSpriteGenerator] fails=" + fails);

        if (Application.isBatchMode) EditorApplication.Exit(fails == 0 ? 0 : 1);
    }

    static void Import(Spec sp)
    {
        string path = OutDir + "/" + sp.Id + ".png";
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) return;

        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.filterMode = FilterMode.Point;              // 도트가 뭉개지지 않게
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.spritePixelsPerUnit = 100f;                 // 캔버스 기준과 맞춘다 (§11)
        if (sp.Slice) ti.spriteBorder = new Vector4(BORDER, BORDER, BORDER, BORDER);
        ti.SaveAndReimport();
    }

    /// <summary>테두리 2px + 안쪽. 9슬라이스로 늘려도 테두리가 안 뭉개진다.</summary>
    static Texture2D Draw(Spec sp)
    {
        int n = sp.Size;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var px = new Color32[n * n];

        byte f = (byte)Mathf.RoundToInt(sp.Fill * 255f);
        byte e = (byte)Mathf.RoundToInt(sp.Edge * 255f);
        byte fa = (byte)Mathf.RoundToInt(sp.Alpha * 255f);
        var fill = new Color32(f, f, f, fa);
        var edge = new Color32(e, e, e, 255);

        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                bool border = x < 2 || y < 2 || x >= n - 2 || y >= n - 2;
                bool inner = (x == 2 || y == 2 || x == n - 3 || y == n - 3);   // 안쪽 한 줄 밝기 차
                px[y * n + x] = border ? edge : (inner ? new Color32(e, e, e, fa) : fill);
            }

        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    static int Verify(Spec sp, StringBuilder log)
    {
        string path = OutDir + "/" + sp.Id + ".png";
        if (!File.Exists(path)) { log.AppendLine("  FAIL 파일 없음: " + path); return 1; }

        var spr = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (spr == null || ti == null) { log.AppendLine("  FAIL 임포트 실패: " + sp.Id); return 1; }

        bool sizeOk = Mathf.Approximately(spr.rect.width, sp.Size)
                   && Mathf.Approximately(spr.rect.height, sp.Size);
        bool ppuOk = Mathf.Approximately(spr.pixelsPerUnit, 100f);
        bool pointOk = ti.filterMode == FilterMode.Point;
        bool borderOk = !sp.Slice || (spr.border.x == BORDER && spr.border.y == BORDER
                                   && spr.border.z == BORDER && spr.border.w == BORDER);
        bool ok = sizeOk && ppuOk && pointOk && borderOk;

        log.AppendLine(string.Format(
            "  {0} {1,-18} {2}x{3}(기대 {4})  PPU {5}(기대 100)  필터 {6}(기대 Point)  테두리 {7}(기대 {8})",
            ok ? "OK  " : "FAIL", sp.Id, spr.rect.width, spr.rect.height, sp.Size,
            spr.pixelsPerUnit, ti.filterMode, spr.border.x, sp.Slice ? BORDER : 0));
        return ok ? 0 : 1;
    }
}
