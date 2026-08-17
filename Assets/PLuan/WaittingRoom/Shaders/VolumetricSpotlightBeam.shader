Shader "Custom/VolumetricSpotlightBeam"
{
    Properties
    {
        [HDR] _Color ("Beam Color", Color) = (0.85, 0.92, 1.0, 0.4)
        _Intensity ("Intensity Multiplier", Float) = 0.32
        _TopFade ("Top Fade Length", Range(0.0, 0.5)) = 0.15
        _BottomFade ("Bottom Fade Length", Range(0.0, 0.6)) = 0.35
        _RimPower ("Rim Softness Power", Range(0.5, 5.0)) = 1.5
        _CoreGlow ("Core Minimum Glow", Range(0.0, 0.5)) = 0.08
        _RimWeight ("Rim Edge Glow Weight", Range(0.0, 1.0)) = 0.45
        _NoiseSpeed ("Noise Scroll Speed", Float) = 0.25
        _NoiseTiling ("Noise Tiling", Vector) = (2.0, 3.0, 0, 0)
        _NoiseStrength ("Noise Distortion Strength", Range(0.0, 0.5)) = 0.10
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+100"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        LOD 100
        Cull Front // CULL FRONT: The front wall is never rendered in front of the character! Only renders as a background halo!
        ZWrite Off
        Blend One One // Pure Additive volumetric light blending

        Pass
        {
            Name "VolumetricBeamURP"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 viewDirWS  : TEXCOORD2;
                float2 uv         : TEXCOORD3;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Intensity;
                float _TopFade;
                float _BottomFade;
                float _RimPower;
                float _CoreGlow;
                float _RimWeight;
                float _NoiseSpeed;
                float4 _NoiseTiling;
                float _NoiseStrength;
            CBUFFER_END

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            float SmoothNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = Hash(i + float2(0.0, 0.0));
                float b = Hash(i + float2(1.0, 0.0));
                float c = Hash(i + float2(0.0, 1.0));
                float d = Hash(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = normalize(GetCameraPositionWS() - output.positionWS);
                output.uv = input.uv;
                output.color = input.color;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float v = input.uv.y; // 0 at top, 1 at bottom

                // 1. Smooth vertical fade at top and bottom
                float topFade = smoothstep(0.0, max(0.001, _TopFade), v);
                float bottomFade = 1.0 - smoothstep(1.0 - max(0.001, _BottomFade), 1.0, v);
                float vertMask = topFade * bottomFade;

                // 2. Fresnel rim edge falloff for soft backdrop aura
                float3 normal = normalize(input.normalWS);
                float3 viewDir = normalize(input.viewDirWS);
                float NdotV = abs(dot(normal, viewDir));
                float rim = pow(saturate(1.0 - NdotV), _RimPower);
                
                float volumeShape = _CoreGlow + rim * _RimWeight;

                // 3. Subtle animated light shimmer rays
                float2 noiseUV = input.uv * _NoiseTiling.xy + float2(0.0, -_Time.y * _NoiseSpeed);
                float noise = SmoothNoise(noiseUV) * 0.6 + SmoothNoise(noiseUV * 2.0 + float2(_Time.y * 0.2, 0.0)) * 0.4;
                float shimmer = 1.0 + (noise - 0.5) * _NoiseStrength;

                // 4. Subtle, ethereal additive background
                float alpha = vertMask * volumeShape * shimmer * input.color.a;
                half3 finalColor = _Color.rgb * _Intensity * alpha * input.color.rgb;

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }

    // Fallback SubShader
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+100"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        LOD 100
        Cull Front
        ZWrite Off
        Blend One One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 normal   : TEXCOORD1;
                float2 uv       : TEXCOORD2;
                float4 color    : COLOR;
            };

            fixed4 _Color;
            float _Intensity;
            float _TopFade;
            float _BottomFade;
            float _RimPower;
            float _CoreGlow;
            float _RimWeight;
            float _NoiseSpeed;
            float4 _NoiseTiling;
            float _NoiseStrength;

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            float SmoothNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = Hash(i + float2(0.0, 0.0));
                float b = Hash(i + float2(1.0, 0.0));
                float c = Hash(i + float2(0.0, 1.0));
                float d = Hash(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float v = i.uv.y;
                float topFade = smoothstep(0.0, max(0.001, _TopFade), v);
                float bottomFade = 1.0 - smoothstep(1.0 - max(0.001, _BottomFade), 1.0, v);
                float vertMask = topFade * bottomFade;

                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float NdotV = abs(dot(normalize(i.normal), viewDir));
                float rim = pow(saturate(1.0 - NdotV), _RimPower);
                float volumeShape = _CoreGlow + rim * _RimWeight;

                float2 noiseUV = i.uv * _NoiseTiling.xy + float2(0.0, -_Time.y * _NoiseSpeed);
                float noise = SmoothNoise(noiseUV);
                float shimmer = 1.0 + (noise - 0.5) * _NoiseStrength;

                float alpha = vertMask * volumeShape * shimmer * i.color.a;
                fixed3 finalColor = _Color.rgb * _Intensity * alpha * i.color.rgb;

                return fixed4(finalColor, 1.0);
            }
            ENDCG
        }
    }
    FallBack "Transparent/Cutout/VertexLit"
}
