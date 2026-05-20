using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 씬에 배치된 Global Volume weight를 게이지 값으로 실시간 Lerp.
/// fantasyVolume.weight = 1 - (gauge/100), realityVolume.weight = gauge/100
/// glitchVolume: 게이지 30~69 구간에서 weight=1 (Saturation -100으로 회색화)
/// </summary>
public class PostProcessingController : MonoBehaviour
{
    [Header("Volume 연결 (Inspector)")]
    public Volume fantasyVolume;
    public Volume realityVolume;
    [Tooltip("Saturation -100 으로 설정된 Volume. 게이지 30~69 구간에서 활성화됩니다.")]
    public Volume glitchVolume;

    [Header("추종 속도")]
    public float lerpSpeed = 2f;

    private float _targetFantasy = 1f;
    private float _targetReality = 0f;
    private float _targetGlitch  = 0f;

    void OnEnable()
    {
        if (GaugeManager.Instance != null)
            GaugeManager.Instance.OnGaugeChanged += OnGaugeChanged;
    }

    void OnDisable()
    {
        if (GaugeManager.Instance != null)
            GaugeManager.Instance.OnGaugeChanged -= OnGaugeChanged;
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
}
