namespace Sandbox;

/// <summary>
/// Everything a player owns in money, on the player object: <see cref="Cash"/> (dollars, the real world),
/// <see cref="Credits"/> (the Wired) and <see cref="Crypto"/> (the dark web). It is the only way to read a balance
/// and the only way to change one. See <c>docs/currency.md</c>.
/// <para>
/// The numbers are saved in the player's <see cref="ProgressRecord"/> (<c>Money</c>, <c>Credits</c>, <c>Crypto</c>),
/// which <see cref="PlayerProgress"/> loads and saves on the host; the wallet reads and writes that record and shows the
/// result to everyone through <c>[Sync]</c>. Every method that changes a balance does nothing off the host.
/// </para>
/// </summary>
/// <remarks>
/// Rules: credits never turn into anything else, so there is no method that takes credits and gives another currency.
/// Crypto and cash change into each other only through <see cref="Exchange"/>. <see cref="PlayerProgress"/> creates this
/// component on the host if the player prefab doesn't have it, and refreshes the network object so clients get it.
/// </remarks>
public sealed class PlayerWallet : Component
{
	/// <summary>
	/// Dollars, for the real world.
	/// </summary>
	[Sync( SyncFlags.FromHost ), Change( nameof( OnBalanceChanged ) )]
	public long Cash { get; private set; }

	/// <summary>
	/// Credits, for the Wired. They never convert to anything.
	/// </summary>
	[Sync( SyncFlags.FromHost ), Change( nameof( OnBalanceChanged ) )]
	public long Credits { get; private set; }

	/// <summary>
	/// Crypto, for the dark web. It changes to cash and back only through <see cref="Exchange"/>.
	/// </summary>
	[Sync( SyncFlags.FromHost ), Change( nameof( OnBalanceChanged ) )]
	public long Crypto { get; private set; }

	/// <summary>
	/// Bumps whenever a balance changes, on every machine, so a panel knows to redraw. To react on the host instead,
	/// implement <see cref="Global.IProgressEvents"/> (<see cref="ProgressChange.Wallet"/>).
	/// </summary>
	public int Version { get; private set; }

	/// <summary>
	/// The wallet of the player on this computer, or null before it has arrived.
	/// </summary>
	public static PlayerWallet Local => Players.Local<PlayerWallet>();

	/// <summary>
	/// The wallet of the player on this connection, or null.
	/// </summary>
	public static PlayerWallet For( Connection connection ) => Players.For<PlayerWallet>( connection );

	PlayerProgress Progress => field ??= Components.Get<PlayerProgress>();

	ProgressRecord Record => Progress?.Record;

	bool CanChange => Networking.IsHost && Progress is { Loaded: true } && Record is not null;

	void OnBalanceChanged( long oldValue, long newValue ) => Version++;

	/// <summary>
	/// Copy the record's balances into the synced values. Host only. Returns whether any changed.
	/// </summary>
	internal bool Refresh()
	{
		if ( Record is not { } record )
			return false;

		var changed = Cash != record.Money || Credits != record.Credits || Crypto != record.Crypto;

		Cash = record.Money;
		Credits = record.Credits;
		Crypto = record.Crypto;

		if ( changed )
			Version++;

		return changed;
	}

	/// <summary>
	/// A balance changed: show it, save it soon and tell the scene.
	/// </summary>
	void Changed() => Progress?.NotifyChanged( ProgressChange.Wallet );

	/// <summary>
	/// Whether the player can hold this many more dollars before the cap.
	/// </summary>
	public bool CanHoldCash( long amount ) => Record?.CanHoldCash( amount ) ?? Cash + amount <= ProgressRecord.MaxMoney;

	/// <summary>
	/// Whether the player can hold this much more crypto before the cap.
	/// </summary>
	public bool CanHoldCrypto( long amount ) =>
		Record?.CanHoldCrypto( amount ) ?? Crypto + amount <= ProgressRecord.MaxCrypto;

	/// <summary>
	/// Give dollars. Host only. Zero and negative amounts are ignored.
	/// </summary>
	public void AddCash( long amount )
	{
		if ( !CanChange || amount <= 0 )
			return;

		Record.AddCash( amount );
		Changed();
	}

	/// <summary>
	/// Take dollars if there are enough. Host only. Returns whether they were taken.
	/// </summary>
	public bool TrySpendCash( long amount )
	{
		if ( !CanChange || amount <= 0 || Record.Money < amount )
			return false;

		Record.Money -= amount;
		Changed();
		return true;
	}

	/// <summary>
	/// Give credits. Host only.
	/// </summary>
	public void AddCredits( long amount )
	{
		if ( !CanChange || amount <= 0 )
			return;

		Record.AddCredits( amount );
		Changed();
	}

	/// <summary>
	/// Take credits if there are enough. Host only. Returns whether they were taken.
	/// </summary>
	public bool TrySpendCredits( long amount )
	{
		if ( !CanChange || amount <= 0 || Record.Credits < amount )
			return false;

		Record.Credits -= amount;
		Changed();
		return true;
	}

	/// <summary>
	/// Give crypto. Host only.
	/// </summary>
	public void AddCrypto( long amount )
	{
		if ( !CanChange || amount <= 0 )
			return;

		Record.AddCrypto( amount );
		Changed();
	}

	/// <summary>
	/// Take crypto if there are enough. Host only. Returns whether it was taken.
	/// </summary>
	public bool TrySpendCrypto( long amount )
	{
		if ( !CanChange || amount <= 0 || Record.Crypto < amount )
			return false;

		Record.Crypto -= amount;
		Changed();
		return true;
	}

	/// <summary>
	/// Move dollars from this player to another, and write it in both payment histories (the Payments app). Host only.
	/// Returns whether it was moved: not to oneself, not more than there is, not past the most the other can hold.
	/// </summary>
	public bool TryPayCash( PlayerWallet to, long amount )
	{
		if ( !CanChange || to is null || to == this || !to.CanChange || amount <= 0 )
			return false;

		if ( Record.Money < amount || to.Record.Money + amount > ProgressRecord.MaxMoney )
			return false;

		var at = WorldClock.Now;
		var from = Progress.Owner;
		var target = to.Progress.Owner;

		Record.Money -= amount;
		to.Record.Money += amount;

		Progress.AddPayment( new() { At = at, PeerId = target.SteamId.ToString(), PeerName = target.DisplayName, Amount = -amount } );
		to.Progress.AddPayment( new() { At = at, PeerId = from.SteamId.ToString(), PeerName = from.DisplayName, Amount = amount } );
		Changed();
		to.Changed();
		return true;
	}

	/// <summary>
	/// Move crypto from this player to another, and write it in both payment histories (the Payments app). Host only.
	/// Returns whether it was moved: not to oneself, not more than there is, not past the most the other can hold.
	/// </summary>
	public bool TryPayCrypto( PlayerWallet to, long amount )
	{
		if ( !CanChange || to is null || to == this || !to.CanChange || amount <= 0 )
			return false;

		if ( Record.Crypto < amount || to.Record.Crypto + amount > ProgressRecord.MaxCrypto )
			return false;

		var at = WorldClock.Now;
		var from = Progress.Owner;
		var target = to.Progress.Owner;

		Record.Crypto -= amount;
		to.Record.Crypto += amount;

		Progress.AddPayment( new()
		{
			At = at, PeerId = target.SteamId.ToString(), PeerName = target.DisplayName, Amount = -amount,
			Currency = PaymentCurrency.Crypto,
		} );

		to.Progress.AddPayment( new()
		{
			At = at, PeerId = from.SteamId.ToString(), PeerName = from.DisplayName, Amount = amount,
			Currency = PaymentCurrency.Crypto,
		} );

		Changed();
		to.Changed();
		return true;
	}

	/// <summary>
	/// Testing only: the player asks the host for dollars, credits and crypto. The host refuses unless it is the editor,
	/// and unless the caller is this player.
	/// </summary>
	[Rpc.Host]
	public void DebugGrant( long cash, long credits, long crypto )
	{
		if ( !Application.IsEditor || Rpc.Caller != (Network.Owner ?? Connection.Local) )
			return;

		AddCash( cash );
		AddCredits( credits );
		AddCrypto( crypto );
	}
}
