// 受击闪白 Shader：结构完全照抄内置 Sprites-Default（premultiplied alpha），
// 仅增加一个 _FlashAmount 参数，在贴图色基础上向纯白插值。
// _FlashAmount=0 与原版渲染一致；=1 整体纯白（保留 alpha 轮廓）。
Shader "Custom/SpriteFlashWhite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FlashAmount ("Flash Amount", Range(0, 1)) = 0
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

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
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

            fixed4 _Color;
            float  _FlashAmount;
            sampler2D _MainTex;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex   = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color    = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, IN.texcoord) * IN.color;
                // 闪白：先向纯白插值（alpha 不变，轮廓柔和），再 premultiply
                c.rgb = lerp(c.rgb, fixed3(1, 1, 1), _FlashAmount);
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
