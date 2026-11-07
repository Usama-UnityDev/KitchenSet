Shader "HUsama/PresentationSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _PaintTex ("Nozzle paint mask", 2D) = "black" {}
        _Complete ("Complete", Range(0,1)) = 1
        _LocalRect ("Sprite local bounds", Vector) = (0,0,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off ZWrite Off Lighting Off ZTest Always
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            sampler2D _PaintTex;
            float _Complete;
            float4 _LocalRect;
            struct input { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct output { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; float2 local:TEXCOORD1; };
            output vert(input v)
            {
                output o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv; o.color = v.color;
                o.local = (v.vertex.xy - _LocalRect.xy) / max(_LocalRect.zw, float2(0.001,0.001));
                return o;
            }
            fixed4 frag(output i):SV_Target
            {
                fixed4 c = tex2D(_MainTex,i.uv) * i.color;
                c.a *= max(_Complete, tex2D(_PaintTex, saturate(i.local)).r);
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
