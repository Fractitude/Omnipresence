namespace Sandbox;

/// <summary>
/// What is saved for one player. Plain data, so it goes to a file now and to a backend later. Level isn't in it: it is
/// worked out from <see cref="Xp"/> by <see cref="ProgressCurve"/>.
/// </summary>
public sealed class ProgressRecord
{
	public const int CurrentVersion = 1;
	public const long MaxMoney = 1_000_000_000_000;
	public const long MaxCredits = 1_000_000_000_000;

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
	/// Dollars: what the player spends in the real world.
	/// </summary>
	public long Money { get; set; }

	/// <summary>
	/// What the player spends in the Wired. Not convertible to <see cref="Money"/>.
	/// </summary>
	public long Credits { get; set; }

	/// <summary>
	/// Ids of the dead servers the player has breached. The reward of a server is paid once.
	/// </summary>
	public List<string> Cleared { get; set; } = new();

	/// <summary>
	/// Ids of the blueprints the player has found, for building at home.
	/// </summary>
	public List<string> Blueprints { get; set; } = new();

	/// <summary>
	/// Ids of the blueprints the player has built at home. Each one is built once and then works for good.
	/// </summary>
	public List<string> Built { get; set; } = new();

	/// <summary>
	/// The newest payments with other players, oldest first. See <see cref="PaymentRecord"/>.
	/// </summary>
	public List<PaymentRecord> Payments { get; set; } = new();

	/// <summary>
	/// Make a loaded record safe: a file or a backend is outside input, and can hold anything.
	/// </summary>
	public void Sanitize()
	{
		Xp = System.Math.Clamp( Xp, 0, ProgressCurve.MaxXp );
		Money = System.Math.Clamp( Money, 0, MaxMoney );
		Credits = System.Math.Clamp( Credits, 0, MaxCredits );
		Revision = System.Math.Max( Revision, 0 );

		Cleared = CleanIds( Cleared );
		Blueprints = CleanIds( Blueprints );
		Built = CleanIds( Built );

		Payments ??= new();
		Payments.RemoveAll( x => x is null );

		if ( Payments.Count > PaymentRecord.MaxKept )
			Payments.RemoveRange( 0, Payments.Count - PaymentRecord.MaxKept );
	}

	const int MaxIds = 500;
	const int MaxIdLength = 64;

	static List<string> CleanIds( List<string> ids ) =>
		(ids ?? [])
			.Where( x => !string.IsNullOrWhiteSpace( x ) && x.Length <= MaxIdLength )
			.Distinct()
			.Take( MaxIds )
			.ToList();
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
