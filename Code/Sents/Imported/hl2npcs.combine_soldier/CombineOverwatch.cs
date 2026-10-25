using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;
using Sandbox.Npcs;

namespace Hl2Combine;

/// <summary>
/// Overwatch: the network every Combine squad on the map is on. Forms squads from soldiers
/// standing near each other, runs each squad's brain, and sends free squads to anyone calling
/// for reinforcements - however far across the map they are. Host only.
/// </summary>
public sealed class CombineOverwatch : GameObjectSystem<CombineOverwatch>
{
	public List<CombineSquad> Squads { get; } = new();

	/// <summary>How far a gunshot carries in the open; through walls it's about half.</summary>
	public const float GunshotRange = 5000f;

	private readonly Dictionary<BaseCombatWeapon, int> _rounds = new();
	private TimeSince _sinceTick;
	private TimeSince _sinceForming = 10f;
	private bool _preloaded;

	public CombineOverwatch( Scene scene ) : base( scene )
	{
		Listen( Stage.StartUpdate, 20, Tick, "Combine.Overwatch" );
	}

	private static readonly Dictionary<Scene, CombineOverwatch> Started = new();

	/// <summary>
	/// This scene's Overwatch. The engine only creates systems when a scene is made, so when the
	/// soldier is spawned from the menu into a map that was already running, his package's code
	/// came too late and there is none - the first soldier starts it then.
	/// </summary>
	public static CombineOverwatch For( Scene scene )
	{
		if ( !scene.IsValid() ) return null;
		if ( scene.GetSystem<CombineOverwatch>() is { } system ) return system;

		foreach ( var gone in Started.Keys.Where( s => !s.IsValid() ).ToList() )
			Started.Remove( gone );

		if ( !Started.TryGetValue( scene, out var started ) )
		{
			started = new CombineOverwatch( scene );
			Started[scene] = started;
		}

		return started;
	}

	private void Tick()
	{
		if ( Scene.IsEditor || !Networking.IsHost ) return;

		// Only one Overwatch runs a scene
		if ( For( Scene ) != this ) return;
		if ( _sinceTick < 0.25f ) return;
		_sinceTick = 0;

		if ( !_preloaded )
		{
			_preloaded = true;
			_ = CombineVoice.Preload();
		}

		Squads.RemoveAll( s =>
		{
			s.Members.RemoveAll( m => !m.IsValid() || !m.IsAlive );
			return s.Members.Count == 0;
		} );

		if ( _sinceForming > 1f )
		{
			_sinceForming = 0;
			FormSquads();
		}

		ListenForGunfire();
		ShareContacts();

		foreach ( var squad in Squads.ToList() )
			squad.Tick( Scene );
	}

	private readonly List<SoundHandle> _sounds = new();
	private readonly HashSet<SoundHandle> _heardSounds = new();

	/// <summary>
	/// The sandbox's guns make no noise as far as NPCs are concerned, so listen for it here:
	/// a shot's sound, or a magazine that got lighter since last time.
	/// </summary>
	private void ListenForGunfire()
	{
		ListenForShotSounds();

		foreach ( var gone in _rounds.Keys.Where( w => !w.IsValid() ).ToList() )
			_rounds.Remove( gone );

		foreach ( var weapon in Scene.GetAllComponents<BaseCombatWeapon>() )
		{
			var rounds = weapon.Clip1 + weapon.Ammo1;
			var fired = _rounds.TryGetValue( weapon, out var before ) && rounds < before;
			_rounds[weapon] = rounds;
			if ( !fired ) continue;

			Gunfire( weapon );
		}
	}

	/// <summary>
	/// Every sandbox gun plays its attack sound on the host for every shot - the host's, a joined
	/// player's, an NPC's - even with infinite ammo, when the magazine never gets lighter.
	/// </summary>
	private void ListenForShotSounds()
	{
		_sounds.Clear();
		SoundHandle.GetActive( _sounds );
		_heardSounds.IntersectWith( _sounds );

		foreach ( var sound in _sounds )
		{
			if ( !_heardSounds.Add( sound ) || !sound.Parent.IsValid() ) continue;

			var weapon = sound.Parent.GetComponentInParent<BaseCombatWeapon>( true );
			if ( !weapon.IsValid() || weapon.AttackSound?.Sounds is not { } shots || !shots.Contains( sound.SoundFile ) ) continue;

			Gunfire( weapon );
		}
	}

	private void Gunfire( BaseCombatWeapon weapon )
	{
		var shooter = weapon.GetComponentInParent<Player>()?.GameObject
			?? weapon.GetComponentInParent<Npc>()?.GameObject;
		if ( !shooter.IsValid() ) return;
		if ( CombineSquad.Debug ) Log.Info( $"[overwatch] gunfire from {shooter.Name}" );
		HeardNoise( shooter, shooter.WorldPosition, GunshotRange );
	}

	/// <summary>How far other squads hear a squad's firefight and join in.</summary>
	public const float ComradeRange = 3000f;

	private readonly Dictionary<CombineSquad, TimeSince> _sinceSquadHeard = new();

	/// <summary>
	/// A squad in a firefight is heard by the squads around it: they learn who's being shot at
	/// and come for the same enemy, rather than standing about while the next squad fights.
	/// </summary>
	public void ComradeFiring( CombineNpc shooter, GameObject enemy )
	{
		if ( !enemy.IsValid() || shooter.Squad is not { } own ) return;
		if ( _sinceSquadHeard.TryGetValue( own, out var since ) && since < 1f ) return;
		_sinceSquadHeard[own] = 0;

		foreach ( var gone in _sinceSquadHeard.Keys.Where( s => !Squads.Contains( s ) ).ToList() )
			_sinceSquadHeard.Remove( gone );

		var from = shooter.WorldPosition;
		foreach ( var soldier in Scene.GetAllComponents<CombineNpc>() )
		{
			if ( soldier.IsProxy || !soldier.IsAlive || soldier.Squad is null || soldier.Squad == own ) continue;

			var distance = soldier.WorldPosition.Distance( from );
			if ( distance > ComradeRange ) continue;

			// Another brain hears the shooting like any other noise, and does with it what it does
			if ( soldier.Squad.UsesOtherBrain )
			{
				if ( distance < GunshotRange ) soldier.Squad.Heard( soldier, enemy, from );
				continue;
			}

			var blocked = Scene.Trace.Ray( soldier.EyePosition, from + Vector3.Up * 50f )
				.WithoutTags( "player", "npc", "playercontroller", "trigger", "ragdoll" ).Run().Hit;
			if ( blocked && distance > ComradeRange * 0.5f ) continue;

			if ( soldier.GetDisposition( enemy ) != Disposition.Hostile ) continue;
			soldier.Squad.Heard( soldier, enemy, enemy.WorldPosition );
		}
	}

	/// <summary>Everyone in earshot who'd shoot the source goes after it.</summary>
	public void HeardNoise( GameObject source, Vector3 position, float range )
	{
		if ( source.GetComponent<CombineNpc>().IsValid() ) return;

		foreach ( var soldier in Scene.GetAllComponents<CombineNpc>() )
		{
			if ( soldier.IsProxy || !soldier.IsAlive || soldier.Squad is null ) continue;

			var distance = soldier.WorldPosition.Distance( position );
			if ( distance > range ) continue;

			// Walls muffle: a shot behind cover is heard from about half as far
			var blocked = Scene.Trace.Ray( soldier.EyePosition, position + Vector3.Up * 50f )
				.WithoutTags( "player", "npc", "playercontroller", "trigger", "ragdoll" ).Run().Hit;
			if ( blocked && distance > range * 0.5f ) continue;

			if ( soldier.GetDisposition( source ) != Disposition.Hostile ) continue;

			// The ear isn't exact
			var guess = position + Vector3.Random.WithZ( 0 ) * MathF.Min( 150f, distance * 0.08f );
			soldier.Squad.Heard( soldier, source, guess );
		}
	}

	private void FormSquads()
	{
		var soldiers = Scene.GetAllComponents<CombineNpc>().Where( c => !c.IsProxy && c.IsAlive ).ToList();

		foreach ( var soldier in soldiers )
		{
			if ( soldier.Squad is { } current && !current.Accepts( soldier ) )
				current.Leave( soldier );

			// A lone soldier on patrol joins up with anyone nearby
			var lonely = soldier.Squad is null
				|| soldier.Squad.Members.Count == 1 && soldier.Squad.Posture == SquadPosture.Patrol;
			if ( !lonely ) continue;

			var best = soldiers
				.Where( o => o != soldier && o.Squad is not null && o.Squad != soldier.Squad )
				.Where( o => o.Squad.Members.Count < o.MaxSquadSize && o.Squad.Accepts( soldier ) )
				.Where( o => o.Squad.Members.Count > 1 || o.Squad.Posture == SquadPosture.Patrol )
				.Where( o => o.WorldPosition.Distance( soldier.WorldPosition ) < soldier.SquadJoinDistance )
				.OrderBy( o => o.WorldPosition.Distance( soldier.WorldPosition ) )
				.FirstOrDefault();

			if ( best is not null )
			{
				soldier.Squad?.Leave( soldier );
				best.Squad.Join( soldier );
				continue;
			}

			if ( soldier.Squad is null )
			{
				var squad = new CombineSquad();
				Squads.Add( squad );
				squad.Join( soldier );
			}
		}

		MergeSquads();
		Squads.RemoveAll( s => s.Members.Count == 0 );
	}

	/// <summary>Squads that end up side by side become one, up to the size limit.</summary>
	private void MergeSquads()
	{
		foreach ( var a in Squads.ToList() )
		{
			foreach ( var b in Squads.ToList() )
			{
				if ( a == b || a.Leader is not { } la || b.Leader is not { } lb || !Friendly( a, b ) ) continue;
				// Only squads on the same brain (soldiers with soldiers, Metrocops with Metrocops)
				if ( Busy( a ) || Busy( b ) || a.UsesOtherBrain != b.UsesOtherBrain ) continue;
				if ( la.WorldPosition.Distance( lb.WorldPosition ) > la.SquadJoinDistance ) continue;

				var size = a.Alive.Count() + b.Alive.Count();
				if ( size > Math.Min( la.MaxSquadSize, lb.MaxSquadSize ) ) continue;

				// The bigger one (or the one already fighting) keeps its callsign and orders
				var keep = a.Alive.Count() > b.Alive.Count() || a.Alive.Count() == b.Alive.Count() && a.Posture != SquadPosture.Patrol ? a : b;
				var gone = keep == a ? b : a;
				keep.Absorb( gone );
			}
		}
	}

	private static bool Busy( CombineSquad squad ) =>
		squad.Posture is SquadPosture.Breach or SquadPosture.Reinforce or SquadPosture.Retreat || squad.Members.Count == 0;

	/// <summary>How far Overwatch passes a contact on to the other squads.</summary>
	public const float ShareRange = 3000f;

	private TimeSince _sinceShare;

	/// <summary>
	/// One radio net: once a squad has called a contact in, every friendly squad near enough
	/// knows about it too, and keeps getting its position while someone has eyes on it.
	/// </summary>
	private void ShareContacts()
	{
		if ( _sinceShare < 0.5f ) return;
		_sinceShare = 0;

		foreach ( var source in Squads )
		{
			if ( source.Leader is not { } from || source.UsesOtherBrain ) continue;

			foreach ( var record in source.Enemies.Values.Where( e => e.Reported && e.SinceSeen < 2f && e.Target.IsValid() ).ToList() )
			{
				foreach ( var other in Squads )
				{
					if ( other == source || other.Leader is not { } to || !Friendly( source, other ) || other.UsesOtherBrain ) continue;
					if ( to.WorldPosition.Distance( from.WorldPosition ) > ShareRange ) continue;
					if ( to.GetDisposition( record.Target ) != Disposition.Hostile ) continue;
					other.Relayed( record.Target, record.Position, record.SinceSeen );
				}
			}
		}

		ShareAcrossNet();
	}

	/// <summary>
	/// The other Combine packages (elite, prison guard, manhacks) are on the same net through
	/// <see cref="CombineNet"/>: what our men have eyes on goes out, what they see comes in as if
	/// another squad had called it.
	/// </summary>
	private void ShareAcrossNet()
	{
		var sent = new HashSet<GameObject>();
		foreach ( var squad in Squads )
		{
			if ( squad.Leader is null ) continue;
			foreach ( var record in squad.Enemies.Values )
			{
				if ( record.Reported && record.SinceSighted < 1f && record.Target.IsValid() && sent.Add( record.Target ) )
					CombineNet.ReportContact( Scene, record.Target );
			}
		}

		foreach ( var enemy in CombineNet.Contacts( Scene, includeOwn: false ) )
		{
			foreach ( var squad in Squads )
			{
				if ( squad.Leader is not { } leader || squad.UsesOtherBrain ) continue;
				if ( leader.WorldPosition.Distance( enemy.WorldPosition ) > CombineNet.Range ) continue;
				if ( leader.GetDisposition( enemy ) != Disposition.Hostile ) continue;
				squad.Relayed( enemy, enemy.WorldPosition, 0.5f );
			}
		}
	}

	private static bool Friendly( CombineSquad a, CombineSquad b )
	{
		var la = a.Leader;
		var lb = b.Leader;
		return la is not null && lb is not null && CombineSquad.Compatible( la, lb );
	}

	private IEnumerable<CombineSquad> Candidates( CombineSquad requester )
	{
		var origin = requester.Leader?.WorldPosition ?? Vector3.Zero;
		return Squads
			.Where( s => s != requester && s.Available && !s.UsesOtherBrain && Friendly( s, requester ) )
			.OrderBy( s => s.Leader.WorldPosition.Distance( origin ) );
	}

	public bool CanHelp( CombineSquad requester ) => Candidates( requester ).Any();

	/// <summary>The request was heard: send the nearest free squad, two if things look grim.</summary>
	public void Dispatch( CombineSquad requester )
	{
		var count = requester.Strength < 0.5f ? 2 : 1;
		foreach ( var squad in Candidates( requester ).Take( count ).ToList() )
			squad.Respond( requester );
	}
}

/// <summary>Reading the level: rooms, roofs and doorways.</summary>
public static class CombineGeometry
{
	private static SceneTrace WorldOnly( Scene scene, Vector3 from, Vector3 to )
	{
		return scene.Trace.Ray( from, to ).WithoutTags( "player", "npc", "playercontroller", "trigger", "ragdoll", "prop" );
	}

	public static bool HasRoof( Scene scene, Vector3 position )
	{
		return WorldOnly( scene, position + Vector3.Up * 40f, position + Vector3.Up * 500f ).Run().Hit;
	}

	/// <summary>Under a roof with walls on most sides.</summary>
	public static bool IsIndoors( Scene scene, Vector3 position )
	{
		if ( !HasRoof( scene, position ) ) return false;

		var walls = 0;
		var eye = position + Vector3.Up * 40f;
		for ( var i = 0; i < 8; i++ )
		{
			var direction = Rotation.FromYaw( i * 45f ).Forward;
			if ( WorldOnly( scene, eye, eye + direction * 600f ).Run().Hit ) walls++;
		}

		return walls >= 5;
	}

	/// <summary>
	/// Walk the path to the target and find where it first goes under a roof - the doorway -
	/// plus a spot outside it to stack up at.
	/// </summary>
	public static bool FindEntrance( Scene scene, List<Vector3> path, out Vector3 entrance, out Vector3 inward, out Vector3 stack )
	{
		entrance = inward = stack = default;

		var samples = new List<Vector3>();
		for ( var i = 0; i < path.Count - 1; i++ )
		{
			var a = path[i];
			var b = path[i + 1];
			var steps = Math.Max( 1, (int)(a.Distance( b ) / 24f) );
			for ( var s = 0; s < steps; s++ )
				samples.Add( Vector3.Lerp( a, b, s / (float)steps ) );
		}
		samples.Add( path[^1] );

		var door = samples.FindIndex( p => HasRoof( scene, p ) );
		if ( door < 0 ) return false;

		entrance = samples[door];
		var ahead = samples[Math.Min( samples.Count - 1, door + 3 )];
		inward = (ahead - entrance).WithZ( 0 );
		inward = inward.LengthSquared > 1f ? inward.Normal : (path[^1] - entrance).WithZ( 0 ).Normal;

		// Back along the path, outside, a little way from the door
		var back = door;
		var travelled = 0f;
		while ( back > 0 && travelled < 150f )
		{
			travelled += samples[back].Distance( samples[back - 1] );
			back--;
		}

		if ( travelled < 60f ) return false;
		stack = samples[back];
		return true;
	}
}
