Shader "Custom/UnderwaterShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Strength ("Distortion Strength", Range(0,0.1)) = 0.02
        _Tint ("Water Tint", Color) = (0,0.4,0.7,1)
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

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
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Strength;
            fixed4 _Tint;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float wave = sin(i.uv.y * 20 + _Time.y * 2) * _Strength;

                float2 uv = i.uv;
                uv.x += wave;

                fixed4 col = tex2D(_MainTex, uv);

                col.rgb *= _Tint.rgb * 1.5;

                return col;
            }
            ENDCG
        }
    }
}