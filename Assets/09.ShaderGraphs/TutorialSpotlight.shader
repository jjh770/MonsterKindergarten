Shader "UI/Tutorial Spotlight"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _HoleCenter ("Hole Center", Vector) = (0.5,0.5,0,0)
        _HoleSize ("Hole Half Size", Vector) = (0.15,0.15,0,0)
        _SecondHoleCenter ("Second Hole Center", Vector) = (0.5,0.5,0,0)
        _SecondHoleSize ("Second Hole Half Size", Vector) = (0.15,0.15,0,0)
        _SecondHoleEnabled ("Second Hole Enabled", Float) = 0
        _HoleShape ("Hole Shape", Float) = 0
        _SecondHoleShape ("Second Hole Shape", Float) = 0
        _HoleSoftness ("Hole Softness", Range(0.001, 0.5)) = 0.08
        _GlowColor ("Glow Halo Color", Color) = (1, 0.82, 0.3, 1)
        _GlowCoreColor ("Glow Core Color", Color) = (1, 0.97, 0.8, 1)
        _GlowWidth ("Glow Halo Width (canvas units)", Range(1, 200)) = 44
        _GlowCoreWidth ("Glow Core Width (canvas units)", Range(0.5, 20)) = 3
        _GlowIntensity ("Glow Intensity", Range(0, 5)) = 1.3
        _GlowOuterRatio ("Glow Outer Ratio", Range(0, 2)) = 1
        _GlowInnerRatio ("Glow Inner Ratio", Range(0, 2)) = 0.6
        _GlowInnerFill ("Glow Inner Fill", Range(0, 1)) = 0.1
        _GlowPulseSpeed ("Glow Pulse Speed", Range(0, 10)) = 2.0
        _GlowPulseAmount ("Glow Pulse Amount", Range(0, 1)) = 0.35
        _GlowSweepAmount ("Glow Sweep Amount", Range(0, 2)) = 0.8
        _GlowSweepSpeed ("Glow Sweep Speed (turns per second)", Range(-2, 2)) = 0.3
        _GlowSweepWidth ("Glow Sweep Width (turn fraction)", Range(0.02, 0.5)) = 0.16
        _RootSize ("Root Size (canvas units)", Vector) = (1080,1920,0,0)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
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
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _HoleCenter;
            float4 _HoleSize;
            float4 _SecondHoleCenter;
            float4 _SecondHoleSize;
            float _SecondHoleEnabled;
            float _HoleShape;
            float _SecondHoleShape;
            float _HoleSoftness;
            fixed4 _GlowColor;
            fixed4 _GlowCoreColor;
            float _GlowWidth;
            float _GlowCoreWidth;
            float _GlowIntensity;
            float _GlowOuterRatio;
            float _GlowInnerRatio;
            float _GlowInnerFill;
            float _GlowPulseSpeed;
            float _GlowPulseAmount;
            float _GlowSweepAmount;
            float _GlowSweepSpeed;
            float _GlowSweepWidth;
            float4 _RootSize;

            // 구멍 경계까지의 부호 있는 거리다. 안쪽이 음수이고 단위는 캔버스 단위(px)다.
            // 정규화된 거리에 반지름을 곱하지 않고 화면 크기를 곱해 구하므로 가로로 긴 구멍도
            // 네 변의 띠 두께가 같다. 사각형은 정확하고, 타원은 짧은 반지름 기준의 근사다.
            float SignedEdgeDistance(
                float2 uv,
                float2 center,
                float2 halfSize,
                float shape,
                float2 rootSize)
            {
                float2 p = (uv - center) * rootSize;
                float2 h = max(halfSize * rootSize, float2(0.0001, 0.0001));
                float2 q = abs(p) - h;
                float rectangleEdge = max(q.x, q.y);
                float ellipseEdge = (length(p / h) - 1.0) * min(h.x, h.y);
                return lerp(ellipseEdge, rectangleEdge, saturate(shape));
            }

            // 구멍 하나가 만드는 발광 성분이다. x는 후광, y는 코어 선, z는 구멍 안쪽 채움,
            // w는 테두리를 도는 하이라이트의 위치 가중치다. 후광은 경계에서 가장 밝고 양쪽으로
            // 옅어지며, 바깥과 안쪽 폭은 _GlowOuterRatio와 _GlowInnerRatio로 따로 정한다. 코어는 경계 안쪽에
            // 붙는 얇은 선이라, 구멍을 화면 안으로 잘라 넘겨도 화면 가장자리에서 보인다.
            // 띠에서 먼 픽셀은 바로 돌려보내 전체 화면 오버레이의 비용을 줄인다.
            float4 HoleGlow(
                float2 uv,
                float2 center,
                float2 halfSize,
                float shape,
                float2 rootSize,
                float wave)
            {
                float edge = SignedEdgeDistance(uv, center, halfSize, shape, rootSize);
                float haloWidth = max(_GlowWidth, 1.0) * (1.0 + _GlowPulseAmount * 0.3 * wave);
                float outerWidth = max(haloWidth * _GlowOuterRatio, 0.0001);
                float innerWidth = max(haloWidth * _GlowInnerRatio, 0.0001);
                float innerReach = max(innerWidth, haloWidth) * 1.5;
                if (edge > outerWidth || edge < -innerReach)
                {
                    return float4(0.0, 0.0, 0.0, 0.0);
                }

                float side = edge > 0.0 ? outerWidth : innerWidth;
                float halo = 1.0 - saturate(abs(edge) / side);
                halo *= halo;

                float coreWidth = max(_GlowCoreWidth, 0.5);
                float core = 1.0 - smoothstep(
                    coreWidth * 0.5,
                    coreWidth * 0.5 + 1.0,
                    abs(edge + coreWidth * 0.5));

                float fill = edge < 0.0 ? 1.0 - saturate(-edge / innerReach) : 0.0;
                fill *= fill;

                // 구멍 중심에서 본 각도를 한 바퀴 1.0으로 잡고, 시간에 따라 움직이는 한 점
                // 가까이에서만 커지는 가중치를 만든다.
                float2 direction = (uv - center) / max(halfSize, float2(0.0001, 0.0001));
                float turn = atan2(direction.y, direction.x) * 0.15915494 + 0.5;
                float behind = frac(turn - _Time.y * _GlowSweepSpeed);
                float distanceToSpot = min(behind, 1.0 - behind);
                float spot = 1.0 - smoothstep(0.0, max(_GlowSweepWidth, 0.001), distanceToSpot);

                return float4(halo, core, fill, spot);
            }

            // layer를 base 위에 얹은 결과다. 구멍이 뚫려 알파가 낮은 곳에서도 발광 색이
            // 어두운 오버레이 색에 끌려가지 않고 그대로 나오게 한다.
            float4 OverLayer(float4 base, float3 layerColor, float layerAlpha)
            {
                float outAlpha = layerAlpha + base.a * (1.0 - layerAlpha);
                float3 outColor =
                    (layerColor * layerAlpha + base.rgb * base.a * (1.0 - layerAlpha)) /
                    max(outAlpha, 0.0001);
                return float4(outColor, outAlpha);
            }

            v2f vert(appdata_t input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.worldPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 color = (tex2D(_MainTex, input.texcoord) + _TextureSampleAdd) * input.color;
                float2 holeSize = max(_HoleSize.xy, float2(0.0001, 0.0001));
                float2 normalizedHolePosition =
                    (input.texcoord - _HoleCenter.xy) / holeSize;
                float ellipseDistance = length(normalizedHolePosition);
                float rectangleDistance = max(
                    abs(normalizedHolePosition.x),
                    abs(normalizedHolePosition.y));
                float holeDistance = lerp(
                    ellipseDistance,
                    rectangleDistance,
                    saturate(_HoleShape));
                float outsideHole = smoothstep(
                    1.0 - max(_HoleSoftness, 0.0001),
                    1.0,
                    holeDistance);
                float2 secondHoleSize = max(
                    _SecondHoleSize.xy,
                    float2(0.0001, 0.0001));
                float2 normalizedSecondHolePosition =
                    (input.texcoord - _SecondHoleCenter.xy) / secondHoleSize;
                float secondEllipseDistance = length(normalizedSecondHolePosition);
                float secondRectangleDistance = max(
                    abs(normalizedSecondHolePosition.x),
                    abs(normalizedSecondHolePosition.y));
                float secondHoleDistance = lerp(
                    secondEllipseDistance,
                    secondRectangleDistance,
                    saturate(_SecondHoleShape));
                float outsideSecondHole = smoothstep(
                    1.0 - max(_HoleSoftness, 0.0001),
                    1.0,
                    secondHoleDistance);
                float combinedHole = lerp(
                    outsideHole,
                    min(outsideHole, outsideSecondHole),
                    saturate(_SecondHoleEnabled));
                color.a *= combinedHole;

                // 후광, 코어 선, 안쪽 채움, 도는 하이라이트를 구멍마다 구해 겹친다.
                // 두 번째 구멍은 켜져 있을 때만 더한다. _Time.y는 초 단위 시간이라 C# 갱신 없이
                // 셰이더 안에서 저절로 움직이고, 맥동 폭이 0이면 밝기와 폭이 고정된다.
                float2 rootSize = max(_RootSize.xy, float2(1.0, 1.0));
                float wave = sin(_Time.y * _GlowPulseSpeed);
                float4 glow = HoleGlow(
                    input.texcoord, _HoleCenter.xy, holeSize, _HoleShape, rootSize, wave);
                float4 secondGlow = HoleGlow(
                    input.texcoord, _SecondHoleCenter.xy, secondHoleSize,
                    _SecondHoleShape, rootSize, wave);
                glow = max(glow, secondGlow * saturate(_SecondHoleEnabled));

                float pulse = 1.0 - _GlowPulseAmount * 0.5 + _GlowPulseAmount * 0.5 * wave;
                float boost = 1.0 + _GlowSweepAmount * glow.w;
                float haloAlpha = saturate(
                    (glow.x * _GlowIntensity * pulse + glow.z * _GlowInnerFill) * boost);
                float coreAlpha = saturate(glow.y * boost);

                float4 result = color;
                result = OverLayer(result, _GlowColor.rgb, haloAlpha * _GlowColor.a);
                result = OverLayer(result, _GlowCoreColor.rgb, coreAlpha * _GlowCoreColor.a);
                color = fixed4(result);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
