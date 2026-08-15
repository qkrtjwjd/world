using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// URP Renderer Feature — 색맹 보정(daltonization) 풀스크린 필터.
///
/// [에디터 설정]
/// 1. Project Settings → Graphics → URP Renderer Asset 의 Add Renderer Feature
///    → ColorblindRenderFeature 선택
/// 2. Inspector 의 Correct Material 슬롯에 ColorblindCorrect 셰이더로 만든 머티리얼 연결
/// 3. 효과는 Game(월드) 카메라의 Base 카메라에만 적용됨.
///    UI 는 Screen Space - Overlay Canvas 를 사용하면 자동으로 제외됨.
///
/// 모드는 SettingsManager.OnColorblindModeChanged 를 구독하는
/// PostProcessingController 가 SetMode 로 전달한다.
/// </summary>
public class ColorblindRenderFeature : ScriptableRendererFeature
{
    /// <summary>PostProcessingController 가 참조하는 싱글턴.</summary>
    public static ColorblindRenderFeature Instance { get; private set; }

    [SerializeField] private Material correctMaterial;

    private ColorblindRenderPass _pass;
    private int _mode; // 0=없음, 1~3=색맹 유형

    public Material Material => correctMaterial;

    // ── ScriptableRendererFeature ─────────────────────────────────────────

    public override void Create()
    {
        Instance = this;

        _pass = new ColorblindRenderPass(correctMaterial)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
        };
        _pass.SetMode(_mode);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // 모드 0(없음)이면 패스 자체를 넣지 않아 오버헤드 0
        if (_pass == null || correctMaterial == null || _mode <= 0) return;

        var cam = renderingData.cameraData;

        // SceneView / Preview 카메라 제외
        if (cam.cameraType != CameraType.Game) return;

        // Overlay 카메라(UI 카메라 스택) 제외 — 베이스(월드) 카메라에만 적용
        if (cam.renderType == CameraRenderType.Overlay) return;

        renderer.EnqueuePass(_pass);
    }

    /// <summary>0=없음, 1=적록(1형), 2=적록(2형), 3=청황.</summary>
    public void SetMode(int mode)
    {
        _mode = mode;
        _pass?.SetMode(mode);
    }

    protected override void Dispose(bool disposing)
    {
        Instance = null;
        _pass?.Cleanup();
    }
}
