// Made with Amplify Shader Editor v1.9.7.1
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Vefects/SH_Vefects_BIRP_Fake_Decal_Lit_01"
{
	Properties
	{
		_Emissive("Emissive", Float) = 1
		_Specular("Specular", Float) = 0
		_Smoothness("Smoothness", Float) = 0
		_ParticleColorLUT("Particle Color / LUT", Float) = 0
		_ColorTint("Color Tint", Color) = (1,1,1,1)
		_TextureColor("Texture Color", Float) = 0
		[Space(33)][Header(Decal)][Space(13)]_DecalTexture("Decal Texture", 2D) = "white" {}
		_DecalRotation("Decal Rotation", Float) = 0
		_DecalScaleFromCenter("Decal Scale From Center", Float) = 1
		_DecalScaleFromCenterNonUniform("Decal Scale From Center Non Uniform", Vector) = (1,1,0,0)
		[Space(33)][Header(Fake Decal)][Space(13)]_FakeDecalDepthFade("Fake Decal Depth Fade", Float) = 1
		_FakeDecalDepthFadeErosion("Fake Decal Depth Fade Erosion", Float) = 0
		_FakeDecalDepthFadeErosionSmoothness("Fake Decal Depth Fade Erosion Smoothness", Float) = 0.1
		_ErosionSmoothness("Erosion Smoothness", Float) = 1
		_OpacityBoost("Opacity Boost", Float) = 1
		[Space(33)][Header(LUT)][Space(13)]_LUT("LUT", 2D) = "white" {}
		_LUTAmplitude("LUT Amplitude", Float) = 1
		_LUTOffset("LUT Offset", Float) = 0
		_LUTPanSpeed("LUT Pan Speed", Float) = 0
		_LUTErosionSmoothness("LUT Erosion Smoothness", Float) = 1
		[Space(33)][Header(AR)][Space(13)]_Cull("Cull", Float) = 2
		_Src("Src", Float) = 5
		_Dst("Dst", Float) = 10
		_ZWrite("ZWrite", Float) = 0
		_ZTest("ZTest", Float) = 2
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] _texcoord2( "", 2D ) = "white" {}
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
			float4 uv_texcoord;
			float4 uv2_texcoord2;
			float4 screenPos;
		};

		uniform float _Src;
		uniform float _Dst;
		uniform float _ZWrite;
		uniform float _ZTest;
		uniform float _Cull;
		uniform float4 _ColorTint;
		uniform sampler2D _DecalTexture;
		uniform float2 _DecalScaleFromCenterNonUniform;
		uniform float _DecalScaleFromCenter;
		uniform float _DecalRotation;
		uniform float _TextureColor;
		uniform sampler2D _LUT;
		uniform float _LUTPanSpeed;
		uniform float _LUTErosionSmoothness;
		uniform float _LUTAmplitude;
		uniform float _LUTOffset;
		uniform float _ParticleColorLUT;
		uniform float _Emissive;
		uniform float _Specular;
		uniform float _Smoothness;
		uniform float _ErosionSmoothness;
		uniform float _OpacityBoost;
		uniform float _FakeDecalDepthFadeErosion;
		uniform float _FakeDecalDepthFadeErosionSmoothness;
		UNITY_DECLARE_DEPTH_TEXTURE( _CameraDepthTexture );
		uniform float4 _CameraDepthTexture_TexelSize;
		uniform float _FakeDecalDepthFade;

		void surf( Input i , inout SurfaceOutputStandardSpecular o )
		{
			o.Normal = float3(0,0,1);
			float4 temp_output_40_0 = ( i.vertexColor * _ColorTint );
			float2 _Vector2 = float2(0.5,0.5);
			float randomRotate57 = i.uv2_texcoord2.x;
			float cos52 = cos( ( ( ( _DecalRotation + randomRotate57 ) * ( 2.0 * UNITY_PI ) ) / 360.0 ) );
			float sin52 = sin( ( ( ( _DecalRotation + randomRotate57 ) * ( 2.0 * UNITY_PI ) ) / 360.0 ) );
			float2 rotator52 = mul( ( ( ( i.uv_texcoord.xy - _Vector2 ) / ( _DecalScaleFromCenterNonUniform * _DecalScaleFromCenter ) ) + _Vector2 ) - float2( 0.5,0.5 ) , float2x2( cos52 , -sin52 , sin52 , cos52 )) + float2( 0.5,0.5 );
			float4 tex2DNode24 = tex2D( _DecalTexture, rotator52 );
			float4 lerpResult32 = lerp( temp_output_40_0 , ( tex2DNode24 * temp_output_40_0 ) , _TextureColor);
			float2 temp_cast_0 = (_LUTPanSpeed).xx;
			float eros79 = i.uv_texcoord.w;
			float smoothstepResult110 = smoothstep( eros79 , ( eros79 + _LUTErosionSmoothness ) , tex2DNode24.g);
			float2 temp_cast_1 = (( ( saturate( smoothstepResult110 ) * _LUTAmplitude ) + _LUTOffset )).xx;
			float2 panner92 = ( 1.0 * _Time.y * temp_cast_0 + temp_cast_1);
			float4 lerpResult98 = lerp( lerpResult32 , ( float4( tex2D( _LUT, panner92 ).rgb , 0.0 ) * lerpResult32 ) , _ParticleColorLUT);
			o.Albedo = lerpResult98.rgb;
			float em78 = i.uv_texcoord.z;
			o.Emission = ( lerpResult98 * ( _Emissive * em78 ) ).rgb;
			float3 temp_cast_5 = (_Specular).xxx;
			o.Specular = temp_cast_5;
			o.Smoothness = _Smoothness;
			float smoothstepResult82 = smoothstep( eros79 , ( eros79 + _ErosionSmoothness ) , tex2DNode24.g);
			float4 ase_screenPos = float4( i.screenPos.xyz , i.screenPos.w + 0.00000000001 );
			float4 ase_screenPosNorm = ase_screenPos / ase_screenPos.w;
			ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
			float screenDepth125 = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE( _CameraDepthTexture, ase_screenPosNorm.xy ));
			float distanceDepth125 = saturate( ( screenDepth125 - LinearEyeDepth( ase_screenPosNorm.z ) ) / ( _FakeDecalDepthFade ) );
			float smoothstepResult128 = smoothstep( _FakeDecalDepthFadeErosion , ( _FakeDecalDepthFadeErosion + _FakeDecalDepthFadeErosionSmoothness ) , distanceDepth125);
			o.Alpha = saturate( ( saturate( ( saturate( ( saturate( smoothstepResult82 ) * _OpacityBoost ) ) * i.vertexColor.a ) ) * ( 1.0 - saturate( smoothstepResult128 ) ) ) );
		}

		ENDCG
		CGPROGRAM
		#pragma surface surf StandardSpecular keepalpha fullforwardshadows 

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
				float4 tSpace0 : TEXCOORD5;
				float4 tSpace1 : TEXCOORD6;
				float4 tSpace2 : TEXCOORD7;
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
				half3 worldTangent = UnityObjectToWorldDir( v.tangent.xyz );
				half tangentSign = v.tangent.w * unity_WorldTransformParams.w;
				half3 worldBinormal = cross( worldNormal, worldTangent ) * tangentSign;
				o.tSpace0 = float4( worldTangent.x, worldBinormal.x, worldNormal.x, worldPos.x );
				o.tSpace1 = float4( worldTangent.y, worldBinormal.y, worldNormal.y, worldPos.y );
				o.tSpace2 = float4( worldTangent.z, worldBinormal.z, worldNormal.z, worldPos.z );
				o.customPack1.xyzw = customInputData.uv_texcoord;
				o.customPack1.xyzw = v.texcoord;
				o.customPack2.xyzw = customInputData.uv2_texcoord2;
				o.customPack2.xyzw = v.texcoord1;
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
				surfIN.uv_texcoord = IN.customPack1.xyzw;
				surfIN.uv2_texcoord2 = IN.customPack2.xyzw;
				float3 worldPos = IN.worldPos;
				half3 worldViewDir = normalize( UnityWorldSpaceViewDir( worldPos ) );
				surfIN.screenPos = IN.screenPos;
				surfIN.vertexColor = IN.color;
				SurfaceOutputStandardSpecular o;
				UNITY_INITIALIZE_OUTPUT( SurfaceOutputStandardSpecular, o )
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
Node;AmplifyShaderEditor.TexCoordVertexDataNode;77;-4864,-384;Inherit;False;1;4;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.CommentaryNode;69;-5312,-48;Inherit;False;1205.095;817.495;Decal Rotator;10;52;60;53;61;68;58;54;59;75;76;Decal Rotator;0,0,0,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;113;-5216,1392;Inherit;False;964;530.95;Decal Scale From Center;8;121;120;119;118;117;116;115;114;Decal Scale From Center;0,0,0,1;0;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;57;-4480,-384;Inherit;False;randomRotate;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;59;-5120,512;Inherit;False;Constant;_Float1;Float 1;12;0;Create;True;0;0;0;False;0;False;2;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;54;-5120,384;Inherit;False;Property;_DecalRotation;Decal Rotation;8;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;76;-4864,256;Inherit;False;57;randomRotate;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;114;-4912,1568;Inherit;False;Constant;_Vector2;Vector 1;16;0;Create;True;0;0;0;False;0;False;0.5,0.5;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.TextureCoordinatesNode;115;-5168,1440;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector2Node;116;-4656,1680;Inherit;False;Property;_DecalScaleFromCenterNonUniform;Decal Scale From Center Non Uniform;10;0;Create;True;0;0;0;False;0;False;1,1;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.RangedFloatNode;117;-4656,1808;Inherit;False;Property;_DecalScaleFromCenter;Decal Scale From Center;9;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.PiNode;58;-4864,512;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;75;-4864,384;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;118;-4912,1440;Inherit;False;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;119;-4656,1552;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;68;-4608,384;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;61;-4352,512;Inherit;False;Constant;_Float2;Float 2;12;0;Create;True;0;0;0;False;0;False;360;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;120;-4656,1440;Inherit;False;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TexCoordVertexDataNode;56;-4864,-640;Inherit;False;0;4;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector2Node;53;-5248,128;Inherit;False;Constant;_Vector0;Vector 0;11;0;Create;True;0;0;0;False;0;False;0.5,0.5;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.SimpleDivideOpNode;60;-4352,384;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;121;-4400,1440;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;79;-4480,-512;Inherit;False;eros;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RotatorNode;52;-4864,0;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;108;-3456,768;Inherit;False;79;eros;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;107;-3456,1024;Inherit;False;Property;_LUTErosionSmoothness;LUT Erosion Smoothness;20;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;85;-3456,512;Inherit;False;Property;_ErosionSmoothness;Erosion Smoothness;14;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;24;-3840,0;Inherit;True;Property;_DecalTexture;Decal Texture;7;0;Create;True;0;0;0;False;3;Space(33);Header(Decal);Space(13);False;-1;123b792a735506d4593764fa22a848de;123b792a735506d4593764fa22a848de;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.GetLocalVarNode;83;-3456,256;Inherit;False;79;eros;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;109;-3200,896;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;84;-3200,384;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SmoothstepOpNode;110;-3072,768;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;122;-2096,1392;Inherit;False;1636;418.95;Fake Decal Depth Fade;9;131;130;129;128;127;126;125;124;123;Fake Decal Depth Fade;0,0,0,1;0;0
Node;AmplifyShaderEditor.SmoothstepOpNode;82;-3072,256;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;89;-3072,-768;Inherit;False;Property;_LUTAmplitude;LUT Amplitude;17;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;112;-2816,768;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;23;-2432,384;Inherit;False;Property;_OpacityBoost;Opacity Boost;15;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;111;-2816,256;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;123;-2048,1696;Inherit;False;Property;_FakeDecalDepthFadeErosionSmoothness;Fake Decal Depth Fade Erosion Smoothness;13;0;Create;True;0;0;0;False;0;False;0.1;0.1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;124;-2048,1440;Inherit;False;Property;_FakeDecalDepthFade;Fake Decal Depth Fade;11;0;Create;True;0;0;0;False;3;Space(33);Header(Fake Decal);Space(13);False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;126;-2048,1568;Inherit;False;Property;_FakeDecalDepthFadeErosion;Fake Decal Depth Fade Erosion;12;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.VertexColorNode;39;-3840,-640;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ColorNode;31;-3840,-256;Inherit;False;Property;_ColorTint;Color Tint;5;0;Create;True;0;0;0;False;0;False;1,1,1,1;1,1,1,1;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;88;-3072,-896;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;91;-2816,-768;Inherit;False;Property;_LUTOffset;LUT Offset;18;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;26;-2432,256;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DepthFade;125;-1792,1440;Inherit;False;True;True;False;2;1;FLOAT3;0,0,0;False;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;127;-1408,1568;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;40;-3584,-640;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleAddOpNode;90;-2816,-896;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;93;-2560,-768;Inherit;False;Property;_LUTPanSpeed;LUT Pan Speed;19;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;73;-2176,256;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SmoothstepOpNode;128;-1408,1440;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;30;-3456,0;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.PannerNode;92;-2560,-896;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;33;-1536,-128;Inherit;False;Property;_TextureColor;Texture Color;6;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;129;-1152,1440;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;74;-1664,256;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;78;-4480,-640;Inherit;False;em;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;94;-2304,-896;Inherit;True;Property;_LUT;LUT;16;0;Create;True;0;0;0;False;3;Space(33);Header(LUT);Space(13);False;-1;ebc6571ef101faa4a98d42416dea7ae5;ebc6571ef101faa4a98d42416dea7ae5;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.LerpOp;32;-1536,0;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.OneMinusNode;130;-896,1440;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;132;-1456,256;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;28;-640,-384;Inherit;False;Property;_Emissive;Emissive;1;0;Create;True;0;0;0;False;0;False;1;13;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;81;-640,-256;Inherit;False;78;em;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;96;-1792,-896;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;106;-1536,-640;Inherit;False;Property;_ParticleColorLUT;Particle Color / LUT;4;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;25;576,-48;Inherit;False;1241;165;Lush was here! <3;5;38;37;36;35;34;Lush was here! <3;0.4629898,0.2327043,1,1;0;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;131;-640,1440;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;80;-640,-128;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;98;-1536,-768;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;34;896,0;Inherit;False;Property;_Src;Src;22;0;Create;True;0;0;0;True;0;False;5;5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;35;1152,0;Inherit;False;Property;_Dst;Dst;23;0;Create;True;0;0;0;True;0;False;10;10;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;36;1408,0;Inherit;False;Property;_ZWrite;ZWrite;24;0;Create;True;0;0;0;True;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;37;1664,0;Inherit;False;Property;_ZTest;ZTest;25;0;Create;True;0;0;0;True;0;False;2;2;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;38;640,0;Inherit;False;Property;_Cull;Cull;21;0;Create;True;0;0;0;True;3;Space(33);Header(AR);Space(13);False;2;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;27;-640,128;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.Vector3Node;71;-768,768;Inherit;False;Constant;_Vector1;Vector 1;13;0;Create;True;0;0;0;False;0;False;0,0,1;0,0,0;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.RangedFloatNode;70;-768,1024;Inherit;False;Property;_Specular;Specular;2;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;72;-768,1152;Inherit;False;Property;_Smoothness;Smoothness;3;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;29;-640,256;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;134;0,0;Float;False;True;-1;3;ASEMaterialInspector;0;0;StandardSpecular;Vefects/SH_Vefects_BIRP_Fake_Decal_Lit_01;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;Back;0;True;_ZWrite;0;True;_ZTest;False;0;False;;0;False;;False;0;Custom;0.5;True;True;0;True;Transparent;;Transparent;All;12;all;True;True;True;True;0;False;;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;True;1;5;True;_Src;10;True;_Dst;0;0;False;;0;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;0;-1;-1;-1;0;False;0;0;True;_Cull;-1;0;False;;0;0;0;False;0.1;False;;0;False;;False;17;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;16;FLOAT4;0,0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
Node;AmplifyShaderEditor.CommentaryNode;51;-6528,-1024;Inherit;False;768.0875;257.7429;Check Clevender.com courses to learn about shaders <3;0;Check Clevender.com courses to learn about shaders <3;1,0,0.5501842,1;0;0
WireConnection;57;0;77;1
WireConnection;58;0;59;0
WireConnection;75;0;54;0
WireConnection;75;1;76;0
WireConnection;118;0;115;0
WireConnection;118;1;114;0
WireConnection;119;0;116;0
WireConnection;119;1;117;0
WireConnection;68;0;75;0
WireConnection;68;1;58;0
WireConnection;120;0;118;0
WireConnection;120;1;119;0
WireConnection;60;0;68;0
WireConnection;60;1;61;0
WireConnection;121;0;120;0
WireConnection;121;1;114;0
WireConnection;79;0;56;4
WireConnection;52;0;121;0
WireConnection;52;1;53;0
WireConnection;52;2;60;0
WireConnection;24;1;52;0
WireConnection;109;0;108;0
WireConnection;109;1;107;0
WireConnection;84;0;83;0
WireConnection;84;1;85;0
WireConnection;110;0;24;2
WireConnection;110;1;108;0
WireConnection;110;2;109;0
WireConnection;82;0;24;2
WireConnection;82;1;83;0
WireConnection;82;2;84;0
WireConnection;112;0;110;0
WireConnection;111;0;82;0
WireConnection;88;0;112;0
WireConnection;88;1;89;0
WireConnection;26;0;111;0
WireConnection;26;1;23;0
WireConnection;125;0;124;0
WireConnection;127;0;126;0
WireConnection;127;1;123;0
WireConnection;40;0;39;0
WireConnection;40;1;31;0
WireConnection;90;0;88;0
WireConnection;90;1;91;0
WireConnection;73;0;26;0
WireConnection;128;0;125;0
WireConnection;128;1;126;0
WireConnection;128;2;127;0
WireConnection;30;0;24;0
WireConnection;30;1;40;0
WireConnection;92;0;90;0
WireConnection;92;2;93;0
WireConnection;129;0;128;0
WireConnection;74;0;73;0
WireConnection;74;1;39;4
WireConnection;78;0;56;3
WireConnection;94;1;92;0
WireConnection;32;0;40;0
WireConnection;32;1;30;0
WireConnection;32;2;33;0
WireConnection;130;0;129;0
WireConnection;132;0;74;0
WireConnection;96;0;94;5
WireConnection;96;1;32;0
WireConnection;131;0;132;0
WireConnection;131;1;130;0
WireConnection;80;0;28;0
WireConnection;80;1;81;0
WireConnection;98;0;32;0
WireConnection;98;1;96;0
WireConnection;98;2;106;0
WireConnection;27;0;98;0
WireConnection;27;1;80;0
WireConnection;29;0;131;0
WireConnection;134;0;98;0
WireConnection;134;1;71;0
WireConnection;134;2;27;0
WireConnection;134;3;70;0
WireConnection;134;4;72;0
WireConnection;134;9;29;0
ASEEND*/
//CHKSM=8D163C206F35371CB0F18FB5424D18BC2F0D294D