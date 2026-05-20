// =============================================================================
//  GlassCrack.shader — URP UI 유리 균열 셰이더 (사실적 톤)
//  fBm 곡선 주균열 + 재귀 분기 + Voronoi 파편 + 미세 헤어 크랙
//  단면 프로파일: 어두운 중심(균열 골) + 밝은 rim(빛 반사 모서리)
// =============================================================================

Shader "UI/GlassCrack"
{
    Properties
    {
        [PerRendererData] _MainTex ("Main Texture", 2D) = "white" {}

        // ── 공통 ────────────────────────────────────────────────────────────
        _CrackAmount    ("Crack Amount",    Range(0, 1))        = 0.0
        _CrackColor     ("균열 골 색상 (어두움)",  Color)          = (0, 0, 0, 1)
        _HighlightColor ("Rim 색상 (밝음)",        Color)          = (1, 1, 1, 1)
        _CrackIntensity ("Crack Intensity", Range(0, 5))        = 1.2
        _ImpactPoint    ("Impact Point",    Vector)             = (0.5, 0.5, 0, 0)
        _RadialFalloff  ("Radial Falloff",  Range(0, 3))        = 1.5

        // ── 주 균열 ──────────────────────────────────────────────────────────
        _MainCrackCount  ("주 균열 개수",       Range(3, 16))       = 8
        _MainCrackWidth  ("주 균열 기본 굵기",  Range(0.001, 0.02)) = 0.005
        _MainCrackTaper  ("주 균열 가늘어짐",   Range(0, 5))        = 2.5
        _MainCrackNoise  ("주 균열 지그재그",   Range(0, 1))        = 0.6
        _NoiseFrequency  ("노이즈 주파수",      Range(1, 50))       = 25
        _Curvature       ("주 균열 곡률",       Range(0, 1.5))      = 0.5

        // ── 분기 ────────────────────────────────────────────────────────────
        _BranchCount     ("1차 분기 개수",  Range(0, 5))            = 3
        _BranchAngle     ("분기 각도",      Range(0, 1.5))          = 0.7
        _BranchLength    ("분기 길이 비율", Range(0, 1))            = 0.5
        _SubBranchChance ("2차 분기 확률",  Range(0, 1))            = 0.6

        // ── Voronoi 셀 파편 ─────────────────────────────────────────────────
        _ShardDensity    ("파편 밀도",        Range(2, 30))       = 14
        _ShardEdgeWidth  ("파편 경계 굵기",   Range(0.005, 0.2))  = 0.025
        _ShardFalloff    ("파편 밀도 감쇠",   Range(0.5, 4))      = 1.8
        _ShardDepth      ("셀 톤 편차",       Range(0, 0.3))      = 0.08
        _ShardFill       ("셀 내부 알파",     Range(0, 0.3))      = 0.02
        _ShardShine      ("셀 내부 반짝임",   Range(0, 0.5))      = 0.15

        // ── 미세 헤어 크랙 ──────────────────────────────────────────────────
        _MicroCrackFreq  ("미세 크랙 주파수", Range(20, 300))     = 140
        _MicroCrackWidth ("미세 크랙 굵기",   Range(0, 0.1))      = 0.02
        _MicroCrackRange ("미세 크랙 범위",   Range(0, 1))        = 0.35
        _MicroCrackAmount("미세 크랙 세기",   Range(0, 1))        = 0.5

        // ── 프로파일 / 톤 ───────────────────────────────────────────────────
        _HighlightRatio  ("골 폭 비율(중심 어두움)", Range(0, 0.9))  = 0.55
        _StyleHardness   ("에지 하드함",             Range(0, 1))    = 0.1

        // ── 발광 ─────────────────────────────────────────────────────────────
        _GlowStrength    ("발광 강도",  Range(0, 5))                = 0.8
        _GlowFalloff     ("발광 감쇠",  Range(0.5, 5))              = 2.5

        // ── 산산조각(Shatter) ────────────────────────────────────────────────
        _ShatterAmount   ("Shatter Amount", Range(0, 1))           = 0.0
        _ShatterScale    ("Shatter Scale",  Range(1, 5))           = 2.5

        // ── UI Stencil ───────────────────────────────────────────────────────
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
            Name "GlassCrack"

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ── 버텍스 구조체 ────────────────────────────────────────────────
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

            // ── 텍스처 ───────────────────────────────────────────────────────
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // ── 상수 버퍼 (SRP Batcher 호환) ─────────────────────────────────
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float  _CrackAmount;
                float4 _CrackColor;
                float4 _HighlightColor;
                float  _CrackIntensity;
                float4 _ImpactPoint;
                float  _RadialFalloff;
                float  _MainCrackCount;
                float  _MainCrackWidth;
                float  _MainCrackTaper;
                float  _MainCrackNoise;
                float  _NoiseFrequency;
                float  _Curvature;
                float  _BranchCount;
                float  _BranchAngle;
                float  _BranchLength;
                float  _SubBranchChance;
                float  _ShardDensity;
                float  _ShardEdgeWidth;
                float  _ShardFalloff;
                float  _ShardDepth;
                float  _ShardFill;
                float  _ShardShine;
                float  _MicroCrackFreq;
                float  _MicroCrackWidth;
                float  _MicroCrackRange;
                float  _MicroCrackAmount;
                float  _HighlightRatio;
                float  _StyleHardness;
                float  _GlowStrength;
                float  _GlowFalloff;
                float  _ShatterAmount;
                float  _ShatterScale;
            CBUFFER_END

            float unity_GUIZTestMode;

            // ── 버텍스 셰이더 ────────────────────────────────────────────────
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

            // ── 해시 / 노이즈 ────────────────────────────────────────────────
            float Hash1(float x)
            {
                return frac(sin(x) * 43758.5453);
            }

            float2 Hash2(float2 p)
            {
                return frac(sin(float2(
                    dot(p, float2(127.1, 311.7)),
                    dot(p, float2(269.5, 183.3))
                )) * 43758.5453);
            }

            float ValueNoise(float2 np)
            {
                float2 ni = floor(np);
                float2 nf = frac(np);
                nf = nf * nf * (3.0 - 2.0 * nf);

                float na = frac(sin(dot(ni + float2(0,0), float2(127.1, 311.7))) * 43758.5);
                float nb = frac(sin(dot(ni + float2(1,0), float2(127.1, 311.7))) * 43758.5);
                float nc = frac(sin(dot(ni + float2(0,1), float2(127.1, 311.7))) * 43758.5);
                float nd = frac(sin(dot(ni + float2(1,1), float2(127.1, 311.7))) * 43758.5);

                return lerp(lerp(na, nb, nf.x), lerp(nc, nd, nf.x), nf.y);
            }

            // 2-옥타브 fBm (컴파일 시간 단축)
            float Fbm2(float2 p, float seed)
            {
                float2 op = p + float2(seed * 1.37, seed * 2.91);
                float v  = ValueNoise(op) * 0.6;
                v       += ValueNoise(op * 2.03) * 0.3;
                return v;
            }

            // Voronoi F1, F2, cell ID
            // 반환: (F1, F2, cellId.x, cellId.y)
            float4 VoronoiF1F2(float2 p)
            {
                float2 pi = floor(p);
                float2 pf = frac(p);
                float F1 = 8.0;
                float F2 = 8.0;
                float2 bestCell = float2(0, 0);

                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 neigh = float2(x, y);
                        float2 cellId = pi + neigh;
                        float2 jitter = Hash2(cellId);
                        float2 diff = neigh + jitter - pf;
                        float d = dot(diff, diff);

                        if (d < F1)
                        {
                            F2 = F1;
                            F1 = d;
                            bestCell = cellId;
                        }
                        else if (d < F2)
                        {
                            F2 = d;
                        }
                    }
                }
                return float4(sqrt(F1), sqrt(F2), bestCell);
            }

            // 2-톤 에지 계산: outline, core 동시 반환
            // x: outline(굵은 바깥선), y: core(얇은 하이라이트선)
            float2 TwoToneEdge(float absV, float width)
            {
                float hard = saturate(_StyleHardness);
                float tr   = width * (1.0 - hard * 0.92);

                // outline: |v| < width
                float outline = 1.0 - smoothstep(width - tr, width, absV);

                // core: |v| < width * _HighlightRatio
                float iW = width * _HighlightRatio;
                float iTr = iW * (1.0 - hard * 0.92);
                float core = (_HighlightRatio > 0.001)
                    ? (1.0 - smoothstep(iW - iTr, iW, absV))
                    : 0.0;

                return float2(outline, core);
            }

            // ── 프래그먼트 셰이더 ────────────────────────────────────────────
            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv       = IN.uv;
                float2 impactUV = _ImpactPoint.xy;

                // Shatter: UV를 임팩트 방향으로 당겨 패턴이 확장되는 느낌
                float shatterScale = 1.0 + _ShatterAmount * _ShatterScale;
                uv = impactUV + (uv - impactUV) / shatterScale;

                float2 delta = uv - impactUV;
                float  dist  = length(delta);

                // 방사형 진행 마스크
                float localCrack = _CrackAmount - dist * _RadialFalloff;
                if (localCrack <= 0.0)
                    return half4(0.0, 0.0, 0.0, 0.0);

                // ─── 1. 주 균열 (fBm 곡선 경로) + 재귀 2차 분기 ────────────
                float mainOutline = 0.0;
                float mainCore    = 0.0;

                int mainCountI = (int)_MainCrackCount;
                [loop]
                for (int ci = 0; ci < mainCountI; ci++)
                {
                    float crackSeed = float(ci) * 1.618033;
                    float baseAngle = Hash1(crackSeed) * 6.28318 - 3.14159;

                    // 축 / 수직 벡터
                    float2 axis = float2(cos(baseAngle), sin(baseAngle));
                    float2 perp = float2(-axis.y, axis.x);

                    float u = dot(delta, axis); // 따라가는 거리
                    float v = dot(delta, perp); // 수직 거리

                    // 뒤쪽(u < 0)은 다른 균열 영역 → 무시
                    if (u <= 0.0) continue;

                    // fBm 곡률: 뿌리에서 끝으로 갈수록 휨이 커짐
                    float curv  = Fbm2(float2(u * _NoiseFrequency, crackSeed * 7.3), crackSeed) - 0.5;
                    float vAdj  = v - curv * _Curvature * u * 0.8;

                    // 미세 지그재그 (고주파)
                    float zig   = (Fbm2(float2(u * _NoiseFrequency * 2.5, crackSeed * 11.1), crackSeed * 3.1) - 0.5)
                                  * _MainCrackNoise * 0.04;
                    vAdj       += zig;

                    // 테이퍼 굵기
                    float taperBase = max(0.0, 1.0 - u * 0.7);
                    float tapW      = _MainCrackWidth * pow(taperBase, _MainCrackTaper);
                    tapW           *= 0.7 + ValueNoise(float2(u * 30.0, crackSeed)) * 0.6;
                    tapW            = max(tapW, 0.0001);

                    // 길이 페이드
                    float lenFade = Hash1(crackSeed * 7.123);
                    float maxLen  = 0.3 + lenFade * 0.5;
                    float lenMask = 1.0 - smoothstep(maxLen * 0.7, maxLen, u);

                    float2 edgeMC = TwoToneEdge(abs(vAdj), tapW) * lenMask;
                    mainOutline = max(mainOutline, edgeMC.x);
                    mainCore    = max(mainCore,    edgeMC.y);

                    // 1차 분기 (dynamic loop)
                    int brCountI = (int)_BranchCount;
                    [loop]
                    for (int bi = 0; bi < brCountI; bi++)
                    {
                        float brSeed = crackSeed * 3.7 + float(bi) * 17.3;
                        float brU    = 0.08 + Hash1(brSeed) * 0.25;
                        if (u <= brU) continue;

                        float brDirSign = sign(Hash1(brSeed * 2.1) - 0.5);
                        float brAng     = _BranchAngle * brDirSign * (0.6 + Hash1(brSeed * 3.7) * 0.8);

                        float ca = cos(brAng);
                        float sa = sin(brAng);
                        float2 brAxis = float2(axis.x*ca - axis.y*sa, axis.x*sa + axis.y*ca);
                        float2 brPerp = float2(-brAxis.y, brAxis.x);

                        float2 brOrigin = impactUV + axis * brU;
                        float2 brDelta  = uv - brOrigin;
                        float  bu = dot(brDelta, brAxis);
                        float  bv = dot(brDelta, brPerp);

                        if (bu <= 0.0) continue;

                        float bCurv = Fbm2(float2(bu * _NoiseFrequency, brSeed * 4.1), brSeed) - 0.5;
                        float bvAdj = bv - bCurv * _Curvature * bu * 0.7;

                        float bTapW = max(tapW * 0.55 * max(0.0, 1.0 - bu * 1.2), 0.0001);

                        float brMaxLen = (maxLen - brU) * _BranchLength;
                        float bLenMask = 1.0 - smoothstep(brMaxLen * 0.7, brMaxLen, bu);

                        float2 edgeB = TwoToneEdge(abs(bvAdj), bTapW) * bLenMask;
                        mainOutline = max(mainOutline, edgeB.x);
                        mainCore    = max(mainCore,    edgeB.y);

                        // 2차 분기 (확률적, 가벼운 단일 갈림)
                        float sbSeed = brSeed * 2.73;
                        if (Hash1(sbSeed) <= _SubBranchChance)
                        {
                            float sbU = 0.04 + Hash1(sbSeed * 1.9) * 0.15;
                            if (bu > sbU)
                            {
                                float sbDirSign = sign(Hash1(sbSeed * 2.1) - 0.5);
                                float sbAng = _BranchAngle * sbDirSign * (0.5 + Hash1(sbSeed * 3.3) * 0.6);
                                float sbCa = cos(sbAng);
                                float sbSa = sin(sbAng);
                                float2 sbAxis = float2(brAxis.x*sbCa - brAxis.y*sbSa, brAxis.x*sbSa + brAxis.y*sbCa);
                                float2 sbPerp = float2(-sbAxis.y, sbAxis.x);

                                float2 sbOrigin = brOrigin + brAxis * sbU;
                                float2 sbDelta  = uv - sbOrigin;
                                float  su = dot(sbDelta, sbAxis);
                                float  sv = dot(sbDelta, sbPerp);

                                if (su > 0.0)
                                {
                                    float sbCurv = ValueNoise(float2(su * _NoiseFrequency, sbSeed * 5.1)) - 0.5;
                                    float svAdj  = sv - sbCurv * _Curvature * su * 0.6;

                                    float sbTapW = max(bTapW * 0.55 * max(0.0, 1.0 - su * 2.0), 0.0001);
                                    float sbMaxLen = (brMaxLen - sbU) * _BranchLength * 0.9;
                                    float sbLenMask = 1.0 - smoothstep(sbMaxLen * 0.7, sbMaxLen, su);

                                    float2 edgeSB = TwoToneEdge(abs(svAdj), sbTapW) * sbLenMask;
                                    mainOutline = max(mainOutline, edgeSB.x);
                                    mainCore    = max(mainCore,    edgeSB.y);
                                }
                            }
                        }
                    }
                }

                // ─── 2. Voronoi 셀 파편 ─────────────────────────────────────
                float cellDensity = lerp(_ShardDensity,
                                         _ShardDensity * 0.35,
                                         saturate(dist * _ShardFalloff));
                float2 cellUV = (uv - impactUV) * cellDensity + float2(13.7, 29.1);

                float4 vor = VoronoiF1F2(cellUV);
                float edgeGap = vor.y - vor.x; // 작을수록 셀 경계

                float shardHard = saturate(_StyleHardness);
                float shardEdgeInner = _ShardEdgeWidth * (1.0 - shardHard * 0.92);
                float shardOutline = 1.0 - smoothstep(shardEdgeInner, _ShardEdgeWidth, edgeGap);

                float shardCoreW = _ShardEdgeWidth * _HighlightRatio;
                float shardCoreInner = shardCoreW * (1.0 - shardHard * 0.92);
                float shardCore = (_HighlightRatio > 0.001)
                    ? (1.0 - smoothstep(shardCoreInner, shardCoreW, edgeGap))
                    : 0.0;

                // 셀 ID 기반 톤 편차
                float cellRand = Hash1(dot(vor.zw, float2(91.3, 47.1)) + 0.7);
                float cellTone = (cellRand - 0.5) * 2.0 * _ShardDepth;

                // 셀 내부 각도 gradient(가짜 굴절 반짝임)
                float cellAngle = Hash1(dot(vor.zw, float2(37.1, 71.9))) * 6.28318;
                float2 cellDir  = float2(cos(cellAngle), sin(cellAngle));
                float2 pCellLocal = frac(cellUV) - 0.5;
                float cellGrad    = saturate(dot(pCellLocal, cellDir) * 1.2 + 0.5);
                float cellShine   = pow(cellGrad, 3.0) * _ShardShine;

                // ─── 3. 미세 헤어 크랙 (고주파 isoline) ────────────────────
                float2 microUV  = (uv - impactUV) * _MicroCrackFreq;
                float microN    = Fbm2(microUV, 5.7);
                float microIso  = 1.0 - smoothstep(0.0, max(_MicroCrackWidth, 0.001), abs(microN - 0.5));
                float microMask = 1.0 - smoothstep(0.0, max(_MicroCrackRange, 0.001), dist);
                float microCrack = microIso * microMask * _MicroCrackAmount;

                // ─── 4. 합성 ────────────────────────────────────────────────
                float crackOutline = max(mainOutline, shardOutline);
                float crackCore    = max(mainCore,    shardCore);
                crackOutline = max(crackOutline, microCrack);
                crackCore    = max(crackCore,    microCrack * 0.6);

                float crackMask = saturate(localCrack * 3.0);
                crackOutline *= crackMask;
                crackCore    *= crackMask;

                // 사실적 프로파일:
                //   darkPart = 균열 골(중심, 빛 차단) → _CrackColor
                //   rimPart  = 외곽 rim(빛 반사)     → _HighlightColor
                float darkPart = crackCore;
                float rimPart  = max(crackOutline - crackCore, 0.0);

                // 글로우(아주 약하게, rim 주변 빛 번짐)
                float glow = pow(crackOutline, _GlowFalloff) * _GlowStrength * 0.3;

                half3 darkRGB = _CrackColor.rgb;
                half3 rimRGB  = _HighlightColor.rgb;

                float totalCrack = max(darkPart + rimPart, 0.0001);
                half3 crackRGB = (darkRGB * darkPart + rimRGB * rimPart) / totalCrack;
                crackRGB *= _CrackIntensity;

                // 셀 내부 기본 알파 + 셀별 톤 편차 + 반짝 streak
                float fillAlpha = _ShardFill * crackMask;
                half3 fillRGB = lerp(darkRGB, rimRGB, 0.5) * _CrackIntensity * (1.0 + cellTone);
                fillRGB += rimRGB * cellShine * _CrackIntensity;

                // 최종 컬러: 크랙 영역은 crackRGB, 나머지는 셀 fill
                float crackPresence = saturate(crackOutline);
                half3 finalRGB = lerp(fillRGB, crackRGB, crackPresence);
                finalRGB += rimRGB * glow * _CrackIntensity; // rim 발광

                float finalAlpha = saturate(max(crackOutline, fillAlpha) + glow * 0.2) * IN.color.a;

                // Shatter 페이드 아웃
                float shatterFade = 1.0 - smoothstep(0.0, 1.0, _ShatterAmount);
                finalAlpha *= shatterFade;

                return half4(finalRGB, finalAlpha);
            }
            ENDHLSL
        }
    }

    FallBack "UI/Default"
}
