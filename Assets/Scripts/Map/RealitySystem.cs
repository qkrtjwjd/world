using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class RealitySystem : MonoBehaviour
{
    [Header("기본 설정")]
    public string nextSceneName = SceneNames.DarkReality;
    public float  maxTime       = 10f;
    public float  fillSpeed     = 1f;

    [Header("연출 설정")]
    public float flashDuration = 0.5f;

    [Header("연결")]
    public Image      overlayImage;
    public Slider     realityGauge;
    public GameObject player;

    public static RealitySystem Instance;

    /// <summary>현재 현실 침투 게이지 값 (0 ~ maxTime).</summary>
    public float CurrentReality => _currentReality;

    /// <summary>
    /// 현실 게이지가 변동될 때 발행됩니다.
    /// BattleSystem 등이 구독하여 폴링 없이 반응할 수 있습니다.
    /// </summary>
    public event System.Action<float> OnRealityChanged;

    [Header("씬 전체 설정")]
    public bool sceneDefaultActive = false;

    [Header("현재 상태")]
    public bool isSystemActive = false;

    private float _currentReality  = 0f;
    private bool  _isTransitioning = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        _currentReality = GameState.battleReturn.isComingFromBattle
            ? GameState.battleReturn.savedGaugeValue
            : 0f;

        GameState.battleReturn.savedGaugeValue = _currentReality;

        if (realityGauge != null)
        {
            realityGauge.maxValue = maxTime;
            realityGauge.value    = _currentReality;
        }

        UpdateOverlay();

        if (GameState.hasPositionSaved && player != null)
            player.transform.position = GameState.lastPosition;
    }

    void Update()
    {
        if (!isSystemActive) return;

        GameState.battleReturn.savedGaugeValue = _currentReality;

        if (_isTransitioning) return;

        // 멘탈 붕괴 중이면 게이지 정지
        if (GameState.mentalBreakdownTimer > 0) return;

        float mentalRatio = (PlayerStats.Instance != null)
            ? PlayerStats.Instance.currentMental / PlayerStats.Instance.maxMental
            : 1f;
        float dynamicFillSpeed = fillSpeed * (1f + (1f - mentalRatio) * 0.5f);
        _currentReality += Time.deltaTime * dynamicFillSpeed;
        if (realityGauge != null) realityGauge.value = _currentReality;
        OnRealityChanged?.Invoke(_currentReality);

        UpdateOverlay();

        if (_currentReality >= maxTime)
        {
            _isTransitioning = true;

            if (player != null)
            {
                GameState.lastPosition     = player.transform.position;
                GameState.hasPositionSaved = true;
            }

            GameState.isComingFromBattle = false;
            SceneManager.LoadScene(nextSceneName);
        }
    }

    void UpdateOverlay()
    {
        if (overlayImage == null) return;
        float startFlash = maxTime - flashDuration;
        overlayImage.fillAmount = (_currentReality >= startFlash)
            ? (_currentReality - startFlash) / flashDuration
            : 0f;
    }
}