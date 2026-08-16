using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 저널(목표 일지) 패널 — 코드 생성 UI (DialogueLogUI 패턴, 에디터 배선 불필요).
/// 상단에 진행 중 목표(강조), 하단에 완료 목표 목록(회색·취소선)을 표시한다.
/// 열기는 PauseSystem.OpenJournal() 폴백 경유, ESC 닫기는 PauseSystem이 담당.
/// 데이터는 <see cref="JournalManager"/>가 보관한다.
/// </summary>
public class JournalUI : MonoBehaviour
{
    public static JournalUI Instance
    {
        get
        {
            if (_instance == null) CreateInstance();
            return _instance;
        }
    }
    static JournalUI _instance;

    public static bool IsOpen { get; private set; }

    static readonly Color PanelBg     = new Color(0.10f, 0.10f, 0.12f, 1f);
    static readonly Color ActiveCol   = new Color(1.00f, 0.85f, 0.55f, 1f);
    static readonly Color SectionCol  = new Color(0.55f, 0.75f, 1.00f, 1f);
    static readonly Color DoneCol     = new Color(0.55f, 0.55f, 0.58f, 1f);
    static readonly Color DangerCol   = new Color(0.75f, 0.20f, 0.20f, 1f);

    GameObject    _panel;
    RectTransform _contentRt;
    ScrollRect    _scrollRect;

    // ─── 자동 생성 ────────────────────────────────────────────────────────
    static void CreateInstance()
    {
        var root = new GameObject("JournalUI [Auto]");
        DontDestroyOnLoad(root);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 98;   // DialogueLogUI 와 동일 계층 (동시에 열리지 않음)
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;
        root.AddComponent<GraphicRaycaster>();

        _instance = root.AddComponent<JournalUI>();
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

    // ─── UI 빌드 (DialogueLogUI 패턴) ─────────────────────────────────────
    void BuildUI(Transform root)
    {
        _panel = new GameObject("JournalPanel");
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
        var title = MakeText(pt, "일지", 36, new Vector2(0f, 255f), new Vector2(600f, 50f));
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

        // ⚠ Mask 를 쓰면 안 된다. Mask 는 그래픽의 알파로 스텐실을 쓰는데, 여기 그래픽은
        // 색이 Color.clear(알파 0)라 스텐실이 하나도 안 써지고 → 자식이 전부 스텐실 테스트에
        // 걸려 화면에서 사라진다. RectMask2D 는 사각형으로 자르므로 그래픽이 필요 없다.
        // (Image 는 ScrollRect 드래그 레이캐스트용으로 남긴다.)
        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollGo.transform, false);
        viewport.AddComponent<RectMask2D>();
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

    // ─── 목록 재빌드 ──────────────────────────────────────────────────────
    void RebuildContent()
    {
        for (int i = _contentRt.childCount - 1; i >= 0; i--)
            Destroy(_contentRt.GetChild(i).gameObject);

        var entries = JournalManager.Entries;

        if (entries.Count == 0)
        {
            var empty = MakeText(_contentRt, "아직 기록된 목표가 없습니다.", 16,
                new Vector2(0f, -40f), new Vector2(600f, 30f));
            empty.color = new Color(0.70f, 0.70f, 0.70f, 1f);
            SetTopAnchored(empty.rectTransform, 0f, -40f, 600f, 30f);
            _contentRt.sizeDelta = new Vector2(0f, 430f);
            return;
        }

        const float rowWidth = 600f;
        const float padTop   = 12f;
        const float headerH  = 26f;
        const float rowGap   = 14f;

        float y = -padTop;

        // ── 진행 중 목표 (최신 활성 1개) ──
        var current = JournalManager.CurrentEntry;
        if (current != null)
        {
            var section = MakeText(_contentRt, "◈ 진행 중", 17, Vector2.zero, Vector2.zero);
            section.fontStyle = FontStyles.Bold;
            section.color     = SectionCol;
            section.alignment = TextAlignmentOptions.MidlineLeft;
            SetTopAnchored(section.rectTransform, 0f, y, rowWidth, headerH);
            y -= headerH + 4f;

            y = AddEntryRow(current, y, rowWidth, active: true) - rowGap;
        }

        // ── 완료 목표 (최신순) ──
        bool hasDone = false;
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            var e = entries[i];
            if (!e.isCompleted) continue;

            if (!hasDone)
            {
                hasDone = true;
                var section = MakeText(_contentRt, "◈ 완료", 17, Vector2.zero, Vector2.zero);
                section.fontStyle = FontStyles.Bold;
                section.color     = SectionCol;
                section.alignment = TextAlignmentOptions.MidlineLeft;
                SetTopAnchored(section.rectTransform, 0f, y, rowWidth, headerH);
                y -= headerH + 4f;
            }
            y = AddEntryRow(e, y, rowWidth, active: false) - rowGap;
        }

        _contentRt.sizeDelta = new Vector2(0f, Mathf.Max(430f, -y + padTop));
        Canvas.ForceUpdateCanvases();
        _scrollRect.verticalNormalizedPosition = 1f; // 진행 중 목표가 위 — 최상단 표시
    }

    /// <returns>다음 행이 시작할 y 좌표.</returns>
    float AddEntryRow(JournalManager.JournalEntry e, float y, float rowWidth, bool active)
    {
        const float headerH = 24f;

        if (!string.IsNullOrEmpty(e.header))
        {
            var header = MakeText(_contentRt, e.header, 15, Vector2.zero, Vector2.zero);
            header.fontStyle = FontStyles.Bold;
            header.color     = active ? ActiveCol : DoneCol;
            header.alignment = TextAlignmentOptions.MidlineLeft;
            SetTopAnchored(header.rectTransform, 0f, y, rowWidth, headerH);
            y -= headerH;
        }

        var body = MakeText(_contentRt, e.body, 15, Vector2.zero, Vector2.zero);
        body.alignment = TextAlignmentOptions.TopLeft;
        if (active)
        {
            body.color = Color.white;
        }
        else
        {
            body.color     = DoneCol;
            body.fontStyle = FontStyles.Strikethrough;
        }
        float bodyH = Mathf.Max(22f, body.GetPreferredValues(e.body, rowWidth, 0f).y);
        SetTopAnchored(body.rectTransform, 0f, y, rowWidth, bodyH);
        return y - bodyH;
    }

    // ─── 기본 UI 헬퍼 (DialogueLogUI 패턴 복제) ───────────────────────────
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
