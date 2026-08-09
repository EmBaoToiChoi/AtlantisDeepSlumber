// Made with Amplify Shader Editor v1.9.7.1
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Vefects/SH_Vefects_BIRP_Unlit_Master_01"
{
	Properties
	{
		_TexturesMultiplySoftLight("Textures Multiply / Soft Light", Float) = 0
		_MaskMultiplySubtract("Mask Multiply / Subtract", Float) = 0
		_ParticleColorLUT("Particle Color / LUT", Float) = 0
		_BlendWithSecondaryTexture("Blend With Secondary Texture", Float) = 0
		[Space(33)][Header(Noise)][Space(13)]_NoiseTexture("Noise Texture", 2D) = "white" {}
		_NoiseTextureSelector("Noise Texture Selector", Vector) = (0,1,0,0)
		_NoiseUVScale("Noise UV Scale", Vector) = (0.3,1,0,0)
		_NoiseUVSpeed("Noise UV Speed", Vector) = (0,0,0,0)
		[Space(33)][Header(Secondary Noise)][Space(13)]_SecondaryNoiseTexture("Secondary Noise Texture", 2D) = "white" {}
		_SecondaryNoiseTextureSelector("Secondary Noise Texture Selector", Vector) = (0,1,0,0)
		_SecondaryNoiseUVScale("Secondary Noise UV Scale", Vector) = (0.3,1,0,0)
		_SecondaryNoiseUVSpeed("Secondary Noise UV Speed", Vector) = (0,0,0,0)
		_ErosionSmoothness("Erosion Smoothness", Float) = 1
		_Emission("Emission", Float) = 1
		_DepthFade("Depth Fade", Float) = 1
		[Space(33)][Header(Distortion)][Space(13)]_DistortionNoise("Distortion Noise", 2D) = "white" {}
		_DistortionNoiseTextureSelector("Distortion Noise Texture Selector", Vector) = (0,1,0,0)
		_DistortionIntensity1("Distortion Intensity", Float) = 0.1
		_DistortionNoiseUVScale("Distortion Noise UV Scale", Vector) = (1,1,0,0)
		_DistortionNoiseUVPanSpeed("Distortion Noise UV Pan Speed", Vector) = (0.05,-0.2,0,0)
		[Space(33)][Header(Cutout)][Space(13)]_CutoutTexture("Cutout Texture", 2D) = "white" {}
		_CutoutTextureSelector("Cutout Texture Selector", Vector) = (0,1,0,0)
		_CutoutErosion("Cutout Erosion", Float) = 0
		_CutoutErosionSmoothness("Cutout Erosion Smoothness", Float) = 1
		_CutoutMaskStrength("Cutout Mask Strength", Float) = 1
		_FresnelErosion("Fresnel Erosion", Float) = 0.1
		_FresnelErosionSmoothness("Fresnel Erosion Smoothness", Float) = 0.3
		_FresnelFade("Fresnel Fade", Float) = 0
		[Space(33)][Header(LUT)][Space(13)]_LUT("LUT", 2D) = "white" {}
		_LUTAmplitude("LUT Amplitude", Float) = 1
		_LUTOffset("LUT Offset", Float) = 0
		_LUTPanSpeed("LUT Pan Speed", Float) = 0
		[Space(33)][Header(AR)][Space(13)]_Cull("Cull", Float) = 2
		_Src("Src", Float) = 5
		_Dst("Dst", Float) = 10
		_ZWrite("ZWrite", Float) = 0
		_ZTest("ZTest", Float) = 2
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] _texcoord2( "", 2D ) = "white" {}
		[HideInInspector] _texcoord3( "", 2D ) = "white" {}
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
			float4 uv3_texcoord3;
			float3 worldPos;
			float3 worldNormal;
			float4 screenPos;
		};

		uniform float _Dst;
		uniform float _ZWrite;
		uniform float _ZTest;
		uniform float _Cull;
		uniform float _Src;
		uniform sampler2D _LUT;
		uniform float _LUTPanSpeed;
		uniform float _ErosionSmoothness;
		uniform sampler2D _NoiseTexture;
		uniform float2 _NoiseUVSpeed;
		uniform float2 _NoiseUVScale;
		uniform sampler2D _DistortionNoise;
		uniform float2 _DistortionNoiseUVPanSpeed;
		uniform float2 _DistortionNoiseUVScale;
		uniform float4 _DistortionNoiseTextureSelector;
		uniform float _DistortionIntensity1;
		uniform float4 _NoiseTextureSelector;
		uniform sampler2D _SecondaryNoiseTexture;
		uniform float2 _SecondaryNoiseUVSpeed;
		uniform float2 _SecondaryNoiseUVScale;
		uniform float4 _SecondaryNoiseTextureSelector;
		uniform float _TexturesMultiplySoftLight;
		uniform float _BlendWithSecondaryTexture;
		uniform float _CutoutErosion;
		uniform float _CutoutErosionSmoothness;
		uniform sampler2D _CutoutTexture;
		uniform float4 _CutoutTexture_ST;
		uniform float4 _CutoutTextureSelector;
		uniform float _CutoutMaskStrength;
		uniform float _MaskMultiplySubtract;
		uniform float _LUTAmplitude;
		uniform float _LUTOffset;
		uniform float _ParticleColorLUT;
		uniform float _Emission;
		uniform float _FresnelErosion;
		uniform float _FresnelErosionSmoothness;
		uniform float _FresnelFade;
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
			float2 panner61 = ( 1.0 * _Time.y * _NoiseUVSpeed + ( i.uv_texcoord.xy * _NoiseUVScale ));
			float2 appendResult139 = (float2(i.uv_texcoord.z , i.uv_texcoord.w));
			float2 panner47 = ( 1.0 * _Time.y * _DistortionNoiseUVPanSpeed + ( i.uv_texcoord.xy * _DistortionNoiseUVScale ));
			float dotResult80 = dot( tex2D( _DistortionNoise, panner47 ) , _DistortionNoiseTextureSelector );
			float UVNoise83 = ( ( ( saturate( dotResult80 ) + -0.5 ) * 2.0 ) * i.uv3_texcoord3.x );
			float2 temp_cast_2 = (UVNoise83).xx;
			float2 lerpResult55 = lerp( float2( 0,0 ) , temp_cast_2 , _DistortionIntensity1);
			float dotResult98 = dot( tex2D( _NoiseTexture, ( ( panner61 + appendResult139 ) + lerpResult55 ) ) , _NoiseTextureSelector );
			float temp_output_99_0 = saturate( dotResult98 );
			float2 panner90 = ( 1.0 * _Time.y * _SecondaryNoiseUVSpeed + ( i.uv_texcoord.xy * _SecondaryNoiseUVScale ));
			float2 appendResult141 = (float2(i.uv2_texcoord2.x , i.uv2_texcoord2.y));
			float2 temp_cast_4 = (UVNoise83).xx;
			float2 lerpResult94 = lerp( float2( 0,0 ) , temp_cast_4 , _DistortionIntensity1);
			float dotResult101 = dot( tex2D( _SecondaryNoiseTexture, ( ( panner90 + appendResult141 ) + lerpResult94 ) ) , _SecondaryNoiseTextureSelector );
			float temp_output_102_0 = saturate( dotResult101 );
			float lerpResult104 = lerp( saturate( ( temp_output_99_0 * temp_output_102_0 ) ) , saturate( ( 1.0 - ( ( 1.0 - temp_output_99_0 ) * ( 1.0 - temp_output_102_0 ) ) ) ) , _TexturesMultiplySoftLight);
			float lerpResult113 = lerp( temp_output_99_0 , lerpResult104 , _BlendWithSecondaryTexture);
			float2 uv_CutoutTexture = i.uv_texcoord * _CutoutTexture_ST.xy + _CutoutTexture_ST.zw;
			float dotResult77 = dot( float4( tex2D( _CutoutTexture, uv_CutoutTexture ).rgb , 0.0 ) , _CutoutTextureSelector );
			float smoothstepResult36 = smoothstep( _CutoutErosion , ( _CutoutErosion + _CutoutErosionSmoothness ) , saturate( dotResult77 ));
			float temp_output_40_0 = saturate( ( saturate( smoothstepResult36 ) * _CutoutMaskStrength ) );
			float lerpResult115 = lerp( saturate( ( lerpResult113 * temp_output_40_0 ) ) , saturate( ( lerpResult113 - ( 1.0 - temp_output_40_0 ) ) ) , _MaskMultiplySubtract);
			float smoothstepResult29 = smoothstep( i.uv2_texcoord2.z , ( i.uv2_texcoord2.z + _ErosionSmoothness ) , saturate( lerpResult115 ));
			float temp_output_30_0 = saturate( smoothstepResult29 );
			float2 temp_cast_7 = (( ( temp_output_30_0 * ( _LUTAmplitude * i.uv3_texcoord3.y ) ) + _LUTOffset )).xx;
			float2 panner133 = ( 1.0 * _Time.y * temp_cast_0 + temp_cast_7);
			float4 lerpResult135 = lerp( i.vertexColor , ( i.vertexColor * float4( tex2D( _LUT, panner133 ).rgb , 0.0 ) ) , _ParticleColorLUT);
			o.Emission = ( lerpResult135 * ( i.uv2_texcoord2.w * _Emission ) ).rgb;
			float3 ase_worldPos = i.worldPos;
			float3 ase_viewVectorWS = ( _WorldSpaceCameraPos.xyz - ase_worldPos );
			float3 ase_viewDirWS = normalize( ase_viewVectorWS );
			float3 ase_worldNormal = i.worldNormal;
			float fresnelNdotV67 = dot( ase_worldNormal, ase_viewDirWS );
			float fresnelNode67 = ( 0.0 + 1.0 * pow( max( 1.0 - fresnelNdotV67 , 0.0001 ), 1.0 ) );
			float smoothstepResult71 = smoothstep( _FresnelErosion , ( _FresnelErosion + _FresnelErosionSmoothness ) , ( 1.0 - fresnelNode67 ));
			float lerpResult146 = lerp( temp_output_30_0 , saturate( ( temp_output_30_0 * saturate( smoothstepResult71 ) ) ) , _FresnelFade);
			float4 ase_screenPos = float4( i.screenPos.xyz , i.screenPos.w + 0.00000000001 );
			float4 ase_screenPosNorm = ase_screenPos / ase_screenPos.w;
			ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
			float screenDepth26 = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE( _CameraDepthTexture, ase_screenPosNorm.xy ));
			float distanceDepth26 = saturate( ( screenDepth26 - LinearEyeDepth( ase_screenPosNorm.z ) ) / ( _DepthFade ) );
			o.Alpha = saturate( ( saturate( ( i.vertexColor.a * lerpResult146 ) ) * distanceDepth26 ) );
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
				float4 customPack3 : TEXCOORD3;
				float3 worldPos : TEXCOORD4;
				float4 screenPos : TEXCOORD5;
				float3 worldNormal : TEXCOORD6;
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
				o.worldNormal = worldNormal;
				o.customPack1.xyzw = customInputData.uv2_texcoord2;
				o.customPack1.xyzw = v.texcoord1;
				o.customPack2.xyzw = customInputData.uv_texcoord;
				o.customPack2.xyzw = v.texcoord;
				o.customPack3.xyzw = customInputData.uv3_texcoord3;
				o.customPack3.xyzw = v.texcoord2;
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
				surfIN.uv3_texcoord3 = IN.customPack3.xyzw;
				float3 worldPos = IN.worldPos;
				half3 worldViewDir = normalize( UnityWorldSpaceViewDir( worldPos ) );
				surfIN.worldPos = worldPos;
				surfIN.worldNormal = IN.worldNormal;
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
Node;AmplifyShaderEditor.Vector2Node;43;-9472,1408;Inherit;False;Property;_DistortionNoiseUVScale;Distortion Noise UV Scale;19;0;Create;True;0;0;0;False;0;False;1,1;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.TextureCoordinatesNode;44;-9856,1280;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector2Node;45;-9088,1408;Inherit;False;Property;_DistortionNoiseUVPanSpeed;Distortion Noise UV Pan Speed;20;0;Create;True;0;0;0;False;0;False;0.05,-0.2;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;46;-9472,1280;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.PannerNode;47;-9088,1280;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SamplerNode;48;-8704,1280;Inherit;True;Property;_DistortionNoise;Distortion Noise;16;0;Create;True;0;0;0;False;3;Space(33);Header(Distortion);Space(13);False;-1;801b4be3075bc4840b14161c01e73734;801b4be3075bc4840b14161c01e73734;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.Vector4Node;81;-8448,1536;Inherit;False;Property;_DistortionNoiseTextureSelector;Distortion Noise Texture Selector;17;0;Create;True;0;0;0;False;0;False;0,1,0,0;0,1,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.DotProductOpNode;80;-8064,1280;Inherit;False;2;0;COLOR;0,0,0,0;False;1;FLOAT4;0,0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;82;-7936,1280;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ComponentMaskNode;50;-7680,1280;Inherit;False;True;True;False;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;54;-7424,1280;Inherit;False;ConstantBiasScale;-1;;1;63208df05c83e8e49a48ffbdce2e43a0;0;3;3;FLOAT;0;False;1;FLOAT;-0.5;False;2;FLOAT;2;False;1;FLOAT;0
Node;AmplifyShaderEditor.TexCoordVertexDataNode;86;-7424,1536;Inherit;False;2;4;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TextureCoordinatesNode;87;-7808,512;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector2Node;97;-7808,640;Inherit;False;Property;_SecondaryNoiseUVScale;Secondary Noise UV Scale;11;0;Create;True;0;0;0;False;0;False;0.3,1;0.3,1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;85;-7040,1280;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;49;-7808,0;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector2Node;62;-7808,128;Inherit;False;Property;_NoiseUVScale;Noise UV Scale;7;0;Create;True;0;0;0;False;0;False;0.3,1;0.3,1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;88;-7424,512;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector2Node;89;-7168,640;Inherit;False;Property;_SecondaryNoiseUVSpeed;Secondary Noise UV Speed;12;0;Create;True;0;0;0;False;0;False;0,0;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.TexCoordVertexDataNode;143;-6784,768;Inherit;False;1;4;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RegisterLocalVarNode;83;-6784,1280;Inherit;False;UVNoise;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;60;-7424,0;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector2Node;63;-7168,128;Inherit;False;Property;_NoiseUVSpeed;Noise UV Speed;8;0;Create;True;0;0;0;False;0;False;0,0;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.TexCoordVertexDataNode;140;-6784,256;Inherit;False;0;4;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector2Node;91;-6272,640;Inherit;False;Constant;_Vector1;Vector 0;6;0;Create;True;0;0;0;False;0;False;0,0;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.GetLocalVarNode;93;-6272,768;Inherit;False;83;UVNoise;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;90;-7168,512;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;141;-6784,640;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;84;-6272,256;Inherit;False;83;UVNoise;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;92;-6016,256;Inherit;False;Property;_DistortionIntensity1;Distortion Intensity;18;0;Create;True;0;0;0;False;0;False;0.1;0.1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;51;-6272,128;Inherit;False;Constant;_Vector0;Vector 0;6;0;Create;True;0;0;0;False;0;False;0,0;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.PannerNode;61;-7168,0;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;139;-6784,128;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;94;-6016,640;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;142;-6784,512;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;55;-6016,128;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;137;-6784,0;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;95;-6016,512;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;57;-6016,0;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SamplerNode;96;-5760,512;Inherit;True;Property;_SecondaryNoiseTexture;Secondary Noise Texture;9;0;Create;True;0;0;0;False;3;Space(33);Header(Secondary Noise);Space(13);False;-1;a9e3ca617beadc84c9cafa940c488eb2;1736d5320abaee941b186957301a85a3;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.Vector4Node;103;-5248,640;Inherit;False;Property;_SecondaryNoiseTextureSelector;Secondary Noise Texture Selector;10;0;Create;True;0;0;0;False;0;False;0,1,0,0;0,1,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;16;-5760,0;Inherit;True;Property;_NoiseTexture;Noise Texture;5;0;Create;True;0;0;0;False;3;Space(33);Header(Noise);Space(13);False;-1;a9e3ca617beadc84c9cafa940c488eb2;1736d5320abaee941b186957301a85a3;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.Vector4Node;100;-5248,128;Inherit;False;Property;_NoiseTextureSelector;Noise Texture Selector;6;0;Create;True;0;0;0;False;0;False;0,1,0,0;0,1,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;35;-5248,1024;Inherit;True;Property;_CutoutTexture;Cutout Texture;21;0;Create;True;0;0;0;False;3;Space(33);Header(Cutout);Space(13);False;-1;31e0c2c4a297a6b42acec1b5af8c7447;31e0c2c4a297a6b42acec1b5af8c7447;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.Vector4Node;78;-5248,1280;Inherit;False;Property;_CutoutTextureSelector;Cutout Texture Selector;22;0;Create;True;0;0;0;False;0;False;0,1,0,0;0,1,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.DotProductOpNode;101;-5248,512;Inherit;False;2;0;COLOR;0,0,0,0;False;1;FLOAT4;0,0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DotProductOpNode;98;-5248,0;Inherit;False;2;0;COLOR;0,0,0,0;False;1;FLOAT4;0,0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;38;-4864,1408;Inherit;False;Property;_CutoutErosionSmoothness;Cutout Erosion Smoothness;24;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.DotProductOpNode;77;-4864,1024;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT4;0,0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;102;-5120,512;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;37;-4864,1280;Inherit;False;Property;_CutoutErosion;Cutout Erosion;23;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;99;-5120,0;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;109;-4864,256;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;108;-4864,640;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;79;-4736,1024;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;39;-4608,1152;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;111;-4864,384;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SmoothstepOpNode;36;-4608,1024;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;106;-4864,128;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;110;-4864,512;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;123;-4352,1024;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;124;-4224,1152;Inherit;False;Property;_CutoutMaskStrength;Cutout Mask Strength;25;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;107;-4608,128;Inherit;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;105;-4352,256;Inherit;False;Property;_TexturesMultiplySoftLight;Textures Multiply / Soft Light;1;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;112;-4608,384;Inherit;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;122;-4224,1024;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;104;-4352,128;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;114;-3968,128;Inherit;False;Property;_BlendWithSecondaryTexture;Blend With Secondary Texture;4;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;40;-4096,1024;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;113;-3968,0;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;125;-3840,1024;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;117;-3712,0;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;119;-3712,384;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;116;-3328,128;Inherit;False;Property;_MaskMultiplySubtract;Mask Multiply / Subtract;2;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;118;-3584,0;Inherit;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;120;-3584,384;Inherit;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;74;-2304,1280;Inherit;False;Property;_FresnelErosionSmoothness;Fresnel Erosion Smoothness;27;0;Create;True;0;0;0;False;0;False;0.3;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.FresnelNode;67;-2816,896;Inherit;False;Standard;WorldNormal;ViewDir;True;True;5;0;FLOAT3;0,0,1;False;4;FLOAT3;0,0,0;False;1;FLOAT;0;False;2;FLOAT;1;False;3;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;72;-2304,1152;Inherit;False;Property;_FresnelErosion;Fresnel Erosion;26;0;Create;True;0;0;0;False;0;False;0.1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;31;-2944,256;Inherit;False;Property;_ErosionSmoothness;Erosion Smoothness;13;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;115;-3328,0;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TexCoordVertexDataNode;126;-3328,-1024;Inherit;False;1;4;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.OneMinusNode;69;-2560,896;Inherit;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;73;-2048,1152;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;33;-2944,128;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;121;-3200,0;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SmoothstepOpNode;71;-2048,896;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SmoothstepOpNode;29;-2944,0;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;130;-2432,-896;Inherit;False;Property;_LUTAmplitude;LUT Amplitude;30;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TexCoordVertexDataNode;145;-2432,-640;Inherit;False;2;4;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SaturateNode;68;-1792,896;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;30;-2768,0;Inherit;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;144;-2432,-768;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;75;-1536,896;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;129;-2432,-1024;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;132;-2176,-896;Inherit;False;Property;_LUTOffset;LUT Offset;31;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;76;-1280,896;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;147;-1280,640;Inherit;False;Property;_FresnelFade;Fresnel Fade;28;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;131;-2176,-1024;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;134;-1920,-896;Inherit;False;Property;_LUTPanSpeed;LUT Pan Speed;32;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.VertexColorNode;19;-1536,-512;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.LerpOp;146;-1280,512;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;133;-1920,-1024;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;64;-384,640;Inherit;False;Property;_DepthFade;Depth Fade;15;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;27;-768,384;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;128;-1536,-1024;Inherit;True;Property;_LUT;LUT;29;0;Create;True;0;0;0;False;3;Space(33);Header(LUT);Space(13);False;-1;ebc6571ef101faa4a98d42416dea7ae5;ebc6571ef101faa4a98d42416dea7ae5;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SaturateNode;28;-640,384;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DepthFade;26;-384,512;Inherit;False;True;True;False;2;1;FLOAT3;0,0,0;False;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;136;-768,-384;Inherit;False;Property;_ParticleColorLUT;Particle Color / LUT;3;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;20;-1024,-1024;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT3;0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;22;-384,-256;Inherit;False;Property;_Emission;Emission;14;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;15;462,-50;Inherit;False;1252;162.95;Ge Lush was here! <3;5;10;11;12;13;14;Ge Lush was here! <3;0,0,0,1;0;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;17;-384,384;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;135;-768,-512;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;127;-384,-384;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;12;1024,0;Inherit;False;Property;_Dst;Dst;35;0;Create;True;0;0;0;True;0;False;10;10;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;13;1280,0;Inherit;False;Property;_ZWrite;ZWrite;36;0;Create;True;0;0;0;True;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;14;1536,0;Inherit;False;Property;_ZTest;ZTest;37;0;Create;True;0;0;0;True;0;False;2;2;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;10;512,0;Inherit;False;Property;_Cull;Cull;33;0;Create;True;0;0;0;True;3;Space(33);Header(AR);Space(13);False;2;2;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;11;768,0;Inherit;False;Property;_Src;Src;34;0;Create;True;0;0;0;True;0;False;5;5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;18;-128,384;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;21;-384,-512;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;149;0,0;Float;False;True;-1;3;ASEMaterialInspector;0;0;Unlit;Vefects/SH_Vefects_BIRP_Unlit_Master_01;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;Back;0;True;_ZWrite;0;True;_ZTest;False;0;False;;0;False;;False;0;Custom;0.5;True;True;0;False;Transparent;;Transparent;All;12;all;True;True;True;True;0;False;;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;True;1;5;True;_Src;10;True;_Dst;0;0;False;;0;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;0;-1;-1;-1;0;False;0;0;True;_Cull;-1;0;False;;0;0;0;False;0.1;False;;0;False;;False;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;16;FLOAT4;0,0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;46;0;44;0
WireConnection;46;1;43;0
WireConnection;47;0;46;0
WireConnection;47;2;45;0
WireConnection;48;1;47;0
WireConnection;80;0;48;0
WireConnection;80;1;81;0
WireConnection;82;0;80;0
WireConnection;50;0;82;0
WireConnection;54;3;50;0
WireConnection;85;0;54;0
WireConnection;85;1;86;1
WireConnection;88;0;87;0
WireConnection;88;1;97;0
WireConnection;83;0;85;0
WireConnection;60;0;49;0
WireConnection;60;1;62;0
WireConnection;90;0;88;0
WireConnection;90;2;89;0
WireConnection;141;0;143;1
WireConnection;141;1;143;2
WireConnection;61;0;60;0
WireConnection;61;2;63;0
WireConnection;139;0;140;3
WireConnection;139;1;140;4
WireConnection;94;0;91;0
WireConnection;94;1;93;0
WireConnection;94;2;92;0
WireConnection;142;0;90;0
WireConnection;142;1;141;0
WireConnection;55;0;51;0
WireConnection;55;1;84;0
WireConnection;55;2;92;0
WireConnection;137;0;61;0
WireConnection;137;1;139;0
WireConnection;95;0;142;0
WireConnection;95;1;94;0
WireConnection;57;0;137;0
WireConnection;57;1;55;0
WireConnection;96;1;95;0
WireConnection;16;1;57;0
WireConnection;101;0;96;0
WireConnection;101;1;103;0
WireConnection;98;0;16;0
WireConnection;98;1;100;0
WireConnection;77;0;35;5
WireConnection;77;1;78;0
WireConnection;102;0;101;0
WireConnection;99;0;98;0
WireConnection;109;0;99;0
WireConnection;108;0;102;0
WireConnection;79;0;77;0
WireConnection;39;0;37;0
WireConnection;39;1;38;0
WireConnection;111;0;109;0
WireConnection;111;1;108;0
WireConnection;36;0;79;0
WireConnection;36;1;37;0
WireConnection;36;2;39;0
WireConnection;106;0;99;0
WireConnection;106;1;102;0
WireConnection;110;0;111;0
WireConnection;123;0;36;0
WireConnection;107;0;106;0
WireConnection;112;0;110;0
WireConnection;122;0;123;0
WireConnection;122;1;124;0
WireConnection;104;0;107;0
WireConnection;104;1;112;0
WireConnection;104;2;105;0
WireConnection;40;0;122;0
WireConnection;113;0;99;0
WireConnection;113;1;104;0
WireConnection;113;2;114;0
WireConnection;125;0;40;0
WireConnection;117;0;113;0
WireConnection;117;1;40;0
WireConnection;119;0;113;0
WireConnection;119;1;125;0
WireConnection;118;0;117;0
WireConnection;120;0;119;0
WireConnection;115;0;118;0
WireConnection;115;1;120;0
WireConnection;115;2;116;0
WireConnection;69;0;67;0
WireConnection;73;0;72;0
WireConnection;73;1;74;0
WireConnection;33;0;126;3
WireConnection;33;1;31;0
WireConnection;121;0;115;0
WireConnection;71;0;69;0
WireConnection;71;1;72;0
WireConnection;71;2;73;0
WireConnection;29;0;121;0
WireConnection;29;1;126;3
WireConnection;29;2;33;0
WireConnection;68;0;71;0
WireConnection;30;0;29;0
WireConnection;144;0;130;0
WireConnection;144;1;145;2
WireConnection;75;0;30;0
WireConnection;75;1;68;0
WireConnection;129;0;30;0
WireConnection;129;1;144;0
WireConnection;76;0;75;0
WireConnection;131;0;129;0
WireConnection;131;1;132;0
WireConnection;146;0;30;0
WireConnection;146;1;76;0
WireConnection;146;2;147;0
WireConnection;133;0;131;0
WireConnection;133;2;134;0
WireConnection;27;0;19;4
WireConnection;27;1;146;0
WireConnection;128;1;133;0
WireConnection;28;0;27;0
WireConnection;26;0;64;0
WireConnection;20;0;19;0
WireConnection;20;1;128;5
WireConnection;17;0;28;0
WireConnection;17;1;26;0
WireConnection;135;0;19;0
WireConnection;135;1;20;0
WireConnection;135;2;136;0
WireConnection;127;0;126;4
WireConnection;127;1;22;0
WireConnection;18;0;17;0
WireConnection;21;0;135;0
WireConnection;21;1;127;0
WireConnection;149;2;21;0
WireConnection;149;9;18;0
ASEEND*/
//CHKSM=2B0C4C867C0CA136C9274999FD5F82E920279169