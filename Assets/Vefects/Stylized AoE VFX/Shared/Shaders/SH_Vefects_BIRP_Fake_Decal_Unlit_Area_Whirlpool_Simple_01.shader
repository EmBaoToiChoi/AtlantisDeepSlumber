// Made with Amplify Shader Editor v1.9.7.1
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Vefects/SH_Vefects_BIRP_Fake_Decal_Unlit_Area_Whirlpool_Simple_01"
{
	Properties
	{
		_Emissive("Emissive", Float) = 1
		[Space(33)][Header(Decal)][Space(13)]_DecalTexture("Decal Texture", 2D) = "white" {}
		_DecalTextureSelector("Decal Texture Selector", Vector) = (0,1,0,0)
		_DecalUVScale("Decal UV Scale", Vector) = (1,1,0,0)
		_DecalUVPanSpeed("Decal UV Pan Speed", Vector) = (0,0,0,0)
		_DecalRotation("Decal Rotation", Float) = 0
		_DecalScaleFromCenter("Decal Scale From Center", Float) = 1
		_DecalScaleFromCenterNonUniform("Decal Scale From Center Non Uniform", Vector) = (1,1,0,0)
		[Space(33)][Header(Decal Op)][Space(13)]_DecalOpTexture("Decal Op Texture", 2D) = "white" {}
		_DecalOpTextureSelector("Decal Op Texture Selector", Vector) = (0,1,0,0)
		[Space(33)][Header(Fake Decal)][Space(13)]_FakeDecalDepthFade("Fake Decal Depth Fade", Float) = 1
		_FakeDecalDepthFadeErosion("Fake Decal Depth Fade Erosion", Float) = 0
		_FakeDecalDepthFadeErosionSmoothness("Fake Decal Depth Fade Erosion Smoothness", Float) = 0.1
		_ErosionSmoothness("Erosion Smoothness", Float) = 0.05
		_OpacityBoost("Opacity Boost", Float) = 1
		[Space(33)][Header(Radial)][Space(13)]_RadialUVDistortNoise("Radial UV Distort Noise", 2D) = "white" {}
		_RadialUVDistortScale("Radial UV Distort Scale", Vector) = (1,1,0,0)
		_RadialUVDistortSpeed("Radial UV Distort Speed", Vector) = (0.1,0.01,0,0)
		_RadialUVDistortIntensity("Radial UV Distort Intensity", Float) = 0.1
		[Space(33)][Header(LUT)][Space(13)]_LUT("LUT", 2D) = "white" {}
		_LUTAmplitude("LUT Amplitude", Float) = 1
		_LUTOffset("LUT Offset", Float) = 0
		_LUTPanSpeed("LUT Pan Speed", Float) = 0
		_LUTErosionSmoothness("LUT Erosion Smoothness", Float) = 1
		_TwistIntensity("Twist Intensity", Float) = -0.42
		_PolarCoordinatesRadialScale("Polar Coordinates Radial Scale", Float) = 0.5
		_PolarCoordinatesLengthScale("Polar Coordinates Length Scale", Float) = 1
		_PolarCoordinatesPanSpeed("Polar Coordinates Pan Speed", Vector) = (0.3,0,0,0)
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
		uniform float _LUTErosionSmoothness;
		uniform sampler2D _DecalTexture;
		uniform float2 _DecalUVPanSpeed;
		uniform float _PolarCoordinatesRadialScale;
		uniform float2 _DecalScaleFromCenterNonUniform;
		uniform float _DecalScaleFromCenter;
		uniform float _DecalRotation;
		uniform float _PolarCoordinatesLengthScale;
		uniform float2 _PolarCoordinatesPanSpeed;
		uniform float _TwistIntensity;
		uniform sampler2D _RadialUVDistortNoise;
		uniform float2 _RadialUVDistortScale;
		uniform float2 _RadialUVDistortSpeed;
		uniform float _RadialUVDistortIntensity;
		uniform float2 _DecalUVScale;
		uniform float4 _DecalTextureSelector;
		uniform float _LUTAmplitude;
		uniform float _LUTOffset;
		uniform float _Emissive;
		uniform float _ErosionSmoothness;
		uniform sampler2D _DecalOpTexture;
		uniform float4 _DecalOpTexture_ST;
		uniform float4 _DecalOpTextureSelector;
		uniform float _OpacityBoost;
		uniform float _FakeDecalDepthFadeErosion;
		uniform float _FakeDecalDepthFadeErosionSmoothness;
		UNITY_DECLARE_DEPTH_TEXTURE( _CameraDepthTexture );
		uniform float4 _CameraDepthTexture_TexelSize;
		uniform float _FakeDecalDepthFade;

		inline half4 LightingUnlit( SurfaceOutput s, half3 lightDir, half atten )
		{
			return half4 ( 0, 0, 0, s.Alpha );
		}

		void surf( Input i , inout SurfaceOutput o )
		{
			float2 temp_cast_0 = (_LUTPanSpeed).xx;
			float eros79 = i.uv_texcoord.w;
			float2 _Vector1 = float2(0.5,0.5);
			float randomRotate57 = i.uv2_texcoord2.x;
			float cos52 = cos( ( ( ( _DecalRotation + randomRotate57 ) * ( 2.0 * UNITY_PI ) ) / 360.0 ) );
			float sin52 = sin( ( ( ( _DecalRotation + randomRotate57 ) * ( 2.0 * UNITY_PI ) ) / 360.0 ) );
			float2 rotator52 = mul( ( ( ( i.uv_texcoord.xy - _Vector1 ) / ( _DecalScaleFromCenterNonUniform * _DecalScaleFromCenter ) ) + _Vector1 ) - float2( 0.5,0.5 ) , float2x2( cos52 , -sin52 , sin52 , cos52 )) + float2( 0.5,0.5 );
			float2 decalUV87 = rotator52;
			float2 temp_output_34_0_g9 = ( decalUV87 - float2( 0.5,0.5 ) );
			float2 break39_g9 = temp_output_34_0_g9;
			float2 appendResult50_g9 = (float2(( _PolarCoordinatesRadialScale * ( length( temp_output_34_0_g9 ) * 2.0 ) ) , ( ( atan2( break39_g9.x , break39_g9.y ) * ( 1.0 / 6.28318548202515 ) ) * _PolarCoordinatesLengthScale )));
			float2 panner326 = ( 1.0 * _Time.y * _PolarCoordinatesPanSpeed + float2( 0,0 ));
			float2 break53_g9 = appendResult50_g9;
			float2 twistedUVs342 = ( ( appendResult50_g9 + panner326 ) + ( break53_g9.x * _TwistIntensity ) );
			float2 break234 = twistedUVs342;
			float2 appendResult183 = (float2(( (_RadialUVDistortScale).x * break234.x ) , ( break234.y * (_RadialUVDistortScale).y )));
			float2 panner165 = ( ( (_RadialUVDistortSpeed).x * _Time.y ) * float2( 1,0 ) + twistedUVs342);
			float2 panner166 = ( ( _Time.y * (_RadialUVDistortSpeed).y ) * float2( 0,1 ) + twistedUVs342);
			float2 appendResult182 = (float2((panner165).x , (panner166).y));
			float2 UV_Dist345 = ( (tex2D( _RadialUVDistortNoise, ( appendResult183 + appendResult182 ) )).rg * _RadialUVDistortIntensity );
			float2 panner247 = ( 1.0 * _Time.y * _DecalUVPanSpeed + ( ( twistedUVs342 + UV_Dist345 ) * _DecalUVScale ));
			float dotResult104 = dot( tex2D( _DecalTexture, panner247 ) , _DecalTextureSelector );
			float smoothstepResult93 = smoothstep( eros79 , ( eros79 + _LUTErosionSmoothness ) , saturate( dotResult104 ));
			float LUTOffset109 = i.uv2_texcoord2.y;
			float2 temp_cast_2 = (( ( saturate( smoothstepResult93 ) * _LUTAmplitude ) + ( _LUTOffset + LUTOffset109 ) )).xx;
			float2 panner98 = ( 1.0 * _Time.y * temp_cast_0 + temp_cast_2);
			float em78 = i.uv_texcoord.z;
			o.Emission = ( ( tex2D( _LUT, panner98 ).rgb * (i.vertexColor).rgb ) * ( _Emissive * em78 ) );
			float2 uv_DecalOpTexture = i.uv_texcoord * _DecalOpTexture_ST.xy + _DecalOpTexture_ST.zw;
			float dotResult107 = dot( tex2D( _DecalOpTexture, uv_DecalOpTexture ) , _DecalOpTextureSelector );
			float smoothstepResult82 = smoothstep( eros79 , ( eros79 + _ErosionSmoothness ) , saturate( dotResult107 ));
			float4 ase_screenPos = float4( i.screenPos.xyz , i.screenPos.w + 0.00000000001 );
			float4 ase_screenPosNorm = ase_screenPos / ase_screenPos.w;
			ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
			float screenDepth369 = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE( _CameraDepthTexture, ase_screenPosNorm.xy ));
			float distanceDepth369 = saturate( ( screenDepth369 - LinearEyeDepth( ase_screenPosNorm.z ) ) / ( _FakeDecalDepthFade ) );
			float smoothstepResult372 = smoothstep( _FakeDecalDepthFadeErosion , ( _FakeDecalDepthFadeErosion + _FakeDecalDepthFadeErosionSmoothness ) , distanceDepth369);
			o.Alpha = saturate( ( saturate( ( saturate( ( saturate( saturate( smoothstepResult82 ) ) * _OpacityBoost ) ) * i.vertexColor.a ) ) * ( 1.0 - saturate( smoothstepResult372 ) ) ) );
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
Node;AmplifyShaderEditor.TexCoordVertexDataNode;77;-6272,-384;Inherit;False;1;4;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.CommentaryNode;69;-6208,-48;Inherit;False;1205.095;817.495;Decal Rotator;11;52;60;53;61;68;58;54;59;75;76;87;Decal Rotator;0,0,0,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;357;-6192,1488;Inherit;False;964;530.95;Decal Scale From Center;8;365;364;363;362;361;360;359;358;Decal Scale From Center;0,0,0,1;0;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;57;-5888,-384;Inherit;False;randomRotate;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;59;-6016,512;Inherit;False;Constant;_Float1;Float 1;12;0;Create;True;0;0;0;False;0;False;2;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;54;-6016,384;Inherit;False;Property;_DecalRotation;Decal Rotation;6;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;76;-5760,256;Inherit;False;57;randomRotate;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;358;-5888,1664;Inherit;False;Constant;_Vector1;Vector 1;16;0;Create;True;0;0;0;False;0;False;0.5,0.5;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.TextureCoordinatesNode;359;-6144,1536;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector2Node;360;-5632,1776;Inherit;False;Property;_DecalScaleFromCenterNonUniform;Decal Scale From Center Non Uniform;8;0;Create;True;0;0;0;False;0;False;1,1;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.RangedFloatNode;361;-5632,1904;Inherit;False;Property;_DecalScaleFromCenter;Decal Scale From Center;7;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.PiNode;58;-5760,512;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;75;-5760,384;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;362;-5888,1536;Inherit;False;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;363;-5632,1648;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;68;-5504,384;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;61;-5248,512;Inherit;False;Constant;_Float2;Float 2;12;0;Create;True;0;0;0;False;0;False;360;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;364;-5632,1536;Inherit;False;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector2Node;53;-6144,128;Inherit;False;Constant;_Vector0;Vector 0;11;0;Create;True;0;0;0;False;0;False;0.5,0.5;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.SimpleDivideOpNode;60;-5248,384;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;365;-5376,1536;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RotatorNode;52;-5760,16;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.CommentaryNode;343;-8960,-3328;Inherit;False;1799.189;635.2467;Twist - Lush you're a genius! :O;12;342;327;326;339;337;334;324;312;309;313;315;341;Twist;0,0,0,1;0;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;87;-5248,0;Inherit;False;decalUV;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;341;-8832,-3200;Inherit;False;87;decalUV;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;315;-8832,-2816;Inherit;False;Property;_PolarCoordinatesLengthScale;Polar Coordinates Length Scale;27;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;313;-8832,-2944;Inherit;False;Property;_PolarCoordinatesRadialScale;Polar Coordinates Radial Scale;26;0;Create;True;0;0;0;False;0;False;0.5;0.5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;309;-8832,-3072;Inherit;False;Constant;_Vector7;Vector 5;38;0;Create;True;0;0;0;False;0;False;0.5,0.5;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.Vector2Node;327;-8064,-2944;Inherit;False;Property;_PolarCoordinatesPanSpeed;Polar Coordinates Pan Speed;28;0;Create;True;0;0;0;False;0;False;0.3,0;0.3,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.FunctionNode;312;-8448,-3200;Inherit;False;Polar Coordinates;-1;;9;7dab8e02884cf104ebefaa2e788e4162;0;4;1;FLOAT2;0,0;False;2;FLOAT2;0.5,0.5;False;3;FLOAT;1;False;4;FLOAT;1;False;3;FLOAT2;0;FLOAT;55;FLOAT;56
Node;AmplifyShaderEditor.RangedFloatNode;339;-7680,-2944;Inherit;False;Property;_TwistIntensity;Twist Intensity;25;0;Create;True;0;0;0;False;0;False;-0.42;-0.42;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;326;-8064,-3072;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.CommentaryNode;144;-8960,-2560;Inherit;False;3458.605;1282.172;Radial Distort;25;231;345;194;191;193;189;186;183;182;178;177;176;175;234;168;167;166;165;161;159;158;153;152;151;148;Radial Distort;0,0,0,1;0;0
Node;AmplifyShaderEditor.SimpleAddOpNode;324;-8064,-3200;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;337;-7680,-3072;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;148;-8912,-1744;Inherit;False;Property;_RadialUVDistortSpeed;Radial UV Distort Speed;18;0;Create;True;0;0;0;False;0;False;0.1,0.01;0.1,0.01;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.SimpleAddOpNode;334;-7680,-3200;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ComponentMaskNode;151;-8528,-1488;Inherit;False;False;True;True;True;1;0;FLOAT2;0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ComponentMaskNode;152;-8528,-1744;Inherit;False;True;False;True;True;1;0;FLOAT2;0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleTimeNode;153;-8528,-1616;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;342;-7424,-3200;Inherit;False;twistedUVs;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;158;-8272,-1744;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;159;-8272,-1488;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;161;-8912,-2512;Inherit;False;Property;_RadialUVDistortScale;Radial UV Distort Scale;17;0;Create;True;0;0;0;False;0;False;1,1;1,1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.GetLocalVarNode;231;-8016,-2256;Inherit;False;342;twistedUVs;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.PannerNode;165;-7760,-1744;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;1,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.PannerNode;166;-7760,-1472;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,1;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ComponentMaskNode;167;-8528,-2512;Inherit;False;True;False;True;True;1;0;FLOAT2;0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ComponentMaskNode;168;-8528,-2256;Inherit;False;False;True;True;True;1;0;FLOAT2;0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.BreakToComponentsNode;234;-7760,-2256;Inherit;False;FLOAT2;1;0;FLOAT2;0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.ComponentMaskNode;175;-7504,-1472;Inherit;False;False;True;True;True;1;0;FLOAT2;0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ComponentMaskNode;176;-7504,-1744;Inherit;False;True;False;True;True;1;0;FLOAT2;0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;177;-7504,-2128;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;178;-7520,-2512;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;182;-7248,-1616;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;183;-7248,-2384;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;186;-7120,-2000;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SamplerNode;189;-6864,-2000;Inherit;True;Property;_RadialUVDistortNoise;Radial UV Distort Noise;16;0;Create;True;0;0;0;False;3;Space(33);Header(Radial);Space(13);False;-1;801b4be3075bc4840b14161c01e73734;801b4be3075bc4840b14161c01e73734;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.RangedFloatNode;193;-6224,-2128;Inherit;False;Property;_RadialUVDistortIntensity;Radial UV Distort Intensity;19;0;Create;True;0;0;0;False;0;False;0.1;0.1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ComponentMaskNode;191;-6480,-2000;Inherit;False;True;True;False;False;1;0;COLOR;0,0,0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;194;-6224,-2000;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;345;-5840,-2000;Inherit;False;UV Dist;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;295;-4608,-256;Inherit;False;342;twistedUVs;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;346;-4352,-384;Inherit;False;345;UV Dist;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector2Node;250;-4608,128;Inherit;False;Property;_DecalUVScale;Decal UV Scale;4;0;Create;True;0;0;0;False;0;False;1,1;1,1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.SimpleAddOpNode;347;-4352,-256;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TexCoordVertexDataNode;56;-6272,-640;Inherit;False;0;4;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;249;-4608,0;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector2Node;248;-4224,128;Inherit;False;Property;_DecalUVPanSpeed;Decal UV Pan Speed;5;0;Create;True;0;0;0;False;0;False;0,0;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.Vector4Node;108;-3456,896;Inherit;False;Property;_DecalOpTextureSelector;Decal Op Texture Selector;10;0;Create;True;0;0;0;False;0;False;0,1,0,0;0,1,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;86;-3840,768;Inherit;True;Property;_DecalOpTexture;Decal Op Texture;9;0;Create;True;0;0;0;False;3;Space(33);Header(Decal Op);Space(13);False;-1;33fec401cab050343a3ee851eca78e34;33fec401cab050343a3ee851eca78e34;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.RegisterLocalVarNode;79;-5888,-512;Inherit;False;eros;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;247;-4224,0;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DotProductOpNode;107;-3456,768;Inherit;False;2;0;COLOR;0,0,0,0;False;1;FLOAT4;0,0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;85;-3456,1408;Inherit;False;Property;_ErosionSmoothness;Erosion Smoothness;14;0;Create;True;0;0;0;False;0;False;0.05;0.05;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;83;-3456,1152;Inherit;False;79;eros;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector4Node;103;-3456,128;Inherit;False;Property;_DecalTextureSelector;Decal Texture Selector;3;0;Create;True;0;0;0;False;0;False;0,1,0,0;0,1,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;24;-3840,0;Inherit;True;Property;_DecalTexture;Decal Texture;2;0;Create;True;0;0;0;False;3;Space(33);Header(Decal);Space(13);False;-1;788d74a2951ba514fa76781ea2676e75;788d74a2951ba514fa76781ea2676e75;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SimpleAddOpNode;84;-3200,1280;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;106;-3200,768;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DotProductOpNode;104;-3456,0;Inherit;False;2;0;COLOR;0,0,0,0;False;1;FLOAT4;0,0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;90;-3456,640;Inherit;False;Property;_LUTErosionSmoothness;LUT Erosion Smoothness;24;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;91;-3456,384;Inherit;False;79;eros;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SmoothstepOpNode;82;-3072,1152;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;105;-3200,0;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;92;-3200,512;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;366;-1840,1488;Inherit;False;1636;418.95;Fake Decal Depth Fade;9;375;374;373;372;371;370;369;368;367;Fake Decal Depth Fade;0,0,0,1;0;0
Node;AmplifyShaderEditor.SaturateNode;141;-2816,1152;Inherit;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SmoothstepOpNode;93;-3072,384;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;109;-5888,-256;Inherit;False;LUTOffset;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;23;-1408,640;Inherit;False;Property;_OpacityBoost;Opacity Boost;15;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;101;-1664,1152;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;367;-1792,1792;Inherit;False;Property;_FakeDecalDepthFadeErosionSmoothness;Fake Decal Depth Fade Erosion Smoothness;13;0;Create;True;0;0;0;False;0;False;0.1;0.1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;368;-1792,1536;Inherit;False;Property;_FakeDecalDepthFade;Fake Decal Depth Fade;11;0;Create;True;0;0;0;False;3;Space(33);Header(Fake Decal);Space(13);False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;370;-1792,1664;Inherit;False;Property;_FakeDecalDepthFadeErosion;Fake Decal Depth Fade Erosion;12;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;102;-2816,384;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;97;-1920,-1152;Inherit;False;Property;_LUTOffset;LUT Offset;22;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;95;-2176,-1152;Inherit;False;Property;_LUTAmplitude;LUT Amplitude;21;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;257;-1920,-896;Inherit;False;109;LUTOffset;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;26;-1408,512;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DepthFade;369;-1536,1536;Inherit;False;True;True;False;2;1;FLOAT3;0,0,0;False;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;371;-1152,1664;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;94;-2176,-1280;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;256;-1920,-1024;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.VertexColorNode;39;-3840,-640;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SaturateNode;73;-1152,512;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SmoothstepOpNode;372;-1152,1536;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;96;-1920,-1280;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;99;-1664,-1152;Inherit;False;Property;_LUTPanSpeed;LUT Pan Speed;23;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;373;-896,1536;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;74;-896,256;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;78;-5888,-640;Inherit;False;em;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;98;-1664,-1280;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.OneMinusNode;374;-640,1536;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;29;-640,256;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;28;-640,-384;Inherit;False;Property;_Emissive;Emissive;1;0;Create;True;0;0;0;False;0;False;1;13;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;81;-640,-256;Inherit;False;78;em;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.ComponentMaskNode;129;-3584,-640;Inherit;False;True;True;True;False;1;0;COLOR;0,0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SamplerNode;100;-1408,-1280;Inherit;True;Property;_LUT;LUT;20;0;Create;True;0;0;0;False;3;Space(33);Header(LUT);Space(13);False;-1;f6092ac86f74df947bf3a96e82ee5fbd;f6092ac86f74df947bf3a96e82ee5fbd;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.CommentaryNode;25;576,-48;Inherit;False;1241;165;Lush was here! <3;5;38;37;36;35;34;Lush was here! <3;0.4629898,0.2327043,1,1;0;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;375;-384,1536;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;80;-640,-128;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;128;-896,128;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;34;896,0;Inherit;False;Property;_Src;Src;30;0;Create;True;0;0;0;True;0;False;5;5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;35;1152,0;Inherit;False;Property;_Dst;Dst;31;0;Create;True;0;0;0;True;0;False;10;10;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;36;1408,0;Inherit;False;Property;_ZWrite;ZWrite;32;0;Create;True;0;0;0;True;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;37;1664,0;Inherit;False;Property;_ZTest;ZTest;33;0;Create;True;0;0;0;True;0;False;2;2;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;38;640,0;Inherit;False;Property;_Cull;Cull;29;0;Create;True;0;0;0;True;3;Space(33);Header(AR);Space(13);False;2;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;27;-640,128;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SaturateNode;376;-256,256;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;378;0,0;Float;False;True;-1;3;ASEMaterialInspector;0;0;Unlit;Vefects/SH_Vefects_BIRP_Fake_Decal_Unlit_Area_Whirlpool_Simple_01;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;Back;0;True;_ZWrite;0;True;_ZTest;False;0;False;;0;False;;False;0;Custom;0.5;True;True;0;False;Transparent;;Transparent;All;12;all;True;True;True;True;0;False;;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;True;1;5;True;_Src;10;True;_Dst;0;0;False;;0;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;0;-1;-1;-1;0;False;0;0;True;_Cull;-1;0;False;;0;0;0;False;0.1;False;;0;False;;False;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;16;FLOAT4;0,0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
Node;AmplifyShaderEditor.CommentaryNode;51;-7936,-1024;Inherit;False;768.0875;257.7429;Check Clevender.com courses to learn about shaders <3;0;Check Clevender.com courses to learn about shaders <3;1,0,0.5501842,1;0;0
WireConnection;57;0;77;1
WireConnection;58;0;59;0
WireConnection;75;0;54;0
WireConnection;75;1;76;0
WireConnection;362;0;359;0
WireConnection;362;1;358;0
WireConnection;363;0;360;0
WireConnection;363;1;361;0
WireConnection;68;0;75;0
WireConnection;68;1;58;0
WireConnection;364;0;362;0
WireConnection;364;1;363;0
WireConnection;60;0;68;0
WireConnection;60;1;61;0
WireConnection;365;0;364;0
WireConnection;365;1;358;0
WireConnection;52;0;365;0
WireConnection;52;1;53;0
WireConnection;52;2;60;0
WireConnection;87;0;52;0
WireConnection;312;1;341;0
WireConnection;312;2;309;0
WireConnection;312;3;313;0
WireConnection;312;4;315;0
WireConnection;326;2;327;0
WireConnection;324;0;312;0
WireConnection;324;1;326;0
WireConnection;337;0;312;55
WireConnection;337;1;339;0
WireConnection;334;0;324;0
WireConnection;334;1;337;0
WireConnection;151;0;148;0
WireConnection;152;0;148;0
WireConnection;342;0;334;0
WireConnection;158;0;152;0
WireConnection;158;1;153;0
WireConnection;159;0;153;0
WireConnection;159;1;151;0
WireConnection;165;0;231;0
WireConnection;165;1;158;0
WireConnection;166;0;231;0
WireConnection;166;1;159;0
WireConnection;167;0;161;0
WireConnection;168;0;161;0
WireConnection;234;0;231;0
WireConnection;175;0;166;0
WireConnection;176;0;165;0
WireConnection;177;0;234;1
WireConnection;177;1;168;0
WireConnection;178;0;167;0
WireConnection;178;1;234;0
WireConnection;182;0;176;0
WireConnection;182;1;175;0
WireConnection;183;0;178;0
WireConnection;183;1;177;0
WireConnection;186;0;183;0
WireConnection;186;1;182;0
WireConnection;189;1;186;0
WireConnection;191;0;189;0
WireConnection;194;0;191;0
WireConnection;194;1;193;0
WireConnection;345;0;194;0
WireConnection;347;0;295;0
WireConnection;347;1;346;0
WireConnection;249;0;347;0
WireConnection;249;1;250;0
WireConnection;79;0;56;4
WireConnection;247;0;249;0
WireConnection;247;2;248;0
WireConnection;107;0;86;0
WireConnection;107;1;108;0
WireConnection;24;1;247;0
WireConnection;84;0;83;0
WireConnection;84;1;85;0
WireConnection;106;0;107;0
WireConnection;104;0;24;0
WireConnection;104;1;103;0
WireConnection;82;0;106;0
WireConnection;82;1;83;0
WireConnection;82;2;84;0
WireConnection;105;0;104;0
WireConnection;92;0;91;0
WireConnection;92;1;90;0
WireConnection;141;0;82;0
WireConnection;93;0;105;0
WireConnection;93;1;91;0
WireConnection;93;2;92;0
WireConnection;109;0;77;2
WireConnection;101;0;141;0
WireConnection;102;0;93;0
WireConnection;26;0;101;0
WireConnection;26;1;23;0
WireConnection;369;0;368;0
WireConnection;371;0;370;0
WireConnection;371;1;367;0
WireConnection;94;0;102;0
WireConnection;94;1;95;0
WireConnection;256;0;97;0
WireConnection;256;1;257;0
WireConnection;73;0;26;0
WireConnection;372;0;369;0
WireConnection;372;1;370;0
WireConnection;372;2;371;0
WireConnection;96;0;94;0
WireConnection;96;1;256;0
WireConnection;373;0;372;0
WireConnection;74;0;73;0
WireConnection;74;1;39;4
WireConnection;78;0;56;3
WireConnection;98;0;96;0
WireConnection;98;2;99;0
WireConnection;374;0;373;0
WireConnection;29;0;74;0
WireConnection;129;0;39;0
WireConnection;100;1;98;0
WireConnection;375;0;29;0
WireConnection;375;1;374;0
WireConnection;80;0;28;0
WireConnection;80;1;81;0
WireConnection;128;0;100;5
WireConnection;128;1;129;0
WireConnection;27;0;128;0
WireConnection;27;1;80;0
WireConnection;376;0;375;0
WireConnection;378;2;27;0
WireConnection;378;9;376;0
ASEEND*/
//CHKSM=EA165C3CA087ADC59FBBDE86AFCF9868A0BA978E