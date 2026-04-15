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
                var go = new GameObject("TransitionVFXController [Auto]");
                _instance = go.AddComponent<TransitionVFXController>();
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

    [Header("Post-Processing")]
    [Tooltip("Color Adjustments 오버라이드가 포함된 Global Volume.")]
    [SerializeField] private Volume _volume;

    [Header("카메라")]
    [Tooltip("CameraFollow가 붙은 오브젝트. 비워두면 자동 탐색합니다.")]
    [SerializeField] private CameraFollow _cameraFollow;

    // ─────────────────────────────────────────────
    //  내부 상태
    // ─────────────────────────────────────────────
    private ColorAdjustments _colorAdjustments;
    private Camera _cam;

    // 카메라 흔들림 — LateUpdate에서 CameraFollow 위치에 덧씌움
    private Vector3 _shakeOffset = Vector3.zero;

    // 실행 중인 코루틴 핸들 (동일 효과 중복 방지)
    private Coroutine _flashCoroutine;
    private Coroutine _shakeCoroutine;
    private Coroutine _colorGradingCoroutine;
    private Coroutine _zoomCoroutine;

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
            Destroy(gameObject);
            return;
        }

        if (!_flashOverlay)
            _flashOverlay = CreateFlashOverlay();
    }

    void Start()
    {
        // Volume → ColorAdjustments 참조 획득
        if (_volume == null)
            _volume = FindAnyObjectByType<Volume>();

        if (_volume != null)
        {
            if (!_volume.profile.TryGet(out _colorAdjustments))
                Debug.LogWarning("[TransitionVFXController] Volume Profile에 ColorAdjustments 오버라이드가 없습니다. " +
                                 "Volume Profile에 Color Adjustments를 추가하세요.");
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
    /// 카메라 orthographicSize를 targetOrthoSize까지 duration 초 동안 확대합니다.
    /// ※ 이 프로젝트는 2D Orthographic 카메라를 사용하므로 FOV 대신 orthographicSize로 줌을 제어합니다.
    /// </summary>
    public void CameraZoomIn(float targetOrthoSize, float duration)
    {
        if (_zoomCoroutine != null) StopCoroutine(_zoomCoroutine);
        _zoomCoroutine = StartCoroutine(ZoomRoutine(targetOrthoSize, duration));
    }

    /// <summary>
    /// 카메라 orthographicSize를 targetOrthoSize까지 duration 초 동안 축소합니다.
    /// ※ 이 프로젝트는 2D Orthographic 카메라를 사용하므로 FOV 대신 orthographicSize로 줌을 제어합니다.
    /// </summary>
    public void CameraZoomOut(float targetOrthoSize, float duration)
    {
        if (_zoomCoroutine != null) StopCoroutine(_zoomCoroutine);
        _zoomCoroutine = StartCoroutine(ZoomRoutine(targetOrthoSize, duration));
    }

    // ─────────────────────────────────────────────
    //  공개 API — TODO 스텁 (BattleTransitionManager에서 호출)
    // ─────────────────────────────────────────────

    /// <summary>화면 균열 셰이더/이미지 연출. duration 초 동안 실행.</summary>
    // TODO: 화면 균열 셰이더 또는 Image 오버레이 연출 구현
    public void StartScreenCrack(float duration) { }

    /// <summary>적 캐릭터 현실화 변신 VFX. duration 초 동안 실행.</summary>
    // TODO: 적 캐릭터 현실화 변신 파티클/셰이더 VFX 구현
    public void PlayEnemyTransformVFX(float duration) { }

    /// <summary>지정 위치에 녹아내리는 파티클을 스폰합니다.</summary>
    // TODO: position 위치에 마시멜로 녹는 파티클 시스템 스폰 구현
    public void SpawnMeltParticles(Vector3 position) { }

    /// <summary>수채화 번짐 효과. duration 초 동안 실행.</summary>
    // TODO: URP Custom Pass 또는 셰이더 기반 수채화 번짐 연출 구현
    public void StartWatercolorSpread(float duration) { }

    /// <summary>적 캐릭터 환상화 디졸브 VFX. duration 초 동안 실행.</summary>
    // TODO: 적 캐릭터 환상 디졸브 파티클/셰이더 VFX 구현
    public void PlayEnemyDissolveToFantasy(float duration) { }

    // ─────────────────────────────────────────────
    //  코루틴
    // ─────────────────────────────────────────────

    /// <summary>흰 플래시 — 전반부 fade-in, 후반부 fade-out.</summary>
    IEnumerator FlashRoutine(float duration)
    {
        if (_flashOverlay == null) yield break;

        float half    = duration * 0.5f;
        float elapsed = 0f;

        _flashOverlay.gameObject.SetActive(true);

        // fade-in
        while (elapsed < half)
        {
            elapsed += Time.unscaledDeltaTime;
            _flashOverlay.alpha = Mathf.Lerp(0f, 1f, elapsed / half);
            yield return null;
        }
        _flashOverlay.alpha = 1f;

        elapsed = 0f;

        // fade-out
        while (elapsed < half)
        {
            elapsed += Time.unscaledDeltaTime;
            _flashOverlay.alpha = Mathf.Lerp(1f, 0f, elapsed / half);
            yield return null;
        }

        _flashOverlay.alpha = 0f;
        _flashOverlay.gameObject.SetActive(false);
        _flashCoroutine = null;
    }

    /// <summary>카메라 흔들림 — 시간이 지날수록 envelope로 감쇠.</summary>
    IEnumerator ShakeRoutine(float duration, float magnitude)
    {
        if (_cam == null)
        {
            Debug.LogWarning("[TransitionVFXController] CameraShake: 카메라 참조가 없습니다.");
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float envelope = 1f - (elapsed / duration); // 선형 감쇠
            float x = Random.Range(-1f, 1f) * magnitude * envelope;
            float y = Random.Range(-1f, 1f) * magnitude * envelope;
            _shakeOffset = new Vector3(x, y, 0f);

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        _shakeOffset    = Vector3.zero;
        _shakeCoroutine = null;
    }

    /// <summary>Color Adjustments Lerp — 현재 값 → 목표 값.</summary>
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
            float t = elapsed / duration;

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

    // ─────────────────────────────────────────────
    //  내부 헬퍼
    // ─────────────────────────────────────────────

    /// <summary>흰 플래시용 CanvasGroup을 런타임에 자동 생성합니다.</summary>
    CanvasGroup CreateFlashOverlay()
    {
        var canvasGo = new GameObject("FlashOverlay [Auto]");
        DontDestroyOnLoad(canvasGo);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 998; // TransitionManager(999) 바로 아래

        canvasGo.AddComponent<CanvasScaler>();
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
