using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Text;
using System.IO;

public class YarnDialogueBuilder : EditorWindow
{
    // ─── 탭 ───────────────────────────────────────────────
    private int _tab;
    private readonly string[] _tabNames = { "대사", "표정", "분기", "선택지", "트리거", "이동", "라디오" };

    // ─── 노드 정보 ─────────────────────────────────────────
    private string _nodeName    = "House_Kitchen";
    private YarnNodeListData _nodeListData;
    private string[]         _nodeOptions  = System.Array.Empty<string>();
    private int              _nodeSelIndex = 0;

    // ─── 대사 탭 ───────────────────────────────────────────
    private readonly string[] _chars = { "루", "세라", "쿠루", "상인", "파란새", "미루", "아모", "(나레이션)" };
    private int _selChar;
    private bool _isInner;
    private string _talkText = "";

    // ─── 표정 탭 ───────────────────────────────────────────
    private readonly string[] _spriteChars  = { "루", "세라", "쿠루", "상인", "파란새", "미루", "아모" };
    // 환상 버전 표정 (게이지 < 70)
    private readonly string[][] _emotionsF = new string[][]
    {
        new[]{ "neutral","fearful","suspicious","holding_tears","determined","flustered","blank_shock" }, // 루
        new[]{ "neutral_cold","warm_smile","low_whisper" },                                               // 세라
        new[]{ "neutral_observe","smirk_memo","stern","closed_guard","low_observe","glitch_real" },       // 쿠루
        new[]{ "neutral_flat","subtle_pause" },                                                           // 상인
        new[]{ "normal" },                                                                                // 파란새
        new[]{ "working","eyes_alive","mannequin" },                                                      // 미루
        new[]{ "perfect_smile" },                                                                         // 아모
    };

    // 현실 버전 표정 (게이지 >= 70)
    private readonly string[][] _emotionsR = new string[][]
    {
        new[]{ "neutral_real","fearful_real","suspicious_real","holding_tears_real","determined_real","flustered_real","blank_shock_real" }, // 루
        new[]{ "neutral_cold_real","warm_smile_real","low_whisper_real" },                                                                   // 세라
        new[]{ "neutral_observe_real","smirk_memo_real","stern_real","closed_guard_real","low_observe_real","glitch_real" },                 // 쿠루
        new[]{ "neutral_flat_real","subtle_pause_real" },                                                                                    // 상인
        new[]{ "normal" },                                                                                                                   // 파란새
        new[]{ "working","eyes_alive","mannequin" },  // 미루 (단검 트리거 전환, 게이지 무관)
        new[]{ "perfect_smile" },                     // 아모 (변화 없음)
    };

    // 한국어 레이블 (환상/현실 공통)
    private readonly string[][] _emotionLabels = new string[][]
    {
        new[]{ "무표정(기본)","두려움","의심","눈물참기","결심","당황","멍한충격" },
        new[]{ "차가운무표정","통제된미소","낮게중얼거림" },
        new[]{ "평온한관찰","피식웃음(메모)","굳은표정","장난기닫힘","내려다보는시선","현실왜곡글리치" },
        new[]{ "담담한기본","미세한멈춤" },
        new[]{ "기본" },
        new[]{ "카운터앞기본(환상)","눈빛흔들림(환상)","마네킹(왜곡)" },
        new[]{ "완벽한미소" },
    };
    private int _selSpriteChar;
    private int _selEmotion;
    private bool _isRight;

    // ─── 분기 탭 ───────────────────────────────────────────
    private int _selBranchChar;
    private string _branch70 = "", _branch30 = "", _branchElse = "";
    private int _selBranchVar = 0;
    private int _branchThreshold1 = 70;
    private int _branchThreshold2 = 30;
    private static readonly string[] _branchVarNames  = { "$심리게이지", "$인형화" };
    private static readonly int[]    _branchDefaults1 = { 70, 50 };
    private static readonly int[]    _branchDefaults2 = { 30, 20 };

    // ─── 선택지 탭 ─────────────────────────────────────────
    private List<string> _choiceLabels = new List<string> { "", "" };
    private List<string> _choiceJumps  = new List<string> { "", "" };
    private Vector2 _choiceScroll;

    // ─── 트리거 탭 ─────────────────────────────────────────
    private readonly string[] _triggers = {
        "무서운것_목격", "신체_고통", "쿠루_직접_대화", "아버지_유품_접촉", "루_감정_폭발",
        "환상_평화주의_성공", "세라_목소리_들림", "무서운것_회피",
        "NPC_눈_마주침", "쿠루_부재", "마시멜로_냄새", "세라_흔적_발견"
    };
    private readonly string[] _triggerDesc = {
        "+현실15", "+현실10", "+현실10", "+현실10", "+현실10",
        "+현실5", "+환상25", "+환상15",
        "+환상10", "+환상10", "+환상5", "+환상5"
    };
    private int _selTrigger;

    // ─── 이동 탭 ──────────────────────────────────────────
    private string _jumpTarget = "";

    // ─── 라디오 탭 ─────────────────────────────────────────
    private string _radioName   = "유";
    private string _radioText   = "";
    private bool   _radioStatic = true;
    private bool   _radioStyle  = true;

    // ─── 줄 목록 ───────────────────────────────────────────
    private struct Line { public string badge, display, code; }
    private readonly List<Line> _lines = new List<Line>();
    private Vector2 _listScroll;
    private Vector2 _outputScroll;

    // ─── 저장 경로 ─────────────────────────────────────────
    private string _savePath = "Assets/Dialogue";

    // ─── 스타일 캐시 ───────────────────────────────────────
    private GUIStyle _headerStyle;
    private GUIStyle _badgeStyle;
    private GUIStyle _codeStyle;
    private bool     _stylesReady;

    // ─── 지연 액션 큐 ─────────────────────────────────────
    // 컬렉션 변경은 항상 이 큐를 통해 EventType.Layout 시점에만 실행.
    // Layout → Repaint 사이에 컨트롤 수가 달라지지 않으므로
    // "Getting control X in group with only Y controls" 에러가 원천 차단됨.
    private readonly Queue<System.Action> _pending = new Queue<System.Action>();

    // ══════════════════════════════════════════════════════
    // 진입점
    // ══════════════════════════════════════════════════════

    [MenuItem("Tools/Yarn 대사 빌더")]
    public static void ShowWindow()
    {
        var w = GetWindow<YarnDialogueBuilder>("Yarn 대사 빌더");
        w.minSize = new Vector2(520, 640);
    }

    void OnEnable()
    {
        string guid = EditorPrefs.GetString("YarnBuilder_NodeListGuid", "");
        if (!string.IsNullOrEmpty(guid))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path))
                _nodeListData = AssetDatabase.LoadAssetAtPath<YarnNodeListData>(path);
        }
        RefreshNodeOptions();
    }

    void RefreshNodeOptions()
    {
        if (_nodeListData == null || _nodeListData.nodeNames == null)
        {
            _nodeOptions  = new[] { "── 노드 목록 에셋 연결 필요 ──" };
            _nodeSelIndex = 0;
            return;
        }

        var list = new List<string>(_nodeListData.nodeNames);
        list.Add("── 직접 입력 ──");
        _nodeOptions = list.ToArray();

        int idx = System.Array.IndexOf(_nodeOptions, _nodeName);
        _nodeSelIndex = idx >= 0 ? idx : _nodeOptions.Length - 1;
    }

    // ══════════════════════════════════════════════════════
    // OnGUI
    // ══════════════════════════════════════════════════════

    void OnGUI()
    {
        // Layout 이벤트 때만 큐를 비움 → 이 시점 이후 Repaint까지 컬렉션 불변
        if (Event.current.type == EventType.Layout)
            Flush();

        InitStyles();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("노드 이름", _headerStyle);

        // ScriptableObject 연결 필드
        var prev = _nodeListData;
        _nodeListData = (YarnNodeListData)EditorGUILayout.ObjectField(
            "노드 목록", _nodeListData, typeof(YarnNodeListData), false);
        if (_nodeListData != prev)
        {
            if (_nodeListData != null)
            {
                string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(_nodeListData));
                EditorPrefs.SetString("YarnBuilder_NodeListGuid", guid);
            }
            RefreshNodeOptions();
        }

        // 드롭다운 + 새로고침
        EditorGUILayout.BeginHorizontal();
        _nodeSelIndex = EditorGUILayout.Popup(_nodeSelIndex, _nodeOptions);
        if (GUILayout.Button("↺", GUILayout.Width(28)))
            RefreshNodeOptions();
        EditorGUILayout.EndHorizontal();

        bool isCustom = _nodeOptions.Length == 0
                     || _nodeSelIndex == _nodeOptions.Length - 1;
        if (isCustom)
        {
            _nodeName = EditorGUILayout.TextField(_nodeName);
        }
        else
        {
            _nodeName = _nodeOptions[_nodeSelIndex];
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField(_nodeName);
            EditorGUI.EndDisabledGroup();
        }

        EditorGUILayout.Space(8);
        Separator();

        _tab = GUILayout.Toolbar(_tab, _tabNames);
        EditorGUILayout.Space(6);

        switch (_tab)
        {
            case 0: DrawTalkTab();    break;
            case 1: DrawSpriteTab();  break;
            case 2: DrawBranchTab();  break;
            case 3: DrawChoiceTab();  break;
            case 4: DrawTriggerTab(); break;
            case 5: DrawJumpTab();    break;
            case 6: DrawRadioTab();   break;
        }

        Separator();
        DrawLineList();
        Separator();
        DrawOutput();
    }

    void Flush()
    {
        while (_pending.Count > 0)
            _pending.Dequeue().Invoke();
    }

    // ══════════════════════════════════════════════════════
    // 탭 UI
    // ══════════════════════════════════════════════════════

    void DrawTalkTab()
    {
        EditorGUILayout.LabelField("캐릭터", EditorStyles.miniLabel);
        _selChar = GUILayout.SelectionGrid(_selChar, _chars, 6);
        EditorGUILayout.Space(4);
        _isInner = EditorGUILayout.ToggleLeft("속으로 (혼잣말)", _isInner);
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("대사 내용", EditorStyles.miniLabel);
        _talkText = EditorGUILayout.TextArea(_talkText, GUILayout.Height(56));
        EditorGUILayout.Space(4);

        if (GUILayout.Button("대사 추가", GUILayout.Height(28)) &&
            !string.IsNullOrWhiteSpace(_talkText))
        {
            string ch = _chars[_selChar];
            string code;
            if (ch == "(나레이션)")
                code = _talkText.Trim();
            else if (_isInner)
                code = $"{ch}: (속으로) {_talkText.Trim()}";
            else
                code = $"{ch}: {_talkText.Trim()}";

            Enqueue(new Line { badge = "대사", display = code, code = code });
            _talkText = "";
            GUI.FocusControl(null);
        }
    }

    void DrawSpriteTab()
    {
        EditorGUILayout.LabelField("캐릭터", EditorStyles.miniLabel);
        _selSpriteChar = GUILayout.SelectionGrid(_selSpriteChar, _spriteChars, 5);
        EditorGUILayout.Space(6);

        if (_selEmotion >= _emotionLabels[_selSpriteChar].Length) _selEmotion = 0;

        EditorGUILayout.LabelField("표정", EditorStyles.miniLabel);
        _selEmotion = GUILayout.SelectionGrid(_selEmotion, _emotionLabels[_selSpriteChar], 4);
        EditorGUILayout.Space(4);
        _isRight = EditorGUILayout.ToggleLeft("오른쪽 배치", _isRight);
        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("표정 추가 (고정)", GUILayout.Height(28)))
            AddSpriteFixed();
        if (GUILayout.Button("표정 추가 (게이지 자동 분기)", GUILayout.Height(28)))
            AddSpriteWithGauge();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
        Separator();
        EditorGUILayout.LabelField("표정 숨기기", EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("숨기기 (왼쪽)",   GUILayout.Height(24)))
            Enqueue(new Line { badge = "표정", display = "표정 숨기기 (왼쪽)",   code = "<<hideSprite \"left\">>"  });
        if (GUILayout.Button("숨기기 (오른쪽)", GUILayout.Height(24)))
            Enqueue(new Line { badge = "표정", display = "표정 숨기기 (오른쪽)", code = "<<hideSprite \"right\">>" });
        if (GUILayout.Button("숨기기 (전체)",   GUILayout.Height(24)))
            Enqueue(new Line { badge = "표정", display = "표정 숨기기 (전체)",   code = "<<hideSprite \"both\">>"  });
        EditorGUILayout.EndHorizontal();
    }

    void AddSpriteFixed()
    {
        string c       = _spriteChars[_selSpriteChar];
        string eF      = _emotionsF[_selSpriteChar][_selEmotion];
        string sideStr = _isRight ? "right" : "left";
        string label   = _emotionLabels[_selSpriteChar][_selEmotion];
        string display = $"{c} 표정: {label}{(_isRight ? " (오른쪽)" : "")} [고정]";
        string code    = $"<<showSprite \"{c}\" \"{eF}\" \"{sideStr}\" \"fixed\">>";
        Enqueue(new Line { badge = "표정", display = display, code = code });
    }

    void AddSpriteWithGauge()
    {
        string c     = _spriteChars[_selSpriteChar];
        string eF    = _emotionsF[_selSpriteChar][_selEmotion];
        string side  = _isRight ? " \"right\"" : "";
        string label  = _emotionLabels[_selSpriteChar][_selEmotion];
        string display = $"{c} 표정: {label}{(_isRight ? " (오른쪽)" : "")} [게이지 자동 분기]";
        string code    = $"<<showSprite \"{c}\" \"{eF}\"{side}>>";
        Enqueue(new Line { badge = "표정", display = display, code = code });
    }

    void DrawBranchTab()
    {
        EditorGUILayout.LabelField("캐릭터 (분기 대사 화자)", EditorStyles.miniLabel);
        _selBranchChar = GUILayout.SelectionGrid(_selBranchChar, _chars, 6);
        EditorGUILayout.Space(6);

        EditorGUILayout.LabelField("분기 변수", EditorStyles.miniLabel);
        int prevVar = _selBranchVar;
        _selBranchVar = GUILayout.SelectionGrid(_selBranchVar, _branchVarNames, 2);
        if (_selBranchVar != prevVar)
        {
            _branchThreshold1 = _branchDefaults1[_selBranchVar];
            _branchThreshold2 = _branchDefaults2[_selBranchVar];
        }
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("상위 기준값", GUILayout.Width(80));
        _branchThreshold1 = EditorGUILayout.IntField(_branchThreshold1, GUILayout.Width(50));
        EditorGUILayout.LabelField("하위 기준값", GUILayout.Width(80));
        _branchThreshold2 = EditorGUILayout.IntField(_branchThreshold2, GUILayout.Width(50));
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(4);

        string vn = _branchVarNames[_selBranchVar];
        EditorGUILayout.LabelField($"{vn} >= {_branchThreshold1} 일 때 대사", EditorStyles.miniLabel);
        _branch70 = EditorGUILayout.TextArea(_branch70, GUILayout.Height(40));
        EditorGUILayout.LabelField($"{vn} >= {_branchThreshold2} 일 때 대사", EditorStyles.miniLabel);
        _branch30 = EditorGUILayout.TextArea(_branch30, GUILayout.Height(40));
        EditorGUILayout.LabelField("그 외 대사", EditorStyles.miniLabel);
        _branchElse = EditorGUILayout.TextArea(_branchElse, GUILayout.Height(40));
        EditorGUILayout.Space(4);

        if (GUILayout.Button("분기 추가", GUILayout.Height(28)))
        {
            string ch     = _chars[_selBranchChar];
            string prefix = ch == "(나레이션)" ? "" : ch + ": ";
            var sb = new StringBuilder();
            sb.AppendLine($"<<if {vn} >= {_branchThreshold1}>>");
            if (!string.IsNullOrWhiteSpace(_branch70))   sb.AppendLine("    " + prefix + _branch70.Trim());
            sb.AppendLine($"<<elseif {vn} >= {_branchThreshold2}>>");
            if (!string.IsNullOrWhiteSpace(_branch30))   sb.AppendLine("    " + prefix + _branch30.Trim());
            sb.AppendLine("<<else>>");
            if (!string.IsNullOrWhiteSpace(_branchElse)) sb.AppendLine("    " + prefix + _branchElse.Trim());
            sb.Append("<<endif>>");
            Enqueue(new Line { badge = "분기", display = $"{vn} 분기 ({_branchThreshold1}/{_branchThreshold2}/else)", code = sb.ToString() });
            _branch70 = _branch30 = _branchElse = "";
            GUI.FocusControl(null);
        }
    }

    void DrawChoiceTab()
    {
        _choiceScroll = EditorGUILayout.BeginScrollView(_choiceScroll, GUILayout.Height(140));
        try
        {
            for (int i = 0; i < _choiceLabels.Count; i++)
            {
                EditorGUILayout.LabelField($"선택지 {i + 1}", EditorStyles.miniLabel);
                _choiceLabels[i] = EditorGUILayout.TextField("텍스트",    _choiceLabels[i]);
                _choiceJumps[i]  = EditorGUILayout.TextField("이동 노드", _choiceJumps[i]);
                EditorGUILayout.Space(4);
            }
        }
        finally { EditorGUILayout.EndScrollView(); }

        // 버튼 결과를 BeginHorizontal 바깥에서 처리
        bool addOne = false, addAll = false;
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ 선택지 추가"))                          addOne = true;
        if (GUILayout.Button("선택지 전체 추가", GUILayout.Height(28))) addAll = true;
        EditorGUILayout.EndHorizontal();

        if (addOne)
        {
            _pending.Enqueue(() => { _choiceLabels.Add(""); _choiceJumps.Add(""); });
        }

        if (addAll)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < _choiceLabels.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(_choiceLabels[i])) continue;
                sb.AppendLine($"-> {_choiceLabels[i].Trim()}");
                if (!string.IsNullOrWhiteSpace(_choiceJumps[i]))
                    sb.AppendLine($"    <<jump {_choiceJumps[i].Trim()}>>");
            }
            if (sb.Length > 0)
            {
                string code  = sb.ToString().TrimEnd();
                int    count = _choiceLabels.Count;
                _pending.Enqueue(() =>
                {
                    _lines.Add(new Line { badge = "선택지", display = $"선택지 {count}개", code = code });
                    _choiceLabels = new List<string> { "", "" };
                    _choiceJumps  = new List<string> { "", "" };
                });
                GUI.FocusControl(null);
            }
        }
    }

    void DrawTriggerTab()
    {
        EditorGUILayout.LabelField("트리거 선택", EditorStyles.miniLabel);
        var display = new string[_triggers.Length];
        for (int i = 0; i < _triggers.Length; i++)
            display[i] = $"{_triggers[i]}\n{_triggerDesc[i]}";
        _selTrigger = GUILayout.SelectionGrid(_selTrigger, display, 3, GUILayout.Height(160));
        EditorGUILayout.Space(4);

        if (GUILayout.Button("트리거 추가", GUILayout.Height(28)))
        {
            string t    = _triggers[_selTrigger];
            string desc = _triggerDesc[_selTrigger];
            Enqueue(new Line { badge = "트리거", display = $"트리거: {t} ({desc})", code = $"<<applyTrigger \"{t}\">>" });
        }
    }

    void DrawJumpTab()
    {
        EditorGUILayout.LabelField("이동할 노드 이름", EditorStyles.miniLabel);
        _jumpTarget = EditorGUILayout.TextField(_jumpTarget);
        EditorGUILayout.Space(4);

        if (GUILayout.Button("노드 이동 추가", GUILayout.Height(28)) &&
            !string.IsNullOrWhiteSpace(_jumpTarget))
        {
            string t = _jumpTarget.Trim();
            Enqueue(new Line { badge = "이동", display = $"이동: {t}", code = $"<<jump {t}>>" });
            _jumpTarget = "";
            GUI.FocusControl(null);
        }
    }

    void DrawRadioTab()
    {
        EditorGUILayout.LabelField("화자 이름  →  이름(라디오): 형태로 자동 생성됩니다", EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal();
        _radioName = EditorGUILayout.TextField(_radioName);
        EditorGUILayout.LabelField("(라디오):", EditorStyles.miniLabel, GUILayout.Width(72));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox($"미리보기:  {_radioName}(라디오): ...", MessageType.None);
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("대사 내용", EditorStyles.miniLabel);
        _radioText = EditorGUILayout.TextArea(_radioText, GUILayout.Height(56));
        EditorGUILayout.Space(4);
        _radioStatic = EditorGUILayout.ToggleLeft("끊기는 효과  (단어 사이에 ... 자동 삽입)", _radioStatic);
        _radioStyle  = EditorGUILayout.ToggleLeft("라디오 스타일 태그  (<i><color=#d4c97a> 노란 기울임 </color></i>)", _radioStyle);
        EditorGUILayout.Space(4);

        if (GUILayout.Button("라디오 대사 추가", GUILayout.Height(28)) &&
            !string.IsNullOrWhiteSpace(_radioText))
        {
            string speaker = $"{_radioName.Trim()}(라디오)";
            string body    = _radioText.Trim();

            if (_radioStatic)
            {
                string[] words = body.Split(' ');
                int mid = words.Length / 2;
                body = words.Length >= 3
                    ? string.Join(" ", words, 0, mid) + "... " + string.Join(" ", words, mid, words.Length - mid)
                    : body + "...";
            }
            if (_radioStyle)
                body = $"<i><color=#d4c97a>{body}</color></i>";

            string origText = _radioText.Trim();
            bool   st = _radioStatic, ss = _radioStyle;
            string code    = $"{speaker}: {body}";
            string display = $"{speaker}: {origText}{(st ? " (끊김)" : "")}{(ss ? " (스타일)" : "")}";
            Enqueue(new Line { badge = "라디오", display = display, code = code });
            _radioText = "";
            GUI.FocusControl(null);
        }
    }

    // ══════════════════════════════════════════════════════
    // 줄 목록
    // ══════════════════════════════════════════════════════

    void DrawLineList()
    {
        bool requestClear = false;
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("추가된 줄 목록", _headerStyle);
        GUILayout.FlexibleSpace();
        if (_lines.Count > 0 && GUILayout.Button("전체 삭제", GUILayout.Width(70)))
            requestClear = true;
        EditorGUILayout.EndHorizontal();

        // DisplayDialog는 BeginHorizontal 바깥에서 호출
        if (requestClear &&
            EditorUtility.DisplayDialog("확인", "전체 줄을 삭제할까요?", "삭제", "취소"))
        {
            _pending.Enqueue(() => _lines.Clear());
            Repaint();
        }

        if (_lines.Count == 0)
        {
            EditorGUILayout.HelpBox("아직 추가된 내용이 없어요.", MessageType.None);
            return;
        }

        _listScroll = EditorGUILayout.BeginScrollView(
            _listScroll, GUILayout.Height(Mathf.Min(_lines.Count * 28f + 8, 140)));

        int deleteIndex = -1;
        int swapA = -1, swapB = -1;
        try
        {
            for (int i = 0; i < _lines.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                try
                {
                    GUILayout.Label($"[{_lines[i].badge}]", _badgeStyle, GUILayout.Width(52));
                    GUILayout.Label(_lines[i].display, EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("↑", GUILayout.Width(22)) && i > 0)
                        (swapA, swapB) = (i, i - 1);
                    if (GUILayout.Button("↓", GUILayout.Width(22)) && i < _lines.Count - 1)
                        (swapA, swapB) = (i, i + 1);
                    if (GUILayout.Button("✕", GUILayout.Width(22)))
                        deleteIndex = i;
                }
                finally { EditorGUILayout.EndHorizontal(); }
            }
        }
        finally { EditorGUILayout.EndScrollView(); }

        // 모든 Begin/End가 닫힌 뒤, 큐에 넣어 다음 Layout 시점에 반영
        if (swapA >= 0)
        {
            int a = swapA, b = swapB;
            _pending.Enqueue(() =>
            {
                if (a < _lines.Count && b < _lines.Count)
                { var tmp = _lines[a]; _lines[a] = _lines[b]; _lines[b] = tmp; }
            });
        }
        if (deleteIndex >= 0)
        {
            int idx = deleteIndex;
            _pending.Enqueue(() => { if (idx < _lines.Count) _lines.RemoveAt(idx); });
        }
    }

    // ══════════════════════════════════════════════════════
    // 코드 출력 + 저장
    // ══════════════════════════════════════════════════════

    string BuildYarn()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"title: {_nodeName}");
        sb.AppendLine("tags: ");
        sb.AppendLine("---");
        foreach (var l in _lines) sb.AppendLine(l.code);
        sb.Append("===");
        return sb.ToString();
    }

    void DrawOutput()
    {
        EditorGUILayout.LabelField("생성된 Yarn 코드", _headerStyle);
        string yarn = BuildYarn();

        _outputScroll = EditorGUILayout.BeginScrollView(_outputScroll, GUILayout.Height(140));
        try
        {
            EditorGUILayout.TextArea(yarn, _codeStyle, GUILayout.ExpandHeight(true));
        }
        finally { EditorGUILayout.EndScrollView(); }

        EditorGUILayout.Space(4);

        // "..." 버튼 결과를 BeginHorizontal 바깥에서 처리
        bool pickFolder = false;
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("저장 폴더", GUILayout.Width(60));
        _savePath = EditorGUILayout.TextField(_savePath);
        if (GUILayout.Button("...", GUILayout.Width(28))) pickFolder = true;
        EditorGUILayout.EndHorizontal();

        if (pickFolder)
        {
            string picked = EditorUtility.OpenFolderPanel("저장 폴더 선택", _savePath, "");
            if (!string.IsNullOrEmpty(picked))
                _savePath = picked.StartsWith(Application.dataPath)
                    ? "Assets" + picked.Substring(Application.dataPath.Length)
                    : picked;
        }

        EditorGUILayout.Space(4);
        if (GUILayout.Button("📄  .yarn 파일로 저장", GUILayout.Height(32)))
            SaveYarnFile(yarn);
    }

    void SaveYarnFile(string content)
    {
        string fullDir = Path.Combine(Application.dataPath,
            _savePath.StartsWith("Assets/") ? _savePath.Substring(7) : _savePath);
        Directory.CreateDirectory(fullDir);
        File.WriteAllText(Path.Combine(fullDir, _nodeName + ".yarn"), content, Encoding.UTF8);
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("저장 완료", $"{_nodeName}.yarn 이 저장됐어요!\n경로: {_savePath}", "확인");
    }

    // ══════════════════════════════════════════════════════
    // 헬퍼
    // ══════════════════════════════════════════════════════

    void Enqueue(Line line) => _pending.Enqueue(() => _lines.Add(line));

    void InitStyles()
    {
        if (_stylesReady) return;

        _headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13,
            margin   = new RectOffset(0, 0, 8, 4)
        };

        _badgeStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize  = 11,
            alignment = TextAnchor.MiddleCenter,
            normal    = { background = MakeTex(1, 1, new Color(0.55f, 0.47f, 0.86f, 0.25f)) }
        };

        _codeStyle = new GUIStyle(EditorStyles.textArea)
        {
            fontSize = 12,
            wordWrap = true
        };

        _stylesReady = true;
    }

    Texture2D MakeTex(int w, int h, Color col)
    {
        var t  = new Texture2D(w, h);
        var px = new Color[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = col;
        t.SetPixels(px);
        t.Apply();
        return t;
    }

    void Separator()
    {
        EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), new Color(0.5f, 0.5f, 0.5f, 0.2f));
        EditorGUILayout.Space(4);
    }
}
