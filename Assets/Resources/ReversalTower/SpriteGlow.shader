// 패링 신호용 발광 셰이더: 스프라이트의 모양(알파)만 쓰고 색은 SpriteRenderer.color로 칠해 더하기(가산)로 그린다.
// 같은 그림을 여러 장 조금씩 어긋나게 겹치면 테두리 빛, 몸 위에 한 장 얹으면 몸이 빛나 보인다.
Shader "ReversalTower/SpriteGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha One
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            v2f vert (appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.color = v.color; o.uv = v.uv; return o; }
            fixed4 frag (v2f i) : SV_Target { return fixed4(i.color.rgb, i.color.a * tex2D(_MainTex, i.uv).a); }
            ENDCG
        }
    }
}
