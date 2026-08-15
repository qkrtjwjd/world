using System.Collections;
using UnityEngine;

/// <summary>
/// 캐릭터(루·쿠루·NPC)에 부착.
/// 게이지 70 임계값 기준으로 환상/현실 스프라이트 세트를 전환한다.
///   - 게이지 < 70 → fantasySprite
///   - 게이지 ≥ 70 → realitySprite
/// 전환 시 GlitchManager 를 통해 짧은 화면 노이즈를 재생해 툭 튀는 느낌을 없앤다.
///
/// [에디터 설정]
/// Inspector 에서 fantasySprite, realitySprite 를 각각 연결하면 됨.
/// SpriteRenderer 컴포넌트와 함께 사용.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class CharacterSpriteController : MonoBehaviour
{
    private const float RealityThreshold = 70f;

    [Header("스프라이트 세트")]
    public Sprite fantasySprite;
    public Sprite realitySprite;

    [Header("전환 글리치 지속 시간 (초)")]
    public float glitchDuration = 0.25f;

    [Header("렌더링")]
    [SerializeField] private int _sortingOrder = 60;

    private SpriteRenderer _sr;
    private bool _isRealityMode = false;
    private Coroutine _switchCoroutine;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _sr.sortingOrder = _sortingOrder;
    }

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
        // GaugeManager 가 OnEnable 이후에 초기화되는 경우를 대비해 재구독
        // (PostProcessingController 와 동일한 방어 패턴)
        if (GaugeManager.Instance != null)
        {
            GaugeManager.Instance.OnGaugeChanged -= OnGaugeChanged;
            GaugeManager.Instance.OnGaugeChanged += OnGaugeChanged;
            ApplyImmediate(GaugeManager.Instance.fantasyRealityGauge >= RealityThreshold);
        }
    }

    void OnGaugeChanged(float gauge)
    {
        bool shouldBeReality = gauge >= RealityThreshold;
        if (shouldBeReality == _isRealityMode) return;

        if (_switchCoroutine != null) StopCoroutine(_switchCoroutine);
        _switchCoroutine = StartCoroutine(SwitchWithGlitch(shouldBeReality));
    }

    IEnumerator SwitchWithGlitch(bool toReality)
    {
        // 글리치 재생 후 스프라이트 교체
        GlitchManager.Instance?.PlayGlitch(glitchDuration, GlitchManager.PresetMild);

        // 글리치 절반 지점에서 스프라이트 교체 (효과 중간에 전환되어 자연스럽게 보임)
        yield return new WaitForSeconds(glitchDuration * 0.5f);

        ApplyImmediate(toReality);
        _switchCoroutine = null;
    }

    void ApplyImmediate(bool toReality)
    {
        _isRealityMode = toReality;
        Sprite target = toReality ? realitySprite : fantasySprite;
        if (target != null)
            _sr.sprite = target;
    }
}
