using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// 주인공 이름 입력 화면 — DontDestroyOnLoad 자동 생성 싱글톤.
/// Inspector 연결 없이 NameEntryUI.Show(onConfirmed) 만으로 사용한다.
/// (SettingsPanelUI 와 같은 코드 생성 방식 — 유니티 에디터 배치 작업이 필요 없다.)
///
/// 흐름: 이름 입력 ▸ 금지 이름이면 거부 대사 후 재입력 ▸ 통과하면 재확인 ▸ 확정
/// </summary>
public class NameEntryUI : MonoBehaviour
{
    static NameEntryUI _instance;

    public static bool IsOpen { get; private set; }

    // ── 색상 (SettingsPanelUI 톤에 맞춤) ───────────────────────────────────
    static readonly Color PanelBg   = new Color(0.10f, 0.10f, 0.12f, 1f);
    static readonly Color FieldBg   = new Color(0.16f, 0.16f, 0.19f, 1f);
    static readonly Color BtnCol    = new Color(0.22f, 0.22f, 0.22f, 1f);
    static readonly Color AccentCol = new Color(0.30f, 0.55f, 0.90f, 1f);
    static readonly Color RefuseCol = new Color(0.85f, 0.78f, 0.62f, 1f);
    static readonly Color HintCol   = new Color(0.60f, 0.60f, 0.64f, 1f);

    // ── 내부 상태 ─────────────────────────────────────────────────────────
    CanvasGroup     _cg;
    Image           _panel;           // 가운데 패널 (검은 배경과 별개로 껐다 켠다)
    GameObject      _inputStage;      // 1단계: 이름 입력
    GameObject      _confirmStage;    // 2단계: 재확인
    TMP_InputField  _field;
    TMP_Text        _messageText;     // 거부 대사 / 안내
    TMP_Text        _confirmText;     // "○○(이)가 맞나요?"
    Coroutine       _messageRoutine;
    Coroutine       _safetyRoutine;   // 씬 전환 실패 시 검은 화면 탈출용
    string          _pendingName;     // 재확인 대기 중인 이름
    System.Action   _onConfirmed;

    // ─── 공개 API ─────────────────────────────────────────────────────────
    /// <summary>이름 입력 화면을 띄운다. 확정되면 onConfirmed 를 호출한다.</summary>
    public static void Show(System.Action onConfirmed)
    {
        if (_instance == null) CreateInstance();
        if (IsOpen) return;

        IsOpen = true;
        _instance._onConfirmed = onConfirmed;
        _instance.gameObject.SetActive(true);
        _instance.EnsureEventSystem();
        _instance.ResetToInputStage();
        _instance._cg.alpha          = 1f;
        _instance._cg.blocksRaycasts = true;
    }

    public static void Hide()
    {
        if (!IsOpen || _instance == null) return;
        IsOpen = false;
        // 외부에서 직접 Hide 했을 때도 구독이 새지 않도록 정리한다
        _instance.StopSafetyRoutine();
        SceneManager.sceneLoaded    -= _instance.HideAfterSceneLoad;
        _instance._cg.alpha          = 0f;
        _instance._cg.blocksRaycasts = false;
        _instance.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= HideAfterSceneLoad;
        if (_instance == this) _instance = null;
    }

    // ─── 자동 생성 ────────────────────────────────────────────────────────
    static void CreateInstance()
    {
        var root = new GameObject("NameEntryUI [Auto]");
        DontDestroyOnLoad(root);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 97;   // 설정(99)·힌트(94) 사이. 타이틀 UI 위를 덮는다.

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;
        root.AddComponent<GraphicRaycaster>();

        _instance     = root.AddComponent<NameEntryUI>();
        _instance._cg = root.AddComponent<CanvasGroup>();
        _instance._cg.alpha          = 0f;
        _instance._cg.blocksRaycasts = false;
        root.SetActive(false);

        _instance.BuildUI(root.transform);
    }

    /// <summary>버튼 클릭·입력을 받으려면 EventSystem 이 필요하다. 씬에 없으면 만들어 준다.</summary>
    void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;

        var es = new GameObject("EventSystem [Auto]");
        DontDestroyOnLoad(es);
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }

    // ─── UI 빌드 ──────────────────────────────────────────────────────────
    void BuildUI(Transform root)
    {
        // 전체를 덮는 검은 배경. 타이틀 화면을 완전히 가려 이름 정하는 순간에만 집중시킨다.
        // (클릭으로 닫히지 않는다 — 반드시 이름을 정해야 한다)
        var bg = MakeImage(root, "Background", Color.black);
        Stretch(bg.rectTransform);

        _panel = MakeImage(root, "Panel", PanelBg);
        var pr = _panel.rectTransform;
        pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
        pr.sizeDelta        = new Vector2(720f, 420f);
        pr.anchoredPosition = Vector2.zero;

        BuildInputStage(_panel.transform);
        BuildConfirmStage(_panel.transform);
    }

    // 1단계 — 이름 입력
    void BuildInputStage(Transform panel)
    {
        _inputStage = new GameObject("InputStage");
        var stageRt = _inputStage.AddComponent<RectTransform>();
        stageRt.SetParent(panel, false);
        Stretch(stageRt);

        var t = _inputStage.transform;

        var title = MakeText(t, "이름을 정해주세요", 34, new Vector2(0f, 140f), new Vector2(640f, 50f));
        title.fontStyle = FontStyles.Bold;

        _field = MakeInputField(t, new Vector2(0f, 40f), new Vector2(420f, 70f));

        // 거부 대사 / 안내가 나오는 자리
        _messageText = MakeText(t, "", 20, new Vector2(0f, -30f), new Vector2(640f, 60f));
        _messageText.color = RefuseCol;

        var hint = MakeText(t, $"{PlayerIdentity.MaxLength}글자까지 · Enter 로 확인", 16,
                            new Vector2(0f, -80f), new Vector2(640f, 30f));
        hint.color = HintCol;

        MakeButton(t, "확인",   new Vector2(-110f, -145f), new Vector2(180f, 54f), SubmitName,  AccentCol);
        MakeButton(t, "기본값", new Vector2( 110f, -145f), new Vector2(180f, 54f), UseDefault,  BtnCol);
    }

    // 2단계 — 재확인
    void BuildConfirmStage(Transform panel)
    {
        _confirmStage = new GameObject("ConfirmStage");
        var stageRt = _confirmStage.AddComponent<RectTransform>();
        stageRt.SetParent(panel, false);
        Stretch(stageRt);

        var t = _confirmStage.transform;

        _confirmText = MakeText(t, "", 32, new Vector2(0f, 60f), new Vector2(660f, 120f));
        _confirmText.fontStyle = FontStyles.Bold;

        MakeButton(t, "네",    new Vector2(-110f, -90f), new Vector2(180f, 54f), AcceptName,       AccentCol);
        MakeButton(t, "아니요", new Vector2( 110f, -90f), new Vector2(180f, 54f), BackToInput,      BtnCol);

        _confirmStage.SetActive(false);
    }

    // ─── 단계 전환 ────────────────────────────────────────────────────────
    void ResetToInputStage()
    {
        _pendingName = null;
        _panel.gameObject.SetActive(true);   // AcceptName 에서 꺼둔 상태로 다시 열릴 수 있다
        _inputStage.SetActive(true);
        _confirmStage.SetActive(false);
        _field.text = "";
        ClearMessage();
        FocusField();
    }

    void BackToInput()
    {
        _pendingName = null;
        _inputStage.SetActive(true);
        _confirmStage.SetActive(false);
        FocusField();
    }

    void FocusField()
    {
        if (_field == null) return;
        StartCoroutine(FocusFieldNextFrame());
    }

    // 오브젝트를 막 켠 프레임에 ActivateInputField를 부르면 포커스가 잡히지 않는 경우가 있다
    IEnumerator FocusFieldNextFrame()
    {
        yield return null;
        if (_field == null) yield break;
        _field.Select();
        _field.ActivateInputField();
        _field.caretPosition = _field.text.Length;
    }

    /// <summary>
    /// 조합 중인 한글을 확정시킨다.
    /// IME 조합 중에 Enter 를 누르면 마지막 글자가 _field.text 에 아직 안 들어와 있다
    /// ("민준" 을 쳤는데 "민" 만 읽히는 문제). 포커스를 떼면 조합이 확정된다.
    /// </summary>
    void CommitComposition()
    {
        if (_field != null && _field.isFocused)
            _field.DeactivateInputField();
    }

    // ─── 동작 ─────────────────────────────────────────────────────────────
    void UseDefault()
    {
        _field.text = PlayerIdentity.DefaultName;
        SubmitName();
    }

    void SubmitName()
    {
        CommitComposition();
        NameVerdict verdict = PlayerIdentity.Check(_field.text);

        if (!verdict.CanProceed)
        {
            ShowMessage(verdict.line);
            FocusField();
            return;
        }

        _pendingName = verdict.name;

        // 통과지만 한마디 하는 이름(이스터에그)은 그 대사를 재확인 화면에 함께 띄운다
        string question = $"{KoreanParticle.Attach(_pendingName, "가")} 맞나요?";
        _confirmText.text = verdict.judgement == NameJudgement.AllowedWithLine
            ? $"{verdict.line}\n\n{question}"
            : question;

        _inputStage.SetActive(false);
        _confirmStage.SetActive(true);
    }

    void AcceptName()
    {
        PlayerIdentity.Set(_pendingName);

        var callback = _onConfirmed;
        _onConfirmed = null;

        // 패널만 감추고 **검은 배경은 그대로 둔다.**
        // 여기서 전부 꺼버리면 TransitionManager 가 자기 오버레이를 페이드인하는 동안
        // (SceneTransitionRoutine 은 페이드가 끝난 뒤에야 씬을 로드한다)
        // 뒤에 있던 타이틀 화면이 그대로 드러난다.
        // 전환 오버레이는 sortingOrder 999 라 우리(97) 위를 덮으므로, 둘 다 검정이라 이음매가 없다.
        _panel.gameObject.SetActive(false);
        _cg.blocksRaycasts = false;

        SceneManager.sceneLoaded += HideAfterSceneLoad;
        _safetyRoutine = StartCoroutine(HideIfSceneNeverLoads());

        callback?.Invoke();
    }

    void HideAfterSceneLoad(Scene scene, LoadSceneMode mode)
    {
        // 새 씬이 올라온 시점엔 전환 오버레이가 아직 완전히 검다. 지금 꺼야 이음매가 안 보인다.
        StopSafetyRoutine();
        SceneManager.sceneLoaded -= HideAfterSceneLoad;
        Hide();
    }

    /// <summary>전환이 어떤 이유로든 일어나지 않았을 때 검은 화면에 갇히지 않도록 하는 안전장치.</summary>
    IEnumerator HideIfSceneNeverLoads()
    {
        yield return new WaitForSecondsRealtime(10f);
        Debug.LogWarning("[NameEntryUI] 씬 전환이 감지되지 않아 입력 화면을 강제로 닫습니다.");
        _safetyRoutine = null;
        SceneManager.sceneLoaded -= HideAfterSceneLoad;
        Hide();
    }

    void StopSafetyRoutine()
    {
        if (_safetyRoutine == null) return;
        StopCoroutine(_safetyRoutine);
        _safetyRoutine = null;
    }

    // ─── 메시지 표시 ──────────────────────────────────────────────────────
    void ShowMessage(string text)
    {
        if (_messageRoutine != null) StopCoroutine(_messageRoutine);
        _messageRoutine = StartCoroutine(FadeInMessage(text));
    }

    void ClearMessage()
    {
        if (_messageRoutine != null) { StopCoroutine(_messageRoutine); _messageRoutine = null; }
        if (_messageText != null) _messageText.text = "";
    }

    IEnumerator FadeInMessage(string text)
    {
        _messageText.text = text ?? "";

        const float duration = 0.25f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float a  = Mathf.Clamp01(elapsed / duration);
            _messageText.color = new Color(RefuseCol.r, RefuseCol.g, RefuseCol.b, a);
            yield return null;
        }
        _messageText.color = RefuseCol;
        _messageRoutine = null;
    }

    // ─── 키보드 ───────────────────────────────────────────────────────────
    void Update()
    {
        if (!IsOpen) return;

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (_confirmStage.activeSelf) AcceptName();
            else                          SubmitName();
        }
        else if (Input.GetKeyDown(KeyCode.Escape) && _confirmStage.activeSelf)
        {
            BackToInput();
        }
    }

    // ─── UI 헬퍼 (SettingsPanelUI 와 같은 구성) ─────────────────────────────
    static Image MakeImage(Transform p, string name, Color col)
    {
        var go  = new GameObject(name); go.transform.SetParent(p, false);
        var img = go.AddComponent<Image>(); img.color = col;
        return img;
    }

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    static TMP_Text MakeText(Transform p, string text, int size, Vector2 pos, Vector2 sz)
    {
        var go  = new GameObject("Txt"); go.transform.SetParent(p, false);
        var txt = go.AddComponent<TextMeshProUGUI>();
        txt.text      = text;
        txt.fontSize  = size;
        txt.color     = Color.white;
        txt.alignment = TextAlignmentOptions.Center;
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos; r.sizeDelta = sz;
        return txt;
    }

    static Button MakeButton(Transform p, string label, Vector2 pos, Vector2 sz,
                             System.Action onClick, Color col)
    {
        var go  = new GameObject("Btn_" + label); go.transform.SetParent(p, false);
        var img = go.AddComponent<Image>(); img.color = col;
        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick());
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos; r.sizeDelta = sz;

        var lt = MakeText(go.transform, label, 20, Vector2.zero, sz);
        Stretch(lt.GetComponent<RectTransform>());
        return btn;
    }

    /// <summary>
    /// TMP_InputField 를 코드로 조립한다.
    /// Viewport(RectMask2D) ▸ Text / Placeholder 구조를 직접 만들고 슬롯을 연결해야 동작한다.
    /// </summary>
    static TMP_InputField MakeInputField(Transform p, Vector2 pos, Vector2 sz)
    {
        var go  = new GameObject("NameInput"); go.transform.SetParent(p, false);
        var img = go.AddComponent<Image>(); img.color = FieldBg;
        var r   = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos; r.sizeDelta = sz;

        // 텍스트가 잘려 보이도록 마스킹되는 영역
        var viewport = new GameObject("TextArea"); viewport.transform.SetParent(go.transform, false);
        var vpRt = viewport.AddComponent<RectTransform>();
        vpRt.anchorMin = Vector2.zero; vpRt.anchorMax = Vector2.one;
        vpRt.offsetMin = new Vector2(14f, 8f);
        vpRt.offsetMax = new Vector2(-14f, -8f);
        viewport.AddComponent<RectMask2D>();

        var placeholder = MakeText(viewport.transform, PlayerIdentity.DefaultName, 30, Vector2.zero, Vector2.zero);
        placeholder.color     = new Color(1f, 1f, 1f, 0.35f);
        placeholder.fontStyle = FontStyles.Italic;
        Stretch(placeholder.GetComponent<RectTransform>());

        var textComp = MakeText(viewport.transform, "", 30, Vector2.zero, Vector2.zero);
        textComp.richText = false;   // 플레이어 입력이 태그로 해석되지 않게 한다
        Stretch(textComp.GetComponent<RectTransform>());

        var field = go.AddComponent<TMP_InputField>();
        field.targetGraphic  = img;
        field.textViewport   = vpRt;
        field.textComponent  = textComp;
        field.placeholder    = placeholder;
        field.characterLimit = PlayerIdentity.MaxLength;
        field.lineType       = TMP_InputField.LineType.SingleLine;
        field.richText       = false;
        field.caretWidth     = 2;
        field.customCaretColor = true;
        field.caretColor     = Color.white;

        return field;
    }
}
