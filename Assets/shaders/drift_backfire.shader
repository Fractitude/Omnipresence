HEADER
{
	Description = "Exhaust backfire flame";
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
	RenderState( DstBlend, ONE );
	RenderState( DepthWriteEnable, false );

	#include "common/pixel.hlsl"

	float3 FlameCore < Default3( 1, 0.85, 0.4 ); >;
	float3 FlameTip < Default3( 1, 0.12, 0.02 ); >;
	float FlameBrightness < Default( 8 ); Range( 0, 20 ); >;

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		Material m = Material::Init( i );
		float2 uv = i.vTextureCoords.xy;
		float phase = g_flTime * 55.0;
		float broad = sin( uv.x * 18.0 - phase + uv.y * 7.0 );
		float fine = sin( uv.x * 39.0 + phase * 1.43 - uv.y * 19.0 );
		float flicker = sin( uv.x * 27.0 - phase * 1.81 + uv.y * 13.0 );
		float center = 0.5 + uv.x * (broad * 0.09 + fine * 0.04);
		float width = lerp( 0.43, 0.025, uv.x ) * (0.9 + broad * 0.13 + fine * 0.07);
		float distance = abs( uv.y - center );
		float edge = 1.0 - smoothstep( width * 0.55, width, distance );
		float tongue = saturate( 0.72 + broad * 0.15 + fine * 0.10 + flicker * 0.10 );
		float alpha = edge * tongue * smoothstep( 0.0, 0.08, uv.x ) * (1.0 - smoothstep( 0.77, 1.0, uv.x ));
		float core = saturate( (1.0 - uv.x) * 1.15 - distance * 0.5 );
		float3 color = lerp( FlameTip, FlameCore, core );
		m.Albedo = color;
		m.Emission = color * FlameBrightness;
		m.Opacity = alpha;
		m.Roughness = 1;
		m.Metalness = 0;
		m.AmbientOcclusion = 1;
		return ShadingModelStandard::Shade( i, m );
	}
}
