// NPC 더미 걷기·대기 스프라이트 생성기 — 실제 도트가 나오기 전 배선용 (CLAUDE.md §11)
//
// DummyLuSpriteGenerator 와 같은 규격·같은 그리기 방식이다. 캐릭터 목록만 데이터로 뺐다.
//   캔버스 32x48 / 바닥에서 2px 띄움 (피벗 2/48 과 일치) / 실루엣 높이는 캐릭터별
//
// right 방향은 만들지 않는다. left 를 flipX 로 뒤집어 쓴다 (§11 어긋남 3).
//
// 색 구분은 스프라이트에 굽지 않는다 — 씬의 SpriteRenderer.color 가 담당한다.
// 그래서 톤을 밝게(0.70~1.00) 잡는다. 실제 아트가 오면 color 를 흰색으로 되돌리면 된다.
//
// 나중에 실제 도트로 교체할 때 **파일만 덮어쓰면 되도록** 파일명과 규격을 맞춰 둔다.
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class DummyCharSpriteGenerator
{
    const string Root = "Assets/Art/Characters";

    // 규격 상수 — 바꾸지 말 것. CLAUDE.md §11 과 같이 움직여야 한다.
    const int W = 32, H = 48;
    const int FootMargin = 2;    // 발끝 아래 여백
    const int LegH = 8, HeadH = 12;   // 몸통 높이는 실루엣 높이에서 역산한다

    // 방향별 명도. 단색 실루엣에서 방향을 구분하기 위한 최소 장치.
    // 루(0.25~0.65)보다 밝다 — NPC 는 color 틴트를 받기 때문이다.
    static readonly (string dir, float tone)[] Tone =
    {
        ("down", 0.70f),
        ("left", 0.85f),
        ("up",   1.00f),
    };

    class Spec
    {
        public string Folder;      // Assets/Art/Characters/<Folder>/_dummy
        public string Prefix;      // 파일명 접두
        public int SilHeight;      // 실루엣 높이(px)
        public bool Walk;          // true = 3방향 x 3프레임, false = 정면 1장
        public string Note;
    }

    // 정본 D 근거:
    //   문단 625  세라 마을 보행(뛰는 모션 없음) / 솔 좌판 앉은 자세 /
    //             미루 반죽 치는 루프 / 아모 화분 만지는 루프
    //   문단 1027 쿠루 … 시선을 피하고 앞서 걷는 동작
    // 키: B 문단 22(루 외형 16세) · 1063(쿠루 외형 16~18세) 기준으로 세라만 성인 체격.
    static readonly Spec[] Specs =
    {
        new Spec { Folder = "Sera", Prefix = "sera", SilHeight = 44, Walk = true,
                   Note = "규격서 44px. D-625 마을 보행(뛰는 모션 없음)" },
        new Spec { Folder = "Kuru", Prefix = "kuru", SilHeight = 46, Walk = true,
                   Note = "규격서 46px — 루보다 확실히 커야 한다. D-1027 앞서 걷는 동작" },
        new Spec { Folder = "Sol",  Prefix = "sol",  SilHeight = 32, Walk = false,
                   Note = "규격서 32px — 앉은 자세 기준. D-625 좌판 앉은 자세" },
        new Spec { Folder = "Miru", Prefix = "miru", SilHeight = 34, Walk = false,
                   Note = "규격서 34px. D-625 반죽 치는 루프" },
        new Spec { Folder = "Amo",  Prefix = "amo",  SilHeight = 34, Walk = false,
                   Note = "규격서 34px. D-625 화분 만지는 루프" },
    };

    [MenuItem("Tools/도트/NPC 더미 스프라이트 생성")]
    public static void Run()
    {
        string outDir = System.Environment.GetEnvironmentVariable("DUMMY_OUT");
        if (string.IsNullOrEmpty(outDir)) outDir = Path.GetTempPath();

        var log = new StringBuilder();
        int fails = 0;
        var made = new List<string>();

        try
        {
            foreach (var sp in Specs)
            {
                string dir = string.Format("{0}/{1}/_dummy", Root, sp.Folder);
                Directory.CreateDirectory(dir);
                log.AppendLine(string.Format("[{0}] 실루엣 {1}px · {2} — {3}",
                    sp.Folder, sp.SilHeight, sp.Walk ? "보행 9장" : "정면 1장", sp.Note));

                if (sp.Walk)
                {
                    foreach (var t in Tone)
                        for (int frame = 1; frame <= 3; frame++)
                        {
                            // 01=정지 02=왼발 03=오른발 (규격서 5장)
                            string path = string.Format("{0}/{1}_walk_{2}_{3:00}.png",
                                                        dir, sp.Prefix, t.dir, frame);
                            Write(path, sp, t.dir, t.tone, frame);
                            made.Add(path);
                        }
                }
                else
                {
                    string path = string.Format("{0}/{1}_idle_down.png", dir, sp.Prefix);
                    Write(path, sp, "down", Tone[0].tone, 1);
                    made.Add(path);
                }
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
        foreach (var p in made)
        {
            var sp = SpecOf(p);
            fails += Verify(p, sp.SilHeight, log);
        }

        log.AppendLine();
        log.AppendLine("fails = " + fails);
        File.WriteAllText(Path.Combine(outDir, "dummy_npc.txt"), log.ToString(),
                          new UTF8Encoding(false));
        Debug.Log("[DummyCharSpriteGenerator] fails=" + fails);

        if (Application.isBatchMode) EditorApplication.Exit(fails == 0 ? 0 : 1);
    }

    static Spec SpecOf(string path)
    {
        foreach (var sp in Specs)
            if (path.Contains("/" + sp.Folder + "/")) return sp;
        return Specs[0];
    }

    static void Write(string path, Spec sp, string dir, float tone, int frame)
    {
        var tex = Draw(sp, dir, tone, frame);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    /// <summary>32x48 캔버스에 단색 실루엣을 그린다. y 는 아래가 0.
    /// frame 1=정지 2=왼발 3=오른발. 든 다리만 2px 올리므로 실루엣 높이·바닥 여백은 변하지 않는다.</summary>
    static Texture2D Draw(Spec sp, string dir, float tone, int frame)
    {
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        var px = new Color32[W * H];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 0);

        byte v = (byte)Mathf.RoundToInt(tone * 255f);
        var col = new Color32(v, v, v, 255);

        int baseY = FootMargin;                     // 발끝
        int cx = W / 2;
        int bodyH = sp.SilHeight - LegH - HeadH;    // 40→20 · 44→24 · 46→26 · 34→14 · 32→12

        // 다리 2개 — 사이를 비워 실루엣이 뭉치지 않게 한다.
        // 든 발은 2px 짧게 그려 위로 올라간 것처럼 보이게 한다.
        int lLift = (frame == 2) ? 2 : 0;           // 왼발
        int rLift = (frame == 3) ? 2 : 0;           // 오른발
        Rect(px, cx - 5, baseY + lLift, 4, LegH - lLift, col);
        Rect(px, cx + 1, baseY + rLift, 4, LegH - rLift, col);

        // 몸통
        Rect(px, cx - 7, baseY + LegH, 14, bodyH, col);

        // 머리. left 는 옆모습이라 좁고 왼쪽으로 치우친다 — 방향을 눈으로 구분하기 위한 것
        int headY = baseY + LegH + bodyH;
        if (dir == "left") Rect(px, cx - 8, headY, 8, HeadH, col);
        else               Rect(px, cx - 6, headY, 12, HeadH, col);

        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    static void Rect(Color32[] px, int x0, int y0, int w, int h, Color32 col)
    {
        for (int y = y0; y < y0 + h; y++)
        {
            if (y < 0 || y >= H) continue;
            for (int x = x0; x < x0 + w; x++)
            {
                if (x < 0 || x >= W) continue;
                px[y * W + x] = col;
            }
        }
    }

    /// <summary>PNG 를 다시 읽어 캔버스·실루엣 높이·바닥 여백을 픽셀로 판정하고,
    /// 임포트된 Sprite 의 PPU·피벗까지 확인한다.</summary>
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

        int silH = (minY > maxY) ? 0 : maxY - minY + 1;
        int margin = (minY > maxY) ? -1 : minY;
        int wantMargin = FootMargin;

        // 임포트 결과(후처리기가 붙인 도트 프리셋)까지 본다
        var spr = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        float ppu = spr != null ? spr.pixelsPerUnit : -1f;
        float piv = (spr != null && spr.rect.height > 0) ? spr.pivot.y / spr.rect.height : -1f;

        bool ok = (w == W) && (h == H) && (silH == silHeight) && (margin == wantMargin)
                  && Mathf.Approximately(ppu, 32f) && Mathf.Abs(piv - 2f / 48f) < 0.001f;

        log.AppendLine(string.Format(
            "  {0} {1}  캔버스 {2}x{3}  실루엣 {4}px(기대 {5})  바닥여백 {6}(기대 {7})  PPU {8}  피벗 {9:0.0000}",
            ok ? "OK  " : "FAIL", Path.GetFileName(path), w, h, silH, silHeight,
            margin, wantMargin, ppu, piv));
        return ok ? 0 : 1;
    }
}
