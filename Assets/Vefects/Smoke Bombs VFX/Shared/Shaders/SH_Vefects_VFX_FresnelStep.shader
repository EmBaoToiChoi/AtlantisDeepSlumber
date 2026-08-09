// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "SH_Vefects_VFX_FresnelStep"
{
	Properties
	{
		_Noise_01_Texture("Noise_01_Texture", 2D) = "white" {}
		_Noise_02_Texture("Noise_02_Texture", 2D) = "white" {}
		_NoiseDistortion_Texture("NoiseDistortion_Texture", 2D) = "white" {}
		_Noise_01_Scale("Noise_01_Scale", Vector) = (0.8,0.8,0,0)
		_Noise_02_Scale("Noise_02_Scale", Vector) = (1,1,0,0)
		_NoiseDistortion_Scale("NoiseDistortion_Scale", Vector) = (1,1,0,0)
		_Noise_01_Speed("Noise_01_Speed", Vector) = (0.5,0.5,0,0)
		_Noise_02_Speed("Noise_02_Speed", Vector) = (-0.2,0.4,0,0)
		_NoiseDistortion_Speed("NoiseDistortion_Speed", Vector) = (0.2,0.25,0,0)
		_NoiseDistortion_Intensity("NoiseDistortion_Intensity", Float) = 1
		_Opacity_Power("Opacity_Power", Float) = 1
		_Fresnel_Scale("Fresnel_Scale", Float) = 1
		_Fresnel_Power("Fresnel_Power", Float) = 5
		_Global_Speed("Global_Speed", Float) = 1
		_Emissive_Intensity("Emissive_Intensity", Float) = 1
		_Opacity_Boost("Opacity_Boost", Float) = 5
		_Color_Step("Color_Step", Float) = 0.05
		_Color_StepMin("Color_StepMin", Float) = 0
		_Color_Min("Color_Min", Float) = 0
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
			float3 worldNormal;
			float4 uv_texcoord;
			float4 vertexColor : COLOR;
			float4 uv2_texcoord2;
		};

		uniform float _Color_StepMin;
		uniform float _Color_Step;
		uniform float _Fresnel_Scale;
		uniform float _Fresnel_Power;
		uniform sampler2D _Noise_01_Texture;
		uniform sampler2D _NoiseDistortion_Texture;
		uniform float _Global_Speed;
		uniform float2 _NoiseDistortion_Speed;
		uniform float2 _NoiseDistortion_Scale;
		uniform float _NoiseDistortion_Intensity;
		uniform float2 _Noise_01_Speed;
		uniform float2 _Noise_01_Scale;
		uniform sampler2D _Noise_02_Texture;
		uniform float2 _Noise_02_Speed;
		uniform float2 _Noise_02_Scale;
		uniform float _Opacity_Power;
		uniform float _Color_Min;
		uniform float _Emissive_Intensity;
		uniform float _Opacity_Boost;

		inline half4 LightingUnlit( SurfaceOutput s, half3 lightDir, half atten )
		{
			return half4 ( 0, 0, 0, s.Alpha );
		}

		void surf( Input i , inout SurfaceOutput o )
		{
			float3 ase_worldPos = i.worldPos;
			float3 ase_worldViewDir = normalize( UnityWorldSpaceViewDir( ase_worldPos ) );
			float3 ase_worldNormal = i.worldNormal;
			float fresnelNdotV167 = dot( ase_worldNormal, ase_worldViewDir );
			float fresnelNode167 = ( i.uv_texcoord.z + _Fresnel_Scale * pow( 1.0 - fresnelNdotV167, _Fresnel_Power ) );
			float global_speed178 = ( _Global_Speed * _Time.y );
			float2 uvs_TexCoord30 = i.uv_texcoord;
			uvs_TexCoord30.xy = i.uv_texcoord.xy * _NoiseDistortion_Scale;
			float2 panner79 = ( global_speed178 * _NoiseDistortion_Speed + uvs_TexCoord30.xy);
			float Distortion64 = ( ( tex2D( _NoiseDistortion_Texture, panner79 ).r * 0.1 ) * _NoiseDistortion_Intensity );
			float2 uvs_TexCoord26 = i.uv_texcoord;
			uvs_TexCoord26.xy = i.uv_texcoord.xy * _Noise_01_Scale;
			float2 panner78 = ( global_speed178 * _Noise_01_Speed + uvs_TexCoord26.xy);
			float2 uvs_TexCoord58 = i.uv_texcoord;
			uvs_TexCoord58.xy = i.uv_texcoord.xy * _Noise_02_Scale;
			float2 panner80 = ( global_speed178 * _Noise_02_Speed + uvs_TexCoord58.xy);
			float clampResult169 = clamp( ( fresnelNode167 * pow( ( tex2D( _Noise_01_Texture, ( Distortion64 + panner78 ) ).r * tex2D( _Noise_02_Texture, ( Distortion64 + panner80 ) ).r ) , _Opacity_Power ) ) , 0.0 , 1.0 );
			float smoothstepResult200 = smoothstep( _Color_StepMin , ( _Color_StepMin + _Color_Step ) , clampResult169);
			float clampResult196 = clamp( smoothstepResult200 , _Color_Min , 1.0 );
			o.Emission = ( ( clampResult196 * (i.vertexColor).rgb ) * _Emissive_Intensity );
			float smoothstepResult205 = smoothstep( i.uv2_texcoord2.x , ( i.uv2_texcoord2.x + i.uv2_texcoord2.y ) , ( clampResult169 * _Opacity_Boost ));
			o.Alpha = ( i.vertexColor.a * saturate( smoothstepResult205 ) );
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
				float4 customPack2 : TEXCOORD2;
				float3 worldPos : TEXCOORD3;
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
				surfIN.uv_texcoord = IN.customPack1.xyzw;
				surfIN.uv2_texcoord2 = IN.customPack2.xyzw;
				float3 worldPos = IN.worldPos;
				half3 worldViewDir = normalize( UnityWorldSpaceViewDir( worldPos ) );
				surfIN.worldPos = worldPos;
				surfIN.worldNormal = IN.worldNormal;
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
192;443;2546;1168;859.5357;229.1718;1.525752;True;True
Node;AmplifyShaderEditor.RangedFloatNode;176;-2799.298,-157.2652;Inherit;False;Property;_Global_Speed;Global_Speed;16;0;Create;True;0;0;0;False;0;False;1;0.7;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleTimeNode;175;-2796.73,-80.57121;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;77;-3334.254,-921.8143;Inherit;False;2038.752;588.1584;DISTORTION;11;64;50;44;52;45;43;79;32;30;31;180;;0.4669811,0.9251378,1,1;0;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;177;-2636.73,-167.5712;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;31;-3297.445,-822.2875;Inherit;False;Property;_NoiseDistortion_Scale;NoiseDistortion_Scale;8;0;Create;True;0;0;0;False;0;False;1,1;2,2;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.RegisterLocalVarNode;178;-2468.066,-180.7602;Inherit;False;global_speed;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;32;-3113.602,-706.9553;Inherit;False;Property;_NoiseDistortion_Speed;NoiseDistortion_Speed;11;0;Create;True;0;0;0;False;0;False;0.2,0.25;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.GetLocalVarNode;180;-3061.534,-568.554;Inherit;False;178;global_speed;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;30;-3042.984,-842.979;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.PannerNode;79;-2789.843,-842.0295;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SamplerNode;43;-2603.995,-871.8142;Inherit;True;Property;_NoiseDistortion_Texture;NoiseDistortion_Texture;3;0;Create;True;0;0;0;False;0;False;-1;02b0d0a96eedb3d4fa5664350b35ba23;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;45;-2315.042,-712.4779;Inherit;False;Constant;_Float0;Float 0;8;0;Create;True;0;0;0;False;0;False;0.1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;44;-2171.852,-839.3594;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0.1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;52;-2243.284,-592.0629;Inherit;False;Property;_NoiseDistortion_Intensity;NoiseDistortion_Intensity;12;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;50;-1939.284,-840.0629;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;59;-2236.794,668.5274;Inherit;False;Property;_Noise_02_Scale;Noise_02_Scale;7;0;Create;True;0;0;0;False;0;False;1,1;2,5;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.Vector2Node;25;-2174.5,191.2139;Inherit;False;Property;_Noise_01_Scale;Noise_01_Scale;6;0;Create;True;0;0;0;False;0;False;0.8,0.8;3,6;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.GetLocalVarNode;179;-1981.819,926.5082;Inherit;False;178;global_speed;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;64;-1528.308,-846.3292;Inherit;False;Distortion;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;60;-2004.024,783.8644;Inherit;False;Property;_Noise_02_Speed;Noise_02_Speed;10;0;Create;True;0;0;0;False;0;False;-0.2,0.4;-0.5,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.TextureCoordinatesNode;58;-2022.089,649.5414;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.GetLocalVarNode;174;-1962.045,470.9908;Inherit;False;178;global_speed;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;29;-1976.418,312.6519;Inherit;False;Property;_Noise_01_Speed;Noise_01_Speed;9;0;Create;True;0;0;0;False;0;False;0.5,0.5;-0.2,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.TextureCoordinatesNode;26;-1964.394,172.2441;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.GetLocalVarNode;69;-1647.529,24.72726;Inherit;False;64;Distortion;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;80;-1763.381,653.4103;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;71;-1651.56,434.4473;Inherit;False;64;Distortion;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;78;-1713.982,219.5743;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;54;-1458.889,218.7063;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;61;-1480.758,589.7628;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SamplerNode;55;-1331.504,592.9574;Inherit;True;Property;_Noise_02_Texture;Noise_02_Texture;2;0;Create;True;0;0;0;False;0;False;-1;2f822f6dfbe26f44eadb94a1b925409d;bd2a188e11c0c2145b7428bc4aa7efec;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;24;-1296.029,200.5704;Inherit;True;Property;_Noise_01_Texture;Noise_01_Texture;1;0;Create;True;0;0;0;False;0;False;-1;301564cad965bbb4da5072b5cf9406cb;301564cad965bbb4da5072b5cf9406cb;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;185;-872.0938,529.9515;Inherit;False;Property;_Opacity_Power;Opacity_Power;13;0;Create;True;0;0;0;False;0;False;1;0.8;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;62;-930.8992,412.2656;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TexCoordVertexDataNode;173;-866.1883,-638.0415;Inherit;False;0;4;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;170;-825.6573,-393.5589;Inherit;False;Property;_Fresnel_Scale;Fresnel_Scale;14;0;Create;True;0;0;0;False;0;False;1;2.5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;171;-823.6573,-303.5588;Inherit;False;Property;_Fresnel_Power;Fresnel_Power;15;0;Create;True;0;0;0;False;0;False;5;1.5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.PowerNode;184;-659.0938,420.9515;Inherit;False;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.FresnelNode;167;-572.8887,-450.4738;Inherit;True;Standard;WorldNormal;ViewDir;False;False;5;0;FLOAT3;0,0,1;False;4;FLOAT3;0,0,0;False;1;FLOAT;0;False;2;FLOAT;1;False;3;FLOAT;5;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;198;394.5234,-232.8516;Inherit;False;Property;_Color_Step;Color_Step;19;0;Create;True;0;0;0;False;0;False;0.05;0.4;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;168;-144.9373,383.5121;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;197;415.8464,-412.8485;Inherit;False;Property;_Color_StepMin;Color_StepMin;20;0;Create;True;0;0;0;False;0;False;0;0.01;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;199;685.0006,-371.7933;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0.1;False;1;FLOAT;0
Node;AmplifyShaderEditor.TexCoordVertexDataNode;206;337.0954,686.9056;Inherit;False;1;4;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ClampOpNode;169;132.1332,387.8163;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;202;401.8744,481.5981;Inherit;False;Property;_Opacity_Boost;Opacity_Boost;18;0;Create;True;0;0;0;False;0;False;5;6;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SmoothstepOpNode;200;825.8001,-617.0392;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.VertexColorNode;8;1432.628,-143.1708;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleAddOpNode;203;707.3115,713.4346;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0.1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;204;647.3896,387.2093;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;195;1212.479,-398.1753;Inherit;False;Property;_Color_Min;Color_Min;21;0;Create;True;0;0;0;False;0;False;0;0.4;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SmoothstepOpNode;205;954.9089,387.3936;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;196;1424.479,-607.9699;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0.25;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ComponentMaskNode;23;1688.728,-147.2391;Inherit;False;True;True;True;False;1;0;COLOR;0,0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.WireNode;139;1576.472,235.3673;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;53;1745.061,407.4351;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;193;1724.684,31.1771;Inherit;False;Property;_Emissive_Intensity;Emissive_Intensity;17;0;Create;True;0;0;0;False;0;False;1;0.8;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;201;1873.432,-393.8446;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;12;1918.812,263.1591;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;192;1964.767,3.840881;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.Vector2Node;164;-3951.299,413.7024;Inherit;False;Property;_Mask_Scale;Mask_Scale;4;0;Create;True;0;0;0;False;0;False;1,1;1,1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.SamplerNode;9;-2833.499,469.5833;Inherit;True;Property;_Mask_Texture;Mask_Texture;0;0;Create;True;0;0;0;False;0;False;-1;2b03b59c1f786984297d70163e795d57;2b03b59c1f786984297d70163e795d57;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TextureCoordinatesNode;7;-3751.9,515.0793;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleAddOpNode;38;-3032.625,490.4753;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;70;-3216.261,362.7645;Inherit;False;64;Distortion;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;165;-3404.587,513.5638;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector2Node;166;-3621.262,659.0785;Inherit;False;Property;_Mask_Speed;Mask_Speed;5;0;Create;True;0;0;0;False;0;False;0,0;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;229;2353.689,-31.05927;Float;False;True;-1;2;ASEMaterialInspector;0;0;Unlit;SH_Vefects_VFX_FresnelStep;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;False;False;False;False;False;False;Back;0;False;-1;0;False;-1;False;0;False;-1;0;False;-1;False;0;Transparent;0.5;True;True;0;False;Transparent;;Transparent;All;18;all;True;True;True;True;0;False;-1;False;0;False;-1;255;False;-1;255;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;False;2;15;10;25;False;0.5;True;2;5;False;-1;10;False;-1;0;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;-1;-1;0;False;-1;0;0;0;False;0.1;False;-1;0;False;-1;False;15;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
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
WireConnection;58;0;59;0
WireConnection;26;0;25;0
WireConnection;80;0;58;0
WireConnection;80;2;60;0
WireConnection;80;1;179;0
WireConnection;78;0;26;0
WireConnection;78;2;29;0
WireConnection;78;1;174;0
WireConnection;54;0;69;0
WireConnection;54;1;78;0
WireConnection;61;0;71;0
WireConnection;61;1;80;0
WireConnection;55;1;61;0
WireConnection;24;1;54;0
WireConnection;62;0;24;1
WireConnection;62;1;55;1
WireConnection;184;0;62;0
WireConnection;184;1;185;0
WireConnection;167;1;173;3
WireConnection;167;2;170;0
WireConnection;167;3;171;0
WireConnection;168;0;167;0
WireConnection;168;1;184;0
WireConnection;199;0;197;0
WireConnection;199;1;198;0
WireConnection;169;0;168;0
WireConnection;200;0;169;0
WireConnection;200;1;197;0
WireConnection;200;2;199;0
WireConnection;203;0;206;1
WireConnection;203;1;206;2
WireConnection;204;0;169;0
WireConnection;204;1;202;0
WireConnection;205;0;204;0
WireConnection;205;1;206;1
WireConnection;205;2;203;0
WireConnection;196;0;200;0
WireConnection;196;1;195;0
WireConnection;23;0;8;0
WireConnection;139;0;8;4
WireConnection;53;0;205;0
WireConnection;201;0;196;0
WireConnection;201;1;23;0
WireConnection;12;0;139;0
WireConnection;12;1;53;0
WireConnection;192;0;201;0
WireConnection;192;1;193;0
WireConnection;9;1;38;0
WireConnection;7;0;164;0
WireConnection;38;0;70;0
WireConnection;38;1;165;0
WireConnection;165;0;7;0
WireConnection;165;2;166;0
WireConnection;229;2;192;0
WireConnection;229;9;12;0
ASEEND*/
//CHKSM=60F730090212A51CE7392CD99100B7A55F772395