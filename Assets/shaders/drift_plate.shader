HEADER
{
	Description = "Licence plate with the logo fitted to its face";
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
};

VS
{
	#include "common/vertex.hlsl"

	PixelInput MainVs( VertexInput v ) { return FinalizeVertex( ProcessVertex( v ) ); }
}

PS
{
	#include "common/pixel.hlsl"

	CreateInputTexture2D( PlateArtwork, Srgb, 8, "", "", "Plate", Default4( 0, 0, 0, 0 ) );
	Texture2D g_tArtwork < Channel( RGBA, Box( PlateArtwork ), Srgb ); OutputFormat( BC7 ); SrgbRead( true ); >;
	SamplerState g_sPlate < Filter( ANISO ); MaxAniso( 8 ); AddressU( CLAMP ); AddressV( CLAMP ); >;
	float2 PlateUVScale < Default2( 1, 1 ); >;
	float2 PlateUVBias < Default2( 0, 0 ); >;

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		Material m = Material::Init( i );
		float2 uv = i.vTextureCoords.xy * PlateUVScale + PlateUVBias;
		float2 dx = ddx( uv ), dy = ddy( uv );
		float determinant = dx.x * dy.y - dx.y * dy.x;
		float3 px = ddx( m.WorldPosition ), py = ddy( m.WorldPosition );
		float3 u = px * dy.y - py * dx.y;
		float3 v = py * dx.x - px * dy.x;
		float surfaceAspect = abs( determinant ) > 0.0000001 ? length( u ) / max( length( v ), 0.000001 ) : 4.0;
		float2 wordmarkOrigin = float2( 74.0, 22.0 );
		float2 wordmarkSize = float2( 240.0, 40.0 );
		float logoAspect = wordmarkSize.x / wordmarkSize.y;
		float2 fit = float2( max( 1.0, surfaceAspect / logoAspect ), max( 1.0, logoAspect / surfaceAspect ) ) / 0.94;
		float2 artworkUv = (uv - 0.5) * fit + 0.5;
		float2 sourceUv = (wordmarkOrigin + artworkUv * wordmarkSize) / float2( 316.0, 80.0 );
		float4 artwork = g_tArtwork.Sample( g_sPlate, sourceUv );
		artwork.a *= all( artworkUv >= 0 ) && all( artworkUv <= 1 ) ? 1 : 0;
		m.Albedo = lerp( float3( 0.008, 0.009, 0.010 ), artwork.rgb, artwork.a );
		m.Roughness = 0.7;
		m.Metalness = 0;
		m.AmbientOcclusion = 1;
		m.Opacity = 1;
		return ShadingModelStandard::Shade( i, m );
	}
}
