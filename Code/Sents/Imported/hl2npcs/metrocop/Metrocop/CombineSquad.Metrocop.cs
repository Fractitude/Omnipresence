using System.Collections.Generic;
using System.Linq;
using Sandbox;

namespace Hl2Metrocop;

/// <summary>The turns an HL2 police squad hands out, per enemy (npc_metropolice.cpp's squad slots).</summary>
public enum CopSlot
{
	Attack1,
	Attack2,
	ChargeEnemy,
	Harass,
	DeployManhack,
	Advance,
	AttackOccluder1,
	AttackOccluder2,
	CoveringFire1,
	CoveringFire2,
	ArrestEnemy,
	InvestigateSound,
}

public sealed partial class CombineSquad
{
	// HL2's squads give no orders: what one sees, all know at once
	partial void OtherBrainSighted( EnemyRecord record, bool isNew, ref bool handled )
	{
		record.Reported = true;
		handled = isNew;
	}

	// A fight or running feet heard is a noise to look into (HL2's COND_HEAR_COMBAT / COND_HEAR_PLAYER),
	// not an enemy to go after
	partial void OtherBrainHeard( CombineNpc listener, Vector3 position, ref bool handled )
	{
		handled = true;
		listener.CopHeard( position );
	}

	partial void OtherBrainTick( ref bool handled )
	{
		handled = true;

		// Free knowledge: for 6 s after he ducks out of sight they still know where he went
		foreach ( var record in Enemies.Values )
		{
			if ( record.Target.IsValid() && record.SinceSighted < CombineNpc.CopFreeKnowledge && record.SinceSighted > 0.05f )
				record.Position = record.Target.WorldPosition;
		}

		var fighting = Enemies.Values.Any( e => e.SinceSeen < 20f );
		Posture = fighting ? SquadPosture.Assault : SquadPosture.Patrol;

		TickCopChatter();
	}

	// ---------------------------------------------------------------- turns

	private readonly Dictionary<(GameObject Enemy, CopSlot Slot), CombineNpc> _copSlots = new();
	private readonly Dictionary<CombineNpc, (GameObject Enemy, CopSlot Slot)> _copHeld = new();

	private bool CopSlotTaken( GameObject enemy, CopSlot slot, CombineNpc asking ) =>
		_copSlots.TryGetValue( (enemy, slot), out var holder ) && holder != asking && holder.IsValid() && holder.IsAlive
		&& _copHeld.TryGetValue( holder, out var held ) && held.Enemy == enemy && held.Slot == slot;

	/// <summary>Take the first free turn in the range against that enemy (one already held counts).</summary>
	public bool CopOccupy( CombineNpc npc, GameObject enemy, CopSlot first, CopSlot last )
	{
		if ( _copHeld.TryGetValue( npc, out var held ) && held.Enemy == enemy && held.Slot >= first && held.Slot <= last )
			return true;

		for ( var slot = first; slot <= last; slot++ )
		{
			if ( CopSlotTaken( enemy, slot, npc ) ) continue;
			CopVacate( npc );
			_copSlots[(enemy, slot)] = npc;
			_copHeld[npc] = (enemy, slot);
			return true;
		}

		return false;
	}

	public bool CopOccupy( CombineNpc npc, GameObject enemy, CopSlot slot ) => CopOccupy( npc, enemy, slot, slot );

	public bool CopHolds( CombineNpc npc, CopSlot first, CopSlot last ) =>
		_copHeld.TryGetValue( npc, out var held ) && held.Slot >= first && held.Slot <= last;

	/// <summary>A new schedule: whatever turn the last one held goes back (HL2's OnScheduleChange).</summary>
	public void CopVacate( CombineNpc npc )
	{
		if ( !_copHeld.Remove( npc, out var held ) ) return;
		if ( _copSlots.TryGetValue( held, out var holder ) && holder == npc ) _copSlots.Remove( held );
	}

	/// <summary>How many of the squad are on an arrest right now.</summary>
	public int CopArresting => Alive.Count( m => m.ActiveSchedule is CopArrestSchedule or CopWarnAndArrestSchedule );

	// ---------------------------------------------------------------- one voice at a time (FOkToMakeSound)

	private float _copTalkUntil;
	private int _copTalkPriority;

	public bool CopMayTalk( int priority ) => Time.Now >= _copTalkUntil || priority > _copTalkPriority;

	public void CopTalking( int priority, float duration )
	{
		_copTalkUntil = Time.Now + duration;
		_copTalkPriority = priority;
	}

	/// <summary>bits_MEMORY_PLAYER_HURT, remembered squad-wide: the "he's hurt" call is made once.</summary>
	public bool CopPlayerHurtSaid { get; set; }

	/// <summary>bits_MEMORY_PLAYER_HARASSED, squad-wide.</summary>
	public bool CopPlayerHarassed { get; set; }

	// ---------------------------------------------------------------- deaths

	public void CopMemberDied( CombineNpc dead )
	{
		var survivors = Alive.Where( m => m != dead ).ToList();
		var speaker = survivors.OrderBy( m => m.WorldPosition.Distance( dead.WorldPosition ) ).FirstOrDefault();
		if ( speaker is null ) return;

		// A question asked and never answered: ask again later
		foreach ( var m in survivors ) m.CopResetChatterIfWaiting();

		// NotifyDeadFriend
		speaker.Invoke( 0.6f, () =>
		{
			if ( !speaker.IsValid() || !speaker.IsAlive ) return;
			if ( survivors.Count < 2 ) MetrocopVoice.Speak( speaker, "METROPOLICE_LAST_OF_SQUAD", MetrocopVoice.Medium );
			else MetrocopVoice.Speak( speaker, "METROPOLICE_MAN_DOWN", MetrocopVoice.Medium );
		} );
	}

	// ---------------------------------------------------------------- idle chatter

	private TimeUntil _copNextIdle = 8f;

	/// <summary>
	/// HL2's IdleSound in an idle or alert squad: one cop asks (a status check or a question), the
	/// others answer; the words differ once the player is wanted (_CR lines). A player standing close
	/// gets told off first, once per squad. Alone, a cop just mutters into the radio.
	/// </summary>
	private void TickCopChatter()
	{
		if ( !_copNextIdle ) return;
		_copNextIdle = Game.Random.Float( 10f, 25f );

		if ( Enemies.Values.Any( e => e.SinceSeen < 30f ) ) return;
		var cops = Alive.Where( m => !m.NoChatter ).ToList();
		if ( cops.Count == 0 ) return;

		var speaker = Game.Random.FromList( cops );
		var criminal = speaker.PlayerIsCriminal( null );

		if ( speaker.PlayerNear && !CopPlayerHarassed )
		{
			if ( MetrocopVoice.Speak( speaker, "METROPOLICE_IDLE_HARASS_PLAYER" ) ) CopPlayerHarassed = true;
			return;
		}

		if ( Game.Random.Int( 0, 1 ) == 0 ) return;

		var question = Game.Random.Int( 0, 2 );
		if ( cops.Count < 2 || question == 2 )
		{
			MetrocopVoice.Speak( speaker, criminal ? "METROPOLICE_IDLE_CR" : "METROPOLICE_IDLE" );
			return;
		}

		var ask = question == 0 ? "METROPOLICE_IDLE_CHECK" : "METROPOLICE_IDLE_QUEST";
		if ( !MetrocopVoice.Speak( speaker, criminal ? ask + "_CR" : ask ) ) return;

		var answerer = Game.Random.FromList( cops.Where( c => c != speaker ).ToList() );
		var answer = question == 0 ? "METROPOLICE_IDLE_CLEAR" : "METROPOLICE_IDLE_ANSWER";
		answerer.CopWaitingToAnswer = true;
		answerer.Invoke( 3f, () =>
		{
			if ( !answerer.IsValid() || !answerer.IsAlive || !answerer.CopWaitingToAnswer ) return;
			answerer.CopWaitingToAnswer = false;
			MetrocopVoice.Speak( answerer, criminal ? answer + "_CR" : answer );
		} );
	}
}
