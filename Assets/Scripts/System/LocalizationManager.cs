using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// JSON 로컬라이제이션 매니저.
/// JSON 키를 평탄화(flatten)해서 딕셔너리로 저장합니다.
/// 아이템이 늘어나도 이 스크립트를 건드릴 필요가 없습니다.
/// </summary>
public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager Instance;

    public enum Language { KO, EN, JP }
    public Language currentLanguage = Language.KO;

    private Dictionary<string, string> _table = new Dictionary<string, string>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadLanguage(currentLanguage);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void ChangeLanguage(Language lang)
    {
        currentLanguage = lang;
        LoadLanguage(lang);
    }

    // ─────────────────────────────────────────────
    //  로딩
    // ─────────────────────────────────────────────
    void LoadLanguage(Language lang)
    {
        _table.Clear();

        string fileName = lang.ToString().ToLower(); // ko / en / jp
        TextAsset json  = Resources.Load<TextAsset>($"Localization/{fileName}");

        if (json == null)
        {
            Debug.LogError($"[LocalizationManager] 파일 없음: Localization/{fileName}");
            return;
        }

        // MiniJSON 없이 직접 평탄화 파싱
        FlattenJson(json.text, "", _table);
        Debug.Log($"[LocalizationManager] {lang} 로드 완료 ({_table.Count}개 키)");
    }

    // ─────────────────────────────────────────────
    //  JSON 평탄화 (Nested JSON -> "section.key" = "value")
    //  Unity 빌트인 JsonUtility 는 Dictionary 를 못 쓰므로
    //  간단한 수동 파서로 처리합니다.
    // ─────────────────────────────────────────────
    static void FlattenJson(string json, string prefix,
                             Dictionary<string, string> result)
    {
        // 바깥 중괄호 제거
        json = json.Trim();
        if (json.StartsWith("{")) json = json.Substring(1);
        if (json.EndsWith("}"))   json = json.Substring(0, json.Length - 1);

        int i = 0;
        while (i < json.Length)
        {
            // 키 찾기
            int keyStart = json.IndexOf('"', i);
            if (keyStart < 0) break;
            int keyEnd = json.IndexOf('"', keyStart + 1);
            if (keyEnd < 0) break;
            string key = json.Substring(keyStart + 1, keyEnd - keyStart - 1);

            // ':' 찾기
            int colon = json.IndexOf(':', keyEnd);
            if (colon < 0) break;

            // 값 시작
            int valStart = colon + 1;
            while (valStart < json.Length && json[valStart] == ' ') valStart++;

            string fullKey = string.IsNullOrEmpty(prefix) ? key : $"{prefix}.{key}";

            if (valStart < json.Length && json[valStart] == '{')
            {
                // 중첩 객체 → 재귀
                int depth = 0;
                int objEnd = valStart;
                for (; objEnd < json.Length; objEnd++)
                {
                    if (json[objEnd] == '{') depth++;
                    else if (json[objEnd] == '}') { depth--; if (depth == 0) break; }
                }
                string nested = json.Substring(valStart, objEnd - valStart + 1);
                FlattenJson(nested, fullKey, result);
                i = objEnd + 1;
            }
            else if (valStart < json.Length && json[valStart] == '"')
            {
                // 문자열 값
                int valEnd = valStart + 1;
                while (valEnd < json.Length)
                {
                    if (json[valEnd] == '\\') { valEnd += 2; continue; }
                    if (json[valEnd] == '"')  break;
                    valEnd++;
                }
                string val = json.Substring(valStart + 1, valEnd - valStart - 1);
                // 이스케이프 처리
                val = val.Replace("\\n", "\n").Replace("\\t", "\t").Replace("\\\"", "\"");
                result[fullKey] = val;
                i = valEnd + 1;
            }
            else
            {
                // 숫자·불리언 등 (문자열이 아닌 값 — 로컬라이제이션에선 거의 없음)
                int nextComma = json.IndexOf(',', valStart);
                int nextBrace = json.IndexOf('}', valStart);
                int valEnd    = (nextComma >= 0 && nextComma < nextBrace) ? nextComma : nextBrace;
                if (valEnd < 0) valEnd = json.Length;
                result[fullKey] = json.Substring(valStart, valEnd - valStart).Trim();
                i = valEnd + 1;
            }
        }
    }

    // ─────────────────────────────────────────────
    //  조회
    // ─────────────────────────────────────────────
    /// <summary>키에 해당하는 번역 문자열을 반환합니다. 없으면 키 자체를 반환합니다.</summary>
    public string GetText(string key)
    {
        return _table.TryGetValue(key, out string val) ? val : key;
    }

    /// <summary>string.Format 과 동일하게 {0}, {1} 을 치환합니다.</summary>
    public string GetText(string key, params object[] args)
    {
        string text = GetText(key);
        try { return string.Format(text, args); }
        catch { return text; }
    }
}