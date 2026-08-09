// Made with Amplify Shader Editor v1.9.7.1
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Vefects/SH_Vefects_BIRP_Area_Indicator_01"
{
	Properties
	{
		_DepthFade("Depth Fade", Float) = 0
		_Emission("Emission", Float) = 1
		_IsAdd("Is Add", Float) = 0
		_ErosionSmoothness("Erosion Smoothness", Float) = 0.777
		[Space(33)][Header(Texture 01)][Space(13)]_Texture01("Texture 01", 2D) = "white" {}
		_Texture01Selector("Texture 01 Selector", Vector) = (1,0,0,0)
		_Texture01UVScale("Texture 01 UV Scale", Vector) = (1,1,0,0)
		_Texture01UVPanSpeed("Texture 01 UV Pan Speed", Vector) = (0,0,0,0)
		[Space(33)][Header(Texture 02)][Space(13)]_Texture02("Texture 02", 2D) = "white" {}
		_Texture02Selector("Texture 02 Selector", Vector) = (0,1,0,0)
		_Texture02UVScale("Texture 02 UV Scale", Vector) = (1,1,0,0)
		_Texture02UVPanSpeed("Texture 02 UV Pan Speed", Vector) = (0,0,0,0)
		[Space(33)][Header(LUT)][Space(13)]_LUT("LUT", 2D) = "white" {}
		_LUTAmplitude("LUT Amplitude", Float) = 1
		_LUTOffset("LUT Offset", Float) = 0
		_LUTPanSpeed("LUT Pan Speed", Float) = 0
		_LUTErosionSmoothness("LUT Erosion Smoothness", Float) = 0.777
		[Space(33)][Header(Distortion Texture)][Space(13)]_DistortionTexture("Distortion Texture", 2D) = "white" {}
		_DistortionUVScale("Distortion UV Scale", Vector) = (1,1,0,0)
		_DistortionUVPanSpeed("Distortion UV Pan Speed", Vector) = (-0.005,0.03,0,0)
		_DistortionAmount("Distortion Amount", Float) = 0.3
		[Space(33)][Header(AR)][Space(13)]_Cull("Cull", Float) = 2
		_Src("Src", Float) = 5
		_Dst("Dst", Float) = 10
		_ZWrite("ZWrite", Float) = 0
		_ZTest("ZTest", Float) = 2
		[HideInInspector] _texcoord2( "", 2D ) = "white" {}
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Transparent"  "Queue" = "Transparent+0" "IsEmissive" = "true"  }
		Cull [_Cull]
		ZWrite [_ZWrite]
		ZTest [_ZTest]
		Blend [_Src] [_Dst]
		CGINCLUDE
		#include "UnityShaderVariables.cginc"
		#include "UnityCG.cginc"
		#include "UnityPBSLighting.cginc"
		#include "Lighting.cginc"
		#pragma target 3.5
		#define ASE_VERSION 19701
		#undef TRANSFORM_TEX
		#define TRANSFORM_TEX(tex,name) float4(tex.xy * name##_ST.xy + name##_ST.zw, tex.z, tex.w)
		struct Input
		{
			float4 vertexColor : COLOR;
			float4 uv2_texcoord2;
			float4 uv_texcoord;
			float4 screenPos;
		};

		uniform float _Dst;
		uniform float _ZWrite;
		uniform float _ZTest;
		uniform float _Cull;
		uniform float _Src;
		uniform sampler2D _LUT;
		uniform float _LUTPanSpeed;
		uniform float _LUTErosionSmoothness;
		uniform sampler2D _Texture01;
		uniform float2 _Texture01UVPanSpeed;
		uniform float2 _Texture01UVScale;
		uniform float4 _Texture01Selector;
		uniform sampler2D _Texture02;
		uniform float2 _Texture02UVPanSpeed;
		uniform float2 _Texture02UVScale;
		uniform sampler2D _DistortionTexture;
		uniform float2 _DistortionUVPanSpeed;
		uniform float2 _DistortionUVScale;
		uniform float _DistortionAmount;
		uniform float4 _Texture02Selector;
		uniform float _LUTAmplitude;
		uniform float _LUTOffset;
		uniform float _ErosionSmoothness;
		uniform float _IsAdd;
		uniform float _Emission;
		UNITY_DECLARE_DEPTH_TEXTURE( _CameraDepthTexture );
		uniform float4 _CameraDepthTexture_TexelSize;
		uniform float _DepthFade;

		inline half4 LightingUnlit( SurfaceOutput s, half3 lightDir, half atten )
		{
			return half4 ( 0, 0, 0, s.Alpha );
		}

		void surf( Input i , inout SurfaceOutput o )
		{
			float2 temp_cast_0 = (_LUTPanSpeed).xx;
			float2 panner87 = ( 1.0 * _Time.y * _Texture01UVPanSpeed + ( i.uv_texcoord.xy * _Texture01UVScale ));
			float dotResult46 = dot( tex2D( _Texture01, panner87 ) , _Texture01Selector );
			float2 panner106 = ( 1.0 * _Time.y * _Texture02UVPanSpeed + ( i.uv_texcoord.xy * _Texture02UVScale ));
			float2 panner94 = ( 1.0 * _Time.y * _DistortionUVPanSpeed + ( i.uv_texcoord.xy * _DistortionUVScale ));
			float dotResult99 = dot( tex2D( _Texture02, ( panner106 + ( (tex2D( _DistortionTexture, panner94 ).rgb).xy * _DistortionAmount ) ) ) , _Texture02Selector );
			float temp_output_97_0 = saturate( dotResult99 );
			float lerpResult123 = lerp( temp_output_97_0 , 1.0 , 0.5);
			float temp_output_114_0 = saturate( ( ( 1.0 - saturate( ( saturate( ( 1.0 - saturate( ( saturate( dotResult46 ) * lerpResult123 ) ) ) ) * ( 1.0 - ( temp_output_97_0 * 0.5 ) ) ) ) ) - saturate( pow( saturate( ( 1.0 - i.uv_texcoord.xy.y ) ) , i.uv_texcoord.w ) ) ) );
			float smoothstepResult53 = smoothstep( i.uv2_texcoord2.x , ( i.uv2_texcoord2.x + _LUTErosionSmoothness ) , temp_output_114_0);
			float2 temp_cast_3 = (( ( saturate( smoothstepResult53 ) * _LUTAmplitude ) + _LUTOffset )).xx;
			float2 panner42 = ( 1.0 * _Time.y * temp_cast_0 + temp_cast_3);
			float4 temp_output_36_0 = ( i.vertexColor * float4( tex2D( _LUT, panner42 ).rgb , 0.0 ) );
			float smoothstepResult29 = smoothstep( i.uv2_texcoord2.x , ( i.uv2_texcoord2.x + _ErosionSmoothness ) , temp_output_114_0);
			float temp_output_30_0 = saturate( smoothstepResult29 );
			float4 lerpResult44 = lerp( temp_output_36_0 , ( temp_output_36_0 * temp_output_30_0 ) , _IsAdd);
			o.Emission = ( lerpResult44 * ( _Emission * i.uv_texcoord.z ) ).rgb;
			float4 ase_screenPos = float4( i.screenPos.xyz , i.screenPos.w + 0.00000000001 );
			float4 ase_screenPosNorm = ase_screenPos / ase_screenPos.w;
			ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
			float screenDepth26 = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE( _CameraDepthTexture, ase_screenPosNorm.xy ));
			float distanceDepth26 = saturate( ( screenDepth26 - LinearEyeDepth( ase_screenPosNorm.z ) ) / ( _DepthFade ) );
			o.Alpha = saturate( ( saturate( ( temp_output_30_0 * i.vertexColor.a ) ) * distanceDepth26 ) );
		}

		ENDCG
		CGPROGRAM
		#pragma surface surf Unlit keepalpha fullforwardshadows 

		ENDCG
		Pass
		{
			Name "ShadowCaster"
			Tags{ "LightMode" = "ShadowCaster" }
			ZWrite On
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.5
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
				float4 customPack1 : TEXCOORD1;
				float4 customPack2 : TEXCOORD2;
				float3 worldPos : TEXCOORD3;
				float4 screenPos : TEXCOORD4;
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
				float3 worldPos = mul( unity_ObjectToWorld, v.vertex ).xyz;
				half3 worldNormal = UnityObjectToWorldNormal( v.normal );
				o.customPack1.xyzw = customInputData.uv2_texcoord2;
				o.customPack1.xyzw = v.texcoord1;
				o.customPack2.xyzw = customInputData.uv_texcoord;
				o.customPack2.xyzw = v.texcoord;
				o.worldPos = worldPos;
				TRANSFER_SHADOW_CASTER_NORMALOFFSET( o )
				o.screenPos = ComputeScreenPos( o.pos );
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
				surfIN.uv2_texcoord2 = IN.customPack1.xyzw;
				surfIN.uv_texcoord = IN.customPack2.xyzw;
				float3 worldPos = IN.worldPos;
				half3 worldViewDir = normalize( UnityWorldSpaceViewDir( worldPos ) );
				surfIN.screenPos = IN.screenPos;
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
Version=19701
Node;AmplifyShaderEditor.TextureCoordinatesNode;92;-7808,1024;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector2Node;95;-7424,1152;Inherit;False;Property;_DistortionUVScale;Distortion UV Scale;19;0;Create;True;0;0;0;False;0;False;1,1;1,1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;93;-7424,1024;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector2Node;96;-6912,1152;Inherit;False;Property;_DistortionUVPanSpeed;Distortion UV Pan Speed;20;0;Create;True;0;0;0;False;0;False;-0.005,0.03;-0.005,0.03;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.PannerNode;94;-6912,1024;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;102;-6912,640;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector2Node;103;-6528,768;Inherit;False;Property;_Texture02UVScale;Texture 02 UV Scale;11;0;Create;True;0;0;0;False;0;False;1,1;1,1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.SamplerNode;89;-6400,1024;Inherit;True;Property;_DistortionTexture;Distortion Texture;18;0;Create;True;0;0;0;False;3;Space(33);Header(Distortion Texture);Space(13);False;-1;6f282ed6b98e16f44a7a673df7b37443;6f282ed6b98e16f44a7a673df7b37443;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.RangedFloatNode;84;-5376,1024;Inherit;False;Property;_DistortionAmount;Distortion Amount;21;0;Create;True;0;0;0;False;0;False;0.3;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;104;-6528,640;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector2Node;105;-6016,768;Inherit;False;Property;_Texture02UVPanSpeed;Texture 02 UV Pan Speed;12;0;Create;True;0;0;0;False;0;False;0,0;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.ComponentMaskNode;91;-6016,1024;Inherit;True;True;True;False;False;1;0;FLOAT3;0,0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;80;-6912,0;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector2Node;86;-6528,128;Inherit;False;Property;_Texture01UVScale;Texture 01 UV Scale;7;0;Create;True;0;0;0;False;0;False;1,1;1,1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;83;-5376,896;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.PannerNode;106;-6016,640;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;85;-6528,0;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector2Node;88;-6016,128;Inherit;False;Property;_Texture01UVPanSpeed;Texture 01 UV Pan Speed;8;0;Create;True;0;0;0;False;0;False;0,0;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.SimpleAddOpNode;81;-5120,640;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.PannerNode;87;-6016,0;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector4Node;98;-4480,768;Inherit;False;Property;_Texture02Selector;Texture 02 Selector;10;0;Create;True;0;0;0;False;0;False;0,1,0,0;0,1,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;100;-4864,640;Inherit;True;Property;_Texture02;Texture 02;9;0;Create;True;0;0;0;False;3;Space(33);Header(Texture 02);Space(13);False;-1;788d74a2951ba514fa76781ea2676e75;788d74a2951ba514fa76781ea2676e75;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SamplerNode;16;-4864,0;Inherit;True;Property;_Texture01;Texture 01;5;0;Create;True;0;0;0;False;3;Space(33);Header(Texture 01);Space(13);False;-1;788d74a2951ba514fa76781ea2676e75;788d74a2951ba514fa76781ea2676e75;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.Vector4Node;47;-4480,128;Inherit;False;Property;_Texture01Selector;Texture 01 Selector;6;0;Create;True;0;0;0;False;0;False;1,0,0,0;1,0,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.DotProductOpNode;99;-4480,640;Inherit;False;2;0;COLOR;0,0,0,0;False;1;FLOAT4;0,0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DotProductOpNode;46;-4480,0;Inherit;False;2;0;COLOR;0,0,0,0;False;1;FLOAT4;0,0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;97;-4224,640;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;50;-4224,0;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;123;-3968,256;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;1;False;2;FLOAT;0.5;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;120;-3968,-128;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;108;-3840,768;Inherit;False;Constant;_Float2;Float 2;32;0;Create;True;0;0;0;False;0;False;0.5;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;122;-3712,-128;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;107;-3840,640;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;117;-4096,1024;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.OneMinusNode;121;-3456,-128;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;109;-3584,640;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;119;-3840,1024;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;124;-3200,-128;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;118;-3584,1024;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TexCoordVertexDataNode;23;-1664,-768;Inherit;False;0;4;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;110;-3584,512;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.PowerNode;116;-3328,1024;Inherit;False;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;111;-3328,512;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;115;-3072,1024;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;112;-3072,512;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;113;-3072,768;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TexCoordVertexDataNode;32;-2304,-384;Inherit;False;1;4;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;51;-1792,896;Inherit;False;Property;_LUTErosionSmoothness;LUT Erosion Smoothness;17;0;Create;True;0;0;0;False;0;False;0.777;0.777;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;114;-2816,768;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;52;-1792,768;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SmoothstepOpNode;53;-1792,640;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;54;-1536,640;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;40;-1536,-1280;Inherit;False;Property;_LUTAmplitude;LUT Amplitude;14;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;31;-1792,256;Inherit;False;Property;_ErosionSmoothness;Erosion Smoothness;4;0;Create;True;0;0;0;False;0;False;0.777;0.777;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;38;-1536,-1408;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;41;-1280,-1280;Inherit;False;Property;_LUTOffset;LUT Offset;15;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;33;-1792,128;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;39;-1280,-1408;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;43;-1024,-1280;Inherit;False;Property;_LUTPanSpeed;LUT Pan Speed;16;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SmoothstepOpNode;29;-1792,0;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;42;-1024,-1408;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SaturateNode;30;-1536,0;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.VertexColorNode;19;-1664,-512;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;34;-768,-1408;Inherit;True;Property;_LUT;LUT;13;0;Create;True;0;0;0;False;3;Space(33);Header(LUT);Space(13);False;-1;ebc6571ef101faa4a98d42416dea7ae5;ebc6571ef101faa4a98d42416dea7ae5;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.RangedFloatNode;101;-896,640;Inherit;False;Property;_DepthFade;Depth Fade;1;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;27;-1280,384;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;36;-1024,-512;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT3;0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SaturateNode;28;-1152,384;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DepthFade;26;-896,512;Inherit;False;True;True;False;2;1;FLOAT3;0,0,0;False;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;20;-1024,0;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;22;-512,128;Inherit;False;Property;_Emission;Emission;2;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;45;-768,128;Inherit;False;Property;_IsAdd;Is Add;3;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;15;462,-50;Inherit;False;1252;162.95;Ge Lush was here! <3;5;10;11;12;13;14;Ge Lush was here! <3;0,0,0,1;0;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;17;-896,384;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;24;-384,128;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;44;-768,0;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;12;1024,0;Inherit;False;Property;_Dst;Dst;24;0;Create;True;0;0;0;True;0;False;10;10;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;13;1280,0;Inherit;False;Property;_ZWrite;ZWrite;25;0;Create;True;0;0;0;True;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;14;1536,0;Inherit;False;Property;_ZTest;ZTest;26;0;Create;True;0;0;0;True;0;False;2;2;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;10;512,0;Inherit;False;Property;_Cull;Cull;22;0;Create;True;0;0;0;True;3;Space(33);Header(AR);Space(13);False;2;2;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;11;768,0;Inherit;False;Property;_Src;Src;23;0;Create;True;0;0;0;True;0;False;5;5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;18;-640,384;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;21;-384,0;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;78;-2048,-384;Inherit;False;Pan Offset;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;126;0,0;Float;False;True;-1;3;ASEMaterialInspector;0;0;Unlit;Vefects/SH_Vefects_BIRP_Area_Indicator_01;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;Back;0;True;_ZWrite;0;True;_ZTest;False;0;False;;0;False;;False;0;Custom;0.5;True;True;0;False;Transparent;;Transparent;All;12;all;True;True;True;True;0;False;;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;True;1;5;True;_Src;10;True;_Dst;0;0;False;;0;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;0;-1;-1;-1;0;False;0;0;True;_Cull;-1;0;False;;0;0;0;False;0.1;False;;0;False;;False;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;16;FLOAT4;0,0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;93;0;92;0
WireConnection;93;1;95;0
WireConnection;94;0;93;0
WireConnection;94;2;96;0
WireConnection;89;1;94;0
WireConnection;104;0;102;0
WireConnection;104;1;103;0
WireConnection;91;0;89;5
WireConnection;83;0;91;0
WireConnection;83;1;84;0
WireConnection;106;0;104;0
WireConnection;106;2;105;0
WireConnection;85;0;80;0
WireConnection;85;1;86;0
WireConnection;81;0;106;0
WireConnection;81;1;83;0
WireConnection;87;0;85;0
WireConnection;87;2;88;0
WireConnection;100;1;81;0
WireConnection;16;1;87;0
WireConnection;99;0;100;0
WireConnection;99;1;98;0
WireConnection;46;0;16;0
WireConnection;46;1;47;0
WireConnection;97;0;99;0
WireConnection;50;0;46;0
WireConnection;123;0;97;0
WireConnection;120;0;50;0
WireConnection;120;1;123;0
WireConnection;122;0;120;0
WireConnection;107;0;97;0
WireConnection;107;1;108;0
WireConnection;121;0;122;0
WireConnection;109;0;107;0
WireConnection;119;0;117;2
WireConnection;124;0;121;0
WireConnection;118;0;119;0
WireConnection;110;0;124;0
WireConnection;110;1;109;0
WireConnection;116;0;118;0
WireConnection;116;1;23;4
WireConnection;111;0;110;0
WireConnection;115;0;116;0
WireConnection;112;0;111;0
WireConnection;113;0;112;0
WireConnection;113;1;115;0
WireConnection;114;0;113;0
WireConnection;52;0;32;1
WireConnection;52;1;51;0
WireConnection;53;0;114;0
WireConnection;53;1;32;1
WireConnection;53;2;52;0
WireConnection;54;0;53;0
WireConnection;38;0;54;0
WireConnection;38;1;40;0
WireConnection;33;0;32;1
WireConnection;33;1;31;0
WireConnection;39;0;38;0
WireConnection;39;1;41;0
WireConnection;29;0;114;0
WireConnection;29;1;32;1
WireConnection;29;2;33;0
WireConnection;42;0;39;0
WireConnection;42;2;43;0
WireConnection;30;0;29;0
WireConnection;34;1;42;0
WireConnection;27;0;30;0
WireConnection;27;1;19;4
WireConnection;36;0;19;0
WireConnection;36;1;34;5
WireConnection;28;0;27;0
WireConnection;26;0;101;0
WireConnection;20;0;36;0
WireConnection;20;1;30;0
WireConnection;17;0;28;0
WireConnection;17;1;26;0
WireConnection;24;0;22;0
WireConnection;24;1;23;3
WireConnection;44;0;36;0
WireConnection;44;1;20;0
WireConnection;44;2;45;0
WireConnection;18;0;17;0
WireConnection;21;0;44;0
WireConnection;21;1;24;0
WireConnection;78;0;32;2
WireConnection;126;2;21;0
WireConnection;126;9;18;0
ASEEND*/
//CHKSM=BE30728D8F626094464D02BAFB90B813859AE44B