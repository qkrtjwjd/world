using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 설정 패널 — DontDestroyOnLoad 자동 생성 싱글톤.
/// Inspector 연결 없이 SettingsPanelUI.Show() / Hide() 만으로 사용.
/// 7개 탭: 사운드 | 화면 | 조작 | 접근성 | 저장 | 언어 | 게임플레이
/// </summary>
public class SettingsPanelUI : MonoBehaviour
{
    public static SettingsPanelUI Instance
    {
        get
        {
            if (_instance == null) CreateInstance();
            return _instance;
        }
    }
    static SettingsPanelUI _instance;

    public static bool IsOpen { get; private set; }

    // ── 내부 상태 ──────────────────────────────────────────────────────────
    CanvasGroup _cg;
    int         _currentTab = 0;
    GameObject[] _tabPanels;
    Button[]     _tabButtons;

    /// <summary>설정 패널 전체 배율. 항목 좌표가 전부 상수라 스케일로 크기를 조절한다.</summary>
    const float PanelScale = 1.3f;

    /// <summary>패널 가로 폭. 탭 7개 라벨이 들어갈 만큼 넓혀 뒀다(구 680).</summary>
    const float PanelWidth   = 880f;
    const float DividerWidth = PanelWidth - 40f;

    static readonly Color TabActive   = new Color(0.30f, 0.55f, 0.90f, 1f);
    static readonly Color TabInactive = new Color(0.22f, 0.22f, 0.22f, 1f);
    static readonly Color PanelBg     = new Color(0.10f, 0.10f, 0.12f, 1f);
    static readonly Color SectionCol  = new Color(0.55f, 0.75f, 1.00f, 1f);
    static readonly Color WarningCol  = new Color(1.00f, 0.80f, 0.20f, 1f);
    static readonly Color DangerCol   = new Color(0.75f, 0.20f, 0.20f, 1f);
    static readonly Color SliderFill  = new Color(0.35f, 0.65f, 1.00f, 1f);

    // 키 리바인딩 대기 상태
    int _rebindingSlot = -1;  // -1=없음, 0=interact, 1=inventory, 2=dagger, 3=pause, 4=skip, 5=log
    int _rebindEndFrame = -1; // 리바인드가 끝난(취소 포함) 프레임 — 같은 프레임에 PauseSystem이 키를 처리하지 않게 함

    /// <summary>리바인딩 진행 중이거나 이번 프레임에 막 끝났는지. PauseSystem이 키 입력을 무시해야 하는 구간.</summary>
    public static bool IsRebinding =>
        _instance != null && (_instance._rebindingSlot >= 0 || _instance._rebindEndFrame == Time.frameCount);
    Button _rebindingButton;

    // ConfirmPopup
    GameObject _confirmPopup;

    // ─── 자동 생성 ────────────────────────────────────────────────────────
    static void CreateInstance()
    {
        var root = new GameObject("SettingsPanelUI [Auto]");
        DontDestroyOnLoad(root);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 99;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;
        root.AddComponent<GraphicRaycaster>();

        _instance     = root.AddComponent<SettingsPanelUI>();
        _instance._cg = root.AddComponent<CanvasGroup>();
        _instance._cg.alpha = 0f;
        _instance._cg.blocksRaycasts = false;
        root.SetActive(false);

        _instance.BuildUI(root.transform);
    }

    // ─── 공개 API ─────────────────────────────────────────────────────────
    public static void Show(int tab = 0)
    {
        var inst = Instance;
        if (IsOpen) return;
        IsOpen = true;
        inst.gameObject.SetActive(true);
        inst.SwitchTab(tab);
        inst.RebuildAllTabs();
        inst.StartCoroutine(inst.FadeIn());
    }

    public static void Hide()
    {
        if (!IsOpen || _instance == null) return;
        IsOpen = false;
        _instance._cg.alpha = 0f;
        _instance._cg.blocksRaycasts = false;
        _instance.gameObject.SetActive(false);
        PlayerPrefs.Save();
    }

    // ─── UI 빌드 ──────────────────────────────────────────────────────────
    void BuildUI(Transform root)
    {
        // 어두운 배경
        var bg = MakeImage(root, "Dimmer", new Color(0f, 0f, 0f, 0.78f));
        Stretch(bg.rectTransform);
        // 배경 클릭으로 닫기
        var bgBtn = bg.gameObject.AddComponent<Button>();
        bgBtn.targetGraphic = bg;
        bgBtn.transition = Selectable.Transition.None;
        bgBtn.onClick.AddListener(Hide);

        // 중앙 패널
        // 내부 항목 위치가 전부 이 패널 중심 기준 상수라, 크기를 줄이려면 sizeDelta 가 아니라
        // 스케일을 줄여야 한다. sizeDelta 를 건드리면 항목들이 패널 밖으로 밀린다.
        // 가로만 넓혔다(680 -> 880). 탭 7개 라벨이 버튼 밖으로 넘쳐 잘리기 때문이다.
        // 세로는 건드리지 않는다 — 항목 y 좌표가 전부 상수라 높이를 바꾸면 밀린다.
        var panel = MakeImage(root, "Panel", PanelBg);
        var pr = panel.rectTransform;
        pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
        pr.sizeDelta = new Vector2(PanelWidth, 580f);
        pr.anchoredPosition = Vector2.zero;
        pr.localScale = Vector3.one * PanelScale;
        var pt = panel.transform;

        // 제목
        var title = MakeText(pt, "설정", 36, new Vector2(0f, 255f), new Vector2(600f, 50f));
        title.fontStyle = FontStyles.Bold;

        // 구분선 (제목 아래)
        Divider(pt, new Vector2(0f, 225f), DividerWidth);

        // 탭 버튼 행
        string[] tabLabels = { "🔊 사운드", "🖥️ 화면", "⌨️ 조작", "♿ 접근성", "💾 저장", "🌐 언어", "📋 게임플레이" };
        _tabButtons = new Button[tabLabels.Length];
        _tabPanels  = new GameObject[tabLabels.Length];

        // 라벨이 버튼보다 길어 넘치던 것을 버튼 폭·간격을 키워 담는다.
        // 바깥 버튼 끝 = 360 + 58 = 418 < 440(패널 반폭) 이라 패널 안에 들어온다.
        float tabW   = 116f;
        float tabH   = 34f;
        float tabGap = 120f;
        float tabStartX = -(tabGap * 3f);  // 7개 탭 가운데 정렬

        for (int i = 0; i < tabLabels.Length; i++)
        {
            int idx = i;
            var tabBtn = MakeButton(pt, tabLabels[i], new Vector2(tabStartX + tabGap * i, 198f),
                new Vector2(tabW, tabH), () => SwitchTab(idx), 18);
            _tabButtons[i] = tabBtn;
        }

        // 스크롤 컨텐츠 영역
        var scrollGo   = new GameObject("ScrollArea"); scrollGo.transform.SetParent(pt, false);
        var scrollRect = scrollGo.AddComponent<ScrollRect>();
        var scrollRt   = scrollGo.GetComponent<RectTransform>();
        // 탭 버튼 줄(y 198, 높이 34 → 아래끝 181)과 겹치지 않게 내려서 잡는다.
        // 아래끝은 닫기 위 구분선(y -231) 위에 둔다.
        scrollRt.anchoredPosition = new Vector2(0f, -14f);
        scrollRt.sizeDelta        = new Vector2(PanelWidth - 20f, 366f);   // y -197 ~ 169

        // ⚠ Mask 를 쓰면 안 된다. Mask 는 그래픽의 알파로 스텐실을 쓰는데, 여기 그래픽은
        // 색이 Color.clear(알파 0)라 스텐실이 하나도 안 써지고 → 자식이 전부 스텐실 테스트에
        // 걸려 화면에서 사라진다. RectMask2D 는 사각형으로 자르므로 그래픽이 필요 없다.
        // (Image 는 ScrollRect 드래그 레이캐스트용으로 남긴다.)
        var viewport = new GameObject("Viewport"); viewport.transform.SetParent(scrollGo.transform, false);
        viewport.AddComponent<RectMask2D>();
        viewport.AddComponent<Image>().color = Color.clear;
        var vpRt = viewport.GetComponent<RectTransform>();
        Stretch(vpRt);

        var content = new GameObject("Content"); content.transform.SetParent(viewport.transform, false);
        var contentRt = content.AddComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot     = new Vector2(0.5f, 1f);
        contentRt.sizeDelta = new Vector2(0f, 760f);   // 화면·접근성 탭 항목 증가분 수용
        contentRt.anchoredPosition = Vector2.zero;

        scrollRect.content          = contentRt;
        scrollRect.viewport         = vpRt;
        scrollRect.horizontal       = false;
        scrollRect.vertical         = true;
        scrollRect.scrollSensitivity = 30f;
        scrollRect.movementType     = ScrollRect.MovementType.Clamped;

        // 7개 탭 패널 생성
        _tabPanels[0] = BuildSoundTab(content.transform);
        _tabPanels[1] = BuildScreenTab(content.transform);
        _tabPanels[2] = BuildControlsTab(content.transform);
        _tabPanels[3] = BuildAccessibilityTab(content.transform);
        _tabPanels[4] = BuildSaveTab(content.transform);
        _tabPanels[5] = BuildLanguageTab(content.transform);
        _tabPanels[6] = BuildGameplayTab(content.transform);

        // 구분선 (닫기 위)
        Divider(pt, new Vector2(0f, -231f), DividerWidth);

        // 닫기 버튼
        var closeBtn = MakeButton(pt, "닫기", new Vector2(0f, -257f), new Vector2(160f, 40f), Hide);
        closeBtn.GetComponent<Image>().color = DangerCol;
    }

    // ─── 탭 패널 빌더 ─────────────────────────────────────────────────────

    // ── 🔊 사운드 ──
    GameObject BuildSoundTab(Transform parent)
    {
        var p = TabPanel(parent, "Tab_Sound");
        var sm = SettingsManager.Instance;

        Section(p, "볼륨", 0f);
        LabelSlider(p, "마스터 볼륨",   -38f,  sm?.masterVolume      ?? 1f, v => SettingsManager.Instance?.SetMasterVolume(v));
        LabelSlider(p, "BGM 볼륨",      -88f,  sm?.bgmVolume         ?? 1f, v => SettingsManager.Instance?.SetBGMVolume(v));
        LabelSlider(p, "효과음 볼륨",   -138f, sm?.sfxVolume         ?? 1f, v => SettingsManager.Instance?.SetSFXVolume(v));
        LabelSlider(p, "보이스 볼륨",   -188f, sm?.voiceVolume       ?? 1f, v => SettingsManager.Instance?.SetVoiceVolume(v));
        LabelSlider(p, "환경음 볼륨",   -238f, sm?.ambientVolume     ?? 1f, v => SettingsManager.Instance?.SetAmbientVolume(v));

        Section(p, "⚠ 게임 전용", -290f);
        NoteText(p, "인형화 구간에서 강도가 달라지는 사운드를 개별 조절합니다.", -325f);
        LabelSlider(p, "딱딱 소리 볼륨",    -370f, sm?.clickingVolume    ?? 1f, v => SettingsManager.Instance?.SetClickingVolume(v));
        LabelSlider(p, "글리치 노이즈 볼륨",-420f, sm?.glitchNoiseVolume ?? 1f, v => SettingsManager.Instance?.SetGlitchNoiseVolume(v));

        Section(p, "시스템", -472f);
        LabelToggle(p, "비활성 창일 때 음소거", -510f, sm?.muteWhenUnfocused ?? false,
            v => SettingsManager.Instance?.SetMuteWhenUnfocused(v));

        return p;
    }

    // ── 🖥️ 화면 ──
    GameObject BuildScreenTab(Transform parent)
    {
        var p = TabPanel(parent, "Tab_Screen");
        var sm = SettingsManager.Instance;
        float y = 0f;

        Section(p, "디스플레이", y); y -= 42f;
        // 해상도 드롭다운
        MakeResolutionDropdown(p, y); y -= 52f;
        // 화면 모드 (전체화면 / 테두리 없는 창 / 창모드)
        MakeSegment(p, "화면 모드", y, new[] { "전체화면", "테두리없는 창", "창모드" },
            sm?.displayMode ?? 1,
            (idx, btns) => { SettingsManager.Instance?.SetDisplayMode(idx); RefreshSegment(btns, idx); }); y -= 44f;
        LabelToggle(p, "V-Sync",        y, sm?.vsync      ?? true,          v => SettingsManager.Instance?.SetVSync(v)); y -= 46f;
        // FPS 제한
        int[] fpsValues = { 0, 30, 60, 120, 144 };
        int curFps = sm?.frameRateCap ?? 0;
        int curFpsIdx = System.Array.IndexOf(fpsValues, curFps); if (curFpsIdx < 0) curFpsIdx = 0;
        MakeSegment(p, "FPS 제한", y, new[] { "무제한", "30", "60", "120", "144" }, curFpsIdx,
            (idx, btns) => { SettingsManager.Instance?.SetFrameRateCap(fpsValues[idx]); RefreshSegment(btns, idx); }); y -= 38f;
        NoteText(p, "V-Sync가 켜져 있으면 FPS 제한은 무시됩니다.", y); y -= 32f;

        Section(p, "화질", y); y -= 42f;
        LabelSlider(p, "밝기",          y, sm?.brightness           ?? 0.5f, v => SettingsManager.Instance?.SetBrightness(v));           y -= 50f;

        Section(p, "⚠ 게임 전용", y); y -= 42f;
        NoteText(p, "환상↔현실 대비 및 글리치 연출 강도를 개별 조절합니다.", y - 10f); y -= 38f;
        LabelSlider(p, "색채 강도 (채도)",   y, sm?.saturation             ?? 1f,   v => SettingsManager.Instance?.SetSaturation(v));           y -= 50f;
        LabelSlider(p, "글리치 효과 강도",   y, sm?.glitchEffectIntensity   ?? 1f,   v => SettingsManager.Instance?.SetGlitchEffectIntensity(v)); y -= 50f;
        LabelToggle(p, "화면 흔들림",         y, sm?.cameraShakeEnabled      ?? true, v => SettingsManager.Instance?.SetCameraShake(v));           y -= 46f;
        LabelToggle(p, "화면 테두리 효과",    y, sm?.screenEdgeEffectEnabled ?? true, v => SettingsManager.Instance?.SetScreenEdgeEffect(v));

        return p;
    }

    // ── ⌨️ 조작 ──
    GameObject BuildControlsTab(Transform parent)
    {
        var p = TabPanel(parent, "Tab_Controls");
        var sm = SettingsManager.Instance;
        float y = 0f;

        Section(p, "이동", y); y -= 42f;
        NoteText(p, "이동 키(WASD/방향키)는 Unity 입력 설정에 고정되어 있어\n런타임 변경이 불가합니다.", y - 6f); y -= 50f;

        Section(p, "키 리바인딩", y); y -= 42f;
        NoteText(p, "버튼을 클릭한 뒤 원하는 키를 누르세요.", y - 6f); y -= 32f;

        RebindRow(p, "상호작용",    0, sm?.keyInteract     ?? KeyCode.E,      y); y -= 46f;
        RebindRow(p, "인벤토리",    1, sm?.keyInventory    ?? KeyCode.I,      y); y -= 46f;
        RebindRow(p, "단검 파지",   2, sm?.keyDagger       ?? KeyCode.F,      y); y -= 46f;
        RebindRow(p, "일시정지",    3, sm?.keyPause        ?? KeyCode.Escape, y); y -= 46f;
        RebindRow(p, "대화 스킵",   4, sm?.keyDialogueSkip ?? KeyCode.Space,  y); y -= 46f;
        RebindRow(p, "대화 로그",   5, sm?.keyDialogueLog  ?? KeyCode.L,      y); y -= 46f;
        RebindRow(p, "빠른 저장",   6, sm?.keyQuickSave    ?? KeyCode.F5,     y); y -= 56f;

        var resetKeyBtn = MakeButton(p.transform, "키 기본값 복원", new Vector2(0f, y), new Vector2(180f, 38f),
            () => { SettingsManager.Instance?.ResetKeyBindings(); RebuildTab(2); });
        resetKeyBtn.GetComponent<Image>().color = new Color(0.45f, 0.35f, 0.15f, 1f);

        return p;
    }

    // ── ♿ 접근성 ──
    GameObject BuildAccessibilityTab(Transform parent)
    {
        var p = TabPanel(parent, "Tab_Access");
        var sm = SettingsManager.Instance;
        float y = 0f;

        Section(p, "시각 · 광과민증", y); y -= 42f;
        // 색맹 모드
        MakeSegment(p, "색맹 모드", y, new[] { "없음", "적록1형", "적록2형", "청황" },
            sm?.colorblindMode ?? 0,
            (idx, btns) => { SettingsManager.Instance?.SetColorblindMode(idx); RefreshSegment(btns, idx); }); y -= 44f;
        LabelToggle(p, "글리치 효과 완전 비활성화",   y, sm?.glitchEffectDisabled ?? false, v => SettingsManager.Instance?.SetGlitchEffectDisabled(v));  y -= 46f;
        LabelToggle(p, "화면 번쩍임 비활성화",         y, sm?.flashEffectDisabled  ?? false, v => SettingsManager.Instance?.SetFlashEffectDisabled(v));   y -= 46f;

        Section(p, "청각", y); y -= 42f;
        LabelToggle(p, "딱딱 소리 비활성화",           y, sm?.clickingSoundDisabled ?? false, v => SettingsManager.Instance?.SetClickingSoundDisabled(v)); y -= 46f;

        Section(p, "대화 · 텍스트", y); y -= 42f;
        LabelSlider(p, "자동 대화 속도 (×배)", y, sm?.dialogueSpeed ?? 1f, v => SettingsManager.Instance?.SetDialogueSpeed(v), 0.25f, 3f); y -= 50f;
        LabelToggle(p, "대사 자동 진행 모드",   y, sm?.autoDialogue ?? false, v => SettingsManager.Instance?.SetAutoDialogue(v));  y -= 46f;

        // 텍스트 크기 3버튼
        Section(p, "텍스트 크기", y); y -= 42f;
        TextSizeButtons(p, y); y -= 50f;

        LabelSlider(p, "텍스트 배경 불투명도", y, sm?.textBgOpacity ?? 0.5f, v => SettingsManager.Instance?.SetTextBgOpacity(v)); y -= 50f;

        Section(p, "⚠ 게임 전용", y); y -= 42f;
        LabelToggle(p, "조작 반전 알림 표시", y, sm?.showInputReverseAlert ?? true, v => SettingsManager.Instance?.SetShowInputReverseAlert(v));
        NoteText(p, "벨 등장 시 조작 반전 효과에 UI 경고 텍스트를 표시합니다.", y - 36f);

        return p;
    }

    // ── 💾 저장 ──
    GameObject BuildSaveTab(Transform parent)
    {
        var p = TabPanel(parent, "Tab_Save");
        float y = 0f;

        Section(p, "세이브 슬롯", y); y -= 42f;

        // 슬롯 3개
        for (int slot = 0; slot < 3; slot++)
        {
            int s = slot;
            SaveSlotRow(p, s, y); y -= 64f;
        }

        Section(p, "자동 저장", y); y -= 42f;
        var sm = SettingsManager.Instance;
        LabelToggle(p, "전투 전 자동 저장", y, sm?.autoSaveEnabled ?? true, v => SettingsManager.Instance?.SetAutoSave(v)); y -= 46f;

        Section(p, "데이터 초기화", y); y -= 42f;
        NoteText(p, "게임 저장 데이터를 포함한 모든 데이터가 삭제됩니다.", y - 10f); y -= 36f;
        var resetBtn = MakeButton(p.transform, "데이터 초기화", new Vector2(0f, y), new Vector2(200f, 40f), ShowResetConfirm);
        resetBtn.GetComponent<Image>().color = DangerCol;

        return p;
    }

    // ── 🌐 언어 ──
    GameObject BuildLanguageTab(Transform parent)
    {
        var p = TabPanel(parent, "Tab_Language");
        float y = 0f;

        Section(p, "표시 언어 (UI / 메뉴)", y); y -= 50f;
        NoteText(p, "게임 메뉴와 UI 텍스트에 사용되는 언어를 설정합니다.", y); y -= 38f;
        LangButtons(p, y, isDialogue: false); y -= 58f;

        Section(p, "대사 언어", y); y -= 50f;
        NoteText(p, "대화·독백 텍스트에 사용되는 언어를 설정합니다.\n표시 언어와 독립적으로 변경 가능합니다.", y); y -= 52f;
        LangButtons(p, y, isDialogue: true);

        return p;
    }

    // ── 📋 게임플레이 ──
    GameObject BuildGameplayTab(Transform parent)
    {
        var p = TabPanel(parent, "Tab_Gameplay");
        var sm = SettingsManager.Instance;
        float y = 0f;

        Section(p, "HUD / UI", y); y -= 42f;
        LabelToggle(p, "튜토리얼 힌트 표시",     y, sm?.showTutorialHints    ?? true,  v => SettingsManager.Instance?.SetShowTutorialHints(v));   y -= 46f;
        LabelToggle(p, "목표 UI 표시",          y, sm?.showObjectiveUI      ?? true,  v => SettingsManager.Instance?.SetShowObjectiveUI(v));     y -= 46f;
        LabelToggle(p, "대화 로그 사용 (L키)",   y, sm?.showDialogueLog      ?? true,  v => SettingsManager.Instance?.SetShowDialogueLog(v));     y -= 46f;

        Section(p, "⚠ 게임 전용 게이지", y); y -= 42f;
        NoteText(p, "OFF 시 루의 상태를 연출로만 판단하는 하드코어 모드.", y - 6f); y -= 36f;
        LabelToggle(p, "인형화 게이지 표시",          y, sm?.showDollificationGauge   ?? true,  v => SettingsManager.Instance?.SetShowDollificationGauge(v));   y -= 46f;
        LabelToggle(p, "환상/현실 게이지 표시",       y, sm?.showFantasyRealityGauge  ?? true,  v => SettingsManager.Instance?.SetShowFantasyRealityGauge(v));  y -= 46f;

        Section(p, "⚠ 전투 모드", y); y -= 42f;
        NoteText(p, "ON: 글리치 구간 전투 시 게이지 기준으로 자동 모드 결정.\nOFF: 매번 단검/마시멜로 선택 UI가 표시됩니다.", y - 6f); y -= 46f;
        LabelToggle(p, "전투 모드 자동 선택 (PendingModeUI)", y, sm?.combatModeAuto ?? false, v => SettingsManager.Instance?.SetCombatModeAuto(v));

        return p;
    }

    // ─── UI 컨트롤 헬퍼 ───────────────────────────────────────────────────

    /// <summary>
    /// 탭 내용을 담는 컨테이너.
    ///
    /// ⚠ <b>pivot 을 (0.5,0.5) 로 두는 것이 핵심이다.</b> 이 안의 항목들은 MakeText·MakeButton·
    /// MakeSlider 가 전부 <c>anchor (0.5,0.5)</c> 로 만들어 <b>이 패널의 중심 기준</b>으로 놓인다.
    /// pivot 이 위쪽이면 중심이 뷰포트 위끝에서 380px(높이 760의 절반) 아래로 내려가,
    /// 항목이 전부 뷰포트(높이 390) 밖으로 밀려 Mask 에 잘린다 — 화면에 아무것도 안 나온다.
    /// 같은 스크롤 구조를 쓰는 <see cref="JournalUI"/> 는 행마다 SetTopAnchored 로 재앵커해 피한다.
    /// 여기서는 항목 좌표를 그대로 두고 컨테이너 기준만 맞춘다.
    /// </summary>
    static GameObject TabPanel(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(640f, 760f);
        rt.anchoredPosition = new Vector2(0f, -TabTopMargin);   // 항목 y=0 이 위끝에서 이만큼 아래
        return go;
    }

    /// <summary>탭 내용 첫 항목(y=0)이 뷰포트 위끝에서 떨어지는 거리.</summary>
    const float TabTopMargin = 24f;

    static void Section(GameObject p, string label, float y)
    {
        var t = MakeText(p.transform, label, 17, new Vector2(-200f, y + 10f), new Vector2(240f, 28f));
        t.color     = SectionCol;
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.fontStyle = FontStyles.Bold;
        // 구분선
        var line = MakeImage(p.transform, "SectionLine", new Color(0.35f, 0.35f, 0.40f, 1f));
        line.rectTransform.anchoredPosition = new Vector2(0f, y - 2f);
        line.rectTransform.sizeDelta        = new Vector2(620f, 1f);
    }

    static void NoteText(GameObject p, string msg, float y)
    {
        var t = MakeText(p.transform, msg, 13, new Vector2(0f, y + 8f), new Vector2(560f, 36f));
        t.color     = new Color(0.70f, 0.70f, 0.70f, 1f);
        t.alignment = TextAlignmentOptions.TopLeft;
    }

    static void Divider(Transform parent, Vector2 pos, float width)
    {
        var img = MakeImage(parent, "Divider", new Color(0.30f, 0.30f, 0.35f, 1f));
        img.rectTransform.anchoredPosition = pos;
        img.rectTransform.sizeDelta        = new Vector2(width, 1f);
    }

    /// <summary>라벨 + 슬라이더 (한 행)</summary>
    static Slider LabelSlider(GameObject p, string label, float y, float val,
        System.Action<float> onChange, float min = 0f, float max = 1f)
    {
        MakeText(p.transform, label, 16, new Vector2(-210f, y), new Vector2(200f, 30f))
            .alignment = TextAlignmentOptions.MidlineLeft;
        var s = MakeSlider(p.transform, new Vector2(105f, y), new Vector2(260f, 22f), val, min, max);
        s.onValueChanged.AddListener(v => onChange?.Invoke(v));
        // 퍼센트 텍스트
        var pctTxt = MakeText(p.transform, Pct(val), 14, new Vector2(262f, y), new Vector2(48f, 26f));
        pctTxt.alignment = TextAlignmentOptions.MidlineRight;
        s.onValueChanged.AddListener(v => pctTxt.text = Pct(v));
        return s;
    }

    static string Pct(float v) => Mathf.RoundToInt(v * 100f) + "%";

    /// <summary>라벨 + 토글 (한 행)</summary>
    static Toggle LabelToggle(GameObject p, string label, float y, bool val,
        System.Action<bool> onChange)
    {
        MakeText(p.transform, label, 16, new Vector2(-165f, y), new Vector2(280f, 30f))
            .alignment = TextAlignmentOptions.MidlineLeft;
        var tg = MakeToggle(p.transform, new Vector2(240f, y), new Vector2(46f, 26f), val);
        tg.onValueChanged.AddListener(v => onChange?.Invoke(v));
        return tg;
    }

    void RebindRow(GameObject p, string label, int slot, KeyCode current, float y)
    {
        MakeText(p.transform, label, 16, new Vector2(-200f, y), new Vector2(180f, 30f))
            .alignment = TextAlignmentOptions.MidlineLeft;
        var btn = MakeButton(p.transform, KeyLabel(current),
            new Vector2(165f, y), new Vector2(150f, 34f), () => StartRebind(slot));
        btn.GetComponent<Image>().color = new Color(0.18f, 0.28f, 0.42f, 1f);
    }

    Button[] _textSizeButtons;

    void TextSizeButtons(GameObject p, float y)
    {
        string[] labels = { "소", "중", "대" };
        int cur = SettingsManager.Instance?.textSize ?? 1;
        _textSizeButtons = new Button[3];
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            var btn = MakeButton(p.transform, labels[i],
                new Vector2(-100f + 100f * i, y), new Vector2(80f, 36f),
                () =>
                {
                    SettingsManager.Instance?.SetTextSize(idx);
                    RefreshTextSizeButtons(idx);   // 클릭 즉시 선택 하이라이트 갱신
                });
            btn.GetComponent<Image>().color = (cur == i) ? TabActive : TabInactive;
            _textSizeButtons[i] = btn;
        }
    }

    void RefreshTextSizeButtons(int selected)
    {
        if (_textSizeButtons == null) return;
        for (int i = 0; i < _textSizeButtons.Length; i++)
            if (_textSizeButtons[i] != null)
                _textSizeButtons[i].GetComponent<Image>().color = (i == selected) ? TabActive : TabInactive;
    }

    /// <summary>라벨 + N개 세그먼트 버튼 (한 행). 선택 시 onSelect(idx, buttons) 호출.</summary>
    static Button[] MakeSegment(GameObject p, string label, float y, string[] labels, int current,
        System.Action<int, Button[]> onSelect)
    {
        MakeText(p.transform, label, 16, new Vector2(-210f, y), new Vector2(150f, 30f))
            .alignment = TextAlignmentOptions.MidlineLeft;

        int n = labels.Length;
        const float areaLeft = -60f, areaRight = 305f, gap = 6f, h = 32f;
        float bw = (areaRight - areaLeft - gap * (n - 1)) / n;
        var btns = new Button[n];
        for (int i = 0; i < n; i++)
        {
            int idx = i;
            float cx = areaLeft + bw * 0.5f + (bw + gap) * i;
            var btn = MakeButton(p.transform, labels[i], new Vector2(cx, y), new Vector2(bw, h),
                () => onSelect(idx, btns), 13);
            btn.GetComponent<Image>().color = (current == i) ? TabActive : TabInactive;
            btns[i] = btn;
        }
        return btns;
    }

    /// <summary>세그먼트 버튼 그룹의 선택 하이라이트 갱신.</summary>
    static void RefreshSegment(Button[] btns, int selected)
    {
        if (btns == null) return;
        for (int i = 0; i < btns.Length; i++)
            if (btns[i] != null)
                btns[i].GetComponent<Image>().color = (i == selected) ? TabActive : TabInactive;
    }

    void SaveSlotRow(GameObject p, int slot, float y)
    {
        var data = SaveManager.Instance?.LoadSaveData(slot);
        string info = data != null
            ? $"슬롯 {slot + 1}  {data.sceneName}  {FormatPlayTime(data.playTime)}"
            : $"슬롯 {slot + 1}  —  비어 있음";

        MakeText(p.transform, info, 14, new Vector2(-80f, y + 8f), new Vector2(310f, 28f))
            .alignment = TextAlignmentOptions.MidlineLeft;

        int s = slot;
        var saveBtn = MakeButton(p.transform, "저장", new Vector2(130f, y + 8f), new Vector2(72f, 30f),
            () => { SaveManager.Instance?.SaveGame(s); });
        saveBtn.GetComponent<Image>().color = new Color(0.18f, 0.45f, 0.18f, 1f);

        var loadBtn = MakeButton(p.transform, "불러오기", new Vector2(212f, y + 8f), new Vector2(80f, 30f),
            () => { SaveManager.Instance?.LoadGame(s); Hide(); });
        loadBtn.GetComponent<Image>().color = new Color(0.18f, 0.28f, 0.42f, 1f);

        var delBtn = MakeButton(p.transform, "삭제", new Vector2(302f, y + 8f), new Vector2(60f, 30f),
            () => ShowDeleteConfirm(s));
        delBtn.GetComponent<Image>().color = DangerCol;

        // 슬롯 배경 라인
        var line = MakeImage(p.transform, "SlotLine", new Color(0.25f, 0.25f, 0.28f, 1f));
        line.rectTransform.anchoredPosition = new Vector2(0f, y - 18f);
        line.rectTransform.sizeDelta        = new Vector2(620f, 1f);
    }

    void LangButtons(GameObject p, float y, bool isDialogue)
    {
        var labels = new[] { "한국어", "English", "日本語" };
        var langs  = new[] {
            LocalizationManager.Language.KO,
            LocalizationManager.Language.EN,
            LocalizationManager.Language.JP
        };
        var sm = SettingsManager.Instance;
        var cur = isDialogue ? (sm?.dialogueLanguage ?? LocalizationManager.Language.KO)
                             : (sm?.language        ?? LocalizationManager.Language.KO);

        var btns = new Button[3];
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            var lbl = labels[i]; var lang = langs[i];
            var btn = MakeButton(p.transform, lbl,
                new Vector2(-160f + 160f * i, y), new Vector2(140f, 44f),
                () =>
                {
                    if (isDialogue) SettingsManager.Instance?.SetDialogueLanguage(lang);
                    else            SettingsManager.Instance?.SetLanguage(lang);
                    RefreshSegment(btns, idx);   // 클릭 즉시 선택 하이라이트 갱신
                }, 20);
            btn.GetComponent<Image>().color = (cur == langs[i]) ? TabActive : TabInactive;
            btns[i] = btn;
        }
    }

    void StartRebind(int slot)
    {
        _rebindingSlot = slot;
    }

    void Update()
    {
        if (_rebindingSlot < 0) return;
        if (!Input.anyKeyDown) return;
        foreach (KeyCode kc in System.Enum.GetValues(typeof(KeyCode)))
        {
            if (kc == KeyCode.Mouse0 || kc == KeyCode.Mouse1) continue;
            if (!Input.GetKeyDown(kc)) continue;
            if (kc == KeyCode.Escape) { _rebindingSlot = -1; _rebindEndFrame = Time.frameCount; return; }
            ApplyRebind(_rebindingSlot, kc);
            _rebindingSlot = -1;
            _rebindEndFrame = Time.frameCount;
            return;
        }
    }

    void ApplyRebind(int slot, KeyCode kc)
    {
        var sm = SettingsManager.Instance;
        if (sm == null) return;

        // 중복 검사: 같은 키가 다른 슬롯에 배정돼 있으면 두 슬롯의 키를 스왑
        KeyCode oldKey = GetSlotKey(sm, slot);
        for (int other = 0; other < 7; other++)
        {
            if (other == slot) continue;
            if (GetSlotKey(sm, other) == kc)
            {
                SetSlotKey(sm, other, oldKey);
                break;
            }
        }
        SetSlotKey(sm, slot, kc);

        // 컨트롤 탭 새로고침
        RebuildTab(2);
    }

    static KeyCode GetSlotKey(SettingsManager sm, int slot) => slot switch
    {
        0 => sm.keyInteract,
        1 => sm.keyInventory,
        2 => sm.keyDagger,
        3 => sm.keyPause,
        4 => sm.keyDialogueSkip,
        5 => sm.keyDialogueLog,
        6 => sm.keyQuickSave,
        _ => KeyCode.None
    };

    static void SetSlotKey(SettingsManager sm, int slot, KeyCode k)
    {
        switch (slot)
        {
            case 0: sm.SetKeyInteract(k);     break;
            case 1: sm.SetKeyInventory(k);    break;
            case 2: sm.SetKeyDagger(k);       break;
            case 3: sm.SetKeyPause(k);        break;
            case 4: sm.SetKeyDialogueSkip(k); break;
            case 5: sm.SetKeyDialogueLog(k);  break;
            case 6: sm.SetKeyQuickSave(k);    break;
        }
    }

    void RebuildTab(int idx)
    {
        if (_tabPanels == null || idx >= _tabPanels.Length) return;
        var old = _tabPanels[idx];
        Transform parent = old.transform.parent;
        Destroy(old);
        switch (idx)
        {
            case 2: _tabPanels[idx] = BuildControlsTab(parent); break;
            case 4: _tabPanels[idx] = BuildSaveTab(parent);     break;
        }
        bool active = (_currentTab == idx);
        _tabPanels[idx].SetActive(active);
    }

    void ShowResetConfirm()
    {
        ShowConfirmPopup("모든 저장 데이터와 설정이 초기화됩니다.\n정말 진행하시겠습니까?",
            () =>
            {
                // ResetAllSettings() 내부에서 PlayerPrefs.DeleteAll() 을 수행하므로 여기선 중복 호출하지 않는다.
                SaveManager.Instance?.DeleteAllSlots();
                SettingsManager.Instance?.ResetAllSettings();
                RebuildAllTabs();   // 초기화된 기본값을 열려 있는 패널에 즉시 반영
            });
    }

    void ShowDeleteConfirm(int slot)
    {
        ShowConfirmPopup($"슬롯 {slot + 1}의 데이터를 삭제하시겠습니까?",
            () =>
            {
                SaveManager.Instance?.DeleteSlot(slot);
                RebuildTab(4);
            });
    }

    void ShowConfirmPopup(string message, System.Action onConfirm)
    {
        if (_confirmPopup != null) Destroy(_confirmPopup);

        var root = new GameObject("ConfirmPopup");
        root.transform.SetParent(transform, false);
        _confirmPopup = root;

        var bg = MakeImage(root.transform, "Bg", new Color(0.08f, 0.08f, 0.10f, 0.98f));
        var bgR = bg.rectTransform;
        bgR.anchorMin = bgR.anchorMax = new Vector2(0.5f, 0.5f);
        bgR.sizeDelta = new Vector2(460f, 180f);
        bgR.anchoredPosition = Vector2.zero;

        var pt = bg.transform;
        var msg = MakeText(pt, message, 17, new Vector2(0f, 40f), new Vector2(400f, 70f));
        msg.alignment = TextAlignmentOptions.Top;

        MakeButton(pt, "확인", new Vector2(-80f, -52f), new Vector2(130f, 40f), () =>
        {
            onConfirm?.Invoke();
            Destroy(_confirmPopup);
            _confirmPopup = null;
        }).GetComponent<Image>().color = DangerCol;

        MakeButton(pt, "취소", new Vector2(80f, -52f), new Vector2(130f, 40f), () =>
        {
            Destroy(_confirmPopup);
            _confirmPopup = null;
        });
    }

    void SwitchTab(int idx)
    {
        // 탭 버튼 색
        if (_tabButtons != null)
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                if (_tabButtons[i] != null)
                    _tabButtons[i].GetComponent<Image>().color = (i == idx) ? TabActive : TabInactive;
            }
        // 패널 활성/비활성
        if (_tabPanels != null)
            for (int i = 0; i < _tabPanels.Length; i++)
                if (_tabPanels[i] != null) _tabPanels[i].SetActive(i == idx);

        _currentTab = idx;
    }

    void RebuildAllTabs()
    {
        // 패널을 열거나 초기화한 뒤, 모든 탭을 현재 SettingsManager 값 기준으로 다시 빌드한다.
        // (값 in-place 갱신 대신 탭 패널 전체를 재생성하는 단순 구현)
        if (_tabPanels == null) return;
        for (int i = 0; i < _tabPanels.Length; i++)
        {
            Transform parent = _tabPanels[i].transform.parent;
            bool active      = (_currentTab == i);
            Destroy(_tabPanels[i]);
            switch (i)
            {
                case 0: _tabPanels[i] = BuildSoundTab(parent);       break;
                case 1: _tabPanels[i] = BuildScreenTab(parent);      break;
                case 2: _tabPanels[i] = BuildControlsTab(parent);    break;
                case 3: _tabPanels[i] = BuildAccessibilityTab(parent);break;
                case 4: _tabPanels[i] = BuildSaveTab(parent);        break;
                case 5: _tabPanels[i] = BuildLanguageTab(parent);    break;
                case 6: _tabPanels[i] = BuildGameplayTab(parent);    break;
            }
            _tabPanels[i].SetActive(active);
        }
    }

    static string KeyLabel(KeyCode kc)
    {
        return kc switch
        {
            KeyCode.Alpha0 => "0", KeyCode.Alpha1 => "1", KeyCode.Alpha2 => "2",
            KeyCode.Alpha3 => "3", KeyCode.Alpha4 => "4", KeyCode.Alpha5 => "5",
            KeyCode.Alpha6 => "6", KeyCode.Alpha7 => "7", KeyCode.Alpha8 => "8",
            KeyCode.Alpha9 => "9",
            KeyCode.Return => "Enter", KeyCode.Escape => "ESC",
            KeyCode.Space  => "Space", KeyCode.Tab => "Tab",
            KeyCode.LeftShift => "L.Shift", KeyCode.RightShift => "R.Shift",
            _ => kc.ToString()
        };
    }

    static string FormatPlayTime(float seconds)
    {
        int h = (int)(seconds / 3600);
        int m = (int)(seconds % 3600 / 60);
        return h > 0 ? $"{h}시간 {m:D2}분" : $"{m}분";
    }

    // ─── FadeIn ───────────────────────────────────────────────────────────
    IEnumerator FadeIn()
    {
        float t = 0f;
        while (t < 0.15f)
        {
            t += Time.unscaledDeltaTime;
            _cg.alpha = Mathf.Clamp01(t / 0.15f);
            yield return null;
        }
        _cg.alpha = 1f;
        _cg.blocksRaycasts = true;
    }

    // ─── 기본 UI 헬퍼 ─────────────────────────────────────────────────────
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

    /// <summary>
    /// 해상도 드롭다운을 생성합니다.
    /// Screen.resolutions에서 고유한 해상도 목록을 구성하고,
    /// 현재 해상도를 초기 선택값으로 설정합니다.
    /// </summary>
    static void MakeResolutionDropdown(GameObject p, float y)
    {
        MakeText(p.transform, "해상도", 16, new Vector2(-210f, y), new Vector2(200f, 30f))
            .alignment = TextAlignmentOptions.MidlineLeft;

        // 드롭다운 루트
        var go  = new GameObject("ResolutionDropdown");
        go.transform.SetParent(p.transform, false);
        var rt  = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(105f, y);
        rt.sizeDelta = new Vector2(260f, 30f);

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.22f, 0.22f, 0.25f, 1f);

        var dropdown = go.AddComponent<TMP_Dropdown>();

        // 라벨
        var labelGo  = new GameObject("Label");
        labelGo.transform.SetParent(go.transform, false);
        var labelTxt = labelGo.AddComponent<TextMeshProUGUI>();
        labelTxt.fontSize  = 14;
        labelTxt.color     = Color.white;
        labelTxt.alignment = TextAlignmentOptions.MidlineLeft;
        var labelRt  = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero; labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = new Vector2(8f, 0f); labelRt.offsetMax = Vector2.zero;

        // 화살표
        var arrowGo  = new GameObject("Arrow");
        arrowGo.transform.SetParent(go.transform, false);
        var arrowTxt = arrowGo.AddComponent<TextMeshProUGUI>();
        arrowTxt.text      = "▼";
        arrowTxt.fontSize  = 12;
        arrowTxt.color     = Color.white;
        arrowTxt.alignment = TextAlignmentOptions.Center;
        var arrowRt  = arrowGo.GetComponent<RectTransform>();
        arrowRt.anchorMin = new Vector2(1f, 0f); arrowRt.anchorMax = new Vector2(1f, 1f);
        arrowRt.sizeDelta = new Vector2(20f, 0f);
        arrowRt.anchoredPosition = new Vector2(-10f, 0f);

        dropdown.captionText     = labelTxt;
        dropdown.targetGraphic   = bg;

        // 옵션 목록 템플릿 (없으면 TMP_Dropdown.Show()가 에러만 내고 열리지 않음)
        dropdown.template = BuildDropdownTemplate(go.transform, out TMP_Text itemLabel);
        dropdown.itemText = itemLabel;

        // 해상도 목록 구성 (중복 제거)
        var resolutions = Screen.resolutions;
        var options = new System.Collections.Generic.List<TMP_Dropdown.OptionData>();
        int currentIndex = 0;
        int w = Screen.width, h = Screen.height;

        var seen = new System.Collections.Generic.HashSet<string>();
        for (int i = resolutions.Length - 1; i >= 0; i--)
        {
            var r = resolutions[i];
            string key = $"{r.width}x{r.height}";
            if (!seen.Add(key)) continue;
            options.Add(new TMP_Dropdown.OptionData($"{r.width} × {r.height}"));
            if (r.width == w && r.height == h)
                currentIndex = options.Count - 1;
        }
        if (options.Count == 0)
            options.Add(new TMP_Dropdown.OptionData($"{w} × {h}"));

        dropdown.ClearOptions();
        dropdown.AddOptions(options);
        dropdown.value = currentIndex;
        dropdown.RefreshShownValue();

        dropdown.onValueChanged.AddListener(idx =>
        {
            // 선택된 해상도 텍스트를 파싱해 적용
            string opt = dropdown.options[idx].text.Replace(" ", "").Replace("×", "x");
            var parts  = opt.Split('x');
            if (parts.Length == 2 &&
                int.TryParse(parts[0], out int nw) &&
                int.TryParse(parts[1], out int nh))
            {
                // SettingsManager 경유로 적용 + PlayerPrefs 저장(재시작 시 복원)
                SettingsManager.Instance?.SetResolution(nw, nh);
            }
        });
    }

    /// <summary>
    /// TMP_Dropdown이 요구하는 옵션 목록 템플릿 계층을 생성합니다.
    /// Template(ScrollRect) → Viewport(Mask) → Content → Item(Toggle) + Background/Checkmark/Label
    /// </summary>
    static RectTransform BuildDropdownTemplate(Transform dropdownRoot, out TMP_Text itemLabel)
    {
        var tmplGo = new GameObject("Template");
        tmplGo.transform.SetParent(dropdownRoot, false);
        var tmplRt = tmplGo.AddComponent<RectTransform>();
        tmplRt.anchorMin        = new Vector2(0f, 0f);
        tmplRt.anchorMax        = new Vector2(1f, 0f);
        tmplRt.pivot            = new Vector2(0.5f, 1f);
        tmplRt.anchoredPosition = new Vector2(0f, 2f);
        tmplRt.sizeDelta        = new Vector2(0f, 150f);
        var tmplImg = tmplGo.AddComponent<Image>();
        tmplImg.color = new Color(0.15f, 0.15f, 0.17f, 1f);
        var scroll = tmplGo.AddComponent<ScrollRect>();

        var viewportGo = new GameObject("Viewport");
        viewportGo.transform.SetParent(tmplGo.transform, false);
        var viewportRt = viewportGo.AddComponent<RectTransform>();
        viewportRt.anchorMin = Vector2.zero; viewportRt.anchorMax = Vector2.one;
        viewportRt.offsetMin = viewportRt.offsetMax = Vector2.zero;
        viewportRt.pivot     = new Vector2(0f, 1f);
        var vpImg = viewportGo.AddComponent<Image>();
        vpImg.color = Color.white;
        var mask = viewportGo.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        var contentGo = new GameObject("Content");
        contentGo.transform.SetParent(viewportGo.transform, false);
        var contentRt = contentGo.AddComponent<RectTransform>();
        contentRt.anchorMin        = new Vector2(0f, 1f);
        contentRt.anchorMax        = new Vector2(1f, 1f);
        contentRt.pivot            = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta        = new Vector2(0f, 30f);

        var itemGo = new GameObject("Item");
        itemGo.transform.SetParent(contentGo.transform, false);
        var itemRt = itemGo.AddComponent<RectTransform>();
        itemRt.anchorMin = new Vector2(0f, 0.5f);
        itemRt.anchorMax = new Vector2(1f, 0.5f);
        itemRt.sizeDelta = new Vector2(0f, 30f);
        var itemToggle = itemGo.AddComponent<Toggle>();

        var itemBg = MakeImage(itemGo.transform, "Item Background", new Color(0.22f, 0.22f, 0.25f, 1f));
        Stretch(itemBg.rectTransform);

        var itemCheck = MakeImage(itemGo.transform, "Item Checkmark", SliderFill);
        var checkRt = itemCheck.rectTransform;
        checkRt.anchorMin = checkRt.anchorMax = new Vector2(0f, 0.5f);
        checkRt.sizeDelta        = new Vector2(8f, 8f);
        checkRt.anchoredPosition = new Vector2(10f, 0f);

        var labelGo = new GameObject("Item Label");
        labelGo.transform.SetParent(itemGo.transform, false);
        itemLabel = labelGo.AddComponent<TextMeshProUGUI>();
        itemLabel.fontSize  = 14;
        itemLabel.color     = Color.white;
        itemLabel.alignment = TextAlignmentOptions.MidlineLeft;
        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero; labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = new Vector2(24f, 0f); labelRt.offsetMax = new Vector2(-8f, 0f);

        itemToggle.targetGraphic = itemBg;
        itemToggle.graphic       = itemCheck;
        itemToggle.isOn          = true;

        scroll.content           = contentRt;
        scroll.viewport          = viewportRt;
        scroll.horizontal        = false;
        scroll.vertical          = true;
        scroll.movementType      = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 20f;

        tmplGo.SetActive(false); // TMP_Dropdown이 Show/Hide 시 직접 활성화를 제어
        return tmplRt;
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

    static Toggle MakeToggle(Transform p, Vector2 pos, Vector2 sz, bool value)
    {
        var go  = new GameObject("Toggle"); go.transform.SetParent(p, false);
        var r   = go.AddComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos; r.sizeDelta = sz;

        var bg  = MakeImage(go.transform, "Background", new Color(0.25f, 0.25f, 0.25f));
        Stretch(bg.rectTransform);

        var checkmark = MakeImage(bg.transform, "Checkmark", new Color(0.30f, 0.75f, 0.30f));
        checkmark.rectTransform.anchorMin = new Vector2(0.1f, 0.1f);
        checkmark.rectTransform.anchorMax = new Vector2(0.9f, 0.9f);
        checkmark.rectTransform.offsetMin = checkmark.rectTransform.offsetMax = Vector2.zero;

        var tg            = go.AddComponent<Toggle>();
        tg.targetGraphic  = bg;
        tg.graphic        = checkmark;
        tg.isOn           = value;
        return tg;
    }

    static Slider MakeSlider(Transform p, Vector2 pos, Vector2 sz, float value,
        float min = 0f, float max = 1f)
    {
        var go = new GameObject("Slider"); go.transform.SetParent(p, false);
        var r  = go.AddComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos; r.sizeDelta = sz;

        var bgImg = MakeImage(go.transform, "Background", new Color(0.28f, 0.28f, 0.30f));
        Stretch(bgImg.rectTransform);

        var fa = new GameObject("Fill Area"); fa.transform.SetParent(go.transform, false);
        var far = fa.AddComponent<RectTransform>();
        far.anchorMin = new Vector2(0f, 0.25f); far.anchorMax = new Vector2(1f, 0.75f);
        far.offsetMin = new Vector2(5f, 0f);    far.offsetMax = new Vector2(-15f, 0f);

        var fillImg = MakeImage(fa.transform, "Fill", SliderFill);
        var fillR   = fillImg.rectTransform;
        fillR.anchorMin = Vector2.zero; fillR.anchorMax = Vector2.one;
        fillR.offsetMin = fillR.offsetMax = Vector2.zero;

        var ha  = new GameObject("Handle Slide Area"); ha.transform.SetParent(go.transform, false);
        var har = ha.AddComponent<RectTransform>();
        har.anchorMin = Vector2.zero; har.anchorMax = Vector2.one;
        har.offsetMin = new Vector2(10f, 0f); har.offsetMax = new Vector2(-10f, 0f);

        var handleImg = MakeImage(ha.transform, "Handle", Color.white);
        var handleR   = handleImg.rectTransform;
        handleR.anchorMin = new Vector2(0f, 0f); handleR.anchorMax = new Vector2(0f, 1f);
        handleR.sizeDelta = new Vector2(18f, 0f);

        var slider           = go.AddComponent<Slider>();
        slider.fillRect      = fillR;
        slider.handleRect    = handleR;
        slider.targetGraphic = handleImg;
        slider.direction     = Slider.Direction.LeftToRight;
        slider.minValue      = min;
        slider.maxValue      = max;
        slider.value         = value;
        return slider;
    }
}
