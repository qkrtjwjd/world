// ColorblindCorrect.shader — 색맹 보정(daltonization) 전체 화면 필터
// URP Blit 전용. _BlitTexture 는 Blitter 가 공급.
// _Mode: 0=없음(패스스루), 1=적록 1형(Protanopia), 2=적록 2형(Deuteranopia), 3=청황(Tritanopia)
//
// 원리(daltonization):
//   ① 해당 색각 이상의 "보이는 색"을 시뮬레이션 행렬로 계산
//   ② 오차 = 원본 − 시뮬레이션 (구분 못 하는 색 성분)
//   ③ 오차를 구분 가능한 채널로 재분배해 원본에 더함
// 시뮬레이션 행렬: Viénot/Machado 계열 근사값.

Shader "Custom/ColorblindCorrect"
{
    Properties
    {
        _Mode ("Colorblind Mode (0-3)", Float) = 0
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
            Name "ColorblindCorrect"

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Mode;
            CBUFFER_END

            half4 Frag(Varyings IN) : SV_Target
            {
                float2 uv  = IN.texcoord;
                half4 orig = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                half3 c    = orig.rgb;

                int mode = (int)round(_Mode);
                if (mode <= 0) return orig; // 없음 → 패스스루

                // 색각 이상 시뮬레이션 행렬(행 벡터) 선택
                half3 m0, m1, m2;
                if (mode == 1)          // Protanopia (적색맹)
                {
                    m0 = half3(0.567h, 0.433h, 0.000h);
                    m1 = half3(0.558h, 0.442h, 0.000h);
                    m2 = half3(0.000h, 0.242h, 0.758h);
                }
                else if (mode == 2)     // Deuteranopia (녹색맹)
                {
                    m0 = half3(0.625h, 0.375h, 0.000h);
                    m1 = half3(0.700h, 0.300h, 0.000h);
                    m2 = half3(0.000h, 0.300h, 0.700h);
                }
                else                    // Tritanopia (청황색맹)
                {
                    m0 = half3(0.950h, 0.050h, 0.000h);
                    m1 = half3(0.000h, 0.433h, 0.567h);
                    m2 = half3(0.000h, 0.475h, 0.525h);
                }

                // ① 시뮬레이션
                half3 sim = half3(dot(m0, c), dot(m1, c), dot(m2, c));

                // ② 오차
                half3 err = c - sim;

                // ③ 오차 재분배 (표준 daltonization shift 행렬)
                //   R: 0            G: 0.7*eR + eG   B: 0.7*eR + eB
                half3 corrected;
                corrected.r = c.r;
                corrected.g = c.g + 0.7h * err.r + err.g;
                corrected.b = c.b + 0.7h * err.r + err.b;

                corrected = saturate(corrected);
                return half4(corrected, orig.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
