using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// F키 홀드로 환상/현실 필터를 전환합니다.
/// - 누르는 동안: 현실 모드 (realityObjects 활성)
/// - 떼면: 환상 모드 복귀
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

    public bool IsReality { get; private set; } = false;

    /// <summary>
    /// 지금 현실을 보고 있는가. 컨트롤러가 없는 씬에서는 false(환상)로 본다.
    ///
    /// <para>예전에는 씬 이름으로 판정했지만(<c>SceneNames.IsRealityScene</c>) 현실/환상은
    /// 별도 씬이 아니라 한 씬 안에서 F키로 바뀌므로 그 배선은 애초에 동작하지 않았다.
    /// 2026-08-27 에 DarkReality 씬을 폐기하면서 이쪽으로 옮겼다.</para>
    /// </summary>
    public static bool IsRealityView => Instance != null && Instance.IsReality;

    private RealityFilterObject[] _filterObjects = new RealityFilterObject[0];
    private Coroutine _fadeCoroutine;
    private Coroutine _forcedReturnCoroutine;
    private WaitForSeconds _forcedReturnWait;

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
        }
    }

    void CacheFilterObjects()
    {
        _filterObjects = FindObjectsByType<RealityFilterObject>(FindObjectsInactive.Exclude);

        // 필터 대상이 있는 씬에 처음 진입했을 때 단검 파지 조작 힌트 (통산 1회)
        // ⚠ 토글이 열리기 전(S#12 이전)에는 띄우지 않는다. 쓸 수 없는 키를 안내하게 된다.
        if (_filterObjects.Length > 0 && GameState.isDaggerToggleUnlocked)
            HintManager.ShowHint("dagger_filter",
                $"[{SettingsManager.Instance?.keyDagger ?? KeyCode.F}] 키를 누르고 있으면 은빛 단검으로 현실을 볼 수 있습니다.", 5f);
    }

    void Update()
    {
        KeyCode daggerKey = SettingsManager.Instance?.keyDagger ?? KeyCode.F;

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
        IsReality = true;
        if (realityOverlay != null) realityOverlay.alpha = 1f;
        ApplyFilter(true);
    }

    /// <summary>MentalBreakStage에서 호출: 코루틴 간섭 없이 즉시 환상 복귀</summary>
    public void SwitchToFantasyForced()
    {
        if (_forcedReturnCoroutine != null) { StopCoroutine(_forcedReturnCoroutine); _forcedReturnCoroutine = null; }
        if (_fadeCoroutine != null)         { StopCoroutine(_fadeCoroutine);         _fadeCoroutine = null; }
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
