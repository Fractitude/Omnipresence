using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sandbox;

namespace Hl2Combine;

/// <summary>
/// Radio chatter built HL2-style: a click on, an optional callsign, the words, a click off.
/// Lines are grouped by what they mean - the squad acts on the meaning once the line has been heard.
/// </summary>
public static class CombineVoice
{
	public static readonly string[] Spotted =
	{
		"contact", "contactconfim", "contactconfirmprosecuting", "viscon", "weaponsoffsafeprepforcontact",
		"prepforcontact", "goactiveintercept", "callcontacttarget1", "targetcompromisedmovein",
	};

	public static readonly string[] HeardContact = { "prepforcontact", "weaponsoffsafeprepforcontact", "readyweaponshostilesinbound", "stayalert" };
	public static readonly string[] Engaging = { "engaging", "prosecuting", "contactconfirmprosecuting" };
	public static readonly string[] LostContact = { "lostcontact", "motioncheckallradials", "stayalertreportsightlines" };
	public static readonly string[] Search = { "motioncheckallradials", "sweepingin", "stayalertreportsightlines" };
	public static readonly string[] AllClear = { "reportingclear", "sectorissecurenovison", "reportallpositionsclear", "sightlineisclear" };
	public static readonly string[] Cover = { "cover", "coverme" };
	public static readonly string[] CoverHurt = { "coverhurt", "requestmedical", "requeststimdose" };
	public static readonly string[] CoverMe = { "coverme" };
	public static readonly string[] Suppressing = { "suppressing" };
	public static readonly string[] FlankOrder = { "movein", "fixsightlinesmovein", "closing" };
	public static readonly string[] Advance = { "movein", "sweepingin", "unitismovingin", "closing" };
	public static readonly string[] Hold = { "hardenthatposition", "stabilizationteamholding", "stayalert" };
	public static readonly string[] RequestHelp = { "overwatchrequestreinforcement", "heavyresistance", "overwatchrequestreserveactivation" };
	public static readonly string[] Inbound = { "unitisinbound", "inbound", "unitismovingin" };
	public static readonly string[] Closing = { "unitisclosing", "closing", "closing2" };
	public static readonly string[] GoSharp = { "gosharpgosharp", "gosharp", "movein" };
	public static readonly string[] ReadyCharges = { "readycharges", "readyweapons", "weaponsoffsafeprepforcontact" };
	public static readonly string[] Grenade = { "flush", "extractoraway", "flash" };
	public static readonly string[] Danger = { "bouncerbouncer" };
	public static readonly string[] Retreat = { "ripcordripcord", "ripcord" };
	public static readonly string[] Kill = { "onedown", "onecontained", "overwatchtarget1sterilized", "affirmativewegothimnow", "flatline", "delivered", "cleaned", "overwatchtargetcontained" };
	public static readonly string[] ManDown = { "onedutyvacated", "heavyresistance" };
	public static readonly string[] SquadDown = { "overwatchteamisdown", "overwatchsectoroverrun" };
	public static readonly string[] Chatter =
	{
		"stayalert", "reportingclear", "sectorissecurenovison", "teamdeployedandscanning", "stabilizationteamholding",
		"reportallpositionsclear", "reportallradialsfree", "motioncheckallradials", "isholdingatcode", "secure",
	};
	public static readonly string[] Answer = { "copy", "copythat", "affirmative", "affirmative2" };

	public static readonly string[] Callsigns =
	{
		"apex", "blade", "dagger", "dash", "echo", "fist", "ghost", "grid", "hammer", "helix", "hunter", "hurricane",
		"ice", "jet", "judge", "kilo", "mace", "nomad", "nova", "phantom", "quicksand", "razor", "reaper", "savage",
		"scar", "slash", "spear", "stab", "star", "stinger", "storm", "striker", "sundown", "sweeper", "swift",
		"sword", "tracker", "uniform", "vamp", "winder",
	};

	public static readonly string[] Numbers = { "one", "two", "three", "four", "five", "six", "seven", "eight", "niner" };

	private static readonly string[] On = { "on1", "on2" };
	private static readonly string[] Off = { "off1", "off2", "off3" };

	/// <summary>How far away a soldier's radio can be heard.</summary>
	public const float RadioRange = 2000f;

	/// <summary>
	/// Which of HL2's dubs this player hears the radio in. Local to each player and remembered;
	/// a word the dub doesn't have is played in English.
	/// </summary>
	// Gets its console command ("combine_voice", saved per player) once the dub packages are published
	public static string Language
	{
		get => _language;
		set
		{
			_language = value;
			// Start fetching the dub right away so the first lines are already in it
			if ( Dub is { } dub ) _ = DubPack( dub );
		}
	}

	private static string _language = "en";

	private static readonly string[] Dubs = { "de", "ru" };

	private static string Dub => DubFor( _language );

	private static string DubFor( string language ) =>
		language?.Trim().ToLowerInvariant() is { } lang && Dubs.Contains( lang ) ? lang : null;

	// Radio clicks and pauses - HL2 never dubbed these
	private static readonly HashSet<string> Undubbed = new() { "on1", "on2", "off1", "off2", "off3", "_comma", "_period" };

	// The dubs are packages of their own, so only players who pick one download it
	private static readonly Dictionary<string, Task<Dictionary<string, SoundFile>>> DubPacks = new();

	private static Task<Dictionary<string, SoundFile>> DubPack( string dub )
	{
		if ( !DubPacks.TryGetValue( dub, out var pack ) )
		{
			pack = FetchDub( dub );
			DubPacks[dub] = pack;
		}

		return pack;
	}

	/// <summary>A dub's words by name. Empty if it can't be had - the radio then stays in English.</summary>
	private static async Task<Dictionary<string, SoundFile>> FetchDub( string dub )
	{
		var words = new Dictionary<string, SoundFile>();
		try
		{
			// In the project the dub is right here; in the game it's its own package
			var voice = ResourceLibrary.Get<SoundEvent>( $"hl2_combine/sounds/voice_{dub}.sound" )
				?? await Cloud.Load<SoundEvent>( $"hl2npcs.combine_voice_{dub}" );

			foreach ( var file in voice?.Sounds ?? new List<SoundFile>() )
			{
				if ( file is null ) continue;
				var name = file.ResourcePath ?? "";
				name = name[(name.LastIndexOf( '/' ) + 1)..];
				var dot = name.LastIndexOf( '.' );
				words[dot > 0 ? name[..dot] : name] = file;
			}
		}
		catch ( Exception e )
		{
			Log.Warning( $"Combine radio: couldn't load the \"{dub}\" voice ({e.Message}), staying in English" );
		}

		return words;
	}

	// Real lengths once a file has been loaded; a fair guess before that
	private static readonly Dictionary<string, float> Durations = new();

	// What each speaker is saying right now, so death can cut them off
	private static readonly Dictionary<GameObject, List<SoundHandle>> Speaking = new();

	public static string Build( IEnumerable<string> content, string callsign, int number, float callsignChance = 0.35f )
	{
		var words = new List<string> { Game.Random.FromArray( On ) };

		if ( callsign is not null && Game.Random.Float() < callsignChance )
		{
			words.Add( callsign );
			words.Add( Numbers[number % Numbers.Length] );
		}

		words.AddRange( content );
		words.Add( Game.Random.FromArray( Off ) );
		return string.Join( " ", words );
	}

	public static float EstimateDuration( string sentence )
	{
		var total = 0f;
		foreach ( var word in sentence.Split( ' ' ) )
		{
			if ( word.StartsWith( '(' ) ) continue;
			if ( Durations.TryGetValue( word, out var d ) ) total += d;
			else total += word.StartsWith( "on" ) || word.StartsWith( "off" ) ? 0.3f : 0.9f;
		}

		return total;
	}

	/// <summary>Load every line once so durations are known before the first fight.</summary>
	public static async Task Preload()
	{
		var all = Spotted.Concat( HeardContact ).Concat( Engaging ).Concat( LostContact ).Concat( Search ).Concat( AllClear ).Concat( Cover )
			.Concat( CoverHurt ).Concat( Suppressing ).Concat( FlankOrder ).Concat( Advance ).Concat( Hold ).Concat( RequestHelp )
			.Concat( Inbound ).Concat( Closing ).Concat( GoSharp ).Concat( ReadyCharges ).Concat( Grenade ).Concat( Danger )
			.Concat( Retreat ).Concat( Kill ).Concat( ManDown ).Concat( SquadDown ).Concat( Chatter ).Concat( Answer )
			.Concat( Callsigns ).Concat( Numbers ).Concat( On ).Concat( Off ).Distinct();

		foreach ( var word in all )
		{
			if ( Durations.ContainsKey( word ) ) continue;
			var file = await LoadWord( word, null );
			if ( file is not null ) Durations[word] = file.Duration;
		}
	}

	/// <summary>
	/// A word's sound, or null if it can't be loaded (missing from the package). From the dub
	/// when one is chosen and it has that word, in English otherwise.
	/// </summary>
	private static async Task<SoundFile> LoadWord( string word, string dub )
	{
		// A path under sounds/ (HL2's police lines name each word's folder)
		if ( word.Contains( '/' ) )
			return await LoadFile( $"hl2_combine/sounds/{word}.vsnd" );

		if ( dub is not null && !Undubbed.Contains( word ) )
		{
			// Shipped with this soldier, or else in the dub's own package
			if ( await LoadFile( $"hl2_combine/sounds/vo_{dub}/{word}.vsnd" ) is { } shipped )
				return shipped;
			if ( (await DubPack( dub )).TryGetValue( word, out var dubbed ) && await Ready( dubbed ) is { } ready )
				return ready;
		}

		return await LoadFile( $"hl2_combine/sounds/vo/{word}.vsnd" );
	}

	private static async Task<SoundFile> LoadFile( string path ) => await Ready( SoundFile.Load( path ) );

	private static async Task<SoundFile> Ready( SoundFile file )
	{
		if ( file is null ) return null;
		if ( !file.IsLoaded && !await file.LoadAsync() ) return null;
		return file.IsValidForPlayback ? file : null;
	}

	/// <summary>
	/// Plays the words back to back from the speaker's position - in the soldier's own dub if he
	/// has one, otherwise in the one this player picked.
	/// </summary>
	public static async Task Play( GameObject speaker, string sentence, string language = null )
	{
		Stop( speaker );
		var handles = new List<SoundHandle>();
		Speaking[speaker] = handles;

		var delay = 0f;
		var volume = 1f;
		var pitch = 1f;
		var dub = DubFor( language ) ?? Dub;
		foreach ( var word in sentence.Split( ' ' ) )
		{
			// HL2 sentence marks: (v80) volume in percent, (p110) pitch in percent, for the words after it
			if ( word.Length > 2 && word[0] == '(' )
			{
				if ( float.TryParse( word[2..^1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var percent ) )
				{
					if ( word[1] == 'v' ) volume = percent / 100f;
					else if ( word[1] == 'p' ) pitch = percent / 100f;
				}
				continue;
			}

			var file = await LoadWord( word, dub );
			if ( !speaker.IsValid() || !Speaking.TryGetValue( speaker, out var current ) || current != handles ) return;
			if ( file is null )
			{
				if ( CombineSquad.Debug ) Log.Warning( $"[voice] can't play \"{word}\"" );
				continue;
			}

			if ( CombineSquad.Debug ) Log.Info( $"[voice] {file.ResourcePath} {file.Duration:0.00}s" );
			if ( dub is null ) Durations[word] = file.Duration;

			// Named: positionally the fourth number is a delay in seconds
			var handle = Sound.PlayFile( file, volume: volume, pitch: pitch, delay: delay );
			if ( handle.IsValid() )
			{
				handle.Position = speaker.WorldPosition + Vector3.Up * 64f;
				handle.Parent = speaker;
				handle.FollowParent = true;
				handle.DistanceAttenuation = true;
				handle.Distance = RadioRange;
				handles.Add( handle );
			}

			delay += file.Duration / pitch;
		}
	}

	/// <summary>Cut the speaker off mid-sentence.</summary>
	public static void Stop( GameObject speaker )
	{
		if ( speaker is null || !Speaking.Remove( speaker, out var handles ) ) return;

		foreach ( var handle in handles )
		{
			if ( handle.IsValid() ) handle.Stop( 0.05f );
		}
	}
}

public sealed partial class CombineNpc
{
	/// <summary>
	/// Host: say a line that carries no order. Dropped if the squad channel is busy -
	/// orders go through <see cref="CombineSquad.Transmit"/> and always get through.
	/// </summary>
	public void Say( string[] lines, bool force = false )
	{
		if ( IsProxy || !IsAlive ) return;

		if ( Squad is not null )
		{
			Squad.Transmit( this, lines, null, urgent: force, droppable: !force );
			return;
		}

		if ( OtherBrainTalks( lines ) ) return;
		RadioRpc( CombineVoice.Build( new[] { Game.Random.FromArray( lines ) }, null, 0 ) );
	}

	/// <summary>Host: start speaking a prepared sentence everywhere.</summary>
	public void Speak( string sentence )
	{
		if ( IsProxy || !IsAlive ) return;
		RadioRpc( sentence );
	}

	private void RadioRpc( string sentence ) => RadioBroadcast( sentence, RadioLanguage );

	[Rpc.Broadcast( NetFlags.HostOnly )]
	private void RadioBroadcast( string sentence, string language )
	{
		_ = CombineVoice.Play( GameObject, sentence, language );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	private void StopRadioRpc()
	{
		CombineVoice.Stop( GameObject );
	}
}
