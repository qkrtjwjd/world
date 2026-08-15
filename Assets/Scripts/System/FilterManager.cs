using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public enum FilterType { Fantasy, Reality, None }

/// <summary>
/// 연출용 필터(Fantasy/Reality) 오버라이드 매니저.
/// 자체 Global Volume을 런타임 생성하여 PostProcessingController와 충돌 없이 동작한다.
/// 인형화 수치에 따라 ambient glitch를 자동 조정한다.
/// </summary>
public class FilterManager : PersistentSingleton<FilterManager>
{
    [Header("씬 전환 속도")]
    [SerializeField] private float filterLerpDuration = 0.5f;

    // 런타임 생성 Volume
    private Volume            _scenarioVolume;
    private ColorAdjustments  _colorAdjustments;
    private WhiteBalance      _whiteBalance;
    private Coroutine         _filterCoroutine;
    private FilterType        _currentFilter = FilterType.None;
    private bool              _ambientGlitchActive;

    // ── 라이프사이클 ──────────────────────────────────────────────────────
    protected override void OnAwake()
    {
        CreateScenarioVolume();

        if (GameStateManager.Instance != null)
            GameStateManager.Instance.OnDollificationChanged += HandleDollificationChanged;
    }

    void Start()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnDollificationChanged -= HandleDollificationChanged;
            GameStateManager.Instance.OnDollificationChanged += HandleDollificationChanged;
        }
    }

    protected override void OnDestroy()
    {
        if (GameStateManager.Instance != null)
            GameStateManager.Instance.OnDollificationChanged -= HandleDollificationChanged;
        base.OnDestroy();
    }

    // ── 공개 API ─────────────────────────────────────────────────────────
    /// <summary>필터 타입과 강도를 설정한다. intensity 0~1.</summary>
    public void SetFilter(FilterType type, float intensity = 1f)
    {
        _currentFilter = type;
        if (_filterCoroutine != null) StopCoroutine(_filterCoroutine);
        _filterCoroutine = StartCoroutine(ApplyFilterRoutine(type, intensity));
    }

    /// <summary>현재 필터를 즉시 제거한다.</summary>
    public void ClearFilter()
    {
        _currentFilter = FilterType.None;
        if (_filterCoroutine != null) StopCoroutine(_filterCoroutine);
        if (_scenarioVolume != null) _scenarioVolume.weight = 0f;
    }

    /// <summary>GlitchEffect를 duration 초 동안 재생한다.</summary>
    public void GlitchEffect(float duration, float intensity = -1f)
    {
        if (GlitchManager.Instance == null) return;

        GlitchPreset preset;
        if (intensity < 0f)
        {
            preset = GlitchManager.PresetMild;
        }
        else if (intensity >= 0.8f)
        {
            preset = GlitchManager.PresetCrash;
        }
        else if (intensity >= 0.5f)
        {
            preset = GlitchManager.PresetStrong;
        }
        else
        {
            preset = GlitchManager.PresetSubtle;
        }

        GlitchManager.Instance.PlayGlitch(duration, preset);
    }

    // ── 인형화 자동 반응 ──────────────────────────────────────────────────
    void HandleDollificationChanged(float dollification)
    {
        if (GlitchManager.Instance == null) return;

        bool shouldLoop = dollification >= 81f;

        if (shouldLoop && !_ambientGlitchActive)
        {
            _ambientGlitchActive = true;
            GlitchManager.Instance.SetGlitchLoop(true, GlitchManager.PresetAmbientHigh);
        }
        else if (!shouldLoop && _ambientGlitchActive)
        {
            _ambientGlitchActive = false;
            GlitchManager.Instance.SetGlitchLoop(false);
        }
    }

    // ── 내부 ─────────────────────────────────────────────────────────────
    void CreateScenarioVolume()
    {
        var go = new GameObject("ScenarioFilterVolume");
        DontDestroyOnLoad(go);

        _scenarioVolume          = go.AddComponent<Volume>();
        _scenarioVolume.isGlobal = true;
        _scenarioVolume.priority = 10; // PostProcessingController의 Volume보다 높은 우선순위
        _scenarioVolume.weight   = 0f;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        _scenarioVolume.profile = profile;

        _colorAdjustments = profile.Add<ColorAdjustments>(true);
        _whiteBalance     = profile.Add<WhiteBalance>(true);

        _colorAdjustments.saturation.overrideState  = true;
        _colorAdjustments.postExposure.overrideState = true;
        _whiteBalance.temperature.overrideState      = true;
    }

    IEnumerator ApplyFilterRoutine(FilterType type, float targetIntensity)
    {
        float startWeight = _scenarioVolume.weight;
        float elapsed     = 0f;

        // 필터 파라미터 설정
        switch (type)
        {
            case FilterType.Fantasy:
                _colorAdjustments.saturation.value   =  40f;  // 채도+
                _colorAdjustments.postExposure.value =   0.5f; // 명도+
                _whiteBalance.temperature.value      =  30f;  // 따뜻
                break;
            case FilterType.Reality:
                _colorAdjustments.saturation.value   = -40f;  // 채도-
                _colorAdjustments.postExposure.value =  -0.3f; // 명도-
                _whiteBalance.temperature.value      = -30f;  // 차갑
                break;
            case FilterType.None:
                targetIntensity = 0f;
                break;
        }

        if (filterLerpDuration <= 0f)
        {
            _scenarioVolume.weight = targetIntensity;
            _filterCoroutine = null;
            yield break;
        }

        while (elapsed < filterLerpDuration)
        {
            elapsed += Time.deltaTime;
            _scenarioVolume.weight = Mathf.Lerp(startWeight, targetIntensity, elapsed / filterLerpDuration);
            yield return null;
        }

        _scenarioVolume.weight = targetIntensity;
        _filterCoroutine = null;
    }
}
