// ─────────────────────────────────────────────────────────────────────────────
//  Custom/EclipseGauge
//
//  좌측 = 환상  (흰빛→황금빛 HDR 그라디언트, URP Bloom 연동)
//  우측 = 현실  (깊은 검정 #050505, 효과 없음)
//
//  _GaugeValue 0 → 경계가 오른쪽 끝 (환상 100%, 전체 황금빛)
//  _GaugeValue 1 → 경계가 왼쪽 끝  (현실 100%, 왼쪽 끝에 코로나만 남음)
// ─────────────────────────────────────────────────────────────────────────────
Shader "Custom/EclipseGauge"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        // ── 게이지 파라미터 ──────────────────────────────────────────────────
        _GaugeValue          ("Gauge Value",          Range(0, 1))   = 0.0
        _CorruptionLevel     ("Corruption Level",     Range(0, 100)) = 0.0
        _CorruptionThreshold ("Corruption Threshold", Float)         = 81.0

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
        Blend    SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "EclipseGauge"

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

            // ── 현실 구역 그라디언트 (#8B8590 보라회색 → #050505 검정) ─────────
            fixed3 SampleRealityGradient(float t)
            {
                fixed3 c0 = fixed3(0.545, 0.522, 0.565);
                fixed3 c1 = fixed3(0.420, 0.376, 0.439);
                fixed3 c2 = fixed3(0.322, 0.251, 0.345);
                fixed3 c3 = fixed3(0.239, 0.176, 0.282);
                fixed3 c4 = fixed3(0.165, 0.118, 0.208);
                fixed3 c5 = fixed3(0.102, 0.063, 0.125);
                fixed3 c6 = fixed3(0.059, 0.031, 0.059);
                fixed3 c7 = fixed3(0.020, 0.020, 0.020);

                float s = saturate(t) * 7.0;
                fixed3 c = lerp(c0, c1, saturate(s));
                c = lerp(c, lerp(c1, c2, saturate(s - 1.0)), step(1.0, s));
                c = lerp(c, lerp(c2, c3, saturate(s - 2.0)), step(2.0, s));
                c = lerp(c, lerp(c3, c4, saturate(s - 3.0)), step(3.0, s));
                c = lerp(c, lerp(c4, c5, saturate(s - 4.0)), step(4.0, s));
                c = lerp(c, lerp(c5, c6, saturate(s - 5.0)), step(5.0, s));
                c = lerp(c, lerp(c6, c7, saturate(s - 6.0)), step(6.0, s));
                return c;
            }

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

                // ── 경계 위치 계산 ───────────────────────────────────────────
                float edgePos   = 1.0 - saturate(_GaugeValue);
                float dist      = uv.x - edgePos;
                float isFantasy = step(dist, 0.0);

                // ── 인형화 계수 ──────────────────────────────────────────────
                float corruptFactor = saturate(
                    (_CorruptionLevel - _CorruptionThreshold) /
                    (100.0 - _CorruptionThreshold + 0.001)
                );

                // ── 게이지 구간 전환 계수 ────────────────────────────────────
                // Fantasy 0~0.30 / Glitch 0.30~0.70 / Reality 0.70~1.0
                // 경계 ±0.03 구간에서 smoothstep 블렌딩
                float toGlitch     = smoothstep(0.27, 0.33, _GaugeValue);
                float toReality    = smoothstep(0.67, 0.73, _GaugeValue);
                // Glitch 구간에서만 최대 1, Fantasy/Reality 구간에서 0
                float glitchWeight = toGlitch * (1.0 - toReality);

                // ── 환상 구역 색상 (UV.x < edgePos) ─────────────────────────
                float  fantasyT = saturate(uv.x / max(edgePos, 0.001));
                fixed3 white    = fixed3(1.0, 1.0, 1.0);
                fixed3 golden   = fixed3(1.0, 0.843, 0.0);

                // Fantasy: 흰빛→황금 HDR (기존)
                fixed3 fantasyLeft = lerp(white * 2.4, golden * 1.7, fantasyT);

                // Glitch: 환상 경계→따뜻한 회색(황금빛 섞임), 현실 경계→차가운 회색(보라빛 섞임)
                fixed3 grayHot  = fixed3(0.70, 0.68, 0.62); // 따뜻한 중간 회색 (뚜렷하게 낮춤)
                fixed3 grayCold = fixed3(0.60, 0.60, 0.67); // 차가운 중간 회색 (보라빛)
                fixed3 glitchGrayBase = lerp(grayHot, grayCold, toReality);
                // UV.x 방향 명암: 왼쪽(슬라이더 시작)이 밝고 경계선 쪽으로 갈수록 어두움
                fixed3 glitchLeft = lerp(glitchGrayBase * 1.3, glitchGrayBase * 0.85, fantasyT);

                // 환상→회색→보라회색: 3단계 전환
                // Reality 구간 환상쪽: 보라회색 (현실 구역 경계 색과 자연스럽게 이음)
                fixed3 realityLeft = fixed3(0.27, 0.25, 0.30);
                fixed3 leftColor   = fantasyLeft;
                leftColor = lerp(leftColor, glitchLeft,  toGlitch);  // 환상→글리치: 황금→회색
                leftColor = lerp(leftColor, realityLeft, toReality); // 글리치→현실: 회색→보라회색

                // ── 현실 구역 색상 (UV.x >= edgePos) ────────────────────────
                float  realityT = saturate(dist / max(1.0 - edgePos, 0.001));

                // Fantasy/Reality: 보라회색→검정 (기존)
                fixed3 darkBase = SampleRealityGradient(realityT);

                // Glitch: 환상 경계→따뜻한 어두운 회색, 현실 경계→차가운(보라빛) 어두운 회색→검정
                fixed3 grayDkHot  = fixed3(0.22, 0.20, 0.17); // 따뜻한 어두운 회색 (밝기 올림)
                fixed3 grayDkCold = fixed3(0.17, 0.16, 0.21); // 차가운 어두운 회색 (보라빛)
                fixed3 grayDkBase = lerp(grayDkHot, grayDkCold, toReality);
                fixed3 deepBlack  = fixed3(0.02, 0.02, 0.02);
                fixed3 glitchDark = lerp(grayDkBase, deepBlack, realityT);

                // Glitch 구간에서만 회색 계열, 나머지는 보라회색→검정
                fixed3 rightColor = lerp(darkBase, glitchDark, glitchWeight);

                // ── 최종 컬러 합성 ───────────────────────────────────────────
                float boundaryVisible = saturate(edgePos * 1000.0);
                isFantasy *= boundaryVisible;
                fixed3 finalColor = leftColor  * isFantasy
                                  + rightColor * (1.0 - isFantasy);

                // ── Unity UI 필수 처리 ───────────────────────────────────────
                fixed4 col = fixed4(finalColor, 1.0) * i.color;

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDCG
        }
    }
}
