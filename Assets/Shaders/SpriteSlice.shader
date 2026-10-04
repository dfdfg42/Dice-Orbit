// 처치 연출용 — 스프라이트를 직선 하나로 갈라 한쪽만 그린다 (몬스터 처치 '베어 가르기', 2026-10-05).
// DeathSliceEffect가 같은 스프라이트를 두 장 겹쳐 놓고 _Side만 반대로 줘서 두 조각을 만든다.
//   _CutPoint / _CutNormal : 베는 선 (스프라이트 로컬 좌표). _Side = +1이면 법선 쪽, -1이면 반대쪽만 남긴다.
//   _Flash                 : 0~1 — 흰 실루엣 (베이는 순간)
//   _EdgeGlow / _EdgeWidth : 잘린 단면이 빛난다
// 값은 렌더러마다 MaterialPropertyBlock으로 준다. 정점을 로컬 좌표로 받아야 하므로 배칭을 끈다.
// LightMode 태그가 없는 단순 unlit 패스 — URP(SRPDefaultUnlit)·WebGL에서 그대로 동작한다.
Shader "DiceOrbit/SpriteSlice"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _CutPoint ("Cut Point (local xy)", Vector) = (0,0,0,0)
        _CutNormal ("Cut Normal (local xy)", Vector) = (0,1,0,0)
        _Side ("Side (+1 / -1)", Float) = 1
        _Flash ("Flash", Range(0,1)) = 0
        _FlashColor ("Flash Color", Color) = (1,1,1,1)
        _EdgeWidth ("Edge Width (local units)", Float) = 0.05
        _EdgeGlow ("Edge Glow", Range(0,1)) = 0
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
            "DisableBatching" = "True"
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
                float2 localPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _CutPoint;
            float4 _CutNormal;
            float _Side;
            float _Flash;
            fixed4 _FlashColor;
            float _EdgeWidth;
            float _EdgeGlow;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                OUT.localPos = IN.vertex.xy;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // 베는 선에서 이 조각 쪽으로 얼마나 들어와 있나 (음수 = 반대쪽 조각의 몫)
                float depth = dot(IN.localPos - _CutPoint.xy, _CutNormal.xy) * _Side;
                float keep = saturate(depth / max(fwidth(depth), 1e-5) + 0.5);   // 단면을 한 픽셀 폭으로 부드럽게

                fixed4 c = tex2D(_MainTex, IN.texcoord) * IN.color;
                float edge = (1.0 - saturate(depth / max(_EdgeWidth, 1e-5))) * _EdgeGlow;
                c.rgb = lerp(c.rgb, _FlashColor.rgb, saturate(max(_Flash, edge)));

                c.a *= keep;
                c.rgb *= c.a;   // 프리멀티플라이드 알파
                return c;
            }
            ENDCG
        }
    }
}
