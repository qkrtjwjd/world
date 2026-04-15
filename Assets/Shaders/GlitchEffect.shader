// GlitchEffect.shader — URP Blit 전용 글리치 셰이더
// RenderPass 에서 Blitter.BlitCameraTexture 로 사용.
// _MainTex 는 Inspector 표시용 슬롯이며, 실제 화면은 Blitter 가 _BlitTexture 로 공급.

Shader "Custom/GlitchEffect"
{
    Properties
    {
        // Inspector 에서 Material 확인용 (렌더링에는 _BlitTexture 가 사용됨)
        _MainTex        ("Texture",          2D)           = "white" {}
        _Intensity      ("Intensity",        Range(0,1))   = 0.0
        _ColorDrift     ("Color Drift",      Range(0,0.1)) = 0.0
        _ScanLineJitter ("ScanLine Jitter",  Range(0,0.2)) = 0.0
        _StaticNoise    ("Static Noise",     Range(0,1))   = 0.0
        _BlockDisplace  ("Block Displace",   Range(0,0.2)) = 0.0
        _Time2          ("Time Override",    Float)        = 0.0
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
            Name "GlitchEffect"

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag

            // Core.hlsl : URP 기본 매크로
            // Blit.hlsl : Vert / Attributes / Varyings / _BlitTexture / sampler_LinearClamp 제공
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // _MainTex_ST 는 사용하지 않으므로 CBUFFER 에서 제외
            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
                float _ColorDrift;
                float _ScanLineJitter;
                float _StaticNoise;
                float _BlockDisplace;
                float _Time2;
            CBUFFER_END

            // ── 유틸 ─────────────────────────────────────────────────────

            float rand(float2 co)
            {
                return frac(sin(dot(co, float2(12.9898, 78.233))) * 43758.5453);
            }

            // ── 프래그먼트 ───────────────────────────────────────────────
            // Vert 는 Blit.hlsl 이 제공. IN.texcoord = 화면 UV (0~1).

            half4 Frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.texcoord;

                // 강도 0 → 원본 그대로 반환 (픽셀 연산 생략)
                if (_Intensity < 0.001)
                    return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float t = (_Time2 > 0.0) ? _Time2 : _Time.y;

                // ① 블록 분리 ─────────────────────────────────────────────
                //    uv.y 를 24 블록으로 나눠 랜덤하게 x 축으로 밀기
                {
                    float blockIndex = floor(uv.y * 24.0);
                    float blockRand  = rand(float2(blockIndex, floor(t * 8.0)));
                    float threshold  = lerp(0.85, 0.2, _Intensity);
                    float blockApply = step(threshold, blockRand);
                    float shift      = (blockRand * 2.0 - 1.0) * _BlockDisplace * blockApply;
                    uv.x += shift * _Intensity;
                }

                // ② 스캔라인 지터 ─────────────────────────────────────────
                //    120 라인 단위 x 축 떨림
                {
                    float lineIndex = floor(uv.y * 120.0);
                    float lineRand  = rand(float2(lineIndex, floor(t * 15.0)));
                    uv.x += (lineRand * 2.0 - 1.0) * _ScanLineJitter * _Intensity;
                }

                uv.x = saturate(uv.x);

                // ③ 단순 샘플링 — 색수차 제거 (흑백 전용)
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                col.a = 1.0;

                // ④ 스태틱 노이즈 (TV 화이트 노이즈) ──────────────────────
                //    전체 픽셀을 균등하게 noiseStrength 비율로 노이즈와 lerp.
                //    uv * 700 : 촘촘한 픽셀 밀도 / t * 30 : 초당 30회 플리커
                {
                    float noiseStrength = _StaticNoise * _Intensity;
                    if (noiseStrength > 0.0001)
                    {
                        float noise = rand(uv * 700.0 + frac(t * 30.0));
                        col.rgb = lerp(col.rgb, half3(noise, noise, noise), noiseStrength);
                    }
                }

                // ⑤ 최종: alpha 는 항상 1 (Opaque Blit)
                col.a = 1.0;
                return col;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
