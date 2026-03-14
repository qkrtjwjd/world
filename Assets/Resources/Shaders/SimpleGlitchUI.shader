Shader "Custom/SimpleGlitchUI"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _NoiseTex ("Noise Texture", 2D) = "black" {}
        _Intensity ("Glitch Intensity", Range(0, 1)) = 0
        _ColorDrift ("Color Drift", Range(0, 1)) = 0.02
        _ScanLineJitter ("Scan Line Jitter", Range(0, 1)) = 0.05
    }

    SubShader
    {
        Tags
        { 
            "Queue"="Transparent" 
            "IgnoreProjector"="True" 
            "RenderType"="Transparent" 
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            float _Intensity;
            float _ColorDrift;
            float _ScanLineJitter;

            // 랜덤 함수
            float nrand(float x, float y)
            {
                return frac(sin(dot(float2(x, y), float2(12.9898, 78.233))) * 43758.5453);
            }

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float t = _Time.y;

                // 1. 스캔라인 지터 (가로로 찢어지는 효과)
                float jitter = nrand(uv.y, t) * 2 - 1;
                jitter *= step(0.5, abs(jitter)) * _ScanLineJitter * _Intensity;
                
                // 찢어진 UV 적용
                float2 glitchUV = uv;
                glitchUV.x += jitter;

                // 2. 색상 분리 (RGB Split)
                float drift = sin(t) * _ColorDrift * _Intensity;
                
                fixed4 src1 = tex2D(_MainTex, glitchUV + float2(drift, 0));
                fixed4 src2 = tex2D(_MainTex, glitchUV - float2(drift, 0));

                // R, G, B 채널을 각각 다른 위치에서 가져와 섞음
                fixed4 finalColor = fixed4(src1.r, src2.g, src1.b, src1.a);

                // 원본 알파값 유지 (투명도)
                return finalColor * IN.color;
            }
            ENDCG
        }
    }
}