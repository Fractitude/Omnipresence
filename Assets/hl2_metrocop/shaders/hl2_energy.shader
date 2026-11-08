HEADER
{
	Description = "Half-Life 2 energy effect: additive or mod2x, scrolling";
	DevShader = true;
}

FEATURES
{
	#include "common/features.hlsl"
	Feature( F_MOD2X, 0..1, "Material" );
}

MODES
{
	Forward();
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

	PixelInput MainVs( VertexInput i )
	{
		PixelInput o = ProcessVertex( i );
		return FinalizeVertex( o );
	}
}

PS
{
	#define CUSTOM_MATERIAL_INPUTS 1
	#include "common/pixel.hlsl"

	StaticCombo( S_MOD2X, F_MOD2X, Sys( ALL ) );

	CreateInputTexture2D( TextureColor, Srgb, 8, "", "_color", "Material,10/10", Default3( 1.0, 1.0, 1.0 ) );
	Texture2D g_tColor < Channel( RGBA, Box( TextureColor ), Srgb ); OutputFormat( BC7 ); SrgbRead( true ); >;
	float g_flScrollRate < Default( 0.0 ); Range( -4.0, 4.0 ); UiGroup( "Material,10/20" ); >;
	float g_flScrollAngle < Default( 0.0 ); Range( -360.0, 360.0 ); UiGroup( "Material,10/30" ); >;
	float3 g_vTint < UiType( Color ); Default3( 1.0, 1.0, 1.0 ); UiGroup( "Material,10/40" ); >;
	float g_flBrightness < Default( 1.0 ); Range( 0.0, 8.0 ); UiGroup( "Material,10/50" ); >;

	RenderState( CullMode, NONE );
	RenderState( DepthWriteEnable, false );
	RenderState( BlendEnable, true );
	#if S_MOD2X
		RenderState( SrcBlend, DEST_COLOR );
		RenderState( DstBlend, SRC_COLOR );
	#else
		RenderState( SrcBlend, ONE );
		RenderState( DstBlend, ONE );
	#endif

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		float angle = radians( g_flScrollAngle );
		float2 uv = i.vTextureCoords.xy + float2( cos( angle ), sin( angle ) ) * g_flScrollRate * g_flTime;
		float3 color = g_tColor.Sample( g_sDefault, uv ).rgb * g_vTint * g_flBrightness;
		#if S_MOD2X
			color = pow( max( color, 0.0 ), 1.0 / 2.2 );
		#endif
		return float4( color, 1.0 );
	}
}
