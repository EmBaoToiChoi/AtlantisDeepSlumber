Shader "Custom/BuiltIn_FireOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color (HDR)", Color) = (1, 0.3, 0, 1)
        _FireTex ("Fire Noise Texture", 2D) = "white" {}
        _OutlineWidth ("Outline Width", Range(0.001, 0.1)) = 0.03
        _ScrollSpeed ("Scroll Speed (X, Y)", Vector) = (0, -1, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+1" }

        Pass
        {
            Name "OUTLINE"
            Cull Front       // Chỉ vẽ mặt sau (Inverted Hull)
            ZWrite Off       // Không đè Depth của nhân vật
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            fixed4 _OutlineColor;
            sampler2D _FireTex;
            float4 _FireTex_ST;
            float _OutlineWidth;
            float4 _ScrollSpeed;

            v2f vert (appdata v)
            {
                v2f o;
                // Phồng đỉnh mô hình ra ngoài theo hướng Normal
                float3 norm = normalize(v.normal);
                v.vertex.xyz += norm * _OutlineWidth;
                
                o.pos = UnityObjectToClipPos(v.vertex);
                
                // Tính toán UV cuộn theo thời gian
                o.uv = TRANSFORM_TEX(v.uv, _FireTex) + _Time.y * _ScrollSpeed.xy;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Lấy màu từ Noise Texture và nhân với màu Outline
                fixed4 noise = tex2D(_FireTex, i.uv);
                fixed4 col = _OutlineColor * noise.r;
                return col;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}