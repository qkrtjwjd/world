// 루 더미 걷기 스프라이트 생성기 — 실제 도트가 나오기 전 배선용 (CLAUDE.md §11)
//
// 나중에 실제 도트로 교체할 때 **파일만 덮어쓰면 되도록** 규격을 정확히 맞춘다.
//   캔버스 32x48 / 실루엣 높이 40px / 바닥에서 2px 띄움 (피벗 2/48 과 일치)
//
// right 방향은 만들지 않는다. left 를 flipX 로 뒤집어 쓴다 (CLAUDE.md §11 어긋남 3).
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class DummyLuSpriteGenerator
{
    const string OutDir = "Assets/Art/Characters/Lu/_dummy";
    const string LogPath = "C:/Users/THINKP~1/AppData/Local/Temp/claude/C--Users-ThinkPlant/" +
                           "a20ceac4-42db-47b5-a689-453a65613428/scratchpad/dummy_lu.txt";

    // 규격 상수 — 바꾸지 말 것. CLAUDE.md §11 과 같이 움직여야 한다.
    const int W = 32, H = 48;
    const int FootMargin = 2;    // 발끝 아래 여백
    const int SilHeight  = 40;   // 실루엣 높이

    // 방향별 명도. 단색 실루엣을 유지하면서 방향을 구분하기 위한 최소 장치.
    static readonly Dictionary<string, float> Tone = new Dictionary<string, float>
    {
        { "down", 0.25f },
        { "left", 0.45f },
        { "up",   0.65f },
    };

    [MenuItem("Tools/도트/루 더미 걷기 스프라이트 생성")]
    public static void Run()
    {
        var log = new StringBuilder();
        int fails = 0;

        try
        {
            Directory.CreateDirectory(OutDir);

            foreach (var kv in Tone)
            {
                for (int frame = 1; frame <= 3; frame++)
                {
                    // 02 만 1px 위로 — 걷기 bob. 01/03 은 접지 자세로 동일하다.
                    int lift = (frame == 2) ? 1 : 0;
                    var tex = Draw(kv.Key, kv.Value, lift);

                    string path = $"{OutDir}/lu_walk_{kv.Key}_{frame:00}.png";
                    File.WriteAllBytes(path, tex.EncodeToPNG());
                    Object.DestroyImmediate(tex);
                    log.AppendLine($"  {path}  (lift={lift})");
                }
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }
        catch (System.Exception e)
        {
            fails++;
            log.AppendLine("EXCEPTION: " + e);
        }

        // 있음을 센다 — 파일 존재가 아니라 실제 픽셀을 판정한다
        log.AppendLine();
        log.AppendLine("=== 규격 검증 ===");
        foreach (var kv in Tone)
            for (int frame = 1; frame <= 3; frame++)
                fails += Verify($"{OutDir}/lu_walk_{kv.Key}_{frame:00}.png", frame == 2 ? 1 : 0, log);

        log.AppendLine();
        log.AppendLine("fails = " + fails);
        Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
        File.WriteAllText(LogPath, log.ToString(), new UTF8Encoding(false));

        if (Application.isBatchMode) EditorApplication.Exit(fails == 0 ? 0 : 1);
    }

    /// <summary>32x48 캔버스에 40px 단색 실루엣을 그린다. y 는 아래가 0.</summary>
    static Texture2D Draw(string dir, float tone, int lift)
    {
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        var px = new Color32[W * H];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 0);

        byte v = (byte)Mathf.RoundToInt(tone * 255f);
        var col = new Color32(v, v, v, 255);

        int baseY = FootMargin + lift;          // 발끝
        int cx    = W / 2;

        // 다리 2개 (8px) — 두 다리 사이를 비워 실루엣이 뭉치지 않게 한다
        Rect(px, cx - 5, baseY, 4, 8, col);
        Rect(px, cx + 1, baseY, 4, 8, col);

        // 몸통 (20px)
        Rect(px, cx - 7, baseY + 8, 14, 20, col);

        // 머리 (12px). left 는 옆모습이라 좁고 왼쪽으로 치우친다 — 방향을 눈으로 구분하기 위한 것
        if (dir == "left") Rect(px, cx - 8, baseY + 28, 8,  12, col);
        else               Rect(px, cx - 6, baseY + 28, 12, 12, col);

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

    /// <summary>PNG 를 다시 읽어 캔버스 크기·실루엣 높이·바닥 여백을 픽셀로 판정한다.</summary>
    static int Verify(string path, int expectedLift, StringBuilder log)
    {
        if (!File.Exists(path)) { log.AppendLine($"  FAIL {path} — 파일 없음"); return 1; }

        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(File.ReadAllBytes(path));

        int w = tex.width, h = tex.height;
        int minY = int.MaxValue, maxY = int.MinValue;
        var px = tex.GetPixels32();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (px[y * w + x].a > 0) { if (y < minY) minY = y; if (y > maxY) maxY = y; }
        Object.DestroyImmediate(tex);

        int silH   = (minY > maxY) ? 0 : maxY - minY + 1;
        int margin = (minY > maxY) ? -1 : minY;
        int wantMargin = FootMargin + expectedLift;

        bool ok = (w == W) && (h == H) && (silH == SilHeight) && (margin == wantMargin);
        log.AppendLine($"  {(ok ? "OK  " : "FAIL")} {Path.GetFileName(path)}  " +
                       $"캔버스 {w}x{h} (기대 {W}x{H})  실루엣 {silH}px (기대 {SilHeight})  " +
                       $"바닥여백 {margin}px (기대 {wantMargin})");
        return ok ? 0 : 1;
    }
}
