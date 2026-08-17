// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "SH_Vefects_VFX_Fresnel"
{
	Properties
	{
		_Noise_Color_Texture("Noise_Color_Texture", 2D) = "white" {}
		_Noise_01_Texture("Noise_01_Texture", 2D) = "white" {}
		_TextureSample0("Texture Sample 0", 2D) = "white" {}
		_Noise_02_Texture("Noise_02_Texture", 2D) = "white" {}
		_NoiseDistortion_Texture("NoiseDistortion_Texture", 2D) = "white" {}
		_NoiseColor_Scale("NoiseColor_Scale", Vector) = (1,1,0,0)
		_Noise_01_Scale("Noise_01_Scale", Vector) = (0.8,0.8,0,0)
		_Noise_02_Scale("Noise_02_Scale", Vector) = (1,1,0,0)
		_NoiseDistortion_Scale("NoiseDistortion_Scale", Vector) = (1,1,0,0)
		_Noise_01_Speed("Noise_01_Speed", Vector) = (0.5,0.5,0,0)
		_NoiseColor_Speed("NoiseColor_Speed", Vector) = (0,0,0,0)
		_Noise_02_Speed("Noise_02_Speed", Vector) = (-0.2,0.4,0,0)
		_NoiseColor_Power("NoiseColor_Power", Float) = 1
		_Vector0("Vector 0", Vector) = (1,1,0,0)
		_NoiseColor_Intensity("NoiseColor_Intensity", Float) = 1
		_Mask_Offset("Mask_Offset", Vector) = (0,0,0,0)
		_NoiseDistortion_Speed("NoiseDistortion_Speed", Vector) = (0.2,0.25,0,0)
		_NoiseDistortion_Intensity("NoiseDistortion_Intensity", Float) = 1
		_Opacity_Boost("Opacity_Boost", Float) = 10
		_Mask_Multiply("Mask_Multiply", Float) = 1
		_Opacity_Power("Opacity_Power", Float) = 1
		_Fresnel_Scale("Fresnel_Scale", Float) = 1
		_Fresnel_Power("Fresnel_Power", Float) = 5
		_Color_2("Color_2", Color) = (1,1,1,0)
		_Color_1("Color_1", Color) = (1,1,1,0)
		_Mask_Power("Mask_Power", Float) = 1
		_Opacity_DepthFade_Intensity("Opacity_DepthFade_Intensity", Float) = 1
		_DepthFade_Distance("DepthFade_Distance", Float) = 1
		_DistortionMask("DistortionMask", Float) = 0
		_Global_Speed("Global_Speed", Float) = 1
		_Dissolve("Dissolve", Float) = 0
		_Emissive_Intensity("Emissive_Intensity", Float) = 1
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Transparent"  "Queue" = "Transparent+0" "IgnoreProjector" = "True" "IsEmissive" = "true"  }
		Cull Back
		CGINCLUDE
		#include "UnityShaderVariables.cginc"
		#include "UnityCG.cginc"
		#include "UnityPBSLighting.cginc"
		#include "Lighting.cginc"
		#pragma target 3.0
		#undef TRANSFORM_TEX
		#define TRANSFORM_TEX(tex,name) float4(tex.xy * name##_ST.xy + name##_ST.zw, tex.z, tex.w)
		struct Input
		{
			float4 uv_texcoord;
			float4 vertexColor : COLOR;
			float3 worldPos;
			float3 worldNormal;
			float4 screenPos;
		};

		uniform float4 _Color_1;
		uniform float4 _Color_2;
		uniform sampler2D _Noise_Color_Texture;
		uniform sampler2D _NoiseDistortion_Texture;
		uniform float _Global_Speed;
		uniform float2 _NoiseDistortion_Speed;
		uniform float2 _NoiseDistortion_Scale;
		uniform float _NoiseDistortion_Intensity;
		uniform float2 _NoiseColor_Speed;
		uniform float2 _NoiseColor_Scale;
		uniform float _NoiseColor_Intensity;
		uniform float _NoiseColor_Power;
		uniform float _Emissive_Intensity;
		uniform float _Fresnel_Scale;
		uniform float _Fresnel_Power;
		UNITY_DECLARE_DEPTH_TEXTURE( _CameraDepthTexture );
		uniform float4 _CameraDepthTexture_TexelSize;
		uniform float _DepthFade_Distance;
		uniform sampler2D _TextureSample0;
		uniform float _DistortionMask;
		uniform float2 _Vector0;
		uniform float2 _Mask_Offset;
		uniform float _Mask_Power;
		uniform float _Mask_Multiply;
		uniform sampler2D _Noise_01_Texture;
		uniform float2 _Noise_01_Speed;
		uniform float2 _Noise_01_Scale;
		uniform sampler2D _Noise_02_Texture;
		uniform float2 _Noise_02_Speed;
		uniform float2 _Noise_02_Scale;
		uniform float _Opacity_Power;
		uniform float _Opacity_Boost;
		uniform float _Dissolve;
		uniform float _Opacity_DepthFade_Intensity;

		inline half4 LightingUnlit( SurfaceOutput s, half3 lightDir, half atten )
		{
			return half4 ( 0, 0, 0, s.Alpha );
		}

		void surf( Input i , inout SurfaceOutput o )
		{
			float global_speed178 = ( _Global_Speed * _Time.y );
			float2 uvs_TexCoord30 = i.uv_texcoord;
			uvs_TexCoord30.xy = i.uv_texcoord.xy * _NoiseDistortion_Scale;
			float2 panner79 = ( global_speed178 * _NoiseDistortion_Speed + uvs_TexCoord30.xy);
			float Distortion64 = ( ( tex2D( _NoiseDistortion_Texture, panner79 ).r * 0.1 ) * _NoiseDistortion_Intensity );
			float2 uvs_TexCoord246 = i.uv_texcoord;
			uvs_TexCoord246.xy = i.uv_texcoord.xy * _NoiseColor_Scale;
			float2 panner248 = ( 1.0 * _Time.y * _NoiseColor_Speed + uvs_TexCoord246.xy);
			float clampResult235 = clamp( pow( ( tex2D( _Noise_Color_Texture, ( Distortion64 + panner248 ) ).r * _NoiseColor_Intensity ) , _NoiseColor_Power ) , 0.0 , 1.0 );
			float3 lerpResult239 = lerp( (_Color_1).rgb , (_Color_2).rgb , clampResult235);
			o.Emission = ( ( lerpResult239 * (i.vertexColor).rgb ) * _Emissive_Intensity );
			float3 ase_worldPos = i.worldPos;
			float3 ase_worldViewDir = normalize( UnityWorldSpaceViewDir( ase_worldPos ) );
			float3 ase_worldNormal = i.worldNormal;
			float fresnelNdotV167 = dot( ase_worldNormal, ase_worldViewDir );
			float fresnelNode167 = ( i.uv_texcoord.z + _Fresnel_Scale * pow( 1.0 - fresnelNdotV167, _Fresnel_Power ) );
			float4 ase_screenPos = float4( i.screenPos.xyz , i.screenPos.w + 0.00000000001 );
			float4 ase_screenPosNorm = ase_screenPos / ase_screenPos.w;
			ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
			float screenDepth137 = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE( _CameraDepthTexture, ase_screenPosNorm.xy ));
			float distanceDepth137 = abs( ( screenDepth137 - LinearEyeDepth( ase_screenPosNorm.z ) ) / ( _DepthFade_Distance ) );
			float clampResult136 = clamp( ( 1.0 - distanceDepth137 ) , 0.0 , 1.0 );
			float2 appendResult216 = (float2(0.0 , 0.0));
			float2 uvs_TexCoord215 = i.uv_texcoord;
			uvs_TexCoord215.xy = i.uv_texcoord.xy * _Vector0 + _Mask_Offset;
			float2 panner218 = ( global_speed178 * appendResult216 + uvs_TexCoord215.xy);
			float2 uvs_TexCoord26 = i.uv_texcoord;
			uvs_TexCoord26.xy = i.uv_texcoord.xy * _Noise_01_Scale;
			float2 panner78 = ( global_speed178 * _Noise_01_Speed + uvs_TexCoord26.xy);
			float2 uvs_TexCoord58 = i.uv_texcoord;
			uvs_TexCoord58.xy = i.uv_texcoord.xy * _Noise_02_Scale;
			float2 panner80 = ( global_speed178 * _Noise_02_Speed + uvs_TexCoord58.xy);
			float clampResult169 = clamp( ( ( fresnelNode167 + clampResult136 ) * ( pow( ( saturate( ( ( tex2D( _TextureSample0, ( ( Distortion64 * _DistortionMask ) + panner218 ) ).r * _Mask_Power ) * _Mask_Multiply ) ) * ( tex2D( _Noise_01_Texture, ( Distortion64 + panner78 ) ).r * tex2D( _Noise_02_Texture, ( Distortion64 + panner80 ) ).r ) ) , _Opacity_Power ) * _Opacity_Boost ) ) , 0.0 , 1.0 );
			float screenDepth261 = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE( _CameraDepthTexture, ase_screenPosNorm.xy ));
			float distanceDepth261 = abs( ( screenDepth261 - LinearEyeDepth( ase_screenPosNorm.z ) ) / ( _Opacity_DepthFade_Intensity ) );
			float clampResult262 = clamp( distanceDepth261 , 0.0 , 1.0 );
			o.Alpha = ( i.vertexColor.a * saturate( ( ( clampResult169 - ( i.uv_texcoord.w + _Dissolve ) ) * clampResult262 ) ) );
		}

		ENDCG
		CGPROGRAM
		#pragma surface surf Unlit alpha:fade keepalpha fullforwardshadows 

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
				float4 customPack1 : TEXCOORD1;
				float3 worldPos : TEXCOORD2;
				float4 screenPos : TEXCOORD3;
				float3 worldNormal : TEXCOORD4;
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
				o.customPack1.xyzw = customInputData.uv_texcoord;
				o.customPack1.xyzw = v.texcoord;
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
Version=18921
0;21;2546;1168;528.5303;-106.3904;1;True;True
Node;AmplifyShaderEditor.RangedFloatNode;176;-2857.664,-223.5901;Inherit;False;Property;_Global_Speed;Global_Speed;32;0;Create;True;0;0;0;False;0;False;1;2;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleTimeNode;175;-2855.096,-146.8961;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;77;-3334.254,-921.8143;Inherit;False;2038.752;588.1584;DISTORTION;11;64;50;44;52;45;43;79;32;30;31;180;;0.4669811,0.9251378,1,1;0;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;177;-2695.096,-233.8961;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;31;-3297.445,-822.2875;Inherit;False;Property;_NoiseDistortion_Scale;NoiseDistortion_Scale;11;0;Create;True;0;0;0;False;0;False;1,1;1,1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.RegisterLocalVarNode;178;-2526.432,-247.0851;Inherit;False;global_speed;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;32;-3113.602,-706.9553;Inherit;False;Property;_NoiseDistortion_Speed;NoiseDistortion_Speed;19;0;Create;True;0;0;0;False;0;False;0.2,0.25;0.5,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.GetLocalVarNode;180;-3061.534,-568.554;Inherit;False;178;global_speed;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;30;-3042.984,-842.979;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.PannerNode;79;-2789.843,-842.0295;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SamplerNode;43;-2603.995,-871.8142;Inherit;True;Property;_NoiseDistortion_Texture;NoiseDistortion_Texture;5;0;Create;True;0;0;0;False;0;False;-1;02b0d0a96eedb3d4fa5664350b35ba23;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;45;-2315.042,-712.4779;Inherit;False;Constant;_Float0;Float 0;8;0;Create;True;0;0;0;False;0;False;0.1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;52;-2243.284,-592.0629;Inherit;False;Property;_NoiseDistortion_Intensity;NoiseDistortion_Intensity;20;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;44;-2171.852,-839.3594;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0.1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;50;-1939.284,-840.0629;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;64;-1528.308,-846.3292;Inherit;False;Distortion;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;211;-3454.149,358.6586;Inherit;False;Property;_Vector0;Vector 0;16;0;Create;True;0;0;0;False;0;False;1,1;1,1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.Vector2Node;212;-3437.044,535.4034;Inherit;False;Property;_Mask_Offset;Mask_Offset;18;0;Create;True;0;0;0;False;0;False;0,0;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.RangedFloatNode;210;-3193.531,573.4682;Inherit;False;Constant;_Float1;Float 1;35;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;214;-2896.75,187.8543;Inherit;False;64;Distortion;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;216;-3033.104,607.374;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;215;-3221.104,426.0415;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0.28,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.GetLocalVarNode;217;-3050.52,731.7558;Inherit;False;178;global_speed;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;213;-2933.677,315.9085;Inherit;False;Property;_DistortionMask;DistortionMask;31;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;25;-3031.418,1133.028;Inherit;False;Property;_Noise_01_Scale;Noise_01_Scale;9;0;Create;True;0;0;0;False;0;False;0.8,0.8;8,8;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;219;-2681.971,272.8105;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;59;-3093.712,1610.341;Inherit;False;Property;_Noise_02_Scale;Noise_02_Scale;10;0;Create;True;0;0;0;False;0;False;1,1;6,6;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.PannerNode;218;-2830.237,425.4548;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;26;-2821.312,1114.058;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.GetLocalVarNode;174;-2818.963,1412.805;Inherit;False;178;global_speed;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;29;-2833.336,1254.466;Inherit;False;Property;_Noise_01_Speed;Noise_01_Speed;12;0;Create;True;0;0;0;False;0;False;0.5,0.5;-0.6,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.Vector2Node;60;-2860.942,1725.678;Inherit;False;Property;_Noise_02_Speed;Noise_02_Speed;14;0;Create;True;0;0;0;False;0;False;-0.2,0.4;-0.4,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.TextureCoordinatesNode;58;-2879.007,1591.355;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.GetLocalVarNode;179;-2838.737,1868.322;Inherit;False;178;global_speed;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;220;-2568.355,415.9608;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;69;-2504.447,966.5411;Inherit;False;64;Distortion;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;78;-2570.9,1161.388;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SamplerNode;222;-2369.855,402.0196;Inherit;True;Property;_TextureSample0;Texture Sample 0;3;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;221;-2180.011,636.7098;Inherit;False;Property;_Mask_Power;Mask_Power;28;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;80;-2620.299,1595.224;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;71;-2508.478,1376.261;Inherit;False;64;Distortion;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;61;-2378.415,1557.307;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;54;-2315.806,1160.52;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;223;-1996.37,431.7597;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;224;-1985.514,642.9995;Inherit;False;Property;_Mask_Multiply;Mask_Multiply;22;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;244;-848.9886,-1040.557;Inherit;False;Property;_NoiseColor_Scale;NoiseColor_Scale;8;0;Create;True;0;0;0;False;0;False;1,1;1,1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.SamplerNode;24;-2152.947,1142.384;Inherit;True;Property;_Noise_01_Texture;Noise_01_Texture;2;0;Create;True;0;0;0;False;0;False;-1;301564cad965bbb4da5072b5cf9406cb;72107694708b6174380b3be6b8c9e922;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;55;-2188.421,1534.771;Inherit;True;Property;_Noise_02_Texture;Noise_02_Texture;4;0;Create;True;0;0;0;False;0;False;-1;2f822f6dfbe26f44eadb94a1b925409d;9b27e61ccc45c4e43a10d8eeffe0417c;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;138;-1251.399,-169.8699;Inherit;False;Property;_DepthFade_Distance;DepthFade_Distance;30;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;225;-1814.82,431.6154;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;62;-1787.817,1354.08;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;246;-638.8827,-1059.527;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector2Node;245;-650.9066,-919.1185;Inherit;False;Property;_NoiseColor_Speed;NoiseColor_Speed;13;0;Create;True;0;0;0;False;0;False;0,0;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.SaturateNode;226;-1588.998,435.3677;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DepthFade;137;-926.4977,-189.8064;Inherit;False;True;False;True;2;1;FLOAT3;0,0,0;False;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;170;-825.6573,-393.5589;Inherit;False;Property;_Fresnel_Scale;Fresnel_Scale;24;0;Create;True;0;0;0;False;0;False;1;-3;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;171;-823.6573,-303.5588;Inherit;False;Property;_Fresnel_Power;Fresnel_Power;25;0;Create;True;0;0;0;False;0;False;5;0.5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TexCoordVertexDataNode;173;-866.1883,-638.0415;Inherit;False;0;4;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;227;-1245.528,421.3612;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;247;-319.0178,-1188.044;Inherit;False;64;Distortion;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;185;-872.0938,529.9515;Inherit;False;Property;_Opacity_Power;Opacity_Power;23;0;Create;True;0;0;0;False;0;False;1;6;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;182;-606.0945,-172.7847;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;248;-388.4706,-1012.197;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;249;-129.3766,-1029.065;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ClampOpNode;136;-428.2073,-171.2936;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.FresnelNode;167;-572.8887,-450.4738;Inherit;True;Standard;WorldNormal;ViewDir;False;False;5;0;FLOAT3;0,0,1;False;4;FLOAT3;0,0,0;False;1;FLOAT;0;False;2;FLOAT;1;False;3;FLOAT;5;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;76;-613.354,543.0144;Inherit;False;Property;_Opacity_Boost;Opacity_Boost;21;0;Create;True;0;0;0;False;0;False;10;2;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.PowerNode;184;-659.0938,420.9515;Inherit;False;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;234;134.5233,-578.7361;Inherit;False;Property;_NoiseColor_Intensity;NoiseColor_Intensity;17;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;75;-418.8394,411.6259;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;181;-257.6427,-357.5952;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;250;20.48226,-1039.201;Inherit;True;Property;_Noise_Color_Texture;Noise_Color_Texture;1;0;Create;True;0;0;0;False;0;False;-1;301564cad965bbb4da5072b5cf9406cb;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;190;343.5171,682.0491;Inherit;False;Property;_Opacity_DepthFade_Intensity;Opacity_DepthFade_Intensity;29;0;Create;True;0;0;0;False;0;False;1;2.5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;231;98.11817,780.4354;Inherit;False;Property;_Dissolve;Dissolve;33;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TexCoordVertexDataNode;232;-76.89971,613.7072;Inherit;False;0;4;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;236;425.3815,-589.1536;Inherit;False;Property;_NoiseColor_Power;NoiseColor_Power;15;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;168;-144.9373,383.5121;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;237;369.4706,-693.4391;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;229;278.5141,583.7817;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;233;447.5889,-1107.22;Inherit;False;Property;_Color_1;Color_1;27;0;Create;True;0;0;0;False;0;False;1,1,1,0;1,1,1,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ColorNode;238;436.1799,-887.565;Inherit;False;Property;_Color_2;Color_2;26;0;Create;True;0;0;0;False;0;False;1,1,1,0;1,1,1,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.DepthFade;261;650.1532,669.0126;Inherit;False;True;False;True;2;1;FLOAT3;0,0,0;False;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;169;132.1332,387.8163;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.PowerNode;240;631.101,-684.1012;Inherit;True;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;235;910.7948,-688.8664;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.VertexColorNode;8;986.7462,-158.6902;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ClampOpNode;262;923.4697,664.3904;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;228;516.5043,394.5929;Inherit;True;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ComponentMaskNode;242;728.7944,-882.8664;Inherit;False;True;True;True;False;1;0;COLOR;0,0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.ComponentMaskNode;241;708.7816,-1079.047;Inherit;False;True;True;True;False;1;0;COLOR;0,0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.ComponentMaskNode;23;1264.2,-157.8309;Inherit;False;True;True;True;False;1;0;COLOR;0,0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;189;1061.689,466.9366;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;239;1064.571,-995.2856;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.WireNode;139;1109.237,221.4904;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;193;1380.448,5.30018;Inherit;False;Property;_Emissive_Intensity;Emissive_Intensity;34;0;Create;True;0;0;0;False;0;False;1;20;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;53;1277.826,393.5582;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;251;1483.378,-176.123;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode;70;-4123.585,-411.9106;Inherit;False;64;Distortion;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;7;-4659.224,-259.5958;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector2Node;166;-4528.586,-115.5964;Inherit;False;Property;_Mask_Speed;Mask_Speed;7;0;Create;True;0;0;0;False;0;False;0,0;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.SamplerNode;9;-3740.823,-305.0917;Inherit;True;Property;_Mask_Texture;Mask_Texture;0;0;Create;True;0;0;0;False;0;False;-1;2b03b59c1f786984297d70163e795d57;2b03b59c1f786984297d70163e795d57;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector2Node;164;-4858.623,-360.9726;Inherit;False;Property;_Mask_Scale;Mask_Scale;6;0;Create;True;0;0;0;False;0;False;1,1;1,1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;12;1451.577,249.2822;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;165;-4311.911,-261.1113;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;38;-3939.949,-284.1997;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;192;1620.532,-22.03604;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;260;1921.858,-67.09308;Float;False;True;-1;2;ASEMaterialInspector;0;0;Unlit;SH_Vefects_VFX_Fresnel;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;False;False;False;False;False;False;Back;0;False;-1;0;False;-1;False;0;False;-1;0;False;-1;False;0;Transparent;0.5;True;True;0;False;Transparent;;Transparent;All;18;all;True;True;True;True;0;False;-1;False;0;False;-1;255;False;-1;255;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;False;2;15;10;25;False;0.5;True;2;5;False;-1;10;False;-1;0;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;-1;-1;0;False;-1;0;0;0;False;0.1;False;-1;0;False;-1;False;15;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;177;0;176;0
WireConnection;177;1;175;0
WireConnection;178;0;177;0
WireConnection;30;0;31;0
WireConnection;79;0;30;0
WireConnection;79;2;32;0
WireConnection;79;1;180;0
WireConnection;43;1;79;0
WireConnection;44;0;43;1
WireConnection;44;1;45;0
WireConnection;50;0;44;0
WireConnection;50;1;52;0
WireConnection;64;0;50;0
WireConnection;216;0;210;0
WireConnection;215;0;211;0
WireConnection;215;1;212;0
WireConnection;219;0;214;0
WireConnection;219;1;213;0
WireConnection;218;0;215;0
WireConnection;218;2;216;0
WireConnection;218;1;217;0
WireConnection;26;0;25;0
WireConnection;58;0;59;0
WireConnection;220;0;219;0
WireConnection;220;1;218;0
WireConnection;78;0;26;0
WireConnection;78;2;29;0
WireConnection;78;1;174;0
WireConnection;222;1;220;0
WireConnection;80;0;58;0
WireConnection;80;2;60;0
WireConnection;80;1;179;0
WireConnection;61;0;71;0
WireConnection;61;1;80;0
WireConnection;54;0;69;0
WireConnection;54;1;78;0
WireConnection;223;0;222;1
WireConnection;223;1;221;0
WireConnection;24;1;54;0
WireConnection;55;1;61;0
WireConnection;225;0;223;0
WireConnection;225;1;224;0
WireConnection;62;0;24;1
WireConnection;62;1;55;1
WireConnection;246;0;244;0
WireConnection;226;0;225;0
WireConnection;137;0;138;0
WireConnection;227;0;226;0
WireConnection;227;1;62;0
WireConnection;182;0;137;0
WireConnection;248;0;246;0
WireConnection;248;2;245;0
WireConnection;249;0;247;0
WireConnection;249;1;248;0
WireConnection;136;0;182;0
WireConnection;167;1;173;3
WireConnection;167;2;170;0
WireConnection;167;3;171;0
WireConnection;184;0;227;0
WireConnection;184;1;185;0
WireConnection;75;0;184;0
WireConnection;75;1;76;0
WireConnection;181;0;167;0
WireConnection;181;1;136;0
WireConnection;250;1;249;0
WireConnection;168;0;181;0
WireConnection;168;1;75;0
WireConnection;237;0;250;1
WireConnection;237;1;234;0
WireConnection;229;0;232;4
WireConnection;229;1;231;0
WireConnection;261;0;190;0
WireConnection;169;0;168;0
WireConnection;240;0;237;0
WireConnection;240;1;236;0
WireConnection;235;0;240;0
WireConnection;262;0;261;0
WireConnection;228;0;169;0
WireConnection;228;1;229;0
WireConnection;242;0;238;0
WireConnection;241;0;233;0
WireConnection;23;0;8;0
WireConnection;189;0;228;0
WireConnection;189;1;262;0
WireConnection;239;0;241;0
WireConnection;239;1;242;0
WireConnection;239;2;235;0
WireConnection;139;0;8;4
WireConnection;53;0;189;0
WireConnection;251;0;239;0
WireConnection;251;1;23;0
WireConnection;7;0;164;0
WireConnection;9;1;38;0
WireConnection;12;0;139;0
WireConnection;12;1;53;0
WireConnection;165;0;7;0
WireConnection;165;2;166;0
WireConnection;38;0;70;0
WireConnection;38;1;165;0
WireConnection;192;0;251;0
WireConnection;192;1;193;0
WireConnection;260;2;192;0
WireConnection;260;9;12;0
ASEEND*/
//CHKSM=1C9BCBEB0389738A33CB5F2A2631F1E8B006764C