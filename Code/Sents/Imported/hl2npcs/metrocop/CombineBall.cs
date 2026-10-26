using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;
using Sandbox.Npcs;

namespace Hl2Metrocop;

/// <summary>
/// The AR2's alt-fire: an energy ball that bounces off walls and kills anything alive it touches,
/// passing straight through. Soldiers on the shooter's side are left alone. Moved by the host.
/// </summary>
public sealed class CombineBall : Component
{
	public const float Speed = 1100f;
	public const float Radius = 10f;
	public const float Lifetime = 4f;
	public const int MaxBounces = 6;
	public const float Damage = 1000f;

	[Sync] public Vector3 Velocity { get; set; }

	/// <summary>Who fired it (host only).</summary>
	public CombineNpc Shooter { get; set; }

	/// <summary>Everyone the shooter didn't consider an enemy when firing: the ball goes through them.</summary>
	public HashSet<GameObject> Friends { get; } = new();

	private readonly HashSet<GameObject> _passed = new();
	private TimeSince _alive;
	private int _bounces;
	private bool _done;

	// Drawn the way HL2's C_PropCombineBall does it: the ball model turned to face the camera,
	// a flickering ar2_altfire1 halo, and a short motion blur of halos behind it
	private GameObject _body;
	private SpriteRenderer _flicker;
	private readonly List<SpriteRenderer> _blur = new();
	private PointLight _light;
	private Vector3 _lastPosition;
	private SoundHandle _hum;

	protected override void OnStart()
	{
		_alive = 0;
		_lastPosition = WorldPosition;

		_body = new GameObject( GameObject, true, "ball body" );
		_body.Flags |= GameObjectFlags.NotNetworked | GameObjectFlags.NotSaved;
		_body.LocalScale = Radius / 12f;
		var model = _body.AddComponent<ModelRenderer>();
		model.Model = Model.Load( "hl2_metrocop/models/effects/combineball.vmdl" );
		model.RenderType = ModelRenderer.ShadowRenderType.Off;

		_flicker = Halo( "ar2_altfire1b", "flicker" );
		if ( FlickerSprite is { } animated ) _flicker.Sprite = animated;

		for ( var i = 0; i < 8; i++ )
			_blur.Add( Halo( "ar2_altfire1b", "blur" ) );

		_light = GameObject.AddComponent<PointLight>();
		_light.LightColor = new Color( 0.6f, 0.8f, 1f ) * 2f;
		_light.Radius = 160f;
		_light.Shadows = false;
	}

	// HL2's ar2_altfire1 material is the animated "effects/comball/comball" texture: 27 frames at 25 fps
	private static Sprite _flickerSprite;
	private static Sprite FlickerSprite
	{
		get
		{
			if ( _flickerSprite is not null ) return _flickerSprite;
			var frames = new List<Texture>();
			for ( var i = 0; i < 27; i++ )
			{
				var texture = CombineTracer.EffectTexture( $"hl2_metrocop/fx/comball/comball_{i:00}.vmat" );
				if ( texture is not null ) frames.Add( texture );
			}
			_flickerSprite = frames.Count > 0 ? Sprite.FromTextures( frames, 25f ) : null;
			return _flickerSprite;
		}
	}

	private SpriteRenderer Halo( string texture, string name )
	{
		var go = new GameObject( true, name );
		go.Flags |= GameObjectFlags.NotNetworked | GameObjectFlags.NotSaved;
		go.WorldPosition = WorldPosition;

		var sprite = go.AddComponent<SpriteRenderer>();
		sprite.Sprite = CombineTracer.LoadSprite( texture );
		sprite.Billboard = SpriteRenderer.BillboardMode.Always;
		sprite.Additive = true;
		sprite.Lighting = false;
		sprite.Shadows = false;
		return sprite;
	}

	protected override void OnDestroy()
	{
		if ( _flicker.IsValid() ) _flicker.GameObject.Destroy();
		foreach ( var blur in _blur )
		{
			if ( blur.IsValid() ) blur.GameObject.Destroy();
		}
		if ( _hum.IsValid() ) _hum.Stop( 0.1f );
	}

	protected override void OnUpdate()
	{
		var position = WorldPosition;

		// The model always faces the viewer
		if ( _body.IsValid() && Scene.Camera.IsValid() )
			_body.WorldRotation = Rotation.LookAt( Scene.Camera.WorldPosition - position );

		// Flicker: a random size every frame (1.5-2.5 x radius, as in HL2)
		if ( _flicker.IsValid() )
		{
			_flicker.WorldPosition = position;
			var size = 2f * Game.Random.Float( 1.5f, 2.5f ) * Radius;
			_flicker.Size = new Vector2( size, size );
		}

		// Motion blur: halos stepping back along the way it came, fading out
		var moved = position - _lastPosition;
		var speed = MathF.Min( moved.Length, 32f );
		var back = speed > 0.01f ? -moved.Normal * MathF.Min( speed * 0.5f, 4f ) : Vector3.Zero;
		var strength = Math.Clamp( (speed - 4f) / 28f, 0f, 1f );
		for ( var i = 0; i < _blur.Count; i++ )
		{
			var blur = _blur[i];
			if ( !blur.IsValid() ) continue;
			blur.WorldPosition = position + back * (i + 1);
			var fade = i < 5 ? strength : strength * (1f - (i - 5) / 6f);
			blur.Color = Color.White.WithAlpha( fade );
			blur.Size = new Vector2( Radius * 2f, Radius * 2f );
		}
		_lastPosition = position;

		// The hum is a short sample: start it again whenever it ends
		if ( !_hum.IsValid() || _hum.IsStopped )
		{
			var hum = ResourceLibrary.Get<SoundEvent>( "hl2_metrocop/sounds/ball_hum.sound" );
			if ( hum is not null )
			{
				_hum = Sound.Play( hum, WorldPosition );
				if ( _hum.IsValid() )
				{
					_hum.Parent = GameObject;
					_hum.FollowParent = true;
				}
			}
		}
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy || _done ) return;

		if ( _alive > Lifetime || _bounces > MaxBounces )
		{
			Explode();
			return;
		}

		var from = WorldPosition;
		var step = Velocity * Time.Delta;

		var trace = Scene.Trace.Ray( from, from + step )
			.Radius( Radius )
			.IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( "trigger", "ragdoll", "playercontroller", "grenade" )
			.UseHitboxes();

		foreach ( var passed in _passed )
		{
			if ( passed.IsValid() ) trace = trace.IgnoreGameObjectHierarchy( passed );
		}

		var tr = trace.Run();
		if ( !tr.Hit || !tr.GameObject.IsValid() )
		{
			WorldPosition = from + step;
			return;
		}

		var root = tr.GameObject.Root;
		var alive = root.GetComponent<Npc>().IsValid() || root.GetComponent<Player>().IsValid();

		if ( alive )
		{
			// Straight through: kill it (unless it's on our side) and keep going
			_passed.Add( root );
			if ( !Friends.Contains( root ) ) Hit( root, tr.EndPosition, from );
			WorldPosition = from + step;
			return;
		}

		// Props get shoved, walls send it back
		if ( tr.Body is { } body && body.MotionEnabled )
			body.ApplyImpulseAt( tr.EndPosition, Velocity.Normal * body.Mass * 400f );

		var normal = tr.Normal;
		Velocity = (Velocity - 2f * Velocity.Dot( normal ) * normal).Normal * Speed;
		WorldPosition = tr.EndPosition + normal * 2f;
		_bounces++;
		BounceEffect( WorldPosition );
	}

	private void Hit( GameObject victim, Vector3 position, Vector3 origin )
	{
		var damage = new DamageInfo( Damage, Shooter.IsValid() ? Shooter.GameObject : GameObject, GameObject )
		{
			Position = position,
			Origin = origin,
		};

		// Remember who it was before the hit: a killed NPC removes itself straight away
		var npc = victim.GetComponent<Sandbox.Npcs.Npc>();
		var player = victim.GetComponent<Player>();
		var where = victim.WorldPosition;

		foreach ( var damageable in victim.Components.GetAll<Component.IDamageable>() )
			damageable.OnDamage( damage );

		KillEffect( position );

		// Killed: the body dissolves, as in HL2
		var dead = npc is not null ? !npc.IsValid() || npc.Health < 1f
			: player is not null && (!player.IsValid() || player.Health < 1f);
		if ( dead ) CombineDissolve.At( where );
	}

	private void Explode()
	{
		_done = true;
		ExplodeEffect( WorldPosition );
		GameObject.Destroy();
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	private void BounceEffect( Vector3 position ) => PlayAt( "ball_bounce", position );

	[Rpc.Broadcast( NetFlags.HostOnly )]
	private void KillEffect( Vector3 position ) => PlayAt( "ball_kill", position );

	[Rpc.Broadcast( NetFlags.HostOnly )]
	private void ExplodeEffect( Vector3 position )
	{
		PlayAt( "ball_explode", position );

		var flash = new GameObject( true, "ball flash" );
		flash.Flags |= GameObjectFlags.NotNetworked | GameObjectFlags.NotSaved;
		flash.WorldPosition = position;
		var light = flash.AddComponent<PointLight>();
		light.LightColor = new Color( 0.6f, 0.8f, 1f ) * 6f;
		light.Radius = 400f;
		light.Shadows = false;
		flash.AddComponent<TemporaryEffect>().DestroyAfterSeconds = 0.15f;
	}

	private static void PlayAt( string sound, Vector3 position )
	{
		var ev = ResourceLibrary.Get<SoundEvent>( $"hl2_metrocop/sounds/{sound}.sound" );
		if ( ev is not null ) Sound.Play( ev, position );
	}

	/// <summary>Every NPC and player in the scene.</summary>
	public static IEnumerable<GameObject> Agents( Scene scene )
	{
		foreach ( var npc in scene.GetAllComponents<Sandbox.Npcs.Npc>() )
		{
			if ( npc.IsValid() && npc.Health >= 1f ) yield return npc.GameObject;
		}
		foreach ( var player in scene.GetAllComponents<Player>() )
		{
			if ( player.IsValid() && player.Health >= 1f ) yield return player.GameObject;
		}
	}

	/// <summary>
	/// Follow the ball's path, bounces and all, and report the closest it comes to anyone the
	/// shooter wouldn't want to hit.
	/// </summary>
	public static float ClosestPassToFriends( CombineNpc shooter, Vector3 from, Vector3 direction, float distance = 2200f )
	{
		var friends = Agents( shooter.Scene )
			.Where( a => a != shooter.GameObject && shooter.GetDisposition( a ) != Disposition.Hostile )
			.Select( a => a.WorldPosition + Vector3.Up * 40f )
			.ToList();
		if ( friends.Count == 0 ) return float.MaxValue;

		var closest = float.MaxValue;
		var position = from;
		var dir = direction.Normal;
		var left = distance;

		for ( var bounce = 0; bounce <= 4 && left > 0f; bounce++ )
		{
			var tr = shooter.Scene.Trace.Ray( position, position + dir * left )
				.Radius( Radius )
				.IgnoreGameObjectHierarchy( shooter.GameObject )
				.WithoutTags( "trigger", "ragdoll", "playercontroller", "grenade", "player", "npc" )
				.Run();

			var end = tr.Hit ? tr.EndPosition : position + dir * left;
			foreach ( var friend in friends )
				closest = MathF.Min( closest, DistanceToSegment( friend, position, end ) );

			if ( !tr.Hit ) break;
			left -= position.Distance( end );
			dir = dir - 2f * dir.Dot( tr.Normal ) * tr.Normal;
			position = end + tr.Normal * 2f;
		}

		return closest;
	}

	private static float DistanceToSegment( Vector3 point, Vector3 a, Vector3 b )
	{
		var ab = b - a;
		var t = ab.LengthSquared > 0.01f ? Math.Clamp( (point - a).Dot( ab ) / ab.LengthSquared, 0f, 1f ) : 0f;
		return point.Distance( a + ab * t );
	}

	/// <summary>Host: launch a ball.</summary>
	public static void Fire( CombineNpc shooter, Vector3 from, Vector3 direction )
	{
		var go = new GameObject( true, "combine ball" );
		go.WorldPosition = from;
		go.Tags.Add( "combineball" );

		var ball = go.AddComponent<CombineBall>();
		ball.Shooter = shooter;
		ball.Velocity = direction.Normal * Speed;

		if ( shooter.IsValid() )
		{
			ball.Friends.Add( shooter.GameObject );
			foreach ( var agent in Agents( shooter.Scene ) )
			{
				if ( shooter.GetDisposition( agent ) != Disposition.Hostile ) ball.Friends.Add( agent );
			}
		}

		go.NetworkSpawn();
	}
}
