using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Text;
using System.IO;

public class YarnDialogueBuilder : EditorWindow
{
    // ─── 탭 ───────────────────────────────────────────────
    private int _tab;
    private readonly string[] _tabNames = { "대사", "표정", "분기", "선택지", "트리거", "이동", "라디오", "카메라", "제목" };

    // ─── 노드 정보 ─────────────────────────────────────────
    private string _nodeName  = "House_Kitchen";
    private string _titleInput = "";
    private YarnNodeListData _nodeListData;
    private string[]         _nodeOptions  = System.Array.Empty<string>();
    private int              _nodeSelIndex = 0;
    private bool             _renameMode   = false;
    private string           _renameBuffer = "";

    // ─── 대사 탭 ───────────────────────────────────────────
    private readonly string[] _chars = { "루", "세라", "쿠루", "솔", "파란새", "미루", "아모", "(나레이션)" };
    private int _selChar;
    private bool _isInner;
    private string _talkText = "";

    // ─── 표정 탭 ───────────────────────────────────────────
    private readonly string[] _spriteChars  = { "루", "세라", "쿠루", "솔", "파란새", "미루", "아모" };
    // 환상 버전 표정 (게이지 < 70)
    private readonly string[][] _emotionsF = new string[][]
    {
        new[]{ "neutral","fearful","suspicious","holding_tears","determined","flustered","blank_shock","eyes_closed","dreamy","angry","alive_eyes","discomfort_hidden","puzzled" }, // 루
        new[]{ "neutral_cold","warm_smile","low_whisper" },                                               // 세라
        new[]{ "neutral_observe","smirk_memo","stern","closed_guard","low_observe","glitch_real" },       // 쿠루
        new[]{ "neutral_flat","subtle_pause" },                                                           // 솔
        new[]{ "normal" },                                                                                // 파란새
        new[]{ "working","eyes_alive","mannequin" },                                                      // 미루
        new[]{ "perfect_smile" },                                                                         // 아모
    };

    // 현실 버전 표정 (게이지 >= 70)
    private readonly string[][] _emotionsR = new string[][]
    {
        new[]{ "neutral_real","fearful_real","suspicious_real","holding_tears_real","determined_real","flustered_real","blank_shock_real","eyes_closed_real","dreamy_real","angry_real","alive_eyes_real","discomfort_hidden_real","puzzled_real" }, // 루
        new[]{ "neutral_cold_real","warm_smile_real","low_whisper_real" },                                                                   // 세라
        new[]{ "neutral_observe_real","smirk_memo_real","stern_real","closed_guard_real","low_observe_real","glitch_real" },                 // 쿠루
        new[]{ "neutral_flat_real","subtle_pause_real" },                                                                                    // 솔
        new[]{ "normal" },                                                                                                                   // 파란새
        new[]{ "working","eyes_alive","mannequin" },  // 미루 (단검 트리거 전환, 게이지 무관)
        new[]{ "perfect_smile" },                     // 아모 (변화 없음)
    };

    // 한국어 레이블 (환상/현실 공통)
    private readonly string[][] _emotionLabels = new string[][]
    {
        new[]{ "무표정(기본)","두려움","의심","눈물참기","결심","당황","멍한충격","눈감음","몽롱함","분노","눈빛만살아있음","불편함숨김","모르겠음" },
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

    // 게이지 구간별 표정 — 각 구간 독립 선택
    private int _selEmotionHigh;
    private int _selEmotionMid;
    private int _selEmotionLow;


    // ─── 분기 탭 ───────────────────────────────────────────
    private int    _selBranchChar = 0;
    private int    _selBranchVar  = 0;
    private static readonly string[] _branchVarNames = { "$심리게이지", "$인형화" };

    // 변수별 기본 임계값 (높은 값 → 낮은 값)
    private static readonly int[][] _varDefaultThresholds =
    {
        new[]{ 70, 30 },           // $심리게이지: 2개
        new[]{ 100, 81, 61, 31 }, // $인형화: 4개
    };

    // 변수별 구간 레이블 (임계값+1 = 구간 수)
    private static readonly string[][] _varStageLabels =
    {
        new[]{ "≥70  현실 대사", "≥30  중간 대사", "<30  깊은 환상" },
        new[]{ "≥100  소멸(Doll)", "≥81  통제불능(81+)", "≥61  역효과(61+)", "≥31  균열의 시작(31+)", "<31  자율(데모 범위)" },
    };

    // 작업 중인 분기 값
    private int[]    _branchThresholds   = new int[]    { 70, 30 };
    private string[] _branchTexts        = new string[] { "", "", "" };
    private int      _branchUsedSections = -1; // -1 = 최대 구간(자동)

    // AND 조합 조건 (구간별)
    private static readonly string[] _andOps = { ">=", "<", "==" };
    private bool[] _branchAndEnabled = System.Array.Empty<bool>();
    private int[]  _branchAndVar     = System.Array.Empty<int>();
    private int[]  _branchAndOp      = System.Array.Empty<int>();
    private int[]  _branchAndVal     = System.Array.Empty<int>();

    // 전체 AND 조건 (모든 구간에 일괄 적용)
    private bool _globalAndEnabled = false;
    private int  _globalAndVar     = 1;
    private int  _globalAndOp      = 1;
    private int  _globalAndVal     = 31;

    // ─── 복합 분기 모드 (두 변수 동시 입력) ───────────────
    private bool   _combinedMode     = false;
    // 변수 0 (심리게이지) 설정
    private int    _cb0Sections      = 3;
    private int[]  _cb0Thresholds    = new int[] { 70, 30 };
    // 변수 1 (인형화) 설정
    private int    _cb1Sections      = 2;
    private int[]  _cb1Thresholds    = new int[] { 31 };
    // 조합별 텍스트 (최대 5×5 = 25칸)
    private string[] _cbTexts        = new string[25];
    private Vector2  _cbScroll;

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
    private string _jumpTarget    = "";
    private int    _jumpNodeIndex = 0;

    // ─── 선택지 노드 인덱스 ────────────────────────────────
    private List<int> _choiceJumpIndices = new List<int> { 0, 0 };

    // ─── 선택지 조건 ──────────────────────────────────────
    private List<bool>   _choiceCondEnabled = new List<bool>   { false, false };
    private List<string> _choiceConditions  = new List<string> { "", "" };

    // ─── 포커스 플래그 ────────────────────────────────────────
    private bool _focusTalkArea   = false;
    private bool _focusRadioArea  = false;
    private bool _focusTitleInput = false;

    // ─── 라디오 탭 ─────────────────────────────────────────
    private string _radioName          = "유";
    private string _radioText          = "";
    private bool   _radioStatic        = true;
    private bool   _radioStyle         = true;
    private bool   _radioSpriteEnabled = false;
    private int    _radioSpriteChar    = 0;
    private int    _radioSpriteEmotion = 0;
    private bool   _radioSpriteRight   = false;

    // ─── 카메라 탭 ─────────────────────────────────────────
    private string _camTarget        = "";
    private string _camToTarget      = "";
    private float  _camZoomAmount    = 2f;
    private float  _camZoomDuration  = 1f;   // zoom_in / zoom_out 공유
    private float  _camSlowmoDuration = 2f;  // cam_slowmo 전용
    private float  _camShakeDuration  = 0.5f;// cam_shake 전용
    private float  _camFadeDuration   = 1f;  // cam_fade_down 전용
    private float  _camSpeed         = 3f;
    private float  _camHeight        = 2f;
    private float  _camAngle         = 15f;
    private float  _camReturnTime    = 1f;
    private float  _camTimeScale     = 0.3f;
    private float  _camIntensity     = 1f;

    // ─── 줄 목록 ───────────────────────────────────────────
    private struct Line { public string badge, display, code; }
    private readonly List<Line> _lines = new List<Line>();
    private Vector2 _listScroll;
    private bool    _scrollListToBottom = false;
    private Vector2 _outputScroll;
    private Vector2 _mainScroll;
    private int     _selectedLineIndex  = -1;

    // ─── 인라인 편집 ───────────────────────────────────────
    private int    _editingLineIndex   = -1;
    private string _editingLineContent = "";

    // ─── 출력 자동 스크롤 ──────────────────────────────────
    private bool _scrollOutputToBottom = false;

    // ─── 새 노드 생성 ──────────────────────────────────────
    private bool   _newNodeMode   = false;
    private string _newNodeBuffer = "";

    // ─── 탭 스크롤 ─────────────────────────────────────────
    private Vector2 _tabScroll;

    // ─── 수정 모드 ─────────────────────────────────────────
    private bool     _editMode        = false;
    private int      _editYarnIndex   = 0;
    private string[] _editNodeNames   = System.Array.Empty<string>();
    private int      _editNodeIndex   = 0;
    private string   _editNodeContent = "";
    private Vector2  _editScroll;
    private bool     _editDirty       = false;
    private string   _editSourceFile  = "";

    // ─── 저장 모드 ─────────────────────────────────────────
    private bool     _appendMode      = false;
    private string[] _existingYarns   = System.Array.Empty<string>();
    private int      _targetYarnIndex = 0;

    // ─── 저장 경로 ─────────────────────────────────────────
    private string _savePath = "Assets/Dialogue";

    // ─── import 출처 (수정 모드에서 불러온 경우) ────────────
    private string _importedFromFile = "";
    private string _importedNodeName = "";

    // ─── 스타일 캐시 ───────────────────────────────────────
    private GUIStyle  _headerStyle;
    private GUIStyle  _badgeStyle;
    private GUIStyle  _codeStyle;
    private bool      _stylesReady;
    private Texture2D _badgeTex;

    // ─── Yarn 코드 캐시 ────────────────────────────────────
    private string _yarnCache = "";
    private bool   _yarnDirty = true;

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
        _stylesReady = false;
        _yarnDirty   = true;
        _savePath    = EditorPrefs.GetString("YarnBuilder_SavePath", "Assets/Dialogue");
        _tab         = EditorPrefs.GetInt("YarnBuilder_Tab", 0);
        _appendMode  = EditorPrefs.GetBool("YarnBuilder_AppendMode", false);

        string guid = EditorPrefs.GetString("YarnBuilder_NodeListGuid", "");
        if (!string.IsNullOrEmpty(guid))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path))
                _nodeListData = AssetDatabase.LoadAssetAtPath<YarnNodeListData>(path);
        }
        RefreshNodeOptions();
        ResetBranchForVar(_selBranchVar);
    }

    void OnDisable()
    {
        if (_badgeTex != null) { DestroyImmediate(_badgeTex); _badgeTex = null; }
        _stylesReady = false;
        EditorPrefs.SetString("YarnBuilder_SavePath", _savePath);
        EditorPrefs.SetInt("YarnBuilder_Tab", _tab);
        EditorPrefs.SetBool("YarnBuilder_AppendMode", _appendMode);
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

        _mainScroll = EditorGUILayout.BeginScrollView(_mainScroll);

        // ── 모드 토글 ──────────────────────────────────────
        EditorGUILayout.Space(6);
        EditorGUILayout.BeginHorizontal();
        bool wantBuild = GUILayout.Toggle(!_editMode, "🔨 빌드 모드",  "Button", GUILayout.Height(26));
        bool wantEdit  = GUILayout.Toggle(_editMode,  "✏️ 수정 모드", "Button", GUILayout.Height(26));
        EditorGUILayout.EndHorizontal();
        if      (wantBuild && _editMode)  _editMode = false;
        else if (wantEdit  && !_editMode) { _editMode = true; RefreshExistingYarns(); RefreshEditNodes(); }
        EditorGUILayout.Space(4);
        Separator();

        if (_editMode)
        {
            DrawEditMode();
            EditorGUILayout.EndScrollView();
            return;
        }

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
        int prevSel = _nodeSelIndex;
        _nodeSelIndex = EditorGUILayout.Popup(_nodeSelIndex, _nodeOptions);
        bool nodeSelChanged = _nodeSelIndex != prevSel;
        if (nodeSelChanged) _renameMode = false;
        if (GUILayout.Button("↺", GUILayout.Width(28)))
        {
            RefreshNodeOptions();
            _renameMode = false;
        }
        EditorGUILayout.EndHorizontal();

        if (nodeSelChanged && _lines.Count > 0)
        {
            if (EditorUtility.DisplayDialog("줄 초기화", "노드가 변경됐어요. 줄 목록을 초기화할까요?", "초기화", "유지"))
                _pending.Enqueue(() => { _lines.Clear(); _selectedLineIndex = -1; _editingLineIndex = -1; });
        }

        bool isCustom = _nodeOptions.Length == 0
                     || _nodeSelIndex == _nodeOptions.Length - 1;
        if (isCustom)
        {
            _renameMode = false;
            _nodeName = EditorGUILayout.TextField(_nodeName);
        }
        else
        {
            _nodeName = _nodeOptions[_nodeSelIndex];

            if (_renameMode)
            {
                // ── 이름 변경 모드 ──────────────────────────────
                EditorGUILayout.BeginHorizontal();
                GUI.SetNextControlName("RenameField");
                _renameBuffer = EditorGUILayout.TextField(_renameBuffer);
                if (GUILayout.Button("✔ 적용", GUILayout.Width(58)))
                {
                    if (!string.IsNullOrWhiteSpace(_renameBuffer))
                    {
                        string newName = _renameBuffer.Trim();
                        if (_nodeListData != null)
                        {
                            int ri = _nodeListData.nodeNames.IndexOf(_nodeName);
                            if (ri >= 0) _nodeListData.nodeNames[ri] = newName;
                            EditorUtility.SetDirty(_nodeListData);
                            AssetDatabase.SaveAssets();
                        }
                        _nodeName = newName;
                        RefreshNodeOptions();
                    }
                    _renameMode = false;
                    GUI.FocusControl(null);
                }
                if (GUILayout.Button("✕", GUILayout.Width(24)))
                {
                    _renameMode = false;
                    GUI.FocusControl(null);
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.HelpBox($"저장될 노드 이름: {(string.IsNullOrWhiteSpace(_renameBuffer) ? _nodeName : _renameBuffer.Trim())}", MessageType.Info);
            }
            else
            {
                // ── 일반 표시 모드 ─────────────────────────────
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField(_nodeName);
                EditorGUI.EndDisabledGroup();
                if (GUILayout.Button("✏️ 이름 변경", GUILayout.Width(80)))
                {
                    _renameBuffer = _nodeName;
                    _renameMode   = true;
                    EditorGUI.FocusTextInControl("RenameField");
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        EditorGUILayout.Space(6);

        // ── 새 노드 생성 ───────────────────────────────────
        if (_newNodeMode)
        {
            if (_nodeListData == null)
            {
                EditorGUILayout.HelpBox("노드 목록 에셋을 먼저 연결해주세요.", MessageType.Warning);
                if (GUILayout.Button("✕ 취소", GUILayout.Width(60)))
                {
                    _newNodeMode   = false;
                    _newNodeBuffer = "";
                }
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                GUI.SetNextControlName("NewNodeField");
                _newNodeBuffer = EditorGUILayout.TextField("새 노드 이름", _newNodeBuffer);
                bool duplicate = _nodeListData.nodeNames != null &&
                                 _nodeListData.nodeNames.Contains(_newNodeBuffer.Trim());
                EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(_newNodeBuffer) || duplicate);
                bool newNodeCreated = GUILayout.Button("✔ 생성", GUILayout.Width(58));
                EditorGUI.EndDisabledGroup();
                if (GUILayout.Button("✕", GUILayout.Width(24)))
                {
                    _newNodeMode   = false;
                    _newNodeBuffer = "";
                    GUI.FocusControl(null);
                }
                EditorGUILayout.EndHorizontal();
                if (duplicate)
                    EditorGUILayout.HelpBox("이미 존재하는 노드 이름이에요.", MessageType.Error);

                if (newNodeCreated && !string.IsNullOrWhiteSpace(_newNodeBuffer) && !duplicate)
                {
                    string n = _newNodeBuffer.Trim();
                    _nodeListData.nodeNames.Add(n);
                    EditorUtility.SetDirty(_nodeListData);
                    AssetDatabase.SaveAssets();
                    RefreshNodeOptions();
                    _nodeSelIndex  = System.Array.IndexOf(_nodeOptions, n);
                    _nodeName      = n;
                    _newNodeMode   = false;
                    _newNodeBuffer = "";
                    GUI.FocusControl(null);
                    if (_lines.Count > 0 &&
                        EditorUtility.DisplayDialog("줄 초기화", "새 노드로 전환합니다.\n줄 목록을 초기화할까요?", "초기화", "유지"))
                        _pending.Enqueue(() => { _lines.Clear(); _selectedLineIndex = -1; _editingLineIndex = -1; });
                }
            }
        }
        else
        {
            if (GUILayout.Button("＋ 새 노드", GUILayout.Height(22)))
            {
                _newNodeMode   = true;
                _newNodeBuffer = "";
                EditorGUI.FocusTextInControl("NewNodeField");
            }
        }

        EditorGUILayout.Space(8);
        Separator();

        _tab = GUILayout.Toolbar(_tab, _tabNames);
        EditorGUILayout.Space(6);

        float tabH = (_tab == 2 && _combinedMode) ? 420f : 260f;
        _tabScroll = EditorGUILayout.BeginScrollView(_tabScroll, GUILayout.Height(tabH));
        switch (_tab)
        {
            case 0: DrawTalkTab();   break;
            case 1: DrawSpriteTab(); break;
            case 2: DrawBranchTab(); break;
            case 3: DrawChoiceTab(); break;
            case 4: DrawTriggerTab(); break;
            case 5: DrawJumpTab();   break;
            case 6: DrawRadioTab();  break;
            case 7: DrawCameraTab(); break;
            case 8: DrawTitleTab();  break;
        }
        EditorGUILayout.EndScrollView();

        Separator();
        DrawLineList();
        Separator();
        DrawOutput();

        EditorGUILayout.EndScrollView();
    }

    void Flush()
    {
        if (_pending.Count == 0) return;
        while (_pending.Count > 0)
            _pending.Dequeue().Invoke();
        _yarnDirty = true;
    }

    // ══════════════════════════════════════════════════════
    // 탭 UI
    // ══════════════════════════════════════════════════════

    void DrawTalkTab()
    {
        EditorGUILayout.LabelField("캐릭터", EditorStyles.miniLabel);
        _selChar = GUILayout.SelectionGrid(_selChar, _chars, 6);
        EditorGUILayout.Space(4);
        bool isNarration = _selChar == _chars.Length - 1;
        if (!isNarration)
            _isInner = EditorGUILayout.ToggleLeft("기울임체", _isInner);
        else
            _isInner = false;
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("대사 내용", EditorStyles.miniLabel);
        if (_focusTalkArea) { EditorGUI.FocusTextInControl("TalkTextArea"); _focusTalkArea = false; }
        GUI.SetNextControlName("TalkTextArea");
        _talkText = EditorGUILayout.TextArea(_talkText, GUILayout.Height(56));
        EditorGUILayout.Space(4);

        bool addTalk = GUILayout.Button("대사 추가  (Ctrl+Enter)", GUILayout.Height(28));
        // Ctrl+Enter 단축키
        if (Event.current.type == EventType.KeyDown &&
            Event.current.keyCode == KeyCode.Return && Event.current.control)
        { addTalk = true; Event.current.Use(); }

        if (addTalk && !string.IsNullOrWhiteSpace(_talkText))
        {
            string ch = _chars[_selChar];
            string text = _talkText.Trim();
            string code, display;
            if (ch == "(나레이션)")
            { code = text; display = text; }
            else if (_isInner)
            { code = $"{ch}: <i>{text}</i>"; display = $"{ch}: {text} [기울임]"; }
            else
            { code = $"{ch}: {text}"; display = code; }

            Enqueue(new Line { badge = "대사", display = display, code = code });
            _talkText = "";
            _focusTalkArea = true;
            Repaint();
        }
    }

    void DrawSpriteTab()
    {
        EditorGUILayout.LabelField("캐릭터", EditorStyles.miniLabel);
        int prevChar = _selSpriteChar;
        _selSpriteChar = GUILayout.SelectionGrid(_selSpriteChar, _spriteChars, 5);
        if (_selSpriteChar != prevChar)
            _selEmotionHigh = _selEmotionMid = _selEmotionLow = _selEmotion = 0;
        EditorGUILayout.Space(6);

        int maxEmo = _emotionLabels[_selSpriteChar].Length;
        if (_selEmotion >= maxEmo) _selEmotion = 0;

        EditorGUILayout.LabelField("표정", EditorStyles.miniLabel);
        _selEmotion = GUILayout.SelectionGrid(_selEmotion, _emotionLabels[_selSpriteChar], 4);
        EditorGUILayout.Space(4);
        _isRight = EditorGUILayout.ToggleLeft("오른쪽 배치", _isRight);
        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("고정 (환상)", GUILayout.Height(28)))
            AddSpriteFixed(false);
        if (GUILayout.Button("고정 (현실)", GUILayout.Height(28)))
            AddSpriteFixed(true);
        if (GUILayout.Button("자동 분기 (게이지≥70→현실)", GUILayout.Height(28)))
            AddSpriteWithGauge();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);
        Separator();
        EditorGUILayout.LabelField("게이지 구간별 표정", EditorStyles.miniLabel);

        if (_selEmotionHigh >= maxEmo) _selEmotionHigh = 0;
        EditorGUILayout.LabelField("  $심리게이지 >= 70  (현실 버전)", EditorStyles.miniLabel);
        _selEmotionHigh = GUILayout.SelectionGrid(_selEmotionHigh, _emotionLabels[_selSpriteChar], 4);
        EditorGUILayout.Space(2);

        if (_selEmotionMid >= maxEmo) _selEmotionMid = 0;
        EditorGUILayout.LabelField("  $심리게이지 >= 30  (환상 버전)", EditorStyles.miniLabel);
        _selEmotionMid = GUILayout.SelectionGrid(_selEmotionMid, _emotionLabels[_selSpriteChar], 4);
        EditorGUILayout.Space(2);

        if (_selEmotionLow >= maxEmo) _selEmotionLow = 0;
        EditorGUILayout.LabelField("  그 외  (환상 버전)", EditorStyles.miniLabel);
        _selEmotionLow = GUILayout.SelectionGrid(_selEmotionLow, _emotionLabels[_selSpriteChar], 4);
        EditorGUILayout.Space(4);

        if (GUILayout.Button("구간별 표정 추가", GUILayout.Height(28)))
            AddSpriteByGaugeRange();

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

    void AddSpriteFixed(bool useReal)
    {
        string c       = _spriteChars[_selSpriteChar];
        string emotion = useReal
            ? _emotionsR[_selSpriteChar][_selEmotion]
            : _emotionsF[_selSpriteChar][_selEmotion];
        string sideStr = _isRight ? "right" : "left";
        string label   = _emotionLabels[_selSpriteChar][_selEmotion];
        string tag     = useReal ? "[현실 고정]" : "[환상 고정]";
        string display = $"{c} 표정: {label}{(_isRight ? " (오른쪽)" : "")} {tag}";
        string code    = $"<<showSprite \"{c}\" \"{emotion}\" \"{sideStr}\" \"fixed\">>";
        Enqueue(new Line { badge = "표정", display = display, code = code });
    }

    void AddSpriteWithGauge()
    {
        string c       = _spriteChars[_selSpriteChar];
        string eF      = _emotionsF[_selSpriteChar][_selEmotion];
        string side    = _isRight ? " \"right\"" : "";
        string label   = _emotionLabels[_selSpriteChar][_selEmotion];
        string display = $"{c} 표정: {label}{(_isRight ? " (오른쪽)" : "")} [자동 분기]";
        string code    = $"<<showSprite \"{c}\" \"{eF}\"{side}>>";
        Enqueue(new Line { badge = "표정", display = display, code = code });
    }

    void AddSpriteByGaugeRange()
    {
        string c       = _spriteChars[_selSpriteChar];
        string sideArg = _isRight ? " \"right\"" : "";
        string sideStr = _isRight ? " (오른쪽)" : "";

        string eHigh = _emotionsR[_selSpriteChar][_selEmotionHigh];
        string eMid  = _emotionsF[_selSpriteChar][_selEmotionMid];
        string eLow  = _emotionsF[_selSpriteChar][_selEmotionLow];

        string lHigh = _emotionLabels[_selSpriteChar][_selEmotionHigh];
        string lMid  = _emotionLabels[_selSpriteChar][_selEmotionMid];
        string lLow  = _emotionLabels[_selSpriteChar][_selEmotionLow];

        var sb = new StringBuilder();
        sb.AppendLine("<<if $심리게이지 >= 70>>");
        sb.AppendLine($"    <<showSprite \"{c}\" \"{eHigh}\"{sideArg} \"fixed\">>");
        sb.AppendLine("<<elseif $심리게이지 >= 30>>");
        sb.AppendLine($"    <<showSprite \"{c}\" \"{eMid}\"{sideArg} \"fixed\">>");
        sb.AppendLine("<<else>>");
        sb.AppendLine($"    <<showSprite \"{c}\" \"{eLow}\"{sideArg} \"fixed\">>");
        sb.Append("<<endif>>");

        string display = $"{c} 구간별 표정: {lHigh} / {lMid} / {lLow}{sideStr}";
        Enqueue(new Line { badge = "표정", display = display, code = sb.ToString() });
    }

    void DrawBranchTab()
    {
        // ── 모드 토글 ──────────────────────────────────────
        EditorGUILayout.BeginHorizontal();
        bool wantSingle   = GUILayout.Toggle(!_combinedMode, "단일 변수", "Button");
        bool wantCombined = GUILayout.Toggle(_combinedMode,  "복합 분기 (두 변수 동시)", "Button");
        EditorGUILayout.EndHorizontal();
        if      (wantSingle   &&  _combinedMode) _combinedMode = false;
        else if (wantCombined && !_combinedMode) _combinedMode = true;
        EditorGUILayout.Space(4);

        if (_combinedMode) { DrawCombinedBranchTab(); return; }

        // ── 캐릭터 ─────────────────────────────────────────
        EditorGUILayout.LabelField("캐릭터 (분기 대사 화자)", EditorStyles.miniLabel);
        _selBranchChar = GUILayout.SelectionGrid(_selBranchChar, _chars, 6);
        EditorGUILayout.Space(6);

        // ── 분기 변수 선택 ─────────────────────────────────
        EditorGUILayout.LabelField("분기 변수", EditorStyles.miniLabel);
        int prevVar = _selBranchVar;
        _selBranchVar = GUILayout.SelectionGrid(_selBranchVar, _branchVarNames, 2);
        if (_selBranchVar != prevVar)
            ResetBranchForVar(_selBranchVar);

        string vn         = _branchVarNames[_selBranchVar];
        int[]  defThr     = _varDefaultThresholds[_selBranchVar];
        string[] defLabels = _varStageLabels[_selBranchVar];
        int    maxSections = defThr.Length + 1;

        // ── 인형화 안내 ────────────────────────────────────
        if (_selBranchVar == 1)
            EditorGUILayout.HelpBox("데모 실사용 범위: 18~27  —  ≥31 분기는 미래 구현 전용, 데모에서 트리거되지 않음", MessageType.Info);

        // ── 구간 수 선택 ───────────────────────────────────
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("사용할 구간 수", EditorStyles.miniLabel);
        var sectionLabels = new string[maxSections];
        for (int s = maxSections; s >= 1; s--)
            sectionLabels[maxSections - s] = $"{s}구간";

        // _branchUsedSections: -1 = 최대(index 0), 그 외 = index = (maxSections - used)
        int curSectionIdx = _branchUsedSections < 0 ? 0 : _branchUsedSections;
        if (curSectionIdx >= maxSections) curSectionIdx = 0;
        int newSectionIdx = GUILayout.SelectionGrid(curSectionIdx, sectionLabels, sectionLabels.Length);
        if (newSectionIdx != curSectionIdx) _branchUsedSections = newSectionIdx;
        int activeSections = maxSections - (_branchUsedSections < 0 ? 0 : _branchUsedSections);

        // _branchThresholds / _branchTexts 길이 보정
        while (_branchThresholds.Length < defThr.Length)
        {
            var tmp = new int[_branchThresholds.Length + 1];
            System.Array.Copy(_branchThresholds, tmp, _branchThresholds.Length);
            tmp[_branchThresholds.Length] = defThr[_branchThresholds.Length];
            _branchThresholds = tmp;
        }
        while (_branchTexts.Length < maxSections)
        {
            var tmp = new string[_branchTexts.Length + 1];
            System.Array.Copy(_branchTexts, tmp, _branchTexts.Length);
            tmp[_branchTexts.Length] = "";
            _branchTexts = tmp;
        }
        // AND 조건 배열 길이 보정
        while (_branchAndEnabled.Length < maxSections) { System.Array.Resize(ref _branchAndEnabled, _branchAndEnabled.Length + 1); }
        while (_branchAndVar.Length < maxSections)
        {
            System.Array.Resize(ref _branchAndVar, _branchAndVar.Length + 1);
            _branchAndVar[_branchAndVar.Length - 1] = 1 - _selBranchVar;
        }
        while (_branchAndOp.Length  < maxSections) System.Array.Resize(ref _branchAndOp,  _branchAndOp.Length + 1);
        while (_branchAndVal.Length < maxSections) System.Array.Resize(ref _branchAndVal, _branchAndVal.Length + 1);

        int activeThresholds = activeSections - 1; // 구간 수 - 1 = 임계값 수

        // ── 전체 AND 조건 ──────────────────────────────────
        Separator();
        EditorGUILayout.BeginHorizontal();
        _globalAndEnabled = EditorGUILayout.ToggleLeft("전체 AND 조건 (모든 구간에 일괄 적용)", _globalAndEnabled, GUILayout.Width(230));
        if (_globalAndEnabled)
        {
            _globalAndVar = EditorGUILayout.Popup(_globalAndVar, _branchVarNames, GUILayout.Width(96));
            _globalAndOp  = EditorGUILayout.Popup(_globalAndOp,  _andOps,         GUILayout.Width(44));
            _globalAndVal = EditorGUILayout.IntField(_globalAndVal, GUILayout.Width(44));
        }
        EditorGUILayout.EndHorizontal();
        if (_globalAndEnabled)
            EditorGUILayout.HelpBox(
                $"모든 if/elseif 조건에 'and {_branchVarNames[_globalAndVar]} {_andOps[_globalAndOp]} {_globalAndVal}' 추가\n구간별 AND 조건은 무시됩니다.",
                MessageType.Info);
        Separator();

        // ── 임계값 입력 ────────────────────────────────────
        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        for (int t = 0; t < activeThresholds; t++)
        {
            EditorGUILayout.LabelField($"임계값{t + 1}", GUILayout.Width(50));
            _branchThresholds[t] = EditorGUILayout.IntField(_branchThresholds[t], GUILayout.Width(44));
        }
        EditorGUILayout.EndHorizontal();

        // ── 구간별 대사 + AND 조건 입력 ───────────────────
        EditorGUILayout.Space(4);
        for (int s = 0; s < activeSections; s++)
        {
            string stageLabel = s < defLabels.Length ? defLabels[s] : $"구간 {s + 1}";
            int labelIdx = defLabels.Length - activeSections + s;
            if (labelIdx >= 0 && labelIdx < defLabels.Length) stageLabel = defLabels[labelIdx];

            EditorGUILayout.LabelField(stageLabel, EditorStyles.miniLabel);
            _branchTexts[s] = EditorGUILayout.TextArea(_branchTexts[s], GUILayout.Height(32));

            // AND 조건 (else 구간에도 적용 가능)
            EditorGUILayout.BeginHorizontal();
            _branchAndEnabled[s] = EditorGUILayout.ToggleLeft("AND 조건", _branchAndEnabled[s], GUILayout.Width(80));
            if (_branchAndEnabled[s])
            {
                _branchAndVar[s] = EditorGUILayout.Popup(_branchAndVar[s], _branchVarNames, GUILayout.Width(100));
                _branchAndOp[s]  = EditorGUILayout.Popup(_branchAndOp[s],  _andOps,         GUILayout.Width(44));
                _branchAndVal[s] = EditorGUILayout.IntField(_branchAndVal[s], GUILayout.Width(44));
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.Space(4);

        // ── 분기 추가 버튼 ─────────────────────────────────
        if (GUILayout.Button("분기 추가", GUILayout.Height(28)))
        {
            string ch     = _chars[_selBranchChar];
            string prefix = ch == "(나레이션)" ? "" : ch + ": ";
            var sb = new StringBuilder();

            if (activeSections == 1)
            {
                // 1구간: 단독 if 또는 조건 없이 대사만
                bool hasAnd1 = _globalAndEnabled ||
                               (_branchAndEnabled.Length > 0 && _branchAndEnabled[0]);
                string andV1 = _globalAndEnabled
                    ? _branchVarNames[_globalAndVar] : _branchVarNames[_branchAndVar.Length > 0 ? _branchAndVar[0] : 0];
                string andO1 = _globalAndEnabled
                    ? _andOps[_globalAndOp] : _andOps[_branchAndOp.Length > 0 ? _branchAndOp[0] : 0];
                int andVal1  = _globalAndEnabled
                    ? _globalAndVal : (_branchAndVal.Length > 0 ? _branchAndVal[0] : 0);
                if (hasAnd1)
                {
                    sb.AppendLine($"<<if {andV1} {andO1} {andVal1}>>");
                    if (!string.IsNullOrWhiteSpace(_branchTexts[0]))
                        sb.AppendLine("    " + prefix + _branchTexts[0].Trim());
                    sb.Append("<<endif>>");
                }
                else if (!string.IsNullOrWhiteSpace(_branchTexts[0]))
                {
                    sb.Append(prefix + _branchTexts[0].Trim());
                }
            }
            else
            {
                for (int s = 0; s < activeSections; s++)
                {
                    // AND 조건 생성 — 전체 AND 우선, 없으면 구간별 AND
                    string andPart = "";
                    if (_globalAndEnabled)
                    {
                        string andV = _branchVarNames[_globalAndVar];
                        string andO = _andOps[_globalAndOp];
                        andPart = $" and {andV} {andO} {_globalAndVal}";
                    }
                    else if (s < _branchAndEnabled.Length && _branchAndEnabled[s])
                    {
                        string andV = _branchVarNames[_branchAndVar[s]];
                        string andO = _andOps[_branchAndOp[s]];
                        andPart = $" and {andV} {andO} {_branchAndVal[s]}";
                    }

                    if (s == 0)
                        sb.AppendLine($"<<if {vn} >= {_branchThresholds[0]}{andPart}>>");
                    else if (s < activeThresholds)
                        sb.AppendLine($"<<elseif {vn} >= {_branchThresholds[s]}{andPart}>>");
                    else
                    {
                        if (!string.IsNullOrEmpty(andPart))
                            sb.AppendLine($"<<elseif true{andPart}>>"); // else + AND 조건
                        else
                            sb.AppendLine("<<else>>");
                    }

                    if (!string.IsNullOrWhiteSpace(_branchTexts[s]))
                        sb.AppendLine("    " + prefix + _branchTexts[s].Trim());
                }
                sb.Append("<<endif>>");
            }

            // display 요약
            var thrParts = new System.Text.StringBuilder();
            for (int t = 0; t < activeThresholds; t++)
            { if (t > 0) thrParts.Append("/"); thrParts.Append(_branchThresholds[t]); }
            string dispStr = $"{vn} 분기 ({thrParts}/else)";
            if (activeThresholds == 0) dispStr = $"{vn} 분기 (else only)";

            Enqueue(new Line { badge = "분기", display = dispStr, code = sb.ToString() });
            _branchTexts = new string[_branchTexts.Length];
            GUI.FocusControl(null);
        }
    }

    void ResetBranchForVar(int varIdx)
    {
        int[] def = _varDefaultThresholds[varIdx];
        _branchThresholds   = (int[])def.Clone();
        _branchTexts        = new string[def.Length + 1];
        _branchUsedSections = -1;
        int maxS    = def.Length + 1;
        int otherVar = 1 - varIdx; // 0↔1 반대 변수
        _branchAndEnabled = new bool[maxS];
        _branchAndVar     = new int[maxS];
        _branchAndOp      = new int[maxS];
        _branchAndVal     = new int[maxS];
        for (int i = 0; i < maxS; i++) _branchAndVar[i] = otherVar;
        // 전체 AND 기본값도 반대 변수로 갱신
        _globalAndVar = otherVar;
        _globalAndOp  = 1; // <
        _globalAndVal = varIdx == 0 ? 30 : 31; // 심리게이지: 30, 인형화: 31
    }

    // ──────────────────────────────────────────────────────
    // 복합 분기 (두 변수 동시 입력)
    // ──────────────────────────────────────────────────────
    void DrawCombinedBranchTab()
    {
        // ── 캐릭터 ─────────────────────────────────────────
        EditorGUILayout.LabelField("캐릭터 (분기 대사 화자)", EditorStyles.miniLabel);
        _selBranchChar = GUILayout.SelectionGrid(_selBranchChar, _chars, 6);
        EditorGUILayout.Space(6);

        // ── 변수 0 (심리게이지) 설정 ────────────────────────
        EditorGUILayout.LabelField("① 심리게이지  구간 수 / 임계값", EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal();
        _cb0Sections = EditorGUILayout.IntSlider(_cb0Sections, 1, _varDefaultThresholds[0].Length + 1, GUILayout.Width(200));
        EditorGUILayout.LabelField($"{_cb0Sections}구간", GUILayout.Width(40));
        for (int t = 0; t < _cb0Sections - 1; t++)
        {
            if (t >= _cb0Thresholds.Length)
            {
                System.Array.Resize(ref _cb0Thresholds, t + 1);
                _cb0Thresholds[t] = _varDefaultThresholds[0][t];
            }
            EditorGUILayout.LabelField($"≥", GUILayout.Width(14));
            _cb0Thresholds[t] = EditorGUILayout.IntField(_cb0Thresholds[t], GUILayout.Width(40));
        }
        EditorGUILayout.EndHorizontal();

        // ── 변수 1 (인형화) 설정 ────────────────────────────
        EditorGUILayout.LabelField("② 인형화  구간 수 / 임계값", EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal();
        _cb1Sections = EditorGUILayout.IntSlider(_cb1Sections, 1, _varDefaultThresholds[1].Length + 1, GUILayout.Width(200));
        EditorGUILayout.LabelField($"{_cb1Sections}구간", GUILayout.Width(40));
        for (int t = 0; t < _cb1Sections - 1; t++)
        {
            if (t >= _cb1Thresholds.Length)
            {
                System.Array.Resize(ref _cb1Thresholds, t + 1);
                _cb1Thresholds[t] = _varDefaultThresholds[1][t];
            }
            EditorGUILayout.LabelField($"≥", GUILayout.Width(14));
            _cb1Thresholds[t] = EditorGUILayout.IntField(_cb1Thresholds[t], GUILayout.Width(40));
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
            $"조합 수: {_cb0Sections} × {_cb1Sections} = {_cb0Sections * _cb1Sections}칸  " +
            "(비어있는 칸은 코드에서 생략됩니다)", MessageType.None);
        Separator();

        // ── 조합별 대사 입력 ────────────────────────────────
        int total = _cb0Sections * _cb1Sections;
        if (_cbTexts == null || _cbTexts.Length < total)
            System.Array.Resize(ref _cbTexts, Mathf.Max(total, 25));

        string[] mindLabels = CbBuildLabels(0, _cb0Sections, _cb0Thresholds);
        string[] dollLabels = CbBuildLabels(1, _cb1Sections, _cb1Thresholds);

        _cbScroll = EditorGUILayout.BeginScrollView(_cbScroll);
        for (int mi = 0; mi < _cb0Sections; mi++)
        {
            for (int di = 0; di < _cb1Sections; di++)
            {
                int idx = mi * _cb1Sections + di;
                string header = $"심리 {mindLabels[mi]}  +  인형화 {dollLabels[di]}";
                EditorGUILayout.LabelField(header, EditorStyles.miniBoldLabel);
                _cbTexts[idx] = EditorGUILayout.TextArea(_cbTexts[idx] ?? "", GUILayout.Height(30));
                EditorGUILayout.Space(2);
            }
            if (mi < _cb0Sections - 1) Separator();
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(4);
        if (GUILayout.Button("복합 분기 추가  (Ctrl+Enter)", GUILayout.Height(30)))
            AddCombinedBranch();

        if (Event.current.type == EventType.KeyDown &&
            Event.current.keyCode == KeyCode.Return && Event.current.control)
        { AddCombinedBranch(); Event.current.Use(); }
    }

    // 구간 레이블 생성: ["≥70", "≥30", "<30"]
    string[] CbBuildLabels(int varIdx, int sections, int[] thresholds)
    {
        string varName = _branchVarNames[varIdx];
        string[] labels = new string[sections];
        for (int s = 0; s < sections; s++)
        {
            if (s < sections - 1)
                labels[s] = $"≥{(s < thresholds.Length ? thresholds[s] : "?")}";
            else
                labels[s] = s == 0 ? "전체" : $"<{(thresholds.Length > 0 ? thresholds[s - 1].ToString() : "?")}";
        }
        return labels;
    }

    void AddCombinedBranch()
    {
        string ch     = _chars[_selBranchChar];
        string prefix = ch == "(나레이션)" ? "" : ch + ": ";
        string vMind  = _branchVarNames[0]; // $심리게이지
        string vDoll  = _branchVarNames[1]; // $인형화

        // (mind_stage, doll_stage) → 조건 문자열 생성
        string GetMindCond(int mi) =>
            mi < _cb0Sections - 1 && mi < _cb0Thresholds.Length
                ? $"{vMind} >= {_cb0Thresholds[mi]}" : "";
        string GetDollCond(int di) =>
            di < _cb1Sections - 1 && di < _cb1Thresholds.Length
                ? $"{vDoll} >= {_cb1Thresholds[di]}" : "";

        var sb      = new StringBuilder();
        bool first  = true;

        for (int mi = 0; mi < _cb0Sections; mi++)
        {
            for (int di = 0; di < _cb1Sections; di++)
            {
                int    idx  = mi * _cb1Sections + di;
                string text = idx < _cbTexts.Length ? (_cbTexts[idx] ?? "") : "";

                string mc = GetMindCond(mi);
                string dc = GetDollCond(di);

                // 조건 조합
                string fullCond;
                if (string.IsNullOrEmpty(mc) && string.IsNullOrEmpty(dc))
                    fullCond = ""; // else
                else if (string.IsNullOrEmpty(dc))
                    fullCond = mc; // 인형화 else → mind 조건만 (암묵적 doll else)
                else if (string.IsNullOrEmpty(mc))
                    fullCond = dc; // 심리 else → doll 조건만
                else
                    fullCond = $"{mc} and {dc}";

                if (string.IsNullOrEmpty(fullCond))
                    sb.AppendLine("<<else>>");
                else if (first)
                {
                    sb.AppendLine($"<<if {fullCond}>>");
                    first = false;
                }
                else
                    sb.AppendLine($"<<elseif {fullCond}>>");

                if (!string.IsNullOrWhiteSpace(text))
                    sb.AppendLine("    " + prefix + text.Trim());
            }
        }
        sb.Append("<<endif>>");

        string dispStr = $"복합 분기 심리{_cb0Sections}구간×인형화{_cb1Sections}구간";
        Enqueue(new Line { badge = "분기", display = dispStr, code = sb.ToString() });
        _cbTexts = new string[25];
    }

    void DrawChoiceTab()
    {
        bool hasNodeList = _nodeOptions.Length > 1; // 노드 목록 에셋 연결 여부

        _choiceScroll = EditorGUILayout.BeginScrollView(_choiceScroll, GUILayout.Height(160));
        try
        {
            while (_choiceJumpIndices.Count < _choiceLabels.Count) _choiceJumpIndices.Add(0);
            for (int i = 0; i < _choiceLabels.Count; i++)
            {
                EditorGUILayout.LabelField($"선택지 {i + 1}", EditorStyles.miniLabel);
                _choiceLabels[i] = EditorGUILayout.TextField("텍스트", _choiceLabels[i]);

                if (hasNodeList)
                {
                    int prevJIdx = _choiceJumpIndices[i];
                    _choiceJumpIndices[i] = EditorGUILayout.Popup("이동 노드", _choiceJumpIndices[i], _nodeOptions);
                    bool isCustomChoice = _choiceJumpIndices[i] == _nodeOptions.Length - 1;
                    if (!isCustomChoice)
                        _choiceJumps[i] = _nodeOptions[_choiceJumpIndices[i]];
                    if (isCustomChoice)
                        _choiceJumps[i] = EditorGUILayout.TextField("직접 입력", _choiceJumps[i]);
                }
                else
                {
                    _choiceJumps[i] = EditorGUILayout.TextField("이동 노드", _choiceJumps[i]);
                }
                // 조건부 선택지
                while (_choiceCondEnabled.Count <= i) _choiceCondEnabled.Add(false);
                while (_choiceConditions.Count  <= i) _choiceConditions.Add("");
                _choiceCondEnabled[i] = EditorGUILayout.ToggleLeft("조건 추가 (인형화·게이지)", _choiceCondEnabled[i]);
                if (_choiceCondEnabled[i])
                    _choiceConditions[i] = EditorGUILayout.TextField("  조건식 예: $인형화 < 31", _choiceConditions[i]);

                EditorGUILayout.Space(4);
            }
        }
        finally { EditorGUILayout.EndScrollView(); }

        bool addOne = false, addAll = false;
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ 선택지 추가"))                          addOne = true;
        if (GUILayout.Button("선택지 전체 추가", GUILayout.Height(28))) addAll = true;
        EditorGUILayout.EndHorizontal();

        if (addOne)
            _pending.Enqueue(() =>
            {
                _choiceLabels.Add(""); _choiceJumps.Add(""); _choiceJumpIndices.Add(0);
                _choiceCondEnabled.Add(false); _choiceConditions.Add("");
            });

        if (addAll)
        {
            var sb = new StringBuilder();
            int validCount = 0;
            for (int i = 0; i < _choiceLabels.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(_choiceLabels[i])) continue;
                validCount++;
                bool hasCond = i < _choiceCondEnabled.Count && _choiceCondEnabled[i]
                               && !string.IsNullOrWhiteSpace(_choiceConditions[i]);
                string condStr = hasCond ? $" <<if {_choiceConditions[i].Trim()}>>" : "";
                sb.AppendLine($"-> {_choiceLabels[i].Trim()}{condStr}");
                if (!string.IsNullOrWhiteSpace(_choiceJumps[i]))
                    sb.AppendLine($"    <<jump {_choiceJumps[i].Trim()}>>");
            }
            if (sb.Length > 0)
            {
                string code  = sb.ToString().TrimEnd();
                int    count = validCount;
                _pending.Enqueue(() =>
                {
                    _lines.Add(new Line { badge = "선택지", display = $"선택지 {count}개", code = code });
                    _choiceLabels      = new List<string> { "", "" };
                    _choiceJumps       = new List<string> { "", "" };
                    _choiceJumpIndices = new List<int>    { 0, 0 };
                    _choiceCondEnabled = new List<bool>   { false, false };
                    _choiceConditions  = new List<string> { "", "" };
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
        EditorGUILayout.LabelField("이동할 노드", EditorStyles.miniLabel);

        if (_nodeOptions.Length > 1)
        {
            int prevJump = _jumpNodeIndex;
            _jumpNodeIndex = EditorGUILayout.Popup(_jumpNodeIndex, _nodeOptions);
            bool isCustomJump = _jumpNodeIndex == _nodeOptions.Length - 1;
            if (!isCustomJump) _jumpTarget = _nodeOptions[_jumpNodeIndex];
            if (isCustomJump)
                _jumpTarget = EditorGUILayout.TextField("직접 입력", _jumpTarget);
        }
        else
        {
            _jumpTarget = EditorGUILayout.TextField(_jumpTarget);
        }
        EditorGUILayout.Space(4);

        bool addJump = GUILayout.Button("노드 이동 추가  (Ctrl+Enter)", GUILayout.Height(28));
        if (Event.current.type == EventType.KeyDown &&
            Event.current.keyCode == KeyCode.Return && Event.current.control)
        { addJump = true; Event.current.Use(); }
        if (addJump && !string.IsNullOrWhiteSpace(_jumpTarget))
        {
            string t = _jumpTarget.Trim();
            Enqueue(new Line { badge = "이동", display = $"이동: {t}", code = $"<<jump {t}>>" });
            _jumpTarget    = "";
            _jumpNodeIndex = _nodeOptions.Length > 0 ? _nodeOptions.Length - 1 : 0;
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
        if (_focusRadioArea) { EditorGUI.FocusTextInControl("RadioTextArea"); _focusRadioArea = false; }
        GUI.SetNextControlName("RadioTextArea");
        _radioText = EditorGUILayout.TextArea(_radioText, GUILayout.Height(56));
        EditorGUILayout.Space(4);
        _radioStatic = EditorGUILayout.ToggleLeft("끊기는 효과  (단어 사이에 ... 자동 삽입)", _radioStatic);
        _radioStyle  = EditorGUILayout.ToggleLeft("라디오 스타일 태그  (<i><color=#d4c97a> 노란 기울임 </color></i>)", _radioStyle);
        EditorGUILayout.Space(4);

        _radioSpriteEnabled = EditorGUILayout.ToggleLeft("표정 추가  (showSprite 자동 삽입)", _radioSpriteEnabled);
        if (_radioSpriteEnabled)
        {
            EditorGUILayout.LabelField("캐릭터", EditorStyles.miniLabel);
            int prevChar = _radioSpriteChar;
            _radioSpriteChar = GUILayout.SelectionGrid(_radioSpriteChar, _spriteChars, 5);
            if (_radioSpriteChar != prevChar) _radioSpriteEmotion = 0;

            int maxEmo = _emotionLabels[_radioSpriteChar].Length;
            if (_radioSpriteEmotion >= maxEmo) _radioSpriteEmotion = 0;
            EditorGUILayout.LabelField("표정", EditorStyles.miniLabel);
            _radioSpriteEmotion = GUILayout.SelectionGrid(_radioSpriteEmotion, _emotionLabels[_radioSpriteChar], 4);
            _radioSpriteRight = EditorGUILayout.ToggleLeft("오른쪽 배치", _radioSpriteRight);
        }
        EditorGUILayout.Space(4);

        bool addRadio = GUILayout.Button("라디오 대사 추가  (Ctrl+Enter)", GUILayout.Height(28));
        if (Event.current.type == EventType.KeyDown &&
            Event.current.keyCode == KeyCode.Return && Event.current.control)
        { addRadio = true; Event.current.Use(); }
        if (addRadio && !string.IsNullOrWhiteSpace(_radioText))
        {
            if (_radioSpriteEnabled)
            {
                string c       = _spriteChars[_radioSpriteChar];
                string emotion = _emotionsF[_radioSpriteChar][_radioSpriteEmotion];
                string side    = _radioSpriteRight ? " \"right\"" : "";
                string label   = _emotionLabels[_radioSpriteChar][_radioSpriteEmotion];
                string spriteDisplay = $"{c} 표정: {label}{(_radioSpriteRight ? " (오른쪽)" : "")} [자동 분기]";
                string spriteCode    = $"<<showSprite \"{c}\" \"{emotion}\"{side}>>";
                Enqueue(new Line { badge = "표정", display = spriteDisplay, code = spriteCode });
            }

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
                body = $"<i><color=\\#d4c97a>{body}</color></i>";

            string origText = _radioText.Trim();
            bool   st = _radioStatic, ss = _radioStyle;
            string code    = $"{speaker}: {body}";
            string display = $"{speaker}: {origText}{(st ? " (끊김)" : "")}{(ss ? " (스타일)" : "")}";
            Enqueue(new Line { badge = "라디오", display = display, code = code });
            _radioText = "";
            _focusRadioArea = true;
            Repaint();
        }
    }

    // ══════════════════════════════════════════════════════
    // 카메라 탭
    // ══════════════════════════════════════════════════════

    void DrawTitleTab()
    {
        EditorGUILayout.LabelField("새 노드 제목", EditorStyles.miniLabel);
        if (_focusTitleInput) { EditorGUI.FocusTextInControl("TitleInput"); _focusTitleInput = false; }
        GUI.SetNextControlName("TitleInput");
        _titleInput = EditorGUILayout.TextField(_titleInput);
        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox("현재 노드를 끝내고 새 노드를 시작합니다.", MessageType.Info);
        EditorGUILayout.Space(4);

        bool addTitle = GUILayout.Button("제목 추가  (Ctrl+Enter)", GUILayout.Height(28));
        if (Event.current.type == EventType.KeyDown &&
            Event.current.keyCode == KeyCode.Return && Event.current.control)
        { addTitle = true; Event.current.Use(); }

        if (addTitle && !string.IsNullOrWhiteSpace(_titleInput))
        {
            string t    = _titleInput.Trim();
            string code = $"===\ntitle: {t}\n---";
            Enqueue(new Line { badge = "제목", display = $"── {t} ──", code = code });
            _titleInput = "";
            _focusTitleInput = true;
            Repaint();
        }
    }

    void DrawCameraTab()
    {
        EditorGUILayout.LabelField("카메라 연출 커맨드", _headerStyle);

        // ── cam_zoom_in ──────────────────────────────────
        EditorGUILayout.LabelField("cam_zoom_in  (타겟 이동 + 줌인)", EditorStyles.miniBoldLabel);
        _camTarget       = EditorGUILayout.TextField("타겟 오브젝트명", _camTarget);
        _camZoomAmount   = EditorGUILayout.FloatField("줌 량", _camZoomAmount);
        _camZoomDuration = EditorGUILayout.FloatField("지속시간 (초)", _camZoomDuration);
        if (GUILayout.Button("cam_zoom_in 추가", GUILayout.Height(22)))
        {
            string t = _camTarget; float za = _camZoomAmount; float d = _camZoomDuration;
            Enqueue(new Line {
                badge   = "카메라",
                display = $"줌인 → {t} (x{za}, {d}s)",
                code    = $"<<cam_zoom_in \"{t}\" {za} {d}>>"
            });
        }

        Separator();

        // ── cam_zoom_out ─────────────────────────────────
        EditorGUILayout.LabelField("cam_zoom_out  (줌아웃)", EditorStyles.miniBoldLabel);
        _camZoomAmount   = EditorGUILayout.FloatField("줌 량", _camZoomAmount);
        _camZoomDuration = EditorGUILayout.FloatField("지속시간 (초)", _camZoomDuration);
        if (GUILayout.Button("cam_zoom_out 추가", GUILayout.Height(22)))
        {
            float za = _camZoomAmount; float d = _camZoomDuration;
            Enqueue(new Line {
                badge   = "카메라",
                display = $"줌아웃 (x{za}, {d}s)",
                code    = $"<<cam_zoom_out {za} {d}>>"
            });
        }

        Separator();

        // ── cam_cut ──────────────────────────────────────
        EditorGUILayout.LabelField("cam_cut  (즉시 이동)", EditorStyles.miniBoldLabel);
        _camTarget = EditorGUILayout.TextField("타겟 오브젝트명", _camTarget);
        if (GUILayout.Button("cam_cut 추가", GUILayout.Height(22)))
        {
            string t = _camTarget;
            Enqueue(new Line {
                badge   = "카메라",
                display = $"컷 → {t}",
                code    = $"<<cam_cut \"{t}\">>"
            });
        }

        Separator();

        // ── cam_pan ──────────────────────────────────────
        EditorGUILayout.LabelField("cam_pan  (시작 → 끝 팬)", EditorStyles.miniBoldLabel);
        _camTarget   = EditorGUILayout.TextField("시작 타겟", _camTarget);
        _camToTarget = EditorGUILayout.TextField("끝 타겟",   _camToTarget);
        _camSpeed    = EditorGUILayout.FloatField("속도",      _camSpeed);
        if (GUILayout.Button("cam_pan 추가", GUILayout.Height(22)))
        {
            string f = _camTarget; string to = _camToTarget; float sp = _camSpeed;
            Enqueue(new Line {
                badge   = "카메라",
                display = $"팬 {f} → {to} (speed {sp})",
                code    = $"<<cam_pan \"{f}\" \"{to}\" {sp}>>"
            });
        }

        Separator();

        // ── cam_pan_up ───────────────────────────────────
        EditorGUILayout.LabelField("cam_pan_up  (위로 팬)", EditorStyles.miniBoldLabel);
        _camHeight = EditorGUILayout.FloatField("높이", _camHeight);
        _camSpeed  = EditorGUILayout.FloatField("속도", _camSpeed);
        if (GUILayout.Button("cam_pan_up 추가", GUILayout.Height(22)))
        {
            float h = _camHeight; float sp = _camSpeed;
            Enqueue(new Line {
                badge   = "카메라",
                display = $"팬업 +{h} (speed {sp})",
                code    = $"<<cam_pan_up {h} {sp}>>"
            });
        }

        Separator();

        // ── cam_pov ──────────────────────────────────────
        EditorGUILayout.LabelField("cam_pov  (타겟 이동 + Z 회전)", EditorStyles.miniBoldLabel);
        _camTarget = EditorGUILayout.TextField("타겟 오브젝트명", _camTarget);
        _camAngle  = EditorGUILayout.FloatField("회전 각도 (°)", _camAngle);
        if (GUILayout.Button("cam_pov 추가", GUILayout.Height(22)))
        {
            string t = _camTarget; float a = _camAngle;
            Enqueue(new Line {
                badge   = "카메라",
                display = $"POV → {t} ({a}°)",
                code    = $"<<cam_pov \"{t}\" {a}>>"
            });
        }

        Separator();

        // ── cam_static ───────────────────────────────────
        EditorGUILayout.LabelField("cam_static  (추적 중단)", EditorStyles.miniBoldLabel);
        EditorGUILayout.HelpBox("camera_restore 로 원래 상태로 복귀합니다.", MessageType.None);
        if (GUILayout.Button("cam_static 추가", GUILayout.Height(22)))
            Enqueue(new Line { badge = "카메라", display = "카메라 정지", code = "<<cam_static>>" });

        Separator();

        // ── cam_tilt ─────────────────────────────────────
        EditorGUILayout.LabelField("cam_tilt  (넘어짐 연출, 자동 복귀)", EditorStyles.miniBoldLabel);
        _camAngle      = EditorGUILayout.FloatField("기울기 각도 (°)", _camAngle);
        _camReturnTime = EditorGUILayout.FloatField("복귀 대기 (초)",  _camReturnTime);
        if (GUILayout.Button("cam_tilt 추가", GUILayout.Height(22)))
        {
            float a = _camAngle; float r = _camReturnTime;
            Enqueue(new Line {
                badge   = "카메라",
                display = $"틸트 {a}° ({r}s 후 복귀)",
                code    = $"<<cam_tilt {a} {r}>>"
            });
        }

        Separator();

        // ── cam_slowmo ───────────────────────────────────
        EditorGUILayout.LabelField("cam_slowmo  (슬로우 모션)", EditorStyles.miniBoldLabel);
        _camTimeScale        = EditorGUILayout.Slider("배속 (0~1)", _camTimeScale, 0.01f, 1f);
        _camSlowmoDuration   = EditorGUILayout.FloatField("지속시간 실시간 (초)", _camSlowmoDuration);
        if (GUILayout.Button("cam_slowmo 추가", GUILayout.Height(22)))
        {
            float ts = _camTimeScale; float d = _camSlowmoDuration;
            Enqueue(new Line {
                badge   = "카메라",
                display = $"슬로모 x{ts} ({d}s)",
                code    = $"<<cam_slowmo {ts} {d}>>"
            });
        }

        Separator();

        // ── cam_shake ────────────────────────────────────
        EditorGUILayout.LabelField("cam_shake  (카메라 흔들기)", EditorStyles.miniBoldLabel);
        _camIntensity    = EditorGUILayout.FloatField("강도",          _camIntensity);
        _camShakeDuration = EditorGUILayout.FloatField("지속시간 (초)", _camShakeDuration);
        if (GUILayout.Button("cam_shake 추가", GUILayout.Height(22)))
        {
            float i = _camIntensity; float d = _camShakeDuration;
            Enqueue(new Line {
                badge   = "카메라",
                display = $"쉐이크 (강도 {i}, {d}s)",
                code    = $"<<cam_shake {i} {d}>>"
            });
        }

        Separator();

        // ── cam_fade_down ────────────────────────────────
        EditorGUILayout.LabelField("cam_fade_down  (페이드 아웃)", EditorStyles.miniBoldLabel);
        _camFadeDuration = EditorGUILayout.FloatField("지속시간 (초)", _camFadeDuration);
        if (GUILayout.Button("cam_fade_down 추가", GUILayout.Height(22)))
        {
            float d = _camFadeDuration;
            Enqueue(new Line {
                badge   = "카메라",
                display = $"페이드 아웃 ({d}s)",
                code    = $"<<cam_fade_down {d}>>"
            });
        }

        Separator();

        // ── camera_restore ───────────────────────────────
        EditorGUILayout.LabelField("camera_restore  (카메라 상태 복귀)", EditorStyles.miniBoldLabel);
        EditorGUILayout.HelpBox("cam_zoom_in / cam_cut / cam_static 등 이후 원래 상태로 복귀합니다.", MessageType.None);
        if (GUILayout.Button("camera_restore 추가", GUILayout.Height(22)))
            Enqueue(new Line { badge = "카메라", display = "카메라 복귀", code = "<<camera_restore>>" });
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
            _pending.Enqueue(() => { _lines.Clear(); _selectedLineIndex = -1; _editingLineIndex = -1; });
            Repaint();
        }

        if (_lines.Count == 0)
        {
            EditorGUILayout.HelpBox("아직 추가된 내용이 없어요.", MessageType.None);
            return;
        }

        if (_scrollListToBottom && Event.current.type == EventType.Repaint)
        { _listScroll = new Vector2(0, float.MaxValue); _scrollListToBottom = false; }

        _listScroll = EditorGUILayout.BeginScrollView(
            _listScroll, GUILayout.Height(Mathf.Min(_lines.Count * 28f + 8, 140)));

        int deleteIndex = -1;
        int swapA = -1, swapB = -1;
        try
        {
            for (int i = 0; i < _lines.Count; i++)
            {
                bool isSelected = i == _selectedLineIndex;
                if (isSelected)
                    EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), new Color(0.55f, 0.47f, 0.86f, 0.35f));

                EditorGUILayout.BeginHorizontal();
                try
                {
                    GUILayout.Label($"[{_lines[i].badge}]", _badgeStyle, GUILayout.Width(52));
                    if (GUILayout.Button(_lines[i].display, EditorStyles.miniLabel))
                    {
                        if (_selectedLineIndex == i && _editingLineIndex != i)
                        {
                            // 이미 선택된 항목 재클릭 → 바로 편집 모드
                            _editingLineIndex   = i;
                            _editingLineContent = _lines[i].code;
                        }
                        else
                        {
                            _selectedLineIndex = isSelected ? -1 : i;
                            _editingLineIndex  = -1;
                        }
                    }
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

        // 선택된 줄 미리보기 / 인라인 편집
        if (_selectedLineIndex >= 0 && _selectedLineIndex < _lines.Count)
        {
            if (_editingLineIndex == _selectedLineIndex)
            {
                // ── 인라인 편집 모드 ──────────────────────────
                EditorGUILayout.LabelField("인라인 편집", EditorStyles.miniLabel);
                GUI.SetNextControlName("InlineEditField");
                _editingLineContent = EditorGUILayout.TextArea(_editingLineContent, _codeStyle, GUILayout.Height(72));
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("✔ 적용", GUILayout.Height(24)))
                {
                    int idx = _editingLineIndex;
                    string newCode = _editingLineContent;
                    _pending.Enqueue(() =>
                    {
                        if (idx < _lines.Count)
                        {
                            var l = _lines[idx];
                            l.code = newCode;
                            // display 첫 줄로 갱신
                            string firstLine = newCode.Split('\n')[0].Trim();
                            l.display = firstLine.Length > 60 ? firstLine.Substring(0, 60) + "…" : firstLine;
                            _lines[idx] = l;
                        }
                    });
                    _editingLineIndex = -1;
                }
                if (GUILayout.Button("✕ 취소", GUILayout.Height(24)))
                    _editingLineIndex = -1;
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                // ── 미리보기 + 편집 버튼 ──────────────────────
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("코드 미리보기", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("✏️ 편집", GUILayout.Width(54), GUILayout.Height(18)))
                {
                    _editingLineIndex   = _selectedLineIndex;
                    _editingLineContent = _lines[_selectedLineIndex].code;
                    EditorGUI.FocusTextInControl("InlineEditField");
                }
                EditorGUILayout.EndHorizontal();
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextArea(_lines[_selectedLineIndex].code, _codeStyle, GUILayout.Height(56));
                EditorGUI.EndDisabledGroup();
            }
        }

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
            _pending.Enqueue(() =>
            {
                if (idx < _lines.Count) _lines.RemoveAt(idx);
                if (_selectedLineIndex >= _lines.Count) _selectedLineIndex = _lines.Count - 1;
                if (_editingLineIndex == idx || _editingLineIndex >= _lines.Count) _editingLineIndex = -1;
            });
        }
    }

    // ══════════════════════════════════════════════════════
    // 코드 출력 + 저장
    // ══════════════════════════════════════════════════════

    string BuildYarn()
    {
        var sb = new StringBuilder();
        sb.Append($"title: {_nodeName}\n");
        sb.Append("---\n");
        foreach (var l in _lines)
        {
            string code = l.code.Replace("\r\n", "\n");
            if (!code.EndsWith("\n")) code += "\n";
            sb.Append(code);
        }
        sb.Append("===");
        return sb.ToString();
    }

    void DrawOutput()
    {
        if (_yarnDirty) { _yarnCache = BuildYarn(); _yarnDirty = false; }
        string yarn = _yarnCache;
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("생성된 Yarn 코드", _headerStyle);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("📋 복사", GUILayout.Width(58)))
        {
            GUIUtility.systemCopyBuffer = yarn;
            ShowNotification(new GUIContent("클립보드에 복사됐어요!"));
        }
        EditorGUILayout.EndHorizontal();

        if (_scrollOutputToBottom && Event.current.type == EventType.Repaint)
        { _outputScroll = new Vector2(0, float.MaxValue); _scrollOutputToBottom = false; }

        _outputScroll = EditorGUILayout.BeginScrollView(_outputScroll, GUILayout.Height(140));
        try
        {
            EditorGUILayout.TextArea(yarn, _codeStyle, GUILayout.ExpandHeight(true));
        }
        finally { EditorGUILayout.EndScrollView(); }

        EditorGUILayout.Space(4);

        // ── 저장 방식 선택 ────────────────────────────────
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("저장 방식", GUILayout.Width(60));
        bool wantNew    = GUILayout.Toggle(!_appendMode, "새 파일 생성",    "Button");
        bool wantAppend = GUILayout.Toggle(_appendMode,  "기존 파일에 추가", "Button");
        EditorGUILayout.EndHorizontal();

        if      (wantNew    &&  _appendMode) _appendMode = false;
        else if (wantAppend && !_appendMode) { _appendMode = true; RefreshExistingYarns(); }

        // ── append 모드: 대상 파일 선택 ──────────────────
        if (_appendMode)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("↺", GUILayout.Width(28))) RefreshExistingYarns();
            if (_existingYarns.Length == 0)
                EditorGUILayout.HelpBox("저장 폴더에 .yarn 파일이 없어요.", MessageType.Warning);
            else
                _targetYarnIndex = EditorGUILayout.Popup(_targetYarnIndex, _existingYarns);
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(4);

        // ── 저장 폴더 ─────────────────────────────────────
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
            {
                _savePath = picked.StartsWith(Application.dataPath)
                    ? "Assets" + picked.Substring(Application.dataPath.Length)
                    : picked;
                if (_appendMode) RefreshExistingYarns();
            }
        }

        EditorGUILayout.Space(4);

        // ── 저장 버튼 ─────────────────────────────────────
        if (_appendMode)
        {
            EditorGUI.BeginDisabledGroup(_existingYarns.Length == 0);
            if (GUILayout.Button("📎  기존 파일에 노드 추가", GUILayout.Height(32)))
                AppendToYarnFile(yarn);
            EditorGUI.EndDisabledGroup();
        }
        else
        {
            if (GUILayout.Button("📄  .yarn 파일로 저장", GUILayout.Height(32)))
                SaveYarnFile(yarn);
        }

        // 수정 모드에서 불러온 경우 원본 덮어쓰기 버튼 표시
        if (!string.IsNullOrEmpty(_importedFromFile))
        {
            EditorGUILayout.Space(2);
            string shortPath = _importedFromFile.Contains("Assets")
                ? _importedFromFile.Substring(_importedFromFile.IndexOf("Assets"))
                : _importedFromFile;
            EditorGUILayout.HelpBox($"출처: {shortPath}  /  원본 노드: {_importedNodeName}", MessageType.Info);
            if (GUILayout.Button("🔄  원본 파일 노드 덮어쓰기", GUILayout.Height(32)))
                OverwriteNodeInSourceFile(yarn);
        }
    }

    void SaveYarnFile(string content)
    {
        string fullDir = Path.Combine(Application.dataPath,
            _savePath.StartsWith("Assets/") ? _savePath.Substring(7) : _savePath);
        Directory.CreateDirectory(fullDir);
        File.WriteAllText(Path.Combine(fullDir, _nodeName + ".yarn"), content.Replace("\r\n", "\n"), Encoding.UTF8);
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("저장 완료", $"{_nodeName}.yarn 이 저장됐어요!\n경로: {_savePath}", "확인");
        if (_lines.Count > 0 &&
            EditorUtility.DisplayDialog("줄 초기화", "줄 목록을 초기화할까요?", "초기화", "유지"))
            _pending.Enqueue(() => { _lines.Clear(); _selectedLineIndex = -1; _editingLineIndex = -1; });
    }

    void RefreshExistingYarns()
    {
        string fullDir = Path.Combine(Application.dataPath,
            _savePath.StartsWith("Assets/") ? _savePath.Substring(7) : _savePath);
        if (!Directory.Exists(fullDir)) { _existingYarns = System.Array.Empty<string>(); return; }

        var files = Directory.GetFiles(fullDir, "*.yarn", SearchOption.AllDirectories);
        // 드롭다운에 "Scripts/House.yarn" 형태의 상대 경로 표시
        _existingYarns = System.Array.ConvertAll(files, f =>
            f.Substring(fullDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        _targetYarnIndex = Mathf.Clamp(_targetYarnIndex, 0, Mathf.Max(0, _existingYarns.Length - 1));
    }

    void AppendToYarnFile(string nodeContent)
    {
        if (_existingYarns.Length == 0) return;

        string fullDir  = Path.Combine(Application.dataPath,
            _savePath.StartsWith("Assets/") ? _savePath.Substring(7) : _savePath);
        string filePath = Path.Combine(fullDir, _existingYarns[_targetYarnIndex]);

        // 줄바꿈 정규화 (\r\n → \n)
        string existing = File.ReadAllText(filePath, Encoding.UTF8).Replace("\r\n", "\n");
        string content  = nodeContent.Replace("\r\n", "\n");

        // 동일한 타이틀 노드가 이미 존재하는지 확인
        bool titleExists = false;
        foreach (var line in existing.Split('\n'))
        {
            string lt = line.Trim();
            if (lt.StartsWith("title:") && lt.Substring("title:".Length).Trim() == _nodeName)
            { titleExists = true; break; }
        }

        if (titleExists)
        {
            bool overwrite = EditorUtility.DisplayDialog(
                "중복 노드 감지",
                $"'{_nodeName}' 노드가 이미 파일에 존재합니다.\n기존 노드를 새 내용으로 덮어쓸까요?",
                "덮어쓰기", "취소");
            if (!overwrite) return;

            // 기존 노드 교체
            var segments = existing.Split(new string[]{"==="}, System.StringSplitOptions.None);
            bool found = false;
            for (int i = 0; i < segments.Length; i++)
            {
                string trimmed = segments[i].Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;
                foreach (var line in trimmed.Split('\n'))
                {
                    string t = line.Trim();
                    if (t.StartsWith("title:") && t.Substring("title:".Length).Trim() == _nodeName)
                    {
                        string newSeg = content.TrimEnd();
                        if (newSeg.EndsWith("===")) newSeg = newSeg.Substring(0, newSeg.Length - 3).TrimEnd();
                        segments[i] = "\n" + newSeg + "\n";
                        found = true;
                        break;
                    }
                }
                if (found) break;
            }

            if (!found)
            {
                EditorUtility.DisplayDialog("오류", $"파일에서 '{_nodeName}' 노드를 찾을 수 없어요.", "확인");
                return;
            }
            File.WriteAllText(filePath, string.Join("===", segments).Trim(), Encoding.UTF8);
        }
        else
        {
            string trimmedExisting = existing.TrimEnd();
            if (!trimmedExisting.EndsWith("==="))
                trimmedExisting += "\n===";
            File.WriteAllText(filePath, trimmedExisting + "\n" + content, Encoding.UTF8);
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(titleExists ? "덮어쓰기 완료" : "추가 완료",
            $"'{_nodeName}' 노드가\n{_existingYarns[_targetYarnIndex]} 에 {(titleExists ? "덮어써졌어요" : "추가됐어요")}!", "확인");
        if (_lines.Count > 0 &&
            EditorUtility.DisplayDialog("줄 초기화", "줄 목록을 초기화할까요?", "초기화", "유지"))
            _pending.Enqueue(() => { _lines.Clear(); _selectedLineIndex = -1; _editingLineIndex = -1; });
    }

    void OverwriteNodeInSourceFile(string nodeContent)
    {
        if (!File.Exists(_importedFromFile))
        {
            EditorUtility.DisplayDialog("오류", "원본 파일을 찾을 수 없어요.", "확인");
            return;
        }

        string fileText = File.ReadAllText(_importedFromFile, Encoding.UTF8).Replace("\r\n", "\n");
        string content  = nodeContent.Replace("\r\n", "\n");
        var    segments = fileText.Split(new string[]{"==="}, System.StringSplitOptions.None);
        bool   found    = false;

        for (int i = 0; i < segments.Length; i++)
        {
            string trimmed = segments[i].Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;
            foreach (var line in trimmed.Split('\n'))
            {
                string t = line.Trim();
                if (t.StartsWith("title:") && t.Substring("title:".Length).Trim() == _importedNodeName)
                {
                    string newSeg = content.TrimEnd();
                    if (newSeg.EndsWith("===")) newSeg = newSeg.Substring(0, newSeg.Length - 3).TrimEnd();
                    segments[i] = "\n" + newSeg + "\n";
                    found = true;
                    break;
                }
            }
            if (found) break;
        }

        if (!found)
        {
            EditorUtility.DisplayDialog("오류", $"원본 파일에서 '{_importedNodeName}' 노드를 찾을 수 없어요.", "확인");
            return;
        }

        string result = string.Join("===", segments).Trim();
        File.WriteAllText(_importedFromFile, result, Encoding.UTF8);
        AssetDatabase.Refresh();
        _importedNodeName = _nodeName;
        EditorUtility.DisplayDialog("덮어쓰기 완료", $"'{_nodeName}' 노드가 원본 파일에 저장됐어요!", "확인");
        if (_lines.Count > 0 &&
            EditorUtility.DisplayDialog("줄 초기화", "줄 목록을 초기화할까요?", "초기화", "유지"))
            _pending.Enqueue(() => { _lines.Clear(); _selectedLineIndex = -1; _editingLineIndex = -1; });
    }

    // ══════════════════════════════════════════════════════
    // 헬퍼
    // ══════════════════════════════════════════════════════

    void Enqueue(Line line)
    {
        _pending.Enqueue(() => _lines.Add(line));
        _scrollOutputToBottom = true;
        _scrollListToBottom   = true;
    }

    void InitStyles()
    {
        if (_stylesReady) return;

        _headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13,
            margin   = new RectOffset(0, 0, 8, 4)
        };

        _badgeTex   = MakeTex(1, 1, new Color(0.55f, 0.47f, 0.86f, 0.25f));
        _badgeStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize  = 11,
            alignment = TextAnchor.MiddleCenter,
            normal    = { background = _badgeTex }
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

    // ══════════════════════════════════════════════════════
    // 수정 모드
    // ══════════════════════════════════════════════════════

    void DrawEditMode()
    {
        EditorGUILayout.LabelField("기존 노드 수정", _headerStyle);
        EditorGUILayout.Space(4);

        // 저장 폴더
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("저장 폴더", GUILayout.Width(60));
        _savePath = EditorGUILayout.TextField(_savePath);
        if (GUILayout.Button("...", GUILayout.Width(28)))
        {
            string picked = EditorUtility.OpenFolderPanel("저장 폴더 선택", _savePath, "");
            if (!string.IsNullOrEmpty(picked))
            {
                _savePath = picked.StartsWith(Application.dataPath)
                    ? "Assets" + picked.Substring(Application.dataPath.Length)
                    : picked;
                RefreshExistingYarns();
                RefreshEditNodes();
            }
        }
        EditorGUILayout.EndHorizontal();

        // .yarn 파일 선택
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("↺", GUILayout.Width(28))) { RefreshExistingYarns(); RefreshEditNodes(); }
        if (_existingYarns.Length == 0)
            EditorGUILayout.HelpBox("저장 폴더에 .yarn 파일이 없어요.", MessageType.Warning);
        else
        {
            int prevFile = _editYarnIndex;
            _editYarnIndex = EditorGUILayout.Popup(_editYarnIndex, _existingYarns);
            if (_editYarnIndex != prevFile)
            {
                if (_editDirty && !EditorUtility.DisplayDialog("경고",
                    "저장되지 않은 변경사항이 있습니다.\n계속하면 변경사항을 잃게 됩니다.", "계속", "취소"))
                    _editYarnIndex = prevFile;
                else
                    { RefreshEditNodes(); _editNodeContent = ""; _editDirty = false; }
            }
        }
        EditorGUILayout.EndHorizontal();

        if (_existingYarns.Length == 0) return;

        // 노드 선택
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("노드 선택", EditorStyles.miniLabel);
        if (_editNodeNames.Length == 0)
        {
            EditorGUILayout.HelpBox("이 파일에 파싱 가능한 노드가 없어요.", MessageType.Warning);
            return;
        }

        EditorGUILayout.BeginHorizontal();
        int prevNode = _editNodeIndex;
        _editNodeIndex = EditorGUILayout.Popup(_editNodeIndex, _editNodeNames);
        if (_editNodeIndex != prevNode)
        {
            if (_editDirty && !EditorUtility.DisplayDialog("경고",
                "저장되지 않은 변경사항이 있습니다.\n계속하면 변경사항을 잃게 됩니다.", "계속", "취소"))
                _editNodeIndex = prevNode;
            else
                { _editNodeContent = ""; _editDirty = false; }
        }
        if (GUILayout.Button("불러오기", GUILayout.Width(70)))
            LoadSelectedNode();
        EditorGUILayout.EndHorizontal();

        if (string.IsNullOrEmpty(_editNodeContent))
        {
            EditorGUILayout.HelpBox("노드를 선택하고 [불러오기]를 누르세요.", MessageType.None);
            return;
        }

        // 편집 영역
        EditorGUILayout.Space(6);
        if (_editDirty)
            EditorGUILayout.HelpBox("수정됨 — 저장하지 않으면 변경사항이 사라집니다.", MessageType.Warning);

        _editScroll = EditorGUILayout.BeginScrollView(_editScroll, GUILayout.Height(360));
        string newContent = EditorGUILayout.TextArea(_editNodeContent, _codeStyle, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();

        if (newContent != _editNodeContent)
        {
            _editNodeContent = newContent;
            _editDirty = true;
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(!_editDirty);
        if (GUILayout.Button("💾  변경사항 저장", GUILayout.Height(32)))
            SaveEditedNode();
        EditorGUI.EndDisabledGroup();
        if (GUILayout.Button("🔨  빌드 모드에서 열기", GUILayout.Height(32)))
            ImportNodeToBuildMode();
        EditorGUILayout.EndHorizontal();
    }

    void RefreshEditNodes()
    {
        if (_existingYarns.Length == 0) { _editNodeNames = System.Array.Empty<string>(); return; }

        string fullDir  = Path.Combine(Application.dataPath,
            _savePath.StartsWith("Assets/") ? _savePath.Substring(7) : _savePath);
        string filePath = Path.Combine(fullDir, _existingYarns[_editYarnIndex]);
        if (!File.Exists(filePath)) { _editNodeNames = System.Array.Empty<string>(); return; }

        string text = File.ReadAllText(filePath, Encoding.UTF8);
        var nodes   = ParseYarnNodeNames(text);
        _editNodeNames = nodes.ToArray();
        _editNodeIndex = 0;
        _editSourceFile = filePath;
    }

    List<string> ParseYarnNodeNames(string content)
    {
        var names = new List<string>();
        foreach (var seg in content.Split(new string[]{"==="}, System.StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var line in seg.Split('\n'))
            {
                string t = line.Trim();
                if (t.StartsWith("title:"))
                {
                    names.Add(t.Substring("title:".Length).Trim());
                    break;
                }
            }
        }
        return names;
    }

    void LoadSelectedNode()
    {
        if (!File.Exists(_editSourceFile)) return;
        string text    = File.ReadAllText(_editSourceFile, Encoding.UTF8);
        string target  = _editNodeIndex < _editNodeNames.Length ? _editNodeNames[_editNodeIndex] : "";
        if (string.IsNullOrEmpty(target)) return;

        foreach (var seg in text.Split(new string[]{"==="}, System.StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = seg.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            bool found = false;
            foreach (var line in trimmed.Split('\n'))
            {
                string t = line.Trim();
                if (t.StartsWith("title:") && t.Substring("title:".Length).Trim() == target)
                { found = true; break; }
            }

            if (found)
            {
                _editNodeContent = trimmed + "\n===";
                _editDirty = false;
                return;
            }
        }
    }

    void SaveEditedNode()
    {
        if (!File.Exists(_editSourceFile)) return;
        string target = _editNodeIndex < _editNodeNames.Length ? _editNodeNames[_editNodeIndex] : "";
        if (string.IsNullOrEmpty(target)) return;

        string fileText = File.ReadAllText(_editSourceFile, Encoding.UTF8).Replace("\r\n", "\n");
        var segments    = fileText.Split(new string[]{"==="}, System.StringSplitOptions.None);

        for (int i = 0; i < segments.Length; i++)
        {
            string trimmed = segments[i].Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;
            bool found = false;
            foreach (var line in trimmed.Split('\n'))
            {
                string t = line.Trim();
                if (t.StartsWith("title:") && t.Substring("title:".Length).Trim() == target)
                { found = true; break; }
            }
            if (found)
            {
                // _editNodeContent = "title: ...\n---\n...\n===" 형태
                string normalizedEdit = _editNodeContent.Replace("\r\n", "\n");
        string newSeg = normalizedEdit.EndsWith("===")
                    ? normalizedEdit.Substring(0, normalizedEdit.Length - 3).TrimEnd()
                    : normalizedEdit.TrimEnd();
                segments[i] = "\n" + newSeg + "\n";
                break;
            }
        }

        string result = string.Join("===", segments).Trim();
        File.WriteAllText(_editSourceFile, result, Encoding.UTF8);
        AssetDatabase.Refresh();
        _editDirty = false;
        EditorUtility.DisplayDialog("저장 완료", $"'{target}' 노드가 저장됐어요!", "확인");
    }

    void ImportNodeToBuildMode()
    {
        if (string.IsNullOrEmpty(_editNodeContent)) return;

        // 타이틀 추출
        string title = "";
        foreach (var line in _editNodeContent.Split('\n'))
        {
            string t = line.Trim();
            if (t.StartsWith("title:")) { title = t.Substring("title:".Length).Trim(); break; }
        }

        string body   = ExtractNodeBody(_editNodeContent);
        var    parsed = ParseBodyToLines(body);

        if (!string.IsNullOrEmpty(title)) _nodeName = title;
        _importedFromFile = _editSourceFile;
        _importedNodeName = title;
        _pending.Enqueue(() =>
        {
            _lines.Clear();
            foreach (var l in parsed) _lines.Add(l);
        });
        _editMode = false;
    }

    string ExtractNodeBody(string nodeContent)
    {
        int dashIdx = nodeContent.IndexOf("\n---");
        if (dashIdx < 0) return "";
        string body = nodeContent.Substring(dashIdx + 4);
        if (body.StartsWith("\n")) body = body.Substring(1);
        if (body.TrimEnd().EndsWith("==="))
            body = body.TrimEnd().Substring(0, body.TrimEnd().Length - 3).TrimEnd();
        return body;
    }

    List<Line> ParseBodyToLines(string body)
    {
        var result   = new List<Line>();
        var rawLines = body.Split('\n');
        int i = 0;

        while (i < rawLines.Length)
        {
            string raw     = rawLines[i].TrimEnd();
            string trimmed = raw.Trim();

            if (string.IsNullOrWhiteSpace(trimmed)) { i++; continue; }

            // if/elseif/else/endif 블록
            if (trimmed.StartsWith("<<if "))
            {
                var block = new StringBuilder();
                int depth = 1;
                block.AppendLine(raw);
                i++;
                while (i < rawLines.Length && depth > 0)
                {
                    string l  = rawLines[i].TrimEnd();
                    string lt = l.Trim();
                    if (lt.StartsWith("<<if ")) depth++;
                    if (lt == "<<endif>>") depth--;
                    block.AppendLine(l);
                    i++;
                }
                string code  = block.ToString().TrimEnd();
                string badge = code.Contains("showSprite") ? "표정" : "분기";
                result.Add(new Line { badge = badge, display = badge == "표정" ? "[구간별 표정]" : "[분기]", code = code });
                continue;
            }

            // 선택지 블록 (-> 로 시작하는 연속 라인)
            if (trimmed.StartsWith("-> "))
            {
                var block = new StringBuilder();
                int count = 0;
                while (i < rawLines.Length)
                {
                    string l  = rawLines[i].TrimEnd();
                    string lt = l.Trim();
                    if (lt.StartsWith("-> "))                                    { count++; block.AppendLine(l); i++; }
                    else if (l.Length > 0 && char.IsWhiteSpace(l[0]) && lt.StartsWith("<<jump")) { block.AppendLine(l); i++; }
                    else break;
                }
                result.Add(new Line { badge = "선택지", display = $"선택지 {count}개", code = block.ToString().TrimEnd() });
                continue;
            }

            // 단일 커맨드 <<...>>
            if (trimmed.StartsWith("<<") && trimmed.EndsWith(">>"))
            {
                string badge, display;
                if      (trimmed.StartsWith("<<showSprite") || trimmed.StartsWith("<<hideSprite"))
                { badge = "표정";   display = trimmed; }
                else if (trimmed.StartsWith("<<jump "))
                { badge = "이동";   display = "이동: " + trimmed.Substring(7).Replace(">>", "").Trim(); }
                else if (trimmed.StartsWith("<<applyTrigger"))
                { badge = "트리거"; display = trimmed; }
                else if (trimmed.StartsWith("<<cam_") || trimmed.StartsWith("<<camera_"))
                { badge = "카메라"; display = trimmed; }
                else
                { badge = "기타";   display = trimmed; }
                result.Add(new Line { badge = badge, display = display, code = raw });
                i++; continue;
            }

            // 라디오 대사
            if (trimmed.Contains("(라디오):"))
            {
                result.Add(new Line { badge = "라디오", display = trimmed, code = raw });
                i++; continue;
            }

            // 일반 대사 / 나레이션
            result.Add(new Line { badge = "대사", display = trimmed, code = raw });
            i++;
        }

        return result;
    }
}