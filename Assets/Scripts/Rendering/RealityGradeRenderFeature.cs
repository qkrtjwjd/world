using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// URP Renderer Feature — 환상/현실 게이지 기반 풀스크린 컬러 그레이드.
///
/// [에디터 설정]
/// 1. Project Settings → Graphics → URP Renderer Asset 의 Add Renderer Feature
///    → RealityGradeRenderFeature 선택
/// 2. Inspector 의 Grade Material 슬롯에 RealityColorGrade 셰이더로 만든 머티리얼 연결
/// 3. 효과는 Game 카메라(월드 카메라)에만 적용됨.
///    UI 는 Screen Space - Overlay Canvas 를 사용하면 자동으로 제외됨.
/// </summary>
public class RealityGradeRenderFeature : ScriptableRendererFeature
{
    /// <summary>RealityGaugeDriver 가 참조하는 싱글턴.</summary>
    public static RealityGradeRenderFeature Instance { get; private set; }

    [SerializeField] private Material gradeMaterial;

    private RealityGradeRenderPass _pass;

    public Material Material => gradeMaterial;

    // ── ScriptableRendererFeature ─────────────────────────────────────────

    public override void Create()
    {
        Instance = this;

        _pass = new RealityGradeRenderPass(gradeMaterial)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_pass == null || gradeMaterial == null) return;

        var cam = renderingData.cameraData;

        // SceneView / Preview 카메라 제외
        if (cam.cameraType != CameraType.Game) return;

        // Overlay 카메라(UI 카메라 스택) 제외 — 베이스(월드) 카메라에만 적용
        if (cam.renderType == CameraRenderType.Overlay) return;

        renderer.EnqueuePass(_pass);
    }

    /// <summary>RealityGaugeDriver 에서 매 프레임 정규화된 게이지(0~1)를 설정.</summary>
    public void SetGauge(float normalizedGauge)
    {
        _pass?.SetGauge(normalizedGauge);
    }

    protected override void Dispose(bool disposing)
    {
        Instance = null;
        _pass?.Cleanup();
    }
}
