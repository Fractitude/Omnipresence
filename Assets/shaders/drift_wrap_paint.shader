HEADER
{
	Description = "Car paint with a body texture and a box-projected wrap (livery) texture";
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
	float3 WrapLocal : TEXCOORD8;
};

VS
{
	#include "common/vertex.hlsl"

	PixelInput MainVs( VertexInput v )
	{
		PixelInput o = ProcessVertex( v );
		o.WrapLocal = v.vPositionOs.xyz;
		return FinalizeVertex( o );
	}
}

PS
{
	#include "common/pixel.hlsl"

	CreateInputTexture2D( TextureColor, Srgb, 8, "", "", "Paint", Default3( 1, 1, 1 ) );
	Texture2D g_tColor < Channel( RGB, Box( TextureColor ), Srgb ); OutputFormat( BC7 ); SrgbRead( true ); >;
	CreateInputTexture2D( TextureWrap, Srgb, 8, "", "", "Wrap", Default4( 0, 0, 0, 0 ) );
	Texture2D g_tWrap < Channel( RGBA, Box( TextureWrap ), Srgb ); OutputFormat( BC7 ); SrgbRead( true ); >;
	SamplerState g_sPaint < Filter( ANISO ); MaxAniso( 8 ); AddressU( WRAP ); AddressV( WRAP ); >;
	float3 g_vColorTint < Default3( 1, 1, 1 ); >;
	float3 WrapCenter < Default3( 0, 0, 0 ); >;
	float3 WrapSize < Default3( 1, 1, 1 ); >;
	float PaintRoughness < Default( 0.22 ); Range( 0.08, 1 ); >;
	float PaintMetalness < Default( 0.6 ); Range( 0, 1 ); >;
	float PaintDirtOverlay < Default( 0 ); Range( 0, 1 ); >;

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		Material m = Material::Init( i );
		float3 sourceColor = g_tColor.Sample( g_sPaint, i.vTextureCoords.xy ).rgb;
		float dirtMask = saturate( 1.0 - dot( sourceColor, float3( 0.2126, 0.7152, 0.0722 ) ) ) * PaintDirtOverlay * 0.55;
		m.Albedo = lerp( sourceColor * g_vColorTint, g_vColorTint, PaintDirtOverlay );
		float3 p = (i.WrapLocal - WrapCenter) / max( WrapSize, 1.0 ) + 0.5;
		float3 dx = ddx( i.WrapLocal );
		float3 dy = ddy( i.WrapLocal );
		float3 weights = abs( normalize( cross( dx, dy ) ) );
		weights = pow( weights, 8.0 );
		weights /= max( weights.x + weights.y + weights.z, 0.0001 );
		float4 wrap = g_tWrap.Sample( g_sPaint, p.yz ) * weights.x
			+ g_tWrap.Sample( g_sPaint, p.xz ) * weights.y
			+ g_tWrap.Sample( g_sPaint, p.xy ) * weights.z;
		m.Albedo = lerp( m.Albedo, wrap.rgb, wrap.a );
		m.Albedo = lerp( m.Albedo, float3( 0.18, 0.17, 0.15 ), dirtMask );
		m.Roughness = lerp( clamp( PaintRoughness, 0.08, 1.0 ), 0.62, dirtMask );
		m.Metalness = lerp( saturate( PaintMetalness ), 0.0, dirtMask );
		m.AmbientOcclusion = 1;
		m.Opacity = 1;
		return ShadingModelStandard::Shade( i, m );
	}
}
