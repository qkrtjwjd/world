using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

/// <summary>
/// 현재 화면을 임시 RT 로 복사 → GlitchEffect 셰이더로 다시 Blit.
/// GlitchRenderFeature 에 의해 AfterRenderingPostProcessing 타이밍에 삽입됨.
/// URP 17+ RenderGraph API 사용.
/// </summary>
public class GlitchRenderPass : ScriptableRenderPass
{
    private static readonly int PropTime2 = Shader.PropertyToID("_Time2");

    private readonly Material _material;
    private bool _isActive;

    public bool IsActive => _isActive;

    public GlitchRenderPass(Material material)
    {
        _material                   = material;
        profilingSampler            = new ProfilingSampler("GlitchEffect");
        requiresIntermediateTexture = true;
    }

    public void SetActive(bool active) => _isActive = active;

    // ── ScriptableRenderPass ──────────────────────────────────────────────

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        if (_material == null) return;

        var resourceData = frameData.Get<UniversalResourceData>();
        var cameraData   = frameData.Get<UniversalCameraData>();

        // 백버퍼 직접 접근 불가 — 중간 버퍼가 없으면 스킵
        if (resourceData.isActiveTargetBackBuffer) return;

        TextureHandle activeColor = resourceData.activeColorTexture;

        var desc = cameraData.cameraTargetDescriptor;
        desc.depthBufferBits = 0;
        TextureHandle tempHandle = UniversalRenderer.CreateRenderGraphTexture(
            renderGraph, desc, "_GlitchTemp", false);

        // ① 현재 화면 → 임시 RT (셰이더 없이 복사)
        var copyParams = new RenderGraphUtils.BlitMaterialParameters(
            activeColor, tempHandle, Blitter.GetBlitMaterial(TextureDimension.Tex2D), 0);
        renderGraph.AddBlitPass(copyParams, "Glitch - Copy");

        // ② 임시 RT → 화면 (글리치 셰이더 적용)
        _material.SetFloat(PropTime2, Time.time);
        var glitchParams = new RenderGraphUtils.BlitMaterialParameters(
            tempHandle, activeColor, _material, 0);
        renderGraph.AddBlitPass(glitchParams, "Glitch - Apply");
    }

    /// <summary>GlitchRenderFeature.Dispose 에서 호출.</summary>
    public void Cleanup()
    {
        // _GlitchTemp TextureHandle은 RenderGraph가 수명을 관리하므로 별도 해제 불필요.
        // Material은 GlitchRenderFeature 소유이므로 여기서 해제하지 않음.
    }
}
