Shader "Custom/MeshFillDirectional"
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
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model
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

        void vert (inout appdata_full v, out Input o) {
            UNITY_INITIALIZE_OUTPUT(Input,o);
            o.localPos = v.vertex.xyz;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Lấy màu gốc từ Texture và Color
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            
            // Chọn trục để làm chuẩn nạp năng lượng (Mặc định là trục Y = 1)
            float pos = IN.localPos.y; 
            if (_FillAxis == 0) pos = IN.localPos.x;
            else if (_FillAxis == 2) pos = IN.localPos.z;
            else if (_FillAxis == 3) pos = IN.worldPos.y;

            // Tính toán tỷ lệ phần trăm (0 = Bottom, 1 = Top)
            float normalizedPos = (pos - _MinBound) / (_MaxBound - _MinBound);

            // Nếu vị trí này nằm trên vạch năng lượng hiện tại -> Đổi thành màu xám
            if (normalizedPos > _FillAmount)
            {
                float gray = dot(c.rgb, float3(0.299, 0.587, 0.114)); // Công thức chuyển xám chuẩn
                c.rgb = float3(gray, gray, gray) * _EmptyColor.rgb;
            }

            o.Albedo = c.rgb;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
