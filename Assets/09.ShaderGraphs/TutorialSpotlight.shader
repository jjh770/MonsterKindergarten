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
            #pragma target 2.0
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
