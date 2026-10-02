// 슬라임 전용 외곽선 셰이더.
//
// SpriteOutline 애셋의 Sprites/Outline은 스프라이트 하나가 텍스처 한 장 전체라고 가정한다.
// 슬라임 그림은 스프라이트 아틀라스에 묶여 있어서 그 가정이 깨지고, 그래서 외곽선이
// 방향마다 굵기가 다르거나 이웃 스프라이트의 조각이 위쪽에 하얗게 묻어났다.
//
// 이 셰이더는 스프라이트가 차지하는 사각형(_SpriteUV)을 받아 그 안에서만 계산한다.
// 사각형 밖은 샘플하지 않으므로 이웃 조각이 섞일 수 없다. _SpriteUV는 SpriteRenderer마다
// 다르므로 SlimeSpriteMaterialDriver가 MaterialPropertyBlock으로 넣는다. 넣지 않으면
// 기본값(0,0,1,1)이라 아틀라스에 묶이지 않은 스프라이트는 예전과 같게 그려진다.
//
// _Special이 1이면 특별한 슬라임이다. 몸에 무지개 광택이 흐르고 외곽선이 무지개색이 된다.
//
// UI Image에도 쓴다(도감). 스텐실과 클립 사각형을 UI/Default와 같은 방식으로 받아 스크롤
// 뷰의 마스크 안에서 잘린다. _OutlineFollowsAlpha가 1이면 외곽선도 정점 알파를 따라가
// 페이드하는 UI 안에서 외곽선만 남지 않는다. 스프라이트에서는 0이라 예전과 같다.
Shader "Slime/Outline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Outline)]
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _Thickness ("Outline Width (texels)", Float) = 40

        [Header(Special)]
        _RainbowSpeed ("Rainbow Body Speed", Float) = 0.35
        _OutlineSpeed ("Rainbow Outline Speed (cycles per second)", Float) = 0.8
        _OutlineSaturation ("Rainbow Outline Saturation", Range(0, 1)) = 0.8
        _RainbowBody ("Rainbow Body Strength", Range(0, 1)) = 0.55

        [Header(Set Per Renderer)]
        _SpriteUV ("Sprite Rect In Texture (xy min, zw size)", Vector) = (0,0,1,1)
        _ArtRect ("Art Rect In Sprite (xy min, zw max)", Vector) = (0,0,1,1)
        _Special ("Special (0 or 1)", Float) = 0

        [Header(UI)]
        _OutlineFollowsAlpha ("Outline Follows Vertex Alpha", Range(0, 1)) = 0
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
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
        Blend One OneMinusSrcAlpha

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

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
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _OutlineColor;
            float _Thickness;
            float _RainbowSpeed;
            float _OutlineSpeed;
            float _OutlineSaturation;
            float _RainbowBody;
            float4 _SpriteUV;
            float4 _ArtRect;
            float _Special;
            float _OutlineFollowsAlpha;
            float4 _ClipRect;

            // 바깥 고리와 안쪽 고리. 안쪽 고리는 반지름 안에 통째로 들어가는 작은 조각
            // (코인, 뿔 끝)이 바깥 고리 사이로 빠져 외곽선이 끊기는 것을 막는다.
            static const int OuterSteps = 48;
            static const int InnerSteps = 24;
            static const float AlphaCutoff = 0.02;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.worldPosition = IN.vertex;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
                #endif
                return OUT;
            }

            fixed3 Hsv(float h, float s, float v)
            {
                float3 k = abs(frac(h + float3(0.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0);
                return v * lerp(float3(1, 1, 1), saturate(k - 1.0), s);
            }

            // content는 그림이 놓이는 0~1 사각형이다. 밖은 투명으로 본다.
            fixed4 SampleContent(float2 content)
            {
                if (content.x < 0.0 || content.x > 1.0 || content.y < 0.0 || content.y > 1.0)
                {
                    return fixed4(0, 0, 0, 0);
                }

                // 반복문 안에서 조기 종료하므로 미분이 필요한 tex2D 대신 밉 0을 직접 읽는다.
                return tex2Dlod(_MainTex, float4(_SpriteUV.xy + content * _SpriteUV.zw, 0.0, 0.0));
            }

            fixed4 Shade(v2f IN)
            {
                // 스프라이트 사각형 안에서의 위치. 0~1이다.
                float2 local = (IN.texcoord - _SpriteUV.xy) / _SpriteUV.zw;

                // 스프라이트가 차지하는 텍셀 수. 외곽선 두께를 텍셀로 재는 기준이다.
                float2 texels = _SpriteUV.zw * _MainTex_TexelSize.zw;
                float2 pad = _Thickness / texels;

                // 그림을 안쪽으로 줄여 가장자리에 외곽선 자리를 만든다. 사각형 자체는
                // 메시라서 늘릴 수 없다.
                float2 scale = 1.0 / max(1.0 - 2.0 * pad, 0.01);
                float2 content = (local - 0.5) * scale + 0.5;

                fixed4 c = SampleContent(content) * IN.color;
                fixed3 rgb = c.rgb;

                float rainbowPhase = local.x * 0.55 + local.y * 0.45 - _Time.y * _RainbowSpeed;
                if (_Special > 0.5)
                {
                    fixed3 rainbow = Hsv(rainbowPhase, 0.75, 1.0);
                    fixed3 shimmer = saturate(rgb * (0.55 + rainbow * 0.9) + rainbow * 0.12);
                    rgb = lerp(rgb, shimmer, _RainbowBody);
                }

                fixed4 body = fixed4(rgb * c.a, c.a);

                // 완전히 불투명한 곳에는 외곽선이 없다. 가장자리의 반투명 픽셀은 외곽선 위에
                // 겹쳐 그려 테두리에 틈이 생기지 않게 한다.
                if (c.a >= 0.999 || _Thickness <= 0.0)
                {
                    return body;
                }

                // 그림이 있는 사각형에서 아웃라인 반지름보다 먼 픽셀은 탐색해도 닿을 것이 없다.
                // 사각형의 대부분이 이런 픽셀이라 이 검사가 계산량의 대부분을 덜어 낸다.
                float2 artMin = (_ArtRect.xy - 0.5) * (1.0 / scale) + 0.5;
                float2 artMax = (_ArtRect.zw - 0.5) * (1.0 / scale) + 0.5;
                float2 outside = max(max(artMin - local, local - artMax), 0.0) * texels;
                if (dot(outside, outside) > _Thickness * _Thickness)
                {
                    return body;
                }

                // 반지름은 여백보다 조금 작게 잡는다. 그림이 바깥 끝까지 닿아 있어도 사각형
                // 경계에서 외곽선이 잘리지 않는다.
                float2 radius = pad * 0.98;
                bool hit = false;

                [loop]
                for (int i = 0; i < OuterSteps && !hit; i++)
                {
                    float a = (i + 0.5) * (6.2831853 / OuterSteps);
                    float2 p = local + float2(cos(a), sin(a)) * radius;
                    hit = SampleContent((p - 0.5) * scale + 0.5).a > AlphaCutoff;
                }

                [loop]
                for (int j = 0; j < InnerSteps && !hit; j++)
                {
                    float a = (j + 0.25) * (6.2831853 / InnerSteps);
                    float2 p = local + float2(cos(a), sin(a)) * radius * 0.5;
                    hit = SampleContent((p - 0.5) * scale + 0.5).a > AlphaCutoff;
                }

                if (!hit)
                {
                    return body;
                }

                fixed3 outlineRgb = _OutlineColor.rgb;
                if (_Special > 0.5)
                {
                    // 외곽선의 색은 그림 중심을 도는 각도로 정하고 시간에 따라 흘려 보낸다.
                    // 색이 테두리를 따라 계속 돌아서 멈춘 그림처럼 보이지 않는다.
                    float2 fromCenter = local - 0.5;
                    float around = atan2(fromCenter.y, fromCenter.x) * 0.15915494 + 0.5;
                    outlineRgb = Hsv(around - _Time.y * _OutlineSpeed, _OutlineSaturation, 1.0);
                }

                // UI에서는 캔버스 그룹의 페이드를 정점 알파가 실어 온다. 스프라이트에서는
                // _OutlineFollowsAlpha가 0이라 외곽선이 색의 알파와 무관하게 그려진다.
                float outlineAlpha = _OutlineColor.a * lerp(1.0, IN.color.a, _OutlineFollowsAlpha);
                fixed4 outline = fixed4(outlineRgb * outlineAlpha, outlineAlpha);
                return body + outline * (1.0 - body.a);
            }

            // 클립 사각형은 색을 미리 곱한 결과에 그대로 곱해도 된다.
            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 color = Shade(IN);
                #ifdef UNITY_UI_CLIP_RECT
                color *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
