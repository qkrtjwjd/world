using System.Collections;
using UnityEngine;

[System.Serializable]
public struct GlitchPreset
{
    public float intensity;
    public float colorDrift;
    public float scanLineJitter;
    public float staticNoise;
    public float blockDisplace;
}

/// <summary>
/// 글리치 효과 제어 매니저.
/// RawImage 방식 대신 GlitchRenderFeature (URP Renderer Feature) 를 사용.
///
/// [에디터 설정]
/// - GlitchRenderFeature 를 URP Renderer Asset 에 추가하고 GlitchMaterial 연결
/// - 기존 Canvas 의 GlitchPanel RawImage 오브젝트는 삭제해도 됩니다
/// </summary>
public class GlitchManager : MonoBehaviour
{
    public static GlitchManager Instance;

    [Header("기본 설정")]
    [Range(0, 1)] public float defaultIntensity = 0.5f;

    // ── 프리셋 ────────────────────────────────────────────────────────────
    public static readonly GlitchPreset PresetSubtle = new GlitchPreset
        { intensity = 0.28f, colorDrift = 0.012f, scanLineJitter = 0.05f, staticNoise = 0.02f,  blockDisplace = 0.005f };
    public static readonly GlitchPreset PresetMild = new GlitchPreset
        { intensity = 0.62f, colorDrift = 0.035f, scanLineJitter = 0.12f, staticNoise = 0.18f,  blockDisplace = 0.040f };
    public static readonly GlitchPreset PresetStrong = new GlitchPreset
        { intensity = 0.85f, colorDrift = 0.065f, scanLineJitter = 0.18f, staticNoise = 0.35f,  blockDisplace = 0.110f };
    public static readonly GlitchPreset PresetCrash = new GlitchPreset
        { intensity = 1.00f, colorDrift = 0.100f, scanLineJitter = 0.24f, staticNoise = 0.55f,  blockDisplace = 0.200f };

    // 단검 미장착 시 배경 루프용 — 단검 플래시보다 약하게, 인형화 구간별로 사용
    public static readonly GlitchPreset PresetAmbientLow = new GlitchPreset
        { intensity = 0.09f, colorDrift = 0.003f, scanLineJitter = 0.015f, staticNoise = 0.02f,  blockDisplace = 0.003f };
    public static readonly GlitchPreset PresetAmbientMid = new GlitchPreset
        { intensity = 0.16f, colorDrift = 0.008f, scanLineJitter = 0.035f, staticNoise = 0.04f,  blockDisplace = 0.010f };
    public static readonly GlitchPreset PresetAmbientHigh = new GlitchPreset
        { intensity = 0.24f, colorDrift = 0.014f, scanLineJitter = 0.055f, staticNoise = 0.08f,  blockDisplace = 0.018f };

    // ── 셰이더 프로퍼티 ID 캐싱 ───────────────────────────────────────────
    static readonly int PropIntensity      = Shader.PropertyToID("_Intensity");
    static readonly int PropColorDrift     = Shader.PropertyToID("_ColorDrift");
    static readonly int PropScanLineJitter = Shader.PropertyToID("_ScanLineJitter");
    static readonly int PropStaticNoise    = Shader.PropertyToID("_StaticNoise");
    static readonly int PropBlockDisplace  = Shader.PropertyToID("_BlockDisplace");

    private GlitchRenderFeature _feature;
    private Material            _glitchMat;
    private Coroutine           _activeCoroutine;

    // ── 라이프사이클 ──────────────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoCreate()
    {
        if (Instance != null) return;
        var prefab = Resources.Load<GameObject>("Glitch");
        if (prefab != null) Instantiate(prefab);
    }

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
        }
    }

    void Start()
    {
        StartCoroutine(InitAfterFrame());
    }

    IEnumerator InitAfterFrame()
    {
        // URP 렌더러 초기화(Create) 가 Start 보다 늦게 완료될 수 있으므로 한 프레임 대기
        yield return null;

        _feature = GlitchRenderFeature.Instance;
        if (_feature == null)
        {
            Debug.LogWarning("[GlitchManager] GlitchRenderFeature 를 찾을 수 없습니다. " +
                             "URP Renderer Asset 에 GlitchRenderFeature 를 추가했는지 확인하세요.");
            yield break;
        }

        _glitchMat = _feature.Material;
        if (_glitchMat == null)
        {
            Debug.LogWarning("[GlitchManager] GlitchRenderFeature 의 Material 슬롯이 비어 있습니다.");
            yield break;
        }

        ResetShader();
        _feature.SetActive(false);
    }

    // ── 퍼블릭 API ────────────────────────────────────────────────────────

    /// <summary>duration 초 동안 프리셋으로 글리치 재생</summary>
    public void PlayGlitch(float duration, GlitchPreset preset)
    {
        if (!IsReady()) return;
        if (_activeCoroutine != null) StopCoroutine(_activeCoroutine);
        _activeCoroutine = StartCoroutine(RoutinePreset(duration, preset));
    }

    /// <summary>duration 초 동안 강도로 글리치 재생 (기존 호환)</summary>
    public void PlayGlitch(float duration, float intensity = -1f)
    {
        if (!IsReady()) return;
        GlitchPreset p = PresetMild;
        if (intensity >= 0f) p.intensity = intensity;
        if (_activeCoroutine != null) StopCoroutine(_activeCoroutine);
        _activeCoroutine = StartCoroutine(RoutinePreset(duration, p));
    }

    /// <summary>프리셋으로 글리치 루프 켜기/끄기</summary>
    public void SetGlitchLoop(bool isActive, GlitchPreset preset)
    {
        if (!IsReady()) return;
        if (isActive)
        {
            _feature.SetActive(true);
            ApplyPreset(preset, 1f, 1f);
        }
        else
        {
            TurnOffLoop();
        }
    }

    /// <summary>강도로 글리치 루프 켜기/끄기 (기존 호환)</summary>
    public void SetGlitchLoop(bool isActive, float intensity = -1f)
    {
        if (!IsReady()) return;
        if (isActive)
        {
            if (intensity < 0f) intensity = defaultIntensity;
            GlitchPreset p = PresetMild;
            p.intensity = intensity;
            _feature.SetActive(true);
            ApplyPreset(p, 1f, 1f);
        }
        else
        {
            TurnOffLoop();
        }
    }

    // ── 내부 ──────────────────────────────────────────────────────────────

    bool IsReady() => _feature != null && _glitchMat != null;

    void TurnOffLoop()
    {
        // PlayGlitch 코루틴이 실행 중이면 패스를 끄지 않음
        if (_activeCoroutine != null) return;
        _feature.SetActive(false);
    }

    void ApplyPreset(GlitchPreset p, float noise, float envelope)
    {
        // 접근성: 글리치 완전 비활성화 시 즉시 끄기
        if (SettingsManager.Instance != null && SettingsManager.Instance.glitchEffectDisabled)
        {
            _feature?.SetActive(false);
            return;
        }
        float intensityMul = SettingsManager.Instance?.glitchEffectIntensity ?? 1f;
        float scale = noise * envelope * intensityMul;
        _glitchMat.SetFloat(PropIntensity,      p.intensity      * scale);
        _glitchMat.SetFloat(PropColorDrift,     p.colorDrift     * scale);
        _glitchMat.SetFloat(PropScanLineJitter, p.scanLineJitter * scale);
        _glitchMat.SetFloat(PropStaticNoise,    p.staticNoise    * scale);
        _glitchMat.SetFloat(PropBlockDisplace,  p.blockDisplace  * scale);
    }

    void ResetShader()
    {
        _glitchMat.SetFloat(PropIntensity,      0f);
        _glitchMat.SetFloat(PropColorDrift,     0f);
        _glitchMat.SetFloat(PropScanLineJitter, 0f);
        _glitchMat.SetFloat(PropStaticNoise,    0f);
        _glitchMat.SetFloat(PropBlockDisplace,  0f);
    }

    /// <summary>
    /// 핵심 코루틴 — fade-in/out envelope + 프레임마다 노이즈 플리커 적용.
    /// 구조는 기존과 동일, glitchPanel 참조만 feature.SetActive 로 교체.
    /// </summary>
    IEnumerator RoutinePreset(float duration, GlitchPreset preset)
    {
        _feature.SetActive(true);

        float timer           = 0f;
        float flickerTimer    = 0f;
        float flickerInterval = Random.Range(0.008f, 0.05f);
        float currentNoise    = Random.Range(0.3f, 1.5f);

        while (timer < duration)
        {
            float progress = timer / duration;

            // Fade-in 0~20%, Fade-out 80~100%
            float envelope;
            if (progress < 0.2f)
                envelope = progress / 0.2f;
            else if (progress > 0.8f)
                envelope = (1f - progress) / 0.2f;
            else
                envelope = 1f;

            // flickerInterval 마다 노이즈 값 갱신
            flickerTimer += Time.deltaTime;
            if (flickerTimer >= flickerInterval)
            {
                flickerTimer    = 0f;
                flickerInterval = Random.Range(0.008f, 0.05f);
                currentNoise    = Random.Range(0.3f, 1.5f);
            }

            ApplyPreset(preset, currentNoise, envelope);

            timer += Time.deltaTime;
            yield return null;
        }

        ResetShader();
        _feature.SetActive(false);
        _activeCoroutine = null;
    }
}
