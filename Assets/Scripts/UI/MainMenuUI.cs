using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// ESC 메인 메뉴 — 코드 생성 UI (SettingsPanelUI·JournalUI 패턴, 에디터 배선 불필요).
///
/// F-8-1  ESC 로 열고 시간 정지. 좌우 화살표로 탭 이동, ESC 재입력으로 종료.
///        탭 위치를 저장하지 않고 항상 아이템 탭에서 시작한다.
/// C-16-1 탭 4개 고정: 아이템 · 쪽지 · 설정 · 그만두기
/// F-8-3  배경은 현재 화면을 한 장 캡처해 블러 처리한 것. 열려 있는 동안 갱신하지 않는다.
///        (캡처는 카메라 렌더 기준 — 자세한 이유는 CaptureBackdrop 주석 참조)
///
/// 아이템 탭은 기존 씬 프리팹의 InventoryPanel 을 그대로 띄운다(1단계에서 표시 배선을 복구했다).
/// 설정 탭은 기존 <see cref="SettingsPanelUI"/> 를 그대로 호출한다(7탭 유지).
/// <see cref="PauseSystem"/> 은 걷어내지 않았다 — 씬 프리팹 배선이 많아 회귀 위험이 크다.
/// ESC 진입점만 이쪽으로 옮겼고 기존 패널 참조·버튼 배선은 그대로 살아 있다.
///
/// 캔버스 층위 (기존 배분 94~999 사이의 빈 자리를 쓴다)
///   96  배경(캡처+블러) — 씬 UI(0) 를 덮는다
///   97  InventoryPanel — 여는 동안만 overrideSorting 으로 끌어올린다
///   98  탭 바·탭 내용
///   99  SettingsPanelUI (기존)
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    public enum Tab { Item = 0, Note = 1, Settings = 2, Quit = 3 }

    public static MainMenuUI Instance
    {
        get
        {
            if (_instance == null) CreateInstance();
            return _instance;
        }
    }
    static MainMenuUI _instance;

    public static bool IsOpen { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { _instance = null; IsOpen = false; }

    // ── 색 (SettingsPanelUI 와 같은 팔레트) ────────────────────────────────
    static readonly Color TabActive   = new Color(0.30f, 0.55f, 0.90f, 1f);
    static readonly Color TabInactive = new Color(0.22f, 0.22f, 0.22f, 1f);
    static readonly Color PanelBg     = new Color(0.10f, 0.10f, 0.12f, 1f);
    static readonly Color DimCol      = new Color(0f, 0f, 0f, 0.45f);
    static readonly Color NoteCol     = new Color(0.70f, 0.70f, 0.70f, 1f);

    static readonly string[] TabLabels = { "아이템", "쪽지", "설정", "그만두기" };

    // ── 내부 상태 ──────────────────────────────────────────────────────────
    GameObject   _front;          // 탭 바 + 탭 내용 (sortingOrder 98)
    Image        _backdrop;       // 캡처+블러 배경 (sortingOrder 96)
    Image        _dim;
    Button[]     _tabButtons;
    GameObject[] _tabPanels;
    Tab          _current;

    Texture2D _backdropTex;
    Sprite    _backdropSprite;

    RectTransform _tabBarRect;

    // InventoryPanel 을 임시로 끌어올리며 우리가 바꾼 것 (닫을 때 전부 되돌린다)
    Canvas           _invCanvas;
    GraphicRaycaster _invRaycaster;
    Vector2          _invOffsetMax;
    bool             _invOffsetSaved;

    // ─── 자동 생성 ────────────────────────────────────────────────────────
    static void CreateInstance()
    {
        var root = new GameObject("MainMenuUI [Auto]");
        DontDestroyOnLoad(root);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 96;   // 씬 UI(0) 위, InventoryPanel 끌어올림(97) 아래
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;
        root.AddComponent<GraphicRaycaster>();

        _instance = root.AddComponent<MainMenuUI>();
        _instance.BuildUI(root.transform);
        root.SetActive(false);
    }

    // ─── 진입 가드 ────────────────────────────────────────────────────────
    /// <summary>메뉴를 열 수 있는 상태인지. F-8-1 ※ · F-5-5 ※.</summary>
    public static bool CanOpen()
    {
        if (BattleSystem.IsActive)             return false;   // 전투 중
        if (HackSlashCombatManager.IsActive)   return false;   // 액션 전투 중
        if (YarnDialogue.IsRunning)            return false;   // 대화창 표시 중
        if (SolTradeUI.IsOpen)                 return false;   // 거래창이 취소 키를 직접 처리한다
        if (HouseEscapePressureController.IsActive) return false; // 집 90초 압박 중
        var lockOwner = PlayerInputLock.Instance;              // 컷씬 등 입력 잠금 중
        if (lockOwner != null && lockOwner.IsLocked) return false;
        return true;
    }

    // ─── 공개 API ─────────────────────────────────────────────────────────
    public static void Show()
    {
        if (IsOpen) return;
        if (!CanOpen()) return;

        var inst = Instance;
        IsOpen = true;
        Time.timeScale = 0f;

        inst.gameObject.SetActive(true);

        // F-8-3: 지금 화면을 한 장만 찍는다. 열려 있는 동안 갱신하지 않는다.
        // 카메라를 직접 렌더하므로 프레임 끝을 기다리지 않는다
        // (WaitForEndOfFrame 은 게임 뷰가 그려지지 않는 상황에서 재개되지 않는다).
        inst.CaptureBackdrop();
        inst._backdrop.color = inst._backdropSprite != null ? Color.white : Color.black;
        inst._dim.color      = DimCol;
        inst._front.SetActive(true);

        // 탭 바의 화면 픽셀 위치를 재기 전에 스케일러·레이아웃을 확정시킨다
        Canvas.ForceUpdateCanvases();

        // F-8-1: 탭 위치를 저장하지 않는다. 항상 아이템 탭에서 시작한다.
        inst._current = Tab.Item;
        inst.SwitchTab(Tab.Item, force: true);
    }

    public static void Hide()
    {
        if (!IsOpen || _instance == null) return;
        IsOpen = false;

        _instance.SetTabContent(_instance._current, false);
        SettingsPanelUI.Hide();

        _instance.ReleaseBackdrop();
        _instance._front.SetActive(false);
        _instance.gameObject.SetActive(false);

        Time.timeScale = 1f;
    }

    // ─── 입력 ─────────────────────────────────────────────────────────────
    void Update()
    {
        if (!IsOpen) return;
        if (SettingsPanelUI.IsRebinding) return;   // 키 재설정 중에는 아무 키도 가로채지 않는다

        // 설정 탭에서 설정 패널을 바깥 클릭으로 닫았으면 아이템 탭으로 돌아간다
        if (_current == Tab.Settings && !SettingsPanelUI.IsOpen)
        {
            SwitchTab(Tab.Item);
            return;
        }

        KeyCode pauseKey = SettingsManager.Instance?.keyPause ?? KeyCode.Escape;
        if (Input.GetKeyDown(pauseKey) || Input.GetKeyDown(KeyCode.Backspace))
        {
            Hide();
            return;
        }

        if (Input.GetKeyDown(KeyCode.RightArrow)) SwitchTab(Next(_current, +1));
        else if (Input.GetKeyDown(KeyCode.LeftArrow)) SwitchTab(Next(_current, -1));
    }

    static Tab Next(Tab t, int dir)
    {
        int n = TabLabels.Length;
        return (Tab)(((int)t + dir + n) % n);
    }

    // ─── 탭 전환 ──────────────────────────────────────────────────────────
    void SwitchTab(Tab tab, bool force = false)
    {
        if (!force && tab == _current) return;

        SetTabContent(_current, false);
        _current = tab;
        SetTabContent(_current, true);

        for (int i = 0; i < _tabButtons.Length; i++)
        {
            var img = _tabButtons[i].targetGraphic as Image;
            if (img != null) img.color = (i == (int)tab) ? TabActive : TabInactive;
            if (_tabPanels[i] != null) _tabPanels[i].SetActive(i == (int)tab);
        }

        // 상하 항목 선택은 EventSystem 기본 내비게이션에 맡긴다
        var first = _tabPanels[(int)tab] != null
            ? _tabPanels[(int)tab].GetComponentInChildren<Selectable>(false)
            : null;
        if (first != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(first.gameObject);
    }

    /// <summary>탭 고유의 외부 UI(아이템창·설정 패널)를 켜고 끈다.</summary>
    void SetTabContent(Tab tab, bool on)
    {
        switch (tab)
        {
            case Tab.Item:     SetInventoryShown(on); break;
            case Tab.Settings:
                if (on) SettingsPanelUI.Show();
                else    SettingsPanelUI.Hide();
                break;
        }
    }

    // ─── 아이템 탭 = 기존 InventoryPanel 재사용 ───────────────────────────
    void SetInventoryShown(bool on)
    {
        var inv = InventoryManager.Instance;
        if (inv == null || inv.inventoryPanel == null) return;   // 씬에 없으면 빈 상태가 정상

        if (on)
        {
            inv.Open();
            inv.UpdateSlotUI();
            RaiseInventory(inv.inventoryPanel, true);
        }
        else
        {
            RaiseInventory(inv.inventoryPanel, false);
            ItemDetailUI.Instance?.Hide();
            inv.Close();
        }
    }

    /// <summary>
    /// InventoryPanel 은 씬 캔버스(sortingOrder 0) 소속이라 배경(96)에 가린다.
    /// 메뉴가 열려 있는 동안만 overrideSorting 으로 97 에 올리고, 탭 바와 겹치지 않게
    /// 위쪽을 밀어둔다. 닫을 때 붙인 컴포넌트와 여백을 전부 원상복구한다.
    /// </summary>
    void RaiseInventory(GameObject panel, bool raise)
    {
        var rt = panel.transform as RectTransform;

        if (raise)
        {
            if (_invCanvas == null)
            {
                // 이미 Canvas 가 붙어 있으면 우리가 만든 것이 아니므로 건드리지 않는다
                if (panel.GetComponent<Canvas>() != null) return;
                _invCanvas = panel.AddComponent<Canvas>();
                _invCanvas.overrideSorting = true;
                _invCanvas.sortingOrder    = 97;
                _invRaycaster = panel.AddComponent<GraphicRaycaster>();
            }

            // 아이템창 캔버스는 ConstantPixelSize(배율 1)라 단위가 곧 화면 픽셀이다.
            if (rt != null && !_invOffsetSaved)
            {
                _invOffsetMax   = rt.offsetMax;
                _invOffsetSaved = true;
                rt.offsetMax    = new Vector2(_invOffsetMax.x, _invOffsetMax.y - TabBarBottomGapPx());
            }
        }
        else
        {
            if (rt != null && _invOffsetSaved) rt.offsetMax = _invOffsetMax;
            _invOffsetSaved = false;

            if (_invRaycaster != null) { Destroy(_invRaycaster); _invRaycaster = null; }
            if (_invCanvas    != null) { Destroy(_invCanvas);    _invCanvas    = null; }
        }
    }

    // ─── 배경 캡처 + 블러 (F-8-3) ─────────────────────────────────────────
    // 셰이더 없이 처리한다: 카메라를 낮은 해상도로 한 번 렌더하고 한 번 더 축소한 뒤,
    // bilinear 로 화면 전체에 늘려 그린다. 축소·확대 자체가 블러가 된다.
    // UnityEngine.ScreenCapture 는 이 프로젝트의 런타임 어셈블리가 참조하지 않아 쓸 수 없다.
    // 카메라 렌더라 Overlay UI 는 캡처에 담기지 않지만, 씬 UI 는 이 배경(96)에 가려지므로 문제되지 않는다.
    void CaptureBackdrop()
    {
        ReleaseBackdrop();

        var cam = Camera.main;
        if (cam == null) cam = Object.FindAnyObjectByType<Camera>();
        if (cam == null) return;

        RenderTexture quarter = null, small = null;
        var prevActive = RenderTexture.active;
        var prevTarget = cam.targetTexture;
        try
        {
            int w  = Mathf.Max(8, Screen.width  / 4);
            int h  = Mathf.Max(8, Screen.height / 4);
            int w2 = Mathf.Max(2, w / 4);
            int h2 = Mathf.Max(2, h / 4);

            quarter = RenderTexture.GetTemporary(w, h, 16);
            quarter.filterMode = FilterMode.Bilinear;
            cam.targetTexture = quarter;
            cam.Render();
            cam.targetTexture = prevTarget;

            small = RenderTexture.GetTemporary(w2, h2, 0);
            small.filterMode = FilterMode.Bilinear;
            Graphics.Blit(quarter, small);

            _backdropTex = new Texture2D(w2, h2, TextureFormat.RGB24, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode   = TextureWrapMode.Clamp,
            };
            RenderTexture.active = small;
            _backdropTex.ReadPixels(new Rect(0f, 0f, w2, h2), 0, 0);
            _backdropTex.Apply();

            _backdropSprite = Sprite.Create(_backdropTex,
                new Rect(0f, 0f, w2, h2), new Vector2(0.5f, 0.5f));
            _backdrop.sprite = _backdropSprite;
        }
        finally
        {
            cam.targetTexture    = prevTarget;
            RenderTexture.active = prevActive;
            if (quarter != null) RenderTexture.ReleaseTemporary(quarter);
            if (small   != null) RenderTexture.ReleaseTemporary(small);
        }
    }

    void ReleaseBackdrop()
    {
        _backdrop.sprite = null;
        if (_backdropSprite != null) { Destroy(_backdropSprite); _backdropSprite = null; }
        if (_backdropTex    != null) { Destroy(_backdropTex);    _backdropTex    = null; }
    }

    void OnDestroy() => ReleaseBackdrop();

    // ─── UI 빌드 ──────────────────────────────────────────────────────────
    void BuildUI(Transform root)
    {
        // 배경: 캡처 스프라이트가 없을 때를 대비해 검정으로 시작한다
        _backdrop = MakeImage(root, "Backdrop", Color.black);
        Stretch(_backdrop.rectTransform);
        _backdrop.preserveAspect = false;

        _dim = MakeImage(root, "Dim", DimCol);
        Stretch(_dim.rectTransform);

        // 탭 바·탭 내용은 InventoryPanel(97) 위에 와야 한다 → 별도 캔버스 98
        _front = new GameObject("Front");
        _front.transform.SetParent(root, false);
        Stretch(_front.AddComponent<RectTransform>());
        var frontCanvas = _front.AddComponent<Canvas>();
        frontCanvas.overrideSorting = true;
        frontCanvas.sortingOrder    = 98;
        _front.AddComponent<GraphicRaycaster>();

        BuildTabBar(_front.transform);
        BuildTabPanels(_front.transform);
    }

    void BuildTabBar(Transform parent)
    {
        _tabButtons = new Button[TabLabels.Length];

        const float w = 180f, gap = 8f, h = 50f;
        float total = TabLabels.Length * w + (TabLabels.Length - 1) * gap;

        // 탭 바는 화면 위쪽 가장자리에 붙인다. 아이템창을 이 아래로 밀어 겹치지 않게 한다.
        var barGo = new GameObject("TabBar");
        barGo.transform.SetParent(parent, false);
        _tabBarRect = barGo.AddComponent<RectTransform>();
        _tabBarRect.anchorMin = _tabBarRect.anchorMax = new Vector2(0.5f, 1f);
        _tabBarRect.pivot     = new Vector2(0.5f, 1f);
        _tabBarRect.anchoredPosition = new Vector2(0f, -18f);
        _tabBarRect.sizeDelta        = new Vector2(total + 200f, h + 20f);

        var strip = barGo.AddComponent<Image>();
        strip.color = new Color(0.06f, 0.06f, 0.08f, 0.92f);

        // ◀ 탭 4개 ▶
        MakeText(barGo.transform, "◀", 28, new Vector2(-total / 2f - 50f, 0f), new Vector2(44f, h));
        MakeText(barGo.transform, "▶", 28, new Vector2(total / 2f + 50f, 0f), new Vector2(44f, h));

        float x0 = -total / 2f + w / 2f;
        for (int i = 0; i < TabLabels.Length; i++)
        {
            int idx = i;
            _tabButtons[i] = MakeButton(barGo.transform, TabLabels[i],
                new Vector2(x0 + i * (w + gap), 0f), new Vector2(w, h),
                () => SwitchTab((Tab)idx), 20);
            // 좌우 화살표는 탭 이동에 쓰므로 내비게이션은 상하만 허용한다
            var nav = _tabButtons[i].navigation;
            nav.mode = Navigation.Mode.Vertical;
            _tabButtons[i].navigation = nav;
        }
    }

    /// <summary>탭 바 아래끝까지의 화면 픽셀 높이. Overlay 캔버스라 월드 좌표가 곧 화면 픽셀이다.</summary>
    float TabBarBottomGapPx()
    {
        if (_tabBarRect == null) return 0f;
        var corners = new Vector3[4];
        _tabBarRect.GetWorldCorners(corners);   // [0]=좌하 [1]=좌상 [2]=우상 [3]=우하
        return Mathf.Max(0f, Screen.height - corners[0].y) + 8f;
    }

    void BuildTabPanels(Transform parent)
    {
        _tabPanels = new GameObject[TabLabels.Length];

        // 아이템 탭 — 실제 내용은 씬의 InventoryPanel 이 담당한다.
        // 여기에는 아이템창을 띄울 수 없을 때의 빈 상태만 둔다.
        _tabPanels[(int)Tab.Item] = MakePanel(parent, "Tab_Item", show: false);

        // 쪽지 탭 — 데이터 경로(notes.json)가 F-8-5 미해결이라 빈 상태가 정상 동작이다.
        var note = MakePanel(parent, "Tab_Note", show: true);
        var noteTitle = MakeText(note.transform, "쪽지", 30, new Vector2(0f, 200f), new Vector2(500f, 44f));
        noteTitle.fontStyle = FontStyles.Bold;
        MakeText(note.transform, "아직 읽은 쪽지가 없습니다.", 18,
            new Vector2(0f, 0f), new Vector2(520f, 40f)).color = NoteCol;
        _tabPanels[(int)Tab.Note] = note;

        // 설정 탭 — 내용은 기존 SettingsPanelUI(99) 가 그린다
        _tabPanels[(int)Tab.Settings] = MakePanel(parent, "Tab_Settings", show: false);

        // 그만두기 탭 — 중단 저장은 아직 구현이 없다(계획서 4단계).
        // 저장 없이 나가면 진행이 사라지므로 버튼을 눌리지 않게 둔다.
        var quit = MakePanel(parent, "Tab_Quit", show: true);
        var quitTitle = MakeText(quit.transform, "그만두기", 30, new Vector2(0f, 200f), new Vector2(500f, 44f));
        quitTitle.fontStyle = FontStyles.Bold;
        MakeText(quit.transform, "중단 저장 후 게임을 끝냅니다.", 18,
            new Vector2(0f, 60f), new Vector2(520f, 40f)).color = NoteCol;
        var quitBtn = MakeButton(quit.transform, "중단 저장 후 종료",
            new Vector2(0f, -10f), new Vector2(300f, 52f), () => { }, 20);
        quitBtn.interactable = false;
        MakeText(quit.transform, "준비 중입니다.", 15,
            new Vector2(0f, -70f), new Vector2(520f, 32f)).color = NoteCol;
        _tabPanels[(int)Tab.Quit] = quit;

        for (int i = 0; i < _tabPanels.Length; i++)
            if (_tabPanels[i] != null) _tabPanels[i].SetActive(false);
    }

    /// <summary>탭 내용을 담는 가운데 패널. show=false 면 배경판 없이 투명하게 둔다.</summary>
    GameObject MakePanel(Transform parent, string name, bool show)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -20f);
        rt.sizeDelta = new Vector2(680f, 580f);

        if (show)
        {
            var img = go.AddComponent<Image>();
            img.color = PanelBg;
        }
        return go;
    }

    // ─── 기본 UI 헬퍼 (SettingsPanelUI 와 같은 형태) ──────────────────────
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
        var go  = new GameObject("Txt_" + text); go.transform.SetParent(p, false);
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
        System.Action onClick, int fontSize = 16)
    {
        var go  = new GameObject("Btn_" + label); go.transform.SetParent(p, false);
        var img = go.AddComponent<Image>(); img.color = TabInactive;
        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick());
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos; r.sizeDelta = sz;

        var lt = MakeText(go.transform, label, fontSize, Vector2.zero, sz);
        Stretch(lt.GetComponent<RectTransform>());
        return btn;
    }
}
