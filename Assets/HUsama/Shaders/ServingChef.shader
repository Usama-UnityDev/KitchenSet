Shader "HUsama/ServingChef"
{
    Properties
    {
        _MainTex ("Chef texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _ClipBelowWorldY ("Waist crop", Float) = -10000
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Back ZWrite On
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_ST; fixed4 _Color; float _ClipBelowWorldY;
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; };
            struct v2f { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float3 normal:TEXCOORD1; float worldY:TEXCOORD2; };
            v2f vert(appdata v) { v2f o; o.position=UnityObjectToClipPos(v.vertex); o.uv=TRANSFORM_TEX(v.uv,_MainTex); o.normal=UnityObjectToWorldNormal(v.normal); o.worldY=mul(unity_ObjectToWorld,v.vertex).y; return o; }
            fixed4 frag(v2f i):SV_Target
            {
                clip(i.worldY-_ClipBelowWorldY);
                fixed4 color=tex2D(_MainTex,i.uv)*_Color;
                color.rgb *= 0.72 + 0.28*saturate(dot(normalize(i.normal), normalize(float3(-0.4,0.7,-0.7))));
                return color;
            }
            ENDCG
        }
    }
}
