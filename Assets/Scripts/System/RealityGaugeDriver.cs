using System.Collections;
using UnityEngine;

/// <summary>
/// GaugeManager 의 fantasyRealityGauge 값을 읽어:
///   1. RealityGradeRenderFeature 의 _Gauge 를 부드럽게 Lerp 적용
///   2. (CharacterSpriteController 는 GaugeManager.OnGaugeChanged 이벤트를 직접 구독하므로
///      이 스크립트는 Renderer Feature 구동에만 집중한다.)
///
/// [에디터 설정]
/// GameManager 또는 씬의 적절한 오브젝트에 컴포넌트로 추가.
/// lerpSpeed 는 기본 4 — 슬라이더가 살아있어 보이는 속도.
/// </summary>
public class RealityGaugeDriver : MonoBehaviour
{
    [Header("추종 속도 (높을수록 즉각 반응)")]
    public float lerpSpeed = 4f;

    private float _currentNormalized = 0.3f;
    private RealityGradeRenderFeature _feature;

    void Start()
    {
        StartCoroutine(InitAfterFrame());
    }

    IEnumerator InitAfterFrame()
    {
        // RenderFeature.Create() 가 Start 보다 늦게 완료될 수 있으므로 한 프레임 대기
        yield return null;

        _feature = RealityGradeRenderFeature.Instance;
        if (_feature == null)
        {
            Debug.LogWarning("[RealityGaugeDriver] RealityGradeRenderFeature 를 찾을 수 없습니다. " +
                             "URP Renderer Asset 에 RealityGradeRenderFeature 를 추가했는지 확인하세요.");
        }

        // 현재 게이지로 즉시 초기화 (Lerp 시작점)
        if (GaugeManager.Instance != null)
            _currentNormalized = GaugeManager.Instance.fantasyRealityGauge / 100f;

        ApplyToFeature(_currentNormalized);
    }

    void Update()
    {
        if (GaugeManager.Instance == null || _feature == null) return;

        float target = GaugeManager.Instance.fantasyRealityGauge / 100f;
        _currentNormalized = Mathf.Lerp(_currentNormalized, target, Time.deltaTime * lerpSpeed);

        ApplyToFeature(_currentNormalized);
    }

    void ApplyToFeature(float normalized)
    {
        _feature?.SetGauge(normalized);
    }
}
