using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using TMPro;

/// <summary>
/// 게임에 쓰이는 글자를 TMP 글꼴 아틀라스에 미리 굽는다(동적 모드는 유지 — 플레이어가 입력한 이름 등은 그때 추가된다).
///
/// <para>⚠ 동적 글꼴은 아틀라스에 없는 글자를 처음 그릴 때 글꼴 파일을 열어 글꼴 기능표(GPOS · GSUB)를 읽고 글자를 추가한다 —
/// 47~67ms + 묶음마다 1.5~16ms 의 끊김(수동작업 4-4). 예전에는 yarn 과 .cs 의 한글만 구워서, 씬 · 프리팹 · 데이터 에셋
/// (전투 해설 · 아이템 설명 등) · JSON(배드엔딩 문구)에만 있는 글자가 런타임에 처음 쓰일 때 끊겼다(턴제 전투 시작 94ms 중 73ms).
/// 2026-10-05 수집 범위를 넓혔다(실측: 런타임 추가 590자 → 0 · 첫 그리기 합계 2039ms → 24ms).</para>
/// </summary>
public static class KoreanFontAssetPopulator
{
    /// <summary>미리 구울 글꼴 — 실제로 쓰이는 것만(나머지 글꼴 에셋은 어디서도 참조하지 않는다).</summary>
    public static readonly string[] FontPaths =
    {
        "Assets/Font/Pretendard-Medium SDF.asset",   // 본문 · UI · TMP 기본 글꼴의 대체 글꼴
        // HS유지체 는 Home 의 NoteBody 하나가 참조하지만 쪽지 문구 출력은 폐지됐다(F-4-4 v1.25 — 문구까지 그린 이미지 1장). 굽지 않는다.
    };

    [MenuItem("Tools/세계/Populate Korean Font Atlas")]
    static void PopulateMenu() => PopulateAll();

    /// <summary>배치용: <c>-executeMethod KoreanFontAssetPopulator.PopulateBatch</c></summary>
    public static void PopulateBatch()
    {
        PopulateAll();
        EditorApplication.Exit(0);
    }

    public static void PopulateAll()
    {
        string chars = CollectGameCharacters();
        foreach (var path in FontPaths)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font == null) { Debug.LogError("[KoreanFontAssetPopulator] 글꼴 없음: " + path); continue; }
            int before = font.characterTable.Count;
            bool ok = font.TryAddCharacters(chars, out string missing);
            EditorUtility.SetDirty(font);
            Debug.Log($"[KoreanFontAssetPopulator] {Path.GetFileName(path)} — 수집 {chars.Length}자 · 글자표 {before} → {font.characterTable.Count} · " +
                      $"{(ok ? "전부 들어감" : $"글꼴에 없는 글자 {missing?.Length ?? 0}: {Shorten(missing)}")} · 아틀라스 {font.atlasTextures?.Length ?? 0}장");
        }
        AssetDatabase.SaveAssets();
    }

    static string Shorten(string s) => string.IsNullOrEmpty(s) ? "" : (s.Length > 40 ? s.Substring(0, 40) + "…" : s);

    static readonly Regex StringLiteral = new Regex("\"(?:[^\"\\\\]|\\\\.)*\"", RegexOptions.Compiled);
    static readonly Regex UnicodeEscape = new Regex(@"\\u([0-9A-Fa-f]{4})", RegexOptions.Compiled);

    /// <summary>게임에 쓰이는 글자 — ASCII · 자주 쓰는 문장부호 · yarn · .cs 문자열 · 씬/프리팹/에셋/JSON/txt 의 비ASCII 글자.</summary>
    public static string CollectGameCharacters()
    {
        var chars = new HashSet<char>();
        for (int i = 0x20; i <= 0x7E; i++) chars.Add((char)i);
        int[] extra = { 0x2026, 0x2018, 0x2019, 0x201C, 0x201D, 0x00B7, 0x2014, 0x2013, 0x300C, 0x300D, 0x300E, 0x300F,
                        0x3010, 0x3011, 0x3014, 0x3015, 0x300A, 0x300B, 0x3008, 0x3009, 0xFF01, 0xFF1F, 0xFF0C, 0xFF0E,
                        0x2190, 0x2191, 0x2192, 0x2193, 0x25B6, 0x25C0, 0x25B2, 0x25BC, 0x2022, 0x00D7, 0x0025 };
        foreach (int cp in extra) chars.Add((char)cp);

        string assets = Application.dataPath;
        void AddText(string text, bool decodeEscapes)
        {
            if (decodeEscapes)
                text = UnicodeEscape.Replace(text, m => ((char)System.Convert.ToInt32(m.Groups[1].Value, 16)).ToString());
            foreach (char c in text) if (Wanted(c)) chars.Add(c);
        }

        foreach (var f in Files(assets, "*.yarn")) AddText(File.ReadAllText(f, Encoding.UTF8), false);
        // .cs 는 문자열 리터럴만 — 주석의 글자까지 구우면 아틀라스만 커진다
        foreach (var f in Files(assets, "*.cs"))
        {
            if (f.Replace('\\', '/').Contains("/Editor/")) continue;
            foreach (Match m in StringLiteral.Matches(File.ReadAllText(f, Encoding.UTF8))) AddText(m.Value, true);
        }
        foreach (var pattern in new[] { "*.unity", "*.prefab", "*.asset" })
            foreach (var f in Files(assets, pattern))
            {
                if (f.Replace('\\', '/').Contains("/Font/")) continue;   // 글꼴 에셋 자신(글자표)은 빼고
                var info = new FileInfo(f);
                if (info.Length > 40 * 1024 * 1024) continue;
                string text = File.ReadAllText(f, Encoding.UTF8);
                if (!text.StartsWith("%YAML")) continue;   // 바이너리 에셋
                AddText(text, true);
            }
        foreach (var pattern in new[] { "*.json", "*.txt", "*.csv" })
            foreach (var f in Files(assets, pattern)) AddText(File.ReadAllText(f, Encoding.UTF8), true);

        var sb = new StringBuilder();
        foreach (char c in chars.OrderBy(c => c)) sb.Append(c);
        return sb.ToString();
    }

    static IEnumerable<string> Files(string root, string pattern) => Directory.GetFiles(root, pattern, SearchOption.AllDirectories);

    /// <summary>구울 글자 — 한글 음절 · 자모 · CJK 문장부호 · 전각 · 라틴 확장 일부. 제어문자 · 대리쌍은 뺀다.</summary>
    static bool Wanted(char c)
    {
        if (c < 0x20 || char.IsSurrogate(c)) return false;
        if (c <= 0x7E) return true;
        return (c >= 0xAC00 && c <= 0xD7A3)    // 한글 음절
            || (c >= 0x3130 && c <= 0x318F)    // 호환 자모
            || (c >= 0x1100 && c <= 0x11FF)    // 자모
            || (c >= 0x3000 && c <= 0x303F)    // CJK 문장부호
            || (c >= 0xFF00 && c <= 0xFFEF)    // 전각
            || (c >= 0x2000 && c <= 0x206F)    // 일반 문장부호
            || (c >= 0x2190 && c <= 0x21FF)    // 화살표
            || (c >= 0x25A0 && c <= 0x25FF)    // 도형
            || (c >= 0x00A0 && c <= 0x00FF);   // 라틴-1
    }
}
