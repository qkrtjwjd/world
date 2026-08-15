using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 대화 로그(백로그) 패널 — DontDestroyOnLoad 자동 생성 싱글톤.
/// Inspector 연결 없이 L키(리바인딩: SettingsManager.keyDialogueLog)로 열고 닫는다.
///
/// - 기록은 <see cref="DialogueLogRecorder"/>가 담당, 이 클래스는 열람 UI만
/// - showDialogueLog 설정이 false면 열기 비활성
/// - ESC 닫기는 PauseSystem이 담당 (같은 프레임 이중 처리 방지)
/// </summary>
public class DialogueLogUI : MonoBehaviour
{
    public static DialogueLogUI Instance
    {
        get
        {
            if (_instance == null) CreateInstance();
            return _instance;
        }
    }
    static DialogueLogUI _instance;

    public static bool IsOpen { get; private set; }

    static readonly Color PanelBg    = new Color(0.10f, 0.10f, 0.12f, 1f);
    static readonly Color SectionCol = new Color(0.55f, 0.75f, 1.00f, 1f);
    static readonly Color DangerCol  = new Color(0.75f, 0.20f, 0.20f, 1f);

    // ── 내부 상태 ──────────────────────────────────────────────────────────
    GameObject    _panel;       // Dimmer + 패널 (토글 대상). 루트는 키 폴링을 위해 항상 활성
    RectTransform _contentRt;
    ScrollRect    _scrollRect;

    // 게임 시작 시 1회 생성 — 루트가 항상 활성이어야 Update 폴링이 보장됨
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        var _ = Instance;
    }

    // ─── 자동 생성 ────────────────────────────────────────────────────────
    static void CreateInstance()
    {
        var root = new GameObject("DialogueLogUI [Auto]");
        DontDestroyOnLoad(root);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 98;   // 설정 패널(99) 아래, 테두리 효과(95) 위
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;
        root.AddComponent<GraphicRaycaster>();

        _instance = root.AddComponent<DialogueLogUI>();
        _instance.BuildUI(root.transform);
    }

    // ─── 공개 API ─────────────────────────────────────────────────────────
    public static void Show()
    {
        var inst = Instance;
        if (IsOpen) return;
        IsOpen = true;
        inst._panel.SetActive(true);
        inst.RebuildContent();
    }

    public static void Hide()
    {
        if (!IsOpen || _instance == null) return;
        IsOpen = false;
        _instance._panel.SetActive(false);
    }

    // ─── 입력 ─────────────────────────────────────────────────────────────
    void Update()
    {
        var key = SettingsManager.Instance?.keyDialogueLog ?? KeyCode.L;
        if (!Input.GetKeyDown(key)) return;

        if (IsOpen)
        {
            Hide();
        }
        else if ((SettingsManager.Instance?.showDialogueLog ?? true) && !SettingsPanelUI.IsOpen)
        {
            Show();
        }
    }

    // ─── UI 빌드 ──────────────────────────────────────────────────────────
    void BuildUI(Transform root)
    {
        _panel = new GameObject("LogPanel");
        _panel.transform.SetParent(root, false);
        var panelRt = _panel.AddComponent<RectTransform>();
        Stretch(panelRt);

        // 어두운 배경 (클릭으로 닫기)
        var bg = MakeImage(_panel.transform, "Dimmer", new Color(0f, 0f, 0f, 0.78f));
        Stretch(bg.rectTransform);
        var bgBtn = bg.gameObject.AddComponent<Button>();
        bgBtn.targetGraphic = bg;
        bgBtn.transition = Selectable.Transition.None;
        bgBtn.onClick.AddListener(Hide);

        // 중앙 패널
        var panel = MakeImage(_panel.transform, "Panel", PanelBg);
        var pr = panel.rectTransform;
        pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
        pr.sizeDelta = new Vector2(680f, 580f);
        pr.anchoredPosition = Vector2.zero;
        var pt = panel.transform;

        // 제목
        var title = MakeText(pt, "대화 로그", 36, new Vector2(0f, 255f), new Vector2(600f, 50f));
        title.fontStyle = FontStyles.Bold;

        // 구분선
        var line = MakeImage(pt, "Divider", new Color(0.30f, 0.30f, 0.35f, 1f));
        line.rectTransform.anchoredPosition = new Vector2(0f, 225f);
        line.rectTransform.sizeDelta        = new Vector2(640f, 1f);

        // 스크롤 영역
        var scrollGo = new GameObject("ScrollArea");
        scrollGo.transform.SetParent(pt, false);
        _scrollRect = scrollGo.AddComponent<ScrollRect>();
        var scrollRt = scrollGo.GetComponent<RectTransform>();
        scrollRt.anchoredPosition = new Vector2(0f, -12f);
        scrollRt.sizeDelta        = new Vector2(640f, 430f);

        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollGo.transform, false);
        var vpMask = viewport.AddComponent<Mask>();
        vpMask.showMaskGraphic = false;
        viewport.AddComponent<Image>().color = Color.clear;
        var vpRt = viewport.GetComponent<RectTransform>();
        Stretch(vpRt);

        var content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        _contentRt = content.AddComponent<RectTransform>();
        _contentRt.anchorMin = new Vector2(0f, 1f);
        _contentRt.anchorMax = new Vector2(1f, 1f);
        _contentRt.pivot     = new Vector2(0.5f, 1f);
        _contentRt.sizeDelta = new Vector2(0f, 430f);
        _contentRt.anchoredPosition = Vector2.zero;

        _scrollRect.content           = _contentRt;
        _scrollRect.viewport          = vpRt;
        _scrollRect.horizontal        = false;
        _scrollRect.vertical          = true;
        _scrollRect.scrollSensitivity = 30f;
        _scrollRect.movementType      = ScrollRect.MovementType.Clamped;

        // 하단 구분선 + 닫기 버튼
        var line2 = MakeImage(pt, "Divider2", new Color(0.30f, 0.30f, 0.35f, 1f));
        line2.rectTransform.anchoredPosition = new Vector2(0f, -231f);
        line2.rectTransform.sizeDelta        = new Vector2(640f, 1f);

        var closeBtn = MakeButton(pt, "닫기", new Vector2(0f, -257f), new Vector2(160f, 40f), Hide);
        closeBtn.GetComponent<Image>().color = DangerCol;

        _panel.SetActive(false);
    }

    // ─── 로그 목록 재빌드 ─────────────────────────────────────────────────
    void RebuildContent()
    {
        // 기존 행 전부 파괴 후 현재 기록으로 다시 생성 (열 때마다 1회)
        for (int i = _contentRt.childCount - 1; i >= 0; i--)
            Destroy(_contentRt.GetChild(i).gameObject);

        var entries = DialogueLogRecorder.Entries;

        if (entries.Count == 0)
        {
            var empty = MakeText(_contentRt, "아직 대화 기록이 없습니다.", 16,
                new Vector2(0f, -40f), new Vector2(600f, 30f));
            empty.color = new Color(0.70f, 0.70f, 0.70f, 1f);
            SetTopAnchored(empty.rectTransform, 0f, -40f, 600f, 30f);
            _contentRt.sizeDelta = new Vector2(0f, 430f);
            return;
        }

        const float rowWidth  = 600f;
        const float padTop    = 12f;
        const float speakerH  = 24f;
        const float rowGap    = 14f;

        float y = -padTop;

        foreach (var e in entries)
        {
            if (!string.IsNullOrEmpty(e.speaker))
            {
                var speaker = MakeText(_contentRt, e.speaker, 15, Vector2.zero, Vector2.zero);
                speaker.fontStyle = FontStyles.Bold;
                speaker.color     = SectionCol;
                speaker.alignment = TextAlignmentOptions.MidlineLeft;
                SetTopAnchored(speaker.rectTransform, 0f, y, rowWidth, speakerH);
                y -= speakerH;
            }

            var body = MakeText(_contentRt, e.text, 15, Vector2.zero, Vector2.zero);
            body.alignment = TextAlignmentOptions.TopLeft;
            float bodyH = Mathf.Max(22f, body.GetPreferredValues(e.text, rowWidth, 0f).y);
            SetTopAnchored(body.rectTransform, 0f, y, rowWidth, bodyH);
            y -= bodyH + rowGap;
        }

        _contentRt.sizeDelta = new Vector2(0f, Mathf.Max(430f, -y + padTop));

        // 레이아웃 반영 후 최신 대사가 보이도록 최하단으로 스크롤
        Canvas.ForceUpdateCanvases();
        _scrollRect.verticalNormalizedPosition = 0f;
    }

    // ─── 기본 UI 헬퍼 (SettingsPanelUI 패턴 복제) ─────────────────────────
    static void SetTopAnchored(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

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
        txt.raycastTarget = false;
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos; r.sizeDelta = sz;
        return txt;
    }

    static Button MakeButton(Transform p, string label, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject("Btn_" + label); go.transform.SetParent(p, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.22f, 0.22f, 0.22f, 1f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;

        var txt = MakeText(go.transform, label, 18, Vector2.zero, size);
        Stretch(txt.rectTransform);
        return btn;
    }
}
