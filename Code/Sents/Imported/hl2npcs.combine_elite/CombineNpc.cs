using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;
using Sandbox.Npcs;
using Sandbox.Npcs.Schedules;

namespace Hl2CombineElite;

/// <summary>
/// Half-Life 2 Combine soldier. Fights in squads: two members shoot while the rest take cover
/// or flank, they throw grenades at enemies hiding behind cover, run from grenades, and talk
/// over the radio.
/// </summary>
public sealed partial class CombineNpc : Npc, IKillSource
{
	public const string FactionName = "combine";

	[Property, Group( "Balance" )] public float StartHealth { get; set; } = 100f;

	/// <summary>Which gun this soldier carries. Random picks one at spawn.</summary>
	[Property, Group( "Weapon" )] public CombineWeaponType Weapon { get; set; } = CombineWeaponType.Random;

	/// <summary>The gun actually in hand (never Random once spawned).</summary>
	[Sync] public CombineWeaponType ActiveWeapon { get; set; } = CombineWeaponType.Smg1;

	private CombineWeaponStats _gun;
	private CombineWeaponType _gunType;

	public CombineWeaponStats Gun
	{
		get
		{
			if ( _gun is null || _gunType != ActiveWeapon )
			{
				_gun = CombineWeaponStats.Get( ActiveWeapon );
				OtherWeaponStats( ActiveWeapon, ref _gun );
				_gunType = ActiveWeapon;
			}
			return _gun;
		}
	}
	public int ClipSize => Gun.ClipSize;
	public float EngageRange => Gun.Range;
	[Property, Group( "Balance" )] public float RunSpeed { get; set; } = 210f;
	[Property, Group( "Balance" )] public float WalkSpeed { get; set; } = 75f;
	[Property, Group( "Balance" )] public float PatrolRadius { get; set; } = 700f;

	[Property, Group( "Grenades" )] public int GrenadeCount { get; set; } = 3;
	[Property, Group( "Grenades" )] public float GrenadeDamage { get; set; } = 110f;
	[Property, Group( "Grenades" )] public float GrenadeRadius { get; set; } = 250f;
	[Property, Group( "Grenades" )] public float GrenadeFuse { get; set; } = 3f;

	[Property, Group( "Squad" )] public int MaxSquadSize { get; set; } = 6;
	[Property, Group( "Squad" )] public float SquadJoinDistance { get; set; } = 800f;

	/// <summary>Which of HL2's dubs his radio speaks: en, de or ru. The dub's words have to ship with the prefab.</summary>
	[Property, Group( "Voice" )] public string RadioLanguage { get; set; } = "en";

	// Everything the code loads by name (ball, grenade, dropped guns, effects, radio words).
	// Publishing only takes what the prefab references, so they're listed here.
	[Property, Group( "Published With" )] public List<Model> PublishedModels { get; set; } = new();
	[Property, Group( "Published With" )] public List<Material> PublishedMaterials { get; set; } = new();
	[Property, Group( "Published With" )] public List<SoundEvent> PublishedSounds { get; set; } = new();
	[Property, Group( "Published With" )] public List<SoundFile> PublishedRadio { get; set; } = new();

	[Sync] public bool Crouched { get; set; }
	[Sync] public bool Alert { get; set; }

	public CombineSquad Squad { get; set; }
	public int Clip { get; set; }
	public int GrenadesLeft { get; set; }

	/// <summary>Where the patrol leader is walking to.</summary>
	public Vector3? PatrolGoal { get; set; }
	public TimeSince SincePatrolGoal { get; set; }

	/// <summary>Seconds since a move of his last gave up, stuck.</summary>
	public TimeSince SinceMoveStuck { get; set; } = 100f;

	/// <summary>Seconds since a move of his failed for any reason (stuck, or no way there at all).</summary>
	public TimeSince SinceMoveFailed { get; set; } = 100f;

	/// <summary>Is there a whole path there? Real maps are full of navmesh he can't get to - roofs, the ground outside.</summary>
	public bool CanWalkTo( Vector3 point )
	{
		var path = Scene.NavMesh.CalculatePath( new Sandbox.Navigation.CalculatePathRequest { Start = WorldPosition, Target = point } );
		return path.Status == Sandbox.Navigation.NavMeshPathStatus.Complete;
	}

	/// <summary>Somewhere to walk to next on patrol, that he can actually reach.</summary>
	public Vector3? FindPatrolGoal()
	{
		for ( var i = 0; i < 6; i++ )
		{
			if ( Scene.NavMesh.GetRandomPoint( WorldPosition, PatrolRadius ) is not { } point ) continue;
			if ( point.WithZ( 0 ).Distance( WorldPosition.WithZ( 0 ) ) < PatrolRadius * 0.3f ) continue;
			if ( CanWalkTo( point ) ) return point;
		}

		return null;
	}
	public TimeSince SinceSpawn { get; private set; }
	public bool IsAlive => !_dead && Health >= 1f;

	/// <summary>The enemy this soldier knows about (his own sighting or one reported on the radio).</summary>
	public GameObject Enemy => Squad?.KnownEnemy( this )?.Target;
	public Vector3 EnemyPosition => Squad?.KnownEnemy( this )?.Position ?? WorldPosition;

	private TimeSince _sinceHurt = 100f;
	private TimeSince _sincePain = 100f;
	private TimeSince _sinceFlinch = 100f;
	private TimeSince _sinceCover = 100f;
	private int _seenOrders;
	private bool _reconsider;
	private TimeSince _sinceReact = 10f;
	private bool _retreated;

	/// <summary>Testing: give every new soldier this gun (1 SMG1, 2 AR2, 3 shotgun, 0 = normal).</summary>
	[ConVar( "combine_elite_force_weapon" )]
	public static int ForceWeapon { get; set; }

	/// <summary>Closer than this, a visible enemy is shot at once, whatever the squad is doing.</summary>
	public const float CloseRange = 450f;
	private TimeSince _sinceListen;
	private TimeSince _sinceSenseLog;

	/// <summary>An order for this soldier alone: re-plan on the next think.</summary>
	public void Reconsider() => _reconsider = true;

	/// <summary>Hands busy with a magazine right now (set by the reload task). Runs out by itself too,
	/// so it can never stay stuck on.</summary>
	public bool IsReloading => _reloadLeft > 0f;
	private TimeUntil _reloadLeft;
	public void SetReloading( float duration ) => _reloadLeft = duration;
	private bool _dead;

	public override string Faction => FactionName;

	protected override void SetupRelationships()
	{
		Likes( FactionName );
		Hates( Factions.Player, Factions.Ally, Factions.Citizen, Factions.Monster );
	}

	protected override void OnStart()
	{
		base.OnStart();
		GameObject.Tags.Add( "combine" );

		if ( IsProxy ) return;

		ActiveWeapon = ForceWeapon > 0 ? (CombineWeaponType)ForceWeapon
			: Weapon == CombineWeaponType.Random ? CombineWeaponStats.Roll() : Weapon;
		Health = StartHealth;
		Clip = ClipSize;
		GrenadesLeft = GrenadeCount;
		Balls = ActiveWeapon == CombineWeaponType.Ar2 ? StartBalls : 0;
		Navigation.WishSpeed = WalkSpeed;
		Navigation.RunSpeed = RunSpeed;
		Navigation.TurnSpeed = 2.5f;
		SinceSpawn = 0;

		// Squads, the radio and hearing all run in Overwatch: make sure this scene has one
		CombineOverwatch.For( Scene );
		OtherBrainStart();
	}

	protected override void OnDestroy()
	{
		// Removed (cleanup, undo, deleted): whatever he was halfway through saying stops with him
		CombineVoice.Stop( GameObject );
		if ( !_dead ) OtherBrainRemoved();
		Squad?.Leave( this );
		base.OnDestroy();
	}

	protected override void OnUpdate()
	{
		if ( _dead ) return;

		if ( !IsProxy )
			Think();

		base.OnUpdate();
		UpdateAnimation();
	}

	private readonly Dictionary<GameObject, (Vector3 Position, float Time)> _footsteps = new();

	/// <summary>
	/// How fast someone is moving. On the host a joined player's own velocity reads zero,
	/// so it's also measured from how far they got since the last listen.
	/// </summary>
	/// <summary>
	/// Footsteps need a floor: someone flying (noclip, a jump) or moving inside the level's
	/// geometry makes no sound to hear.
	/// </summary>
	private bool OnFloor( GameObject target )
	{
		if ( target.Tags.Has( "noclip" ) ) return false;
		var feet = target.WorldPosition;
		var tr = Scene.Trace.Ray( feet + Vector3.Up * 4f, feet - Vector3.Up * 20f )
			.IgnoreGameObjectHierarchy( target )
			.WithoutTags( "player", "npc", "playercontroller", "trigger", "ragdoll" )
			.Run();
		return tr.Hit && !tr.StartedSolid;
	}

	private float MeasuredSpeed( GameObject target )
	{
		var now = Time.Now;
		var position = target.WorldPosition.WithZ( 0 );
		var speed = Sandbox.Npcs.Layers.SensesLayer.GetVelocity( target ).WithZ( 0 ).Length;

		if ( _footsteps.TryGetValue( target, out var last ) && now - last.Time is > 0.05f and < 2f )
			speed = MathF.Max( speed, position.Distance( last.Position ) / (now - last.Time) );

		_footsteps[target] = (position, now);
		if ( _footsteps.Count > 32 )
		{
			foreach ( var gone in _footsteps.Keys.Where( k => !k.IsValid() ).ToList() )
				_footsteps.Remove( gone );
		}

		return speed;
	}

	/// <summary>
	/// Ordered to suppress, has rounds left, the gun reaches that far, the last try wasn't into a wall -
	/// and someone saw the enemy there a moment ago, nothing has been heard of him elsewhere since,
	/// and the spot is behind cover from here (out in the open he'd be seen if he were still there).
	/// </summary>
	private bool CanSuppress( EnemyRecord enemy ) =>
		Squad is { } squad && squad.Suppressor == this && squad.SuppressActive && !RecentlyBlocked && Clip > 0
		&& enemy.SinceSighted < CombineSquad.SuppressMemory && !enemy.HeardElsewhere
		&& enemy.SeenPosition.Distance( WorldPosition ) < EngageRange && SpotHidden( enemy.SeenPosition );

	private bool _lastCrouched;

	/// <summary>Seconds since he last knelt down or stood up.</summary>
	public TimeSince SincePostureChange { get; private set; } = 100f;

	private void Think()
	{
		var squad = Squad;
		RechargeBalls();
		OtherBrainThink();

		if ( Crouched != _lastCrouched )
		{
			_lastCrouched = Crouched;
			SincePostureChange = 0;
		}

		if ( CombineSquad.Debug && _sinceSenseLog > 0.7f )
		{
			_sinceSenseLog = 0;
			Log.Info( $"[eyes] {GameObject.Id.ToString()[..4]} sees {Senses.VisibleTargets.Count}, nearest hostile {(Senses.Nearest.IsValid() ? $"{Senses.DistanceToNearest:0}" : "-")}, doing {ActiveSchedule?.GetDebugString() ?? "-"}, posture {squad?.Posture}" );
		}

		// Report what we see; new contacts go out over the radio
		foreach ( var target in Senses.VisibleTargets )
		{
			if ( target.IsValid() && !IsDeadTarget( target ) )
				squad?.Sighted( this, target, target.WorldPosition );
		}

		if ( squad is not null && _sinceListen > 0.5f )
		{
			_sinceListen = 0;

			if ( CombineSquad.Debug && Senses.Disturbance is { } heard )
				Log.Info( $"[ears] {GameObject.Id.ToString()[..4]} hears {heard.Kind} from {(heard.Source.IsValid() ? heard.Source.Root.Name : "?")} ({GetDisposition( heard.Source?.Root )})" );

			if ( Senses.Disturbance is { } noise && noise.Source.IsValid() && noise.Kind is StimulusKind.Gunshot or StimulusKind.Explosion )
			{
				var source = noise.Source.Root;
				if ( !source.GetComponent<CombineNpc>().IsValid() && GetDisposition( source ) == Disposition.Hostile && !Senses.VisibleTargets.Contains( source ) )
					squad.Heard( this, source, noise.Position );
			}

			foreach ( var target in Senses.AudibleTargets )
			{
				if ( !target.IsValid() ) continue;
				var speed = MeasuredSpeed( target );
				if ( Senses.VisibleTargets.Contains( target ) ) continue;
				var close = target.WorldPosition.Distance( WorldPosition ) < 450f;
				if ( close && speed > 170f && OnFloor( target ) ) squad.Heard( this, target, target.WorldPosition );
			}
		}

		if ( Crouched && Animation.IsValid() && Animation.MoveVelocity.WithZ( 0 ).Length > 110f )
			Crouched = false;

		if ( squad?.Posture != SquadPosture.Retreat ) _retreated = false;

		var known = squad?.KnownEnemy( this );
		// Tense up at once; relax only when standing still, not halfway through a step
		// Anything but a quiet patrol keeps the gun up: fighting, holding, breaching, searching, on the way to help
		var tense = known is not null && known.SinceSeen < 40f || _sinceHurt < 10f
			|| squad is not null && squad.Posture != SquadPosture.Patrol
			|| Senses.VisibleTargets.Count > 0 || Senses.Disturbance is not null;
		if ( tense || !Animation.IsValid() || Animation.MoveVelocity.WithZ( 0 ).Length < 12f )
			Alert = tense;

		// A new order was heard: drop what we're doing (but never stop running from a grenade)
		if ( _reconsider || squad is not null && squad.OrderVersion != _seenOrders )
		{
			_reconsider = false;
			_seenOrders = squad?.OrderVersion ?? 0;
			// Nor halfway through a reload: in HL2 only a grenade breaks one off. Dropping it for a hit
			// would just start the whole reload again from the top
			if ( !UsesOtherBrain && !IsReloading && ActiveSchedule is not CombineDodgeSchedule and not CombineThrowingSchedule )
				EndCurrentSchedule();
		}
	}

	// ---------------------------------------------------------------- another brain

	// A second AI can plug in from its own files (Code/Hl2Brain: Half-Life 2's rules, for comparing).
	// Without those files these calls compile away and every soldier runs ours.

	/// <summary>Is a plugged-in brain running this soldier instead of ours?</summary>
	public bool UsesOtherBrain
	{
		get
		{
			var other = false;
			OtherBrainActive( ref other );
			return other;
		}
	}

	partial void OtherBrainActive( ref bool active );
	partial void OtherBrainSchedule( ref ScheduleBase schedule );
	partial void OtherBrainHurt( in DamageInfo damageInfo );
	partial void OtherBrainBallFired();
	partial void OtherBrainGrenadeThrown();

	// What another NPC built on this code (the Metrocop) changes: its guns, its animation names,
	// its voice, what it does as it dies
	partial void OtherWeaponStats( CombineWeaponType type, ref CombineWeaponStats stats );
	partial void OtherWeaponSound( bool reload, ref SoundEvent sound );
	partial void OtherAnimName( ref string name );
	partial void OtherStrideSpeed( string name, ref float speed );
	partial void OtherFlinch( in DamageInfo damage, ref string name );
	partial void OtherBrainDied( in DamageInfo damage );
	partial void OtherRagdoll( SkinnedModelRenderer body );

	/// <summary>Another brain with a voice of its own takes this line (meaning: one of CombineVoice's lists).</summary>
	public bool OtherBrainTalks( string[] lines )
	{
		var handled = false;
		OtherBrainSay( lines, ref handled );
		return handled;
	}

	partial void OtherBrainSay( string[] lines, ref bool handled );
	partial void OtherBrainStart();
	partial void OtherBrainRemoved();
	partial void OtherBrainThink();
	partial void OtherAnimationUpdate();

	public static bool IsDeadTarget( GameObject go )
	{
		if ( !go.IsValid() ) return true;
		if ( go.GetComponent<Npc>() is { } npc ) return npc.Health < 1f;
		if ( go.GetComponent<Player>() is { } player ) return player.Health < 1f;
		return false;
	}

	public bool CanSee( GameObject target ) => target.IsValid() && Senses.VisibleTargets.Contains( target );

	/// <summary>Has a live enemy in plain view right now.</summary>
	public bool SeesEnemy => Senses.VisibleTargets.Any( t => t.IsValid() && !IsDeadTarget( t ) );

	public override ScheduleBase GetSchedule()
	{
		if ( UsesOtherBrain )
		{
			ScheduleBase other = null;
			OtherBrainSchedule( ref other );
			if ( other is not null ) return other;
		}

		var grenade = FindDangerousGrenade();
		if ( grenade.IsValid() )
		{
			var dodge = GetSchedule<CombineDodgeSchedule>();
			dodge.Danger = grenade;
			return dodge;
		}

		var squad = Squad;
		var visible = Senses.GetBestTarget();

		// The sandbox keeps the dead in view a moment longer
		if ( visible.IsValid() && IsDeadTarget( visible ) )
			visible = Senses.VisibleTargets.FirstOrDefault( t => t.IsValid() && !IsDeadTarget( t ) );

		if ( visible.IsValid() && Clip > 0 && RecentlyBlocked && FindFiringPosition( visible ) is { } firingSpot )
		{
			var step = GetSchedule<CombineRepositionSchedule>();
			step.Target = visible;
			step.Spot = firingSpot;
			return step;
		}

		if ( visible.IsValid() && CanMelee( visible ) )
		{
			var melee = GetSchedule<CombineMeleeSchedule>();
			melee.Target = visible;
			return melee;
		}

		if ( visible.IsValid() && Clip > 0 && WorldPosition.Distance( visible.WorldPosition ) < CloseRange )
		{
			var engage = GetSchedule<CombineEngageSchedule>();
			engage.Target = visible;
			engage.Light = false;
			return engage;
		}

		if ( squad?.Posture == SquadPosture.Retreat && !_retreated )
		{
			_retreated = true;
			return GetSchedule<CombineRetreatSchedule>();
		}

		if ( visible.IsValid() )
			return GetCombatSchedule( visible );

		// Shot or heard something close behind: spin round to it first, cover can wait
		if ( squad?.KnownEnemy( this ) is { } threat && threat.SinceSeen < 3f && _sinceReact > 3f
			&& (_sinceHurt < 2f || threat.Position.Distance( WorldPosition ) < 1500f) && !IsFacing( threat.Position, 40f ) )
		{
			_sinceReact = 0;
			var react = GetSchedule<CombineReactSchedule>();
			react.Threat = threat.Position;
			return react;
		}

		if ( squad is null )
			return GetSchedule<CombinePatrolSchedule>();

		var known = squad.KnownEnemy( this );

		switch ( squad.Posture )
		{
			case SquadPosture.Breach when squad.Breach is not null && squad.Breach.Team.Contains( this ):
				return GetSchedule<CombineBreachSchedule>();

			case SquadPosture.Breach when known is not null:
				// Not on the entry team: cover the ones going in
				return Hold( known.Position );

			case SquadPosture.Reinforce:
				return GetSchedule<CombineReinforceSchedule>();

			case SquadPosture.Hold when known is not null:
				if ( CanSuppress( known ) ) return Suppress( known );
				if ( WantsReload( Gun.PumpAction ? 0.5f : 0.25f ) ) return GetSchedule<CombineReloadSchedule>();
				return Hold( known.Position );

			case SquadPosture.Assault when known is not null:
				if ( CanSuppress( known ) ) return Suppress( known );
				if ( WantsReload( Gun.PumpAction ? 0.5f : 0.25f ) ) return GetSchedule<CombineReloadSchedule>();
				if ( squad.IsFlankOrdered( this ) ) return Flank( known.Target, known.Position );
				if ( known.SinceSeen > 1.5f && known.SinceSeen < 8f && WantsBall( known.Position, known.Target, false ) )
					return BallSchedule( null, known.Position );
				// He's only just ducked behind something: that's the moment to flush him out (as HL2 does)
				if ( known.SinceSeen > 1.5f && CanThrowGrenadeAt( known.Position ) && Game.Random.Float() < (known.SinceSighted < 3f ? 0.6f : 0.2f) )
					return GrenadeSchedule( known.Position );
				return Advance( known.Position );

			case SquadPosture.Search:
				return GetSchedule<CombineSearchSchedule>();

			case SquadPosture.Patrol when known is not null:
				return Hold( known.Position );
		}

		if ( WantsReload( 0.6f ) )
			return GetSchedule<CombineReloadSchedule>();

		if ( Senses.Disturbance is { } disturbance )
		{
			squad.OnHeardDisturbance( this, disturbance.Position );
			var investigate = GetSchedule<InvestigateSchedule>();
			investigate.Target = disturbance.Position;
			return investigate;
		}

		return GetSchedule<CombinePatrolSchedule>();
	}

	private ScheduleBase GetCombatSchedule( GameObject target )
	{
		var squad = Squad;

		if ( Clip <= 0 )
		{
			// One time in five, a grenade first to keep their heads down while he reloads (HL2)
			if ( Game.Random.Float() < 0.2f && WorldPosition.Distance( target.WorldPosition ) > 300f && CanThrowGrenadeAt( target.WorldPosition ) )
				return GrenadeSchedule( target.WorldPosition );

			if ( FindCover( target.WorldPosition, 300f ) is not null )
				return CoverSchedule( target.WorldPosition, reload: true );
			return GetSchedule<CombineReloadSchedule>();
		}

		// Hurt badly: get out of the line of fire for a moment
		if ( Health < StartHealth * 0.5f && _sinceHurt < 3f && _sinceCover > 6f )
			return CoverSchedule( target.WorldPosition, reload: WantsReload( 0.5f ) );

		// Holding: fight only from cover
		if ( squad?.Posture == SquadPosture.Hold && _sinceCover > 6f && !IsCovered( WorldPosition, target.WorldPosition + Vector3.Up * 60f )
			&& FindCover( target.WorldPosition, 400f ) is not null )
			return CoverSchedule( target.WorldPosition, reload: false );

		// The flanker keeps moving even under fire, that's the point
		if ( squad is not null && squad.IsFlankOrdered( this ) && WorldPosition.Distance( target.WorldPosition ) > 300f )
			return Flank( target, target.WorldPosition );

		var distance = WorldPosition.Distance( target.WorldPosition );
		if ( _sinceBallThought > 2f )
		{
			_sinceBallThought = 0;
			if ( WantsBall( target.WorldPosition, target, true ) )
				return BallSchedule( target, target.WorldPosition );
		}

		if ( distance > 300f && CanThrowGrenadeAt( target.WorldPosition ) && Game.Random.Float() < 0.15f )
			return GrenadeSchedule( target.WorldPosition );

		if ( distance > EngageRange )
			return Advance( target.WorldPosition );

		if ( squad is null || squad.TryClaimAttack( this ) || squad.Suppressor == this )
		{
			var engage = GetSchedule<CombineEngageSchedule>();
			engage.Target = target;
			engage.Light = false;
			return engage;
		}

		// Far off and no slot to fire from: close the distance rather than trade long-range shots
		var far = distance > 2200f && squad.Posture != SquadPosture.Hold;
		var covered = IsCovered( WorldPosition, target.WorldPosition + Vector3.Up * 60f );

		// Not ahead in this fight: don't walk into their guns - get into cover and shoot from there
		var outgunned = squad.Strength < 1.2f;
		if ( !far && (squad.Posture == SquadPosture.Hold || covered || outgunned || Game.Random.Float() < 0.5f) )
		{
			if ( outgunned && !covered && _sinceCover > 4f && FindCover( target.WorldPosition, 500f ) is not null )
				return CoverSchedule( target.WorldPosition, reload: WantsReload( 0.5f ) );

			var engage = GetSchedule<CombineEngageSchedule>();
			engage.Target = target;
			engage.Light = true;
			return engage;
		}

		// No attack slot: work closer from cover to cover (or sit tight while holding)
		return squad.Posture == SquadPosture.Hold
			? Hold( target.WorldPosition )
			: Advance( target.WorldPosition );
	}

	private ScheduleBase CoverSchedule( Vector3 threat, bool reload )
	{
		_sinceCover = 0;
		var cover = GetSchedule<CombineCoverSchedule>();
		cover.Threat = threat;
		cover.Reload = reload;
		return cover;
	}

	private ScheduleBase GrenadeSchedule( Vector3 target )
	{
		var throwing = GetSchedule<CombineThrowingSchedule>();
		throwing.Target = target;
		throwing.Flat = false;
		return throwing;
	}

	private TimeSince _sinceBallThought = 10f;

	private ScheduleBase BallSchedule( GameObject target, Vector3 position )
	{
		var ball = GetSchedule<CombineBallSchedule>();
		ball.Target = target;
		ball.Position = position;
		return ball;
	}

	private ScheduleBase Hold( Vector3 threat )
	{
		var hold = GetSchedule<CombineHoldSchedule>();
		hold.Threat = threat;
		return hold;
	}

	private ScheduleBase Advance( Vector3 threat )
	{
		var advance = GetSchedule<CombineAdvanceSchedule>();
		advance.Threat = threat;
		return advance;
	}

	private ScheduleBase Suppress( EnemyRecord enemy )
	{
		var suppress = GetSchedule<CombineSuppressSchedule>();
		suppress.Enemy = enemy;
		return suppress;
	}

	private ScheduleBase Flank( GameObject target, Vector3 position )
	{
		var flank = GetSchedule<CombineFlankSchedule>();
		flank.Target = target;
		flank.Position = position;
		return flank;
	}

	protected override void OnHurt( in DamageInfo damage )
	{
		if ( _dead ) return;

		_sinceHurt = 0;
		OtherBrainHurt( damage );
		var attacker = damage.Attacker;
		if ( CombineSquad.Debug ) Log.Info( $"[hurt] {GameObject.Id.ToString()[..4]} hit for {damage.Damage:0} by {(attacker.IsValid() ? attacker.Name : "nobody")}" );

		if ( attacker.IsValid() )
		{
			attacker = attacker.GetComponentInParent<Player>()?.GameObject
				?? attacker.GetComponentInParent<Npc>()?.GameObject
				?? attacker;

			// Squadmates hitting each other is an accident, not a betrayal
			var friendlyFire = attacker.GetComponent<Npc>() is { } other && CombineSquad.Allied( this, other );
			var isAgent = attacker.GetComponent<Npc>().IsValid() || attacker.GetComponent<Player>().IsValid();

			if ( friendlyFire && attacker != GameObject )
			{
				var refund = damage.Damage * (damage.Tags.Contains( DamageTags.Headshot ) ? 2f : 1f);
				Health = MathF.Min( StartHealth, Health + refund );
				return;
			}

			if ( attacker != GameObject && !friendlyFire && isAgent )
			{
				SetDisposition( attacker, Disposition.Hostile, priority: 10 );
				Squad?.Attacked( this, attacker );

				// Another brain's schedules decide for themselves whether a hit breaks them off
				if ( !UsesOtherBrain ) Reconsider();
			}
		}

		if ( Health < 1f ) return;

		// One grunt per volley, not one per bullet
		if ( _sincePain > 1.2f )
		{
			_sincePain = 0;
			PlayPainSound();
		}

		var still = !Animation.IsValid() || Animation.MoveVelocity.WithZ( 0 ).Length < 40f;
		if ( still && _sinceFlinch > 2f && (damage.Damage >= 15f || Game.Random.Float() < 0.35f) )
		{
			_sinceFlinch = 0;
			Flinch( damage );
		}
	}

	private void Flinch( in DamageInfo damage )
	{
		if ( HasAction && !CurrentActionIsFlinch && !CurrentActionIsShooting ) return;

		string name;
		if ( damage.Damage >= 40f || damage.Tags.Contains( DamageTags.Explosion ) )
			name = "flinch_big";
		else if ( damage.Tags.Contains( DamageTags.Headshot ) )
			name = "flinch_head";
		else
		{
			var local = WorldTransform.PointToLocal( damage.Position );
			if ( local.z < 38f ) name = "flinch_gut";
			else if ( local.y > 8f ) name = "flinch_left";
			else if ( local.y < -8f ) name = "flinch_right";
			else name = "flinch_chest";
		}

		OtherFlinch( damage, ref name );
		if ( name is null ) return;
		PlayAction( name );
	}

	protected override void Die( in DamageInfo damage )
	{
		if ( _dead ) return;
		_dead = true;

		// Cut off whatever he was saying on the radio
		StopRadioRpc();
		OtherBrainDied( damage );

		EmitStimulus( StimulusKind.Death, radius: 1024f, lifetime: 2f );
		NotifyKill( damage );
		Squad?.OnMemberDied( this, damage.Attacker );
		Squad?.Leave( this );
		PlayDeathSound();

		var velocity = GetDeathLaunchVelocity( damage );
		var origin = damage.Origin;
		EndCurrentSchedule();
		Navigation.Stop();

		// Grenades kill inside the physics step, where a new ragdoll would be created without bodies
		Invoke( 0.01f, () =>
		{
			DropWeapon();
			CreateCombineRagdoll( velocity, origin );
			GameObject.Destroy();
		} );
	}

	private void NotifyKill( in DamageInfo damage )
	{
		var source = damage.Attacker.IsValid() ? damage.Attacker.GetComponent<IKillSource>() : null;
		source?.OnKill( GameObject );

		var icon = damage.Weapon.IsValid() ? damage.Weapon.GetComponentInChildren<IKillIcon>() : null;
		var attackerName = source?.DisplayName;
		var attackerSteamId = source?.SteamId ?? 0L;
		var attackerTags = source?.Tags ?? "";
		var damageTags = damage.Tags.ToString();
		var victimName = DisplayName;
		var weaponIcon = icon?.DisplayIcon;

		Scene.RunEvent<Feed>( x => x.NotifyKill( victimName, attackerName, attackerSteamId, damageTags, attackerTags, "npc", weaponIcon ) );
	}

	void IKillSource.OnKill( GameObject victim )
	{
		if ( _dead ) return;
		Squad?.OnEnemyKilled( this, victim );
	}
}
