using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Yarn.Unity;

public class YarnCommandBridge : MonoBehaviour
{
    [Header("Yarn Spinner 연결")]
    [SerializeField] private DialogueRunner          dialogueRunner;
    [SerializeField] private InMemoryVariableStorage variableStorage;

    [Header("대화 패널 루트")]
    [SerializeField] private GameObject dialoguePanel;

    [Header("대화 시작 시 초기화 대상 (씬 오버라이드 대응)")]
    [SerializeField] private TMP_Text       lineBodyText;          // BodyText TMP — 기본 텍스트 제거용
    [SerializeField] private GameObject     characterNameContainer; // NameText 오브젝트

    [Header("포트레이트 UI (Left / Right)")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private Image portraitImageRight;

    [Header("스프라이트 데이터")]
    [SerializeField] private CharacterSpriteData spriteData;

    /// <summary>
    /// 자막 본문 TMP. EavesdropAttenuator 처럼 자막을 흐리게 만드는 쪽에서 쓴다.
    /// TMP_Text.alpha 는 LinePresenter 가 쓰는 CanvasGroup.alpha 와 곱해지는 별개 채널이라,
    /// 여기에 써도 대사 페이드인 연출과 충돌하지 않는다.
    /// </summary>
    public static TMP_Text LineBodyText => _instance != null ? _instance.lineBodyText : null;

    [Header("설정 연동 (선택 — 비우면 런타임 자동 탐색)")]
    [Tooltip("대사 자동 진행/속도 제어용. 비워두면 자식에서 자동 탐색합니다.")]
    [SerializeField] private LinePresenter linePresenter;
    [Tooltip("텍스트 배경 불투명도 제어용. 비워두면 대화 패널 루트의 Image를 사용합니다.")]
    [SerializeField] private Image dialogueBackground;

    // 설정 적용 기준값 (프리팹 기본값을 배율/역수의 기준으로 캐시)
    private float _baseFontSize;
    private int   _baseLettersPerSecond = 60;
    private float _baseAutoAdvanceDelay = 1f;
    // 텍스트 크기 배율 (0=소, 1=중, 2=대). 중=프리팹 기본 크기 유지.
    private static readonly float[] TextSizeScale = { 0.85f, 1f, 1.25f };

    // ── Yarn Spinner 3.x: [YarnCommand] 인스턴스 메서드는 첫 번째 인자를 ──────
    // GameObject 이름으로 해석하므로, static 메서드 + _instance 패턴을 사용한다.
    private static YarnCommandBridge _instance;

    // ── 스프라이트 캐시 ───────────────────────────────────────────────────
    private static readonly Dictionary<string, Sprite> _spriteCache =
        new Dictionary<string, Sprite>();

    // 스프라이트 누락 경고를 키당 1회만 내보내기 위한 기록 (대사 줄마다 도배 방지)
    private static readonly HashSet<string> _missingSpriteWarned =
        new HashSet<string>();

    // ── Yarn 변수 키 ──────────────────────────────────────────────────────
    private const string VAR_GAUGE      = "$심리게이지";
    private const string VAR_CORRUPTION = "$인형화";
    private const string VAR_RESOLVE    = "$결심";
    private const string VAR_NAME       = "$이름";

    // ── 포트레이트 상태 ───────────────────────────────────────────────────
    private RectTransform _portraitRT;
    private RectTransform _portraitRightRT;
    private Vector2       _portraitRestPos;
    private Vector2       _portraitRightRestPos;
    private string        _leftCharacter;
    private string        _rightCharacter;
    private Coroutine     _leftAnimCoroutine;
    private Coroutine     _rightAnimCoroutine;

    // ── 라이프사이클 ──────────────────────────────────────────────────────
    private void Awake()
    {
        _instance = this;

        YarnDialogue.Register(dialogueRunner);
        dialogueRunner.onDialogueStart.AddListener(OnDialogueStart);
        dialogueRunner.onDialogueComplete.AddListener(OnDialogueComplete);

        // 대화 로그 기록 프레젠터를 런타임 등록 (씬/프리팹 편집 불필요)
        var logRecorder = dialogueRunner.GetComponent<DialogueLogRecorder>()
                          ?? dialogueRunner.gameObject.AddComponent<DialogueLogRecorder>();
        if (!System.Linq.Enumerable.Contains(dialogueRunner.DialoguePresenters, logRecorder))
            dialogueRunner.DialoguePresenters =
                System.Linq.Enumerable.Append(dialogueRunner.DialoguePresenters, logRecorder);

        SettingsManager.OnDialogueLanguageChanged += ApplyDialogueLanguage;
        if (SettingsManager.Instance != null)
            ApplyDialogueLanguage(SettingsManager.Instance.dialogueLanguage);

        // 대사 표시 설정 이벤트 구독 (실제 적용은 Start에서 — LinePresenter.Awake 이후)
        SettingsManager.OnAutoDialogueChanged   += OnAutoDialogueChanged;
        SettingsManager.OnDialogueSpeedChanged  += ApplyDialogueSpeed;
        SettingsManager.OnTextSizeChanged       += ApplyTextSize;
        SettingsManager.OnTextBgOpacityChanged  += ApplyTextBgOpacity;

        // 씬 단위 인스턴스이므로 Awake 시점에 static 캐시를 초기화한다.
        _spriteCache.Clear();
        _missingSpriteWarned.Clear();

        // 포트레이트 RectTransform 및 기준 위치 캐싱
        if (portraitImage != null)
        {
            _portraitRT      = portraitImage.GetComponent<RectTransform>();
            _portraitRestPos = _portraitRT != null ? _portraitRT.anchoredPosition : Vector2.zero;
        }
        if (portraitImageRight != null)
        {
            _portraitRightRT      = portraitImageRight.GetComponent<RectTransform>();
            _portraitRightRestPos = _portraitRightRT != null ? _portraitRightRT.anchoredPosition : Vector2.zero;
        }
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
            YarnDialogue.Register(null);
        }
        if (dialogueRunner != null)
        {
            dialogueRunner.onDialogueStart.RemoveListener(OnDialogueStart);
            dialogueRunner.onDialogueComplete.RemoveListener(OnDialogueComplete);
        }
        SettingsManager.OnDialogueLanguageChanged -= ApplyDialogueLanguage;

        SettingsManager.OnAutoDialogueChanged   -= OnAutoDialogueChanged;
        SettingsManager.OnDialogueSpeedChanged  -= ApplyDialogueSpeed;
        SettingsManager.OnTextSizeChanged       -= ApplyTextSize;
        SettingsManager.OnTextBgOpacityChanged  -= ApplyTextBgOpacity;
    }

    // ── 대사 표시 설정 적용 ────────────────────────────────────────────────
    private void Start()
    {
        // LinePresenter/배경 참조 확보 + 프리팹 기본값 캐시 (모든 Awake 이후)
        if (linePresenter == null)
            linePresenter = GetComponentInChildren<LinePresenter>(true);
        if (dialogueBackground == null && dialoguePanel != null)
            dialogueBackground = dialoguePanel.GetComponent<Image>();

        if (lineBodyText != null)
            _baseFontSize = lineBodyText.fontSize;
        if (linePresenter != null)
        {
            _baseLettersPerSecond = Mathf.Max(1, linePresenter.lettersPerSecond);
            _baseAutoAdvanceDelay = Mathf.Max(0.01f, linePresenter.autoAdvanceDelay);
        }

        // 저장된 설정 즉시 반영
        var sm = SettingsManager.Instance;
        if (sm == null) return;
        OnAutoDialogueChanged(sm.autoDialogue);
        ApplyDialogueSpeed(sm.dialogueSpeed);
        ApplyTextSize(sm.textSize);
        ApplyTextBgOpacity(sm.textBgOpacity);
    }

    private void OnAutoDialogueChanged(bool enabled)
    {
        if (linePresenter != null) linePresenter.autoAdvance = enabled;
    }

    private void ApplyDialogueSpeed(float speed)
    {
        if (linePresenter == null) return;
        if (speed <= 0f) speed = 1f;

        int lps = Mathf.Max(1, Mathf.RoundToInt(_baseLettersPerSecond * speed));
        linePresenter.lettersPerSecond = lps;
        // Awake에서 1회 생성된 타자기 인스턴스에도 즉시 반영 (RunTypewriter가 매 라인 시작 시 값을 읽음)
        if (linePresenter.Typewriter is LetterTypewriter lt)
            lt.CharactersPerSecond = lps;
        // 자동 진행 대기시간은 속도에 반비례 (빠를수록 짧게)
        linePresenter.autoAdvanceDelay = _baseAutoAdvanceDelay / speed;
    }

    private void ApplyTextSize(int size)
    {
        if (lineBodyText == null || _baseFontSize <= 0f) return;
        size = Mathf.Clamp(size, 0, TextSizeScale.Length - 1);
        lineBodyText.fontSize = _baseFontSize * TextSizeScale[size];
    }

    private void ApplyTextBgOpacity(float alpha)
    {
        if (dialogueBackground == null) return;
        var c = dialogueBackground.color;
        c.a = Mathf.Clamp01(alpha);
        dialogueBackground.color = c;
    }

    // ── 대사 진행 입력 ────────────────────────────────────────────────────
    private void Update()
    {
        if (dialogueRunner == null || !dialogueRunner.IsDialogueRunning) return;

        // 로그/설정/거래 패널이 열려 있으면 패널 위 클릭·키 입력이 대사를 진행시키지 않게 차단
        if (DialogueLogUI.IsOpen || SettingsPanelUI.IsOpen || SolTradeUI.IsOpen) return;

        var key = SettingsManager.Instance != null
            ? SettingsManager.Instance.keyDialogueSkip
            : KeyCode.Space;

        if (Input.GetKeyDown(key) || Input.GetMouseButtonDown(0))
        {
            dialogueRunner.RequestHurryUpLine();
            dialogueRunner.RequestNextLine();
        }
    }

    // ── 언어 설정 ─────────────────────────────────────────────────────────
    // Yarn Spinner 3.x: textLanguage 프로퍼티 삭제됨. 언어는 YarnProject 설정으로 관리.
    private void ApplyDialogueLanguage(LocalizationManager.Language lang) { }

    // ── 대화 시작: C# → Yarn 변수 주입 ──────────────────────────────────
    private void OnDialogueStart()
    {
        // 씬 오버라이드(m_IsActive:1)로 Dialogue 루트 활성화 시 함께 보이는 요소들 즉시 초기화
        if (portraitImage != null)       portraitImage.gameObject.SetActive(false);
        if (portraitImageRight != null)  portraitImageRight.gameObject.SetActive(false);
        if (lineBodyText != null)        lineBodyText.text = "";
        if (characterNameContainer != null) characterNameContainer.SetActive(false);

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(true);
            var cg = dialoguePanel.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 0f;
        }
        DialogueEvents.RaiseStarted();

        // 플레이어가 정한 주인공 이름. 대사에서는 {$이름} 으로 참조한다.
        variableStorage.SetValue(VAR_NAME, PlayerIdentity.Name);

        if (GaugeManager.Instance != null)
            variableStorage.SetValue(VAR_GAUGE, GaugeManager.Instance.fantasyRealityGauge);

        if (CorruptionManager.Instance != null)
            variableStorage.SetValue(VAR_CORRUPTION, CorruptionManager.Instance.currentCorruption);

        variableStorage.SetValue(VAR_RESOLVE, GameState.isResolved);

        if (FlagManager.Instance != null)
        {
            variableStorage.SetValue("$상인_광장_만남", FlagManager.Instance.GetFlag("상인_광장_만남"));
            variableStorage.SetValue("$꽃집_탐색",      FlagManager.Instance.GetFlag("꽃집_탐색"));
            variableStorage.SetValue("$빵반죽_획득",     FlagManager.Instance.GetFlag("빵반죽_획득"));
            variableStorage.SetValue("$쿠루_합류",       FlagManager.Instance.GetFlag("쿠루_합류"));
        }
    }

    // ── 대화 종료: Yarn 변수 → C# 반영 ──────────────────────────────────
    private void OnDialogueComplete()
    {
        if (variableStorage.TryGetValue(VAR_GAUGE, out float gauge) && GaugeManager.Instance != null)
            GaugeManager.Instance.SetGaugeValue(gauge);

        if (variableStorage.TryGetValue(VAR_RESOLVE, out bool resolved))
            GameState.isResolved = resolved;

        if (FlagManager.Instance != null)
        {
            if (variableStorage.TryGetValue("$상인_광장_만남", out bool b0)) FlagManager.Instance.SetFlag("상인_광장_만남", b0);
            if (variableStorage.TryGetValue("$꽃집_탐색",      out bool b1)) FlagManager.Instance.SetFlag("꽃집_탐색",      b1);
            if (variableStorage.TryGetValue("$빵반죽_획득",     out bool b2)) FlagManager.Instance.SetFlag("빵반죽_획득",     b2);
            if (variableStorage.TryGetValue("$쿠루_합류",       out bool b3)) FlagManager.Instance.SetFlag("쿠루_합류",       b3);
        }

        if (characterNameContainer != null) characterNameContainer.SetActive(false);
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        DialogueEvents.RaiseEnded();
    }

    // ── [YarnFunction] 주인공 이름 ────────────────────────────────────────
    // 조사 없는 자리는 {$이름} 변수를, 조사가 붙는 자리는 이 함수를 쓴다.
    //   {이름조사("가")}  →  "루가" / "민준이"
    // 인자는 받침 없는 형태로 넘긴다: 가 · 는 · 를 · 와 · 야 · 로 · 라
    [YarnFunction("이름조사")]
    public static string NameWithParticle(string particle)
        => PlayerIdentity.WithParticle(particle);

    // ── [YarnCommand] static 래퍼 ────────────────────────────────────────
    // Yarn Spinner 3.x: static 메서드만 첫 인자를 GameObject 이름으로 해석하지 않음.

    // <<showSprite "캐릭터" "감정" [side] [mode]>>
    [YarnCommand("showSprite")]
    public static IEnumerator ShowSprite(string character, string emotion,
                                         string side = "auto", string mode = "auto")
    {
        if (_instance == null) yield break;
        yield return _instance.ShowSpriteInternal(character, emotion, side, mode);
    }

    // <<hideSprite ["left"|"right"|"both"]>>
    [YarnCommand("hideSprite")]
    public static IEnumerator HideSprite(string side = "both")
    {
        if (_instance == null) yield break;
        yield return _instance.HideSpriteInternal(side);
    }

    // <<triggerGoodEnding>>
    [YarnCommand("triggerGoodEnding")]
    public static void TriggerGoodEnding() => EndingManager.TriggerGoodEnding();

    // <<applyTrigger "트리거이름">>
    [YarnCommand("applyTrigger")]
    public static void ApplyTrigger(string triggerName)
    {
        if (_instance == null)
        {
            Debug.LogWarning($"[YarnCommandBridge] ApplyTrigger '{triggerName}': instance null");
            return;
        }
        _instance.ApplyTriggerInternal(triggerName);
    }

    // ════════════════════════════════════════════════════════════════════
    // 추가 Yarn 커맨드 — 필터 · 오디오 · UI · 씬 전환 · 연출
    // ════════════════════════════════════════════════════════════════════

    // ── 필터 ─────────────────────────────────────────────────────────────

    // <<set_filter "fantasy"|"reality"|"none" [intensity=1.0]>>
    [YarnCommand("set_filter")]
    public static void SetFilter(string type, float intensity = 1f)
    {
        if (FilterManager.Instance == null) return;
        FilterType filterType = type.ToLowerInvariant() switch
        {
            "fantasy" => FilterType.Fantasy,
            "reality" => FilterType.Reality,
            _         => FilterType.None,
        };
        FilterManager.Instance.SetFilter(filterType, intensity);
    }

    // <<clear_filter>>
    [YarnCommand("clear_filter")]
    public static void ClearFilter()
        => FilterManager.Instance?.ClearFilter();

    // ── BGM ──────────────────────────────────────────────────────────────

    // <<play_bgm "clipName" [fadeIn=0]>>  (Resources 폴더 기준 경로)
    [YarnCommand("play_bgm")]
    public static void PlayBGM(string clipName, float fadeIn = 0f)
    {
        if (SFXManager.Instance == null) return;
        var clip = Resources.Load<AudioClip>(clipName);
        if (clip == null)
        {
            Debug.LogWarning($"[YarnCommand:play_bgm] AudioClip '{clipName}' not found in Resources.");
            return;
        }
        SFXManager.Instance.PlayBGM(clip, fadeIn);
    }

    // <<stop_bgm [fadeOut=0]>>
    [YarnCommand("stop_bgm")]
    public static void StopBGM(float fadeOut = 0f)
        => SFXManager.Instance?.StopBGM(fadeOut);

    // ── SFX ──────────────────────────────────────────────────────────────

    // <<play_sfx "soundName">>  (AudioManager.sounds에 등록된 이름)
    [YarnCommand("play_sfx")]
    public static void PlaySFX(string soundName)
        => AudioManager.Instance?.Play(soundName);

    // <<play_snap [dollification=-1]>>  (-1이면 현재 인형화 수치 자동 사용)
    [YarnCommand("play_snap")]
    public static void PlaySnap(float dollification = -1f)
    {
        if (SFXManager.Instance == null) return;
        float doll = dollification >= 0f
            ? dollification
            : CorruptionManager.Instance?.currentCorruption ?? 0f;
        SFXManager.Instance.PlaySnap(doll);
    }

    // ── 목표 UI ──────────────────────────────────────────────────────────

    // <<show_objective "header" "body">>
    [YarnCommand("show_objective")]
    public static void ShowObjective(string header, string body)
        => ObjectiveManager.Instance?.ShowObjective(header, body);

    // <<hide_objective>>
    [YarnCommand("hide_objective")]
    public static void HideObjective()
        => ObjectiveManager.Instance?.HideObjective();

    // ── 씬 전환 ──────────────────────────────────────────────────────────

    // <<scene_transition "sceneName">>
    [YarnCommand("scene_transition")]
    public static void SceneTransition(string sceneName)
        => TransitionManager.Instance?.DoSceneTransition(sceneName);

    // <<fade_to_black [duration=1.0]>>
    [YarnCommand("fade_to_black")]
    public static IEnumerator YarnFadeToBlack(float duration = 1f)
    {
        if (TransitionManager.Instance == null) yield break;
        yield return TransitionManager.Instance.StartCoroutine(
            TransitionManager.Instance.FadeToBlack(duration));
    }

    // <<fade_from_black [duration=1.0]>>
    [YarnCommand("fade_from_black")]
    public static IEnumerator YarnFadeFromBlack(float duration = 1f)
    {
        if (TransitionManager.Instance == null) yield break;
        yield return TransitionManager.Instance.StartCoroutine(
            TransitionManager.Instance.FadeFromBlack(duration));
    }

    // ── 글리치 ────────────────────────────────────────────────────────────

    // <<play_glitch [duration=1.0] [preset="mild"]>>
    // preset: "subtle" | "mild"(기본값) | "strong" | "crash"
    [YarnCommand("play_glitch")]
    public static void PlayGlitch(float duration = 1f, string preset = "")
    {
        if (GlitchManager.Instance == null) return;
        GlitchPreset p = preset.ToLowerInvariant() switch
        {
            "subtle" => GlitchManager.PresetSubtle,
            "strong" => GlitchManager.PresetStrong,
            "crash"  => GlitchManager.PresetCrash,
            _        => GlitchManager.PresetMild,
        };
        GlitchManager.Instance.PlayGlitch(duration, p);
    }

    // ── 화면 테두리 효과 ────────────────────────────────────────────────────

    // <<show_marshmallow [duration=1.5]>>
    [YarnCommand("show_marshmallow")]
    public static void ShowMarshmallow(float duration = 1.5f)
        => ScreenEdgeEffectController.ShowMarshmallow(duration);

    // <<show_heartbeat [duration=0.4]>>
    [YarnCommand("show_heartbeat")]
    public static void ShowHeartbeat(float duration = 0.4f)
        => ScreenEdgeEffectController.ShowHeartbeat(duration);

    // <<show_edge "gold"|"white"|"red" [duration=1.5]>>
    [YarnCommand("show_edge")]
    public static void ShowEdge(string preset, float duration = 1.5f)
    {
        Color c = preset.ToLowerInvariant() switch
        {
            "gold"  => new Color(1f,    0.84f, 0.20f, 0.55f),
            "white" => new Color(0.97f, 0.97f, 0.97f, 0.65f),
            "red"   => new Color(0.75f, 0.10f, 0.10f, 0.60f),
            _       => new Color(1f,    0.84f, 0.20f, 0.55f),  // 기본 gold
        };
        ScreenEdgeEffectController.ShowEdge(c, duration);
    }

    // ── 인형화 · 게이지 직접 변동 ─────────────────────────────────────────

    // <<add_corruption <delta>>>  (+이면 증가, -이면 감소)
    [YarnCommand("add_corruption")]
    public static void AddCorruption(float delta)
        => CorruptionManager.Instance?.AddCorruption(delta);

    // <<change_gauge <delta>>>  (+이면 현실 방향, -이면 환상 방향)
    [YarnCommand("change_gauge")]
    public static void ChangeGauge(float delta)
        => GaugeManager.Instance?.ChangeGauge(delta);

    // <<force_temp_reality>>  현실 100% 강제 후 tempForceDuration 초 뒤 복원
    // ※ 사용처였던 Forest_Road_Marshmallow 는 2026-08-16 정본 교체로 삭제됐다. 현재 호출처 없음.
    [YarnCommand("force_temp_reality")]
    public static void ForceTempReality()
        => GaugeManager.Instance?.ForceTempReality();

    // <<open_sol_trade "stockName" ["forest"]>>  Resources/SolStock/에서 로드 후 솔 거래창 열기
    // 두 번째 인자를 "forest" 로 주면 ForestTrade, 생략하거나 그 외면 VillageBrowse.
    // ※ 사용처였던 Shelter_Exit_Sol 은 2026-08-16 쉼터 데모 제외로 삭제됐다.
    //   마을 거래는 SolTradeInteraction 이 직접 연다.
    [YarnCommand("open_sol_trade")]
    public static void OpenSolTrade(string stockName, string mode = "village")
    {
        var stock = Resources.Load<SolStock>($"SolStock/{stockName}");
        if (stock == null)
        {
            Debug.LogWarning($"[YarnCommand:open_sol_trade] SolStock '{stockName}' 를 Resources 에서 찾을 수 없습니다.");
            return;
        }

        var tradeMode = mode != null && mode.Equals("forest", System.StringComparison.OrdinalIgnoreCase)
            ? TradeMode.ForestTrade
            : TradeMode.VillageBrowse;

        SolTradeUI.Instance?.Open(stock, tradeMode);
    }

    // ── 인스턴스 구현부 ───────────────────────────────────────────────────

    private IEnumerator ShowSpriteInternal(string character, string emotion,
                                            string side, string mode)
    {
        // 1. 배치 방향 결정
        bool useRight = ResolveSide(character, side);

        // 2. 감정 코드 결정
        string effectiveEmotion = emotion;
        if (!mode.Equals("fixed", System.StringComparison.OrdinalIgnoreCase) &&
            variableStorage != null &&
            variableStorage.TryGetValue(VAR_GAUGE, out float gauge) &&
            gauge >= 70f)
        {
            string realKey = $"{character}_{emotion}_real";
            if (!_spriteCache.TryGetValue(realKey, out Sprite realSprite) || realSprite == null)
            {
                _spriteCache.Remove(realKey);
                realSprite = Resources.Load<Sprite>($"Sprites/{realKey}");
                if (realSprite == null && spriteData != null)
                    realSprite = spriteData.GetSprite(character, emotion + "_real");
                if (realSprite != null)
                    _spriteCache[realKey] = realSprite;
            }
            if (realSprite != null)
                effectiveEmotion = emotion + "_real";
        }

        // 3. 스프라이트 로드 (stale 참조 체크 포함)
        string key = $"{character}_{effectiveEmotion}";
        if (!_spriteCache.TryGetValue(key, out Sprite sprite) || sprite == null)
        {
            _spriteCache.Remove(key);
            sprite = Resources.Load<Sprite>($"Sprites/{key}");
            if (sprite == null && spriteData != null)
                sprite = spriteData.GetSprite(character, effectiveEmotion);
            if (sprite != null)
                _spriteCache[key] = sprite;
        }

        // 3-1. 전투 중이면 필드 초상화를 띄우지 않는다.
        // 필드 대화창은 Dialogue 캔버스(sortingOrder 0)에 있고 BattleUI 는 100 이라
        // 여기서 켜 봐야 전투 UI 뒤에 깔려 보이지 않고, hideSprite 도 안 불려 켜진 채 남는다.
        // 동료 초상화는 BattleUI 안 동료 패널로 넘긴다.
        if (BattleSystem.Instance != null)
        {
            bool isLu = string.IsNullOrWhiteSpace(character)
                     || character == PlayerIdentity.Name
                     || character == "루" || character == "루독백";
            if (!isLu) BattleCompanionUI.Instance?.SetPortrait(sprite);
            yield break;
        }

        // 4. active / inactive 이미지 선택
        Image active   = useRight ? portraitImageRight : portraitImage;
        Image inactive = useRight ? portraitImage      : portraitImageRight;

        // 4a. 반대쪽 즉시 숨김
        if (inactive != null && inactive.gameObject.activeSelf)
        {
            StopAnim(!useRight);
            inactive.gameObject.SetActive(false);
            ResetPosition(inactive);
        }

        if (active == null) yield break;

        if (sprite == null)
        {
            // 여기서 조용히 넘어가면 포트레이트가 왜 안 뜨는지 알 방법이 없다.
            // 어느 캐릭터의 어느 감정이 비었는지 키당 1회만 남긴다.
            if (_missingSpriteWarned.Add(key))
                Debug.LogWarning(
                    $"[YarnCommandBridge] 스프라이트 없음: {key} — " +
                    $"Resources/Sprites/{key} 도 CharacterSpriteData 도 비어 있습니다. " +
                    $"추가 방법: Assets/Docs/포트레이트_추가방법.md");

            active.gameObject.SetActive(false);
            yield break;
        }

        // 5. 캐릭터 추적 업데이트
        if (useRight) _rightCharacter = character;
        else          _leftCharacter  = character;

        // 6. 진행 중인 애니메이션 중단
        StopAnim(useRight);

        // 7. 스프라이트 설정 + 등장 연출
        bool wasActive = active.gameObject.activeSelf;
        active.sprite = sprite;

        if (!wasActive)
        {
            var (motion, duration) = GetEntryParams(character);
            var co = StartCoroutine(EntryRoutine(active, motion, useRight, duration));
            if (useRight) _rightAnimCoroutine = co;
            else          _leftAnimCoroutine  = co;
            yield return co;
        }
    }

    private IEnumerator HideSpriteInternal(string side)
    {
        bool hideLeft  = !side.Equals("right", System.StringComparison.OrdinalIgnoreCase);
        bool hideRight = !side.Equals("left",  System.StringComparison.OrdinalIgnoreCase);

        float maxDuration = 0f;

        if (hideLeft && portraitImage != null && portraitImage.gameObject.activeSelf)
        {
            StopAnim(false);
            var (motion, duration) = GetExitParams(_leftCharacter);
            _leftCharacter     = null;
            _leftAnimCoroutine = StartCoroutine(ExitRoutine(portraitImage, motion, false, duration));
            maxDuration = Mathf.Max(maxDuration, duration);
        }

        if (hideRight && portraitImageRight != null && portraitImageRight.gameObject.activeSelf)
        {
            StopAnim(true);
            var (motion, duration) = GetExitParams(_rightCharacter);
            _rightCharacter     = null;
            _rightAnimCoroutine = StartCoroutine(ExitRoutine(portraitImageRight, motion, true, duration));
            maxDuration = Mathf.Max(maxDuration, duration);
        }

        if (maxDuration > 0f)
            yield return new WaitForSecondsRealtime(maxDuration);   // timeScale=0 대응
    }

    private void ApplyTriggerInternal(string triggerName)
    {
        if (GaugeManager.Instance == null)
        {
            Debug.LogWarning($"[YarnCommandBridge] ApplyTrigger '{triggerName}': GaugeManager 없음");
            return;
        }
        GaugeManager.Instance.ApplyTrigger(triggerName);
        if (variableStorage != null)
            variableStorage.SetValue(VAR_GAUGE, GaugeManager.Instance.fantasyRealityGauge);
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────────────

    bool ResolveSide(string character, string side)
    {
        if (side.Equals("right", System.StringComparison.OrdinalIgnoreCase)) return true;
        if (side.Equals("left",  System.StringComparison.OrdinalIgnoreCase)) return false;
        return PortraitRegistry.GetDefaultSide(character) == PortraitSide.Right;
    }

    void StopAnim(bool isRight)
    {
        if (isRight)
        {
            if (_rightAnimCoroutine != null) { StopCoroutine(_rightAnimCoroutine); _rightAnimCoroutine = null; }
        }
        else
        {
            if (_leftAnimCoroutine != null)  { StopCoroutine(_leftAnimCoroutine);  _leftAnimCoroutine  = null; }
        }
    }

    void ResetPosition(Image img)
    {
        RectTransform rt = img == portraitImageRight ? _portraitRightRT : _portraitRT;
        if (rt == null) return;
        rt.anchoredPosition = img == portraitImageRight ? _portraitRightRestPos : _portraitRestPos;
    }

    (EntryMotion motion, float duration) GetEntryParams(string characterId)
    {
        if (PortraitRegistry.TryGet(characterId, out var cfg))
            return (cfg.entryMotion, cfg.entryDuration);
        return (EntryMotion.SlideIn, 0.3f);
    }

    (ExitMotion motion, float duration) GetExitParams(string characterId)
    {
        if (characterId == null)                               return (ExitMotion.None,    0f);
        if (PortraitRegistry.TryGet(characterId, out var cfg)) return (cfg.exitMotion,     cfg.exitDuration);
        return (ExitMotion.SlideOut, 0.3f);
    }

    // ── 등장 애니메이션 ───────────────────────────────────────────────────
    // 시간은 unscaled 로 센다. 턴제 전투는 Time.timeScale=0 으로 도는데,
    // 스케일 시간을 쓰면 이 루프가 끝나지 않고 <<showSprite>> 가 대사를 통째로 붙잡는다.
    IEnumerator EntryRoutine(Image img, EntryMotion motion, bool isRight, float duration)
    {
        RectTransform rt      = img == portraitImageRight ? _portraitRightRT : _portraitRT;
        Vector2       restPos = img == portraitImageRight ? _portraitRightRestPos : _portraitRestPos;

        switch (motion)
        {
            case EntryMotion.SlideIn:
            {
                float   offset = (rt != null && rt.rect.width > 0f) ? rt.rect.width : 300f;
                Vector2 from   = restPos + new Vector2(isRight ? offset : -offset, 0f);
                if (rt != null) rt.anchoredPosition = from;
                img.gameObject.SetActive(true);

                if (duration > 0f)
                {
                    float t = 0f;
                    while (t < duration)
                    {
                        t += Time.unscaledDeltaTime;
                        if (rt != null)
                            rt.anchoredPosition = Vector2.LerpUnclamped(
                                from, restPos,
                                Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration)));
                        yield return null;
                    }
                }
                if (rt != null) rt.anchoredPosition = restPos;
                break;
            }
            case EntryMotion.FadeIn:
            {
                Color c = img.color; c.a = 0f; img.color = c;
                img.gameObject.SetActive(true);

                if (duration > 0f)
                {
                    float t = 0f;
                    while (t < duration)
                    {
                        t += Time.unscaledDeltaTime;
                        c.a = Mathf.Clamp01(t / duration);
                        img.color = c;
                        yield return null;
                    }
                }
                c.a = 1f; img.color = c;
                break;
            }
            default:
                img.gameObject.SetActive(true);
                break;
        }
    }

    // ── 퇴장 애니메이션 ───────────────────────────────────────────────────
    // EntryRoutine 과 같은 이유로 unscaled 시간을 쓴다.
    IEnumerator ExitRoutine(Image img, ExitMotion motion, bool isRight, float duration)
    {
        RectTransform rt      = img == portraitImageRight ? _portraitRightRT : _portraitRT;
        Vector2       restPos = img == portraitImageRight ? _portraitRightRestPos : _portraitRestPos;

        switch (motion)
        {
            case ExitMotion.SlideOut:
            {
                float   offset  = (rt != null && rt.rect.width > 0f) ? rt.rect.width : 300f;
                Vector2 from    = rt != null ? rt.anchoredPosition : restPos;
                Vector2 to      = restPos + new Vector2(isRight ? offset : -offset, 0f);

                if (duration > 0f)
                {
                    float t = 0f;
                    while (t < duration)
                    {
                        t += Time.unscaledDeltaTime;
                        if (rt != null)
                            rt.anchoredPosition = Vector2.LerpUnclamped(
                                from, to,
                                Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration)));
                        yield return null;
                    }
                }
                if (rt != null) rt.anchoredPosition = restPos;
                img.gameObject.SetActive(false);
                break;
            }
            case ExitMotion.FadeOut:
            {
                Color c     = img.color;
                float start = c.a;

                if (duration > 0f)
                {
                    float t = 0f;
                    while (t < duration)
                    {
                        t += Time.unscaledDeltaTime;
                        c.a = Mathf.Lerp(start, 0f, Mathf.Clamp01(t / duration));
                        img.color = c;
                        yield return null;
                    }
                }
                c.a = 1f; img.color = c;
                img.gameObject.SetActive(false);
                break;
            }
            default:
                img.gameObject.SetActive(false);
                break;
        }
    }
}
