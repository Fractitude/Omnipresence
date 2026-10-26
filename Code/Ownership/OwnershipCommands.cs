namespace Sandbox;

/// <summary>
/// Console commands for ownership: what you look at, whose it is, and giving it away. Editor only, on the host, like
/// the other dev commands. <c>ownership_give</c> and <c>ownership_claim</c> work on the object in the crosshair within
/// <see cref="Reach"/> units; the rest take a key, an owner or neither.
/// </summary>
public static class OwnershipCommands
{
	/// <summary>How far the crosshair reaches for the commands that work on what you look at.</summary>
	const float Reach = 400f;

	static bool Allowed() => Application.IsEditor && Networking.IsHost;

	/// <summary>The ownership of the object in the crosshair, or null.</summary>
	static OwnershipComponent Looking()
	{
		var player = Players.LocalObject;
		var scene = Game.ActiveScene;

		if ( !player.IsValid() || !scene.IsValid() || player.Components.Get<Player>() is not { } body )
			return null;

		var eye = body.EyeTransform;
		var trace = scene.Trace.Ray( eye.Position, eye.Position + eye.Forward * Reach )
			.IgnoreGameObjectHierarchy( player )
			.Run();

		return trace.Hit ? OwnershipComponent.On( trace.GameObject ) : null;
	}

	/// <summary>
	/// <c>ownership_give &lt;owner&gt;</c> gives what you look at to that owner: <c>player:76561197960287930</c> (or
	/// the bare id), <c>org:a1b2c3d4e5f6</c>, <c>public</c>, <c>government</c>, or <c>nobody</c> to release it. On the
	/// object itself this is a claim; use <see cref="OwnershipComponent.Transfer"/> in game code for anything a player
	/// may not do.
	/// </summary>
	[ConCmd( "ownership_give" )]
	public static void Give( string owner = "nobody" )
	{
		if ( !Allowed() )
			return;

		if ( Looking() is not { } thing )
		{
			Log.Info( "Look at an object with an Ownership component" );
			return;
		}

		var to = ParseOwner( owner );

		if ( OwnershipRules.Check( to ) is not null )
		{
			Log.Info( $"{OwnershipRules.Check( to )} Owners: player:<id>, org:<id>, public, government, nobody" );
			return;
		}

		var by = (ulong)Connection.Local.SteamId;
		Log.Info( OwnershipRegistry.Transfer( thing.Key, to, by, "console" )
			? $"{thing.Key} is now {to.DisplayName}'s"
			: $"{thing.Key} is already {thing.Owner.DisplayName}'s" );
	}

	/// <summary><c>ownership_claim</c> claims the object in the crosshair for yourself, if nobody owns it.</summary>
	[ConCmd( "ownership_claim" )]
	public static void Claim()
	{
		if ( !Allowed() )
			return;

		if ( Looking() is not { } thing )
		{
			Log.Info( "Look at an object with an Ownership component" );
			return;
		}

		var said = thing.Claim( Connection.Local.SteamId ) ? "is yours"
			: thing.Owner.IsNobody ? "could not be written down"
			: $"is {thing.Owner.DisplayName}'s";

		Log.Info( $"{thing.Key} {said}" );
	}

	/// <summary>
	/// <c>ownership_show</c> logs what you look at: its key, its owner, since when, and the last changes of hands.
	/// <c>ownership_show &lt;key&gt;</c> does the same for a key, whether or not the thing is in the world.
	/// </summary>
	[ConCmd( "ownership_show" )]
	public static void Show( string key = "" )
	{
		if ( !Allowed() )
			return;

		var thingKey = string.IsNullOrWhiteSpace( key ) ? Looking()?.Key : key;

		if ( string.IsNullOrWhiteSpace( thingKey ) )
		{
			Log.Info( "Look at an object with an Ownership component, or give a key" );
			return;
		}

		var record = OwnershipRegistry.Find( thingKey );
		var owner = Ownership.Parse( record?.Owner );

		if ( record is null )
		{
			Log.Info( $"{thingKey}: no owner" );
			return;
		}

		Log.Info( $"{record.Id}: {owner.DisplayName} ({owner.Key}) since {When( record.Since )}" );

		foreach ( var change in record.History )
		{
			Log.Info( $"  {When( change.At )}: {Describe( change.From )} to {Describe( change.To )}, "
			          + $"by {change.By}, {change.Reason}" );
		}
	}

	/// <summary>
	/// <c>ownership_list &lt;owner&gt;</c> logs everything an owner holds, one line per thing:
	/// <c>ownership_list player:76561197960287930</c>, <c>ownership_list org:a1b2c3d4e5f6</c>,
	/// <c>ownership_list public</c>.
	/// </summary>
	[ConCmd( "ownership_list" )]
	public static void List( string owner )
	{
		if ( !Allowed() )
			return;

		var who = ParseOwner( owner );

		if ( !who.IsOwned )
		{
			Log.Info( "Owners: player:<id>, org:<id>, public, government" );
			return;
		}

		var records = OwnershipRegistry.RecordsOf( who );
		Log.Info( $"{who.DisplayName} owns {records.Count} things" );

		foreach ( var record in records )
			Log.Info( $"  {record.Id} since {When( record.Since )}" );
	}

	/// <summary><c>ownership_clear</c> forgets every title and clears the copies on the objects in the scene.</summary>
	[ConCmd( "ownership_clear" )]
	public static void Clear()
	{
		if ( !Allowed() )
			return;

		Log.Info( $"Cleared {OwnershipRegistry.Wipe()} titles from {OwnershipRegistry.FileName}" );
	}

	/// <summary>
	/// What an argument names: a key, a bare Steam id, or one of the words. Anything else is nobody.
	/// </summary>
	static Ownership ParseOwner( string owner )
	{
		if ( string.IsNullOrWhiteSpace( owner ) )
			return Ownership.Nobody;

		var text = owner.Trim();

		return ulong.TryParse( text, out var steamId ) ? Ownership.Player( steamId ) : Ownership.Parse( text );
	}

	static string Describe( string key ) => Ownership.Parse( key ).IsOwned ? Ownership.Parse( key ).Key : "nobody";

	static string When( long unix ) => unix == 0
		? "the beginning"
		: System.DateTimeOffset.FromUnixTimeSeconds( unix ).LocalDateTime.ToString( "yyyy-MM-dd HH:mm" );
}
