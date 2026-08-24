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
    private bool  _warned;

    /// <summary>
    /// 렌더 피처는 <b>캐시하지 않는다.</b> <c>Instance</c> 는 URP 가 렌더러를 실제로 만들 때
    /// (<c>ScriptableRendererFeature.Create()</c>) 대입되는데, 그 시점이 <c>Start</c> 보다 늦을 수 있다.
    /// 예전에는 한 프레임 뒤에 한 번만 캐시해서, 그때 없으면 <c>Update</c> 가 계속 빠져나가
    /// <b>그 세션 내내 무채색 보정이 죽었다</b>(배치모드에서 실측). 정적 프로퍼티 조회는 싸다.
    /// </summary>
    private static RealityGradeRenderFeature Feature => RealityGradeRenderFeature.Instance;

    void Start()
    {
        StartCoroutine(InitAfterFrame());
    }

    IEnumerator InitAfterFrame()
    {
        yield return null;

        // 현재 게이지로 즉시 초기화 (Lerp 시작점)
        if (GaugeManager.Instance != null)
            _currentNormalized = GaugeManager.Instance.fantasyRealityGauge / 100f;

        ApplyToFeature(_currentNormalized);
    }

    void Update()
    {
        if (GaugeManager.Instance == null) return;

        float target = GaugeManager.Instance.fantasyRealityGauge / 100f;
        _currentNormalized = Mathf.Lerp(_currentNormalized, target, Time.deltaTime * lerpSpeed);

        ApplyToFeature(_currentNormalized);
    }

    void ApplyToFeature(float normalized)
    {
        var f = Feature;
        if (f == null) { WarnIfStillMissing(); return; }
        f.SetGauge(normalized);
    }

    /// <summary>
    /// 첫 렌더 전에는 피처가 없는 것이 정상이라 바로 경고하지 않는다.
    /// 유예 시간이 지나도 없으면 그때 한 번만 남긴다(배선이 진짜 빠진 경우).
    /// <para><b>배치모드에서는 아예 경고하지 않는다.</b> 게임 뷰가 없어 아무것도 안 그리므로
    /// 피처가 만들어지지 않는 것이 정상이고, 여기서 나오는 경고는 배선과 무관한 잡음이다.</para>
    /// </summary>
    void WarnIfStillMissing()
    {
        if (_warned || Application.isBatchMode) return;
        if (Time.realtimeSinceStartup < WarnGraceSeconds) return;
        _warned = true;
        Debug.LogWarning("[RealityGaugeDriver] RealityGradeRenderFeature 를 찾을 수 없습니다. " +
                         "URP Renderer Asset 에 RealityGradeRenderFeature 를 추가했는지 확인하세요.");
    }

    const float WarnGraceSeconds = 5f;
}
