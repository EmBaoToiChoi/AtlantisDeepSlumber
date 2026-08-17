Shader "Custom/LobbyGroundLightDisc"
{
    Properties
    {
        _Color ("Core Color", Color) = (1.0, 0.88, 0.42, 1.0)
        _Intensity ("Intensity", Float) = 0.35
        _InnerRadius ("Inner Core Radius", Range(0.0, 0.8)) = 0.05
        _OuterSoftness ("Outer Softness", Range(0.1, 1.0)) = 0.85
        _RingIntensity ("Pulsing Ring Intensity", Range(0.0, 1.0)) = 0.05
        _PulseSpeed ("Pulse Speed", Float) = 1.5
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+110"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        LOD 100
        Cull Off
        ZWrite Off
        Blend One One // Soft Additive blending for subtle floor light glow

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float2 uv    : TEXCOORD0;
                float4 color : COLOR;
            };

            fixed4 _Color;
            float _Intensity;
            float _InnerRadius;
            float _OuterSoftness;
            float _RingIntensity;
            float _PulseSpeed;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 centeredUV = i.uv - float2(0.5, 0.5);
                float dist = length(centeredUV) * 2.0;

                // Ultra smooth soft radial falloff (no harsh ring)
                float core = 1.0 - smoothstep(_InnerRadius, _InnerRadius + _OuterSoftness, dist);
                
                float pulse = sin(_Time.y * _PulseSpeed) * 0.5 + 0.5;
                float ringDist = abs(dist - (0.5 + pulse * 0.1));
                float ring = (1.0 - smoothstep(0.0, 0.2, ringDist)) * _RingIntensity;

                float combinedAlpha = saturate(core + ring) * i.color.a;
                fixed3 finalColor = _Color.rgb * _Intensity * combinedAlpha * i.color.rgb;

                return fixed4(finalColor, 1.0);
            }
            ENDCG
        }
    }
    FallBack "Mobile/Particles/Additive"
}
