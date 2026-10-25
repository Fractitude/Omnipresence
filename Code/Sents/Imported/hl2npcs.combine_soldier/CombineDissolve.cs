using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;

namespace Hl2Combine;

/// <summary>
/// HL2's energy dissolve (env_entity_dissolver, what a combine ball does to a body): the corpse
/// loses its weight and drifts up, turns black under crawling lightning and sparks, then is gone.
/// Purely visual, runs locally on every client.
/// </summary>
public sealed class CombineDissolve : Component
{
	// Same timeline as HL2's C_EntityDissolve
	private const float BlackAt = 1.0f;
	private const float FadeOutStart = 2.0f;
	private const float FadeOutEnd = 2.5f;

	private ModelPhysics _physics;
	private List<SkinnedModelRenderer> _renderers;
	private TimeSince _age;
	private TimeSince _sinceEffect;
	private readonly List<(GameObject Effect, float Dies)> _effects = new();

	protected override void OnStart()
	{
		_age = 0;
		_physics = GetComponent<ModelPhysics>();
		_renderers = GameObject.Components.GetAll<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants ).ToList();

		foreach ( var rb in Bodies() )
		{
			rb.Gravity = false;
			rb.LinearDamping = 1.5f;
			rb.AngularDamping = 1f;
			rb.Velocity = Vector3.Up * Game.Random.Float( 40f, 70f ) + Vector3.Random * 25f;
			rb.AngularVelocity = Vector3.Random * 1.5f;
		}
	}

	private IEnumerable<Rigidbody> Bodies()
	{
		if ( !_physics.IsValid() ) yield break;
		foreach ( var body in _physics.Bodies )
		{
			if ( body.Component.IsValid() ) yield return body.Component;
		}
	}

	protected override void OnUpdate()
	{
		var t = (float)_age;

		// Black, then gone
		var dark = Math.Clamp( t / BlackAt, 0f, 1f );
		var alpha = 1f - Math.Clamp( (t - FadeOutStart) / (FadeOutEnd - FadeOutStart), 0f, 1f );
		var tint = Color.Lerp( Color.White, Color.Black, dark ).WithAlpha( alpha );
		foreach ( var renderer in _renderers )
		{
			if ( renderer.IsValid() ) renderer.Tint = tint;
		}

		// Keep it gently rising
		foreach ( var rb in Bodies() )
		{
			if ( rb.Velocity.z < 30f ) rb.Velocity += Vector3.Up * 60f * Time.Delta;
		}

		if ( t < FadeOutEnd && _sinceEffect > 0.06f )
		{
			_sinceEffect = 0;
			SpawnEffects( t );
		}

		var now = Time.Now;
		for ( var i = _effects.Count - 1; i >= 0; i-- )
		{
			if ( _effects[i].Dies > now ) continue;
			if ( _effects[i].Effect.IsValid() ) _effects[i].Effect.Destroy();
			_effects.RemoveAt( i );
		}

		if ( t >= FadeOutEnd )
		{
			Flash( WorldCenter(), 60f, 0.2f );
			Finish();
		}
	}

	/// <summary>
	/// Gone. A player's corpse is what the death camera orbits, so it stays (invisible, frozen)
	/// and the game cleans it up as usual; anything else is removed.
	/// </summary>
	private void Finish()
	{
		if ( !GameObject.Components.Get<DeathCameraTarget>( FindMode.EverythingInSelf ).IsValid() )
		{
			GameObject.Destroy();
			return;
		}

		foreach ( var renderer in _renderers )
		{
			if ( !renderer.IsValid() ) continue;
			renderer.Tint = Color.Black.WithAlpha( 0f );
			renderer.RenderType = ModelRenderer.ShadowRenderType.Off;
		}

		foreach ( var rb in Bodies() )
			rb.MotionEnabled = false;

		Destroy();
	}

	protected override void OnDestroy()
	{
		foreach ( var (effect, _) in _effects )
		{
			if ( effect.IsValid() ) effect.Destroy();
		}
	}

	private Vector3 WorldCenter()
	{
		var bodies = Bodies().ToList();
		if ( bodies.Count == 0 ) return WorldPosition;
		var sum = Vector3.Zero;
		foreach ( var rb in bodies ) sum += rb.WorldPosition;
		return sum / bodies.Count;
	}

	private Vector3 RandomBodyPoint()
	{
		var bodies = Bodies().ToList();
		if ( bodies.Count == 0 ) return WorldPosition + Vector3.Random * 20f;
		return Game.Random.FromList( bodies ).WorldPosition + Vector3.Random * 6f;
	}

	private void SpawnEffects( float t )
	{
		// Tesla arcs crawling over the body
		var arcs = t < BlackAt ? 2 : 1;
		for ( var i = 0; i < arcs; i++ )
			Lightning( RandomBodyPoint(), RandomBodyPoint() );

		// White-hot glows on the body (fluttercore)
		Flash( RandomBodyPoint(), Game.Random.Float( 14f, 28f ), 0.12f, "fluttercore" );

		// Blue sparks drifting off
		var spark = Sprite( "bluespark", RandomBodyPoint(), Game.Random.Float( 6f, 10f ), 0.35f );
		if ( spark.IsValid() )
			spark.AddComponent<CombineDrift>().Velocity = Vector3.Up * Game.Random.Float( 30f, 80f ) + Vector3.Random * 30f;
	}

	private void Lightning( Vector3 from, Vector3 to )
	{
		if ( from.Distance( to ) < 6f ) to += Vector3.Random * 20f;

		var go = new GameObject( true, "dissolve arc" );
		go.Flags |= GameObjectFlags.NotNetworked | GameObjectFlags.NotSaved;
		go.WorldPosition = from;

		var line = go.AddComponent<LineRenderer>();
		line.UseVectorPoints = true;
		line.Lighting = false;
		line.Opaque = false;
		line.Additive = true;
		line.CastShadows = false;
		line.Color = new Color( 0.75f, 0.85f, 1f );
		line.Width = Game.Random.Float( 2f, 4f );

		var texture = CombineTracer.EffectTexture( "hl2_combine/fx/lgtning.vmat" );
		if ( texture is not null )
			line.Texturing = line.Texturing with { Texture = texture, UnitsPerTexture = 24f };

		// A jagged path between the two points
		var points = new List<Vector3> { from };
		for ( var i = 1; i < 5; i++ )
			points.Add( Vector3.Lerp( from, to, i / 5f ) + Vector3.Random * 5f );
		points.Add( to );
		line.VectorPoints = points;

		_effects.Add( (go, Time.Now + 0.08f) );
	}

	private void Flash( Vector3 position, float size, float life, string texture = "tesla_glow" )
	{
		Sprite( texture, position, size, life );
	}

	private GameObject Sprite( string texture, Vector3 position, float size, float life )
	{
		var sprite = CombineTracer.LoadSprite( texture );
		if ( sprite is null ) return null;

		var go = new GameObject( true, "dissolve fx" );
		go.Flags |= GameObjectFlags.NotNetworked | GameObjectFlags.NotSaved;
		go.WorldPosition = position;

		var renderer = go.AddComponent<SpriteRenderer>();
		renderer.Sprite = sprite;
		renderer.Billboard = SpriteRenderer.BillboardMode.Always;
		renderer.Additive = true;
		renderer.Lighting = false;
		renderer.Shadows = false;
		renderer.Size = new Vector2( size, size );

		_effects.Add( (go, Time.Now + life) );
		return go;
	}

	/// <summary>
	/// Dissolve whatever body turns up at this spot in the next moment - the corpse of whatever a
	/// ball just killed is created by its own code, a frame or two later.
	/// </summary>
	[Rpc.Broadcast( NetFlags.HostOnly )]
	public static void At( Vector3 position )
	{
		var scene = Game.ActiveScene;
		if ( scene is null ) return;

		using ( scene.Push() )
		{
			var go = new GameObject( true, "dissolve finder" );
			go.Flags |= GameObjectFlags.NotNetworked | GameObjectFlags.NotSaved;
			go.WorldPosition = position;
			go.AddComponent<CombineDissolveFinder>();
		}
	}
}

/// <summary>Waits briefly for a fresh ragdoll near its position and starts dissolving it.</summary>
public sealed class CombineDissolveFinder : Component
{
	private TimeSince _age;

	protected override void OnStart() => _age = 0;

	protected override void OnUpdate()
	{
		if ( _age > 0.6f )
		{
			GameObject.Destroy();
			return;
		}

		ModelPhysics best = null;
		var bestDistance = 160f;
		foreach ( var physics in Scene.GetAllComponents<ModelPhysics>() )
		{
			if ( !physics.IsValid() || physics.GetComponent<CombineDissolve>().IsValid() ) continue;
			if ( !physics.GameObject.Tags.Has( "ragdoll" ) && physics.GameObject.Name != "Ragdoll" ) continue;

			var distance = physics.WorldPosition.WithZ( 0 ).Distance( WorldPosition.WithZ( 0 ) );
			if ( distance >= bestDistance ) continue;

			bestDistance = distance;
			best = physics;
		}

		if ( best is null ) return;

		best.AddComponent<CombineDissolve>();
		GameObject.Destroy();
	}
}

/// <summary>Moves an effect along at a fixed velocity (sparks drifting off).</summary>
public sealed class CombineDrift : Component
{
	public Vector3 Velocity { get; set; }

	protected override void OnUpdate() => WorldPosition += Velocity * Time.Delta;
}
