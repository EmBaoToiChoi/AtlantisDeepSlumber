// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "SH_Vefects_VFX_Dissolve"
{
	Properties
	{
		_Noise_01_Texture("Noise_01_Texture", 2D) = "white" {}
		_Noise_02_Texture("Noise_02_Texture", 2D) = "white" {}
		_Mask_Texture("Mask_Texture", 2D) = "white" {}
		_MaskMove_Texture("MaskMove_Texture", 2D) = "white" {}
		_Color("Color", Color) = (1,1,1,0)
		_NoiseDistortion_Texture("NoiseDistortion_Texture", 2D) = "white" {}
		_Noise_01_Scale("Noise_01_Scale", Vector) = (0.8,0.8,0,0)
		_Noise_02_Scale("Noise_02_Scale", Vector) = (1,1,0,0)
		_CamOffSet("CamOffSet", Float) = 0
		_NoiseDistortion_Scale("NoiseDistortion_Scale", Vector) = (1,1,0,0)
		_Noise_01_Speed("Noise_01_Speed", Vector) = (0.5,0.5,0,0)
		_Noise_02_Speed("Noise_02_Speed", Vector) = (-0.2,0.4,0,0)
		_MaskMove_Scale("MaskMove_Scale", Vector) = (1,1,0,0)
		_Mask_Scale("Mask_Scale", Vector) = (1,1,0,0)
		_Mask_Offset("Mask_Offset", Vector) = (0,0,0,0)
		_NoiseDistortion_Speed("NoiseDistortion_Speed", Vector) = (0.2,0.25,0,0)
		_Mask_Multiply("Mask_Multiply", Float) = 1
		_MaskMove_Multiply("MaskMove_Multiply", Float) = 1
		_Noises_Multiply("Noises_Multiply", Float) = 1
		_Mask_Power("Mask_Power", Float) = 1
		_Noises_Power("Noises_Power", Float) = 1
		_MaskMove_Power("MaskMove_Power", Float) = 1
		_Distortion("Distortion", Float) = 1
		_DistortionMask("DistortionMask", Float) = 0
		_Opacity_Boost("Opacity_Boost", Float) = 5
		_Emissive("Emissive", Float) = 1
		_Global_Speed("Global_Speed", Float) = 1
		_Mask_Speed("Mask_Speed", Float) = 0
		_Dissolve("Dissolve", Float) = 0
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] _texcoord2( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Transparent"  "Queue" = "Transparent+0" "IgnoreProjector" = "True" "IsEmissive" = "true"  }
		Cull Back
		CGINCLUDE
		#include "UnityShaderVariables.cginc"
		#include "UnityPBSLighting.cginc"
		#include "Lighting.cginc"
		#pragma target 3.0
		#undef TRANSFORM_TEX
		#define TRANSFORM_TEX(tex,name) float4(tex.xy * name##_ST.xy + name##_ST.zw, tex.z, tex.w)
		struct Input
		{
			float3 worldPos;
			float4 vertexColor : COLOR;
			float2 uv_texcoord;
			float4 uv2_texcoord2;
		};

		uniform float _CamOffSet;
		uniform float4 _Color;
		uniform sampler2D _Mask_Texture;
		uniform sampler2D _NoiseDistortion_Texture;
		uniform float _Global_Speed;
		uniform float2 _NoiseDistortion_Speed;
		uniform float2 _NoiseDistortion_Scale;
		uniform float _Distortion;
		uniform float _DistortionMask;
		uniform float _Mask_Speed;
		uniform float2 _Mask_Scale;
		uniform float2 _Mask_Offset;
		uniform float _Mask_Power;
		uniform float _Mask_Multiply;
		uniform sampler2D _MaskMove_Texture;
		uniform float2 _MaskMove_Scale;
		uniform float _MaskMove_Power;
		uniform float _MaskMove_Multiply;
		uniform sampler2D _Noise_01_Texture;
		uniform float2 _Noise_01_Speed;
		uniform float2 _Noise_01_Scale;
		uniform sampler2D _Noise_02_Texture;
		uniform float2 _Noise_02_Speed;
		uniform float2 _Noise_02_Scale;
		uniform float _Noises_Power;
		uniform float _Noises_Multiply;
		uniform float _Emissive;
		uniform float _Opacity_Boost;
		uniform float _Dissolve;

		void vertexDataFunc( inout appdata_full v, out Input o )
		{
			UNITY_INITIALIZE_OUTPUT( Input, o );
			float3 ase_worldPos = mul( unity_ObjectToWorld, v.vertex );
			v.vertex.xyz += ( ( ase_worldPos - _WorldSpaceCameraPos ) * ( ( _CamOffSet + v.texcoord3.xy.y ) * 0.01 ) );
			v.vertex.w = 1;
		}

		inline half4 LightingUnlit( SurfaceOutput s, half3 lightDir, half atten )
		{
			return half4 ( 0, 0, 0, s.Alpha );
		}

		void surf( Input i , inout SurfaceOutput o )
		{
			float global_speed200 = ( _Global_Speed * _Time.y );
			float2 uv_TexCoord30 = i.uv_texcoord * _NoiseDistortion_Scale;
			float2 panner79 = ( global_speed200 * _NoiseDistortion_Speed + uv_TexCoord30);
			float Distortion64 = ( ( tex2D( _NoiseDistortion_Texture, panner79 ).r * 0.1 ) * _Distortion );
			float2 appendResult264 = (float2(_Mask_Speed , 0.0));
			float2 uv_TexCoord216 = i.uv_texcoord * _Mask_Scale + _Mask_Offset;
			float2 panner266 = ( global_speed200 * appendResult264 + uv_TexCoord216);
			float2 uv_TexCoord212 = i.uv_texcoord * _MaskMove_Scale;
			float2 appendResult226 = (float2(i.uv2_texcoord2.z , i.uv2_texcoord2.w));
			float clampResult224 = clamp( ( saturate( ( ( tex2D( _Mask_Texture, ( ( Distortion64 * _DistortionMask ) + panner266 ) ).r * _Mask_Power ) * _Mask_Multiply ) ) * saturate( ( ( tex2D( _MaskMove_Texture, ( uv_TexCoord212 + appendResult226 ) ).r * _MaskMove_Power ) * _MaskMove_Multiply ) ) ) , 0.0 , 1.0 );
			float2 uv_TexCoord26 = i.uv_texcoord * _Noise_01_Scale;
			float2 panner78 = ( global_speed200 * _Noise_01_Speed + uv_TexCoord26);
			float2 uv_TexCoord58 = i.uv_texcoord * _Noise_02_Scale;
			float2 panner80 = ( global_speed200 * _Noise_02_Speed + uv_TexCoord58);
			float noise205 = saturate( ( ( ( tex2D( _Noise_01_Texture, ( Distortion64 + panner78 ) ).r * tex2D( _Noise_02_Texture, ( Distortion64 + panner80 ) ).r ) * _Noises_Power ) * _Noises_Multiply ) );
			float temp_output_207_0 = ( clampResult224 * noise205 );
			o.Emission = ( ( ( (_Color).rgb * (i.vertexColor).rgb ) * temp_output_207_0 ) * _Emissive );
			float temp_output_237_0 = ( saturate( ( temp_output_207_0 * _Opacity_Boost ) ) - ( i.uv2_texcoord2.x + _Dissolve ) );
			float clampResult238 = clamp( temp_output_237_0 , 0.0 , 1.0 );
			o.Alpha = ( i.vertexColor.a * clampResult238 );
		}

		ENDCG
		CGPROGRAM
		#pragma surface surf Unlit alpha:fade keepalpha fullforwardshadows vertex:vertexDataFunc 

		ENDCG
		Pass
		{
			Name "ShadowCaster"
			Tags{ "LightMode" = "ShadowCaster" }
			ZWrite On
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0
			#pragma multi_compile_shadowcaster
			#pragma multi_compile UNITY_PASS_SHADOWCASTER
			#pragma skip_variants FOG_LINEAR FOG_EXP FOG_EXP2
			#include "HLSLSupport.cginc"
			#if ( SHADER_API_D3D11 || SHADER_API_GLCORE || SHADER_API_GLES || SHADER_API_GLES3 || SHADER_API_METAL || SHADER_API_VULKAN )
				#define CAN_SKIP_VPOS
			#endif
			#include "UnityCG.cginc"
			#include "Lighting.cginc"
			#include "UnityPBSLighting.cginc"
			sampler3D _DitherMaskLOD;
			struct v2f
			{
				V2F_SHADOW_CASTER;
				float2 customPack1 : TEXCOORD1;
				float4 customPack2 : TEXCOORD2;
				float3 worldPos : TEXCOORD3;
				half4 color : COLOR0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};
			v2f vert( appdata_full v )
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID( v );
				UNITY_INITIALIZE_OUTPUT( v2f, o );
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( o );
				UNITY_TRANSFER_INSTANCE_ID( v, o );
				Input customInputData;
				vertexDataFunc( v, customInputData );
				float3 worldPos = mul( unity_ObjectToWorld, v.vertex ).xyz;
				half3 worldNormal = UnityObjectToWorldNormal( v.normal );
				o.customPack1.xy = customInputData.uv_texcoord;
				o.customPack1.xy = v.texcoord;
				o.customPack2.xyzw = customInputData.uv2_texcoord2;
				o.customPack2.xyzw = v.texcoord1;
				o.worldPos = worldPos;
				TRANSFER_SHADOW_CASTER_NORMALOFFSET( o )
				o.color = v.color;
				return o;
			}
			half4 frag( v2f IN
			#if !defined( CAN_SKIP_VPOS )
			, UNITY_VPOS_TYPE vpos : VPOS
			#endif
			) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID( IN );
				Input surfIN;
				UNITY_INITIALIZE_OUTPUT( Input, surfIN );
				surfIN.uv_texcoord = IN.customPack1.xy;
				surfIN.uv2_texcoord2 = IN.customPack2.xyzw;
				float3 worldPos = IN.worldPos;
				half3 worldViewDir = normalize( UnityWorldSpaceViewDir( worldPos ) );
				surfIN.worldPos = worldPos;
				surfIN.vertexColor = IN.color;
				SurfaceOutput o;
				UNITY_INITIALIZE_OUTPUT( SurfaceOutput, o )
				surf( surfIN, o );
				#if defined( CAN_SKIP_VPOS )
				float2 vpos = IN.pos;
				#endif
				half alphaRef = tex3D( _DitherMaskLOD, float3( vpos.xy * 0.25, o.Alpha * 0.9375 ) ).a;
				clip( alphaRef - 0.01 );
				SHADOW_CASTER_FRAGMENT( IN )
			}
			ENDCG
		}
	}
	Fallback "Diffuse"
	CustomEditor "ASEMaterialInspector"
}
/*ASEBEGIN
Version=18921
235;651;2546;1906;2512.6;356.4334;2.691143;True;True
Node;AmplifyShaderEditor.SimpleTimeNode;198;-6111.567,537.3937;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;197;-6130.209,435.6946;Inherit;False;Property;_Global_Speed;Global_Speed;27;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;199;-5951.567,450.3937;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;77;-3869.485,-1140.933;Inherit;False;2184.752;611.1584;DISTORTION;11;64;32;31;30;45;44;50;43;52;79;204;;0.4669811,0.9251378,1,1;0;0
Node;AmplifyShaderEditor.Vector2Node;31;-3832.676,-1041.406;Inherit;False;Property;_NoiseDistortion_Scale;NoiseDistortion_Scale;9;0;Create;True;0;0;0;False;0;False;1,1;1,1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.RegisterLocalVarNode;200;-5782.903,437.2048;Inherit;False;global_speed;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;30;-3578.215,-1062.098;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.GetLocalVarNode;204;-3557.685,-781.188;Inherit;False;200;global_speed;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;32;-3610.241,-905.1642;Inherit;False;Property;_NoiseDistortion_Speed;NoiseDistortion_Speed;15;0;Create;True;0;0;0;False;0;False;0.2,0.25;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.PannerNode;79;-3325.074,-1061.148;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SamplerNode;43;-3139.226,-1090.933;Inherit;True;Property;_NoiseDistortion_Texture;NoiseDistortion_Texture;5;0;Create;True;0;0;0;False;0;False;-1;02b0d0a96eedb3d4fa5664350b35ba23;02b0d0a96eedb3d4fa5664350b35ba23;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;45;-2850.273,-931.5964;Inherit;False;Constant;_Float0;Float 0;8;0;Create;True;0;0;0;False;0;False;0.1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;44;-2707.083,-1058.478;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0.1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;52;-2649.515,-923.1813;Inherit;False;Property;_Distortion;Distortion;22;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;210;-7019.084,-1172.157;Inherit;False;2458.886;1220.625;NOISE;23;205;241;245;243;244;62;242;55;24;54;61;78;71;80;69;202;60;26;29;203;58;25;59;;0.8847253,1,0.2783019,1;0;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;50;-2474.515,-1059.181;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;59;-6800.541,-387.4911;Inherit;False;Property;_Noise_02_Scale;Noise_02_Scale;7;0;Create;True;0;0;0;False;0;False;1,1;1,1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.Vector2Node;25;-6957.598,-1025.96;Inherit;False;Property;_Noise_01_Scale;Noise_01_Scale;6;0;Create;True;0;0;0;False;0;False;0.8,0.8;1,1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.RangedFloatNode;265;-4208.769,-108.6733;Inherit;False;Property;_Mask_Speed;Mask_Speed;28;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;58;-6585.835,-406.4763;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RegisterLocalVarNode;64;-2230.502,-1065.448;Inherit;False;Distortion;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;217;-4469.387,-323.4828;Inherit;False;Property;_Mask_Scale;Mask_Scale;13;0;Create;True;0;0;0;False;0;False;1,1;1,1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.Vector2Node;219;-4452.282,-146.738;Inherit;False;Property;_Mask_Offset;Mask_Offset;14;0;Create;True;0;0;0;False;0;False;0,0;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.Vector2Node;29;-6729.519,-856.5219;Inherit;False;Property;_Noise_01_Speed;Noise_01_Speed;10;0;Create;True;0;0;0;False;0;False;0.5,0.5;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.TextureCoordinatesNode;26;-6747.494,-1044.93;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector2Node;60;-6555.771,-228.1528;Inherit;False;Property;_Noise_02_Speed;Noise_02_Speed;11;0;Create;True;0;0;0;False;0;False;-0.2,0.4;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.GetLocalVarNode;202;-6500.976,-61.63034;Inherit;False;200;global_speed;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;203;-6726.481,-676.5518;Inherit;False;200;global_speed;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;272;-3911.988,-494.2871;Inherit;False;64;Distortion;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;80;-6327.127,-402.6078;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;71;-6310.012,-493.8325;Inherit;False;64;Distortion;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;216;-4236.343,-256.0999;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0.28,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;273;-3948.915,-366.2329;Inherit;False;Property;_DistortionMask;DistortionMask;23;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;267;-4065.758,49.61431;Inherit;False;200;global_speed;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.TexCoordVertexDataNode;213;-3928.677,637.9155;Inherit;False;1;4;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.GetLocalVarNode;69;-6213.771,-1119.859;Inherit;False;64;Distortion;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;264;-4048.342,-74.76745;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.PannerNode;78;-6497.082,-997.5993;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector2Node;239;-4160.831,288.9683;Inherit;False;Property;_MaskMove_Scale;MaskMove_Scale;12;0;Create;True;0;0;0;False;0;False;1,1;1,1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.PannerNode;266;-3845.475,-256.6866;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;226;-3702.881,702.9133;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;274;-3697.21,-409.3309;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;61;-6121.987,-431.0781;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;54;-6014.026,-1024.589;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;212;-3912.376,276.5662;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;55;-5895.251,-463.06;Inherit;True;Property;_Noise_02_Texture;Noise_02_Texture;1;0;Create;True;0;0;0;False;0;False;-1;2f822f6dfbe26f44eadb94a1b925409d;2f822f6dfbe26f44eadb94a1b925409d;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;24;-5859.776,-855.4482;Inherit;True;Property;_Noise_01_Texture;Noise_01_Texture;0;0;Create;True;0;0;0;False;0;False;-1;301564cad965bbb4da5072b5cf9406cb;301564cad965bbb4da5072b5cf9406cb;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleAddOpNode;211;-3592.357,289.645;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;271;-3583.594,-266.1806;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;229;-3240.023,516.3466;Inherit;False;Property;_MaskMove_Power;MaskMove_Power;21;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;62;-5524.486,-632.6779;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;222;-3195.25,-45.43172;Inherit;False;Property;_Mask_Power;Mask_Power;19;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;242;-5509.104,-413.9599;Inherit;False;Property;_Noises_Power;Noises_Power;20;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;214;-3385.094,-280.1219;Inherit;True;Property;_Mask_Texture;Mask_Texture;2;0;Create;True;0;0;0;False;0;False;-1;None;a51bf6d25586253479546e919004de48;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;206;-3439.438,259.8869;Inherit;True;Property;_MaskMove_Texture;MaskMove_Texture;3;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;243;-5293.606,-405.6702;Inherit;False;Property;_Noises_Multiply;Noises_Multiply;18;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;220;-3011.609,-250.3817;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;230;-3035.382,294.2706;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;221;-3000.753,-39.14202;Inherit;False;Property;_Mask_Multiply;Mask_Multiply;16;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;244;-5304.462,-636.0359;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;228;-3024.526,524.6363;Inherit;False;Property;_MaskMove_Multiply;MaskMove_Multiply;17;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;231;-2853.832,294.1263;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;223;-2830.059,-250.526;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;245;-5122.913,-636.1801;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;232;-2680.074,296.4127;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;241;-4949.154,-633.8939;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;227;-2604.237,-246.7737;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;205;-4798.302,-640.9857;Inherit;False;noise;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;215;-2381.488,279.5225;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;208;-2108.049,446.9693;Inherit;False;205;noise;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;224;-2168.45,282.1025;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;207;-1894.318,283.0367;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode;188;-1704.792,820.649;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;76;-1755.961,958.0763;Inherit;False;Property;_Opacity_Boost;Opacity_Boost;24;0;Create;True;0;0;0;False;0;False;5;2;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;75;-1510.446,863.6877;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;270;-1274.441,1260.165;Inherit;False;Property;_Dissolve;Dissolve;29;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.VertexColorNode;8;-562.7887,-153.1591;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ColorNode;260;-454.0623,-391.2823;Inherit;False;Property;_Color;Color;4;0;Create;True;0;0;0;False;0;False;1,1,1,0;1,1,1,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TexCoordVertexDataNode;134;-1339.318,1065.572;Inherit;False;1;4;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TextureCoordinatesNode;251;317.3975,1718.399;Inherit;False;3;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ComponentMaskNode;23;-261.2951,-154.1163;Inherit;False;True;True;True;False;1;0;COLOR;0,0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;250;311.2366,1557.733;Inherit;False;Property;_CamOffSet;CamOffSet;8;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ComponentMaskNode;259;-203.463,-390.2491;Inherit;False;True;True;True;False;1;0;COLOR;0,0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SaturateNode;262;-1226.629,871.7328;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;269;-1113.632,1087.995;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode;258;-1613.096,114.1061;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.WorldPosInputsNode;254;305.0638,1090.092;Inherit;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.SimpleSubtractOpNode;237;-919.7125,866.9775;Inherit;True;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;261;11.75638,-165.3471;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.WorldSpaceCameraPos;253;276.3985,1307.398;Inherit;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.SimpleAddOpNode;252;585.3965,1626.399;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode;139;-440.2977,227.0215;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;255;780.2356,1630.733;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0.01;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;10;190.8025,-7.698912;Inherit;True;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;240;369.9243,297.217;Inherit;False;Property;_Emissive;Emissive;26;0;Create;True;0;0;0;False;0;False;1;70;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;256;571.8704,1218.148;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.ClampOpNode;238;400.7935,870.4963;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;12;828.4266,239.2052;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;257;902.3965,1213.398;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;236;554.3409,135.7876;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.ClampOpNode;136;-3.36903,1099.656;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;135;155.3503,879.2562;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DepthFade;137;-274.8853,1105.719;Inherit;False;True;False;True;2;1;FLOAT3;0,0,0;False;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;138;-537.6568,1133.016;Inherit;False;Property;_Opacity_DepthDistance;Opacity_DepthDistance;25;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;290;1239.138,72.99976;Float;False;True;-1;2;ASEMaterialInspector;0;0;Unlit;SH_Vefects_VFX_Dissolve;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;False;False;False;False;False;False;Back;0;False;-1;0;False;-1;False;0;False;-1;0;False;-1;False;0;Transparent;0.5;True;True;0;False;Transparent;;Transparent;All;18;all;True;True;True;True;0;False;-1;False;0;False;-1;255;False;-1;255;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;False;2;15;10;25;False;0.5;True;2;5;False;-1;10;False;-1;0;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;0;-1;-1;-1;0;False;0;0;False;-1;-1;0;False;-1;0;0;0;False;0.1;False;-1;0;False;-1;False;15;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;199;0;197;0
WireConnection;199;1;198;0
WireConnection;200;0;199;0
WireConnection;30;0;31;0
WireConnection;79;0;30;0
WireConnection;79;2;32;0
WireConnection;79;1;204;0
WireConnection;43;1;79;0
WireConnection;44;0;43;1
WireConnection;44;1;45;0
WireConnection;50;0;44;0
WireConnection;50;1;52;0
WireConnection;58;0;59;0
WireConnection;64;0;50;0
WireConnection;26;0;25;0
WireConnection;80;0;58;0
WireConnection;80;2;60;0
WireConnection;80;1;202;0
WireConnection;216;0;217;0
WireConnection;216;1;219;0
WireConnection;264;0;265;0
WireConnection;78;0;26;0
WireConnection;78;2;29;0
WireConnection;78;1;203;0
WireConnection;266;0;216;0
WireConnection;266;2;264;0
WireConnection;266;1;267;0
WireConnection;226;0;213;3
WireConnection;226;1;213;4
WireConnection;274;0;272;0
WireConnection;274;1;273;0
WireConnection;61;0;71;0
WireConnection;61;1;80;0
WireConnection;54;0;69;0
WireConnection;54;1;78;0
WireConnection;212;0;239;0
WireConnection;55;1;61;0
WireConnection;24;1;54;0
WireConnection;211;0;212;0
WireConnection;211;1;226;0
WireConnection;271;0;274;0
WireConnection;271;1;266;0
WireConnection;62;0;24;1
WireConnection;62;1;55;1
WireConnection;214;1;271;0
WireConnection;206;1;211;0
WireConnection;220;0;214;1
WireConnection;220;1;222;0
WireConnection;230;0;206;1
WireConnection;230;1;229;0
WireConnection;244;0;62;0
WireConnection;244;1;242;0
WireConnection;231;0;230;0
WireConnection;231;1;228;0
WireConnection;223;0;220;0
WireConnection;223;1;221;0
WireConnection;245;0;244;0
WireConnection;245;1;243;0
WireConnection;232;0;231;0
WireConnection;241;0;245;0
WireConnection;227;0;223;0
WireConnection;205;0;241;0
WireConnection;215;0;227;0
WireConnection;215;1;232;0
WireConnection;224;0;215;0
WireConnection;207;0;224;0
WireConnection;207;1;208;0
WireConnection;188;0;207;0
WireConnection;75;0;188;0
WireConnection;75;1;76;0
WireConnection;23;0;8;0
WireConnection;259;0;260;0
WireConnection;262;0;75;0
WireConnection;269;0;134;1
WireConnection;269;1;270;0
WireConnection;258;0;207;0
WireConnection;237;0;262;0
WireConnection;237;1;269;0
WireConnection;261;0;259;0
WireConnection;261;1;23;0
WireConnection;252;0;250;0
WireConnection;252;1;251;2
WireConnection;139;0;8;4
WireConnection;255;0;252;0
WireConnection;10;0;261;0
WireConnection;10;1;258;0
WireConnection;256;0;254;0
WireConnection;256;1;253;0
WireConnection;238;0;237;0
WireConnection;12;0;139;0
WireConnection;12;1;238;0
WireConnection;257;0;256;0
WireConnection;257;1;255;0
WireConnection;236;0;10;0
WireConnection;236;1;240;0
WireConnection;136;0;137;0
WireConnection;135;0;237;0
WireConnection;135;1;136;0
WireConnection;137;0;138;0
WireConnection;290;2;236;0
WireConnection;290;9;12;0
WireConnection;290;11;257;0
ASEEND*/
//CHKSM=D1C7574D1C6AB59FD50DEE108AAED23024479C2F