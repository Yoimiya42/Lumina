Shader "MaskStamp"
{
    Properties
    {
        _MainTex ("Prev Mask", 2D) = "black" {}
        _BrushUV ("Brush UV", Vector) = (0.5,0.5,0,0)
        _BrushRadius ("Brush Radius", Float) = 0.05
        _BrushHardness ("Brush Hardness", Float) = 0.8
        _BrushStrength ("Brush Strength", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Overlay" }
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            Blend Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _BrushUV;
            float _BrushRadius;
            float _BrushHardness;
            float _BrushStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float prev = tex2D(_MainTex, i.uv).r;

                float2 d = i.uv - _BrushUV.xy;
                float dist = length(d);

                float inner = _BrushRadius * _BrushHardness;
                float outer = _BrushRadius;

                float stamp = 1.0 - smoothstep(inner, outer, dist);
                stamp *= _BrushStrength;

                float outMask = saturate(prev + stamp);
                return fixed4(outMask, outMask, outMask, 1);
            }
            ENDCG
        }
    }
}
