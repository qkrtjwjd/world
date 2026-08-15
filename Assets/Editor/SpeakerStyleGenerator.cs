using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Scenario/node_map.json 의 speaker_display 블록 → Assets/Resources/Dialogue/SpeakerStyle.json.
///
/// 매핑표를 두 벌 관리하지 않기 위한 생성기다. 손으로 채우지 말 것 —
/// 정본이 개정돼 화자가 늘면 node_map.json 만 고치고 이 메뉴를 다시 돌린다.
/// (문서상 단일 출처는 node_map.json 의 speaker_display 다. CLAUDE.md §5 · F-4-4)
///
/// 산출물은 SpeakerStylePresenter 가 런타임에 읽는다.
/// </summary>
public static class SpeakerStyleGenerator
{
    const string NodeMapPath = "Scenario/node_map.json";              // 저장소 루트 기준
    const string OutputPath  = "Assets/Resources/Dialogue/SpeakerStyle.json";

    [MenuItem("무채색낙원/화자 스타일 갱신 (node_map → Resources)")]
    public static void Generate()
    {
        string repoRoot = Directory.GetParent(Application.dataPath).FullName;
        string src = Path.Combine(repoRoot, NodeMapPath);

        if (!File.Exists(src))
        {
            Debug.LogError($"[SpeakerStyleGenerator] node_map.json 을 찾지 못했습니다: {src}");
            return;
        }

        string json = File.ReadAllText(src, Encoding.UTF8);
        string block = ExtractObject(json, "\"speaker_display\"");
        if (block == null)
        {
            Debug.LogError("[SpeakerStyleGenerator] speaker_display 블록을 찾지 못했습니다.");
            return;
        }

        var entries = new List<string>();
        var skipped = new List<string>();

        foreach (var kv in EnumerateEntries(block))
        {
            string id = kv.Key;
            string body = kv.Value;

            // "_about" · "_참고표기" 처럼 밑줄로 시작하는 것은 주석이다.
            if (id.StartsWith("_")) continue;

            string display = Match1(body, "\"display\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            if (display == null) { skipped.Add($"{id} (display 없음)"); continue; }

            // 루의 display 는 "{$이름}" — Yarn 변수다. 변환기가 yarn 본문에서 이미 치환하므로
            // 런타임의 CharacterName 은 플레이어가 정한 이름이지 "루" 가 아니다. 치환 대상이 아니다.
            if (display.StartsWith("{"))
            {
                skipped.Add($"{id} (display 가 Yarn 변수 — 런타임에 이미 치환됨)");
                continue;
            }

            string style = ExtractObject(body, "\"style\"");
            bool italic = style != null && Regex.IsMatch(style, "\"italic\"\\s*:\\s*true");
            string color = style != null ? Match1(style, "\"color\"\\s*:\\s*\"([^\"]*)\"") : null;

            entries.Add(
                "    { \"id\": " + Quote(id) +
                ", \"display\": " + Quote(display) +
                ", \"italic\": " + (italic ? "true" : "false") +
                ", \"color\": " + Quote(color ?? "") + " }");
        }

        if (entries.Count == 0)
        {
            Debug.LogError("[SpeakerStyleGenerator] 추출된 항목이 0건입니다. 생성을 중단합니다.");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"_about\": \"자동 생성 파일. 직접 고치지 마세요 — Scenario/node_map.json 의 speaker_display 를 고치고 메뉴 [무채색낙원/화자 스타일 갱신] 을 다시 실행하세요.\",");
        sb.AppendLine("  \"entries\": [");
        sb.AppendLine(string.Join(",\n", entries));
        sb.AppendLine("  ]");
        sb.AppendLine("}");

        string dst = Path.Combine(repoRoot, OutputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(dst));
        File.WriteAllText(dst, sb.ToString(), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(OutputPath);

        Debug.Log($"[SpeakerStyleGenerator] {entries.Count}건 생성 → {OutputPath}" +
                  (skipped.Count > 0 ? $"\n  건너뜀: {string.Join(" · ", skipped)}" : ""));
    }

    // ── JSON 조각 추출 ────────────────────────────────────────────────
    // 완전한 JSON 파서를 붙이지 않는 이유: Unity 에 Newtonsoft 가 없고 JsonUtility 는
    // 딕셔너리를 못 읽는다. speaker_display 는 평면 구조라 중괄호 세기로 충분하다.

    /// <summary>key 뒤에 오는 { … } 를 중괄호 짝을 세어 통째로 잘라낸다. 없으면 null.</summary>
    static string ExtractObject(string s, string key)
    {
        int k = s.IndexOf(key, System.StringComparison.Ordinal);
        if (k < 0) return null;
        int open = s.IndexOf('{', k + key.Length);
        if (open < 0) return null;

        // key 와 { 사이에 다른 값(null 등)이 오면 그 key 의 객체가 아니다.
        string between = s.Substring(k + key.Length, open - (k + key.Length));
        if (!Regex.IsMatch(between, "^\\s*:\\s*$")) return null;

        int depth = 0;
        bool inStr = false, esc = false;
        for (int i = open; i < s.Length; i++)
        {
            char c = s[i];
            if (esc) { esc = false; continue; }
            if (c == '\\' && inStr) { esc = true; continue; }
            if (c == '"') { inStr = !inStr; continue; }
            if (inStr) continue;
            if (c == '{') depth++;
            else if (c == '}' && --depth == 0) return s.Substring(open, i - open + 1);
        }
        return null;
    }

    /// <summary>블록 안의 "키": { … } 쌍을 순서대로 돌려준다.</summary>
    static IEnumerable<KeyValuePair<string, string>> EnumerateEntries(string block)
    {
        int depth = 0;
        bool inStr = false, esc = false;
        string pendingKey = null;
        int keyEnd = -1;

        for (int i = 0; i < block.Length; i++)
        {
            char c = block[i];
            if (esc) { esc = false; continue; }
            if (c == '\\' && inStr) { esc = true; continue; }

            if (c == '"')
            {
                if (!inStr && depth == 1)
                {
                    int close = FindStringEnd(block, i);
                    if (close > 0)
                    {
                        pendingKey = block.Substring(i + 1, close - i - 1);
                        keyEnd = close;
                        i = close;
                        continue;
                    }
                }
                inStr = !inStr;
                continue;
            }
            if (inStr) continue;

            if (c == '{')
            {
                depth++;
                if (depth == 2 && pendingKey != null)
                {
                    string obj = ExtractBraces(block, i);
                    if (obj != null)
                    {
                        yield return new KeyValuePair<string, string>(pendingKey, obj);
                        i += obj.Length - 1;
                        depth--;
                        pendingKey = null;
                        continue;
                    }
                }
            }
            else if (c == '}') depth--;
            else if (c == ',' && depth == 1) pendingKey = null;
            else if (c == '[' && depth == 1 && keyEnd > 0)
            {
                // "_about": [ … ] 같은 주석 배열. 키를 버린다.
                pendingKey = null;
            }
        }
    }

    static int FindStringEnd(string s, int openQuote)
    {
        bool esc = false;
        for (int i = openQuote + 1; i < s.Length; i++)
        {
            if (esc) { esc = false; continue; }
            if (s[i] == '\\') { esc = true; continue; }
            if (s[i] == '"') return i;
        }
        return -1;
    }

    static string ExtractBraces(string s, int open)
    {
        int depth = 0;
        bool inStr = false, esc = false;
        for (int i = open; i < s.Length; i++)
        {
            char c = s[i];
            if (esc) { esc = false; continue; }
            if (c == '\\' && inStr) { esc = true; continue; }
            if (c == '"') { inStr = !inStr; continue; }
            if (inStr) continue;
            if (c == '{') depth++;
            else if (c == '}' && --depth == 0) return s.Substring(open, i - open + 1);
        }
        return null;
    }

    static string Match1(string s, string pattern)
    {
        var m = Regex.Match(s, pattern);
        return m.Success ? Unescape(m.Groups[1].Value) : null;
    }

    static string Unescape(string s) =>
        s.Replace("\\\"", "\"").Replace("\\\\", "\\").Replace("\\n", "\n");

    static string Quote(string s) =>
        "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";
}
