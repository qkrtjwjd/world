// ─────────────────────────────────────────────────────────────────────────────
//  Custom/SliderGlow
//
//  슬라이더 뒤에 배치하는 Additive 후광 이미지.
//  Blend One One: 이 이미지의 RGB가 배경에 더해짐 → 씬 전체 무영향.
//  환상 구역(왼쪽)에만 황금빛 후광, 현실 구역(오른쪽)은 0.
//  세로 방향: 중심이 가장 밝고 가장자리(슬라이더 바깥)로 갈수록 은은하게 감쇠.
// ─────────────────────────────────────────────────────────────────────────────
Shader "Custom/SliderGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        _GaugeValue          ("Gauge Value",          Range(0, 1))   = 0.0
        _CorruptionLevel     ("Corruption Level",     Range(0, 100)) = 0.0
        _CorruptionThreshold ("Corruption Threshold", Float)         = 81.0
        _GlowIntensity       ("Glow Intensity",       Range(0, 3))   = 1.2

        // ── Unity UI 필수 속성 ───────────────────────────────────────────────
        _StencilComp      ("Stencil Comparison",  Float) = 8
        _Stencil          ("Stencil ID",          Float) = 0
        _StencilOp        ("Stencil Operation",   Float) = 0
        _StencilWriteMask ("Stencil Write Mask",  Float) = 255
        _StencilReadMask  ("Stencil Read Mask",   Float) = 255
        _ColorMask        ("Color Mask",          Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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
        // Additive 블렌딩: 이 픽셀의 RGB를 배경에 그대로 더함
        Blend One One
        ColorMask [_ColorMask]

        Pass
        {
            Name "SliderGlow"

            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4    _MainTex_ST;
            float4    _ClipRect;

            float _GaugeValue;
            float _CorruptionLevel;
            float _CorruptionThreshold;
            float _GlowIntensity;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex        = UnityObjectToClipPos(v.vertex);
                o.texcoord      = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color         = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.texcoord;

                // ── 환상/현실 경계 ───────────────────────────────────────────
                // _GaugeValue=0: 경계 오른쪽 끝 (전체 환상)
                // _GaugeValue=1: 경계 왼쪽 끝  (전체 현실)
                // 세로 강도: 중심(UV.y=0.5)이 최대, 양 가장자리 → 0
                // (가우시안: exp(-t²*k). 이미지가 슬라이더보다 크면 가장자리는 후광)
                float dy         = (uv.y - 0.5) * 2.0;         // -1 ~ 1
                float vertGlow   = exp(-dy * dy * 2.6);

                // ── 게이지 구간 전환 계수 ────────────────────────────────────
                // Fantasy 0~0.30 / Glitch 0.30~0.70 / Reality 0.70~1.0
                float toGlitch    = smoothstep(0.27, 0.33, _GaugeValue);
                float toReality   = smoothstep(0.67, 0.73, _GaugeValue);
                float glitchWeight = toGlitch * (1.0 - toReality);

                // ── 인형화 계수 (81 이상 황금→보라) ────────────────────────
                float corruptFactor = saturate(
                    (_CorruptionLevel - _CorruptionThreshold) /
                    (100.0 - _CorruptionThreshold + 0.001)
                );
                fixed3 golden       = fixed3(1.000, 0.843, 0.000) * 2.5; // #FFD700 HDR
                fixed3 grayGlowWarm = fixed3(0.72, 0.71, 0.67) * 2.0;  // 따뜻한 회색 후광 (강화)
                fixed3 grayGlowCool = fixed3(0.64, 0.64, 0.70) * 1.7;  // 차가운 회색 후광 (강화)
                fixed3 purple       = fixed3(0.176, 0.039, 0.227) * 1.2; // #2D0A3A

                // 인형화 적용: Fantasy→보라, Glitch→회색(환상쪽 따뜻/현실쪽 차가운, 인형화 시 보라 혼합)
                fixed3 fantasyGlow    = lerp(golden, purple, corruptFactor);
                fixed3 glitchGlowBase = lerp(grayGlowWarm, grayGlowCool, toReality);
                fixed3 glitchGlow     = lerp(glitchGlowBase, purple, corruptFactor * 0.5);
                fixed3 glowColor      = lerp(fantasyGlow, glitchGlow, glitchWeight);

                // ── 환상/현실 경계 (sprite=null로 UV 0~1 보장) ─────────
                float boundary  = 1.0 - saturate(_GaugeValue);
                float inFantasy = step(uv.x, boundary);

                // 완전 현실(boundary=0)에서 왼쪽 끝 픽셀 누출 방지
                float boundaryVisible = saturate(boundary * 1000.0);

                // ── 최종 출력 ────────────────────────────────────────────
                float  glowAmount = inFantasy * boundaryVisible * vertGlow * _GlowIntensity;
                fixed3 outRGB     = glowColor * glowAmount * i.color.a;

                // Blend One One: alpha는 RGB에 영향 없음 → 0으로 고정
                fixed4 col = fixed4(outRGB, 0.0);

                #ifdef UNITY_UI_CLIP_RECT
                // ClipRect는 RGB를 0으로 만드는 방식으로 적용
                float clipAlpha = UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                col.rgb *= clipAlpha;
                #endif

                return col;
            }
            ENDCG
        }
    }
}
