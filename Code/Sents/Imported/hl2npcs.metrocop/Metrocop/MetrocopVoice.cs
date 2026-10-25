using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;
using Sandbox.Npcs;

namespace Hl2Metrocop;

/// <summary>
/// How a Metrocop talks: HL2's police sentences (MetrocopSentences.cs, from scripts/sentences.txt)
/// said the way CAI_Sentence says them - by priority, one voice per squad at a time, with his own
/// callsign and number filled in for life. The words play through the soldiers' radio player.
/// </summary>
public static partial class MetrocopVoice
{
	// SENTENCE_PRIORITY_*
	public const int Normal = 0;
	public const int Medium = 1;
	public const int High = 2;

	/// <summary>SENTENCE_CRITERIA_ALWAYS / SENTENCE_PRIORITY_INVALID: said no matter what, over anyone.</summary>
	public const int Always = 100;

	/// <summary>Callsign and number, kept for life (V_MYNAMEP, V_MYNUMP).</summary>
	private static readonly Dictionary<CombineNpc, (string Name, string Number)> Ids = new();

	public static void Init( CombineNpc cop )
	{
		Ids[cop] = (PickVirtual( "V_MYNAMEP" ), PickVirtual( "V_MYNUMP" ));
	}

	private static string PickVirtual( string group ) =>
		VirtualWords.TryGetValue( group, out var lines ) && lines.Length > 0 ? Game.Random.FromArray( lines ) : "";

	/// <summary>
	/// Say a line of this group. Not Always: dropped if someone in his squad is talking - unless this
	/// one matters more (HL2's FOkToMakeSound with squad-wide sound wait times).
	/// </summary>
	public static bool Speak( CombineNpc cop, string group, int priority = Normal, bool leaderOnly = false )
	{
		if ( cop is null || !cop.IsValid() || cop.IsProxy ) return false;
		if ( cop.NoChatter && priority < Medium && group.StartsWith( "METROPOLICE_IDLE" ) ) return false;
		if ( !Sentences.TryGetValue( group, out var lines ) || lines.Length == 0 ) return false;
		if ( leaderOnly && cop.Squad?.Leader is { } leader && leader != cop ) return false;

		var squad = cop.Squad;
		if ( priority < Always && squad is not null && !squad.CopMayTalk( priority ) ) return false;

		var sentence = Expand( cop, Game.Random.FromArray( lines ) );
		if ( string.IsNullOrWhiteSpace( sentence ) ) return false;

		var duration = CombineVoice.EstimateDuration( sentence ) + 0.3f;
		squad?.CopTalking( priority, duration );
		if ( CombineSquad.Debug ) Log.Info( $"[cop voice] {cop.GameObject.Id.ToString()[..4]} {group}: {sentence}" );
		cop.Speak( sentence );
		return true;
	}

	/// <summary>Fill in HL2's virtual words; ones HL2 has nothing for are left out, as the game does.</summary>
	private static string Expand( CombineNpc cop, string line )
	{
		var words = new List<string>();
		var folder = "npc/metropolice/vo/";
		foreach ( var token in line.Split( ' ', StringSplitOptions.RemoveEmptyEntries ) )
		{
			if ( !token.StartsWith( "V_" ) )
			{
				if ( token.Contains( '/' ) ) folder = token[..(token.LastIndexOf( '/' ) + 1)];
				words.Add( token.StartsWith( '(' ) ? token : "police/" + token );
				continue;
			}

			string fill = token switch
			{
				"V_MYNAMEP" => Ids.TryGetValue( cop, out var id ) ? id.Name : PickVirtual( token ),
				"V_MYNUMP" => Ids.TryGetValue( cop, out var id2 ) ? id2.Number : PickVirtual( token ),
				"V_DISTP" => Distance( cop ),
				_ => VirtualGroups.TryGetValue( token, out var group ) && group is not null ? PickVirtual( group ) : "",
			};

			foreach ( var word in fill.Split( ' ', StringSplitOptions.RemoveEmptyEntries ) )
			{
				if ( word.StartsWith( '(' ) ) { words.Add( word ); continue; }

				// A word with no folder takes the line's (HL2), or the other police folder if only that has it
				var path = word.Contains( '/' ) ? word
					: Available.Contains( folder + word ) ? folder + word
					: Available.Contains( "npc/metropolice/vo/" + word ) ? "npc/metropolice/vo/" + word
					: Available.Contains( "npc/overwatch/radiovoice/" + word ) ? "npc/overwatch/radiovoice/" + word
					: null;
				if ( path is not null ) words.Add( "police/" + path );
			}
		}

		return string.Join( " ", words );
	}

	/// <summary>V_DISTP: how far off the enemy is, in meters, in the police number words.</summary>
	private static string Distance( CombineNpc cop )
	{
		var enemy = cop.CopEnemy;
		if ( !enemy.IsValid() ) return "";
		var meters = (int)MathF.Round( cop.WorldPosition.Distance( enemy.WorldPosition ) * 0.0254f );
		if ( meters <= 20 && meters < NumberWords.Length ) return "npc/metropolice/vo/" + NumberWords[Math.Max( 0, meters )];
		var tens = Math.Min( 9, meters / 10 );
		var name = tens switch { 2 => "twenty", 3 => "thirty", 5 => "fifty", 6 => "sixty", 7 => "seventy", 8 => "eighty", 9 => "ninety", _ => null };
		return name is not null && NumberWords.Contains( name ) ? "npc/metropolice/vo/" + name : "";
	}

	public static void Tick( CombineNpc cop ) { }

	// ---------------------------------------------------------------- what HL2's cop says when

	/// <summary>AnnounceEnemyType: the squad leader (or anyone whose leader has another enemy) calls out what it is.</summary>
	public static void AnnounceEnemyType( CombineNpc cop, GameObject enemy )
	{
		if ( !enemy.IsValid() || cop.Squad is null || cop.ArrestEnemy ) return;
		if ( !cop.PlayerIsCriminal( null ) ) return;

		var leader = cop.Squad.Leader;
		if ( leader != cop && leader is not null && leader.CopEnemy == enemy ) return;

		var group = "METROPOLICE_MONST";
		if ( enemy.GetComponent<Player>().IsValid() )
			group = InVehicle( enemy ) ? "METROPOLICE_MONST_PLAYER_VEHICLE" : "METROPOLICE_MONST_PLAYER";
		else if ( enemy.GetComponent<Npc>() is { } npc )
		{
			group = npc.Faction switch
			{
				Factions.Citizen or Factions.Ally => "METROPOLICE_MONST_CITIZENS",
				Factions.Monster => npc.GameObject.Tags.Has( "zombie" ) ? "METROPOLICE_MONST_ZOMBIES"
					: npc.GameObject.Tags.Has( "headcrab" ) || npc.GameObject.Tags.Has( "barnacle" ) ? "METROPOLICE_MONST_PARASITES"
					: npc.GameObject.Tags.Has( "antlion" ) ? "METROPOLICE_MONST_BUGS"
					: "METROPOLICE_MONST",
				_ => "METROPOLICE_MONST",
			};
		}

		Speak( cop, group, High );
	}

	/// <summary>AnnounceEnemyKill.</summary>
	public static void AnnounceEnemyKill( CombineNpc cop, GameObject victim )
	{
		if ( !victim.IsValid() ) return;
		var group = "METROPOLICE_KILL_MONST";
		if ( victim.GetComponent<Player>().IsValid() ) group = "METROPOLICE_KILL_PLAYER";
		else if ( victim.GetComponent<Npc>() is { } npc )
		{
			group = npc.Faction switch
			{
				Factions.Citizen or Factions.Ally => "METROPOLICE_KILL_CITIZENS",
				_ => npc.GameObject.Tags.Has( "zombie" ) ? "METROPOLICE_KILL_ZOMBIES"
					: npc.GameObject.Tags.Has( "headcrab" ) || npc.GameObject.Tags.Has( "barnacle" ) ? "METROPOLICE_KILL_PARASITES"
					: npc.GameObject.Tags.Has( "antlion" ) ? "METROPOLICE_KILL_BUGS"
					: "METROPOLICE_KILL_MONST",
			};
		}

		Speak( cop, group, High );
	}

	public static void AnnounceOutOfAmmo( CombineNpc cop ) =>
		Speak( cop, cop.Clip <= 0 ? "METROPOLICE_COVER_NO_AMMO" : "METROPOLICE_COVER_LOW_AMMO" );

	/// <summary>AnnounceTakeCoverFromDanger: an enemy's grenade, a car, a thrown manhack, anything else.</summary>
	public static void AnnounceDanger( CombineNpc cop, GameObject danger )
	{
		if ( danger.IsValid() && danger.GetComponent<TimedExplosive>() is { } grenade )
		{
			// Not for our own side's grenades
			var thrower = grenade.Attacker;
			if ( thrower.IsValid() && thrower.GetComponent<Npc>() is { } npc && CombineSquad.Allied( cop, npc ) ) return;
			Speak( cop, "METROPOLICE_DANGER_GREN", High );
			return;
		}

		if ( danger.IsValid() && danger.Tags.Has( "manhack" ) )
		{
			Speak( cop, "METROPOLICE_DANGER_MANHACK", High );
			return;
		}

		Speak( cop, "METROPOLICE_DANGER", High );
	}

	/// <summary>LostEnemySound: "lost him" - long after 10 s out of sight - at most every 5-15 s.</summary>
	public static void LostEnemy( CombineNpc cop, bool longAgo )
	{
		if ( !cop.PlayerIsCriminal( null ) || !cop.LostSoundReady ) return;
		if ( Speak( cop, longAgo ? "METROPOLICE_LOST_LONG" : "METROPOLICE_LOST_SHORT" ) )
			cop.LostSoundSaid();
	}

	/// <summary>FoundEnemySound: the one he lost is back in sight.</summary>
	public static void RefoundEnemy( CombineNpc cop )
	{
		if ( cop.ArrestEnemy ) return;
		Speak( cop, "METROPOLICE_REFIND_ENEMY", High );
	}

	/// <summary>SpeakSentence( METROPOLICE_SENTENCE_MOVE_INTO_POSITION ): a hurt player gets called out; a long way round is a flank.</summary>
	public static void MoveIntoPosition( CombineNpc cop, GameObject enemy, float pathLength )
	{
		if ( !cop.PlayerIsCriminal( null ) || !enemy.IsValid() || cop.ArrestEnemy ) return;

		if ( enemy.GetComponent<Player>() is { } player && player.Health <= 20f && cop.Squad is { } squad && !squad.CopPlayerHurtSaid )
		{
			if ( Speak( cop, "METROPOLICE_PLAYERHIT", High ) ) squad.CopPlayerHurtSaid = true;
		}

		if ( pathLength > 20f * 12f ) Speak( cop, "METROPOLICE_FLANK" );
	}

	/// <summary>Sitting in something that moves (the sandbox has no vehicle class of its own: a seat on a body).</summary>
	private static bool InVehicle( GameObject player )
	{
		var seat = player.Parent;
		if ( seat is null || seat is Scene || !seat.IsValid() ) return false;
		var body = seat.Root;
		return body is not null && body != player && body.GetComponent<Rigidbody>() is { MotionEnabled: true };
	}
}

public static partial class CombineVoice
{
	// The cop speaks only HL2's police lines (MetrocopVoice); the soldier's radio words aren't in his package
	static partial void OtherVoiceOnly( ref bool only ) => only = true;
}
