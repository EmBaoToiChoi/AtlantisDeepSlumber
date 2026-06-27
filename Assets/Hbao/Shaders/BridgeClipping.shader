Shader "Custom/BridgeClipping"
{
    Properties
    {
        _Albedo("Albedo (RGB)", 2D) = "white" {}
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
            Name "Unlit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 positionOS   : TEXCOORD1;
            };

            TEXTURE2D(_Albedo);
            SAMPLER(sampler_Albedo);

            CBUFFER_START(UnityPerMaterial)
                float4 _Albedo_ST;
                float _ClipThreshold;
                float4 _ClipAxis;
                float4 _Color;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _Albedo);
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float posVal = dot(input.positionOS.xyz, _ClipAxis.xyz);
                if (posVal > _ClipThreshold)
                {
                    discard;
                }

                half4 col = SAMPLE_TEXTURE2D(_Albedo, sampler_Albedo, input.uv) * _Color;
                return col;
            }
            ENDHLSL
        }
    }
}
