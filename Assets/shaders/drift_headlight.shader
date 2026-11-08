HEADER
{
	Description = "Car headlight lamp";
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

	CreateInputTexture2D( TextureColor, Srgb, 8, "", "", "Lamp", Default3( 1, 1, 1 ) );
	Texture2D g_tColor < Channel( RGB, Box( TextureColor ), Srgb ); OutputFormat( BC7 ); SrgbRead( true ); >;
	SamplerState g_sLamp < Filter( ANISO ); MaxAniso( 8 ); AddressU( WRAP ); AddressV( WRAP ); >;
	float3 g_vColorTint < Default3( 1, 1, 1 ); >;
	float g_flMetalness < Default( 0.25 ); >;
	float g_flRoughnessScaleFactor < Default( 0.48 ); >;
	float LampEmission < Default( 0 ); Range( 0, 16 ); >;
	float3 LampColor < Default3( 1, 0.94, 0.8 ); >;
	float LampEmissionBase < Default( 0 ); Range( 0, 1 ); >;

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		Material m = Material::Init( i );
		m.Albedo = g_tColor.Sample( g_sLamp, i.vTextureCoords.xy ).rgb * g_vColorTint;
		m.Emission = max( m.Albedo, LampEmissionBase ) * LampColor * LampEmission;
		m.Roughness = g_flRoughnessScaleFactor;
		m.Metalness = g_flMetalness;
		m.AmbientOcclusion = 1;
		m.Opacity = 1;
		return ShadingModelStandard::Shade( i, m );
	}
}
