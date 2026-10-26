using System.Collections.Generic;
using Sandbox;

namespace Hl2Metrocop;

/// <summary>
/// The lit baton, as HL2's client draws it (c_weapon_stunstick.cpp): while it's on, two halos of
/// effects/stunstick flicker at the tip every frame - a big dim one (4-6, brightness 0.1-0.2) and a
/// small bright core (2-3, brightness 0.9-1) - and one frame in six a tiny white arc crackles off
/// the tip (sprites/physbeam, 8 segments, 0.5-2 wide). Drawn by every client from the synced BatonOn.
/// </summary>
public sealed partial class CombineNpc
{
	private SpriteRenderer _batonHalo;
	private SpriteRenderer _batonCore;
	private GameObject _batonArc;
	private LineRenderer _batonArcLine;

	private void UpdateBatonGlow()
	{
		var lit = HasBaton && BatonOn && IsAlive;
		if ( !lit )
		{
			if ( _batonHalo.IsValid() ) _batonHalo.Enabled = false;
			if ( _batonCore.IsValid() ) _batonCore.Enabled = false;
			if ( _batonArc.IsValid() ) _batonArc.Enabled = false;
			return;
		}

		_batonHalo ??= BatonSprite( "baton halo" );
		_batonCore ??= BatonSprite( "baton core" );
		if ( _batonHalo is null || _batonCore is null ) return;

		// HL2: the attachment at the tip, pulled back 1 unit along the stick
		var tip = BatonTip;
		Glow( _batonHalo, tip, Game.Random.Float( 4f, 6f ), Game.Random.Float( 0.1f, 0.2f ) );
		Glow( _batonCore, tip, Game.Random.Float( 2f, 3f ), Game.Random.Float( 0.9f, 1f ) );

		if ( _batonArc.IsValid() ) _batonArc.Enabled = false;
		if ( Game.Random.Int( 0, 5 ) == 0 ) BatonArc( tip );
	}

	private static void Glow( SpriteRenderer sprite, Vector3 at, float halfSize, float brightness )
	{
		sprite.Enabled = true;
		sprite.WorldPosition = at;
		sprite.Size = new Vector2( halfSize * 2f, halfSize * 2f );
		sprite.Color = new Color( brightness, brightness, brightness, 1f );
	}

	private SpriteRenderer BatonSprite( string name )
	{
		var sprite = CombineTracer.LoadSprite( "stunstick_glow" );
		if ( sprite is null ) return null;

		var go = new GameObject( GameObject, true, name );
		go.Flags |= GameObjectFlags.NotNetworked | GameObjectFlags.NotSaved;

		var renderer = go.AddComponent<SpriteRenderer>();
		renderer.Sprite = sprite;
		renderer.Billboard = SpriteRenderer.BillboardMode.Always;
		renderer.Additive = true;
		renderer.Lighting = false;
		renderer.Shadows = false;
		return renderer;
	}

	/// <summary>One frame of HL2's TE_BEAMTESLA: from the tip to a random point a few units off it.</summary>
	private void BatonArc( Vector3 tip )
	{
		if ( !_batonArc.IsValid() )
		{
			_batonArc = new GameObject( GameObject, true, "baton arc" );
			_batonArc.Flags |= GameObjectFlags.NotNetworked | GameObjectFlags.NotSaved;
			var line = _batonArc.AddComponent<LineRenderer>();
			_batonArcLine = line;
			line.UseVectorPoints = true;
			line.Lighting = false;
			line.Opaque = false;
			line.Additive = true;
			line.CastShadows = false;
			line.Color = Color.White;
			var material = Material.Load( "hl2_metrocop/fx/physbeam.vmat" );
			if ( material is not null ) line.Texturing = line.Texturing with { Material = material, UnitsPerTexture = 8f };
		}

		// Kept, not looked up: a hidden object's components don't show up in GetComponent
		var arc = _batonArcLine;
		if ( !arc.IsValid() ) return;
		// RandomVector( -6, 2 ) + ( 2, 2, 2 ): up to 4 units off the tip either way
		var end = tip + new Vector3( Game.Random.Float( -4f, 4f ), Game.Random.Float( -4f, 4f ), Game.Random.Float( -4f, 4f ) );
		var amplitude = Game.Random.Float( 1f, 2f );
		var points = new List<Vector3> { tip };
		for ( var i = 1; i < 8; i++ )
			points.Add( Vector3.Lerp( tip, end, i / 8f ) + Vector3.Random * amplitude );
		points.Add( end );

		arc.VectorPoints = points;
		arc.Width = Game.Random.Float( 0.5f, 2f );
		_batonArc.Enabled = true;
	}
}
