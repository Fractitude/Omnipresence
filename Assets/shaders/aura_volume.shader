// Draws a gmk aura as a glowing liquid: the back of its box, marched through a 3D volume.
HEADER
{
	Description = "Aura liquid volume";
	DevShader = true;
}

FEATURES
{
	#include "common/features.hlsl"
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
	float3 Position     		: POSITION		< Semantic( PosXyz ); >;
	uint nInstanceTransformID	: TEXCOORD13	< Semantic( InstanceTransformUv ); >;
};

struct PixelInput
{
	float3 CameraToPositionRay	: TEXCOORD4;
	#if ( PROGRAM == VFX_PROGRAM_VS )
		float4 PixelPosition : SV_Position;
	#endif
	#if ( PROGRAM == VFX_PROGRAM_PS )
		float4 ScreenPosition : SV_Position;
	#endif
};

VS
{
	#include "instancing.fxc"

	PixelInput MainVs( VertexInput i )
	{
		PixelInput o;
		float3x4 matObjectToWorld = GetTransformMatrix( i.nInstanceTransformID );
		float3 vVertexPosWs = mul( matObjectToWorld, float4( i.Position.xyz, 1.0 ) );
		o.PixelPosition.xyzw = Position3WsToPs( vVertexPosWs.xyz );
		o.CameraToPositionRay.xyz = CalculateCameraToPositionRayWs( vVertexPosWs );
		return o;
	}
}

PS
{
	Texture3D g_tVolume < Attribute( "AuraVolume" ); SrgbRead( false ); >;
	SamplerState g_sVolume < Filter( BILINEAR ); AddressU( CLAMP ); AddressV( CLAMP ); AddressW( CLAMP ); >;
	float3 g_vBoxMin    < Attribute( "AuraBoxMin" );   Default3( 0, 0, 0 ); >;
	float3 g_vBoxSize   < Attribute( "AuraBoxSize" );  Default3( 1, 1, 1 ); >;
	float3 g_vMarchMin  < Attribute( "AuraMarchMin" ); Default3( 0, 0, 0 ); >;
	float3 g_vMarchMax  < Attribute( "AuraMarchMax" ); Default3( 1, 1, 1 ); >;
	float  g_flBeat     < Attribute( "AuraBeat" );     Default( 0.0 ); >;
	float  g_flOpacity  < Attribute( "AuraOpacity" );  Default( 1.0 ); >;
	float3 g_vAuraColor < Attribute( "AuraColor" );    Default3( 0.55, 0.2, 1.0 ); >;
	float3 g_vRimColor  < Attribute( "AuraRimColor" ); Default3( 0.95, 0.65, 1.0 ); >;

	#define COLOR_WRITE_ALREADY_SET
	RenderState( ColorWriteEnable0, RGB );
	#define BLEND_MODE_ALREADY_SET
	RenderState( BlendEnable, true );
	RenderState( BlendOp, ADD );
	RenderState( SrcBlend, SRC_ALPHA );
	RenderState( DstBlend, INV_SRC_ALPHA );
	#define DEPTH_STATE_ALREADY_SET
	RenderState( CullMode, FRONT );
	RenderState( DepthEnable, false );
	RenderState( DepthWriteEnable, false );

	#include "vr_common_ps_code.fxc"

	static const int Steps = 48;

	float2 Liquid( float3 vPositionWs )
	{
		float3 vUvw = ( vPositionWs - g_vBoxMin ) / g_vBoxSize;
		return g_tVolume.SampleLevel( g_sVolume, vUvw, 0 ).rg;
	}

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		float3 vCamera = g_vCameraPositionWs;
		float3 vRay = normalize( i.CameraToPositionRay );

		float flDepth = Depth::GetNormalized( i.ScreenPosition.xy );
		float3 vSurface = RecoverWorldPosFromProjectedDepthAndRay( flDepth, i.CameraToPositionRay );
		float flSceneDist = length( vSurface - vCamera );

		float3 vInv = 1.0 / ( abs( vRay ) > 1e-5 ? vRay : 1e-5 );
		float3 vA = ( g_vMarchMin - vCamera ) * vInv;
		float3 vB = ( g_vMarchMax - vCamera ) * vInv;
		float3 vNear = min( vA, vB );
		float3 vFar = max( vA, vB );
		float t0 = max( max( max( vNear.x, vNear.y ), vNear.z ), 0.0 );
		float tBox = min( min( vFar.x, vFar.y ), vFar.z );
		float t1 = min( tBox, flSceneDist );
		if ( tBox <= t0 ) discard;

		float flBeat = saturate( g_flBeat );
		float flFog = 0.0;
		float flRim = 0.0;

		if ( t1 > t0 )
		{
			float dt = ( t1 - t0 ) / Steps;
			float flJitter = frac( sin( dot( i.ScreenPosition.xy, float2( 12.9898, 78.233 ) ) ) * 43758.5453 );

			[loop]
			for ( int s = 0; s < Steps; s++ )
			{
				float3 vP = vCamera + vRay * ( t0 + ( s + flJitter ) * dt );
				float2 vL = Liquid( vP );

				float flInside = smoothstep( 0.4, 0.6, vL.x );
				float flNearSurface = 1.0 - saturate( ( vL.x - 0.5 ) * 2.0 );
				float flStir = 0.7 + 0.3 * sin( vP.z * 0.06 - g_flTime * 2.0 + sin( vP.x * 0.04 + g_flTime ) + vP.y * 0.03 );
				flFog += flInside * flStir * ( 0.3 + 0.7 * flNearSurface ) * dt;

				float flSurface = exp( -pow( ( vL.x - 0.5 ) / 0.18, 2.0 ) );
				flRim += flSurface * ( 1.0 - vL.y ) * dt;
			}
		}

		float flWall = 0.0;
		float flEdge = 0.0;
		if ( flSceneDist <= tBox )
		{
			float3 vNormal = normalize( cross( ddy( vSurface ), ddx( vSurface ) ) );
			if ( dot( vNormal, vCamera - vSurface ) < 0.0 ) vNormal = -vNormal;
			float2 vL = Liquid( vSurface + vNormal * 12.0 );
			flWall = smoothstep( 0.25, 0.45, vL.x );
			flEdge = exp( -pow( ( vL.x - 0.35 ) / 0.08, 2.0 ) ) * ( 1.0 - vL.y );
		}

		float aFog  = 1.0 - exp( -flFog * 0.0006 );
		float aRim  = saturate( flRim * 0.02 ) * ( 0.8 + 0.6 * flBeat );
		float aWall = flWall * ( 0.12 + 0.08 * flBeat );
		float aEdge = flEdge * 0.8;
		float flAlpha = saturate( aFog + aRim + aWall + aEdge ) * g_flOpacity;
		clip( flAlpha - 0.003 );

		float3 vColor = g_vAuraColor * ( aFog + aWall ) + g_vRimColor * ( aRim + aEdge ) * ( 1.0 + flBeat * 0.4 );
		vColor /= max( aFog + aWall + aRim + aEdge, 0.001 );

		return float4( vColor, flAlpha );
	}
}
