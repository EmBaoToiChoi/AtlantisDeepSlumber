Shader "Custom/SpriteFillFromBottom"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FillAmount ("Fill Amount", Range(0, 1)) = 0.0
        _EmptyColor ("Empty Tint (Grayscale)", Color) = (0.3, 0.3, 0.3, 0.5)
    }
    SubShader
    {
        Tags
        { 
            "Queue"="Transparent" 
            "IgnoreProjector"="True" 
            "RenderType"="Transparent" 
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha // Premultiplied alpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _FillAmount;
            fixed4 _EmptyColor;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // Đọc màu gốc của Sprite
                fixed4 c = tex2D(_MainTex, IN.texcoord) * IN.color;
                
                // Trục Y của texcoord chạy từ 0 (dưới) lên 1 (trên)
                // Nếu điểm ảnh hiện tại nằm CAO HƠN _FillAmount (tức là phần chưa nạp tới)
                if (IN.texcoord.y > _FillAmount)
                {
                    // Chuyển sang màu xám (grayscale)
                    float gray = dot(c.rgb, float3(0.299, 0.587, 0.114));
                    c.rgb = float3(gray, gray, gray) * _EmptyColor.rgb;
                    
                    // Giảm độ mờ một chút
                    c.a *= _EmptyColor.a;
                }
                
                c.rgb *= c.a; // Trộn Alpha cho Sprite (Premultiplied)
                return c;
            }
            ENDCG
        }
    }
}
