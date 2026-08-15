using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>이름 입력 판정 결과.</summary>
public enum NameJudgement
{
    /// <summary>그대로 써도 되는 이름.</summary>
    Ok,
    /// <summary>쓸 수 없는 이름. <see cref="NameVerdict.line"/> 을 보여주고 다시 받는다.</summary>
    Blocked,
    /// <summary>한마디 하고 통과시키는 이름 (이스터에그).</summary>
    AllowedWithLine,
}

public struct NameVerdict
{
    public NameJudgement judgement;
    /// <summary>표시할 대사. 없으면 null.</summary>
    public string line;
    /// <summary>앞뒤 공백을 정리한 최종 이름. Blocked 면 의미 없다.</summary>
    public string name;

    public bool CanProceed => judgement != NameJudgement.Blocked;
}

/// <summary>
/// 플레이어가 정한 주인공 이름을 들고 있는 정적 저장소.
/// PlayerGrowth · JournalManager 와 같은 정적 클래스 패턴을 따른다.
/// 대사에서는 Yarn 변수 <c>$이름</c> 과 함수 <c>이름조사()</c> 로 참조된다 (YarnCommandBridge).
/// </summary>
public static class PlayerIdentity
{
    public const string DefaultName = "루";

    /// <summary>금지 이름 목록 파일 경로 (Resources 기준, 확장자 제외).</summary>
    private const string ForbiddenNamesPath = "NameEntry/ForbiddenNames";

    /// <summary>입력 가능한 최대 글자 수.</summary>
    public const int MaxLength = 6;

    public static string Name { get; private set; } = DefaultName;

    /// <summary>이름이 바뀌었을 때. UI 갱신용.</summary>
    public static event System.Action<string> OnNameChanged;

    // ── 이름 설정 ─────────────────────────────────────────────────────────
    public static void Set(string name)
    {
        string trimmed = (name ?? "").Trim();
        if (trimmed.Length == 0) trimmed = DefaultName;

        if (Name == trimmed) return;
        Name = trimmed;
        OnNameChanged?.Invoke(Name);
    }

    /// <summary>세이브에서 복원. 비어 있으면 기본값으로 되돌린다 (구버전 세이브 대응).</summary>
    public static void Load(string saved) => Set(string.IsNullOrWhiteSpace(saved) ? DefaultName : saved);

    /// <summary>이름 + 조사. 예: WithParticle("가") → "루가" / "민준이".</summary>
    public static string WithParticle(string particle) => KoreanParticle.Attach(Name, particle);

    // ── 금지 이름 판정 ────────────────────────────────────────────────────
    /// <summary>입력값을 검사해 사용 가능 여부와 표시할 대사를 돌려준다.</summary>
    public static NameVerdict Check(string input)
    {
        string trimmed = (input ?? "").Trim();

        if (trimmed.Length == 0)
            return new NameVerdict
            {
                judgement = NameJudgement.Blocked,
                line      = "이름을 정해야 해.",
                name      = trimmed,
            };

        if (trimmed.Length > MaxLength)
            return new NameVerdict
            {
                judgement = NameJudgement.Blocked,
                line      = $"그건 너무 길어. {MaxLength}글자까지야.",
                name      = trimmed,
            };

        string key = Normalize(trimmed);
        if (key.Length == 0)
            return new NameVerdict
            {
                judgement = NameJudgement.Blocked,
                line      = "그건 이름이 아니야.",
                name      = trimmed,
            };

        if (Lookup.TryGetValue(key, out ForbiddenNameEntry entry))
            return new NameVerdict
            {
                judgement = entry.IsAllow ? NameJudgement.AllowedWithLine : NameJudgement.Blocked,
                line      = entry.line,
                name      = trimmed,
            };

        return new NameVerdict { judgement = NameJudgement.Ok, line = null, name = trimmed };
    }

    /// <summary>
    /// 표기 흔들림을 흡수한다. 공백·구두점 제거 ▸ 소문자화 ▸ 유니코드 NFC 정규화.
    /// "세 라" · "SERA" · "Sera" 가 전부 같은 키가 된다.
    /// </summary>
    public static string Normalize(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";

        var sb = new StringBuilder(raw.Length);
        foreach (char c in raw)
        {
            if (char.IsWhiteSpace(c) || char.IsPunctuation(c) || char.IsSymbol(c)) continue;
            sb.Append(char.ToLowerInvariant(c));
        }

        // 자모 조합형(ㅅ+ㅔ+ㄹ+ㅏ)으로 들어와도 완성형과 같은 키가 되도록 맞춘다
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    // ── 금지 목록 로딩 ────────────────────────────────────────────────────
    [System.Serializable]
    private class ForbiddenNameEntry
    {
        public string   id;
        public string   policy;   // "block" | "allow"
        public string[] aliases;
        public string   line;

        public bool IsAllow => policy == "allow";
    }

    [System.Serializable]
    private class ForbiddenNameTable
    {
        public ForbiddenNameEntry[] entries;
    }

    private static Dictionary<string, ForbiddenNameEntry> _lookup;

    private static Dictionary<string, ForbiddenNameEntry> Lookup
    {
        get
        {
            if (_lookup == null) BuildLookup();
            return _lookup;
        }
    }

    static void BuildLookup()
    {
        _lookup = new Dictionary<string, ForbiddenNameEntry>();

        var json = Resources.Load<TextAsset>(ForbiddenNamesPath);
        if (json == null)
        {
            Debug.LogWarning($"[PlayerIdentity] 금지 이름 목록을 찾지 못했습니다: Resources/{ForbiddenNamesPath}");
            return;
        }

        ForbiddenNameTable table;
        try
        {
            table = JsonUtility.FromJson<ForbiddenNameTable>(json.text);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PlayerIdentity] 금지 이름 목록 파싱 실패: {e.Message}");
            return;
        }

        if (table?.entries == null) return;

        foreach (var entry in table.entries)
        {
            if (entry == null) continue;

            // id 자체도 별칭으로 취급해, aliases 를 안 적어도 최소한 이름 그대로는 걸린다
            RegisterAlias(entry.id, entry);
            if (entry.aliases == null) continue;
            foreach (string alias in entry.aliases)
                RegisterAlias(alias, entry);
        }

        Dbg.Log($"[PlayerIdentity] 금지 이름 {table.entries.Length}종 / 별칭 {_lookup.Count}개 로드");
    }

    static void RegisterAlias(string alias, ForbiddenNameEntry entry)
    {
        string key = Normalize(alias);
        if (key.Length == 0) return;
        _lookup[key] = entry;   // 중복 별칭은 뒤에 온 항목이 이긴다
    }

    // ── 플레이 시작 시 정적 변수 초기화 ────────────────────────────────────
    // 에디터에서 정적 변수는 플레이 세션 사이에 유지되므로 명시적으로 리셋한다 (GameState 와 같은 이유).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ResetOnPlay()
    {
        Name          = DefaultName;
        _lookup       = null;
        OnNameChanged = null;   // 이전 세션의 구독자가 남아 있으면 파괴된 오브젝트를 부른다
    }
}
