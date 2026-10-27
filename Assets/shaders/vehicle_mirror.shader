// The picture a VehicleMirror's camera draws, painted straight onto the mirror glass.
// Unlit: the feed is the mirror's own light, so the scene's lighting must not touch it.
HEADER
{
	Description = "Vehicle mirror feed";
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
}

COMMON
{
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

	PixelInput MainVs( VS_INPUT i )
	{
		PixelInput o = ProcessVertex( i );
		return FinalizeVertex( o );
	}
}

PS
{
	#include "common/pixel.hlsl"

	// The feed is a linear HDR render target and this pane is drawn into the scene, so the image goes through the
	// scene's own tonemapping exactly once, like everything else.
	Texture2D g_tFeed < Attribute( "Feed" ); SrgbRead( false ); >;
	SamplerState g_sFeedSampler < Filter( MIN_MAG_MIP_LINEAR ); AddressU( CLAMP ); AddressV( CLAMP ); >;

	// A pane is seen from both sides, and the back of a mirror glass is still a mirror face.
	RenderState( CullMode, NONE );
	RenderState( DepthWriteEnable, true );

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		float2 uv = i.vTextureCoords.xy;
		float3 feed = g_tFeed.Sample( g_sFeedSampler, uv ).rgb;

		// Mirror glasses are rounded, not rectangles: shave the corners so the quad never reads as a
		// sticker stuck onto the housing.
		float2 q = abs( uv - 0.5f ) * 2.0f;
		float corner = length( max( q - 0.74f, 0.0f ) );
		clip( 0.26f - corner );

		return float4( feed, 1.0f );
	}
}
