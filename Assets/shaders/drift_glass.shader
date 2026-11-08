HEADER
{
	Description = "Car glass, darkened toward the dashboard";
	DevShader = true;
}

FEATURES
{
	#include "common/features.hlsl"
}

MODES
{
	Forward();
	Depth( S_MODE_DEPTH );
	ToolsShadingComplexity( "tools_shading_complexity.shader" );
}

COMMON
{
	#define S_SPECULAR 1
	#include "common/shared.hlsl"
}

struct VertexInput
{
	#include "common/vertexinput.hlsl"
};

struct PixelInput
{
	#include "common/pixelinput.hlsl"
	float3 LocalPosition : TEXCOORD8;
};

VS
{
	#include "common/vertex.hlsl"

	PixelInput MainVs( VertexInput v )
	{
		PixelInput o = ProcessVertex( v );
		o.LocalPosition = v.vPositionOs.xyz;
		return FinalizeVertex( o );
	}
}

PS
{
	RenderState( CullMode, NONE );

	#include "common/pixel.hlsl"

	float3 GlassTint < Default3( 0.035, 0.037, 0.04 ); UiType( Color ); >;
	float GlassRoughness < Default( 0.1 ); Range( 0.02, 0.5 ); >;

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		Material m = Material::Init( i );
		float3 viewDirection = normalize( g_vCameraPositionWs.xyz - m.WorldPosition );
		float3 normal = normalize( m.Normal );
		if ( dot( normal, viewDirection ) < 0 ) normal = -normal;
		float distanceToGlass = length( g_vCameraPositionWs.xyz - m.WorldPosition );
		float closeView = 1.0 - smoothstep( 130.0, 350.0, distanceToGlass );
		float dashboard = (1.0 - smoothstep( 18.0, 25.0, i.LocalPosition.z )) * closeView;
		float3 localNormal = abs( normalize( cross( ddx( i.LocalPosition ), ddy( i.LocalPosition ) ) ) );
		float sideGlass = smoothstep( 0.6, 0.9, localNormal.y );
		m.Normal = normal;
		m.Albedo = GlassTint * (1.0 - dashboard * 0.45);
		m.Metalness = 0;
		m.Roughness = lerp( GlassRoughness, 0.55, sideGlass );
		#if S_MODE_DEPTH
			m.Roughness = 1;
		#endif
		m.AmbientOcclusion = 1;
		m.Opacity = 1;
		float4 shaded = ShadingModelStandard::Shade( i, m );
		float faceOn = saturate( dot( normal, viewDirection ) );
		shaded.rgb *= (1.0 - dashboard * 0.18) * (1.0 - 0.4 * faceOn * faceOn) * lerp( 1.0, 0.45, sideGlass );
		return shaded;
	}
}
