Shader "Custom/AnimatedLaserShader"
{
    Properties
    {
        [HDR] _GlowColor("Base Fire Color", Color) = (1.0, 0.3, 0.0, 1)
        _ScrollSpeed("Scroll Speed", Float) = 4.0 // Tốc độ cháy cuộn của lửa
        _NoiseScale1("Flame Scale 1", Float) = 12.0 // Tỷ lệ gợn lửa thô
        _NoiseScale2("Flame Scale 2", Float) = 24.0 // Tỷ lệ tia lửa mịn
        _FlameTurbulence("Flame Turbulence", Range(0.0, 0.2)) = 0.08 // Độ hỗn loạn/vỡ hình của ngọn lửa
    }
    SubShader
    {
        Tags 
        { 
            "Queue"="Transparent" 
            "RenderType"="Transparent" 
            "IgnoreProjector"="True" 
        }
        LOD 100
        Blend SrcAlpha One // Tạo hiệu ứng phát sáng cộng dồn alpha (Additive/Screen) để lửa hòa trộn thực tế
        Cull Off
        Lighting Off
        ZWrite Off

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
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
            };

            float4 _GlowColor;
            float _ScrollSpeed;
            float _NoiseScale1;
            float _NoiseScale2;
            float _FlameTurbulence;

            // Hàm tạo mã băm ngẫu nhiên cho nhiễu hạt (Value Noise)
            float hash(float2 p) 
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            // Hàm tạo nhiễu hạt 2D mịn (Bilinear Interpolated Value Noise)
            float noise(float2 p) 
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash(i + float2(0.0, 0.0)), hash(i + float2(1.0, 0.0)), u.x),
                            lerp(hash(i + float2(0.0, 1.0)), hash(i + float2(1.0, 1.0)), u.x), u.y);
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float time = _Time.y * _ScrollSpeed;

                // 1. Tạo 2 lớp nhiễu cuộn ngược chiều/khác tốc độ để mô phỏng lửa cuộn tự nhiên
                float2 uv1 = float2(i.uv.x * _NoiseScale1 - time, i.uv.y * 3.0);
                float2 uv2 = float2(i.uv.x * _NoiseScale2 + time * 0.5, i.uv.y * 6.0 - time * 0.2);
                
                float n1 = noise(uv1);
                float n2 = noise(uv2);
                
                // Kết hợp nhiễu kép để tạo vân lửa (Flame textures)
                float combinedNoise = n1 * 0.6 + n2 * 0.4;

                // 2. Định hình biên ngọn lửa (lửa sẽ mảnh ở 2 đầu, phập phồng sinh động dọc thân)
                float edgeFade = 1.0 - abs(i.uv.y - 0.5) * 2.0;
                
                // Sử dụng nhiễu hạt để bóp méo hình dạng biên của tia sáng (Tia lửa bập bùng)
                float turbulence = (noise(float2(i.uv.x * 10.0 + time, 0.0)) - 0.5) * _FlameTurbulence;
                float distortedEdge = 1.0 - abs(i.uv.y - 0.5 + turbulence) * 2.0;

                // Giá trị năng lượng lửa tích lũy (Càng ở giữa càng mạnh, kết hợp nhiễu)
                float fireVal = combinedNoise * distortedEdge * 1.6;

                // 3. Phân phổ màu lửa (Fire Color Ramp)
                // Cực kỳ yếu -> Trong suốt
                // Yếu -> Đỏ đậm (Deep Red)
                // Trung bình -> Cam phát sáng (Vibrant Orange)
                // Mạnh -> Vàng rực (Bright Yellow)
                // Rất mạnh (Lõi lửa) -> Trắng sáng (Hot White)
                
                fixed4 red = fixed4(0.95, 0.15, 0.0, 1.0);
                fixed4 orange = fixed4(1.0, 0.55, 0.02, 1.0);
                fixed4 yellow = fixed4(1.0, 0.92, 0.35, 1.0);
                fixed4 white = fixed4(1.0, 1.0, 1.0, 1.0);

                fixed4 finalColor = fixed4(0,0,0,0);

                // Dựng dải màu lửa chuyển đổi mượt mà theo cường độ phát sáng
                if (fireVal > 0.1)
                {
                    float tRed = saturate((fireVal - 0.1) / 0.25);
                    finalColor = lerp(fixed4(0,0,0,0), red, tRed);
                }
                if (fireVal > 0.35)
                {
                    float tOrange = saturate((fireVal - 0.35) / 0.25);
                    finalColor = lerp(finalColor, orange, tOrange);
                }
                if (fireVal > 0.6)
                {
                    float tYellow = saturate((fireVal - 0.6) / 0.2);
                    finalColor = lerp(finalColor, yellow, tYellow);
                }
                if (fireVal > 0.8)
                {
                    float tWhite = saturate((fireVal - 0.8) / 0.2);
                    finalColor = lerp(finalColor, white, tWhite);
                }

                // Cường độ phát sáng cộng thêm màu HDR tùy chỉnh
                finalColor.rgb *= _GlowColor.rgb;
                
                // Độ mờ đục giảm dần ra 2 biên tia laser
                finalColor.a = saturate(fireVal * edgeFade * 1.5);

                // Nhân với màu gradient của LineRenderer ở Inspector
                finalColor *= i.color;

                return finalColor;
            }
            ENDCG
        }
    }
}
