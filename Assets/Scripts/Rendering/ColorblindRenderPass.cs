using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

/// <summary>
/// 현재 화면을 임시 RT 로 복사 → ColorblindCorrect 셰이더로 다시 Blit.
/// ColorblindRenderFeature 에 의해 AfterRenderingPostProcessing 타이밍에 삽입됨.
/// URP 17+ RenderGraph API 사용 (RealityGradeRenderPass 와 동일 구조).
/// </summary>
public class ColorblindRenderPass : ScriptableRenderPass
{
    private static readonly int PropMode = Shader.PropertyToID("_Mode");

    private readonly Material _material;

    public ColorblindRenderPass(Material material)
    {
        _material                   = material;
        profilingSampler            = new ProfilingSampler("ColorblindCorrect");
        requiresIntermediateTexture = true;
    }

    public void SetMode(int mode)
    {
        _material?.SetFloat(PropMode, mode);
    }

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
            renderGraph, desc, "_ColorblindTemp", false);

        // ① 현재 화면 → 임시 RT (셰이더 없이 복사)
        var copyParams = new RenderGraphUtils.BlitMaterialParameters(
            activeColor, tempHandle, Blitter.GetBlitMaterial(TextureDimension.Tex2D), 0);
        renderGraph.AddBlitPass(copyParams, "Colorblind - Copy");

        // ② 임시 RT → 화면 (색맹 보정 셰이더 적용)
        var correctParams = new RenderGraphUtils.BlitMaterialParameters(
            tempHandle, activeColor, _material, 0);
        renderGraph.AddBlitPass(correctParams, "Colorblind - Apply");
    }

    public void Cleanup()
    {
        // _ColorblindTemp TextureHandle은 RenderGraph가 수명 관리.
        // Material은 ColorblindRenderFeature 소유이므로 여기서 해제하지 않음.
    }
}
