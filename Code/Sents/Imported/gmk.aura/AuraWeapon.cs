using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Aura. Click to switch it on: the music starts and everything around you gets hurt and thrown
/// away from you - people, NPCs, props, bodies. Click again to switch it off.
/// The aura is a liquid (<see cref="AuraShape"/>): a ball in the open, filling rooms and running along
/// corridors, never through walls. The holder switches it; every client works out the shape, draws it
/// and plays the music itself; the host does the hurting and the throwing, inside that same shape.
/// </summary>
[Title( "Aura" ), Icon( "blur_circular" )]
public sealed class AuraWeapon : BaseSandboxWeapon
{
	/// <summary>Its size out in the open, before any kills; squeezed by walls it runs further.</summary>
	[Property, Group( "Aura" )] public float Radius { get; set; } = 200f;
	[Property, Group( "Aura" )] public float DamagePerSecond { get; set; } = 25f;

	/// <summary>How fast it throws someone right next to you; at the edge of the aura, less than half that.</summary>
	[Property, Group( "Aura" )] public float Push { get; set; } = 900f;

	/// <summary>How much it heals the holder a second while it's on, before any kills.</summary>
	[Property, Group( "Aura" )] public float HealPerSecond { get; set; } = 3f;

	/// <summary>Everyone the aura has killed. Each kill makes it bigger and stronger, each a bit less than the last.</summary>
	[Sync( SyncFlags.FromHost )] public int Kills { get; set; }

	/// <summary>
	/// What the kills have made of it: 1 with none, rising toward 1 + <paramref name="most"/> - quickly at
	/// first, then hardly at all (half the way after 7 kills, nearly all of it after 30).
	/// </summary>
	private float Grown( float most ) => 1f + most * (1f - MathF.Exp( -Kills / 10f ));

	public float CurrentRadius => Radius * Grown( 0.8f );
	public float CurrentDamage => DamagePerSecond * Grown( 1f );
	public float CurrentPush => Push * Grown( 0.5f );
	public float CurrentHeal => HealPerSecond * Grown( 2f );

	[Property, Group( "Aura" )] public SoundEvent Music { get; set; }

	/// <summary>The force field around the holder (set in the prefab, so it and its shader ship with the package).</summary>
	[Property, Group( "Aura" )] public Material BallMaterial { get; set; }

	[Sync] public bool AuraOn { get; set; }

	private const float PulseInterval = 0.1f;

	/// <summary>The track's tempo: the field flares on every beat.</summary>
	private const float Beat = 60f / 90f;
	private const float SenseInterval = 0.05f;

	private AuraShape _shape;
	private AuraShapeObject _field;
	private TimeSince _sinceSense;
	private readonly List<GameObject> _sparkles = new();
	private ParticleEffect _bodyStreaks;
	private ParticleEffect _fieldSparks;
	private float _bodyEmitDebt;
	private float _fieldEmitDebt;
	private Vector3? _lastHolderPosition;
	private SoundHandle _music;
	private TimeSince _sincePulse;
	private TimeSince _sinceOn;
	private bool _wasOn;

	protected override void OnStart()
	{
		UsesAmmo = false;
	}

	public override void PrimaryAttack() { } // upstream's OnPrimaryDown hook is not ported

	protected override void OnControl()
	{
		if ( Input.Pressed( "attack1" ) ) AuraOn = !AuraOn;
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();
		if ( !IsProxy ) AuraOn = false;
		Clear();
	}

	protected override void OnDestroy()
	{
		Clear();
	}

	/// <summary>
	/// Throwing it away leaves nothing lying around (it used to drop a box to pick back up) - it's just
	/// gone, as with the Apple Juice.
	/// </summary>
	protected override bool OnDrop()
	{
		if ( !IsProxy ) AuraOn = false;
		Clear();
		GameObject.Destroy();
		return true;
	}

	protected override void OnUpdate()
	{
		var on = AuraOn && HasOwner;
		if ( on && !_wasOn ) _sinceOn = 0;
		_wasOn = on;
		PlayMusic( on );
		DrawField( on );

		if ( on && Networking.IsHost && _sincePulse >= PulseInterval )
		{
			_sincePulse = 0;
			Pulse();
		}
	}

	// ================= what it does (host)

	/// <summary>Everything inside the aura: hurt once per character, thrown away from the holder body by body.</summary>
	private void Pulse()
	{
		// It looks after the one holding it
		// Owner is the engine's PlayerController here (upstream's is the Player component)
		if ( Owner.GetComponent<Player>() is { Health: > 0f } owner )
			owner.Health = MathF.Min( owner.Health + CurrentHeal * PulseInterval, owner.MaxHealth );

		if ( _shape is null ) return;
		var me = Owner.GameObject;
		var from = Owner.WorldPosition;
		var center = Center;
		var hurt = new HashSet<GameObject>();
		var pushed = new HashSet<Rigidbody>();

		foreach ( var go in Scene.FindInPhysics( new Sphere( center, _shape.Reach ) ) )
		{
			if ( !go.IsValid() ) continue;
			var root = go.Root;
			if ( root == me ) continue;

			// Only what the liquid actually reaches - nothing behind a wall
			var character = root.GetComponent<Player>().IsValid() || root.GetComponent<NavMeshAgent>().IsValid();
			var point = character ? root.WorldPosition + Vector3.Up * 36f : go.WorldPosition;
			if ( !_shape.Contains( point ) && !_shape.Contains( go.WorldPosition ) ) continue;

			var away = point - center;
			var falloff = 1f - Math.Clamp( away.Length / MathF.Max( CurrentRadius, 1f ), 0f, 1f );
			var flat = away.WithZ( 0f );
			var dir = (flat.Length > 1f ? flat.Normal : Vector3.Random.WithZ( 0f ).Normal) + Vector3.Up * 0.35f;
			var speed = CurrentPush * (0.4f + 0.6f * falloff);

			if ( hurt.Add( root ) && root.Components.Get<Component.IDamageable>( FindMode.EverythingInSelfAndDescendants ) is { } damageable )
			{
				// A kill is someone alive before this hit and dead (or gone) after it
				var victim = (Component)root.GetComponent<Player>() ?? root.GetComponent<Sandbox.Npcs.Npc>();
				var aliveBefore = victim.IsValid() && HealthOf( victim ) > 0f;

				damageable.OnDamage( new DamageInfo( CurrentDamage * PulseInterval, me, GameObject )
				{
					Position = go.WorldPosition,
					Origin = from,
				} );

				if ( aliveBefore && (!victim.IsValid() || HealthOf( victim ) <= 0f) ) Kills++;
			}

			if ( root.GetComponent<Player>() is { } player )
			{
				if ( pushed.Add( player.GetComponent<Rigidbody>() ) )
					PushPlayer( player, dir.Normal * speed );
				continue;
			}

			// A walking NPC: its body isn't physical, the navmesh moves it - slide it away along that
			if ( root.GetComponent<NavMeshAgent>() is { } agent && root.GetComponent<Rigidbody>() is { MotionEnabled: false } npcBody )
			{
				if ( pushed.Add( npcBody ) )
					(root.GetComponent<AuraKnockback>() ?? root.AddComponent<AuraKnockback>()).Push( dir.WithZ( 0f ).Normal * speed );
				continue;
			}

			// A collider's own body, or the body it belongs to (a prop, each piece of a corpse)
			if ( go.GetComponentInParent<Rigidbody>() is { MotionEnabled: true } body && pushed.Add( body ) )
				Throw( body, dir.Normal, speed );
		}
	}

	private static float HealthOf( Component character ) => character switch
	{
		Player player => player.Health,
		Sandbox.Npcs.Npc npc => npc.Health,
		_ => 0f,
	};

	/// <summary>Not a shove that adds up every pulse: it brings them up to the speed, away from you.</summary>
	private static void Throw( Rigidbody body, Vector3 dir, float speed )
	{
		var along = body.Velocity.Dot( dir );
		if ( along < speed ) body.Velocity += dir * (speed - along);
	}

	/// <summary>A player's body moves on their own machine, so the push is sent to them.</summary>
	[Rpc.Broadcast( NetFlags.HostOnly )]
	private void PushPlayer( Player player, Vector3 velocity )
	{
		if ( !player.IsValid() || player.IsProxy ) return;
		if ( player.Controller.IsValid() ) player.Controller.PreventGrounding( 0.2f );
		if ( player.GetComponent<Rigidbody>() is { } body )
			Throw( body, velocity.Normal, velocity.Length );
	}

	// ================= what everyone sees and hears

	private void PlayMusic( bool on )
	{
		if ( !on )
		{
			if ( _music.IsValid() ) _music.Stop( 0.5f );
			_music = default;
			return;
		}

		if ( Music is null || _music.IsValid() && !_music.IsStopped ) return;
		_music = Sound.Play( Music, Owner.WorldPosition );
		if ( !_music.IsValid() ) return;
		_music.Parent = Owner.GameObject;
		_music.FollowParent = true;
	}

	/// <summary>The middle of the aura: the holder's chest.</summary>
	private Vector3 Center => Owner.WorldPosition + Vector3.Up * 36f;

	/// <summary>
	/// The force field: the liquid's shape, drawn with the field material. It pours out from the holder's
	/// chest when switched on, feels for the walls ten times a second and flows to fit them, and its rim
	/// flares on every beat - timed by the music itself, so it stays on the beat. The holder, inside it,
	/// sees a fainter one.
	/// </summary>
	private void DrawField( bool on )
	{
		if ( !on )
		{
			if ( _field.IsValid() ) _field.RenderingEnabled = false;
			foreach ( var fx in _sparkles )
				if ( fx.IsValid() ) fx.Enabled = false;
			return;
		}

		if ( _sparkles.Count == 0 )
		{
			// Streaks rising up the body like flames, and a few sparks drifting up all through the liquid -
			// both placed by hand (EmitParticles), so neither comes out on the far side of a wall
			_bodyStreaks = Sparkles( 0f, Vector3.Zero, 0f, 160f, 0.5f, 1f, 3f, 6f );
			_fieldSparks = Sparkles( 0f, Vector3.Zero, 0f, 40f, 1.2f, 2.2f, 1f, 2f );
		}

		foreach ( var fx in _sparkles )
			if ( fx.IsValid() ) fx.Enabled = true;
		EmitParticles();

		if ( _shape is null )
		{
			_shape = new AuraShape();
			_sinceSense = SenseInterval;
		}

		if ( !_field.IsValid() && Scene.SceneWorld is { } world )
		{
			_field = new AuraShapeObject( world, _shape ) { Material = BallMaterial };
		}

		if ( !_field.IsValid() ) return;
		if ( !_field.RenderingEnabled )
		{
			_field.RenderingEnabled = true;
			_shape.Reset();
			_sinceSense = SenseInterval;
		}

		_shape.TestCells( Scene, Owner.GameObject );
		if ( _sinceSense >= SenseInterval )
		{
			_sinceSense = 0;
			_shape.Spread( Scene, Owner.GameObject, Center, CurrentRadius, _sinceOn );
		}
		_shape.Flow( Time.Delta );

		var grow = MathF.Min( 1f, _sinceOn / 0.35f );
		var time = _music.IsValid() && _music.ElapsedTime > 0f ? _music.ElapsedTime : (float)_sinceOn;
		var beat = MathF.Exp( -(time % Beat / Beat) * 5f );
		_field.Beat = beat;
		_field.Opacity = grow * (Owner.IsProxy ? 1f : 0.6f);
	}

	/// <summary>Placed right before drawing, so it sits exactly on the holder and never trails a frame behind.</summary>
	protected override void OnPreRender()
	{
		base.OnPreRender();
		if ( !_field.IsValid() || !_field.RenderingEnabled || !HasOwner || _shape is null ) return;
		_field.Upload();
	}

	/// <summary>
	/// The streaks round the body, 120 a second, born only where there's no wall between them and the
	/// holder (standing against a wall they'd come out on its other side); the sparks, 30 a second, born
	/// only in cells the liquid fills. Both die on hitting a wall.
	/// </summary>
	private void EmitParticles()
	{
		// They're carried along with the holder: born in the world, they'd be left behind by someone running
		var here = Owner.WorldPosition;
		var moved = _lastHolderPosition is { } last ? here - last : Vector3.Zero;
		_lastHolderPosition = here;
		if ( moved.Length > 200f ) moved = Vector3.Zero;   // a teleport, not a run
		if ( !moved.IsNearlyZero() )
		{
			if ( _bodyStreaks.IsValid() ) foreach ( var p in _bodyStreaks.Particles ) p.Position += moved;
			if ( _fieldSparks.IsValid() ) foreach ( var p in _fieldSparks.Particles ) p.Position += moved;
		}

		if ( _fieldSparks.IsValid() && _shape is not null )
		{
			// A spark that drifts out of the liquid is gone
			foreach ( var spark in _fieldSparks.Particles )
				if ( !_shape.Contains( spark.Position ) ) spark.DeathTime = Time.Now;

			_fieldEmitDebt += 30f * Time.Delta;
			var min = _shape.ActiveMin;
			var size = _shape.ActiveMax - min;
			for ( ; _fieldEmitDebt >= 1f; _fieldEmitDebt -= 1f )
			{
				for ( var attempt = 0; attempt < 8; attempt++ )
				{
					var at = min + new Vector3( Game.Random.Float( 0f, size.x ), Game.Random.Float( 0f, size.y ), Game.Random.Float( 0f, size.z ) );
					if ( !_shape.Contains( at ) ) continue;
					_fieldSparks.Emit( at, 0f );
					break;
				}
			}
		}

		if ( !_bodyStreaks.IsValid() ) return;
		_bodyEmitDebt += 120f * Time.Delta;
		var from = Owner.WorldPosition + Vector3.Up * 30f;
		for ( ; _bodyEmitDebt >= 1f; _bodyEmitDebt -= 1f )
		{
			var angle = Game.Random.Float( 0f, MathF.PI * 2f );
			var distance = Game.Random.Float( 18f, 26f );
			var at = Owner.WorldPosition + new Vector3( MathF.Cos( angle ) * distance, MathF.Sin( angle ) * distance, Game.Random.Float( 4f, 14f ) );
			var tr = Scene.Trace.Ray( from, at ).IgnoreGameObjectHierarchy( Owner.GameObject )
				.WithoutTags( "player", "npc", "trigger", "ragdoll", "playercontroller", "debris" ).Run();
			if ( !tr.Hit ) _bodyStreaks.Emit( at, 0f );
		}
	}

	/// <summary>
	/// A cloud of purple streaks fixed to the holder, made from the sandbox's own sparks (without their light):
	/// born anywhere in a squashed sphere of <paramref name="radius"/> and pushed up by <paramref name="rise"/>.
	/// </summary>
	/// With no <paramref name="rate"/> it emits nothing itself - the particles are placed by hand, and die
	/// on hitting a wall.
	private ParticleEffect Sparkles( float radius, Vector3 spread, float rate, float rise, float lifeMin, float lifeMax, float sizeMin, float sizeMax )
	{
		var fx = GameObject.Clone( "entities/particles/fx_sparks.prefab" );
		if ( !fx.IsValid() ) return null;
		fx.Flags |= GameObjectFlags.NotNetworked | GameObjectFlags.NotSaved;
		fx.NetworkMode = NetworkMode.Never;
		fx.SetParent( Owner.GameObject, false );
		fx.LocalPosition = Vector3.Up * 6f;
		fx.LocalRotation = Rotation.Identity;

		foreach ( var light in fx.Components.GetAll<ParticleLightRenderer>( FindMode.EverythingInSelfAndDescendants ).ToList() )
			light.Destroy();

		foreach ( var cone in fx.Components.GetAll<ParticleConeEmitter>( FindMode.EverythingInSelfAndDescendants ).ToList() )
		{
			var host = cone.GameObject;
			cone.Destroy();
			if ( rate <= 0f ) continue;
			var emitter = host.AddComponent<ParticleSphereEmitter>();
			emitter.Radius = radius;
			emitter.DistanceBias = spread;
			emitter.Velocity = 0f;
			emitter.Rate = rate;
			emitter.Loop = true;
			emitter.Duration = 5f;
		}

		ParticleEffect first = null;
		foreach ( var effect in fx.Components.GetAll<ParticleEffect>( FindMode.EverythingInSelfAndDescendants ) )
		{
			first ??= effect;
			effect.Collision = rate <= 0f;
			effect.DieOnCollisionChance = 1f;
			effect.CollisionRadius = 1f;
			effect.Force = true;
			effect.ForceDirection = Vector3.Up * rise;
			effect.StartVelocity = new ParticleFloat( rise * 0.2f, rise * 0.5f );
			effect.InitialVelocity = Vector3.Up;
			effect.Lifetime = new ParticleFloat( lifeMin, lifeMax );
			effect.Scale = new ParticleFloat( sizeMin, sizeMax );
			// Purple instead of the sparks' own orange-to-dark over their life; dim, or it burns out to white
			effect.ApplyColor = true;
			effect.Gradient = new Color( 0.7f, 0.3f, 1f );
			effect.Tint = Color.White;
			effect.Brightness = 1f;
			effect.MaxParticles = 250;
		}

		_sparkles.Add( fx );
		return first;
	}

	private void Clear()
	{
		if ( _music.IsValid() ) _music.Stop( 0.3f );
		_music = default;

		if ( _field.IsValid() ) _field.Delete();
		_field = null;
		_shape = null;

		foreach ( var fx in _sparkles )
			if ( fx.IsValid() ) fx.Destroy();
		_sparkles.Clear();
		_bodyStreaks = null;
		_fieldSparks = null;
		_lastHolderPosition = null;
	}
}

/// <summary>
/// An NPC thrown by the aura (host only). Walking NPCs aren't physics bodies, so it slides them along
/// the navmesh instead - fast at first, slowing to a stop - and then takes itself off.
/// </summary>
public sealed class AuraKnockback : Component
{
	private Vector3 _velocity;

	/// <summary>Brought up to this speed, as the aura throws everything else (it doesn't add up every pulse).</summary>
	public void Push( Vector3 velocity )
	{
		var dir = velocity.Normal;
		var along = _velocity.Dot( dir );
		if ( along < velocity.Length ) _velocity += dir * (velocity.Length - along);
	}

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;

		var step = _velocity * Time.Delta;
		_velocity *= MathF.Pow( 0.02f, Time.Delta );
		if ( _velocity.Length < 20f )
		{
			Destroy();
			return;
		}

		// Stopped by walls
		var from = WorldPosition + Vector3.Up * 30f;
		var tr = Scene.Trace.Ray( from, from + step + step.Normal * 16f ).IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( "player", "npc", "trigger", "ragdoll", "playercontroller", "debris" ).Run();
		if ( tr.Hit ) { _velocity = 0; return; }

		var to = WorldPosition + step;
		if ( GetComponent<NavMeshAgent>() is { } agent ) agent.SetAgentPosition( to );
		WorldPosition = to;
	}
}
