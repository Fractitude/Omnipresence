using Sandbox;
using Sandbox.Navigation;
using Sandbox.Npcs;
using Sandbox.Npcs.Layers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Hl2Manhack;

/// <summary>
/// HL2's manhack (npc_manhack.cpp), retold for the sandbox. It drifts after its enemy at head height,
/// a little sloppy - it can't turn on a dime, and it bobs. Close in (200 units), level with the enemy's
/// body and heading at them, it commits to a one-second burst: blades out (that click), straight at the
/// eyes. Touching someone grinds them every moment it touches; it's bounced off, the burst ends and it
/// waits two seconds before the next. Walls throw it off with sparks. Bullets stall its engine and
/// slow its blades; a melee hit bats it away, stunned. Below half health it trails sparks. Killed, it
/// either bursts apart at once or spins out of control and breaks up when it hits something.
/// In a physgun it stops flying but its blades still cut anyone but the holder; thrown hard into
/// something it breaks. It remembers where its enemy is for 30 seconds after losing sight of them.
/// Only the host thinks; everyone sees the blades, the eye and hears the engine.
/// </summary>
public sealed partial class ManhackNpc : Npc, Component.ICollisionListener
{
	public const string FactionName = "combine";

	/// <summary>Print what each manhack bumps into, cuts and gets hurt by.</summary>
	[ConVar( "manhack_debug" )]
	public static bool Debug { get; set; }

	// HL2's numbers (npc_manhack.h/.cpp, skill.cfg)
	private const float MaxSpeed = 500f;              // MANHACK_MAX_SPEED
	private const float ThrownMaxSpeed = 800f;        // MANHACK_NPC_BURST_SPEED
	private const float ChargeMinDist = 200f;         // MANHACK_CHARGE_MIN_DIST
	private const float SmashTime = 0.35f;            // MANHACK_SMASH_TIME
	private const float SmashSpeed = 500f;            // MANHACK_SMASH_SPEED
	private const float FreeKnowledge = 30f;          // knows where the enemy is this long after losing sight
	private const float GroundHeight = 52f;           // keeps at least this far off the floor
	private const float FullHealth = 25f;             // sk_manhack_health
	private const float BodyRadius = 13f;

	/// <summary>Grinding damage per second while touching someone (sk_manhack_melee_dmg); twice that on players.</summary>
	[Property, Group( "Combat" )] public float BladeDamage { get; set; } = 20f;
	[Property, Group( "Combat" )] public float SightRange { get; set; } = 2500f;
	/// <summary>Senses an enemy through walls this close.</summary>
	[Property, Group( "Combat" )] public float HearingRange { get; set; } = 700f;
	/// <summary>How far from where it was spawned it drifts about with no one to chase.</summary>
	[Property, Group( "Flight" )] public float WanderRadius { get; set; } = 400f;

	[Property, Group( "Effects" )] public SkinnedModelRenderer Renderer { get; set; }
	/// <summary>HL2's six pieces it breaks into (models/gibs/manhack_gib01-06). Listed here so they ship with it.</summary>
	[Property, Group( "Effects" )] public List<Model> Gibs { get; set; } = new();
	[Property, Group( "Sounds" )] public SoundEvent EngineSound { get; set; }
	[Property, Group( "Sounds" )] public SoundEvent Engine2Sound { get; set; }
	[Property, Group( "Sounds" )] public SoundEvent EngineNoiseSound { get; set; }
	[Property, Group( "Sounds" )] public SoundEvent GrindSound { get; set; }
	[Property, Group( "Sounds" )] public SoundEvent SliceSound { get; set; }
	[Property, Group( "Sounds" )] public SoundEvent DieSound { get; set; }
	[Property, Group( "Sounds" )] public SoundEvent BatSound { get; set; }
	[Property, Group( "Sounds" )] public SoundEvent ChargeAnnounceSound { get; set; }
	[Property, Group( "Sounds" )] public SoundEvent ChargeEndSound { get; set; }
	[Property, Group( "Sounds" )] public SoundEvent StunnedSound { get; set; }

	public enum EyeLook { Normal, Charge, Stunned }

	/// <summary>0-100. Slow: you see the blades; fast: only the blur. The fly animation spins at this rate.</summary>
	[Sync( SyncFlags.FromHost )] public float BladeSpeed { get; private set; }
	[Sync( SyncFlags.FromHost )] public EyeLook Eye { get; private set; }
	/// <summary>Distance to its enemy, for the engine pitch everyone hears (0 = none).</summary>
	[Sync( SyncFlags.FromHost )] public float EnemyDistance { get; private set; }
	[Sync( SyncFlags.FromHost )] public bool Crashing { get; private set; }
	/// <summary>Panels flared open for the attack run (HL2's ShowHostile).</summary>
	[Sync( SyncFlags.FromHost )] public bool PanelsOpen { get; private set; }

	private const string SparksPrefab = "entities/particles/fx_sparks.prefab";
	private const string BloodPrefab = "entities/particles/fx_blood_spray.prefab";
	private static readonly string[] NotObstacles = { "trigger", "manhack" };
	private static readonly string[] NotFloor = { "trigger", "manhack", "player", "npc" };

	private Rigidbody _rb;
	private Vector3 _home;
	private bool _dead;
	private DamageInfo _death;
	private TimeSince _sinceCrash;
	private TimeUntil _nextCrashSpark;

	// the enemy
	private GameObject _target;
	private Vector3 _targetPos;
	private bool _targetVisible;
	private TimeSince _sinceSeen = 100f;
	private TimeSince _sincePerceive;
	private TimeSince _sinceNet;

	// the swarm (HL2's attack slots): only so many go for one enemy at once, the rest circle close by
	public static int MaxAttackers { get; set; } = 4;
	public static float SlotTime { get; set; } = 5f;
	private static readonly Dictionary<GameObject, List<ManhackNpc>> Attackers = new();
	/// <summary>Holds one of the attack places on its enemy (otherwise it circles, waiting its turn).</summary>
	public bool Attacking { get; private set; }
	private TimeSince _slotSince;
	private TimeUntil _slotRest;
	private float _orbitAngle = Game.Random.Float( 0f, 360f );
	private readonly float _orbitTurn = Game.Random.Float( 30f, 55f ) * (Game.Random.Int( 1 ) == 0 ? 1f : -1f);
	private readonly float _orbitRadius = Game.Random.Float( 220f, 320f );

	// the attack run
	private TimeUntil _burstEnds;
	private TimeUntil _nextBurst;
	private Vector3 _burstDir;
	private bool _hostile;

	// engine
	private TimeUntil _stall;
	private TimeUntil _stunned;
	private float _noiseMod;
	private TimeUntil _nextEngineNoise;
	private TimeUntil _bumpSuppress;

	// grinding someone
	private readonly Dictionary<GameObject, float> _grind = new();
	private TimeSince _sinceGrindSound = 10f;
	private TimeSince _sinceSliceSound = 10f;

	// wandering
	private Vector3? _wander;
	private TimeUntil _wanderWait;

	// path round walls
	private readonly List<Vector3> _path = new();
	private int _pathIndex;
	private Vector3 _pathGoal;
	private TimeSince _sincePath;

	// physgun
	private bool _held;
	private Vector3 _prevVelocity;
	private TimeSince _sinceSlam = 10f;
	private GameObject _holder;
	private TimeSince _sinceHeld = 100f;
	private TimeSince _sinceHeldCheck;

	// what everyone sees and hears
	private SoundHandle _engine;
	private SoundHandle _engine2;
	private float _yaw;
	private float _panels;
	private bool _smoking;

	// ================= sides

	public override string Faction => FactionName;

	protected override void SetupRelationships()
	{
		Likes( FactionName );
		Hates( Factions.Player, Factions.Ally, Factions.Citizen, Factions.Monster );
	}

	// ================= life

	protected override void OnStart()
	{
		base.OnStart();
		GameObject.Tags.Add( "manhack" );
		_rb = GetComponent<Rigidbody>();
		_home = WorldPosition;
		_yaw = WorldRotation.Angles().yaw;
		_noiseMod = Game.Random.Float( 1.7f, 2.3f );
		if ( !IsProxy ) BladeSpeed = 100f;

		// The sandbox senses look from a man's eye height - no good for a flying thing; it looks for itself.
		// From them it only takes noises (shots, explosions) to go and see about.
		if ( Senses.IsValid() )
		{
			Senses.SightRange = 1f;
			Senses.HearingRange = 1f;
		}

		if ( _rb.IsValid() ) _rb.Gravity = false;
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		ReleaseSlot();
		StopSounds();
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		UpdateLook();
	}

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();
		if ( _rb.IsValid() ) _prevVelocity = _rb.Velocity;
		if ( IsProxy || _dead || !_rb.IsValid() || !_rb.MotionEnabled ) return;
		var dt = Time.Delta;

		if ( _sinceHeldCheck > 0.1f )
		{
			_sinceHeldCheck = 0;
			var holder = HolderOf();
			if ( holder.IsValid() )
			{
				_holder = holder;
				_sinceHeld = 0;
				StopBurst();
			}
			else if ( _held )
			{
				// Just let go of: the engine stalls a while, as HL2's does after the gravity gun lets go
				_stall = 2f;
				Stun( 2f );
			}
			_held = holder.IsValid() || _rb.Joints.Count > 0;
		}

		if ( _stunned && Eye == EyeLook.Stunned ) Eye = EyeLook.Normal;
		SpinBlades( dt );

		if ( Crashing )
		{
			Crash( dt );
			return;
		}

		if ( _held )
		{
			_yaw = WorldRotation.Angles().yaw;
			_home = WorldPosition;
			GrindWhatsTouching( dt );
			GrindHeld();
			return;
		}

		// A player flies it (ManhackNpc.Pilot.cs) - even with the AI paused
		if ( _controlled )
		{
			PilotFly( dt );
			GrindWhatsTouching( dt );
			return;
		}

		if ( !NpcConVars.Enabled )
		{
			Hover( dt );
			return;
		}

		if ( _sincePerceive > 0.2f )
		{
			_sincePerceive = 0;
			Perceive();
		}

		Fly( dt );
		GrindWhatsTouching( dt );
	}

	// ================= seeing and hearing

	private void Perceive()
	{
		if ( !_target.IsValid() && Senses.IsValid() && Senses.Disturbance is { } noise && _wander is null )
			_wander = noise.Position + Vector3.Up * GroundHeight;

		// "No target" (sandbox.ai.notarget) only means players are off limits; NPCs are still fair game
		var noPlayers = NpcConVars.NoTarget;
		if ( noPlayers && IsPlayer( _target ) ) _target = null;

		var pos = WorldPosition;
		GameObject best = null;
		var bestScore = float.MinValue;
		var bestVisible = false;

		void Consider( GameObject go )
		{
			if ( !go.IsValid() || go == GameObject || GetDisposition( go ) != Disposition.Hostile || !IsAlive( go ) ) return;

			var aim = ChasePoint( go );
			var dist = pos.Distance( aim );
			var visible = dist <= SightRange && CanSee( pos, aim, go );
			if ( !visible && (dist > HearingRange || !CanHear( go )) ) return;

			var score = GetRelationship( go ).Priority * 5000f - dist + (visible ? 1500f : 0f) + (go == _target ? 400f : 0f);
			if ( score <= bestScore ) return;
			best = go;
			bestScore = score;
			bestVisible = visible;
		}

		if ( !noPlayers )
		{
			foreach ( var player in Scene.GetAllComponents<Player>() )
				Consider( player.GameObject );
		}
		if ( Scene.GetSystem<NpcSystem>() is { } npcs )
			foreach ( var npc in npcs.Npcs )
				if ( npc != this ) Consider( npc.GameObject );

		if ( best.IsValid() )
		{
			_target = best;
			_targetVisible = bestVisible;
			_sinceSeen = 0;
			_wander = null;
		}
		else
		{
			_targetVisible = false;
		}

		if ( _sinceNet > 0.5f )
		{
			_sinceNet = 0;
			ListenToNet( pos );
		}

		// HL2 gives manhacks 30 seconds of free knowledge: lost from sight, it still knows where you went
		if ( _target.IsValid() && (_sinceSeen > FreeKnowledge || !IsAlive( _target )) )
			_target = null;
		if ( _target.IsValid() && !_targetVisible && !Reachable( _target ) )
		{
			if ( Debug ) Log.Info( $"[manhack] {GameObject.Id.ToString()[..4]} can't get to {_target.Name}, gives up" );
			_target = null;
		}
		if ( _target.IsValid() )
			_targetPos = ChasePoint( _target );

		EnemyDistance = _target.IsValid() ? pos.Distance( _target.WorldPosition ) : 0f;
	}

	/// <summary>
	/// The Combine net (<see cref="CombineNet"/>): whoever it has eyes on goes out to the soldiers and the
	/// other manhacks; when it sees no one itself, it goes for the nearest enemy someone else has seen.
	/// </summary>
	private void ListenToNet( Vector3 pos )
	{
		if ( _targetVisible && _target.IsValid() )
		{
			CombineNet.ReportContact( Scene, _target );
			return;
		}

		GameObject called = null;
		var nearest = CombineNet.Range;
		foreach ( var enemy in CombineNet.Contacts( Scene, includeOwn: true ) )
		{
			if ( GetDisposition( enemy ) != Disposition.Hostile || !IsAlive( enemy ) ) continue;
			if ( NpcConVars.NoTarget && IsPlayer( enemy ) ) continue;
			if ( !Reachable( enemy ) && !CanSee( pos, ChasePoint( enemy ), enemy ) ) continue;
			if ( enemy == _target ) { called = enemy; break; }
			var dist = pos.Distance( enemy.WorldPosition );
			if ( dist >= nearest ) continue;
			called = enemy;
			nearest = dist;
		}

		if ( !called.IsValid() ) return;
		if ( Debug && called != _target ) Log.Info( $"[manhack] {GameObject.Id.ToString()[..4]} called in on {called.Name}" );
		_target = called;
		_sinceSeen = 0;
		_wander = null;
	}

	/// <summary>Where it goes for: three quarters of the way up their body (HL2's TranslateNavGoal).</summary>
	private static Vector3 ChasePoint( GameObject go )
	{
		var bounds = go.GetBounds();
		return go.WorldPosition.WithZ( bounds.Mins.z + bounds.Size.z * 0.75f );
	}

	private static Vector3 EyesOf( GameObject go )
	{
		if ( go.GetComponent<Player>() is { } player ) return player.EyeTransform.Position;
		var bounds = go.GetBounds();
		return go.WorldPosition.WithZ( bounds.Mins.z + bounds.Size.z * 0.9f );
	}

	// ================= flying

	/// <summary>
	/// HL2's flight: it pushes itself toward where it wants to be - harder if it's already heading that
	/// way (300) than if it has to turn round (200), so it swings wide and overshoots - with a little bob,
	/// a 500 speed limit, and never closer than 52 to the floor. A stalled engine doesn't steer at all.
	/// </summary>
	private void Fly( float dt )
	{
		var pos = WorldPosition;
		var v = _rb.Velocity;

		Vector3? goal = null;
		var reachable = true;
		if ( _target.IsValid() )
		{
			Attacking = HasAttackSlot();
			if ( Attacking || !_burstEnds )
			{
				// In sight with a clear way: straight at them; otherwise round the walls by the navmesh
				goal = _targetVisible && CanFly( pos, _targetPos, _target ) ? _targetPos : FollowPath( _targetPos );
				reachable = goal is not null;
				TryBurst( pos, v );
			}
			else
			{
				var orbit = OrbitPoint( dt );
				goal = CanFly( pos, orbit, _target ) ? orbit : FollowPath( orbit );
				reachable = goal is not null;
			}
		}
		else if ( _wander is { } spot )
		{
			goal = FollowPath( spot );
			if ( goal is null || pos.Distance( spot ) < 40f )
			{
				_wander = null;
				_wanderWait = Game.Random.Float( 2f, 5f );
			}
		}
		else if ( _wanderWait && NavReady && Scene.NavMesh.GetRandomPoint( _home, WanderRadius ) is { } floor )
		{
			_wander = floor + Vector3.Up * Game.Random.Float( GroundHeight, GroundHeight + 40f );
		}

		if ( !_stall )
		{
			// Stalled: no steering, it just coasts
		}
		else if ( !_burstEnds )
		{
			// The attack run: straight on, bending toward the enemy's eyes
			var zDist = 500f;
			if ( _target.IsValid() )
			{
				var eyes = EyesOf( _target );
				zDist = MathF.Abs( eyes.z - pos.z );
				var steer = (eyes - pos).Normal;
				_burstDir = (_burstDir + steer * 40f * dt).Normal;
				_burstDir = _burstDir.WithZ( steer.z ).Normal;
			}
			v = MoveInDirection( v, _burstDir, 400f, MathF.Min( 500f, zDist / dt ), dt );
		}
		else if ( goal is { } g )
		{
			var to = g - pos;
			var dist = to.Length;
			if ( dist > 1f )
			{
				var dir = to / dist;
				var accel = MathF.Min( v.Normal.Dot( dir ) > 0.25f ? 300f : 200f, dist / dt );
				v = MoveInDirection( v, dir, accel, MathF.Min( 300f, dist / dt ), dt );
			}
		}
		else
		{
			v *= MathF.Pow( 0.05f, dt );
		}

		// Never bolted in place: a bob, less of it up close
		var close = _target.IsValid() && pos.WithZ( 0 ).Distance( _target.WorldPosition.WithZ( 0 ) ) < ChargeMinDist;
		v.z += (close ? 2f : 7f) * MathF.Sin( _noiseMod * Time.Now + _noiseMod ) * dt * 10f;

		// Held level with what it's after: the run can only start from their height (as HL2's regroup does)
		if ( _target.IsValid() && _burstEnds && _stall && reachable )
			v.z += Math.Clamp( _targetPos.z - pos.z, -60f, 60f ) * 2f * dt;

		v = KeepOffGround( pos, v );
		v = KeepApart( pos, v );
		_rb.Velocity = v.ClampLength( _sinceHeld < SmashTime ? ThrownMaxSpeed : MaxSpeed );

		Face( _target.IsValid() ? EyesOf( _target ) - pos : v, dt );

		if ( _burstEnds && _hostile ) ShowHostile( false );
		PlayEngineNoise();
	}

	/// <summary>
	/// HL2's CAI_BaseFlyingBot::MoveInDirection: what it had fades to 30% a second while it pushes
	/// toward where it wants to be - so at full push it settles about 250 a second, a burst about 330.
	/// </summary>
	private static Vector3 MoveInDirection( Vector3 v, Vector3 dir, float accelXY, float accelZ, float dt )
	{
		var decay = MathF.Pow( 0.3f, dt );
		var xy = v.WithZ( 0f ) * decay + dir.WithZ( 0f ) * accelXY * dt;
		return xy.WithZ( v.z * decay + dir.z * accelZ * dt );
	}

	/// <summary>The attack run (HL2's StartBurst): close, level with their body and heading at their eyes.</summary>
	private void TryBurst( Vector3 pos, Vector3 v )
	{
		if ( !_nextBurst || !_burstEnds || !_stall ) return;
		if ( pos.WithZ( 0 ).Distance( _target.WorldPosition.WithZ( 0 ) ) >= ChargeMinDist ) return;

		// HL2 only starts the run from within the height of their body. A small flying target (another
		// manhack, a drone) is only a hand tall and bobs about, so it gets at least a 60-unit window
		// round its middle and a looser heading check, or the run would almost never start
		var bounds = _target.GetBounds();
		var small = bounds.Size.z < 60f;
		var halfBand = MathF.Max( bounds.Size.z, 60f ) * 0.5f;
		if ( MathF.Abs( pos.z - bounds.Center.z ) >= halfBand ) return;

		var toEyes = (EyesOf( _target ) - pos).Normal;
		if ( v.Length < 1f || v.Normal.Dot( toEyes ) <= (small ? 0.4f : 0.75f) ) return;

		_burstDir = toEyes;
		_burstEnds = 1f;
		_nextBurst = 2f;
		ShowHostile( true );
		Eye = EyeLook.Charge;
	}

	private void StopBurst()
	{
		if ( _burstEnds ) return;
		_burstEnds = 0f;
		_nextBurst = 2f;
		ShowHostile( false );
	}

	private void ShowHostile( bool hostile )
	{
		if ( _hostile == hostile ) return;
		_hostile = hostile;
		PanelsOpen = hostile;
		BroadcastSound( hostile ? 1 : 2, WorldPosition );
		if ( !hostile && Eye == EyeLook.Charge ) Eye = EyeLook.Normal;
	}

	/// <summary>
	/// HL2's attack slots and swarm: only <see cref="MaxAttackers"/> go for one enemy at a time. With no
	/// more than that after him, they all do, as before. Each holds its place for a few seconds - two or
	/// three runs - and after the run it's on, rests a moment so the ones circling get their turn.
	/// </summary>
	private bool HasAttackSlot()
	{
		foreach ( var gone in Attackers.Keys.Where( k => !k.IsValid() ).ToList() )
			Attackers.Remove( gone );

		var target = _target;
		if ( !Attackers.TryGetValue( target, out var list ) ) Attackers[target] = list = new();
		list.RemoveAll( m => !m.IsValid() || m._dead || m.Crashing || m._held || m._target != target );

		var after = 0;
		foreach ( var other in Scene.GetAllComponents<ManhackNpc>() )
			if ( !other._dead && !other.Crashing && !other._held && other._target == target ) after++;
		if ( after <= MaxAttackers ) return true;

		if ( list.Contains( this ) )
		{
			if ( _slotSince < SlotTime || !_burstEnds ) return true;
			ReleaseSlot( 1f );
			return false;
		}

		if ( !_slotRest || list.Count >= MaxAttackers ) return false;
		list.Add( this );
		_slotSince = 0;
		return true;
	}

	private void ReleaseSlot( float rest = 0f )
	{
		foreach ( var list in Attackers.Values ) list.Remove( this );
		_slotRest = rest;
	}

	/// <summary>Waiting its turn: circling the enemy close in, level with the body, kept out of the walls.</summary>
	private Vector3 OrbitPoint( float dt )
	{
		_orbitAngle += _orbitTurn * dt;
		var center = _targetPos;
		var orbit = center + Rotation.FromYaw( _orbitAngle ).Forward * _orbitRadius + Vector3.Up * MathF.Sin( _orbitAngle * 0.05f ) * 20f;

		var tr = Scene.Trace.Sphere( BodyRadius, center, orbit )
			.IgnoreGameObjectHierarchy( GameObject ).IgnoreGameObjectHierarchy( _target )
			.WithoutTags( "trigger", "manhack", "debris", "player", "npc" ).Run();
		return tr.Hit ? tr.EndPosition : orbit;
	}

	/// <summary>HL2's MaintainGroundHeight - a push per think (ten a second), so scaled to the tick here.</summary>
	private Vector3 KeepOffGround( Vector3 pos, Vector3 v )
	{
		if ( v.z > 32f ) return v;
		var tr = Scene.Trace.Sphere( BodyRadius * 0.5f, pos, pos + Vector3.Down * GroundHeight )
			.IgnoreGameObjectHierarchy( GameObject ).WithoutTags( NotFloor ).Run();
		if ( tr.Hit ) v.z += MathF.Max( 16f, -v.z * 0.5f ) * (1f - tr.Fraction) * MathF.Min( 1f, Time.Delta * 10f );
		return v;
	}

	/// <summary>Spreads out from its own swarm. Enemy manhacks are left alone, so it can close in and cut them.</summary>
	private Vector3 KeepApart( Vector3 pos, Vector3 v )
	{
		foreach ( var other in Scene.GetAllComponents<ManhackNpc>() )
		{
			if ( other == this || GetDisposition( other.GameObject ) == Disposition.Hostile ) continue;
			var away = pos - other.WorldPosition;
			var d = away.Length;
			if ( d > 1f && d < 48f ) v += away / d * (48f - d) * 0.5f;
		}
		return v;
	}

	private void Face( Vector3 look, float dt )
	{
		var flat = look.WithZ( 0f );
		if ( flat.Length > 1f )
		{
			var delta = Angles.NormalizeAngle( Rotation.LookAt( flat.Normal ).Angles().yaw - _yaw );
			_yaw += Math.Clamp( delta * 6f * dt, -400f * dt, 400f * dt );
		}

		// Banks into where it's going, up to 40 degrees, as HL2's m_vTargetBanking; shaken about while stalled
		var v = _rb.Velocity;
		var yaw = Rotation.FromYaw( _yaw );
		var pitch = Math.Clamp( v.Dot( yaw.Forward ) / MaxSpeed * 40f, -40f, 40f );
		var roll = Math.Clamp( v.Dot( yaw.Right ) / MaxSpeed * 40f, -40f, 40f );
		if ( !_stall )
		{
			pitch += Game.Random.Float( -20f, 20f );
			roll += Game.Random.Float( -20f, 20f );
		}
		_rb.SmoothRotate( Rotation.From( pitch, _yaw, roll ), 0.15f, dt );
	}

	private void Hover( float dt )
	{
		var v = _rb.Velocity * (1f - 3f * dt);
		v.z += 7f * MathF.Sin( _noiseMod * Time.Now ) * dt * 10f;
		_rb.Velocity = KeepOffGround( WorldPosition, v );
	}

	// ================= the blades

	/// <summary>HL2's SpinBlades: they wind up again after every knock - but not while the engine is stalled.</summary>
	private void SpinBlades( float dt )
	{
		if ( !_stall ) return;
		BladeSpeed = BladeSpeed < 10f ? BladeSpeed * 2f + 1f : MathF.Min( 100f, BladeSpeed + 80f * dt );
	}

	/// <summary>
	/// Whatever the blades are touching gets ground down, every moment they touch - HL2's Slice. Players
	/// take twice as much; held in a physgun it does 100 a second; flung into someone, a lot at once.
	/// Then it's knocked back and the attack run is over.
	/// </summary>
	private void GrindWhatsTouching( float dt )
	{
		var pos = WorldPosition;
		var ahead = _held && _holder.IsValid() && _holder.GetComponent<Player>() is { } carrier && pos.Distance( carrier.EyeTransform.Position ) < 60f
			? carrier.EyeTransform.Rotation.Forward * 40f
			: _rb.Velocity * dt;
		if ( ahead.Length < 1f ) ahead = WorldRotation.Forward * 2f;

		// Other manhacks count too: a friendly one is never cut (that goes by disposition below),
		// an enemy one is
		var tr = Scene.Trace.Sphere( BodyRadius, pos, pos + ahead )
			.IgnoreGameObjectHierarchy( GameObject ).WithoutTags( "trigger", "debris" ).Run();
		if ( !tr.Hit || !tr.GameObject.IsValid() ) return;

		var victim = tr.GameObject.Root;
		var agent = victim.GetComponent<Player>().IsValid() || victim.GetComponent<Npc>().IsValid();
		var cuttable = agent && (_held ? victim != _holder : GetDisposition( victim ) == Disposition.Hostile);
		if ( Debug ) Log.Info( $"[manhack] {GameObject.Id.ToString()[..4]} touches {tr.GameObject.Name} (root {victim.Name}, tags {tr.GameObject.Tags}) agent {agent} cut {cuttable}" );

		if ( cuttable ) Slice( victim, tr, dt );
		else if ( !agent && !_held ) Bump( tr );
	}

	private void Slice( GameObject victim, SceneTraceResult tr, float dt )
	{
		// HL2 scales by its think interval (a tenth of a second): 2 a touch, 4 on a player, held 10 a touch
		var step = MathF.Max( dt, 0.1f );
		var player = victim.GetComponent<Player>().IsValid();
		float damage;
		if ( _held ) damage = 100f * step;
		else if ( _sinceHeld < SmashTime ) damage = 70f;           // flung into them: a whole guard's health
		else damage = MathF.Max( 1f, BladeDamage * step * (player ? 2f : 1f) );

		// HL2 never had manhacks fight each other; its blade numbers would need a dozen touches to
		// down one, so against another manhack the blades bite five times harder (about three hits)
		if ( victim.GetComponent<ManhackNpc>().IsValid() ) damage *= 5f;

		// While it's being held against someone, one cut a think, not one a frame
		if ( _grind.TryGetValue( victim, out var last ) && Time.Now - last < 0.1f ) return;
		_grind[victim] = Time.Now;

		var attacker = (_held || _sinceHeld < SmashTime) && _holder.IsValid() ? _holder : GameObject;
		if ( victim.GetComponentInParent<Component.IDamageable>() is { } damageable )
			damageable.OnDamage( new DamageInfo( damage, attacker, GameObject ) { Position = tr.HitPosition, Origin = WorldPosition } );
		if ( Debug ) Log.Info( $"[manhack] {GameObject.Id.ToString()[..4]} cuts {victim.Name} for {damage:0.0}" );

		if ( _sinceSliceSound > 0.4f )
		{
			_sinceSliceSound = 0;
			BroadcastSound( Bleeds( tr.GameObject ) ? 3 : 4, tr.HitPosition );
		}

		BladeSpeed = 20f;
		if ( _held ) return;

		// Pop back off them (ComputeSliceBounceVelocity) and give them time to react
		var away = (WorldPosition - victim.GetBounds().Center).WithZ( 0f );
		_rb.Velocity = (away.Length > 1f ? away.Normal : -_rb.Velocity.WithZ( 0 ).Normal) * 200f;
		StopBurst();
	}

	/// <summary>Into something it can't hurt: glance off it, sparks and a grind if the blades caught it.</summary>
	private void Bump( SceneTraceResult tr )
	{
		if ( !_bumpSuppress ) return;
		BladeSpeed = 20f;

		if ( tr.Body is { } body && body.MotionEnabled )
		{
			// Shoves loose things round the way the blades spin (HitPhysicsObject)
			var dir = (tr.HitPosition - WorldPosition).Normal;
			body.ApplyForceAt( tr.HitPosition, Vector3.Cross( dir, Vector3.Up ).Normal * 12000f );
		}

		if ( tr.StartedSolid )
		{
			_rb.Velocity = Vector3.Random * 50f;
			_bumpSuppress = 0.5f;
			return;
		}

		var up = WorldRotation.Up;
		if ( MathF.Abs( up.Dot( tr.Normal ) ) < 0.25f && _sinceGrindSound > 0.25f )
		{
			_sinceGrindSound = 0;
			BroadcastSound( Bleeds( tr.GameObject ) ? 3 : 4, tr.HitPosition );
			_rb.AngularVelocity += up * 10f;
		}

		// Deflect off the surface
		var v = _rb.Velocity;
		var reflect = v - 2f * v.Dot( tr.Normal ) * tr.Normal;
		_rb.Velocity = (v + reflect) * 0.5f;
		StopBurst();
	}

	// ================= being hurt

	protected override void OnHurt( in DamageInfo damage )
	{
		if ( Debug ) Log.Info( $"[manhack] {GameObject.Id.ToString()[..4]} hurt {damage.Damage:0} by {(damage.Attacker.IsValid() ? damage.Attacker.Name : "-")} weapon {(damage.Weapon.IsValid() ? damage.Weapon.Name : "-")} tags {string.Join( ",", damage.Tags )}, {Health:0} left" );
		if ( _dead || Crashing ) return;
		var origin = damage.Origin.IsNearlyZero() ? damage.Position : damage.Origin;
		var dir = (WorldPosition - origin).Normal;

		if ( IsMelee( damage ) )
		{
			// Batted away: 150% damage (HL2 makes crowbar duels two hits), stunned, engine stalled
			Health -= damage.Damage * 0.5f;
			_rb.Velocity = dir * MathF.Min( 600f, damage.Damage * 30f );
			_stall = 0.5f;
			BladeSpeed = 10f;
			Stun( 1f );
			BroadcastSound( 5, WorldPosition );
		}
		else
		{
			// Shot: the engine stalls a moment and it's knocked along the shot
			_stall = 0.25f;
			BladeSpeed = 20f;
			_rb.Velocity += dir * MathF.Min( 400f, damage.Damage * 20f );
		}
		StopBurst();

		if ( Health > 0f && Health <= FullHealth * 0.5f && !_smoking ) StartSmoking();

		var attacker = damage.Attacker;
		if ( !attacker.IsValid() ) return;
		attacker = attacker.GetComponentInParent<Player>()?.GameObject ?? attacker.GetComponentInParent<Npc>()?.GameObject ?? attacker;
		if ( attacker == GameObject || GetFactionOf( attacker ) == FactionName ) return;

		SetDisposition( attacker, Disposition.Hostile, priority: 10 );
		_target = attacker;
		_targetPos = ChasePoint( attacker );
		_sinceSeen = 0;
	}

	private static bool IsMelee( in DamageInfo damage ) =>
		damage.Weapon.IsValid() && damage.Weapon.GetComponentInParent<MeleeWeapon>().IsValid();

	protected override void Die( in DamageInfo damage )
	{
		if ( _dead || Crashing ) return;

		// HL2: always bursts apart when smashed, blown up or clubbed; otherwise half the time it spins out first
		var smashed = damage.Tags.Contains( DamageTags.Explosion ) || damage.Tags.Contains( DamageTags.Crush )
			|| damage.Tags.Contains( DamageTags.Impact ) || _held || _sinceHeld < 1f || IsMelee( damage );
		_death = damage;

		if ( smashed || Game.Random.Int( 0, 1 ) == 0 )
		{
			BreakApart();
			return;
		}

		// Out of control: spins, sparks, speeds up and sinks, and breaks up on the first thing it hits
		Crashing = true;
		_sinceCrash = 0;
		Eye = EyeLook.Stunned;
		_rb.Gravity = true;
		_rb.GravityScale = 0.3f;
		BladeSpeed = 60f;
	}

	private void Crash( float dt )
	{
		var v = _rb.Velocity;
		if ( v.Length < 50f ) v += Vector3.Random.WithZ( 0 ) * 100f;
		v += v * 1.5f * dt + Vector3.Random * 50f * dt;
		_rb.Velocity = v.ClampLength( MaxSpeed * 2f );
		_rb.AngularVelocity = new Vector3( Game.Random.Float( -6f, 6f ), Game.Random.Float( -6f, 6f ), 25f );

		if ( _nextCrashSpark )
		{
			_nextCrashSpark = Game.Random.Float( 0.1f, 0.3f );
			BroadcastSparks( WorldPosition, 0.1f );
		}

		if ( _sinceCrash > 6f ) BreakApart();
	}

	private void BreakApart()
	{
		if ( _dead ) return;
		_dead = true;
		Crashing = false;
		BroadcastDeath( WorldPosition, _rb.IsValid() ? _rb.Velocity : Vector3.Zero, WorldRotation );
		base.Die( _death );
	}

	void ICollisionListener.OnCollisionStart( Collision collision )
	{
		if ( IsProxy || _dead ) return;
		var other = collision.Other.GameObject;

		// Spinning out: the first thing it hits breaks it up
		if ( Crashing )
		{
			if ( _sinceCrash > 0.2f ) BreakApart();
			return;
		}

		if ( TrySlam( collision ) ) return;

		// Something thrown at it
		// (a running player is a rigidbody too - he isn't something thrown)
		if ( other.IsValid() && other.GetComponentInParent<Rigidbody>() is { MotionEnabled: true } body && body.Velocity.Length > 300f
			&& !other.Root.GetComponent<Npc>().IsValid() && !other.Root.GetComponent<Player>().IsValid() )
		{
			var info = new DamageInfo( body.Velocity.Length * MathF.Min( body.Mass, 100f ) * 0.002f, other, other ) { Position = collision.Contact.Point, Origin = other.WorldPosition };
			info.Tags.Add( DamageTags.Impact );
			((Component.IDamageable)this).OnDamage( info );
			StopBurst();
		}
	}

	/// <summary>
	/// Still touching something (a map is one object, so dragged along the floor into a wall there's no new
	/// touch): a slam counts here too.
	/// </summary>
	void ICollisionListener.OnCollisionUpdate( Collision collision )
	{
		if ( IsProxy || _dead || Crashing ) return;
		TrySlam( collision );
	}

	// Which way to feel for a surface while held, and how far: out to the blade tips sideways, the hull above and below
	private static readonly (Vector3 Dir, float Reach)[] GrindProbes =
	{
		(Vector3.Forward, BodyRadius + 3f), (Vector3.Backward, BodyRadius + 3f), (Vector3.Left, BodyRadius + 3f),
		(Vector3.Right, BodyRadius + 3f), (Vector3.Up, 12f), (Vector3.Down, 12f),
	};

	/// <summary>
	/// Pushed against a wall or dragged along the floor in the physgun: the blades grind on it - sparks
	/// and the grind, as when it clips a wall in flight (<see cref="Bump"/>). The physics only reports the
	/// first touch, not a long scrape, so it feels for the surface itself.
	/// </summary>
	private void GrindHeld()
	{
		if ( _sinceGrindSound < 0.2f ) return;
		var pos = WorldPosition;

		foreach ( var (dir, reach) in GrindProbes )
		{
			var tr = Scene.Trace.Ray( pos, pos + dir * reach )
				.IgnoreGameObjectHierarchy( GameObject ).WithoutTags( "trigger", "manhack", "debris" ).Run();
			if ( !tr.Hit ) continue;
			if ( tr.GameObject.IsValid() && (tr.GameObject.Root.GetComponent<Npc>().IsValid() || tr.GameObject.Root.GetComponent<Player>().IsValid()) ) continue;

			_sinceGrindSound = 0;
			BladeSpeed = 20f;
			BroadcastSound( Bleeds( tr.GameObject ) ? 3 : 4, tr.HitPosition );
			return;
		}
	}

	/// <summary>
	/// Slammed into something with the physgun, or thrown at it: it breaks, the harder the more (HL2's
	/// TakeDamageFromPhyscannon: full health at 500, twice that at 1000). The speed is how fast it was going
	/// into the surface the tick before. HL2 spares it while the gravity gun carries it, but the sandbox
	/// physgun is for slamming things about, so here it counts.
	/// </summary>
	private bool TrySlam( Collision collision )
	{
		if ( !(_held || _sinceHeld < 1.5f) || !_holder.IsValid() || _sinceSlam < 0.25f ) return false;
		var other = collision.Other.GameObject;
		if ( other.IsValid() && (other.Root == _holder || other.Root.GetComponent<Npc>().IsValid() || other.Root.GetComponent<Player>().IsValid()) ) return false;

		var normal = collision.Contact.Normal;
		var into = MathF.Abs( _prevVelocity.Dot( normal ) );
		if ( Debug ) Log.Info( $"[manhack] {GameObject.Id.ToString()[..4]} held contact {(other.IsValid() ? other.Name : "-")} into {into:0} (contact speed {collision.Contact.Speed.Length:0})" );
		if ( into < 250f ) return false;

		_sinceSlam = 0;
		var damage = FullHealth * Math.Clamp( into / SmashSpeed, 0f, 2f );
		((Component.IDamageable)this).OnDamage( new DamageInfo( damage, _holder, _holder ) { Position = collision.Contact.Point, Origin = collision.Contact.Point - normal * 10f } );
		StopBurst();
		return true;
	}

	// ================= what everyone sees and hears

	private void Stun( float seconds )
	{
		Eye = EyeLook.Stunned;
		_stunned = seconds;
		BroadcastSound( 6, WorldPosition );
	}

	private void StartSmoking()
	{
		_smoking = true;
		BroadcastSmoke();
	}

	/// <summary>Badly hurt: a thin trail of sparks for as long as it lives (HL2 has a smoke trail here).</summary>
	[Rpc.Broadcast( NetFlags.HostOnly )]
	private void BroadcastSmoke()
	{
		var fx = GameObject.Clone( SparksPrefab, new Transform( WorldPosition ) );
		if ( !fx.IsValid() ) return;
		fx.NetworkMode = NetworkMode.Never;
		fx.SetParent( GameObject, true );
		foreach ( var effect in fx.Components.GetAll<ParticleEffect>( FindMode.EverythingInSelfAndDescendants ) )
			effect.MaxParticles = 8;
	}

	/// <summary>1 blades out, 2 blades in, 3 slice, 4 grind, 5 batted, 6 stunned.</summary>
	[Rpc.Broadcast( NetFlags.HostOnly )]
	private void BroadcastSound( int which, Vector3 where )
	{
		var sound = which switch
		{
			1 => ChargeAnnounceSound, 2 => ChargeEndSound, 3 => SliceSound, 4 => GrindSound,
			5 => BatSound, 6 => StunnedSound, _ => null
		};
		if ( sound is not null )
		{
			var handle = Sound.Play( sound, where );
			if ( handle.IsValid() && which is 1 or 2 or 5 or 6 )
			{
				handle.Parent = GameObject;
				handle.FollowParent = true;
			}
		}
		if ( which == 4 ) Sparks( where, 0.15f );
		if ( which == 3 ) Spray( BloodPrefab, where, 0.1f );
	}

	/// <summary>
	/// Flesh or a machine? People and their bodies bleed; another manhack, a roller, a drone - anything that
	/// flies or rolls instead of walking the navmesh - sparks (HL2's DONT_BLEED), and so do walls.
	/// (Every sandbox NPC has a rigidbody, people too, so that tells nothing.)
	/// </summary>
	private static bool Bleeds( GameObject hit )
	{
		if ( !hit.IsValid() ) return false;
		var root = hit.Root;
		if ( root.GetComponent<Player>().IsValid() ) return true;
		if ( root.GetComponent<Npc>().IsValid() )
			return !root.Tags.Has( "manhack" ) && root.GetComponentInChildren<NavMeshAgent>( true ).IsValid();
		return hit.Tags.Has( "ragdoll" ) || root.Tags.Has( "ragdoll" ) || hit.GetComponentInParent<ModelPhysics>().IsValid();
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	private void BroadcastSparks( Vector3 where, float burst ) => Sparks( where, burst );

	private void Sparks( Vector3 where, float burst ) => Spray( SparksPrefab, where, burst );

	private void Spray( string prefab, Vector3 where, float burst )
	{
		var fx = GameObject.Clone( prefab, new Transform( where, Rotation.LookAt( Vector3.Up ) ) );
		if ( !fx.IsValid() ) return;
		fx.NetworkMode = NetworkMode.Never;
		foreach ( var emitter in fx.Components.GetAll<ParticleEmitter>( FindMode.EverythingInSelfAndDescendants ) )
			emitter.Invoke( burst, () => { if ( emitter.IsValid() ) emitter.Enabled = false; } );
		fx.Components.Get<ParticleEffect>( FindMode.EverythingInSelfAndDescendants )?.Invoke( 2f, fx.Destroy );
	}

	/// <summary>
	/// Bursts into HL2's six pieces with sparks and a flash. Everyone makes their own pieces - they're only
	/// debris, nobody needs them in step - and they're gone after a while.
	/// </summary>
	[Rpc.Broadcast( NetFlags.HostOnly )]
	private void BroadcastDeath( Vector3 where, Vector3 velocity, Rotation rotation )
	{
		StopSounds();
		if ( DieSound is not null ) Sound.Play( DieSound, where );
		for ( var i = 0; i < 3; i++ )
			Sparks( where + Vector3.Random * 12f, 0.3f );

		var flash = new GameObject( true, "manhack_flash" );
		flash.NetworkMode = NetworkMode.Never;
		flash.WorldPosition = where;
		var light = flash.AddComponent<PointLight>();
		light.LightColor = new Color( 0.8f, 0.55f, 0.3f );
		light.Radius = 60f;
		light.Shadows = false;
		light.Invoke( 0.1f, flash.Destroy );

		foreach ( var model in Gibs )
		{
			if ( model is null || model.IsError ) continue;

			var go = new GameObject( true, "manhack_gib" );
			go.Tags.Add( "debris" );
			go.NetworkMode = NetworkMode.Never;
			var fly = (Vector3.Random.WithZ( 0f ).Normal + Vector3.Up * Game.Random.Float( 0.3f, 1f )).Normal;
			go.WorldPosition = where + fly * 6f;
			go.WorldRotation = rotation * Rotation.Random;

			go.AddComponent<ModelRenderer>().Model = model;
			var box = go.AddComponent<BoxCollider>();
			box.Center = model.Bounds.Center;
			var size = model.Bounds.Size;
			box.Scale = new Vector3( MathF.Max( size.x, 2f ), MathF.Max( size.y, 2f ), MathF.Max( size.z, 2f ) );
			var rb = go.AddComponent<Rigidbody>();
			rb.Velocity = velocity * 0.4f + fly * Game.Random.Float( 60f, 160f );
			rb.AngularVelocity = Vector3.Random * 9f;

			rb.Invoke( Game.Random.Float( 20f, 25f ), go.Destroy );
		}
	}

	private void UpdateLook()
	{
		if ( _dead ) return;

		if ( Renderer.IsValid() )
		{
			// HL2: blades, blades and blur, then only the blur as they wind up; the fly animation spins with them
			Renderer.UseAnimGraph = false;
			if ( Renderer.Sequence.Name != "fly" ) Renderer.Sequence.Name = "fly";
			Renderer.PlaybackRate = Math.Clamp( BladeSpeed / 100f, 0.05f, 1f );
			Renderer.SetBodyGroup( "blade", BladeSpeed < 40f ? 1 : 0 );
			Renderer.SetBodyGroup( "blur", BladeSpeed >= 20f ? 1 : 0 );
		}

		UpdatePanels();

		// Two engine notes that rise the closer it gets to its enemy (HL2's PlayFlySound)
		_engine = KeepPlaying( _engine, EngineSound );
		_engine2 = KeepPlaying( _engine2, Engine2Sound );
		var near1 = EnemyDistance > 0f ? Math.Clamp( 1f - EnemyDistance / 512f, 0f, 1f ) : 0f;
		var near2 = EnemyDistance > 0f ? Math.Clamp( 1f - EnemyDistance / 400f, 0f, 1f ) : 0f;
		if ( _engine.IsValid() ) _engine.Pitch = (100f + 60f * near1) / 100f * (Crashing ? 1.4f : 1f);
		if ( _engine2.IsValid() ) _engine2.Pitch = (85f + 105f * near2) / 100f;
	}

	// HL2's a_panel1-4: each moves one or two body panels 70 degrees (about the bone's own Y in the
	// SMD, which our conversion turned into the bone's X the other way round)
	private static readonly (string Bone, float Sign)[] Panels =
	{
		("Manhack_MH_ControlPanel1", 1f), ("Manhack_MH_ControlPanel2", -1f), ("Manhack_MH_ControlPanel3", -1f),
		("Manhack_MH_ControlPanel5", 1f), ("Manhack_MH_ControlPanel4", -1f),
	};

	/// <summary>
	/// HL2's UpdatePanels: flared right open for the attack run, easing shut after it, flapping about
	/// while the engine is stalled.
	/// </summary>
	private void UpdatePanels()
	{
		if ( !Renderer.IsValid() || Renderer.SceneModel is not { } model || Renderer.Model is null ) return;

		if ( Eye == EyeLook.Stunned || Crashing )
			_panels = Game.Random.Float( 0f, 1f );
		else
			_panels = PanelsOpen ? 1f : MathX.Approach( _panels, 0f, Time.Delta * 2.5f );

		foreach ( var (name, sign) in Panels )
		{
			var bone = Renderer.Model.Bones.GetBone( name );
			if ( bone is null ) continue;
			var local = bone.LocalTransform;
			local = local.WithRotation( local.Rotation * Rotation.FromAxis( Vector3.Forward, sign * 70f * _panels ) );
			model.SetBoneOverride( bone.Index, ref local );
		}
	}

	/// <summary>Right up close, now and then a snick of the blades (NPC_Manhack.EngineNoise).</summary>
	private void PlayEngineNoise()
	{
		if ( !_nextEngineNoise || EnemyDistance <= 0f || EnemyDistance > 48f ) return;
		_nextEngineNoise = Game.Random.Float( 3f, 10f );
		BroadcastEngineNoise();
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	private void BroadcastEngineNoise()
	{
		if ( EngineNoiseSound is not null ) Sound.Play( EngineNoiseSound, WorldPosition );
	}

	private SoundHandle KeepPlaying( SoundHandle handle, SoundEvent sound )
	{
		if ( sound is null || (handle.IsValid() && !handle.IsStopped) ) return handle;
		handle = Sound.Play( sound, WorldPosition );
		if ( handle.IsValid() )
		{
			handle.Parent = GameObject;
			handle.FollowParent = true;
		}
		return handle;
	}

	private void StopSounds()
	{
		if ( _engine.IsValid() ) _engine.Stop();
		if ( _engine2.IsValid() ) _engine2.Stop();
	}

	// ================= finding the way

	/// <summary>Next point on the way there; null when there's no way at all (no flying straight at it through the ground).</summary>
	private Vector3? FollowPath( Vector3 goal )
	{
		var pos = WorldPosition;
		if ( CanFly( pos, goal ) )
		{
			_path.Clear();
			return goal;
		}

		if ( _path.Count == 0 || _sincePath > 1f || _pathGoal.Distance( goal ) > 120f )
			BuildPath( goal );
		if ( _path.Count == 0 ) return NavReady ? null : goal;

		while ( _pathIndex < _path.Count - 1 && (pos.Distance( _path[_pathIndex] ) < 40f || CanFly( pos, _path[_pathIndex + 1] )) )
			_pathIndex++;
		return _path[_pathIndex];
	}

	private void BuildPath( Vector3 goal )
	{
		_path.Clear();
		_pathIndex = 0;
		_pathGoal = goal;
		_sincePath = 0;
		if ( !NavReady ) return;

		var nav = Scene.NavMesh;
		if ( nav.GetClosestPoint( FloorBelow( WorldPosition ) ?? WorldPosition, 300f ) is not { } a ) return;
		if ( nav.GetClosestPoint( FloorBelow( goal ) ?? goal, 300f ) is not { } b ) return;

		var result = nav.CalculatePath( new CalculatePathRequest { Start = a, Target = b } );
		if ( result.Points is null || result.Points.Count == 0 ) return;
		if ( result.Status is NavMeshPathStatus.PathNotFound or NavMeshPathStatus.StartNotFound or NavMeshPathStatus.TargetNotFound ) return;

		foreach ( var p in result.Points )
			_path.Add( p.Position + Vector3.Up * GroundHeight );
		_path.Add( goal );
	}

	private bool NavReady => Scene.NavMesh is { IsEnabled: true, IsGenerating: false };

	private Vector3? FloorBelow( Vector3 p )
	{
		var tr = Scene.Trace.Ray( p + Vector3.Up * 8f, p + Vector3.Down * 4000f )
			.IgnoreGameObjectHierarchy( GameObject ).WithoutTags( NotFloor ).Run();
		return tr.Hit ? tr.EndPosition : null;
	}

	private bool CanFly( Vector3 from, Vector3 to, GameObject allow = null )
	{
		var tr = Scene.Trace.Sphere( BodyRadius, from, to ).IgnoreGameObjectHierarchy( GameObject ).WithoutTags( NotObstacles ).Run();
		if ( tr.StartedSolid )
			tr = Scene.Trace.Ray( from, to ).IgnoreGameObjectHierarchy( GameObject ).WithoutTags( NotObstacles ).Run();
		return !tr.Hit || (allow.IsValid() && IsPartOf( tr.GameObject, allow ));
	}

	private readonly Dictionary<GameObject, (float Time, bool Can, float Length)> _reach = new();

	/// <summary>
	/// Is there a way to fly to them - in sight with nothing in between, or along the navmesh?
	/// Without a navmesh it can't tell, and tries as before. Worked out at most once a second each.
	/// </summary>
	private bool Reachable( GameObject go ) => ReachInfo( go ).Can;

	private (float Time, bool Can, float Length) ReachInfo( GameObject go )
	{
		if ( !go.IsValid() ) return (0f, false, 0f);
		if ( _reach.TryGetValue( go, out var known ) && Time.Now - known.Time < 1f ) return known;

		var pos = WorldPosition;
		var aim = ChasePoint( go );
		(float, bool, float) result;
		if ( CanFly( pos, aim, go ) ) result = (Time.Now, true, pos.Distance( aim ));
		else if ( !NavReady ) result = (Time.Now, true, pos.Distance( aim ));
		else
		{
			var nav = Scene.NavMesh;
			var a = nav.GetClosestPoint( FloorBelow( pos ) ?? pos, 300f );
			var floor = FloorBelow( aim ) ?? aim;
			var b = nav.GetClosestPoint( floor, 300f );
			var length = 0f;
			var ok = false;

			// The navmesh has to be where they stand - not the nearest bit of it, on the ground above their tunnel
			if ( b is { } near && near.Distance( floor ) > 64f ) b = null;
			if ( a is { } from && b is { } to )
			{
				var path = nav.CalculatePath( new CalculatePathRequest { Start = from, Target = to } );
				ok = path.Status == NavMeshPathStatus.Complete && path.Points is { Count: > 0 };
				if ( ok )
				{
					for ( var i = 1; i < path.Points.Count; i++ ) length += path.Points[i].Position.Distance( path.Points[i - 1].Position );
					// The navmesh ends on the floor: the last bit up or down to them has to be clear
					ok = path.Points[^1].Position.Distance( to ) < 64f;
				}
			}
			result = (Time.Now, ok, length);
		}

		if ( _reach.Count > 32 )
		{
			foreach ( var gone in _reach.Keys.Where( k => !k.IsValid() ).ToList() ) _reach.Remove( gone );
		}
		_reach[go] = result;
		return result;
	}

	/// <summary>
	/// Can it hear them? The sound has to get here: straight through open air, or round the corners
	/// a walking route takes - not through a floor or the ground.
	/// </summary>
	private bool CanHear( GameObject go )
	{
		var pos = WorldPosition;
		if ( CanSee( pos, EyesOf( go ), go ) ) return true;
		var reach = ReachInfo( go );
		return reach.Can && reach.Length <= HearingRange * 1.5f;
	}

	private bool CanSee( Vector3 from, Vector3 to, GameObject target )
	{
		var tr = Scene.Trace.Ray( from, to ).IgnoreGameObjectHierarchy( GameObject ).WithoutTags( NotObstacles ).Run();
		return !tr.Hit || IsPartOf( tr.GameObject, target );
	}

	private static bool IsPartOf( GameObject hit, GameObject target ) =>
		hit.IsValid() && target.IsValid() && (hit == target || hit.Root == target.Root);

	// ================= helpers

	/// <summary>The player whose physgun has hold of it, if anyone's.</summary>
	private GameObject HolderOf()
	{
		foreach ( var gun in Scene.GetAllComponents<Physgun>() )
		{
			var state = gun._state;
			if ( (state.Active || state.Pulling) && state.GameObject.IsValid() && state.GameObject.Root == GameObject ) return gun.GameObject.Root;
		}
		return null;
	}

	private static bool IsPlayer( GameObject go ) => go.IsValid() && go.GetComponent<Player>().IsValid();

	private static bool IsAlive( GameObject go )
	{
		if ( go.GetComponent<Player>() is { } player ) return player.Health > 0f;
		if ( go.GetComponent<Npc>() is { } npc ) return npc.Health >= 1f;
		return true;
	}
}
