using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// 전투 모드 전환 시 비주얼 이펙트를 제공합니다.
/// BattleTransitionManager의 전환 코루틴에서 호출하세요.
///
/// [에디터 설정]
/// - _volume: Color Adjustments 오버라이드가 포함된 Global Volume 연결
/// - _cameraFollow: CameraFollow 오브젝트 연결 (비우면 자동 탐색)
/// - _flashOverlay: 흰 CanvasGroup 연결 (비우면 자동 생성)
/// </summary>
[DefaultExecutionOrder(100)] // CameraFollow(기본값 0) LateUpdate 이후 흔들림 오프셋 적용
public class TransitionVFXController : MonoBehaviour
{
    // ─────────────────────────────────────────────
    //  싱글톤
    // ─────────────────────────────────────────────
    public static TransitionVFXController Instance
    {
        get
        {
            if (!_instance)
            {
                // 연출 이미지가 배선된 상주 프리팹 우선 로드 — Awake에서 _instance 등록됨
                var prefab = Resources.Load<GameObject>("TransitionFX");
                if (prefab != null)
                    Instantiate(prefab).name = "TransitionFX";

                if (!_instance)
                {
                    var go = new GameObject("TransitionVFXController [Auto]");
                    _instance = go.AddComponent<TransitionVFXController>();
                }
            }
            return _instance;
        }
    }
    private static TransitionVFXController _instance;

    // ─────────────────────────────────────────────
    //  Inspector 설정
    // ─────────────────────────────────────────────
    [Header("흰 플래시 오버레이")]
    [Tooltip("흰 Image 위에 올린 CanvasGroup. 비워두면 자동 생성합니다.")]
    [SerializeField] private CanvasGroup _flashOverlay;

    [Header("셰이더 기반 전환 오버레이")]
    [Tooltip("GlassCrack.mat이 적용된 풀스크린 UI Image.")]
    [SerializeField] private Image _glassCrackImage;
    [Tooltip("ImpactFlash.mat이 적용된 풀스크린 UI Image.")]
    [SerializeField] private Image _impactFlashImage;
    [Tooltip("WatercolorSpread.mat이 적용된 풀스크린 UI Image.")]
    [SerializeField] private Image _watercolorImage;
    [Tooltip("ImpactFlash 재생 시 함께 펄스할 ChromaticAberration 피크 (0~1).")]
    [Range(0f, 1f)]
    [SerializeField] private float _chromaticFlashPeak = 0.6f;
    [Tooltip("산산조각 시 crack 이미지의 최대 스케일.")]
    [SerializeField] private float _shatterScaleMax = 1.3f;

    [Header("단검 찌르기")]
    [Tooltip("단검 Sprite가 적용된 UI Image. Inspector에서 연결하세요.")]
    [SerializeField] private Image _daggerImage;
    [Tooltip("단검이 박히는 화면 위치 (UV 0~1). GlassCrack ImpactPoint와 연동됩니다.")]
    [SerializeField] private Vector2 _daggerStabUV    = new Vector2(0.5f, 0.5f);
    [Tooltip("단검 출발 위치 (UV). 1 초과값은 화면 밖을 의미합니다.")]
    [SerializeField] private Vector2 _daggerStartUV   = new Vector2(1.4f, 1.3f);
    [Tooltip("단검 스프라이트 원본 방향 보정 각도 (도).")]
    [SerializeField] private float   _daggerAngleOffset = 0f;

    [Header("마시멜로 / 후광")]
    [Tooltip("마시멜로 Sprite Image (Assets/Images/Marshmallow.png).")]
    [SerializeField] private Image _marshmallowImage;
    [Tooltip("후광 흰 원형 Sprite Image. 마시멜로 뒤에 배치하세요.")]
    [SerializeField] private Image _marshmallowGlowImage;
    [Tooltip("후광 맥동 속도 (Hz).")]
    [SerializeField] private float _glowPulseSpeed = 1.8f;
    [Tooltip("후광 맥동 최소 스케일.")]
    [SerializeField] private float _glowPulseMin   = 0.85f;
    [Tooltip("후광 맥동 최대 스케일.")]
    [SerializeField] private float _glowPulseMax   = 1.15f;

    [Header("Post-Processing")]
    [Tooltip("Color Adjustments 오버라이드가 포함된 Global Volume.")]
    [SerializeField] private Volume _volume;

    [Header("카메라")]
    [Tooltip("CameraFollow가 붙은 오브젝트. 비워두면 자동 탐색합니다.")]
    [SerializeField] private CameraFollow _cameraFollow;
    [Tooltip("카메라 흔들림 Perlin 노이즈 주파수. 높을수록 빠른 진동.")]
    [SerializeField] private float _shakeFrequency = 25f;

    // ─────────────────────────────────────────────
    //  내부 상태
    // ─────────────────────────────────────────────
    private ColorAdjustments _colorAdjustments;
    private ChromaticAberration _chromaticAberration;
    private Camera _cam;

    // 카메라 흔들림 — LateUpdate에서 CameraFollow 위치에 덧씌움
    private Vector3 _shakeOffset  = Vector3.zero;
    private float   _noiseOffsetX;
    private float   _noiseOffsetY;

    // 셰이더 머티리얼 인스턴스 (공유 애셋 변경 방지)
    private Material _crackMat;
    private Material _flashMat;
    private Vector3 _crackImageOriginalScale = Vector3.one;
    private CanvasGroup _crackCanvasGroup;

    // 셰이더 프로퍼티 ID (Shader.PropertyToID)
    private static readonly int ID_CrackAmount   = Shader.PropertyToID("_CrackAmount");
    private static readonly int ID_ShatterAmount = Shader.PropertyToID("_ShatterAmount");
    private static readonly int ID_ImpactPoint   = Shader.PropertyToID("_ImpactPoint");
    private static readonly int ID_FlashAmount   = Shader.PropertyToID("_FlashAmount");
    private static readonly int ID_FlashCenter   = Shader.PropertyToID("_Center");

    // 실행 중인 코루틴 핸들 (동일 효과 중복 방지)
    private Coroutine _flashCoroutine;
    private Coroutine _shakeCoroutine;
    private Coroutine _colorGradingCoroutine;
    private Coroutine _zoomCoroutine;
    private Coroutine _screenCrackCoroutine;
    private Coroutine _shatterCoroutine;
    private Coroutine _impactFlashCoroutine;
    private Coroutine _watercolorCoroutine;
    private Coroutine _daggerCoroutine;
    private Coroutine _marshmallowCoroutine;
    private Coroutine _glowPulseCoroutine;

    private CanvasGroup   _daggerGroup;
    private RectTransform _daggerRect;
    private RectTransform _glowRect;

    private Material _watercolorMat;

    private static readonly int ID_SpreadAmount   = Shader.PropertyToID("_SpreadAmount");
    private static readonly WaitForSecondsRealtime _waitDaggerFade = new WaitForSecondsRealtime(0.5f);

    // ─────────────────────────────────────────────
    //  상수
    // ─────────────────────────────────────────────
    private const float GlowAlphaRatio      = 0.55f; // 후광 알파 = 마시멜로 알파 × 이 값
    private const float ShakeDecayExponent  = -5f;   // 흔들림 지수 감쇠 계수
    private const float DaggerFadeInCurve   = 5f;    // 단검 출현 알파 가속 계수
    private const float WatercolorSpreadMax = 1.05f; // FBM 최대값 ~0.94를 초과해 화면 전체를 덮는 값

    // ─────────────────────────────────────────────
    //  라이프사이클
    // ─────────────────────────────────────────────
    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            // 컴포넌트만 파괴 — 함께 붙은 다른 컴포넌트·자식 이미지 보호
            Destroy(this);
            return;
        }

        if (!_flashOverlay)
            _flashOverlay = CreateFlashOverlay();

        SetupShaderOverlays();
    }

    void Start()
    {
        // Volume → ColorAdjustments / ChromaticAberration 참조 획득
        if (_volume == null)
            _volume = FindAnyObjectByType<Volume>();

        if (_volume != null)
        {
            if (!_volume.profile.TryGet(out _colorAdjustments))
                Debug.LogWarning("[TransitionVFXController] Volume Profile에 ColorAdjustments 오버라이드가 없습니다. " +
                                 "Volume Profile에 Color Adjustments를 추가하세요.");

            _volume.profile.TryGet(out _chromaticAberration);
        }
        else
        {
            Debug.LogWarning("[TransitionVFXController] Scene에 Volume을 찾을 수 없습니다. LerpColorGrading이 동작하지 않습니다.");
        }

        // 카메라 참조 획득
        if (_cameraFollow == null)
            _cameraFollow = FindAnyObjectByType<CameraFollow>();

        _cam = (_cameraFollow != null)
            ? _cameraFollow.GetComponent<Camera>()
            : Camera.main;

        if (_cam == null)
            Debug.LogWarning("[TransitionVFXController] 카메라를 찾을 수 없습니다. CameraShake / CameraZoom이 동작하지 않습니다.");
    }

    void LateUpdate()
    {
        // CameraFollow.LateUpdate(order 0) 이후 흔들림 오프셋을 카메라에 덧씌움
        if (_cam != null && _shakeOffset != Vector3.zero)
            _cam.transform.position += _shakeOffset;
    }

    // ─────────────────────────────────────────────
    //  공개 API
    // ─────────────────────────────────────────────

    /// <summary>화면을 흰색으로 duration 초 동안 점멸시킵니다 (fade-in → fade-out).</summary>
    public void FlashWhite(float duration)
    {
        if (SettingsManager.Instance != null && SettingsManager.Instance.flashEffectDisabled) return;
        if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
        _flashCoroutine = StartCoroutine(FlashRoutine(duration));
    }

    /// <summary>카메라를 duration 초 동안 magnitude 강도로 흔듭니다.</summary>
    public void CameraShake(float duration, float magnitude)
    {
        if (_shakeCoroutine != null) StopCoroutine(_shakeCoroutine);
        _shakeCoroutine = StartCoroutine(ShakeRoutine(duration, magnitude));
    }

    /// <summary>
    /// Post-Processing Color Adjustments 값을 duration 초 동안 현재 값에서 목표 값으로 Lerp합니다.
    /// saturation / contrast 범위: -100 ~ 100
    /// </summary>
    public void LerpColorGrading(float saturation, float contrast, Color colorFilter, float duration)
    {
        if (_colorGradingCoroutine != null) StopCoroutine(_colorGradingCoroutine);
        _colorGradingCoroutine = StartCoroutine(ColorGradingRoutine(saturation, contrast, colorFilter, duration));
    }

    /// <summary>
    /// 카메라 orthographicSize를 targetOrthoSize까지 duration 초 동안 변경합니다.
    /// ※ 이 프로젝트는 2D Orthographic 카메라를 사용하므로 FOV 대신 orthographicSize로 줌을 제어합니다.
    /// </summary>
    public void CameraZoom(float targetOrthoSize, float duration)
    {
        if (_zoomCoroutine != null) StopCoroutine(_zoomCoroutine);
        _zoomCoroutine = StartCoroutine(ZoomRoutine(targetOrthoSize, duration));
    }

    /// <summary>
    /// GlassCrack 셰이더 기반 화면 균열 연출. duration 초 동안 _CrackAmount를 0→1로 애니메이션.
    /// impactUV는 균열 중심 (0~1), 기본값은 화면 중앙.
    /// </summary>
    public void StartScreenCrack(float duration, Vector2 impactUV)
    {
        if (_screenCrackCoroutine != null) StopCoroutine(_screenCrackCoroutine);
        _screenCrackCoroutine = StartCoroutine(ScreenCrackRoutine(duration, impactUV));
    }

    /// <summary>화면 중앙을 기준으로 StartScreenCrack 호출.</summary>
    public void StartScreenCrack(float duration)
    {
        StartScreenCrack(duration, new Vector2(0.5f, 0.5f));
    }

    /// <summary>
    /// GlassCrack 셰이더의 _ShatterAmount를 0→1로 애니메이션 + 이미지 확대/알파 페이드.
    /// duration 초 후 crack 오버레이는 비활성화됩니다.
    /// </summary>
    public void ShatterScreen(float duration)
    {
        if (_shatterCoroutine != null) StopCoroutine(_shatterCoroutine);
        _shatterCoroutine = StartCoroutine(ShatterRoutine(duration));
    }

    /// <summary>GlitchManager를 통해 duration 초 동안 글리치 플래시를 재생합니다.</summary>
    public void GlitchFlash(float duration)
    {
        if (SettingsManager.Instance != null && SettingsManager.Instance.flashEffectDisabled) return;
        var gm = GlitchManager.Instance;
        if (gm == null)
        {
            Debug.LogWarning("[TransitionVFXController] GlitchFlash: GlitchManager.Instance가 없습니다.");
            return;
        }
        gm.PlayGlitch(duration, GlitchManager.PresetMild);
    }

    /// <summary>
    /// ImpactFlash 셰이더 기반 임팩트 플래시 (중심점에서 방사형으로 퍼지는 빛 + ChromaticAberration 펄스).
    /// 기존 FlashWhite(CanvasGroup 알파)와 공존.
    /// </summary>
    public void ImpactFlash(Vector2 uv, float duration)
    {
        if (SettingsManager.Instance != null && SettingsManager.Instance.flashEffectDisabled) return;
        if (_impactFlashCoroutine != null) StopCoroutine(_impactFlashCoroutine);
        _impactFlashCoroutine = StartCoroutine(ImpactFlashRoutine(uv, duration));
    }

    // ─────────────────────────────────────────────
    //  공개 API — 수채화 번짐
    // ─────────────────────────────────────────────

    /// <summary>duration 초 동안 수채화 물감이 화면에 번지며 채색됩니다.</summary>
    public void StartWatercolorSpread(float duration)
    {
        if (_watercolorMat == null || _watercolorImage == null) return;
        if (_watercolorCoroutine != null) StopCoroutine(_watercolorCoroutine);
        _watercolorCoroutine = StartCoroutine(WatercolorSpreadRoutine(duration));
    }

    /// <summary>현재 번짐 상태에서 duration 초 동안 수채화 오버레이를 서서히 지웁니다.</summary>
    public void FadeOutWatercolor(float duration)
    {
        if (_watercolorImage == null || !_watercolorImage.gameObject.activeSelf) return;
        if (_watercolorCoroutine != null) StopCoroutine(_watercolorCoroutine);
        _watercolorCoroutine = StartCoroutine(WatercolorFadeOutRoutine(duration));
    }

    // ─────────────────────────────────────────────
    //  코루틴
    // ─────────────────────────────────────────────

    /// <summary>흰 플래시 — 전반부 fade-in, 후반부 fade-out.</summary>
    IEnumerator FlashRoutine(float duration)
    {
        if (_flashOverlay == null) yield break;

        float halfDuration = duration * 0.5f;
        float elapsed = 0f;

        _flashOverlay.gameObject.SetActive(true);

        // fade-in
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            _flashOverlay.alpha = Mathf.Lerp(0f, 1f, elapsed / halfDuration);
            yield return null;
        }
        _flashOverlay.alpha = 1f;

        elapsed = 0f;

        // fade-out
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            _flashOverlay.alpha = Mathf.Lerp(1f, 0f, elapsed / halfDuration);
            yield return null;
        }

        _flashOverlay.alpha = 0f;
        _flashOverlay.gameObject.SetActive(false);
        _flashCoroutine = null;
    }

    /// <summary>카메라 흔들림 — Perlin 노이즈 + 지수 감쇠.</summary>
    IEnumerator ShakeRoutine(float duration, float magnitude)
    {
        if (_cam == null)
        {
            Debug.LogWarning("[TransitionVFXController] CameraShake: 카메라 참조가 없습니다.");
            yield break;
        }

        _noiseOffsetX = Random.Range(0f, 100f);
        _noiseOffsetY = Random.Range(0f, 100f);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float envelope = Mathf.Exp(ShakeDecayExponent * elapsed / duration); // 지수 감쇠
            float x = (Mathf.PerlinNoise(_noiseOffsetX + elapsed * _shakeFrequency, 0f) * 2f - 1f)
                      * magnitude * envelope;
            float y = (Mathf.PerlinNoise(0f, _noiseOffsetY + elapsed * _shakeFrequency) * 2f - 1f)
                      * magnitude * envelope;
            _shakeOffset = new Vector3(x, y, 0f);

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        _shakeOffset    = Vector3.zero;
        _shakeCoroutine = null;
    }

    /// <summary>Color Adjustments Lerp — 현재 값 → 목표 값 (SmoothStep 이징).</summary>
    IEnumerator ColorGradingRoutine(float targetSaturation, float targetContrast,
                                    Color targetColorFilter, float duration)
    {
        if (_colorAdjustments == null)
        {
            Debug.LogWarning("[TransitionVFXController] LerpColorGrading: ColorAdjustments를 찾을 수 없습니다.");
            yield break;
        }

        float startSaturation  = _colorAdjustments.saturation.value;
        float startContrast    = _colorAdjustments.contrast.value;
        Color startColorFilter = _colorAdjustments.colorFilter.value;
        float elapsed          = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            _colorAdjustments.saturation.Override(Mathf.Lerp(startSaturation, targetSaturation, t));
            _colorAdjustments.contrast.Override(Mathf.Lerp(startContrast, targetContrast, t));
            _colorAdjustments.colorFilter.Override(Color.Lerp(startColorFilter, targetColorFilter, t));

            yield return null;
        }

        // 최종값 확정
        _colorAdjustments.saturation.Override(targetSaturation);
        _colorAdjustments.contrast.Override(targetContrast);
        _colorAdjustments.colorFilter.Override(targetColorFilter);
        _colorGradingCoroutine = null;
    }

    /// <summary>카메라 orthographicSize Lerp — ZoomIn / ZoomOut 공용.</summary>
    IEnumerator ZoomRoutine(float targetOrthoSize, float duration)
    {
        if (_cam == null)
        {
            Debug.LogWarning("[TransitionVFXController] CameraZoom: 카메라 참조가 없습니다.");
            yield break;
        }

        float startSize = _cam.orthographicSize;
        float elapsed   = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            _cam.orthographicSize = Mathf.Lerp(startSize, targetOrthoSize, elapsed / duration);
            yield return null;
        }

        _cam.orthographicSize = targetOrthoSize;
        _zoomCoroutine = null;
    }

    /// <summary>GlassCrack _CrackAmount 0→1 + 임팩트 포인트 설정. timeScale 0.3 대응(unscaled).</summary>
    IEnumerator ScreenCrackRoutine(float duration, Vector2 impactUV)
    {
        if (_crackMat == null || _glassCrackImage == null)
        {
            Debug.LogWarning("[TransitionVFXController] StartScreenCrack: _glassCrackImage 또는 머티리얼이 없습니다.");
            yield break;
        }

        _glassCrackImage.gameObject.SetActive(true);
        _glassCrackImage.transform.localScale = _crackImageOriginalScale;
        if (_crackCanvasGroup != null) _crackCanvasGroup.alpha = 1f;

        _crackMat.SetVector(ID_ImpactPoint, new Vector4(impactUV.x, impactUV.y, 0f, 0f));
        _crackMat.SetFloat(ID_CrackAmount, 0f);
        _crackMat.SetFloat(ID_ShatterAmount, 0f);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            _crackMat.SetFloat(ID_CrackAmount, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        _crackMat.SetFloat(ID_CrackAmount, 1f);
        _screenCrackCoroutine = null;
    }

    /// <summary>_ShatterAmount 0→1 + 이미지 확대 + 알파 페이드. 끝나면 비활성화.</summary>
    IEnumerator ShatterRoutine(float duration)
    {
        if (_crackMat == null || _glassCrackImage == null)
        {
            Debug.LogWarning("[TransitionVFXController] ShatterScreen: _glassCrackImage 또는 머티리얼이 없습니다.");
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.SmoothStep(0f, 1f, t);

            _crackMat.SetFloat(ID_ShatterAmount, eased);
            _glassCrackImage.transform.localScale =
                Vector3.Lerp(_crackImageOriginalScale, _crackImageOriginalScale * _shatterScaleMax, eased);

            if (_crackCanvasGroup != null)
                _crackCanvasGroup.alpha = 1f - eased;

            yield return null;
        }

        _crackMat.SetFloat(ID_ShatterAmount, 1f);
        _glassCrackImage.gameObject.SetActive(false);
        _glassCrackImage.transform.localScale = _crackImageOriginalScale;
        if (_crackCanvasGroup != null) _crackCanvasGroup.alpha = 1f;
        _shatterCoroutine = null;
    }

    /// <summary>ImpactFlash 셰이더 + ChromaticAberration 동시 펄스 (0→피크→0).</summary>
    IEnumerator ImpactFlashRoutine(Vector2 uv, float duration)
    {
        if (_flashMat == null || _impactFlashImage == null)
        {
            Debug.LogWarning("[TransitionVFXController] ImpactFlash: _impactFlashImage 또는 머티리얼이 없습니다.");
            yield break;
        }

        _impactFlashImage.gameObject.SetActive(true);
        _flashMat.SetVector(ID_FlashCenter, new Vector4(uv.x, uv.y, 0f, 0f));
        _flashMat.SetFloat(ID_FlashAmount, 0f);

        float startChroma = (_chromaticAberration != null) ? _chromaticAberration.intensity.value : 0f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // 0 → 1 → 0 (삼각형 피크)
            float pulse = (t < 0.5f) ? (t * 2f) : ((1f - t) * 2f);

            _flashMat.SetFloat(ID_FlashAmount, pulse);
            if (_chromaticAberration != null)
                _chromaticAberration.intensity.Override(Mathf.Lerp(startChroma, _chromaticFlashPeak, pulse));

            yield return null;
        }

        _flashMat.SetFloat(ID_FlashAmount, 0f);
        _impactFlashImage.gameObject.SetActive(false);
        if (_chromaticAberration != null)
            _chromaticAberration.intensity.Override(startChroma);

        _impactFlashCoroutine = null;
    }

    /// <summary>수채화 번짐: _SpreadAmount 0 → WatercolorSpreadMax (완전 커버 보장) 애니메이션.</summary>
    IEnumerator WatercolorSpreadRoutine(float duration)
    {
        SetImageAlpha(_watercolorImage, 1f);
        _watercolorMat.SetFloat(ID_SpreadAmount, 0f);
        _watercolorImage.gameObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            // FBM 최대값 ~0.94이므로 WatercolorSpreadMax까지 올려 전체 화면을 완전히 덮음
            _watercolorMat.SetFloat(ID_SpreadAmount, Mathf.Lerp(0f, WatercolorSpreadMax, elapsed / duration));
            yield return null;
        }
        _watercolorMat.SetFloat(ID_SpreadAmount, WatercolorSpreadMax);
        _watercolorCoroutine = null;
    }

    /// <summary>수채화 페이드아웃: Image.color.a 를 0 으로 낮춘 뒤 비활성화.</summary>
    IEnumerator WatercolorFadeOutRoutine(float duration)
    {
        Color startColor = _watercolorImage.color;
        float elapsed    = 0f;
        Color c          = startColor;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            c.a = Mathf.Lerp(startColor.a, 0f, t);
            _watercolorImage.color = c;
            _watercolorMat.SetFloat(ID_SpreadAmount, Mathf.Lerp(WatercolorSpreadMax, 0f, t));
            yield return null;
        }

        c.a = 0f;
        _watercolorImage.color = c;
        _watercolorImage.gameObject.SetActive(false);
        _watercolorMat.SetFloat(ID_SpreadAmount, 0f);
        _watercolorCoroutine = null;
    }

    // ─────────────────────────────────────────────
    //  내부 헬퍼
    // ─────────────────────────────────────────────

    /// <summary>_glassCrackImage / _impactFlashImage 의 머티리얼을 인스턴스화하고 초기 상태로 세팅.</summary>
    void SetupShaderOverlays()
    {
        if (_glassCrackImage != null && _glassCrackImage.material != null)
        {
            _crackMat = new Material(_glassCrackImage.material);
            _glassCrackImage.material = _crackMat;
            _crackMat.SetFloat(ID_CrackAmount, 0f);
            _crackMat.SetFloat(ID_ShatterAmount, 0f);
            _crackImageOriginalScale = _glassCrackImage.transform.localScale;
            _crackCanvasGroup = _glassCrackImage.GetComponent<CanvasGroup>();
            _glassCrackImage.gameObject.SetActive(false);
        }

        if (_impactFlashImage != null && _impactFlashImage.material != null)
        {
            _flashMat = new Material(_impactFlashImage.material);
            _impactFlashImage.material = _flashMat;
            _flashMat.SetFloat(ID_FlashAmount, 0f);
            _impactFlashImage.gameObject.SetActive(false);
        }

        if (_watercolorImage != null && _watercolorImage.material != null)
        {
            _watercolorMat = new Material(_watercolorImage.material);
            _watercolorImage.material = _watercolorMat;
            _watercolorMat.SetFloat(ID_SpreadAmount, 0f);
            _watercolorImage.gameObject.SetActive(false);
        }

        if (_daggerImage != null)
        {
            _daggerGroup = _daggerImage.GetComponent<CanvasGroup>();
            if (_daggerGroup == null) _daggerGroup = _daggerImage.gameObject.AddComponent<CanvasGroup>();
            _daggerGroup.alpha = 0f;
            _daggerRect = _daggerImage.GetComponent<RectTransform>();
            _daggerImage.gameObject.SetActive(false);
        }

        if (_marshmallowImage != null)
        {
            SetImageAlpha(_marshmallowImage, 0f);
            _marshmallowImage.gameObject.SetActive(false);
        }

        if (_marshmallowGlowImage != null)
        {
            SetImageAlpha(_marshmallowGlowImage, 0f);
            _glowRect = _marshmallowGlowImage.GetComponent<RectTransform>();
            _marshmallowGlowImage.gameObject.SetActive(false);
        }
    }

    static void SetImageAlpha(Image img, float alpha)
    {
        if (img == null) return;
        Color c = img.color; c.a = alpha; img.color = c;
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
        if (_crackMat      != null) Destroy(_crackMat);
        if (_flashMat      != null) Destroy(_flashMat);
        if (_watercolorMat != null) Destroy(_watercolorMat);
    }

    // ─────────────────────────────────────────────
    //  공개 API — 단검 찌르기
    // ─────────────────────────────────────────────

    /// <summary>단검 이미지가 Inspector에 연결되어 있으면 true.</summary>
    public bool HasDaggerImage => _daggerRect != null;

    /// <summary>단검이 박히는 화면 UV 위치. BattleGlitchTransition에서 crack 기준점으로 사용.</summary>
    public Vector2 DaggerStabUV => _daggerStabUV;

    /// <summary>
    /// 단검이 _daggerStartUV에서 _daggerStabUV로 날아온 뒤 박힙니다.
    /// 박히는 순간 onImpact 콜백을 실행합니다.
    /// </summary>
    public void PlayDaggerStab(float flyDuration, System.Action onImpact)
    {
        if (_daggerCoroutine != null) StopCoroutine(_daggerCoroutine);
        _daggerCoroutine = StartCoroutine(DaggerStabRoutine(flyDuration, onImpact));
    }

    // ─────────────────────────────────────────────
    //  공개 API — 마시멜로 / 후광
    // ─────────────────────────────────────────────

    /// <summary>마시멜로 이미지와 후광을 fadeInDuration 초 동안 페이드인한 뒤 후광 맥동을 시작합니다.</summary>
    public void ShowMarshmallow(float fadeInDuration)
    {
        if (_marshmallowCoroutine != null) StopCoroutine(_marshmallowCoroutine);
        _marshmallowCoroutine = StartCoroutine(MarshmallowFadeInRoutine(fadeInDuration));
    }

    /// <summary>후광 맥동을 멈추고 마시멜로와 후광을 fadeOutDuration 초 동안 페이드아웃합니다.</summary>
    public void HideMarshmallow(float fadeOutDuration)
    {
        if (_glowPulseCoroutine != null) { StopCoroutine(_glowPulseCoroutine); _glowPulseCoroutine = null; }
        if (_marshmallowCoroutine != null) StopCoroutine(_marshmallowCoroutine);
        _marshmallowCoroutine = StartCoroutine(MarshmallowFadeOutRoutine(fadeOutDuration));
    }

    // ─────────────────────────────────────────────
    //  코루틴 — 단검
    // ─────────────────────────────────────────────

    static Vector2 UVToScreenLocal(Vector2 uv)
        => new Vector2((uv.x - 0.5f) * Screen.width, (uv.y - 0.5f) * Screen.height);

    void SetupDaggerTransform(Vector2 startPos, Vector2 stabPos)
    {
        Vector2 dir = stabPos - startPos;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + _daggerAngleOffset;
        _daggerRect.localEulerAngles = new Vector3(0f, 0f, angle);
        _daggerRect.anchoredPosition = startPos;
    }

    IEnumerator DaggerStabRoutine(float duration, System.Action onImpact)
    {
        if (_daggerRect == null) { onImpact?.Invoke(); yield break; }

        Vector2 startPos = UVToScreenLocal(_daggerStartUV);
        Vector2 stabPos  = UVToScreenLocal(_daggerStabUV);

        SetupDaggerTransform(startPos, stabPos);

        bool hasGroup = _daggerGroup != null;

        _daggerImage.gameObject.SetActive(true);
        if (hasGroup) _daggerGroup.alpha = 0f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // 이징: 가속으로 날아오는 느낌
            float eased = 1f - (1f - t) * (1f - t);
            _daggerRect.anchoredPosition = Vector2.Lerp(startPos, stabPos, eased);
            if (hasGroup) _daggerGroup.alpha = Mathf.Clamp01(t * DaggerFadeInCurve);
            yield return null;
        }

        _daggerRect.anchoredPosition = stabPos;
        if (hasGroup) _daggerGroup.alpha = 1f;

        onImpact?.Invoke();

        // 산산조각 연출 후 단검 페이드아웃
        yield return _waitDaggerFade;

        float fadeElapsed = 0f;
        const float daggerFadeDuration = 0.15f;
        while (fadeElapsed < daggerFadeDuration)
        {
            fadeElapsed += Time.unscaledDeltaTime;
            if (hasGroup) _daggerGroup.alpha = Mathf.Lerp(1f, 0f, fadeElapsed / daggerFadeDuration);
            yield return null;
        }

        _daggerImage.gameObject.SetActive(false);
        if (hasGroup) _daggerGroup.alpha = 0f;
        _daggerCoroutine = null;
    }

    // ─────────────────────────────────────────────
    //  코루틴 — 마시멜로 / 후광
    // ─────────────────────────────────────────────

    IEnumerator MarshmallowFadeInRoutine(float duration)
    {
        bool hasM = _marshmallowImage     != null;
        bool hasG = _marshmallowGlowImage != null;
        if (!hasM && !hasG) yield break;

        if (hasM) _marshmallowImage.gameObject.SetActive(true);
        if (hasG) _marshmallowGlowImage.gameObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(elapsed / duration);
            if (hasM) SetImageAlpha(_marshmallowImage,     a);
            if (hasG) SetImageAlpha(_marshmallowGlowImage, a * GlowAlphaRatio);
            yield return null;
        }

        // 페이드인 완료 → 후광 맥동 시작
        if (_glowPulseCoroutine != null) StopCoroutine(_glowPulseCoroutine);
        _glowPulseCoroutine = StartCoroutine(GlowPulseRoutine());
        _marshmallowCoroutine = null;
    }

    IEnumerator MarshmallowFadeOutRoutine(float duration)
    {
        bool hasM = _marshmallowImage     != null;
        bool hasG = _marshmallowGlowImage != null;

        if (hasG) _glowRect.localScale = Vector3.one;

        float startAlphaM = hasM ? _marshmallowImage.color.a     : 0f;
        float startAlphaG = hasG ? _marshmallowGlowImage.color.a : 0f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            if (hasM) SetImageAlpha(_marshmallowImage,     Mathf.Lerp(startAlphaM, 0f, t));
            if (hasG) SetImageAlpha(_marshmallowGlowImage, Mathf.Lerp(startAlphaG, 0f, t));
            yield return null;
        }

        if (hasM) _marshmallowImage.gameObject.SetActive(false);
        if (hasG) _marshmallowGlowImage.gameObject.SetActive(false);
        _marshmallowCoroutine = null;
    }

    IEnumerator GlowPulseRoutine()
    {
        if (_glowRect == null) yield break;

        Vector3 baseScale = _glowRect.localScale;

        while (true)
        {
            float t = (Mathf.Sin(Time.unscaledTime * _glowPulseSpeed * Mathf.PI) + 1f) * 0.5f;
            float s = Mathf.Lerp(_glowPulseMin, _glowPulseMax, t);
            _glowRect.localScale = baseScale * s;
            yield return null;
        }
    }

    /// <summary>흰 플래시용 CanvasGroup을 런타임에 자동 생성합니다.</summary>
    CanvasGroup CreateFlashOverlay()
    {
        var canvasGo = new GameObject("FlashOverlay [Auto]");
        DontDestroyOnLoad(canvasGo);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 998; // TransitionManager(999) 바로 아래

        UiCanvasScale.Add(canvasGo);   // 640x360 Expand — 단일 출처
        canvasGo.AddComponent<GraphicRaycaster>();

        var imageGo = new GameObject("WhiteImage");
        imageGo.transform.SetParent(canvasGo.transform, false);

        var image = imageGo.AddComponent<Image>();
        image.color = Color.white;

        var rect = imageGo.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var cg = imageGo.AddComponent<CanvasGroup>();
        cg.alpha           = 0f;
        cg.blocksRaycasts  = false;
        imageGo.SetActive(false); // 플래시 비활성 상태로 시작
        return cg;
    }
}
