// =============================================================================
//  ImpactFlash.shader — URP UI 임팩트 순간 방사형 섬광
//  중심에서 외곽으로 퍼지는 흰 플래시. _FlashAmount로 전체 세기 제어.
// =============================================================================

Shader "UI/ImpactFlash"
{
    Properties
    {
        [PerRendererData] _MainTex ("Main Texture", 2D) = "white" {}

        _FlashColor    ("Flash Color",    Color)        = (1, 1, 1, 1)
        _FlashAmount   ("Flash Amount",   Range(0, 1)) = 0.0
        _Center        ("Center (UV)",    Vector)       = (0.5, 0.5, 0, 0)
        _InnerRadius   ("Inner Radius",   Range(0, 1)) = 0.0
        _OuterRadius   ("Outer Radius",   Range(0, 2)) = 0.6
        _CoreIntensity ("Core Intensity", Range(0, 5)) = 2.5
        _RayCount      ("방사선 개수",    Range(0, 30)) = 10
        _RayStrength   ("방사선 세기",    Range(0, 1)) = 0.35
        _RayFalloff    ("방사선 감쇠",    Range(0.5, 5)) = 2.0

        _StencilComp     ("Stencil Comparison", Float) = 8
        _Stencil         ("Stencil ID",         Float) = 0
        _StencilOp       ("Stencil Operation",  Float) = 0
        _StencilWriteMask("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask",  Float) = 255
        _ColorMask       ("Color Mask",         Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"             = "Transparent"
            "IgnoreProjector"   = "True"
            "RenderType"        = "Transparent"
            "PreviewType"       = "Plane"
            "CanUseSpriteAtlas" = "True"
            "RenderPipeline"    = "UniversalPipeline"
        }

        Stencil
        {
            Ref       [_Stencil]
            Comp      [_StencilComp]
            Pass      [_StencilOp]
            ReadMask  [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull     Off
        Lighting Off
        ZWrite   Off
        ZTest    [unity_GUIZTestMode]
        Blend    SrcAlpha One
        ColorMask[_ColorMask]

        Pass
        {
            Name "ImpactFlash"

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color       : COLOR;
                float2 uv          : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _FlashColor;
                float  _FlashAmount;
                float4 _Center;
                float  _InnerRadius;
                float  _OuterRadius;
                float  _CoreIntensity;
                float  _RayCount;
                float  _RayStrength;
                float  _RayFalloff;
            CBUFFER_END

            float unity_GUIZTestMode;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color       = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 delta = IN.uv - _Center.xy;
                float  dist  = length(delta);
                float  angle = atan2(delta.y, delta.x);

                // 방사형 코어
                float core = 1.0 - smoothstep(_InnerRadius, _OuterRadius, dist);
                core = pow(core, 2.0) * _CoreIntensity;

                // 방사선 rays (각도 기반 sin)
                float rays = 0.0;
                if (_RayCount > 0.5)
                {
                    float rayPattern = abs(sin(angle * _RayCount * 0.5));
                    rayPattern = pow(rayPattern, 8.0);
                    float rayFade = 1.0 - smoothstep(0.0, _OuterRadius * 1.2, dist);
                    rays = rayPattern * pow(rayFade, _RayFalloff) * _RayStrength;
                }

                float total = core + rays;
                total *= _FlashAmount;

                half3 rgb = _FlashColor.rgb * total;
                float alpha = saturate(total) * IN.color.a * _FlashColor.a;

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "UI/Default"
}
