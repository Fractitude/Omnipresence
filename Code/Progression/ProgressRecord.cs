namespace Sandbox;

/// <summary>
/// What is saved for one player. Plain data, so it goes to a file now and to a backend later. Level isn't in it: it is
/// worked out from <see cref="Xp"/> by <see cref="ProgressCurve"/>.
/// </summary>
public sealed class ProgressRecord
{
	public const int CurrentVersion = 1;
	public const long MaxMoney = 1_000_000_000_000;

	/// <summary>
	/// The shape of this record, for migrating old saves.
	/// </summary>
	public int Version { get; set; } = CurrentVersion;

	/// <summary>
	/// Goes up by one on every save. A shared backend uses it to refuse a write that started from an old copy.
	/// </summary>
	public long Revision { get; set; }

	public long Xp { get; set; }
	public long Money { get; set; }

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
		Revision = System.Math.Max( Revision, 0 );

		Payments ??= new();
		Payments.RemoveAll( x => x is null );

		if ( Payments.Count > PaymentRecord.MaxKept )
			Payments.RemoveRange( 0, Payments.Count - PaymentRecord.MaxKept );
	}
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
