// Made with Amplify Shader Editor v1.9.7.1
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Vefects/SH_Vefects_BIRP_Fake_Decal_Cracks_Fire_01"
{
	Properties
	{
		_Specular("Specular", Float) = 0
		_Smoothness("Smoothness", Float) = 0
		_NoiseEmissive("Noise Emissive", 2D) = "white" {}
		_Emissive("Emissive", Float) = 1
		_EmissiveErosionSmoothness("Emissive Erosion Smoothness", Float) = 0.5
		_EmissivePanSpeed("Emissive Pan Speed", Float) = 0.3
		_NoiseSpots("Noise Spots", 2D) = "white" {}
		_EmissiveSpots("Emissive Spots", Float) = 1
		_EmissiveSpotsColor("Emissive Spots Color", Color) = (1,0.6235294,0,1)
		_SpotsPanSpeed("Spots Pan Speed", Float) = 0.3
		[Space(33)][Header(Decal)][Space(13)]_DecalTexture("Decal Texture", 2D) = "white" {}
		_DecalRotation("Decal Rotation", Float) = 0
		_DecalScaleFromCenter("Decal Scale From Center", Float) = 1
		_DecalScaleFromCenterNonUniform("Decal Scale From Center Non Uniform", Vector) = (1,1,0,0)
		_FakeDecalDepthFade("Fake Decal Depth Fade", Float) = 1
		_FakeDecalDepthFadeErosion("Fake Decal Depth Fade Erosion", Float) = 0
		_FakeDecalDepthFadeErosionSmoothness("Fake Decal Depth Fade Erosion Smoothness", Float) = 0.1
		_ErosionSmoothness("Erosion Smoothness", Float) = 0.25
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
			float4 uv_texcoord;
			float4 uv2_texcoord2;
			float4 vertexColor : COLOR;
			float4 screenPos;
		};

		uniform float _Src;
		uniform float _Dst;
		uniform float _ZWrite;
		uniform float _ZTest;
		uniform float _Cull;
		uniform sampler2D _LUT;
		uniform float _LUTPanSpeed;
		uniform sampler2D _DecalTexture;
		uniform float2 _DecalScaleFromCenterNonUniform;
		uniform float _DecalScaleFromCenter;
		uniform float _DecalRotation;
		uniform float _LUTAmplitude;
		uniform float _LUTOffset;
		uniform sampler2D _NoiseEmissive;
		uniform float _EmissivePanSpeed;
		uniform float _EmissiveErosionSmoothness;
		uniform float _Emissive;
		uniform float _EmissiveSpots;
		uniform float4 _EmissiveSpotsColor;
		uniform sampler2D _NoiseSpots;
		uniform float _SpotsPanSpeed;
		uniform float _Specular;
		uniform float _Smoothness;
		uniform float _ErosionSmoothness;
		uniform float _FakeDecalDepthFadeErosion;
		uniform float _FakeDecalDepthFadeErosionSmoothness;
		UNITY_DECLARE_DEPTH_TEXTURE( _CameraDepthTexture );
		uniform float4 _CameraDepthTexture_TexelSize;
		uniform float _FakeDecalDepthFade;

		void surf( Input i , inout SurfaceOutputStandardSpecular o )
		{
			float2 temp_cast_0 = (_LUTPanSpeed).xx;
			float2 _Vector1 = float2(0.5,0.5);
			float randomRotate57 = i.uv2_texcoord2.x;
			float cos52 = cos( ( ( ( _DecalRotation + randomRotate57 ) * ( 2.0 * UNITY_PI ) ) / 360.0 ) );
			float sin52 = sin( ( ( ( _DecalRotation + randomRotate57 ) * ( 2.0 * UNITY_PI ) ) / 360.0 ) );
			float2 rotator52 = mul( ( ( ( i.uv_texcoord.xy - _Vector1 ) / ( _DecalScaleFromCenterNonUniform * _DecalScaleFromCenter ) ) + _Vector1 ) - float2( 0.5,0.5 ) , float2x2( cos52 , -sin52 , sin52 , cos52 )) + float2( 0.5,0.5 );
			float2 decalUV87 = rotator52;
			float4 tex2DNode24 = tex2D( _DecalTexture, decalUV87 );
			float crackTexture275 = tex2DNode24.g;
			float LUTMult109 = i.uv2_texcoord2.y;
			float2 temp_cast_1 = (( ( crackTexture275 * ( _LUTAmplitude + LUTMult109 ) ) + _LUTOffset )).xx;
			float2 panner98 = ( 1.0 * _Time.y * temp_cast_0 + temp_cast_1);
			o.Albedo = tex2D( _LUT, panner98 ).rgb;
			float mulTime343 = _Time.y * _EmissivePanSpeed;
			float2 panner324 = ( mulTime343 * float2( 0,-0.25 ) + ( decalUV87 * float2( 0.3,0.3 ) ));
			float2 panner325 = ( mulTime343 * float2( 0,0.05 ) + decalUV87);
			float erosEmi335 = i.uv2_texcoord2.w;
			float emissiveTexture274 = tex2DNode24.r;
			float smoothstepResult93 = smoothstep( erosEmi335 , ( erosEmi335 + _EmissiveErosionSmoothness ) , emissiveTexture274);
			float em78 = i.uv_texcoord.z;
			float spotsEmissive319 = i.uv2_texcoord2.z;
			float burnSpots276 = tex2DNode24.b;
			float mulTime341 = _Time.y * _SpotsPanSpeed;
			float2 temp_output_312_0 = ( decalUV87 * float2( 3,3 ) );
			float2 panner309 = ( mulTime341 * float2( 0,-0.2 ) + temp_output_312_0);
			float saferPower301 = abs( tex2D( _NoiseSpots, panner309 ).g );
			float2 panner310 = ( mulTime341 * float2( 0.05,0.1 ) + temp_output_312_0);
			float saferPower302 = abs( tex2D( _NoiseSpots, panner310 ).g );
			float saferPower297 = abs( ( ( burnSpots276 * saturate( ( ( pow( saferPower301 , 2.0 ) * pow( saferPower302 , 2.0 ) ) * 2.0 ) ) ) * 2.0 ) );
			o.Emission = max( ( ( saturate( ( saturate( ( tex2D( _NoiseEmissive, panner324 ).g * tex2D( _NoiseEmissive, panner325 ).g ) ) * saturate( smoothstepResult93 ) ) ) * (i.vertexColor).rgb ) * ( _Emissive * em78 ) ) , ( ( ( spotsEmissive319 * _EmissiveSpots ) * _EmissiveSpotsColor.rgb ) * saturate( ( pow( saferPower297 , 1.5 ) * 10.0 ) ) ) );
			float3 temp_cast_2 = (_Specular).xxx;
			o.Specular = temp_cast_2;
			o.Smoothness = _Smoothness;
			float eros79 = i.uv_texcoord.w;
			float smoothstepResult82 = smoothstep( eros79 , ( eros79 + _ErosionSmoothness ) , crackTexture275);
			float4 ase_screenPos = float4( i.screenPos.xyz , i.screenPos.w + 0.00000000001 );
			float4 ase_screenPosNorm = ase_screenPos / ase_screenPos.w;
			ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
			float screenDepth357 = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE( _CameraDepthTexture, ase_screenPosNorm.xy ));
			float distanceDepth357 = saturate( ( screenDepth357 - LinearEyeDepth( ase_screenPosNorm.z ) ) / ( _FakeDecalDepthFade ) );
			float smoothstepResult360 = smoothstep( _FakeDecalDepthFadeErosion , ( _FakeDecalDepthFadeErosion + _FakeDecalDepthFadeErosionSmoothness ) , distanceDepth357);
			o.Alpha = saturate( ( saturate( ( saturate( smoothstepResult82 ) * i.vertexColor.a ) ) * ( 1.0 - saturate( smoothstepResult360 ) ) ) );
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
Node;AmplifyShaderEditor.TexCoordVertexDataNode;77;-6272,-640;Inherit;False;1;4;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.CommentaryNode;69;-6208,-48;Inherit;False;1205.095;817.495;Decal Rotator;11;52;60;53;61;68;58;54;59;75;76;87;Decal Rotator;0,0,0,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;346;-6192,848;Inherit;False;964;530.95;Decal Scale From Center;8;354;353;352;351;350;349;348;347;Decal Scale From Center;0,0,0,1;0;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;57;-5888,-640;Inherit;False;randomRotate;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;59;-6016,512;Inherit;False;Constant;_Float1;Float 1;12;0;Create;True;0;0;0;False;0;False;2;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;54;-6016,384;Inherit;False;Property;_DecalRotation;Decal Rotation;12;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;76;-5760,256;Inherit;False;57;randomRotate;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;347;-5888,1024;Inherit;False;Constant;_Vector1;Vector 1;16;0;Create;True;0;0;0;False;0;False;0.5,0.5;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.TextureCoordinatesNode;349;-6144,896;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector2Node;353;-5632,1136;Inherit;False;Property;_DecalScaleFromCenterNonUniform;Decal Scale From Center Non Uniform;14;0;Create;True;0;0;0;False;0;False;1,1;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.RangedFloatNode;354;-5632,1264;Inherit;False;Property;_DecalScaleFromCenter;Decal Scale From Center;13;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.PiNode;58;-5760,512;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;75;-5760,384;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;348;-5888,896;Inherit;False;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;350;-5632,1008;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;68;-5504,384;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;61;-5248,512;Inherit;False;Constant;_Float2;Float 2;12;0;Create;True;0;0;0;False;0;False;360;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;351;-5632,896;Inherit;False;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector2Node;53;-6144,128;Inherit;False;Constant;_Vector0;Vector 0;11;0;Create;True;0;0;0;False;0;False;0.5,0.5;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.SimpleDivideOpNode;60;-5248,384;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;352;-5376,896;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RotatorNode;52;-5760,16;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;87;-5248,0;Inherit;False;decalUV;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;313;-3072,640;Inherit;False;87;decalUV;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;342;-3072,256;Inherit;False;Property;_SpotsPanSpeed;Spots Pan Speed;10;0;Create;True;0;0;0;False;0;False;0.3;0.3;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;312;-2816,512;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;3,3;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleTimeNode;341;-2816,256;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;310;-2560,768;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0.05,0.1;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TexturePropertyNode;305;-2176,256;Inherit;True;Property;_NoiseSpots;Noise Spots;7;0;Create;True;0;0;0;False;0;False;1736d5320abaee941b186957301a85a3;1736d5320abaee941b186957301a85a3;False;white;Auto;Texture2D;-1;0;2;SAMPLER2D;0;SAMPLERSTATE;1
Node;AmplifyShaderEditor.PannerNode;309;-2560,512;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,-0.2;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SamplerNode;308;-2176,768;Inherit;True;Property;_TextureSample1;Texture Sample 0;19;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Instance;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SamplerNode;307;-2176,512;Inherit;True;Property;_TextureSample0;Texture Sample 0;19;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Instance;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.GetLocalVarNode;258;-4608,0;Inherit;False;87;decalUV;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.PowerNode;301;-1792,512;Inherit;False;True;2;0;FLOAT;0;False;1;FLOAT;2;False;1;FLOAT;0
Node;AmplifyShaderEditor.PowerNode;302;-1792,768;Inherit;False;True;2;0;FLOAT;0;False;1;FLOAT;2;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;326;-4096,-1024;Inherit;False;87;decalUV;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;344;-3584,-1536;Inherit;False;Property;_EmissivePanSpeed;Emissive Pan Speed;6;0;Create;True;0;0;0;False;0;False;0.3;0.3;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;24;-4352,0;Inherit;True;Property;_DecalTexture;Decal Texture;11;0;Create;True;0;0;0;False;3;Space(33);Header(Decal);Space(13);False;-1;13af17fcb9ea0b74cafacae41377a551;04b65e545b916e74d918756d1513018b;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.TexCoordVertexDataNode;56;-6272,-896;Inherit;False;0;4;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;303;-1536,512;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;330;-3584,-1280;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0.3,0.3;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;335;-5888,-256;Inherit;False;erosEmi;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleTimeNode;343;-3328,-1536;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;79;-5888,-768;Inherit;False;eros;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;274;-3968,0;Inherit;False;emissiveTexture;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;276;-3968,256;Inherit;False;burnSpots;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;318;-1408,512;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;2;False;1;FLOAT;0
Node;AmplifyShaderEditor.TexturePropertyNode;329;-2816,-1536;Inherit;True;Property;_NoiseEmissive;Noise Emissive;3;0;Create;True;0;0;0;False;0;False;2ee23f48726cb264ebf439a2950dc996;2ee23f48726cb264ebf439a2950dc996;False;white;Auto;Texture2D;-1;0;2;SAMPLER2D;0;SAMPLERSTATE;1
Node;AmplifyShaderEditor.PannerNode;324;-3200,-1280;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,-0.25;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.PannerNode;325;-3200,-1024;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0.05;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;90;-3200,-512;Inherit;False;Property;_EmissiveErosionSmoothness;Emissive Erosion Smoothness;5;0;Create;True;0;0;0;False;0;False;0.5;0.5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;91;-3200,-640;Inherit;False;335;erosEmi;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;345;-2176,1744;Inherit;False;1636;418.95;Fake Decal Depth Fade;9;363;362;361;360;359;358;357;356;355;Fake Decal Depth Fade;0,0,0,1;0;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;275;-3968,128;Inherit;False;crackTexture;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;85;-3968,2304;Inherit;False;Property;_ErosionSmoothness;Erosion Smoothness;18;0;Create;True;0;0;0;False;0;False;0.25;0.25;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;83;-3968,2048;Inherit;False;79;eros;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;304;-1536,384;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;295;-1792,256;Inherit;False;276;burnSpots;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;322;-2816,-1024;Inherit;True;Property;_TextureSample2;Texture Sample 0;19;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Instance;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SamplerNode;328;-2816,-1280;Inherit;True;Property;_TextureSample3;Texture Sample 0;19;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Instance;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.GetLocalVarNode;336;-3200,-768;Inherit;False;274;emissiveTexture;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;92;-2816,-640;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;315;-3968,1920;Inherit;False;275;crackTexture;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;84;-3712,2176;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;355;-2128,1792;Inherit;False;Property;_FakeDecalDepthFade;Fake Decal Depth Fade;15;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;356;-2128,2048;Inherit;False;Property;_FakeDecalDepthFadeErosionSmoothness;Fake Decal Depth Fade Erosion Smoothness;17;0;Create;True;0;0;0;False;0;False;0.1;0.1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;358;-2128,1920;Inherit;False;Property;_FakeDecalDepthFadeErosion;Fake Decal Depth Fade Erosion;16;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;296;-1536,256;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;331;-2432,-1280;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;109;-5888,-512;Inherit;False;LUTMult;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SmoothstepOpNode;93;-2816,-768;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SmoothstepOpNode;82;-3584,2048;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.DepthFade;357;-1872,1792;Inherit;False;True;True;False;2;1;FLOAT3;0,0,0;False;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;359;-1488,1920;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;298;-1280,256;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;2;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;257;-1920,-1536;Inherit;False;109;LUTMult;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;95;-1920,-1792;Inherit;False;Property;_LUTAmplitude;LUT Amplitude;20;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;332;-2304,-1280;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;319;-5888,-384;Inherit;False;spotsEmissive;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;102;-2560,-768;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.VertexColorNode;39;-2176,-640;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SaturateNode;141;-3328,2048;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SmoothstepOpNode;360;-1488,1792;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;289;-1280,-128;Inherit;False;Property;_EmissiveSpots;Emissive Spots;8;0;Create;True;0;0;0;False;0;False;1;13;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;320;-1280,-256;Inherit;False;319;spotsEmissive;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;256;-1920,-1664;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;277;-2304,-1920;Inherit;False;275;crackTexture;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;333;-2048,-1280;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;78;-5888,-896;Inherit;False;em;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.PowerNode;297;-1152,256;Inherit;False;True;2;0;FLOAT;0;False;1;FLOAT;1.5;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;316;-3072,2048;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;361;-1232,1792;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ComponentMaskNode;129;-1920,-640;Inherit;False;True;True;True;False;1;0;COLOR;0,0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode;81;-1024,-896;Inherit;False;78;em;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;292;-1280,0;Inherit;False;Property;_EmissiveSpotsColor;Emissive Spots Color;9;0;Create;True;0;0;0;False;0;False;1,0.6235294,0,1;1,0.6235294,0,1;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;321;-1024,-256;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;28;-1024,-1024;Inherit;False;Property;_Emissive;Emissive;4;0;Create;True;0;0;0;False;0;False;1;13;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;94;-1920,-1920;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;97;-1664,-1792;Inherit;False;Property;_LUTOffset;LUT Offset;21;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;334;-1920,-1280;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;300;-1024,256;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;10;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;362;-976,1792;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;364;-2527.989,1993.703;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;80;-1024,-768;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;290;-1024,-128;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SaturateNode;294;-768,0;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;96;-1664,-1920;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;99;-1408,-1792;Inherit;False;Property;_LUTPanSpeed;LUT Pan Speed;22;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;337;-1664,-1280;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.CommentaryNode;25;576,-48;Inherit;False;1241;165;Lush was here! <3;5;38;37;36;35;34;Lush was here! <3;0.4629898,0.2327043,1,1;0;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;363;-720,1792;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;27;-1024,-640;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;293;-768,-128;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.PannerNode;98;-1408,-1920;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;34;896,0;Inherit;False;Property;_Src;Src;24;0;Create;True;0;0;0;True;0;False;5;5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;35;1152,0;Inherit;False;Property;_Dst;Dst;25;0;Create;True;0;0;0;True;0;False;10;10;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;36;1408,0;Inherit;False;Property;_ZWrite;ZWrite;26;0;Create;True;0;0;0;True;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;37;1664,0;Inherit;False;Property;_ZTest;ZTest;27;0;Create;True;0;0;0;True;0;False;2;2;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;38;640,0;Inherit;False;Property;_Cull;Cull;23;0;Create;True;0;0;0;True;3;Space(33);Header(AR);Space(13);False;2;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMaxOpNode;288;-640,-384;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;311;-3072,512;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;100;-1152,-1920;Inherit;True;Property;_LUT;LUT;19;0;Create;True;0;0;0;False;3;Space(33);Header(LUT);Space(13);False;-1;fd766865e5fdced43beaa99c3e953eb9;fd766865e5fdced43beaa99c3e953eb9;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.TextureCoordinatesNode;323;-4096,-1280;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;338;-512,0;Inherit;False;Property;_Specular;Specular;1;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;339;-512,128;Inherit;False;Property;_Smoothness;Smoothness;2;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;101;-640,384;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;366;0,0;Float;False;True;-1;3;ASEMaterialInspector;0;0;StandardSpecular;Vefects/SH_Vefects_BIRP_Fake_Decal_Cracks_Fire_01;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;Back;0;True;_ZWrite;0;True;_ZTest;False;0;False;;0;False;;False;0;Custom;0.5;True;True;0;False;Transparent;;Transparent;All;12;all;True;True;True;True;0;False;;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;True;1;5;True;_Src;10;True;_Dst;0;0;False;;0;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;0;-1;-1;-1;0;False;0;0;True;_Cull;-1;0;False;;0;0;0;False;0.1;False;;0;False;;False;17;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;16;FLOAT4;0,0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
Node;AmplifyShaderEditor.CommentaryNode;51;-7936,-1024;Inherit;False;768.0875;257.7429;Check Clevender.com courses to learn about shaders <3;0;Check Clevender.com courses to learn about shaders <3;1,0,0.5501842,1;0;0
WireConnection;57;0;77;1
WireConnection;58;0;59;0
WireConnection;75;0;54;0
WireConnection;75;1;76;0
WireConnection;348;0;349;0
WireConnection;348;1;347;0
WireConnection;350;0;353;0
WireConnection;350;1;354;0
WireConnection;68;0;75;0
WireConnection;68;1;58;0
WireConnection;351;0;348;0
WireConnection;351;1;350;0
WireConnection;60;0;68;0
WireConnection;60;1;61;0
WireConnection;352;0;351;0
WireConnection;352;1;347;0
WireConnection;52;0;352;0
WireConnection;52;1;53;0
WireConnection;52;2;60;0
WireConnection;87;0;52;0
WireConnection;312;0;313;0
WireConnection;341;0;342;0
WireConnection;310;0;312;0
WireConnection;310;1;341;0
WireConnection;309;0;312;0
WireConnection;309;1;341;0
WireConnection;308;0;305;0
WireConnection;308;1;310;0
WireConnection;307;0;305;0
WireConnection;307;1;309;0
WireConnection;301;0;307;2
WireConnection;302;0;308;2
WireConnection;24;1;258;0
WireConnection;303;0;301;0
WireConnection;303;1;302;0
WireConnection;330;0;326;0
WireConnection;335;0;77;4
WireConnection;343;0;344;0
WireConnection;79;0;56;4
WireConnection;274;0;24;1
WireConnection;276;0;24;3
WireConnection;318;0;303;0
WireConnection;324;0;330;0
WireConnection;324;1;343;0
WireConnection;325;0;326;0
WireConnection;325;1;343;0
WireConnection;275;0;24;2
WireConnection;304;0;318;0
WireConnection;322;0;329;0
WireConnection;322;1;325;0
WireConnection;328;0;329;0
WireConnection;328;1;324;0
WireConnection;92;0;91;0
WireConnection;92;1;90;0
WireConnection;84;0;83;0
WireConnection;84;1;85;0
WireConnection;296;0;295;0
WireConnection;296;1;304;0
WireConnection;331;0;328;2
WireConnection;331;1;322;2
WireConnection;109;0;77;2
WireConnection;93;0;336;0
WireConnection;93;1;91;0
WireConnection;93;2;92;0
WireConnection;82;0;315;0
WireConnection;82;1;83;0
WireConnection;82;2;84;0
WireConnection;357;0;355;0
WireConnection;359;0;358;0
WireConnection;359;1;356;0
WireConnection;298;0;296;0
WireConnection;332;0;331;0
WireConnection;319;0;77;3
WireConnection;102;0;93;0
WireConnection;141;0;82;0
WireConnection;360;0;357;0
WireConnection;360;1;358;0
WireConnection;360;2;359;0
WireConnection;256;0;95;0
WireConnection;256;1;257;0
WireConnection;333;0;332;0
WireConnection;333;1;102;0
WireConnection;78;0;56;3
WireConnection;297;0;298;0
WireConnection;316;0;141;0
WireConnection;316;1;39;4
WireConnection;361;0;360;0
WireConnection;129;0;39;0
WireConnection;321;0;320;0
WireConnection;321;1;289;0
WireConnection;94;0;277;0
WireConnection;94;1;256;0
WireConnection;334;0;333;0
WireConnection;300;0;297;0
WireConnection;362;0;361;0
WireConnection;364;0;316;0
WireConnection;80;0;28;0
WireConnection;80;1;81;0
WireConnection;290;0;321;0
WireConnection;290;1;292;5
WireConnection;294;0;300;0
WireConnection;96;0;94;0
WireConnection;96;1;97;0
WireConnection;337;0;334;0
WireConnection;337;1;129;0
WireConnection;363;0;364;0
WireConnection;363;1;362;0
WireConnection;27;0;337;0
WireConnection;27;1;80;0
WireConnection;293;0;290;0
WireConnection;293;1;294;0
WireConnection;98;0;96;0
WireConnection;98;2;99;0
WireConnection;288;0;27;0
WireConnection;288;1;293;0
WireConnection;100;1;98;0
WireConnection;101;0;363;0
WireConnection;366;0;100;5
WireConnection;366;2;288;0
WireConnection;366;3;338;0
WireConnection;366;4;339;0
WireConnection;366;9;101;0
ASEEND*/
//CHKSM=5C65D6A580C59F465300A397ECE94A5EBC3B0D8F