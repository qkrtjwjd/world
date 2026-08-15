using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 턴제 전투 UI의 전환 연출을 담당합니다.
/// BattleTransitionManager의 전환 코루틴에서 호출하세요.
///
/// [에디터 설정]
/// - _crackImage    : fillAmount로 균열을 표현할 Image (FillMethod 설정 필요)
/// - _uiElements    : 흔들기·폭발·등장 연출 대상 RectTransform 목록
/// - _explodeDistance: ExplodeUI 시 요소가 날아가는 최대 거리(픽셀), 기본 400
///
/// ※ CanvasGroup 이 없는 요소는 알파 제어를 건너뜁니다.
/// </summary>
public class TransitionUIController : MonoBehaviour
{
    // ─────────────────────────────────────────────
    //  싱글톤
    // ─────────────────────────────────────────────
    public static TransitionUIController Instance
    {
        get
        {
            if (!_instance)
            {
                var go = new GameObject("TransitionUIController [Auto]");
                _instance = go.AddComponent<TransitionUIController>();
            }
            return _instance;
        }
    }
    private static TransitionUIController _instance;

    // ─────────────────────────────────────────────
    //  Inspector 설정
    // ─────────────────────────────────────────────
    [Header("균열 오버레이")]
    [Tooltip("fillAmount 로 균열 연출할 Image. Fill Method 를 Radial360 등으로 설정하세요.")]
    [SerializeField] private Image _crackImage;

    [Header("전환 대상 UI 요소 목록")]
    [Tooltip("ShakeUIElements / ExplodeUI / BloomInTurnUI 의 대상 RectTransform 목록.")]
    [SerializeField] private RectTransform[] _uiElements;

    [Header("폭발 설정")]
    [Tooltip("ExplodeUI 시 요소가 날아가는 기준 거리(픽셀). 실제 거리는 0.7~1.3배 랜덤 보정됩니다.")]
    [SerializeField] private float _explodeDistance = 400f;

    // ─────────────────────────────────────────────
    //  원본 상태 캐시 (Start에서 초기화)
    // ─────────────────────────────────────────────
    private Vector2[]     _originalPositions;
    private Vector3[]     _originalScales;
    private float[]       _originalRotationsZ;  // localEulerAngles.z
    private CanvasGroup[] _elementGroups;        // null 가능 (없으면 알파 제어 생략)

    // ─────────────────────────────────────────────
    //  코루틴 핸들 (중복 방지)
    // ─────────────────────────────────────────────
    private Coroutine _crackCoroutine;
    private Coroutine _shakeCoroutine;
    private Coroutine _explodeCoroutine;
    private Coroutine _bloomCoroutine;

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
            // 컴포넌트만 파괴 — 매니저 루트 오브젝트에 함께 붙은 다른 컴포넌트 보호
            Destroy(this);
            return;
        }
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    void Start()
    {
        CacheOriginalState();

        if (_crackImage != null)
        {
            _crackImage.fillAmount = 0f;
            _crackImage.gameObject.SetActive(false);
        }
    }

    // ─────────────────────────────────────────────
    //  공개 API
    // ─────────────────────────────────────────────

    /// <summary>균열 Image의 fillAmount를 0 → 1로 duration 초 동안 진행합니다.</summary>
    public void StartCrackOverlay(float duration)
    {
        if (_crackCoroutine != null) StopCoroutine(_crackCoroutine);
        _crackCoroutine = StartCoroutine(CrackRoutine(duration));
    }

    /// <summary>각 UI 요소의 anchoredPosition을 intensity 범위로 duration 초 동안 흔듭니다.</summary>
    public void ShakeUIElements(float duration, float intensity)
    {
        if (_shakeCoroutine != null)
        {
            StopCoroutine(_shakeCoroutine);
            RestorePositions(); // 중단 시 위치 복구
        }
        _shakeCoroutine = StartCoroutine(ShakeRoutine(duration, intensity));
    }

    /// <summary>
    /// 각 UI 요소가 랜덤 방향으로 날아가며 사라집니다.
    /// 코루틴 완료 후 요소는 SetActive(false) + 원본 상태 복구됩니다.
    /// </summary>
    public void ExplodeUI(float duration)
    {
        if (_explodeCoroutine != null) StopCoroutine(_explodeCoroutine);
        _explodeCoroutine = StartCoroutine(ExplodeRoutine(duration));
    }

    /// <summary>
    /// 각 UI 요소가 스케일 0 → 바운스 → 1로 순차 등장합니다.
    /// staggerDelay 초 간격으로 요소가 하나씩 시작됩니다. (권장값: 0.1f)
    /// </summary>
    public void BloomInTurnUI(float duration, float staggerDelay)
    {
        if (_bloomCoroutine != null) StopCoroutine(_bloomCoroutine);
        _bloomCoroutine = StartCoroutine(BloomInRoutine(duration, staggerDelay));
    }

    // ─────────────────────────────────────────────
    //  코루틴
    // ─────────────────────────────────────────────

    /// <summary>균열 오버레이 fillAmount 0 → 1.</summary>
    IEnumerator CrackRoutine(float duration)
    {
        if (_crackImage == null)
        {
            Debug.LogWarning("[TransitionUIController] StartCrackOverlay: _crackImage 가 연결되지 않았습니다.");
            yield break;
        }

        _crackImage.gameObject.SetActive(true);
        _crackImage.fillAmount = 0f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            _crackImage.fillAmount = Mathf.Lerp(0f, 1f, elapsed / duration);
            yield return null;
        }

        _crackImage.fillAmount = 1f;
        _crackCoroutine = null;
    }

    /// <summary>UI 요소 흔들기 — 시간이 지날수록 선형 감쇠.</summary>
    IEnumerator ShakeRoutine(float duration, float intensity)
    {
        if (!ValidateElements("ShakeUIElements")) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float envelope = 1f - (elapsed / duration); // 선형 감쇠

            for (int i = 0; i < _uiElements.Length; i++)
            {
                if (_uiElements[i] == null) continue;
                float ox = Random.Range(-1f, 1f) * intensity * envelope;
                float oy = Random.Range(-1f, 1f) * intensity * envelope;
                _uiElements[i].anchoredPosition = _originalPositions[i] + new Vector2(ox, oy);
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        RestorePositions();
        _shakeCoroutine = null;
    }

    /// <summary>UI 폭발 연출 — 위치·회전·스케일·알파를 동시에 애니메이션.</summary>
    IEnumerator ExplodeRoutine(float duration)
    {
        if (!ValidateElements("ExplodeUI")) yield break;

        int count = _uiElements.Length;

        // BloomIn DOTween 트윈이 남아 있을 경우 정리
        for (int i = 0; i < count; i++)
            if (_uiElements[i] != null) DOTween.Kill(_uiElements[i]);

        // 요소별 랜덤 파라미터 사전 계산
        Vector2[] dirs      = new Vector2[count];
        float[]   distances = new float[count];
        float[]   rotSpeeds = new float[count]; // 도/초, 부호 = 방향
        float[]   startAlphas = new float[count];

        for (int i = 0; i < count; i++)
        {
            if (_uiElements[i] == null) continue;

            // 활성화 보장 (ExplodeUI 직전에 비활성화된 경우 대비)
            _uiElements[i].gameObject.SetActive(true);

            Vector2 rnd = Random.insideUnitCircle;
            dirs[i]      = (rnd.sqrMagnitude < 0.001f ? Vector2.up : rnd).normalized;
            distances[i] = _explodeDistance * Random.Range(0.7f, 1.3f);
            rotSpeeds[i] = Random.Range(90f, 270f) * (Random.value > 0.5f ? 1f : -1f);
            startAlphas[i] = _elementGroups[i] != null ? _elementGroups[i].alpha : 1f;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            for (int i = 0; i < count; i++)
            {
                if (_uiElements[i] == null) continue;

                _uiElements[i].anchoredPosition =
                    _originalPositions[i] + dirs[i] * (distances[i] * t);

                _uiElements[i].localScale =
                    Vector3.Lerp(_originalScales[i], Vector3.zero, t);

                _uiElements[i].localEulerAngles =
                    new Vector3(0f, 0f, _originalRotationsZ[i] + rotSpeeds[i] * elapsed);

                if (_elementGroups[i] != null)
                    _elementGroups[i].alpha = Mathf.Lerp(startAlphas[i], 0f, t);
            }

            yield return null;
        }

        // 정리: 비활성화 + 원본 상태 복구 (BloomInTurnUI 재사용을 위해)
        for (int i = 0; i < count; i++)
        {
            if (_uiElements[i] == null) continue;
            _uiElements[i].gameObject.SetActive(false);
            _uiElements[i].anchoredPosition = _originalPositions[i];
            _uiElements[i].localScale       = _originalScales[i];
            _uiElements[i].localEulerAngles = new Vector3(0f, 0f, _originalRotationsZ[i]);
            if (_elementGroups[i] != null) _elementGroups[i].alpha = 1f;
        }

        _explodeCoroutine = null;
    }

    /// <summary>
    /// UI 순차 등장 — DOTween Ease.OutElastic으로 요소별 stagger 팝인.
    /// SetUpdate(true) 로 TimeScale 무관 동작.
    /// </summary>
    IEnumerator BloomInRoutine(float duration, float staggerDelay)
    {
        if (!ValidateElements("BloomInTurnUI")) yield break;

        int count = _uiElements.Length;

        // 전 요소 초기화: 활성화 + 스케일 0 + 알파 1
        for (int i = 0; i < count; i++)
        {
            if (_uiElements[i] == null) continue;
            DOTween.Kill(_uiElements[i]); // 이전 트윈 정리
            _uiElements[i].gameObject.SetActive(true);
            _uiElements[i].localScale = Vector3.zero;
            if (_elementGroups[i] != null) _elementGroups[i].alpha = 1f;
        }

        for (int i = 0; i < count; i++)
        {
            if (_uiElements[i] == null) continue;
            _uiElements[i]
                .DOScale(_originalScales[i], duration)
                .SetEase(Ease.OutElastic)
                .SetDelay(i * staggerDelay)
                .SetUpdate(true);
        }

        float totalDuration = (count - 1) * staggerDelay + duration;
        yield return new WaitForSecondsRealtime(totalDuration + 0.05f);

        // 최종값 확정
        for (int i = 0; i < count; i++)
        {
            if (_uiElements[i] == null) continue;
            _uiElements[i].localScale = _originalScales[i];
        }

        _bloomCoroutine = null;
    }

    // ─────────────────────────────────────────────
    //  내부 헬퍼
    // ─────────────────────────────────────────────

    /// <summary>원본 anchoredPosition 복구. ShakeUIElements 중단 시 호출.</summary>
    void RestorePositions()
    {
        if (_uiElements == null || _originalPositions == null) return;
        for (int i = 0; i < _uiElements.Length; i++)
        {
            if (_uiElements[i] != null)
                _uiElements[i].anchoredPosition = _originalPositions[i];
        }
    }

    /// <summary>Start() 에서 각 요소의 원본 상태와 CanvasGroup 을 캐시합니다.</summary>
    void CacheOriginalState()
    {
        if (_uiElements == null || _uiElements.Length == 0) return;

        int count = _uiElements.Length;
        _originalPositions  = new Vector2[count];
        _originalScales     = new Vector3[count];
        _originalRotationsZ = new float[count];
        _elementGroups      = new CanvasGroup[count];

        for (int i = 0; i < count; i++)
        {
            if (_uiElements[i] == null) continue;
            _originalPositions[i]  = _uiElements[i].anchoredPosition;
            _originalScales[i]     = _uiElements[i].localScale;
            _originalRotationsZ[i] = _uiElements[i].localEulerAngles.z;
            _elementGroups[i]      = _uiElements[i].GetComponent<CanvasGroup>();
        }
    }

    /// <summary>_uiElements 유효성 검사. 비어있으면 LogWarning 후 false 반환.</summary>
    bool ValidateElements(string callerName)
    {
        if (_uiElements == null || _uiElements.Length == 0)
        {
            Debug.LogWarning($"[TransitionUIController] {callerName}: _uiElements 가 비어 있습니다.");
            return false;
        }
        return true;
    }
}
