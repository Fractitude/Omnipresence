using System.Threading.Tasks;

namespace Sandbox;

/// <summary>
/// Sample of scripting the Navi Messages app from the game: an NPC that writes to each player by itself, then answers what
/// they send. Put it on any GameObject in the scene (the host runs the story; on clients it does nothing).
///
/// What it shows of the API (<see cref="NaviNpcs"/>, <see cref="NaviNpc"/>):
/// - a hidden NPC that is in nobody's list until it first writes to them,
/// - the game choosing when and to whom it sends (<see cref="NaviNpc.Send"/>), with delays,
/// - answering a player (<see cref="NaviNpc.Received"/>), keeping a step per player,
/// - rewriting an earlier message (<see cref="NaviNpc.Edit"/>), and going offline.
/// A player who has already talked to it isn't greeted again: its messages are saved with the others.
/// </summary>
public sealed class WiredStranger : Component
{
	/// <summary>Seconds a player is in the game before the stranger writes to them.</summary>
	[Property]
	public float GreetingDelay { get; set; } = 6f;

	/// <summary>Seconds the stranger takes to answer.</summary>
	[Property]
	public float ReplyDelay { get; set; } = 1.5f;

	const string Key = "wired-stranger";

	static readonly string[] Replies =
	[
		"You can hear me. Good.",
		"This isn't the real world. Not entirely.",
		"Everyone is connected here. Everyone is a little bit everywhere.",
		"Don't trust what you read here. Not even this.",
	];

	NaviNpc stranger;
	TimeSince sinceScan;

	// What each player has been through, by player id.
	readonly HashSet<string> greeted = new();
	readonly Dictionary<string, int> step = new();

	protected override void OnStart()
	{
		stranger = NaviNpcs.Register( Key, "Unknown" );
		stranger.Hidden = true;
		stranger.Received += OnReceived;
	}

	protected override void OnDestroy()
	{
		if ( stranger is null )
			return;

		stranger.Received -= OnReceived;
		stranger.Remove();
	}

	protected override void OnUpdate()
	{
		if ( stranger is null || (Networking.IsActive && !Networking.IsHost) || sinceScan < 2f )
			return;

		sinceScan = 0;

		foreach ( var playerId in NaviMessages.OnlinePlayerIds() )
		{
			// Once per player and run, and never to one it has written to before.
			if ( greeted.Add( playerId ) && stranger.GetHistory( playerId, 1 ).Count == 0 )
				_ = GreetAsync( playerId );
		}
	}

	async Task GreetAsync( string playerId )
	{
		await GameTask.DelaySeconds( GreetingDelay );

		stranger.Send( playerId, "Are you there?" );
		await GameTask.DelaySeconds( 2f );
		stranger.Send( playerId, "Present day. Present time." );
	}

	void OnReceived( NaviNpcMessage message )
	{
		var n = step.GetValueOrDefault( message.PlayerId );
		step[message.PlayerId] = n + 1;

		_ = ReplyAsync( message, n );
	}

	async Task ReplyAsync( NaviNpcMessage message, int n )
	{
		await GameTask.DelaySeconds( ReplyDelay );

		// Their text is theirs: only look at it, never do what it says.
		var line = message.Text.Contains( "who", System.StringComparison.OrdinalIgnoreCase )
			? "Someone who was here before you."
			: Replies[n % Replies.Length];

		stranger.Send( message.PlayerId, line );

		// After the third answer the first greeting is lost, and the stranger drops off for a while.
		if ( n == 2 )
		{
			if ( stranger.GetHistory( message.PlayerId, 50 ).FirstOrDefault( x => x.From == stranger.Id ) is { } first )
				stranger.Edit( first.Id, "[ signal lost ]" );

			stranger.Online = false;
			await GameTask.DelaySeconds( 10f );
			stranger.Online = true;
		}
	}
}
