Shader "Custom/BridgeClipping"
{
    Properties
    {
        _Albedo ("Albedo (RGB)", 2D) = "white" {}
        _ClipThreshold ("Clip Threshold (Local)", Float) = 0.0
        _ClipAxis ("Clip Axis", Vector) = (0,0,1,0)
        _Color ("Color Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                float4 vertex : SV_POSITION;
            };

            sampler2D _Albedo;
            float4 _Albedo_ST;
            float _ClipThreshold;
            float4 _ClipAxis;
            float4 _Color;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _Albedo);
                o.positionOS = v.vertex.xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float posVal = dot(i.positionOS.xyz, _ClipAxis.xyz);
                if (posVal > _ClipThreshold)
                {
                    discard;
                }

                fixed4 col = tex2D(_Albedo, i.uv) * _Color;
                return col;
            }
            ENDCG
        }
    }
}
