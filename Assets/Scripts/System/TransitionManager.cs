using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 모든 순간이동/씬 전환에 검은색 페이드 효과를 적용합니다.
/// DontDestroyOnLoad로 씬 전환 후에도 유지됩니다.
/// </summary>
public class TransitionManager : MonoBehaviour
{
    public static TransitionManager Instance
    {
        get
        {
            if (!_instance)
            {
                var go = new GameObject("TransitionManager [Auto]");
                _instance = go.AddComponent<TransitionManager>();
            }
            return _instance;
        }
    }
    private static TransitionManager _instance;

    [Header("오버레이")]
    [Tooltip("검은색 Image를 가진 Canvas의 CanvasGroup을 연결하세요.")]
    [SerializeField] private CanvasGroup fadeOverlay;

    [Header("설정")]
    [SerializeField] private float defaultFadeDuration = 0.3f;

    // 씬 로드 시 검은 화면에서 시작해야 하는지 여부 (DoSceneTransition 이후)
    public static bool IsFadedIn { get; private set; } = false;

    private bool _isTransitioning = false;
    private ClearSky.SimplePlayerController _ctrl;
    private Rigidbody2D _playerRb;

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
        else
        {
            Destroy(gameObject);
            return;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;

        if (!fadeOverlay)
            fadeOverlay = CreateFadeOverlay();

        if (!IsFadedIn)
        {
            fadeOverlay.alpha = 0f;
            fadeOverlay.blocksRaycasts = false;
            fadeOverlay.gameObject.SetActive(false);
        }
    }

    CanvasGroup CreateFadeOverlay()
    {
        var canvasGo = new GameObject("FadeOverlay [Auto]");
        DontDestroyOnLoad(canvasGo);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        var imageGo = new GameObject("BlackImage");
        imageGo.transform.SetParent(canvasGo.transform, false);

        var image = imageGo.AddComponent<Image>();
        image.color = Color.black;

        var rect = imageGo.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var cg = imageGo.AddComponent<CanvasGroup>();
        return cg;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // ─────────────────────────────────────────────
    //  씬 로드 콜백: 씬 전환 후 페이드 인
    // ─────────────────────────────────────────────
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _ctrl = null;
        _playerRb = null;

        if (!IsFadedIn) return;

        if (fadeOverlay)
        {
            fadeOverlay.gameObject.SetActive(true);
            fadeOverlay.alpha = 1f;
            fadeOverlay.blocksRaycasts = true;
        }

        IsFadedIn = false;
        LockPlayer();
        StartCoroutine(FadeRoutine(0f, defaultFadeDuration, UnlockPlayer));
    }

    // ─────────────────────────────────────────────
    //  공개 API
    // ─────────────────────────────────────────────
    /// <summary>
    /// 검은 화면으로 페이드 아웃 → onBlack 실행 (이동 등) → 페이드 인.
    /// fadeDuration이 -1이면 기본값 사용.
    /// </summary>
    public void DoTransition(Action onBlack, float fadeDuration = -1f)
    {
        if (_isTransitioning) return;
        float dur = fadeDuration < 0f ? defaultFadeDuration : fadeDuration;
        StartCoroutine(TransitionRoutine(onBlack, dur));
    }

    /// <summary>
    /// 검은 화면으로 페이드 아웃만 수행합니다.
    /// 페이드 인은 FadeFromBlack() 으로 별도 호출하세요.
    /// </summary>
    public IEnumerator FadeToBlack(float fadeDuration = -1f)
    {
        float dur = fadeDuration < 0f ? defaultFadeDuration : fadeDuration;
        yield return StartCoroutine(FadeRoutine(1f, dur, null));
    }

    /// <summary>
    /// 검은 화면에서 페이드 인만 수행합니다.
    /// FadeToBlack() 이후에 호출하세요.
    /// </summary>
    public IEnumerator FadeFromBlack(float fadeDuration = -1f)
    {
        float dur = fadeDuration < 0f ? defaultFadeDuration : fadeDuration;
        yield return StartCoroutine(FadeRoutine(0f, dur, null));
    }

    /// <summary>
    /// 검은 화면으로 페이드 아웃 → 씬 로드 → 새 씬에서 페이드 인.
    /// </summary>
    public void DoSceneTransition(string sceneName, float fadeDuration = -1f)
    {
        if (_isTransitioning) return;
        float dur = fadeDuration < 0f ? defaultFadeDuration : fadeDuration;
        StartCoroutine(SceneTransitionRoutine(sceneName, dur));
    }

    // ─────────────────────────────────────────────
    //  코루틴
    // ─────────────────────────────────────────────
    IEnumerator TransitionRoutine(Action onBlack, float fadeDuration)
    {
        _isTransitioning = true;
        LockPlayer();

        yield return StartCoroutine(FadeRoutine(1f, fadeDuration, null));

        onBlack?.Invoke();
        Physics2D.SyncTransforms();

        yield return StartCoroutine(FadeRoutine(0f, fadeDuration, null));

        UnlockPlayer();
        _isTransitioning = false;
    }

    IEnumerator SceneTransitionRoutine(string sceneName, float fadeDuration)
    {
        _isTransitioning = true;
        LockPlayer();

        yield return StartCoroutine(FadeRoutine(1f, fadeDuration, null));

        IsFadedIn = true;
        _isTransitioning = false;
        SceneManager.LoadScene(sceneName);
    }

    IEnumerator FadeRoutine(float targetAlpha, float duration, Action onComplete)
    {
        if (!fadeOverlay)
            fadeOverlay = CreateFadeOverlay();

        float startAlpha = fadeOverlay.alpha;
        float elapsed = 0f;

        if (targetAlpha > 0f)
        {
            fadeOverlay.gameObject.SetActive(true);
            fadeOverlay.blocksRaycasts = true;
        }

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeOverlay.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            yield return null;
        }

        fadeOverlay.alpha = targetAlpha;

        if (targetAlpha == 0f)
        {
            fadeOverlay.blocksRaycasts = false;
            fadeOverlay.gameObject.SetActive(false);
        }

        onComplete?.Invoke();
    }

    // ─────────────────────────────────────────────
    //  플레이어 이동 잠금
    // ─────────────────────────────────────────────
    void LockPlayer()
    {
        if (!_ctrl)
        {
            _ctrl = FindAnyObjectByType<ClearSky.SimplePlayerController>();
            if (_ctrl != null) _playerRb = _ctrl.GetComponent<Rigidbody2D>();
        }
        if (!_ctrl) return;
        _ctrl.Lock();
        if (_playerRb != null) _playerRb.linearVelocity = Vector2.zero;
    }

    void UnlockPlayer()
    {
        if (!_ctrl)
            _ctrl = FindAnyObjectByType<ClearSky.SimplePlayerController>();
        if (_ctrl != null) _ctrl.Unlock();
    }
}
