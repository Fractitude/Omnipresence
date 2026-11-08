HEADER
{
	Description = "Car tail light lens, translucent";
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
	#define S_TRANSLUCENT 1
	#include "common/shared.hlsl"
}

struct VertexInput
{
	#include "common/vertexinput.hlsl"
};

struct PixelInput
{
	#include "common/pixelinput.hlsl"
};

VS
{
	#include "common/vertex.hlsl"

	PixelInput MainVs( VertexInput v ) { return FinalizeVertex( ProcessVertex( v ) ); }
}

PS
{
	RenderState( BlendEnable, true );
	RenderState( SrcBlend, SRC_ALPHA );
	RenderState( DstBlend, INV_SRC_ALPHA );
	RenderState( DepthWriteEnable, false );

	#include "common/pixel.hlsl"

	CreateInputTexture2D( LensColor, Srgb, 8, "", "", "Lens", Default4( 1, 1, 1, 1 ) );
	Texture2D g_tLens < Channel( RGBA, Box( LensColor ), Srgb ); OutputFormat( BC7 ); SrgbRead( true ); >;
	SamplerState g_sLens < Filter( ANISO ); MaxAniso( 8 ); AddressU( WRAP ); AddressV( WRAP ); >;
	float3 LensTint < Default3( 1, 1, 1 ); >;
	float LensOpacity < Default( 1 ); Range( 0, 1 ); >;
	float LampEmission < Default( 0 ); Range( 0, 16 ); >;
	float3 LampColor < Default3( 1, 0.94, 0.8 ); >;
	float LampEmissionBase < Default( 0 ); Range( 0, 1 ); >;

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		Material m = Material::Init( i );
		float4 color = g_tLens.Sample( g_sLens, i.vTextureCoords.xy );
		m.Albedo = color.rgb * LensTint;
		m.Emission = max( m.Albedo, LampEmissionBase ) * LampColor * LampEmission;
		m.Opacity = color.a * LensOpacity;
		m.Roughness = 0.2;
		m.Metalness = 0;
		m.AmbientOcclusion = 1;
		return ShadingModelStandard::Shade( i, m );
	}
}
