Shader "Custom/PillarLitGlowShader"
{
    Properties
    {
        [Header(Base Map)]
        _MainTex("Base Color Map", 2D) = "white" {}
        _Color("Base Color Tint", Color) = (1, 1, 1, 1)
        
        [Header(Normal Map)]
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        
        [Header(Metallic Smoothness)]
        _Metallic("Metallic", Range(0, 1)) = 0.0
        _Glossiness("Smoothness", Range(0, 1)) = 0.5
        
        [Header(Glow Effect)]
        _CharacterMask("Character Mask (R)", 2D) = "white" {}
        [HDR] _GlowColor("Glow Color", Color) = (1, 1, 1, 1)
        _Disolve("Disolve (1=Off, 0=On)", Range(0, 1)) = 1.0
        _DisolveRemapMin("Disolve Remap Min (Bottom)", Float) = -0.45
        _DisolveRemapMax("Disolve Remap Max (Top)", Float) = 0.45
        _DisolveSmooth("Disolve Smooth", Range(0.01, 1.0)) = 0.1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        CGPROGRAM
        // Use shader model 3.0 target, enable Standard lighting
        #pragma target 3.0
        #pragma surface surf Standard

        sampler2D _MainTex;
        sampler2D _BumpMap;
        sampler2D _CharacterMask;

        fixed4 _Color;
        half _Metallic;
        half _Glossiness;

        half4 _GlowColor;
        half _Disolve;
        half _DisolveRemapMin;
        half _DisolveRemapMax;
        half _DisolveSmooth;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos; // Unity automatically populates World Space Position
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            // 1. Sample Base Color and Normal Map
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_MainTex));

            // 2. Metallic and Smoothness
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;

            // 3. Calculate Emissive Dissolve
            half mask = tex2D(_CharacterMask, IN.uv_MainTex).r;
            
            // Lerp from Min (bottom) to Max (top) as Dissolve goes from 1.0 (Off) to 0.0 (On)
            float remappedThreshold = lerp(_DisolveRemapMin, _DisolveRemapMax, 1.0 - _Disolve);
            
            // BULLETPROOF VERTICAL HEIGHT CALCULATION:
            // Find the world position of the object's pivot point
            float3 worldPivot = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
            
            // Calculate the height of this pixel relative to the pivot, aligned with World Y (always vertical!)
            float relativeHeight = IN.worldPos.y - worldPivot.y;
            
            // Smoothstep vertical height sweep (guaranteed to sweep bottom-to-top)
            float sweep = smoothstep(remappedThreshold - _DisolveSmooth, remappedThreshold + _DisolveSmooth, relativeHeight);
            
            // Active glow is below the threshold
            float glowAmount = (1.0 - sweep) * mask;
            
            // Set final emission color
            o.Emission = _GlowColor.rgb * glowAmount;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
