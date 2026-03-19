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
    public float switchDuration = 0.15f;

    [Tooltip("인형화 80%+ 시 강제 현실 유지 시간 (초)")]
    public float forcedRealityDuration = 0.5f;

    public bool IsReality { get; private set; } = false;

    private RealityFilterObject[] _filterObjects = new RealityFilterObject[0];
    private Coroutine _fadeCoroutine;
    private Coroutine _forcedReturnCoroutine;

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
    }

    void CacheFilterObjects()
    {
        _filterObjects = FindObjectsOfType<RealityFilterObject>();
    }

    void Update()
    {
        if (DialogueManager.Instance != null && DialogueManager.Instance.isTalking)
            return;

        if (Input.GetKeyDown(KeyCode.F))
            SwitchToReality();

        if (Input.GetKeyUp(KeyCode.F))
            SwitchToFantasy();
    }

    void SwitchToReality()
    {
        if (IsReality) return;

        IsReality = true;

        if (GlitchManager.Instance != null)
            GlitchManager.Instance.PlayGlitch(switchDuration);

        StartFade(1f);
        ApplyFilter(true);

        if (IsHighPuppetization())
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
            GlitchManager.Instance.PlayGlitch(switchDuration);

        StartFade(0f);
        ApplyFilter(false);
    }

    IEnumerator ForcedReturnRoutine()
    {
        yield return new WaitForSeconds(forcedRealityDuration);
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

    bool IsHighPuppetization()
    {
        if (CorruptionManager.instance == null) return false;
        float ratio = CorruptionManager.instance.currentCorruption / CorruptionManager.instance.maxCorruption;
        return ratio >= 0.8f;
    }
}
