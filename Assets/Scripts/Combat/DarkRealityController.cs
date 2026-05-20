using UnityEngine;
using UnityEngine.UI;

public class DarkRealityController : MonoBehaviour
{
    [Header("기본 설정")]
    public float drainSpeed   = 1.667f;   // GaugeManager 0~100 기준, 60초에 100 감소

    [Header("연출 설정")]
    public float clearDuration = 0.5f;

    [Header("연결")]
    public Image      overlayImage;
    public GameObject player;

    private bool  _isTransitioning = false;
    private float _timeSinceStart  = 0f;

    void Start()
    {
        // GaugeManager는 DontDestroyOnLoad → 전투 복귀 시 게이지 자동 유지.
        // 첫 진입(전투 복귀가 아닐 때)만 게이지를 100으로 초기화.
        if (!GameState.battleReturn.isComingFromBattle)
            GaugeManager.Instance?.SetGaugeValue(100f);

        _timeSinceStart = 0f;

        if (GameState.hasPositionSaved && player != null)
            player.transform.position = GameState.lastPosition;
    }

    void Update()
    {
        if (_isTransitioning) return;

        GaugeManager.Instance?.ChangeGauge(-drainSpeed * Time.deltaTime);

        _timeSinceStart += Time.deltaTime;
        if (overlayImage != null)
        {
            float progress = (_timeSinceStart < clearDuration)
                ? 1f - (_timeSinceStart / clearDuration)
                : 0f;
            overlayImage.fillAmount = progress;
        }

        // 게이지 소진 → 전투 강제 종료
        if (GaugeManager.Instance != null && GaugeManager.Instance.fantasyRealityGauge <= 0f)
        {
            _isTransitioning = true;
            HackSlashCombatManager.Instance?.ForceEndCombatByGauge();
        }
    }
}
