using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;
using Sandbox.Npcs;

namespace Hl2Metrocop;

public enum SquadPosture
{
	/// <summary>No known enemies: walk around, chat.</summary>
	Patrol,
	/// <summary>Fighting: attack slots, flanking, suppression, advancing cover to cover.</summary>
	Assault,
	/// <summary>Outmatched: stay in cover and only shoot from it - usually while help is on the way.</summary>
	Hold,
	/// <summary>Enemy holed up indoors: stack at the entrance, grenade in, rush.</summary>
	Breach,
	/// <summary>Lost the enemy: spread out over the last known area.</summary>
	Search,
	/// <summary>Answering another squad's call for help.</summary>
	Reinforce,
	/// <summary>Beaten: fall back.</summary>
	Retreat,
}

public sealed class EnemyRecord
{
	public GameObject Target { get; init; }
	public CombineNpc Spotter { get; set; }
	public Vector3 Position { get; set; }
	public TimeSince SinceSeen { get; set; }

	/// <summary>False until the spotter's contact call has been heard - before that only the spotter knows.</summary>
	public bool Reported { get; set; }

	public TimeSince SinceFirstSeen { get; init; }

	/// <summary>Since someone actually had eyes on him (a heard noise doesn't count).</summary>
	public TimeSince SinceSighted { get; set; } = 100f;

	/// <summary>
	/// Where he was actually last seen, or got to in the moment after ducking out of sight (HL2's free
	/// knowledge). Position can be newer but only a guess from hearing.
	/// </summary>
	public Vector3 SeenPosition { get; set; }

	/// <summary>Has he been heard well away from where he was last seen since?</summary>
	public bool HeardElsewhere => Position.Distance( SeenPosition ) > 300f;
}

public sealed class BreachPlan
{
	public enum Stage { Stacking, Throwing, Blast, Rushing }

	public Stage Step { get; set; }
	public TimeSince SinceStep { get; set; }
	public Vector3 Target { get; init; }
	public Vector3 Entrance { get; init; }
	public Vector3 Stack { get; init; }
	public Vector3 Inward { get; init; }
	public Vector3 ThrowFrom { get; init; }
	public Vector3 ThrowAt { get; init; }
	public CombineNpc Grenadier { get; set; }
	public List<CombineNpc> Team { get; } = new();
	public bool Ordered { get; set; }

	public Vector3 StackSpot( CombineNpc npc )
	{
		var index = Math.Max( 0, Team.IndexOf( npc ) );
		var side = new Vector3( -Inward.y, Inward.x, 0f );
		var row = index / 2;
		var sign = index % 2 == 0 ? 1f : -1f;
		return Stack + side * sign * 45f - Inward * row * 55f;
	}
}

/// <summary>
/// A squad's shared brain (host only). Knowledge of enemies spreads by radio: the squad only
/// acts on a contact once the spotter's call has been heard, and every order - flank, hold,
/// breach, go - takes effect when the line finishes, not before.
/// </summary>
public sealed partial class CombineSquad
{
	public const int MaxAttackers = 3;

	/// <summary>How long a just-lost enemy's position is still tracked (HL2 uses 1.75 s).</summary>
	public const float FreeKnowledge = 1.75f;

	/// <summary>How long a sighting stays worth suppressing: after that he could be anywhere.</summary>
	public const float SuppressMemory = 6f;

	/// <summary>Print every radio line and posture change to the console.</summary>
	[ConVar( "metrocop_debug" )]
	public static bool Debug { get; set; }

	private void DebugLog( string text )
	{
		if ( Debug ) Log.Info( $"[{Callsign}] {text}" );
	}
	public const float SearchTime = 25f;

	public List<CombineNpc> Members { get; } = new();
	public string Callsign { get; } = Game.Random.FromArray( CombineVoice.Callsigns );
	public CombineNpc Leader => Members.FirstOrDefault( m => m.IsValid() && m.IsAlive );
	public IEnumerable<CombineNpc> Alive => Members.Where( m => m.IsValid() && m.IsAlive );

	public SquadPosture Posture { get; private set; } = SquadPosture.Patrol;

	/// <summary>Led by a soldier on a plugged-in brain: our orders, radio rules and Overwatch extras stay out of it.</summary>
	public bool UsesOtherBrain => Leader is { } leader && leader.UsesOtherBrain;

	partial void OtherBrainSighted( EnemyRecord record, bool isNew, ref bool handled );
	partial void OtherBrainHeard( CombineNpc listener, Vector3 position, ref bool handled );
	partial void OtherBrainTick( ref bool handled );
	public TimeSince SincePosture { get; private set; }

	/// <summary>Bumped on every new order; members drop what they're doing to follow it.</summary>
	public int OrderVersion { get; private set; }

	public Dictionary<GameObject, EnemyRecord> Enemies { get; } = new();

	/// <summary>Where the last enemy we knew of was - the place to sweep once the shooting stops.</summary>
	public Vector3? LastContact { get; private set; }

	/// <summary>The middle of the patch being searched; it moves on, the way they went, when a sweep finds nothing.</summary>
	private Vector3? _searchCenter;
	private Vector3 _searchHeading;
	private int _searchPushes;
	private readonly HashSet<CombineNpc> _searched = new();
	private const int MaxSearchPushes = 3;
	public BreachPlan Breach { get; private set; }

	public CombineNpc Flanker { get; private set; }
	public CombineNpc Suppressor { get; private set; }
	public bool SuppressActive => Suppressor.IsValid() && !_suppressUntil;

	/// <summary>Another squad on its way to help us.</summary>
	public CombineSquad HelpInbound { get; private set; }
	/// <summary>The squad we're on our way to help.</summary>
	public CombineSquad Helping { get; private set; }
	public Vector3 HelpPosition { get; private set; }

	public int Losses { get; private set; }

	private readonly Dictionary<CombineNpc, TimeSince> _attackers = new();
	private readonly Dictionary<CombineNpc, Vector3> _cover = new();
	private readonly List<(float Time, Action Action)> _pending = new();
	private float _channelFree;
	private TimeUntil _nextGrenade;
	private TimeUntil _nextChatter = 15f;
	private TimeUntil _suppressUntil;
	private TimeSince _sinceFlank = 100f;
	private TimeSince _sinceSuppress = 100f;
	private TimeSince _sinceRequest = 1000f;
	private TimeSince _sinceBreach = 1000f;
	private bool _announcedClosing;
	private bool _changingPosture;

	public bool GrenadeReady => _nextGrenade;
	public bool CanSpeak => Time.Now >= _channelFree;

	// ---------------------------------------------------------------- radio

	/// <summary>How long after a shouted contact call starts that the squad has taken it in.</summary>
	private const float CalloutReaction = 0.4f;

	/// <summary>
	/// Say a line over the squad channel. The channel carries one voice at a time, so the line
	/// waits its turn; <paramref name="onHeard"/> runs once the whole line has been spoken -
	/// or, for a <paramref name="callout"/> (a contact being shouted out), as soon as the others
	/// have caught the first word.
	/// Droppable lines (flavour) are skipped when the channel is busy.
	/// </summary>
	public void Transmit( CombineNpc speaker, string[] options, Action onHeard = null, bool urgent = false, bool droppable = false, string[] tail = null, bool callout = false )
	{
		if ( !speaker.IsValid() || !speaker.IsAlive )
		{
			onHeard?.Invoke();
			return;
		}

		// Another brain speaks for itself (the Metrocop: HL2's police lines, not the soldiers' radio)
		if ( speaker.OtherBrainTalks( options ) )
		{
			onHeard?.Invoke();
			return;
		}

		var now = Time.Now;
		var wait = urgent ? 0f : MathF.Max( 0f, _channelFree - now );
		if ( droppable && wait > 0.3f ) return;

		var words = new List<string> { Game.Random.FromArray( options ) };
		if ( tail is not null ) words.Add( Game.Random.FromArray( tail ) );

		var sentence = CombineVoice.Build( words, Callsign, NumberOf( speaker ) );
		var duration = CombineVoice.EstimateDuration( sentence );
		_channelFree = now + wait + duration + 0.25f;

		DebugLog( $"#{NumberOf( speaker )} says \"{sentence}\"{(wait > 0f ? $" (after {wait:0.0}s)" : "")}" );
		if ( wait <= 0f ) speaker.Speak( sentence );
		else speaker.Invoke( wait, () => speaker.Speak( sentence ) );

		if ( onHeard is not null )
			_pending.Add( (now + wait + (callout ? MathF.Min( duration, CalloutReaction ) : duration), onHeard) );
	}

	private void DeliverMessages()
	{
		var now = Time.Now;
		for ( var i = _pending.Count - 1; i >= 0; i-- )
		{
			if ( _pending[i].Time > now ) continue;
			var action = _pending[i].Action;
			_pending.RemoveAt( i );
			action();
		}
	}

	// ---------------------------------------------------------------- membership

	public void Join( CombineNpc npc )
	{
		if ( !Members.Contains( npc ) ) Members.Add( npc );
		npc.Squad = this;
		npc.Reconsider();
	}

	public void Leave( CombineNpc npc )
	{
		Members.Remove( npc );
		_attackers.Remove( npc );
		_cover.Remove( npc );
		Breach?.Team.Remove( npc );
		if ( Flanker == npc ) Flanker = null;
		if ( Suppressor == npc ) Suppressor = null;
		if ( npc.Squad == this ) npc.Squad = null;
	}

	public int NumberOf( CombineNpc npc ) => Math.Max( 0, Members.IndexOf( npc ) );

	public static bool Compatible( CombineNpc a, CombineNpc b )
	{
		return a.GetDisposition( b.GameObject ) != Disposition.Hostile
			&& b.GetDisposition( a.GameObject ) != Disposition.Hostile;
	}

	/// <summary>
	/// Same side, by faction rather than by type: the Combine Elite comes in a package of its own,
	/// with a CombineNpc type of its own, and his stray rounds are still friendly fire, not a betrayal.
	/// </summary>
	public static bool Allied( Npc a, Npc b ) =>
		a.IsValid() && b.IsValid() && a.Faction == b.Faction
		&& a.GetDisposition( b.GameObject ) != Disposition.Hostile
		&& b.GetDisposition( a.GameObject ) != Disposition.Hostile;

	public bool Accepts( CombineNpc npc ) => Alive.All( m => m == npc || Compatible( npc, m ) && m.UsesOtherBrain == npc.UsesOtherBrain );

	/// <summary>Not fighting and not already running an errand - free to answer a call for help.</summary>
	public bool Available => Posture is SquadPosture.Patrol or SquadPosture.Search && Helping is null && Leader is not null
		&& !Enemies.Values.Any( e => e.Reported && e.SinceSeen < 10f );

	// ---------------------------------------------------------------- knowledge

	/// <summary>A member sees an enemy. New contacts must be called in before the others know.</summary>
	public void Sighted( CombineNpc member, GameObject target, Vector3 position )
	{
		if ( Enemies.TryGetValue( target, out var record ) )
		{
			record.Position = position;
			record.SeenPosition = position;
			record.SinceSeen = 0;
			record.SinceSighted = 0;
			var known = false;
			OtherBrainSighted( record, false, ref known );
			return;
		}

		record = new EnemyRecord { Target = target, Spotter = member, Position = position, SeenPosition = position, SinceSeen = 0, SinceFirstSeen = 0, SinceSighted = 0 };
		Enemies[target] = record;

		var handled = false;
		OtherBrainSighted( record, true, ref handled );
		if ( handled ) return;

		// Already in a fight: one more hostile is just shouted out. Otherwise it's a contact report.
		var lines = Enemies.Values.Any( e => e != record && e.Reported ) ? CombineVoice.Engaging : CombineVoice.Spotted;
		Transmit( member, lines, () =>
		{
			record.Reported = true;
			if ( Posture is SquadPosture.Patrol or SquadPosture.Search or SquadPosture.Reinforce )
				EnterCombat();
		}, urgent: true, callout: true );
	}

	/// <summary>
	/// Heard an enemy (gunfire, running feet) without seeing them: the position is only rough,
	/// but it's still called in and the squad goes to deal with it.
	/// </summary>
	public void Heard( CombineNpc listener, GameObject source, Vector3 position )
	{
		var handled = false;
		OtherBrainHeard( listener, position, ref handled );
		if ( handled ) return;

		if ( Enemies.TryGetValue( source, out var known ) )
		{
			if ( known.SinceSeen > 1.5f )
			{
				known.Position = position;
				known.SinceSeen = 1.5f;
			}
			return;
		}

		var record = new EnemyRecord { Target = source, Spotter = listener, Position = position, SeenPosition = position, SinceSeen = 1.5f, SinceFirstSeen = 0 };
		Enemies[source] = record;
		listener.Reconsider();
		DebugLog( $"#{NumberOf( listener )} heard {source.Name}" );

		Transmit( listener, CombineVoice.HeardContact, () =>
		{
			record.Reported = true;
			if ( Posture is SquadPosture.Patrol or SquadPosture.Search or SquadPosture.Reinforce )
				EnterCombat();
		}, urgent: true, callout: true );
	}

	/// <summary>
	/// Overwatch passing on a contact another squad called in: known at once, as if heard on our
	/// own channel. The leader acknowledges a new one and the squad goes after it.
	/// </summary>
	public void Relayed( GameObject target, Vector3 position, float sinceSeen )
	{
		if ( Enemies.TryGetValue( target, out var known ) )
		{
			known.Reported = true;
			if ( known.SinceSeen > sinceSeen )
			{
				known.Position = position;
				known.SinceSeen = sinceSeen;
			}
			return;
		}

		Enemies[target] = new EnemyRecord { Target = target, Position = position, SeenPosition = position, SinceSeen = sinceSeen, SinceFirstSeen = 0, Reported = true };
		DebugLog( $"overwatch reports {target.Name}" );
		if ( Leader is { } leader ) Transmit( leader, CombineVoice.Answer, droppable: true );
	}

	/// <summary>Take in a squad that ended up next to us: its men and everything it knows.</summary>
	public void Absorb( CombineSquad other )
	{
		DebugLog( $"{other.Callsign} joins us" );
		foreach ( var record in other.Enemies.Values.Where( e => e.Reported ) )
			Relayed( record.Target, record.Position, record.SinceSeen );

		foreach ( var member in other.Alive.ToList() )
		{
			other.Leave( member );
			Join( member );
		}
	}

	/// <summary>Got hit by someone: that's as good as a contact report.</summary>
	public void Attacked( CombineNpc member, GameObject attacker )
	{
		if ( !Enemies.ContainsKey( attacker ) )
			Sighted( member, attacker, attacker.WorldPosition );
		else
			Enemies[attacker].SinceSeen = MathF.Min( Enemies[attacker].SinceSeen, 1f );
	}

	/// <summary>What this member knows: his own sightings plus everything reported on the radio.</summary>
	public EnemyRecord KnownEnemy( CombineNpc member )
	{
		EnemyRecord best = null;
		foreach ( var record in Enemies.Values )
		{
			if ( !record.Reported && record.Spotter != member ) continue;
			if ( best is null || record.SinceSeen < best.SinceSeen ) best = record;
		}

		return best;
	}

	private EnemyRecord PrimaryEnemy => Enemies.Values.Where( e => e.Reported ).OrderBy( e => (float)e.SinceSeen ).FirstOrDefault();

	public bool InContact => Enemies.Values.Any( e => e.Reported && e.SinceSeen < 3f );

	private static float ThreatOf( GameObject target )
	{
		if ( target.GetComponent<Player>().IsValid() ) return 1.5f;
		return 1f;
	}

	/// <summary>Our health against how dangerous the known enemies are. Below 1: we're losing.</summary>
	public float Strength
	{
		get
		{
			var ours = Alive.Sum( m => MathF.Max( 0f, m.Health ) / m.StartHealth );
			var theirs = Enemies.Values.Where( e => e.Reported && e.SinceSeen < 15f ).Sum( e => ThreatOf( e.Target ) );
			return theirs <= 0f ? 10f : ours / theirs;
		}
	}

	// ---------------------------------------------------------------- orders

	private void SetPosture( SquadPosture posture )
	{
		_changingPosture = false;
		if ( Posture == posture ) return;

		DebugLog( $"posture {Posture} -> {posture}" );
		Posture = posture;
		SincePosture = 0;
		OrderVersion++;

		if ( posture == SquadPosture.Search ) StartSearch();
		else _searchCenter = null;

		if ( posture != SquadPosture.Assault ) Flanker = null;
		if ( posture != SquadPosture.Breach ) Breach = null;
		if ( posture is SquadPosture.Patrol or SquadPosture.Retreat ) { Helping = null; _announcedClosing = false; }
	}

	/// <summary>Change posture once the leader's order has been heard.</summary>
	private void Order( string[] lines, SquadPosture posture, bool urgent = false, string[] tail = null )
	{
		if ( _changingPosture ) return;
		_changingPosture = true;
		var leader = Leader;
		if ( leader is null || Alive.Count() == 1 )
		{
			SetPosture( posture );
			return;
		}

		Transmit( leader, lines, () => SetPosture( posture ), urgent, tail: tail );
	}

	private void EnterCombat()
	{
		if ( Strength < 1f && TryRequestHelp() ) return;
		SetPosture( SquadPosture.Assault );
	}

	private bool TryRequestHelp()
	{
		if ( HelpInbound is not null && HelpInbound.Helping == this ) return true;
		if ( _sinceRequest < 45f ) return false;

		var overwatch = CombineOverwatch.For( Leader?.Scene );
		if ( overwatch is null || !overwatch.CanHelp( this ) ) return false;

		_sinceRequest = 0;
		Transmit( Leader, CombineVoice.RequestHelp, () => overwatch.Dispatch( this ) );

		// Dig in while waiting
		Order( CombineVoice.Hold, SquadPosture.Hold );
		return true;
	}

	/// <summary>Called by Overwatch: another squad is coming.</summary>
	public void HelpPromised( CombineSquad helper ) => HelpInbound = helper;

	/// <summary>Called by Overwatch: go help that squad.</summary>
	public void Respond( CombineSquad requester )
	{
		DebugLog( $"answering a call for help from {requester.Callsign}" );
		Helping = requester;
		HelpPosition = requester.Leader?.WorldPosition ?? HelpPosition;
		requester.HelpPromised( this );

		var leader = Leader;
		if ( leader is null ) return;

		Transmit( leader, CombineVoice.Answer, () =>
		{
			SetPosture( SquadPosture.Reinforce );
			Helping = requester;
		}, tail: CombineVoice.Inbound );
	}

	private bool HelpArrived()
	{
		var helper = HelpInbound;
		if ( helper is null || helper.Helping != this || helper.Leader is null || Leader is null ) return false;
		return helper.Posture is SquadPosture.Assault or SquadPosture.Search
			|| helper.Leader.WorldPosition.Distance( Leader.WorldPosition ) < 900f;
	}

	// ---------------------------------------------------------------- slots and roles

	public bool TryClaimAttack( CombineNpc npc )
	{
		foreach ( var stale in _attackers.Where( kv => !kv.Key.IsValid() || !kv.Key.IsAlive || kv.Value > 10f ).Select( kv => kv.Key ).ToList() )
			_attackers.Remove( stale );

		if ( _attackers.ContainsKey( npc ) ) return true;

		var slots = Posture == SquadPosture.Hold ? 3 : MaxAttackers;
		if ( _attackers.Count >= slots ) return false;

		_attackers[npc] = 0;
		return true;
	}

	public void ReleaseAttack( CombineNpc npc ) => _attackers.Remove( npc );

	/// <summary>Would a claim succeed right now? (Doesn't claim.)</summary>
	public bool HasFreeAttack( CombineNpc npc )
	{
		if ( _attackers.ContainsKey( npc ) ) return true;
		var live = _attackers.Count( kv => kv.Key.IsValid() && kv.Key.IsAlive && kv.Value <= 10f );
		return live < (Posture == SquadPosture.Hold ? 3 : MaxAttackers);
	}

	public void ClaimCover( CombineNpc npc, Vector3 position ) => _cover[npc] = position;

	public bool IsCoverTaken( CombineNpc npc, Vector3 position )
	{
		foreach ( var (owner, spot) in _cover )
		{
			if ( owner == npc || !owner.IsValid() || !owner.IsAlive ) continue;
			if ( spot.Distance( position ) < 60f ) return true;
			if ( owner.WorldPosition.Distance( position ) < 50f ) return true;
		}

		return false;
	}

	public void OnGrenadeThrown() => _nextGrenade = Game.Random.Float( 6f, 10f );

	private TimeUntil _nextBall;
	public bool BallReady => _nextBall;
	public void OnBallFired() => _nextBall = 12f;

	private readonly HashSet<GameObject> _warnedGrenades = new();

	/// <summary>True if someone already shouted about this grenade; marks it shouted otherwise.</summary>
	public bool Warned( GameObject grenade )
	{
		_warnedGrenades.RemoveWhere( g => !g.IsValid() );
		return !_warnedGrenades.Add( grenade );
	}

	/// <summary>"Cover me!" - the nearest squadmate starts suppressing once he hears it.</summary>
	private TimeSince _sinceCoverRequest = 100f;

	public void RequestCover( CombineNpc reloader )
	{
		// Only worth shouting about mid-firefight, and not every single reload
		if ( !InContact || _sinceCoverRequest < 10f ) return;

		var mate = Alive.Where( m => m != reloader && m != Flanker && !m.Navigation.IsMoving && m.Clip > 4 )
			.OrderBy( m => m.WorldPosition.Distance( reloader.WorldPosition ) ).FirstOrDefault();
		if ( mate is null || PrimaryEnemy is null ) return;
		_sinceCoverRequest = 0;

		Transmit( reloader, CombineVoice.CoverMe, () => AssignSuppressor( mate, 4f ) );
	}

	private void AssignSuppressor( CombineNpc member, float seconds )
	{
		if ( !member.IsValid() || !member.IsAlive ) return;
		DebugLog( $"#{NumberOf( member )} suppressing" );
		Suppressor = member;
		_suppressUntil = seconds;
		_sinceSuppress = 0;
		member.Reconsider();
	}

	public bool IsFlankOrdered( CombineNpc npc ) => Flanker == npc && Posture == SquadPosture.Assault;

	public void FlankDone( CombineNpc npc )
	{
		if ( Flanker == npc ) Flanker = null;
	}

	/// <summary>
	/// Where this member belongs in the patrol formation right now. The formation faces where
	/// the leader is walking to, not where he happens to look, so it doesn't swing about.
	/// </summary>
	public Vector3 FormationSlot( CombineNpc npc )
	{
		var leader = Leader;
		if ( leader is null || leader == npc ) return npc.WorldPosition;

		var heading = leader.WorldRotation;
		if ( leader.PatrolGoal is { } goal && goal.Distance( leader.WorldPosition ) > 60f )
			heading = Rotation.LookAt( (goal - leader.WorldPosition).WithZ( 0 ).Normal, Vector3.Up );

		var spot = leader.WorldPosition + heading * FormationOffset( npc );
		if ( OpenFrom( leader, spot ) ) return npc.Scene.NavMesh.GetClosestPoint( spot ) ?? spot;

		// No room either side (a corridor, a doorway): fall in single file behind him
		var file = leader.WorldPosition - heading.Forward * 70f * Math.Max( 1, Members.IndexOf( npc ) );
		if ( OpenFrom( leader, file ) ) return npc.Scene.NavMesh.GetClosestPoint( file ) ?? file;

		return leader.WorldPosition;
	}

	/// <summary>Can he see straight to that spot - no wall between it and the leader?</summary>
	private static bool OpenFrom( CombineNpc leader, Vector3 spot ) =>
		!leader.Scene.Trace.Ray( leader.WorldPosition + Vector3.Up * 40f, spot.WithZ( leader.WorldPosition.z + 40f ) )
			.WithoutTags( "player", "npc", "playercontroller", "trigger", "ragdoll" ).Run().Hit;

	public Vector3 FormationOffset( CombineNpc npc )
	{
		var index = Members.IndexOf( npc ) - 1;
		if ( index < 0 ) return Vector3.Zero;

		var row = 1 + index / 2;
		var side = index % 2 == 0 ? 1f : -1f;
		return new Vector3( -70f * row, 60f * side, 0f );
	}

	/// <summary>Each searcher gets his own patch around the last known position.</summary>
	private void StartSearch()
	{
		_searchCenter = PrimaryEnemy?.Position ?? LastContact;
		_searchPushes = 0;
		_searched.Clear();
		if ( _searchCenter is { } center && Leader is { } leader )
			_searchHeading = (center - leader.WorldPosition).WithZ( 0 ).Normal;
	}

	/// <summary>
	/// A member has checked his patch. Once all of them have and found nothing, the search moves
	/// on further the way the enemy went, rather than circling the same spot and giving up.
	/// </summary>
	public void SearchPointDone( CombineNpc npc )
	{
		if ( Posture != SquadPosture.Search || _searchCenter is not { } center ) return;

		_searched.Add( npc );
		if ( Alive.Any( m => !_searched.Contains( m ) ) || _searchPushes >= MaxSearchPushes ) return;

		_searched.Clear();
		if ( _searchHeading.LengthSquared < 0.5f || Leader is not { } leader ) return;

		var next = leader.Scene.NavMesh.GetClosestPoint( center + _searchHeading * 350f );
		if ( next is not { } spot || !leader.CanWalkTo( spot ) ) return;

		_searchPushes++;
		_searchCenter = spot;
		SincePosture = MathF.Min( SincePosture, SearchTime - 12f );
		DebugLog( $"search moves on ({_searchPushes})" );
		Transmit( leader, CombineVoice.Search, droppable: true );
	}

	public Vector3 SearchPoint( CombineNpc npc, Scene scene )
	{
		var center = _searchCenter ?? PrimaryEnemy?.Position ?? LastContact ?? npc.WorldPosition;
		var alive = Alive.ToList();
		var index = Math.Max( 0, alive.IndexOf( npc ) );

		// Only patches he can actually walk to - on a real map plenty of navmesh is on roofs and ledges
		for ( var i = 0; i < 6; i++ )
		{
			var point = center;
			if ( index > 0 || i > 0 )
			{
				var angle = 360f / MathF.Max( 1, alive.Count - 1 ) * (index - 1) + Game.Random.Float( -20f, 20f ) + i * 60f;
				point = center + Rotation.FromYaw( angle ).Forward * Game.Random.Float( 180f, 380f );
			}

			if ( scene.NavMesh.GetClosestPoint( point ) is { } spot && npc.CanWalkTo( spot ) )
				return spot;
		}

		return npc.WorldPosition;
	}

	// ---------------------------------------------------------------- events

	public void OnMemberDied( CombineNpc dead, GameObject killer )
	{
		Losses++;

		if ( killer.IsValid() )
		{
			killer = killer.GetComponentInParent<Player>()?.GameObject
				?? killer.GetComponentInParent<Npc>()?.GameObject
				?? killer;
		}

		var friendlyFire = killer.IsValid() && killer.GetComponent<Npc>() is { } k && Allied( dead, k );
		var survivors = Alive.Where( m => m != dead ).ToList();

		foreach ( var member in survivors )
		{
			if ( !killer.IsValid() || friendlyFire || killer == member.GameObject ) continue;
			member.SetDisposition( killer, Disposition.Hostile, priority: 10 );
		}

		var speaker = survivors.OrderBy( m => m.WorldPosition.Distance( dead.WorldPosition ) ).FirstOrDefault();
		if ( speaker is null ) return;

		var lines = survivors.Count == 1 ? CombineVoice.SquadDown : CombineVoice.ManDown;
		speaker.Invoke( 0.6f, () =>
		{
			if ( killer.IsValid() && !friendlyFire && !CombineNpc.IsDeadTarget( killer ) )
				Attacked( speaker, killer );
			Transmit( speaker, lines );
		} );
	}

	public void OnEnemyKilled( CombineNpc killer, GameObject victim )
	{
		if ( !Enemies.Remove( victim, out var record ) ) return;
		if ( record.Reported ) LastContact = record.Position;
		Transmit( killer, CombineVoice.Kill, droppable: true );
	}

	public void OnHeardDisturbance( CombineNpc member, Vector3 position )
	{
		if ( Posture != SquadPosture.Patrol ) return;
		Transmit( member, CombineVoice.Search, droppable: true );
	}

	// ---------------------------------------------------------------- the brain

	public void Tick( Scene scene )
	{
		Members.RemoveAll( m => !m.IsValid() );
		DeliverMessages();

		foreach ( var dead in Enemies.Where( kv => CombineNpc.IsDeadTarget( kv.Key ) || kv.Value.SinceSeen > SearchTime + 15f ).Select( kv => kv.Key ).ToList() )
		{
			if ( Enemies[dead].Reported ) LastContact = Enemies[dead].Position;
			Enemies.Remove( dead );
		}

		if ( Suppressor is not null && (_suppressUntil || !Suppressor.IsValid()) )
		{
			if ( Suppressor.IsValid() ) Suppressor.Reconsider();
			Suppressor = null;
		}

		if ( HelpInbound is not null && (HelpInbound.Helping != this || HelpInbound.Leader is null) )
			HelpInbound = null;

		var leader = Leader;
		if ( leader is null ) return;

		var otherBrain = false;
		OtherBrainTick( ref otherBrain );
		if ( otherBrain ) return;

		// As in HL2: for a moment after he ducks out of sight, whoever saw him still knows where he went
		foreach ( var record in Enemies.Values )
		{
			if ( record.Target.IsValid() && record.SinceSighted < FreeKnowledge && record.SinceSighted > 0.05f )
				record.Position = record.SeenPosition = record.Target.WorldPosition;
		}

		var known = Enemies.Values.Where( e => e.Reported ).ToList();
		var contact = known.Any( e => e.SinceSeen < 3f );

		switch ( Posture )
		{
			case SquadPosture.Patrol:
				if ( known.Count > 0 ) EnterCombat();
				break;

			case SquadPosture.Reinforce:
				TickReinforce( leader, contact, known );
				break;

			case SquadPosture.Assault:
			case SquadPosture.Hold:
				TickFight( scene, leader, contact, known );
				break;

			case SquadPosture.Breach:
				TickBreach( contact );
				break;

			case SquadPosture.Search:
				if ( contact ) Order( CombineVoice.Engaging, SquadPosture.Assault, urgent: true );
				else if ( SincePosture > SearchTime )
				{
					Enemies.Clear();
					LastContact = null;
					Order( CombineVoice.AllClear, SquadPosture.Patrol );
				}
				break;

			case SquadPosture.Retreat:
				if ( SincePosture > 5f ) SetPosture( known.Count > 0 ? SquadPosture.Hold : SquadPosture.Patrol );
				break;
		}
	}

	private void TickReinforce( CombineNpc leader, bool contact, List<EnemyRecord> known )
	{
		if ( contact )
		{
			Order( CombineVoice.Engaging, SquadPosture.Assault, urgent: true );
			return;
		}

		var target = Helping;
		if ( target is null || target.Leader is null )
		{
			// Too late: the squad that called is gone, but what they knew went out on the net
			if ( target is not null )
			{
				foreach ( var record in target.Enemies.Values.Where( e => e.Reported && e.Target.IsValid() ).ToList() )
					Relayed( record.Target, record.Position, record.SinceSeen );
			}

			SetPosture( Enemies.Count > 0 ? SquadPosture.Search : SquadPosture.Patrol );
			return;
		}

		HelpPosition = target.Leader.WorldPosition;
		var distance = leader.WorldPosition.Distance( HelpPosition );

		if ( !_announcedClosing && distance < 1500f )
		{
			_announcedClosing = true;
			Transmit( leader, CombineVoice.Closing, droppable: true );
		}

		if ( distance < 600f )
		{
			// On site: take over what they know and go after it together
			foreach ( var record in target.Enemies.Values.Where( e => e.Reported ) )
			{
				if ( !Enemies.ContainsKey( record.Target ) )
					Enemies[record.Target] = new EnemyRecord { Target = record.Target, Position = record.Position, SeenPosition = record.SeenPosition, SinceSeen = record.SinceSeen, SinceFirstSeen = 0, Reported = true };
			}

			SetPosture( Enemies.Count > 0 ? SquadPosture.Assault : SquadPosture.Search );
		}
	}

	private void TickFight( Scene scene, CombineNpc leader, bool contact, List<EnemyRecord> known )
	{
		if ( known.Count == 0 )
		{
			// The one we knew of is down, but where there was one there may be more: sweep where
			// they came from before calling it clear
			if ( LastContact is not null ) Order( CombineVoice.Search, SquadPosture.Search );
			else Order( CombineVoice.AllClear, SquadPosture.Patrol );
			return;
		}

		var strength = Strength;

		// Down to the last man and losing badly, nobody coming: fall back
		var badlyHurt = Alive.All( m => m.Health < m.StartHealth * 0.45f );
		if ( strength < 0.25f && badlyHurt && HelpInbound is null && Alive.Count() <= 2 && SincePosture > 6f )
		{
			Order( CombineVoice.Retreat, SquadPosture.Retreat, urgent: true );
			return;
		}

		if ( (Losses > 0 || strength < 1f) && TryRequestHelp() && Posture == SquadPosture.Assault )
			return;

		if ( Posture == SquadPosture.Hold )
		{
			if ( HelpArrived() )
			{
				Order( CombineVoice.GoSharp, SquadPosture.Assault );
				return;
			}

			// Nobody coming and no longer behind - take the fight to them
			if ( HelpInbound is null && SincePosture > 12f && strength >= 1f )
			{
				Order( CombineVoice.Advance, SquadPosture.Assault );
				return;
			}
		}

		// Losing a fight in the open: dig in and fight from cover until it turns
		if ( Posture == SquadPosture.Assault && contact && strength < 0.8f && SincePosture > 5f )
		{
			Order( CombineVoice.Hold, SquadPosture.Hold );
			return;
		}

		if ( !contact )
		{
			var freshest = known.Min( e => (float)e.SinceSeen );
			if ( freshest > 4f && Posture == SquadPosture.Assault && TryPlanBreach( scene, leader ) ) return;

			if ( freshest > 10f && Posture == SquadPosture.Assault )
			{
				Order( CombineVoice.LostContact, SquadPosture.Search );
				return;
			}
		}

		if ( Posture == SquadPosture.Assault && contact ) TryOrderFlank( leader );
		if ( contact && !SuppressActive && _sinceSuppress > 12f ) TryOrderSuppression();
	}

	private void TryOrderFlank( CombineNpc leader )
	{
		if ( Flanker.IsValid() && Flanker.IsAlive && _sinceFlank < 15f ) return;
		if ( _sinceFlank < 18f ) return;

		var alive = Alive.ToList();
		if ( alive.Count < 3 ) return;

		var candidate = alive.Where( m => m != leader && !_attackers.ContainsKey( m ) ).OrderBy( _ => Game.Random.Float() ).FirstOrDefault();
		if ( candidate is null ) return;

		_sinceFlank = 0;
		Transmit( leader, CombineVoice.FlankOrder, () =>
		{
			if ( !candidate.IsValid() || !candidate.IsAlive ) return;
			Flanker = candidate;
			DebugLog( $"#{NumberOf( candidate )} flanking" );
			candidate.Reconsider();
			Transmit( candidate, CombineVoice.Answer, droppable: true );
		} );
	}

	private void TryOrderSuppression()
	{
		// Only once he's ducked out of everyone's sight (in view he just gets shot), and only at a spot
		// someone actually saw him at - a noise through a wall is no place to pour rounds into
		var target = PrimaryEnemy;
		if ( target is null || target.SinceSeen < 0.5f || target.SinceSighted > SuppressMemory || target.HeardElsewhere ) return;

		var aim = target.SeenPosition + Vector3.Up * 45f;
		var gunner = Alive.Where( m => m != Flanker && !_attackers.ContainsKey( m ) && m.Clip > 4 )
			.OrderBy( m => m.WorldPosition.Distance( target.SeenPosition ) )
			.FirstOrDefault( m => m.SpotHidden( target.SeenPosition ) && m.FindSuppressionSpot( aim ) is not null );
		if ( gunner is null )
		{
			// Nobody has anything but wall to shoot at: look again in a few seconds
			_sinceSuppress = 9f;
			return;
		}

		_sinceSuppress = 0;
		Transmit( gunner, CombineVoice.Suppressing, () => AssignSuppressor( gunner, 6f ), droppable: false );
	}

	// ---------------------------------------------------------------- breach

	private bool TryPlanBreach( Scene scene, CombineNpc leader )
	{
		if ( Breach is not null ) return true;
		if ( _sinceBreach < 25f ) return false;

		var target = PrimaryEnemy;
		if ( target is null || target.SinceSeen > 20f ) return false;
		if ( !Alive.Any( m => m.GrenadesLeft > 0 ) ) return false;
		if ( !CombineGeometry.IsIndoors( scene, target.Position ) ) return false;
		if ( CombineGeometry.HasRoof( scene, leader.WorldPosition ) ) return false;

		var result = scene.NavMesh.CalculatePath( new Sandbox.Navigation.CalculatePathRequest { Start = leader.WorldPosition, Target = target.Position } );
		if ( !result.IsValid || result.Points is null || result.Points.Count < 2 ) return false;
		var path = result.Points.Select( p => p.Position ).ToList();

		if ( !CombineGeometry.FindEntrance( scene, path, out var entrance, out var inward, out var stack ) ) return false;

		var plan = new BreachPlan
		{
			Target = target.Position,
			Entrance = entrance,
			Inward = inward,
			Stack = stack,
			ThrowFrom = scene.NavMesh.GetClosestPoint( entrance - inward * 70f ) ?? entrance - inward * 70f,
			ThrowAt = entrance + inward * MathF.Min( 220f, entrance.Distance( target.Position ) ),
			Step = BreachPlan.Stage.Stacking,
			SinceStep = 0,
		};

		plan.Team.AddRange( Alive.OrderBy( m => m.WorldPosition.Distance( stack ) ).Take( 4 ) );
		plan.Grenadier = plan.Team.Where( m => m.GrenadesLeft > 0 ).FirstOrDefault();
		if ( plan.Grenadier is null ) return false;

		_sinceBreach = 0;
		Breach = plan;
		DebugLog( $"breach planned: entrance {entrance}, team {plan.Team.Count}" );
		SetPosture( SquadPosture.Breach );
		Transmit( leader, CombineVoice.ReadyCharges, () => plan.Ordered = true );
		return true;
	}

	public void OnBreachGrenadeThrown()
	{
		if ( Breach is null ) return;
		Breach.Step = BreachPlan.Stage.Blast;
		Breach.SinceStep = 0;
		OrderVersion++;
	}

	private void TickBreach( bool contact )
	{
		var plan = Breach;
		if ( plan is null )
		{
			SetPosture( SquadPosture.Search );
			return;
		}

		plan.Team.RemoveAll( m => !m.IsValid() || !m.IsAlive );

		// They came out to fight - forget the plan
		if ( contact && plan.Step != BreachPlan.Stage.Rushing )
		{
			SetPosture( SquadPosture.Assault );
			return;
		}

		switch ( plan.Step )
		{
			case BreachPlan.Stage.Stacking:
				var ready = plan.Team.Count( m => m.WorldPosition.Distance( plan.StackSpot( m ) ) < 90f );
				if ( plan.Ordered && (ready >= Math.Min( 2, plan.Team.Count ) || plan.SinceStep > 14f) )
				{
					plan.Step = BreachPlan.Stage.Throwing;
					plan.SinceStep = 0;
					DebugLog( "breach: throwing" );
					OrderVersion++;
				}
				break;

			case BreachPlan.Stage.Throwing:
				if ( !plan.Grenadier.IsValid() || !plan.Grenadier.IsAlive || plan.SinceStep > 10f )
				{
					plan.Step = BreachPlan.Stage.Blast;
					plan.SinceStep = 0;
					OrderVersion++;
				}
				break;

			case BreachPlan.Stage.Blast:
				var fuse = plan.Grenadier.IsValid() ? plan.Grenadier.GrenadeFuse : 3f;
				if ( plan.SinceStep > fuse + 0.3f && plan.Team.Count > 0 )
				{
					plan.SinceStep = -100f;
					Transmit( plan.Team.First(), CombineVoice.GoSharp, () =>
					{
						plan.Step = BreachPlan.Stage.Rushing;
						plan.SinceStep = 0;
						DebugLog( "breach: rushing" );
						OrderVersion++;
					}, urgent: true );
				}
				break;

			case BreachPlan.Stage.Rushing:
				if ( plan.SinceStep > 10f ) SetPosture( SquadPosture.Search );
				break;
		}
	}

	// ---------------------------------------------------------------- idle

	/// <summary>Idle radio traffic: one asks, another answers.</summary>
	public void TryChatter( CombineNpc speaker )
	{
		if ( !_nextChatter || !CanSpeak ) return;
		_nextChatter = Game.Random.Float( 18f, 35f );

		Transmit( speaker, CombineVoice.Chatter, () =>
		{
			var other = Alive.Where( m => m != speaker ).OrderBy( _ => Game.Random.Float() ).FirstOrDefault();
			if ( other is not null ) Transmit( other, CombineVoice.Answer, droppable: true );
		}, droppable: true );
	}
}
