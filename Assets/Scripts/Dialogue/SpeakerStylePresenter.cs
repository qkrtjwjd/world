using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using Yarn.Unity;

/// <summary>
/// 화자 ID 기준으로 이름창 표시명과 대사 서식을 런타임에 입힌다 (F-4-4 · CLAUDE.md §5).
///
/// <para><b>왜 이 형태인가.</b> 변환기는 대사 본문만 출력하고 서식 태그를 넣지 않는다 —
/// 넣으면 다음 변환 때 사라지고 <c>#</c> 이스케이프 문제가 따라온다. 그래서 서식은
/// Dialogue Runner 쪽에서 붙여야 하는데, Yarn 의 <see cref="LinePresenter"/> 는
/// <c>sealed</c> 라 상속할 수 없다.</para>
///
/// <para><b>동작 근거.</b> <see cref="DialogueRunner"/> 는 presenter 들의
/// <c>RunLineAsync</c> 를 순서대로 시작한 뒤 <c>WhenAll</c> 로 기다린다. 각 presenter 는
/// 첫 <c>await</c> 까지 동기로 실행되고, <c>LinePresenter</c> 는 이름창 대입을
/// <b>첫 await 이전</b>에 끝낸다. 따라서 이 presenter 가 목록에서 <b>LinePresenter 뒤</b>에
/// 있으면 그 값을 덮어쓸 수 있다. 순서는 <see cref="OnDialogueStartedAsync"/> 에서 검사한다.</para>
///
/// <para><b>서식을 문자열이 아니라 TMP 속성으로 거는 이유.</b> 본문 글자는 타이프라이터가
/// await 이후에 쓰므로 <c>lineText.text</c> 를 건드려도 덮어써진다. 반면
/// <c>fontStyle</c> · <c>color</c> 는 문자열과 무관한 컴포넌트 속성이라 그대로 남는다.</para>
///
/// <para>매핑 데이터는 <c>Assets/Resources/Dialogue/SpeakerStyle.json</c> 이며
/// <c>Scenario/node_map.json</c> 에서 생성된다. 손으로 고치지 말 것 —
/// 메뉴 <c>무채색낙원/화자 스타일 갱신</c> 을 쓴다.</para>
/// </summary>
[RequireComponent(typeof(LinePresenter))]
public class SpeakerStylePresenter : DialoguePresenterBase
{
    const string StylePath = "Dialogue/SpeakerStyle";

    [Tooltip("이름창·본문을 소유한 LinePresenter. 비우면 같은 오브젝트에서 찾는다.")]
    [SerializeField] LinePresenter linePresenter;

    [Tooltip("끄면 매핑을 적용하지 않는다. 원인 격리용.")]
    [SerializeField] bool applyStyles = true;

    // 프리팹 기본값. 매핑이 없는 화자에서 여기로 되돌린다.
    FontStyles _baseFontStyle;
    Color      _baseColor;
    bool       _baseCached;

    static Dictionary<string, Entry> _styles;

    // ── 데이터 ────────────────────────────────────────────────────────
    [System.Serializable]
    class Entry
    {
        public string id;
        public string display;
        public bool   italic;
        public string color;      // "#d4c97a" 또는 빈 문자열
    }

    [System.Serializable]
    class Table { public Entry[] entries; }

    static Dictionary<string, Entry> Styles
    {
        get
        {
            if (_styles == null) Build();
            return _styles;
        }
    }

    static void Build()
    {
        _styles = new Dictionary<string, Entry>();

        var json = Resources.Load<TextAsset>(StylePath);
        if (json == null)
        {
            Debug.LogWarning($"[SpeakerStylePresenter] 매핑 파일이 없습니다: Resources/{StylePath}. " +
                             "메뉴 [무채색낙원/화자 스타일 갱신] 을 실행하세요.");
            return;
        }

        Table table;
        try { table = JsonUtility.FromJson<Table>(json.text); }
        catch (System.Exception e)
        {
            Debug.LogError($"[SpeakerStylePresenter] 매핑 파일을 읽지 못했습니다: {e.Message}");
            return;
        }

        if (table?.entries == null) return;
        foreach (var e in table.entries)
            if (!string.IsNullOrEmpty(e.id)) _styles[e.id] = e;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => _styles = null;

    // ── Presenter ─────────────────────────────────────────────────────
    void Awake()
    {
        if (linePresenter == null) linePresenter = GetComponent<LinePresenter>();
    }

    public override YarnTask OnDialogueStartedAsync()
    {
        CacheBaseline();
        WarnIfOrderedBeforeLinePresenter();
        return YarnTask.CompletedTask;
    }

    public override YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
    {
        // 동기 프롤로그에서만 처리한다. await 를 두면 LinePresenter 의 대입 순서를 놓친다.
        if (applyStyles && linePresenter != null) Apply(line);
        return YarnTask.CompletedTask;
    }

    public override YarnTask OnDialogueCompleteAsync()
    {
        RestoreBaseline();
        return YarnTask.CompletedTask;
    }

    // ── 적용 ──────────────────────────────────────────────────────────
    void Apply(LocalizedLine line)
    {
        CacheBaseline();

        string id = line?.CharacterName;
        Entry style = null;
        if (!string.IsNullOrEmpty(id)) Styles.TryGetValue(id, out style);

        var body = linePresenter.lineText;
        var nameText = linePresenter.characterNameText;
        var nameBox = linePresenter.characterNameContainer != null
            ? linePresenter.characterNameContainer
            : (nameText != null ? nameText.gameObject : null);

        if (style == null)
        {
            // 매핑에 없는 화자(루 등). 이름은 LinePresenter 가 쓴 그대로 두고 서식만 되돌린다.
            RestoreBaseline();
            return;
        }

        // 표시명 — 빈 문자열이면 이름창 자체를 띄우지 않는다.
        // 루독백이 여기 해당한다. 루의 이름은 {$이름} 변수라 고정 문자열이 뜨면
        // 이름 커스터마이즈와 충돌한다 (CLAUDE.md §5).
        if (string.IsNullOrEmpty(style.display))
        {
            if (nameBox != null) nameBox.SetActive(false);
        }
        else if (nameText != null)
        {
            if (nameBox != null) nameBox.SetActive(true);
            nameText.text = style.display;
        }

        if (body == null) return;

        body.fontStyle = style.italic
            ? _baseFontStyle | FontStyles.Italic
            : _baseFontStyle;

        body.color = (!string.IsNullOrEmpty(style.color) &&
                      ColorUtility.TryParseHtmlString(style.color, out var c))
            ? c
            : _baseColor;
    }

    void CacheBaseline()
    {
        if (_baseCached || linePresenter == null || linePresenter.lineText == null) return;
        _baseFontStyle = linePresenter.lineText.fontStyle;
        _baseColor     = linePresenter.lineText.color;
        _baseCached    = true;
    }

    void RestoreBaseline()
    {
        if (!_baseCached || linePresenter == null || linePresenter.lineText == null) return;
        linePresenter.lineText.fontStyle = _baseFontStyle;
        linePresenter.lineText.color     = _baseColor;
    }

    /// <summary>
    /// 목록 순서가 뒤집혀 있으면 이름창이 원래 ID 로 되돌아간다. 조용히 실패하지 않게 짚는다.
    /// </summary>
    void WarnIfOrderedBeforeLinePresenter()
    {
        var runner = DialogueRunner.FindRunner(this);
        if (runner == null) return;

        var list = runner.DialoguePresenters.ToList();
        int mine = list.IndexOf(this);
        int theirs = list.IndexOf(linePresenter);

        if (mine < 0)
        {
            Debug.LogWarning("[SpeakerStylePresenter] DialogueRunner 의 Dialogue Presenters 목록에 " +
                             "등록돼 있지 않습니다. 이름창 표시명과 서식이 적용되지 않습니다.", this);
        }
        else if (theirs >= 0 && mine < theirs)
        {
            Debug.LogWarning("[SpeakerStylePresenter] 목록에서 LinePresenter 보다 앞에 있습니다 " +
                             $"({mine} < {theirs}). LinePresenter 가 이름창을 나중에 덮어쓰므로 " +
                             "표시명이 화자 ID 그대로 보입니다. 순서를 뒤로 옮기세요.", this);
        }
    }
}
