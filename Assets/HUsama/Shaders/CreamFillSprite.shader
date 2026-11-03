Shader "HUsama/CreamFillSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FillAmount ("Fill Amount", Range(0, 1)) = 0
        _Center ("Top Center (X, Y)", Vector) = (0.5, 0.72, 0, 0)
        _Aspect ("Top Ellipse Aspect", Float) = 2.1
        _RimY ("Top Rim Y", Float) = 0.58
        _SoftEdge ("Soft Edge Width", Range(0.001, 0.15)) = 0.04
        _WobbleScale ("Organic Wobble", Range(0, 0.05)) = 0.02
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
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
            #pragma multi_compile _ PIXELSNAP_ON
            #include "UnityCG.cginc"

            struct appdata
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
            float _FillAmount;
            float4 _Center;
            float _Aspect;
            float _RimY;
            float _SoftEdge;
            float _WobbleScale;

            v2f vert(appdata IN)
            {
                v2f OUT;
                OUT.vertex   = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color    = IN.color * _Color;
                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
                #endif
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, IN.texcoord) * IN.color;

                if (_FillAmount <= 0.001)
                {
                    return fixed4(0, 0, 0, 0);
                }

                if (_FillAmount >= 0.995)
                {
                    c.rgb *= c.a;
                    return c;
                }

                float2 uv = IN.texcoord;

                // 1. Calculate distance from top center
                float dx = uv.x - _Center.x;
                float baseDist = 0.0;

                if (uv.y >= _RimY)
                {
                    // Top Surface (Ellipse)
                    float dy = (uv.y - _Center.y) * _Aspect;
                    baseDist = sqrt(dx * dx + dy * dy);
                }
                else
                {
                    // Side Surface (Spreading downward from the rim)
                    float rimDy = (_RimY - _Center.y) * _Aspect;
                    float rimDist = sqrt(dx * dx + rimDy * rimDy);
                    float dropDist = (_RimY - uv.y) * 1.35;
                    baseDist = rimDist + dropDist;
                }

                // 2. Add subtle natural organic wobble so it doesn't look like a rigid circle
                float angle = atan2(uv.y - _Center.y, uv.x - _Center.x);
                float wobble = (sin(angle * 5.0) * 0.6 + cos(angle * 3.0) * 0.4) * _WobbleScale;
                float dist = baseDist + wobble;

                // 3. Smooth expansion threshold
                // Max distance from top center to bottom corners is ~1.25
                float maxDist = 1.25;
                float fillThreshold = _FillAmount * maxDist;
                float soft = max(0.001, _SoftEdge);

                // smoothstep: 1 when dist < fillThreshold - soft, 0 when dist > fillThreshold
                float creamAlpha = smoothstep(fillThreshold, fillThreshold - soft, dist);

                c.a   *= creamAlpha;
                c.rgb *= c.a; // premultiplied alpha

                return c;
            }
            ENDCG
        }
    }
}
