// =============================================================================
//  WatercolorSpread.shader — UI 수채화 번짐 오버레이
//  FBM 노이즈 기반의 유기적 물감 번짐. _SpreadAmount (0→1) 로 번짐 정도 제어.
//  TransitionVFXController.StartWatercolorSpread() / FadeOutWatercolor() 에서 호출.
// =============================================================================

Shader "UI/WatercolorSpread"
{
    Properties
    {
        [PerRendererData] _MainTex ("Main Texture", 2D) = "white" {}

        _Color         ("Paint Color",    Color)        = (0.9, 0.82, 0.98, 0.85)
        _SpreadAmount  ("Spread Amount",  Range(0, 1))  = 0.0
        _EdgeSoftness  ("Edge Softness",  Range(0.01, 0.4)) = 0.12
        _NoiseScale    ("Noise Scale",    Float)        = 5.5
        _GrainStrength ("Grain Strength", Range(0, 0.3)) = 0.08
        _CenterClear   ("중앙 투명 반지름", Range(0, 0.6)) = 0.25
        _CenterFalloff ("경계 부드러움",   Range(0, 0.3)) = 0.12

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
        Blend    SrcAlpha OneMinusSrcAlpha
        ColorMask[_ColorMask]

        Pass
        {
            Name "WatercolorSpread"

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
                float4 _Color;
                float  _SpreadAmount;
                float  _EdgeSoftness;
                float  _NoiseScale;
                float  _GrainStrength;
                float  _CenterClear;
                float  _CenterFalloff;
            CBUFFER_END

            float unity_GUIZTestMode;

            // ── 노이즈 함수 ──────────────────────────────────────
            float hash(float2 p)
            {
                p = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 19.19);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f); // smoothstep
                return lerp(
                    lerp(hash(i),               hash(i + float2(1,0)), u.x),
                    lerp(hash(i + float2(0,1)), hash(i + float2(1,1)), u.x),
                    u.y);
            }

            // FBM 4옥타브 — 결과 범위 [0, ~0.94]
            float fbm(float2 p)
            {
                float v  = 0.500 * valueNoise(p);
                      v += 0.250 * valueNoise(p * 2.03 + float2(5.2, 1.3));
                      v += 0.125 * valueNoise(p * 4.01 + float2(2.8, 7.9));
                      v += 0.063 * valueNoise(p * 8.02 + float2(9.4, 3.1));
                return v;
            }

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
                float2 uv = IN.uv;

                // FBM 노이즈: 유기적 번짐 경계 생성
                float noise = fbm(uv * _NoiseScale);

                // SpreadAmount가 noise 값을 넘으면 해당 픽셀이 채색됨
                float paintAlpha = smoothstep(noise - _EdgeSoftness,
                                             noise + _EdgeSoftness,
                                             _SpreadAmount);
                paintAlpha = saturate(paintAlpha);

                // 종이 질감: 고주파 노이즈로 색소 농담 변화 표현
                float grain = valueNoise(uv * (_NoiseScale * 3.5)) * _GrainStrength;

                // 번짐 전선에서 색소 농도 살짝 높임 (물감 경계 집중 효과)
                float distToFront = abs(noise - _SpreadAmount) / max(_EdgeSoftness, 0.01);
                float wavefront   = saturate(1.0 - distToFront) * 0.2;

                half3 finalColor = _Color.rgb * (1.0 + grain + wavefront);
                half  finalAlpha = _Color.a * IN.color.a * paintAlpha;

                // 중앙이 투명하게 비치도록 방사형 마스크: 중앙(dist=0) → 0, 테두리 → 1
                float2 centerDelta = uv - float2(0.5, 0.5);
                float  distCenter  = length(centerDelta);
                float  radialMask  = smoothstep(
                    _CenterClear - _CenterFalloff,
                    _CenterClear + _CenterFalloff,
                    distCenter);
                finalAlpha *= radialMask;

                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }

    FallBack "UI/Default"
}
