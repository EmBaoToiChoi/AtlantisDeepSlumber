Shader "Custom/BridgeClipping"
{
    Properties
    {
        _Albedo("Albedo (RGB)", 2D) = "white" {}
        _Normal("Normal Map", 2D) = "bump" {}
        _Specular("Specular Map (RGB)", 2D) = "white" {}
        _ClipThreshold("Clip Threshold (Local)", Float) = 0.0
        _ClipAxis("Clip Axis", Vector) = (0,0,1,0)
        _Color("Color tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // Core library
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float2 uv           : TEXCOORD3;
                float3 positionOS   : TEXCOORD4;
            };

            TEXTURE2D(_Albedo);
            SAMPLER(sampler_Albedo);
            
            TEXTURE2D(_Normal);
            SAMPLER(sampler_Normal);

            TEXTURE2D(_Specular);
            SAMPLER(sampler_Specular);

            CBUFFER_START(UnityPerMaterial)
                float4 _Albedo_ST;
                float _ClipThreshold;
                float4 _ClipAxis;
                float4 _Color;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.normalWS = normalInput.normalWS;
                
                output.uv = TRANSFORM_TEX(input.uv, _Albedo);
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Clip based on the projection onto the clip axis in object space
                float posVal = dot(input.positionOS.xyz, _ClipAxis.xyz);
                if (posVal > _ClipThreshold)
                {
                    discard;
                }

                half4 albedoColor = SAMPLE_TEXTURE2D(_Albedo, sampler_Albedo, input.uv) * _Color;
                half3 normalWS = normalize(input.normalWS);

                // Simple Lambertian / Blinn-Phong lighting calculation for URP
                Light mainLight = GetMainLight();
                half3 diffuse = LightingLambert(mainLight.color, mainLight.direction, normalWS);
                
                // Add ambient lighting
                half3 ambient = SampleSH(normalWS) * albedoColor.rgb;

                half3 finalColor = (diffuse + 0.15) * albedoColor.rgb + ambient * 0.2;
                
                return half4(finalColor, albedoColor.a);
            }
            ENDHLSL
        }
    }
}
