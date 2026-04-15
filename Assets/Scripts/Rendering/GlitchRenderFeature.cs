using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// URP Renderer Feature — 글리치 후처리 효과.
///
/// [에디터 설정]
/// 1. Project Settings → Graphics → URP Renderer Asset 의 Add Renderer Feature
///    → GlitchRenderFeature 선택
/// 2. Inspector 의 Glitch Material 슬롯에 GlitchMaterial(GlitchEffect 셰이더) 연결
/// 3. 기존 Canvas 의 GlitchPanel RawImage 오브젝트는 삭제해도 됩니다
/// </summary>
public class GlitchRenderFeature : ScriptableRendererFeature
{
    /// <summary>씬 전체에서 GlitchManager 가 참조할 싱글턴.</summary>
    public static GlitchRenderFeature Instance { get; private set; }

    [SerializeField] private Material glitchMaterial;

    private GlitchRenderPass _pass;

    /// <summary>GlitchManager 가 셰이더 프로퍼티를 직접 설정할 수 있도록 머티리얼 노출.</summary>
    public Material Material => glitchMaterial;

    // ── ScriptableRendererFeature ─────────────────────────────────────────

    public override void Create()
    {
        Instance = this;

        _pass = new GlitchRenderPass(glitchMaterial)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
        };
        _pass.SetActive(false);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_pass == null || glitchMaterial == null) return;
        if (!_pass.IsActive) return;

        // SceneView / Preview 카메라는 제외
        if (renderingData.cameraData.cameraType != CameraType.Game) return;

        renderer.EnqueuePass(_pass);
    }

    /// <summary>패스 켜기/끄기. GlitchManager 에서 호출.</summary>
    public new void SetActive(bool active) => _pass?.SetActive(active);

    protected override void Dispose(bool disposing)
    {
        Instance = null;
        _pass?.Cleanup();
    }
}
