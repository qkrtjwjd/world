Shader "Custom/GaugeRealityFill"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        // ── 그라디언트 색상 스탑 (왼쪽 보라회색 → 오른쪽 깊은 검정) ─────────
        _Color0 ("Color 0", Color) = (0.545, 0.522, 0.565, 1)  // #8B8590 중립 보라빛 회색
        _Color1 ("Color 1", Color) = (0.420, 0.376, 0.439, 1)  // #6B6070 어두운 보라빛 회색
        _Color2 ("Color 2", Color) = (0.322, 0.251, 0.345, 1)  // #524058 더 어두운 보라
        _Color3 ("Color 3", Color) = (0.239, 0.176, 0.282, 1)  // #3D2D48 깊은 보라
        _Color4 ("Color 4", Color) = (0.165, 0.118, 0.208, 1)  // #2A1E35 거의 검정에 가까운 보라
        _Color5 ("Color 5", Color) = (0.102, 0.063, 0.125, 1)  // #1A1020 매우 깊은 어둠
        _Color6 ("Color 6", Color) = (0.059, 0.031, 0.059, 1)  // #0F080F 검정 직전
        _Color7 ("Color 7", Color) = (0.020, 0.020, 0.020, 1)  // #050505 깊은 검정

        // ── 인형화 오버레이 (alpha 0~0.6, 코드에서 설정) ──────────────────────
        _OverlayColor ("Overlay Color", Color) = (0, 0, 0, 0)

        // ── Unity UI 필수 속성 ────────────────────────────────────────────────
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

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "GaugeRealityFill"

            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   2.0

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

            fixed4 _Color0, _Color1, _Color2, _Color3;
            fixed4 _Color4, _Color5, _Color6, _Color7;
            fixed4 _OverlayColor;
            float4 _ClipRect;

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

            // ── 8색 그라디언트 샘플 (브랜치 없음) ────────────────────────────
            fixed4 SampleGradient(float t)
            {
                float s  = saturate(t) * 7.0;
                float s0 = saturate(s - 0.0);
                float s1 = saturate(s - 1.0);
                float s2 = saturate(s - 2.0);
                float s3 = saturate(s - 3.0);
                float s4 = saturate(s - 4.0);
                float s5 = saturate(s - 5.0);
                float s6 = saturate(s - 6.0);

                fixed4 c = lerp(_Color0, _Color1, s0);
                c = lerp(c, lerp(_Color1, _Color2, s1), step(1.0, s));
                c = lerp(c, lerp(_Color2, _Color3, s2), step(2.0, s));
                c = lerp(c, lerp(_Color3, _Color4, s3), step(3.0, s));
                c = lerp(c, lerp(_Color4, _Color5, s4), step(4.0, s));
                c = lerp(c, lerp(_Color5, _Color6, s5), step(5.0, s));
                c = lerp(c, lerp(_Color6, _Color7, s6), step(6.0, s));
                return c;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // fill rect 내 UV.x 그대로 사용 — Unity RTL이 fill rect 크기를 조절
                fixed4 col = SampleGradient(i.texcoord.x);

                // CanvasGroup 알파(버텍스 컬러) 반영
                col *= i.color;

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                // 인형화 오버레이 (검은 보라 잠식 연출)
                col.rgb = lerp(col.rgb, _OverlayColor.rgb, _OverlayColor.a);

                return col;
            }
            ENDCG
        }
    }
}
