using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 씬에 배치된 Global Volume weight를 게이지 값으로 실시간 Lerp.
/// fantasyVolume.weight = 1 - (gauge/100), realityVolume.weight = gauge/100
/// glitchVolume: 게이지 30~69 구간에서 weight=1 (Saturation -100으로 회색화)
///
/// [설정 연동]
/// brightnessVolume : ColorAdjustments 프로파일을 가진 Volume.
///                    Inspector에서 연결하면 밝기 설정이 postExposure 로 반영됨.
///                    (0.0 = -2EV, 0.5 = 0EV, 1.0 = +2EV)
/// saturationVolume : ColorAdjustments 프로파일을 가진 Volume.
///                    채도 강도 설정이 saturation 으로 반영됨.
///                    (0.0 = -100, 0.5 = 0, 1.0 = +100)
/// </summary>
public class PostProcessingController : MonoBehaviour
{
    [Header("Volume 연결 (Inspector)")]
    public Volume fantasyVolume;
    public Volume realityVolume;
    [Tooltip("Saturation -100 으로 설정된 Volume. 게이지 30~69 구간에서 활성화됩니다.")]
    public Volume glitchVolume;

    [Header("설정 메뉴 연동 Volume (Inspector, 선택)")]
    [Tooltip("ColorAdjustments 프로파일 포함 Volume. 밝기 슬라이더가 postExposure 를 조절합니다.")]
    public Volume brightnessVolume;
    [Tooltip("ColorAdjustments 프로파일 포함 Volume. 채도 강도 슬라이더가 saturation 을 조절합니다.")]
    public Volume saturationVolume;

    [Header("추종 속도")]
    public float lerpSpeed = 2f;

    private float _targetFantasy = 1f;
    private float _targetReality = 0f;
    private float _targetGlitch  = 0f;

    // ColorAdjustments 캐시
    private ColorAdjustments _brightnessCA;
    private ColorAdjustments _saturationCA;

    void OnEnable()
    {
        if (GaugeManager.Instance != null)
            GaugeManager.Instance.OnGaugeChanged += OnGaugeChanged;

        SettingsManager.OnBrightnessChanged     += ApplyBrightness;
        SettingsManager.OnSaturationChanged     += ApplySaturation;
        SettingsManager.OnColorblindModeChanged += ApplyColorblindMode;
    }

    void OnDisable()
    {
        if (GaugeManager.Instance != null)
            GaugeManager.Instance.OnGaugeChanged -= OnGaugeChanged;

        SettingsManager.OnBrightnessChanged     -= ApplyBrightness;
        SettingsManager.OnSaturationChanged     -= ApplySaturation;
        SettingsManager.OnColorblindModeChanged -= ApplyColorblindMode;
    }

    void Start()
    {
        // GaugeManager가 Start 이후에 초기화될 수 있으므로 직접 구독도 시도
        if (GaugeManager.Instance != null)
        {
            GaugeManager.Instance.OnGaugeChanged -= OnGaugeChanged;
            GaugeManager.Instance.OnGaugeChanged += OnGaugeChanged;
            OnGaugeChanged(GaugeManager.Instance.fantasyRealityGauge);
        }

        // ColorAdjustments 컴포넌트 캐시
        brightnessVolume?.profile.TryGet(out _brightnessCA);
        saturationVolume?.profile.TryGet(out _saturationCA);

        // 설정 메뉴 연동 Volume이 씬에 배선되지 않으면 밝기/채도 슬라이더가 무효가 되므로 경고
        if (brightnessVolume == null || saturationVolume == null)
            Debug.LogWarning("[PostProcessingController] brightness/saturation Volume이 연결되지 않았습니다. " +
                "설정 메뉴의 밝기·채도 슬라이더가 동작하지 않습니다. Inspector에서 ColorAdjustments Volume을 연결하세요.");

        // 저장된 설정 즉시 반영
        if (SettingsManager.Instance != null)
        {
            ApplyBrightness(SettingsManager.Instance.brightness);
            ApplySaturation(SettingsManager.Instance.saturation);
            ApplyColorblindMode(SettingsManager.Instance.colorblindMode);
        }
    }

    /// <summary>
    /// 색맹 보정 모드를 URP ColorblindRenderFeature 로 전달.
    /// RendererFeature 가 URP Renderer 에 배선돼 있지 않으면(Instance == null) 무시된다.
    /// </summary>
    void ApplyColorblindMode(int mode)
    {
        ColorblindRenderFeature.Instance?.SetMode(mode);
    }

    void OnGaugeChanged(float gauge)
    {
        _targetReality = gauge / 100f;
        _targetFantasy = 1f - _targetReality;
        _targetGlitch  = (gauge > 30f && gauge < 70f) ? 1f : 0f;
    }

    void Update()
    {
        if (fantasyVolume != null)
            fantasyVolume.weight = Mathf.Lerp(fantasyVolume.weight, _targetFantasy, Time.deltaTime * lerpSpeed);

        if (realityVolume != null)
            realityVolume.weight = Mathf.Lerp(realityVolume.weight, _targetReality, Time.deltaTime * lerpSpeed);

        if (glitchVolume != null)
            glitchVolume.weight = Mathf.Lerp(glitchVolume.weight, _targetGlitch, Time.deltaTime * lerpSpeed);
    }

    // ─── 설정 메뉴 콜백 ───────────────────────────────────────────────────

    /// <summary>
    /// 밝기 슬라이더 0~1 → postExposure -2 ~ +2 EV 매핑.
    /// brightnessVolume이 없으면 무시.
    /// </summary>
    void ApplyBrightness(float value)
    {
        if (_brightnessCA == null && brightnessVolume != null)
            brightnessVolume.profile.TryGet(out _brightnessCA);
        if (_brightnessCA == null) return;

        float exposure = Mathf.Lerp(-2f, 2f, value);
        _brightnessCA.postExposure.Override(exposure);
    }

    /// <summary>
    /// 채도 강도 슬라이더 0~1 → ColorAdjustments.saturation -100 ~ +100 매핑.
    /// 0.5 = 원본 채도 유지.
    /// saturationVolume이 없으면 무시.
    /// </summary>
    void ApplySaturation(float value)
    {
        if (_saturationCA == null && saturationVolume != null)
            saturationVolume.profile.TryGet(out _saturationCA);
        if (_saturationCA == null) return;

        float sat = Mathf.Lerp(-100f, 100f, value);
        _saturationCA.saturation.Override(sat);
    }
}
