namespace Sandbox;

/// <summary>
/// What is saved for one player. Plain data, so it goes to a file now and to a backend later. Level isn't in it: it is
/// worked out from <see cref="Xp"/> by <see cref="ProgressCurve"/>.
/// </summary>
public sealed class ProgressRecord
{
	public const int CurrentVersion = 3;
	public const long MaxMoney = 1_000_000_000_000;
	public const long MaxCredits = 1_000_000_000_000;
	public const long MaxCrypto = 1_000_000_000_000;

	/// <summary>
	/// The shape of this record, for migrating old saves.
	/// </summary>
	public int Version { get; set; } = CurrentVersion;

	/// <summary>
	/// Goes up by one on every save. A shared backend uses it to refuse a write that started from an old copy.
	/// </summary>
	public long Revision { get; set; }

	public long Xp { get; set; }

	/// <summary>
	/// Hacking XP, from every hacking puzzle. Hacking level is worked out from it by <see cref="HackingCurve"/>.
	/// </summary>
	public long HackXp { get; set; }

	/// <summary>
	/// What the player has done in each operation (the multi-stage break-ins).
	/// </summary>
	public List<OperationRecord> Operations { get; set; } = new();

	/// <summary>
	/// Dollars: what the player spends in the real world.
	/// </summary>
	public long Money { get; set; }

	/// <summary>
	/// What the player spends in the Wired. Not convertible to <see cref="Money"/>.
	/// </summary>
	public long Credits { get; set; }

	/// <summary>
	/// What the player spends on the dark web. Changes to <see cref="Money"/> and back only through the Exchange. Added in
	/// version 3: older saves have none.
	/// </summary>
	public long Crypto { get; set; }

	/// <summary>
	/// Ids of the dead servers the player has breached. The reward of a server is paid once.
	/// </summary>
	public List<string> Cleared { get; set; } = new();

	/// <summary>
	/// Ids of the blueprints the player has found, for building at home.
	/// </summary>
	public List<string> Blueprints { get; set; } = new();

	/// <summary>
	/// Old: what the workbench used to build. Only read, to move it into <see cref="Installed"/> when a record loads.
	/// </summary>
	public List<string> Built { get; set; } = new();

	/// <summary>
	/// Hardware the player owns and hasn't installed, one entry per piece (two RAM sticks are two entries).
	/// </summary>
	public List<string> Inventory { get; set; } = new();

	/// <summary>
	/// Hardware installed in the player's rig, one entry per piece. It changes how the Wired plays.
	/// </summary>
	public List<string> Installed { get; set; } = new();

	/// <summary>
	/// Packages on their way to the player's home.
	/// </summary>
	public List<ShipmentRecord> Shipments { get; set; } = new();

	/// <summary>
	/// Ids of the store orders the player has rerouted to their own home. Each can be rerouted once.
	/// </summary>
	public List<string> Rerouted { get; set; } = new();

	/// <summary>
	/// The newest payments with other players, oldest first. See <see cref="PaymentRecord"/>.
	/// </summary>
	public List<PaymentRecord> Payments { get; set; } = new();

	/// <summary>
	/// The crime game's state. Kept in its own section so a season wipe can reset it alone. Added in version 3.
	/// </summary>
	public CrimeRecord Crime { get; set; } = new();

	/// <summary>
	/// Make a loaded record safe: a file or a backend is outside input, and can hold anything.
	/// </summary>
	public void Sanitize()
	{
		Xp = System.Math.Clamp( Xp, 0, ProgressCurve.MaxXp );
		HackXp = System.Math.Clamp( HackXp, 0, HackingCurve.MaxXp );
		Money = System.Math.Clamp( Money, 0, MaxMoney );
		Credits = System.Math.Clamp( Credits, 0, MaxCredits );
		Crypto = System.Math.Clamp( Crypto, 0, MaxCrypto );
		Revision = System.Math.Max( Revision, 0 );

		Cleared = CleanIds( Cleared );
		Blueprints = CleanIds( Blueprints );
		Built = CleanIds( Built );
		Rerouted = CleanIds( Rerouted );

		Inventory = CleanPieces( Inventory );
		Installed = CleanPieces( Installed );

		// Hardware the workbench built before it only installed.
		foreach ( var id in Built )
			Installed.Add( id );

		Built.Clear();

		Shipments ??= new();
		Shipments.RemoveAll( x => x is null || string.IsNullOrWhiteSpace( x.ItemId ) );

		if ( Shipments.Count > ShipmentRecord.MaxKept )
			Shipments.RemoveRange( 0, Shipments.Count - ShipmentRecord.MaxKept );

		Payments ??= new();
		Payments.RemoveAll( x => x is null );

		Operations ??= new();
		Operations.RemoveAll( x => x is null || string.IsNullOrWhiteSpace( x.Id ) || x.Id.Length > MaxIdLength );
		Operations = Operations.GroupBy( x => x.Id ).Select( x => x.First() ).Take( MaxIds ).ToList();

		foreach ( var operation in Operations )
		{
			operation.Runs = System.Math.Max( operation.Runs, 0 );
			operation.Best = System.Math.Clamp( operation.Best, 0, 4 );
		}

		// Shipments from before the game clock land now. Old payment times are no longer dates. Version 1 saves also
		// hold times from when the clock still worked differently, so every saved time is reset once.
		var resetTimes = Version < 2;

		foreach ( var shipment in Shipments.Where( x => resetTimes || x.ArriveAt > WorldClock.LegacyLimit ) )
			shipment.ArriveAt = 0;

		foreach ( var payment in Payments.Where( x => resetTimes || x.At > WorldClock.LegacyLimit ) )
			payment.At = 0;

		Crime ??= new();
		Crime.Sanitize();

		Version = CurrentVersion;

		if ( Payments.Count > PaymentRecord.MaxKept )
			Payments.RemoveRange( 0, Payments.Count - PaymentRecord.MaxKept );
	}

	public const int MaxPieces = 500;
	const int MaxIds = 500;
	const int MaxIdLength = 64;

	/// <summary>
	/// Like <see cref="CleanIds"/> but a piece can be there more than once.
	/// </summary>
	static List<string> CleanPieces( List<string> ids ) =>
		(ids ?? [])
			.Where( x => !string.IsNullOrWhiteSpace( x ) && x.Length <= MaxIdLength )
			.Take( MaxPieces )
			.ToList();

	static List<string> CleanIds( List<string> ids ) =>
		(ids ?? [])
			.Where( x => !string.IsNullOrWhiteSpace( x ) && x.Length <= MaxIdLength )
			.Distinct()
			.Take( MaxIds )
			.ToList();
}

/// <summary>
/// What a player has done in one operation. Plain data, saved in the <see cref="ProgressRecord"/>.
/// </summary>
public sealed class OperationRecord
{
	public string Id { get; set; } = "";

	/// <summary>
	/// How many times it was finished, burned or not.
	/// </summary>
	public int Runs { get; set; }

	/// <summary>
	/// The best grade, 0 for none yet, up to 4 for a ghost run.
	/// </summary>
	public int Best { get; set; }
}

/// <summary>
/// One payment, as the player it was for sees it. Plain data, saved in the <see cref="ProgressRecord"/>.
/// </summary>
public sealed class PaymentRecord
{
	/// <summary>
	/// The most a player keeps. Older ones make room.
	/// </summary>
	public const int MaxKept = 100;

	/// <summary>
	/// Unix seconds.
	/// </summary>
	public long At { get; set; }

	/// <summary>
	/// The other player's Steam id and their name when it happened.
	/// </summary>
	public string PeerId { get; set; } = "";
	public string PeerName { get; set; } = "";

	/// <summary>
	/// Positive when the player received it, negative when they sent it.
	/// </summary>
	public long Amount { get; set; }
}

/// <summary>
/// One package on its way to the player's home. Plain data, saved in the <see cref="ProgressRecord"/>.
/// </summary>
public sealed class ShipmentRecord
{
	/// <summary>
	/// The most a player has on the way at once.
	/// </summary>
	public const int MaxKept = 50;

	public string ItemId { get; set; } = "";

	/// <summary>
	/// Unix seconds when it arrives and goes into the inventory.
	/// </summary>
	public long ArriveAt { get; set; }

	/// <summary>
	/// "order" for something bought, "reroute" for something taken from another customer's order.
	/// </summary>
	public string Source { get; set; } = "order";
}
