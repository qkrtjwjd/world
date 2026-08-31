// 맵 오브젝트 더미 생성기 — 실제 도트가 나오기 전 배선용 (사용자 규격서 8장 「맵에 놓인 물건」)
//
// 규격: 32×32 · PPU 32 · Point · 피벗 **Center**.
//   Square(256×256 · PPU 256)와 32×32 · PPU 32 는 **둘 다 1 월드유닛**이다.
//   그래서 스프라이트만 갈아끼우면 화면 크기가 그대로다 — 스케일을 건드릴 필요가 없다.
//   피벗도 Center 로 맞춰야 위치가 안 밀린다 (PixelArtImportPostprocessor 의 Objects 분기).
//
// 실제 도트가 오면 **같은 파일명으로 덮어쓰면 끝난다.**
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class DummyObjectGenerator
{
    const string OutDir = "Assets/Art/Objects/_dummy";
    const int W = 32, H = 32;

    enum Form { BoxClosed, BoxOpen, Drawer, DrawerOpen, Rack, Dresser, Door, Lock,
                Plate, Chair, Campfire, Cart, Pot, Sign,
                CartGone, PotGone, SignGone }

    class Spec
    {
        public string Id;
        public Form Kind;
        public float Tone;
        public string Note;
    }

    // 규격서 8장 「맵에 놓인 물건」 목록 그대로. 명도는 서로 벌려 둔다(규격서 4장).
    static readonly Spec[] Specs =
    {
        new Spec { Id = "box_closed",           Kind = Form.BoxClosed,  Tone = 0.55f, Note = "다락방 상자 — 닫힘" },
        new Spec { Id = "box_open",             Kind = Form.BoxOpen,    Tone = 0.70f, Note = "다락방 상자 — 열림(코트·라디오·단검이 보인다)" },
        new Spec { Id = "kitchen_drawer",       Kind = Form.Drawer,     Tone = 0.45f, Note = "부엌 서랍 — 닫힘" },
        new Spec { Id = "kitchen_drawer_open",  Kind = Form.DrawerOpen, Tone = 0.60f, Note = "부엌 서랍 — 열림" },
        new Spec { Id = "shoe_rack",            Kind = Form.Rack,       Tone = 0.50f, Note = "신발장" },
        new Spec { Id = "living_dresser",       Kind = Form.Dresser,    Tone = 0.65f, Note = "거실 서랍장" },
        new Spec { Id = "sera_door",            Kind = Form.Door,       Tone = 0.40f, Note = "세라 방 문" },
        new Spec { Id = "window_lock",          Kind = Form.Lock,       Tone = 0.80f, Note = "창문 잠금장치" },
        new Spec { Id = "plate",                Kind = Form.Plate,      Tone = 0.85f, Note = "식탁 접시" },
        new Spec { Id = "chair_empty",          Kind = Form.Chair,      Tone = 0.35f, Note = "빈 의자" },
        new Spec { Id = "campfire",             Kind = Form.Campfire,   Tone = 0.75f, Note = "캠프파이어 — 숲 야영지" },
        new Spec { Id = "cover_cart",           Kind = Form.Cart,       Tone = 0.55f, Note = "마을 엄폐물 — 수레" },
        new Spec { Id = "cover_pot",            Kind = Form.Pot,        Tone = 0.70f, Note = "마을 엄폐물 — 화분" },
        new Spec { Id = "cover_sign",           Kind = Form.Sign,       Tone = 0.45f, Note = "마을 엄폐물 — 간판" },

        // 소실 단계 3종 (F-6-1). 라운드가 끝날 때마다 수레 → 화분 → 간판 순으로 이 모습이 된다.
        // 결계가 조여든 결과이지 세라가 치우는 것이 아니므로 세라의 동선과 무관하게 일어난다(C-14-3-4).
        // 그냥 사라지게 두면 버그로 읽히므로 남은 자리를 그린다. 명도는 원본보다 낮춘다.
        new Spec { Id = "cover_cart_gone",      Kind = Form.CartGone,   Tone = 0.30f, Note = "마을 엄폐물 소실 — 수레" },
        new Spec { Id = "cover_pot_gone",       Kind = Form.PotGone,    Tone = 0.38f, Note = "마을 엄폐물 소실 — 화분" },
        new Spec { Id = "cover_sign_gone",      Kind = Form.SignGone,   Tone = 0.25f, Note = "마을 엄폐물 소실 — 간판" },
    };

    [MenuItem("Tools/도트/맵 오브젝트 더미 생성")]
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
                log.AppendLine(string.Format("  {0,-20} {1,-11} 명도 {2:0.00} — {3}",
                    sp.Id, sp.Kind, sp.Tone, sp.Note));
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
        foreach (var p in made) fails += Verify(p, log);

        log.AppendLine();
        log.AppendLine("fails = " + fails);
        File.WriteAllText(Path.Combine(outDir, "dummy_object.txt"), log.ToString(),
                          new UTF8Encoding(false));
        Debug.Log("[DummyObjectGenerator] fails=" + fails);

        if (Application.isBatchMode) EditorApplication.Exit(fails == 0 ? 0 : 1);
    }

    static Texture2D Draw(Spec sp)
    {
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        var px = new Color32[W * H];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 0);

        byte v = (byte)Mathf.RoundToInt(sp.Tone * 255f);
        var col = new Color32(v, v, v, 255);
        var dim = new Color32((byte)(v * 0.55f), (byte)(v * 0.55f), (byte)(v * 0.55f), 255);
        int cx = W / 2;

        switch (sp.Kind)
        {
            case Form.BoxClosed:
                Rect(px, 4, 4, 24, 20, col);
                Rect(px, 4, 20, 24, 4, dim);              // 뚜껑
                break;
            case Form.BoxOpen:
                Rect(px, 4, 4, 24, 16, col);
                Rect(px, 6, 12, 20, 8, dim);              // 열린 안쪽
                Rect(px, 2, 22, 12, 4, dim);              // 젖혀진 뚜껑
                break;
            case Form.Drawer:
                Rect(px, 5, 6, 22, 18, col);
                Rect(px, 12, 14, 8, 2, dim);              // 손잡이
                break;
            case Form.DrawerOpen:
                Rect(px, 5, 6, 22, 18, col);
                Rect(px, 3, 10, 24, 8, dim);              // 빠져나온 서랍
                break;
            case Form.Rack:
                Rect(px, 5, 3, 22, 24, col);
                Rect(px, 7, 9, 18, 2, dim);
                Rect(px, 7, 15, 18, 2, dim);
                Rect(px, 7, 21, 18, 2, dim);              // 선반 3단
                break;
            case Form.Dresser:
                Rect(px, 4, 5, 24, 22, col);
                Rect(px, 6, 18, 20, 6, dim);
                Rect(px, 6, 9, 20, 6, dim);               // 서랍 2단
                break;
            case Form.Door:
                Rect(px, 9, 2, 14, 28, col);
                Disc(px, 20, 15, 2, dim);                 // 손잡이
                break;
            case Form.Lock:
                Ring(px, cx, 20, 6, col);                 // 고리
                Rect(px, cx - 6, 8, 12, 10, col);         // 자물통
                Rect(px, cx - 1, 12, 2, 3, dim);
                break;
            case Form.Plate:
                Disc(px, cx, 16, 11, col);
                Disc(px, cx, 16, 7, dim);
                break;
            case Form.Chair:
                Rect(px, 8, 14, 16, 3, col);              // 앉는 면
                Rect(px, 8, 17, 3, 11, col);              // 등받이
                Rect(px, 9, 5, 3, 9, col);
                Rect(px, 20, 5, 3, 9, col);               // 다리
                break;
            case Form.Campfire:
                Rect(px, 6, 6, 20, 3, dim);               // 장작
                Rect(px, 9, 9, 14, 3, dim);
                Rect(px, cx - 4, 12, 8, 8, col);          // 불
                Rect(px, cx - 2, 20, 4, 6, col);
                break;
            case Form.Cart:
                Rect(px, 4, 12, 24, 10, col);             // 짐칸
                Disc(px, 10, 8, 4, dim);
                Disc(px, 22, 8, 4, dim);                  // 바퀴
                break;
            case Form.Pot:
                Rect(px, 9, 4, 14, 12, col);              // 화분
                Rect(px, 7, 15, 18, 3, dim);              // 테두리
                Rect(px, cx - 1, 18, 2, 8, dim);          // 줄기
                break;
            case Form.Sign:
                Rect(px, 5, 14, 22, 12, col);             // 판
                Rect(px, cx - 1, 3, 2, 11, dim);          // 기둥
                Rect(px, 8, 19, 16, 2, dim);
                break;

            // ── 소실 단계 — 숨을 수 없는 잔해만 남는다 ──────────────────────
            case Form.CartGone:
                Rect(px, 5, 4, 22, 3, col);               // 부서진 바닥판
                Disc(px, 22, 7, 4, dim);                  // 바퀴 하나만 남는다
                break;
            case Form.PotGone:
                Rect(px, 10, 4, 12, 3, col);              // 깨진 밑동
                Rect(px, 8, 4, 3, 2, dim);
                Rect(px, 21, 4, 3, 2, dim);               // 흩어진 조각
                break;
            case Form.SignGone:
                Rect(px, cx - 1, 4, 2, 6, col);           // 부러진 기둥 밑동
                Rect(px, 6, 4, 5, 2, dim);                // 떨어진 판 조각
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

    static void Ring(Color32[] px, int cx, int cy, int r, Color32 c)
    {
        for (int y = cy - r; y <= cy + r; y++)
        {
            if (y < 0 || y >= H) continue;
            for (int x = cx - r; x <= cx + r; x++)
            {
                if (x < 0 || x >= W) continue;
                int dx = x - cx, dy = y - cy, d = dx * dx + dy * dy;
                if (d <= r * r && d >= (r - 2) * (r - 2)) px[y * W + x] = c;
            }
        }
    }

    /// <summary>PNG 를 다시 읽어 캔버스·내용을 픽셀로 판정하고 임포트된 PPU·피벗까지 본다.
    /// 맵 오브젝트는 피벗이 Center 다 — 씬의 기존 배치가 그것을 전제로 한다.</summary>
    static int Verify(string path, StringBuilder log)
    {
        if (!File.Exists(path)) { log.AppendLine("  FAIL " + path + " — 파일 없음"); return 1; }

        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(File.ReadAllBytes(path));
        int w = tex.width, h = tex.height, solid = 0;
        var px = tex.GetPixels32();
        foreach (var p in px) if (p.a > 0) solid++;
        Object.DestroyImmediate(tex);

        var spr = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        float ppu = spr != null ? spr.pixelsPerUnit : -1f;
        float pivX = (spr != null && spr.rect.width > 0) ? spr.pivot.x / spr.rect.width : -1f;
        float pivY = (spr != null && spr.rect.height > 0) ? spr.pivot.y / spr.rect.height : -1f;

        bool ok = (w == W) && (h == H) && (solid > 0)
                  && Mathf.Approximately(ppu, 32f)
                  && Mathf.Abs(pivX - 0.5f) < 0.001f && Mathf.Abs(pivY - 0.5f) < 0.001f;

        log.AppendLine(string.Format(
            "  {0} {1,-24} 캔버스 {2}x{3}(기대 {4}x{5})  채운픽셀 {6,4}  PPU {7}(기대 32)  피벗 {8:0.00},{9:0.00}(기대 0.50,0.50)",
            ok ? "OK  " : "FAIL", Path.GetFileName(path), w, h, W, H, solid, ppu, pivX, pivY));
        return ok ? 0 : 1;
    }
}
