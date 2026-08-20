Shader "Custom/LightningGlow"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Full Charge Color", Color) = (1,1,1,1)
        _EmptyColor ("Empty Color (Grayscale)", Color) = (0.3, 0.3, 0.3, 1)
        _FillAmount ("Fill Amount", Range(0,1)) = 0.0
        
        [Header(Fill Settings)]
        _FillAxis ("Fill Axis (0=X, 1=Y, 2=Z)", Int) = 1
        _MinBound ("Bottom Bound (Local Space)", Float) = -0.5
        _MaxBound ("Top Bound (Local Space)", Float) = 0.5
        
        [Header(Glow and Outline Settings)]
        [HDR] _GlowColor ("Fill Glow Color (Phần đã nạp)", Color) = (2, 1.5, 0, 1)
        [HDR] _EdgeColor ("Edge Glow Color (Viền mép)", Color) = (2.5, 2, 0, 1)
        _EdgeWidth ("Edge Width", Range(0, 0.5)) = 0.05
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
            float3 localPos;
            float3 worldPos;
        };

        fixed4 _Color;
        fixed4 _EmptyColor;
        float _FillAmount;
        int _FillAxis;
        float _MinBound;
        float _MaxBound;
        
        fixed4 _GlowColor;
        fixed4 _EdgeColor;
        float _EdgeWidth;

        void vert (inout appdata_full v, out Input o) {
            UNITY_INITIALIZE_OUTPUT(Input,o);
            o.localPos = v.vertex.xyz;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            
            float pos = IN.localPos.y; 
            if (_FillAxis == 0) pos = IN.localPos.x;
            else if (_FillAxis == 2) pos = IN.localPos.z;
            else if (_FillAxis == 3) pos = IN.worldPos.y;

            float normalizedPos = (pos - _MinBound) / (_MaxBound - _MinBound);

            if (normalizedPos > _FillAmount)
            {
                // Phần chưa nạp: Màu xám
                float gray = dot(c.rgb, float3(0.299, 0.587, 0.114));
                o.Albedo = float3(gray, gray, gray) * _EmptyColor.rgb;
                o.Emission = float3(0,0,0);
            }
            else
            {
                // Phần đã nạp: Phát sáng toàn bộ (Vàng đậm)
                o.Albedo = c.rgb;
                o.Emission = c.rgb * _GlowColor.rgb;
            }

            // Viền vạch chạy (Edge Glow) nằm sát vạch nạp
            if (_FillAmount > 0.0 && _FillAmount < 1.0)
            {
                if (normalizedPos <= _FillAmount && normalizedPos > _FillAmount - _EdgeWidth)
                {
                    float edgeIntensity = (normalizedPos - (_FillAmount - _EdgeWidth)) / _EdgeWidth;
                    // Cộng dồn vạch viền sáng rực vào
                    o.Emission += _EdgeColor.rgb * edgeIntensity;
                }
            }

            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
