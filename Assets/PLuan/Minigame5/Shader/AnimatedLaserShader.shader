Shader "Custom/AnimatedLaserShader"
{
    Properties
    {
        [HDR] _GlowColor("Glow Color", Color) = (1, 0.3, 0.1, 1)
        _ScrollSpeed("Scroll Speed", Float) = 5.0 // Tốc độ chạy của luồng năng lượng
        _WaveFreq("Wave Frequency", Float) = 15.0 // Tần số gợn sóng điện
        _WaveAmp("Wave Amplitude", Float) = 0.03 // Biên độ gợn sóng điện (độ rung lắc)
        _CoreWidth("Core Width", Range(0.01, 0.5)) = 0.08 // Độ rộng của lõi trắng sáng ở giữa
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
        Blend One One // Cộng màu phát sáng (Additive) để tạo hiệu ứng phát sáng mạnh HDR
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
            float _WaveFreq;
            float _WaveAmp;
            float _CoreWidth;

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
                // Tạo thời gian chạy luồng năng lượng cuộn dọc theo tia laser
                float time = _Time.y * _ScrollSpeed;

                // Tạo sóng chập chùng mô phỏng tia điện huyết tương (Plasma Arc)
                float wave1 = sin(i.uv.x * _WaveFreq + time) * _WaveAmp;
                float wave2 = cos(i.uv.x * (_WaveFreq * 1.6) - time * 0.8) * (_WaveAmp * 0.6);
                float totalWave = wave1 + wave2;

                // Tính toán khoảng cách tới tâm tia laser có cộng thêm độ biến dạng của sóng điện
                float distortedDist = abs(i.uv.y - 0.5 + totalWave) * 2.0;

                // Lõi trắng sáng ở tâm tia laser (làm tia sáng trông có lực và thật hơn)
                float core = smoothstep(_CoreWidth, 0.0, distortedDist);

                // Viền phát sáng tỏa ra xung quanh nhuộm màu HDR sinh động
                float glow = pow(saturate(1.0 - distortedDist), 3.0);

                // Tổng hợp màu: lõi sáng trắng + viền màu HDR
                fixed4 col = (_GlowColor * glow) + (fixed4(1, 1, 1, 1) * core);

                // Nhân thêm với màu gradient của LineRenderer thiết lập ở Inspector
                col *= i.color;

                return col;
            }
            ENDCG
        }
    }
}
