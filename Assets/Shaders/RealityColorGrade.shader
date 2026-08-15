// RealityColorGrade.shader — 환상/현실 게이지 전체 화면 컬러 그레이드
// URP Blit 전용. _BlitTexture 는 Blitter 가 공급.
// _Gauge: 0(완전 환상) ~ 1(완전 현실), 스크립트에서 0~100 → 0~1 정규화.

Shader "Custom/RealityColorGrade"
{
    Properties
    {
        _Gauge ("Gauge (0-1 normalized)", Range(0,1)) = 0.3
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            Name "RealityColorGrade"

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Gauge;
            CBUFFER_END

            // 휘도 가중 그레이스케일 (ITU-R BT.709)
            static const half3 LumWeights = half3(0.2126h, 0.7152h, 0.0722h);

            // 2차 S커브: 어두운 영역 → 더 어둡게, 밝은 영역 → 더 밝게
            // f(0)=0, f(0.5)=0.5, f(1)=1 보장
            half SCurve(half x)
            {
                return x < 0.5h
                    ? 2.0h * x * x
                    : -1.0h + (4.0h - 2.0h * x) * x;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                half4 orig = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                half3 c = orig.rgb;

                // ─── ① S커브 대비 (항상 적용) ──────────────────────────────
                // 0.35 강도로 블렌드: 너무 과하지 않게
                half3 sCurved = half3(SCurve(c.r), SCurve(c.g), SCurve(c.b));
                c = lerp(c, sCurved, 0.35h);

                // ─── ② 종 모양 채도 감소 (게이지 50에서 최대) ──────────────
                // bell(g) = 1 − 4*(g−0.5)^2  → g=0:0, g=0.5:1, g=1:0
                float g = _Gauge;
                float d = g - 0.5;
                float bell = saturate(1.0 - 4.0 * d * d);

                half lum = dot(c, LumWeights);
                // bell * 0.93: 정중앙(게이지 50)에서 거의 완전 회색
                c = lerp(c, half3(lum, lum, lum), bell * 0.93h);

                // ─── ③ 색온도 + 명도 ────────────────────────────────────────
                // warmCool: -1(g=0 환상, 따뜻함) … 0(중립) … +1(g=1 현실, 차가움)
                float warmCool = g * 2.0 - 1.0;

                // 따뜻한 필터 (g=0): R↑ G소폭↑ B↓
                // 차가운 필터 (g=1): R↓ G소폭↓ B↑
                half3 warmFilter = half3(1.12h, 1.04h, 0.80h);
                half3 neutral    = half3(1.00h, 1.00h, 1.00h);
                half3 coolFilter = half3(0.80h, 0.92h, 1.20h);

                // 분기 없는 색온도 혼합
                // warmFactor: g=0→1, g=0.5→0 / coolFactor: g=1→1, g=0.5→0
                float warmFactor = saturate(-warmCool);
                float coolFactor = saturate( warmCool);
                half3 tempFilter = neutral
                    + (warmFilter - neutral) * warmFactor
                    + (coolFilter - neutral) * coolFactor;

                c *= tempFilter;

                // 명도 보정: 환상(g=0) +밝기, 현실(g=1) −밝기
                // warmCool=-1→+0.09  /  warmCool=0→0  /  warmCool=+1→-0.09
                float brightBias = -warmCool * 0.09;
                c += brightBias;

                c = saturate(c);
                return half4(c, orig.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
