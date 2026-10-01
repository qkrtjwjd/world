using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// F키 홀드로 환상/현실 필터를 전환합니다.
/// - 누르는 동안: 현실 모드 (realityObjects 활성). 심리 게이지를 100 으로 올린다 — 보이는 필터는 게이지다(C-3-2).
///   누르고 있어도 탐험 중에는 <see cref="realityHoldDecaySeconds"/> 에 걸쳐 회색을 거쳐 원래 값으로 풀린다(C-4-2). 전투 중엔 유지.
/// - 떼면: 환상 모드 복귀 — 게이지를 누르기 전 값으로 되돌린다
/// - 인형화 80% 이상: 현실 전환 후 0.5초만 유지 후 강제 환상 복귀
/// - 대화·이벤트(입력 잠금)·일시정지·타이틀 씬: 입력 무시
/// </summary>
public class DaggerFilterController : MonoBehaviour
{
    public static DaggerFilterController Instance { get; private set; }

    [Header("연결 필수")]
    [Tooltip("현실 오버레이 UI CanvasGroup")]
    public CanvasGroup realityOverlay;

    [Header("설정")]
    [Tooltip("전환 페이드 시간 (초)")]
    public float switchDuration = 0.25f;

    [Tooltip("인형화 80%+ 시 강제 현실 유지 시간 (초)")]
    public float forcedRealityDuration = 0.5f;

    [Tooltip("누르고 있어도 현실이 풀리는 데 걸리는 시간(초). 100 에서 누르기 전 값까지 고르게 내려간다(C-4-2 · F-3-1). " +
             "정본에 수치가 없어 2026-10-01 사용자 결정 30초.")]
    public float realityHoldDecaySeconds = 30f;

    public bool IsReality { get; private set; } = false;

    /// <summary>
    /// 지금 현실을 보고 있는가. 컨트롤러가 없는 씬에서는 false(환상)로 본다.
    ///
    /// <para>예전에는 씬 이름으로 판정했지만(<c>SceneNames.IsRealityScene</c>) 현실/환상은
    /// 별도 씬이 아니라 한 씬 안에서 F키로 바뀌므로 그 배선은 애초에 동작하지 않았다.
    /// 2026-08-27 에 DarkReality 씬을 폐기하면서 이쪽으로 옮겼다.</para>
    /// </summary>
    public static bool IsRealityView => Instance != null && Instance.IsReality;

    /// <summary>
    /// 필터 토글이 봉인됐는가. S#21C 에서 결계가 강화되는 순간 세라가 필터까지 밀어붙이며
    /// <b>이후 토글 입력을 받지 않는다</b>(D-3 S#21C · F-6 · C-3-2). 데모는 그 상태로 끝난다.
    ///
    /// <para>⚠ <see cref="GameState.isDaggerToggleUnlocked"/> 를 내리는 것으로 대신하지 않는다.
    /// 그쪽은 「S#12 에서 조작권이 열렸는가」라는 다른 사실이고, 내리면 S#12 이전으로
    /// 되돌리는 것이 되어 힌트 로직까지 함께 바뀐다. 봉인은 별개의 사실이므로 별개로 둔다.</para>
    ///
    /// <para>⚠ 잠겼다는 UI 표시를 두지 않는다(정본 ▶조작). 키가 그냥 듣지 않을 뿐이다.</para>
    /// </summary>
    public static bool IsToggleSealed { get; private set; }

    /// <summary>토글을 봉인한다. 환상으로 덮은 뒤에 부르는 것이 순서다 — 봉인이 먼저면 전환이 막힌다.</summary>
    public static void SealToggle() => IsToggleSealed = true;

    /// <summary>봉인을 푼다. 데모에는 푸는 자리가 없고, 씬 재시작·되감기 복구용이다.</summary>
    public static void UnsealToggle() => IsToggleSealed = false;

    private RealityFilterObject[] _filterObjects = new RealityFilterObject[0];
    private Coroutine _fadeCoroutine;
    private Coroutine _forcedReturnCoroutine;
    private WaitForSeconds _forcedReturnWait;
    private Coroutine _holdDecayCoroutine;
    private float     _gaugeBeforeHold;
    private bool      _holdingGauge;   // F키로 게이지를 올려 둔 상태인가 — 이때만 떼면서 되돌린다

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        _forcedReturnWait = new WaitForSeconds(forcedRealityDuration);
        CacheFilterObjects();
        if (realityOverlay != null) realityOverlay.alpha = 0f;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CacheFilterObjects();
        if (IsReality)
        {
            IsReality = false;
            if (realityOverlay != null) realityOverlay.alpha = 0f;
            ApplyFilter(false);
            ReleaseGauge();
        }
    }

    void CacheFilterObjects()
    {
        _filterObjects = FindObjectsByType<RealityFilterObject>(FindObjectsInactive.Exclude);

        // 필터 대상이 있는 씬에 처음 진입했을 때 단검 파지 조작 힌트 (통산 1회)
        // ⚠ 토글이 열리기 전(S#12 이전)에는 띄우지 않는다. 쓸 수 없는 키를 안내하게 된다.
        if (_filterObjects.Length > 0 && GameState.isDaggerToggleUnlocked)
            HintManager.ShowHint("dagger_filter",
                $"단검을 뽑으면 진짜가 보입니다. [{SettingsManager.Instance?.keyDagger ?? KeyCode.F}]", 5f);  // D 문단 403 — DaggerPickupCutscene 과 같은 문구
    }

    void Update()
    {
        KeyCode daggerKey = SettingsManager.Instance?.keyDagger ?? KeyCode.F;

        // S#21C 결계 강화 이후로는 토글이 봉인된다. 환상으로 덮인 채 데모가 끝난다(F-6).
        // 여기서 return 해도 고착 방지 로직을 건너뛰지 않는다 — 봉인 시점에 이미 환상이다.
        if (IsToggleSealed) return;

        // S#12(다락방 · 단검)에서 토글 조작권이 열리기 전에는 F키 자체가 없는 것으로 취급한다.
        // 정본: "단검 획득 → 현실/환상 필터 토글 조작권 개방"
        // 여기서 return 하면 아래 고착 방지 로직도 타지 않지만, 개방 전에는 IsReality 가 될 수 없으므로 무해하다.
        if (!GameState.isDaggerToggleUnlocked) return;

        // 이벤트/컷신(입력 잠금) · 일시정지 · 타이틀 씬에서는 필터 전환 차단
        if (PlayerInputLock.Instance.IsLocked
            || Time.timeScale == 0f
            || SceneManager.GetActiveScene().name == SceneNames.Title)
        {
            // 홀드 중 해당 상태로 진입한 뒤 키를 뗀 경우 현실 필터 고착 방지
            if (IsReality && _forcedReturnCoroutine == null && !Input.GetKey(daggerKey))
                SwitchToFantasy();
            return;
        }

        if (YarnDialogue.IsRunning)
        {
            // 대화 중 키를 뗀 경우 현실 필터가 켜진 채 고착되지 않도록 복귀 처리
            if (IsReality && _forcedReturnCoroutine == null && !Input.GetKey(daggerKey))
                SwitchToFantasy();
            return;
        }
        if (DaggerKeyRegistry.HasNearby)
        {
            // 근접 상호작용 오브젝트(거울·작업대 등)가 키를 소비 — 필터는 양보.
            // 홀드 중 범위에 진입한 뒤 키를 뗀 경우 현실 필터 고착 방지
            if (IsReality && _forcedReturnCoroutine == null && !Input.GetKey(daggerKey))
                SwitchToFantasy();
            return;
        }
        if (Input.GetKeyDown(daggerKey))
            SwitchToReality();

        if (Input.GetKeyUp(daggerKey))
            SwitchToFantasy();
    }

    /// <summary>MentalBreakStage에서 호출: 코루틴 간섭 없이 즉시 현실 전환</summary>
    public void SwitchToRealityForced()
    {
        if (_forcedReturnCoroutine != null) { StopCoroutine(_forcedReturnCoroutine); _forcedReturnCoroutine = null; }
        if (_fadeCoroutine != null)         { StopCoroutine(_fadeCoroutine);         _fadeCoroutine = null; }
        StopHoldDecay();   // 게이지는 부르는 쪽이 맡는다(S#12 · CutGauge)
        _holdingGauge = false;
        IsReality = true;
        if (realityOverlay != null) realityOverlay.alpha = 1f;
        ApplyFilter(true);
    }

    /// <summary>MentalBreakStage에서 호출: 코루틴 간섭 없이 즉시 환상 복귀</summary>
    public void SwitchToFantasyForced()
    {
        if (_forcedReturnCoroutine != null) { StopCoroutine(_forcedReturnCoroutine); _forcedReturnCoroutine = null; }
        if (_fadeCoroutine != null)         { StopCoroutine(_fadeCoroutine);         _fadeCoroutine = null; }
        StopHoldDecay();
        _holdingGauge = false;
        IsReality = false;
        if (realityOverlay != null) realityOverlay.alpha = 0f;
        ApplyFilter(false);
    }

    void SwitchToReality()
    {
        if (IsReality) return;

        IsReality = true;

        if (GlitchManager.Instance != null)
            GlitchManager.Instance.PlayGlitch(switchDuration, GetGlitchPresetForCurrentState());

        StartFade(1f);
        ApplyFilter(true);
        GrabGauge();

        if (GetCorruptionRatio() >= 0.8f)
        {
            if (_forcedReturnCoroutine != null) StopCoroutine(_forcedReturnCoroutine);
            _forcedReturnCoroutine = StartCoroutine(ForcedReturnRoutine());
        }
    }

    void SwitchToFantasy()
    {
        if (!IsReality) return;

        // 강제 복귀 코루틴이 실행 중이면 취소하지 않음 (이미 복귀 예정)
        // 단, 강제 복귀 중이 아닐 때만 즉시 전환
        if (_forcedReturnCoroutine != null) return;

        DoSwitchToFantasy();
    }

    void DoSwitchToFantasy()
    {
        IsReality = false;

        if (GlitchManager.Instance != null)
            GlitchManager.Instance.PlayGlitch(switchDuration, GetGlitchPresetForCurrentState());

        StartFade(0f);
        ApplyFilter(false);
        ReleaseGauge();
    }

    // ── 심리 게이지 ─────────────────────────────────────────────────────
    // 화면 색은 게이지가 정한다 — IsReality 만 바꾸면 화면은 그대로다(2026-10-01 실측).
    // 전환은 위의 글리치(짧은 노이즈, D 406) 아래에서 CutGauge 로 그 프레임에 바꾼다.

    /// <summary>
    /// 저장할 심리 게이지 — F키로 쥐고 있는 동안의 일시 값(100 · 풀리는 중)이 아니라 누르기 전 값.
    /// 쥔 채 저장한 파일을 불러와 현실에 고정되지 않게 한다(F-9-3 · 2026-10-01).
    /// </summary>
    public static float GaugeWithoutHold()
    {
        if (Instance != null && Instance._holdingGauge) return Instance._gaugeBeforeHold;
        return GaugeManager.Instance != null ? GaugeManager.Instance.fantasyRealityGauge : GaugeManager.DEFAULT_GAUGE;
    }

    void GrabGauge()
    {
        var g = GaugeManager.Instance;
        if (g == null) return;
        StopHoldDecay();
        if (!_holdingGauge) _gaugeBeforeHold = g.fantasyRealityGauge;
        _holdingGauge = true;
        g.CutGauge(100f);
        _holdDecayCoroutine = StartCoroutine(HoldDecayRoutine(_gaugeBeforeHold));
    }

    void ReleaseGauge()
    {
        StopHoldDecay();
        if (!_holdingGauge) return;   // 강제 전환(S#12 · MentalBreakStage)으로 켜진 현실은 그쪽이 게이지를 맡는다

        // 전투 중엔 유지한다(C-4-2 문단 437) — 여기서 되돌리면 핵앤슬래시가 도는 채로 화면만 환상이 된다(2026-10-01 실측).
        //   전투가 끝난 뒤에 누르기 전 값으로 돌린다.
        if (InBattle)
        {
            _holdDecayCoroutine = StartCoroutine(ReleaseAfterBattle());
            return;
        }

        _holdingGauge = false;
        GaugeManager.Instance?.CutGauge(_gaugeBeforeHold);
    }

    static bool InBattle => BattleSystem.IsActive || HackSlashCombatManager.IsActive;

    IEnumerator ReleaseAfterBattle()
    {
        while (InBattle) yield return null;
        _holdDecayCoroutine = null;
        if (!_holdingGauge) yield break;
        _holdingGauge = false;
        GaugeManager.Instance?.CutGauge(_gaugeBeforeHold);
    }

    void StopHoldDecay()
    {
        if (_holdDecayCoroutine != null) { StopCoroutine(_holdDecayCoroutine); _holdDecayCoroutine = null; }
    }

    /// <summary>붙잡고 있어도 풀린다(C-4-2) — 100 에서 누르기 전 값까지 고르게. 전투 중에는 멈춘다(C-4-2 문단 437).</summary>
    IEnumerator HoldDecayRoutine(float target)
    {
        float duration = Mathf.Max(0.01f, realityHoldDecaySeconds);
        float t = 0f;
        while (t < duration)
        {
            yield return null;
            if (InBattle) continue;
            t += Time.deltaTime;
            GaugeManager.Instance?.DriftGauge(Mathf.Lerp(100f, target, t / duration));
        }
        _holdDecayCoroutine = null;
    }

    float GetCorruptionRatio()
    {
        if (CorruptionManager.Instance == null) return 0f;
        return CorruptionManager.Instance.currentCorruption / CorruptionManager.Instance.maxCorruption;
    }

    GlitchPreset GetGlitchPresetForCurrentState()
    {
        float ratio = GetCorruptionRatio();
        if (ratio >= 0.8f)  return GlitchManager.PresetCrash;
        if (ratio >= 0.31f) return GlitchManager.PresetStrong;
        return GlitchManager.PresetMild;
    }

    IEnumerator ForcedReturnRoutine()
    {
        yield return _forcedReturnWait;
        DoSwitchToFantasy();
        _forcedReturnCoroutine = null;
    }

    void ApplyFilter(bool isReality)
    {
        foreach (var obj in _filterObjects)
            if (obj != null) obj.SetFilter(isReality);
    }

    void StartFade(float targetAlpha)
    {
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeRoutine(targetAlpha));
    }

    IEnumerator FadeRoutine(float targetAlpha)
    {
        if (realityOverlay == null) yield break;

        float startAlpha = realityOverlay.alpha;
        float elapsed = 0f;

        while (elapsed < switchDuration)
        {
            elapsed += Time.deltaTime;
            realityOverlay.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / switchDuration);
            yield return null;
        }

        realityOverlay.alpha = targetAlpha;
        _fadeCoroutine = null;
    }

}
