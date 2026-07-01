Shader "Custom/BridgeClipping"
{
    Properties
    {
        _Albedo ("Albedo (RGB)", 2D) = "white" {}
        _ClipThreshold ("Clip Threshold (Local)", Float) = 0.0
        _ClipAxis ("Clip Axis", Vector) = (0,0,1,0)
        _Color ("Color Tint", Color) = (1,1,1,1)
        _InvertClip ("Invert Clip", Float) = 0.0
        
        _Primary_Color ("Primary Color", Color) = (0.5, 0.4, 0.25, 1)
        _Secondary_Color ("Secondary Color", Color) = (0.8, 0.75, 0.5, 1)
        _Tertiary_Color ("Tertiary Color", Color) = (0.3, 0.23, 0.11, 1)

        [HideInInspector] _SrcBlend ("__src", Float) = 1.0
        [HideInInspector] _DstBlend ("__dst", Float) = 0.0
        [HideInInspector] _ZWrite ("__zw", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Blend [_SrcBlend] [_DstBlend]
        ZWrite [_ZWrite]

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
            float _InvertClip;

            fixed4 _Primary_Color;
            fixed4 _Secondary_Color;
            fixed4 _Tertiary_Color;

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
                
                if (_InvertClip > 0.5)
                {
                    // Invert clip: discard the built part, render only the unbuilt part
                    if (posVal <= _ClipThreshold)
                    {
                        discard;
                    }
                }
                else
                {
                    // Standard clip: discard the unbuilt part, render only the built part
                    if (posVal > _ClipThreshold)
                    {
                        discard;
                    }
                }

                fixed4 mask = tex2D(_Albedo, i.uv);
                
                // Blend mask channels with primary, secondary, and tertiary colors
                fixed3 finalRGB = mask.r * _Primary_Color.rgb + 
                                  mask.g * _Secondary_Color.rgb + 
                                  mask.b * _Tertiary_Color.rgb;
                                  
                fixed4 col = fixed4(finalRGB, mask.a * _Color.a);
                return col;
            }
            ENDCG
        }
    }
}
