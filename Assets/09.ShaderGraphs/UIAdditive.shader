// UI 이미지를 가산 합성으로 그리는 셰이더. 빛 번짐처럼 뒤 화면을 밝게 만드는 효과에 쓴다.
//
// 기본 UI 셰이더는 알파 합성이라 뒤 색을 덮을 뿐 밝히지 못한다. 여기서는 그림 색에 이미지의
// 색(Image.color)을 곱해 뒤 화면에 더한다. 투명도는 색의 알파로 줄어든다. 마스크나 클리핑은
// 쓰지 않는다. 터치 효과처럼 화면 전체 오버레이 위에 그리는 조각용이다.
Shader "Monster Kindergarten/UI Additive"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                return tex2D(_MainTex, IN.texcoord) * IN.color;
            }
            ENDCG
        }
    }
}
