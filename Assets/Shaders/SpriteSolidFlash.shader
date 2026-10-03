// 피격 플래시용 — 스프라이트의 알파 모양만 살리고 색은 단색(정점색 × _Color)으로 채운다.
// UnitHitReactor가 유닛 스프라이트 위에 같은 스프라이트를 이 재질로 겹쳐 흰 실루엣을 만든다 (타격감 리워크 2026-10-03).
// LightMode 태그가 없는 단순 unlit 패스 — URP(SRPDefaultUnlit)·WebGL에서 그대로 동작한다.
Shader "DiceOrbit/SpriteSolidFlash"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
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
        Blend One OneMinusSrcAlpha

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
                // 텍스처 색은 버리고 알파만 — 단색 실루엣 (프리멀티플라이드 알파)
                fixed alpha = tex2D(_MainTex, IN.texcoord).a * IN.color.a;
                return fixed4(IN.color.rgb * alpha, alpha);
            }
            ENDCG
        }
    }
}
