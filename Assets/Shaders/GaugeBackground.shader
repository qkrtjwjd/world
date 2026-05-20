Shader "Custom/GaugeBackground"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        // ── 그라디언트 색상 스탑 (왼쪽 환상 → 오른쪽 황금 베이지) ───────────
        _Color0 ("Color 0", Color) = (1.000, 1.000, 1.000, 1)  // #FFFFFF 순백
        _Color1 ("Color 1", Color) = (1.000, 0.980, 0.941, 1)  // #FFFAF0 따뜻한 흰빛
        _Color2 ("Color 2", Color) = (1.000, 0.961, 0.839, 1)  // #FFF5D6 크림빛
        _Color3 ("Color 3", Color) = (1.000, 0.910, 0.478, 1)  // #FFE87A 밝은 황금빛
        _Color4 ("Color 4", Color) = (1.000, 0.843, 0.000, 1)  // #FFD700 황금빛
        _Color5 ("Color 5", Color) = (0.961, 0.784, 0.259, 1)  // #F5C842 살짝 깊어지는 황금
        _Color6 ("Color 6", Color) = (0.910, 0.722, 0.427, 1)  // #E8B86D 부드러운 황금빛 베이지
        _Color7 ("Color 7", Color) = (0.831, 0.659, 0.439, 1)  // #D4A870 황금빛 베이지
        _Color8 ("Color 8", Color) = (0.722, 0.596, 0.502, 1)  // #B89880 베이지 회색

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
            Name "GaugeBackground"

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

            fixed4 _Color0, _Color1, _Color2, _Color3, _Color4;
            fixed4 _Color5, _Color6, _Color7, _Color8;
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

            // ── 9색 그라디언트 샘플 (브랜치 없음) ────────────────────────────
            fixed4 SampleGradient(float t)
            {
                float s  = saturate(t) * 8.0;
                fixed4 c = lerp(_Color0, _Color1, saturate(s));
                c = lerp(c, lerp(_Color1, _Color2, saturate(s - 1.0)), step(1.0, s));
                c = lerp(c, lerp(_Color2, _Color3, saturate(s - 2.0)), step(2.0, s));
                c = lerp(c, lerp(_Color3, _Color4, saturate(s - 3.0)), step(3.0, s));
                c = lerp(c, lerp(_Color4, _Color5, saturate(s - 4.0)), step(4.0, s));
                c = lerp(c, lerp(_Color5, _Color6, saturate(s - 5.0)), step(5.0, s));
                c = lerp(c, lerp(_Color6, _Color7, saturate(s - 6.0)), step(6.0, s));
                c = lerp(c, lerp(_Color7, _Color8, saturate(s - 7.0)), step(7.0, s));
                return c;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 배경은 항상 전체 UV에 그라디언트 표시
                fixed4 col = SampleGradient(i.texcoord.x);

                // CanvasGroup 알파(버텍스 컬러) 반영
                col *= i.color;

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
