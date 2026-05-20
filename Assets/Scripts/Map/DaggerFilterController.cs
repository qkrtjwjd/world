using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// F키 홀드로 환상/현실 필터를 전환합니다.
/// - 누르는 동안: 현실 모드 (realityObjects 활성)
/// - 떼면: 환상 모드 복귀
/// - 인형화 80% 이상: 현실 전환 후 0.5초만 유지 후 강제 환상 복귀
/// - 대화 중(DialogueManager.isTalking): 입력 무시
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
    }

    void Update()
    {
        if (YarnDialogue.IsRunning)
            return;

        if (Input.GetKeyDown(KeyCode.F))
            SwitchToReality();

        if (Input.GetKeyUp(KeyCode.F))
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
