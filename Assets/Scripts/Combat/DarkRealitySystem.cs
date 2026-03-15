using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class DarkRealitySystem : MonoBehaviour
{
    [Header("기본 설정")]
    public string nextSceneName = SceneNames.Map;   // ← 상수 기본값
    public float  maxTime       = 60f;
    public float  drainSpeed    = 1f;

    [Header("연출 설정")]
    public float clearDuration = 0.5f;

    [Header("연결")]
    public Image      overlayImage;
    public Slider     realityGauge;
    public GameObject player;

    private float _currentReality;
    private bool  _isTransitioning = false;
    private float _timeSinceStart  = 0f;

    void Start()
    {
        _currentReality = GameState.battleReturn.isComingFromBattle
            ? GameState.battleReturn.savedGaugeValue
            : maxTime;

        // isComingFromBattle 해제는 BattleCooldownUpdater 가 쿨타임 후 자동으로 처리.
        // 단, 씬 진입 직후 savedGaugeValue 는 이미 읽었으므로 리셋.
        GameState.battleReturn.savedGaugeValue = _currentReality;

        _timeSinceStart = 0f;

        if (realityGauge != null)
        {
            realityGauge.maxValue = maxTime;
            realityGauge.value    = _currentReality;
        }

        if (GameState.hasPositionSaved && player != null)
            player.transform.position = GameState.lastPosition;
    }

    void Update()
    {
        GameState.battleReturn.savedGaugeValue = _currentReality;

        if (_isTransitioning) return;

        _currentReality -= Time.deltaTime * drainSpeed;
        if (realityGauge != null) realityGauge.value = _currentReality;

        // 진입 시 화면이 걷히는 연출
        _timeSinceStart += Time.deltaTime;
        if (overlayImage != null)
        {
            float progress = (_timeSinceStart < clearDuration)
                ? 1f - (_timeSinceStart / clearDuration)
                : 0f;
            overlayImage.fillAmount = progress;
        }

        if (_currentReality <= 0)
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
}