namespace Sandbox;

/// <summary>
/// Everything a player owns in money, on the player object: <see cref="Cash"/> (dollars, the real world),
/// <see cref="Credits"/> (the Wired) and <see cref="Crypto"/> (the dark web). It is the only way to read a balance
/// and the only way to change a credit. Cash and crypto change only through <see cref="Transactions"/>. See
/// <c>docs/currency.md</c>.
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

	/// <summary>
	/// The Steam id of the player this wallet belongs to, or 0 before the host knows. Host only.
	/// </summary>
	public ulong SteamId => Progress?.Owner is { } owner ? (ulong)owner.SteamId : 0UL;

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
	/// Give credits. Host only.
	/// </summary>
	public void AddCredits( long amount, string mechanic = null, string detail = null )
	{
		if ( !CanChange || amount <= 0 )
			return;

		var before = Record.Credits;
		Record.AddCredits( amount );
		Logged( StatsLog.MoneyCredits, Record.Credits - before, Record.Credits, mechanic, detail );
		Changed();
	}

	/// <summary>
	/// Take credits if there are enough. Host only. Returns whether they were taken.
	/// </summary>
	public bool TrySpendCredits( long amount, string mechanic = null, string detail = null )
	{
		if ( !CanChange || amount <= 0 || Record.Credits < amount )
			return false;

		Record.Credits -= amount;
		Logged( StatsLog.MoneyCredits, -amount, Record.Credits, mechanic, detail );
		Changed();
		return true;
	}

	/// <summary>
	/// Write a balance change in the stats log (every wallet method that changes a balance ends here).
	/// </summary>
	void Logged( string currency, long delta, long balance, string mechanic, string detail, Connection other = null ) =>
		StatsLog.Money( Progress.Owner, currency, delta, balance, mechanic ?? "unknown", detail, other );

	/// <summary>
	/// Why a balance changed outside <see cref="Transactions"/>. These two are the only ones allowed: money that is not
	/// moved between parties but appears.
	/// </summary>
	public enum UntrackedReason
	{
		/// <summary>Money lying in the world that the player picked up. No such item exists yet.</summary>
		GroundPickup,

		/// <summary>A test command (<c>progress_give_money</c> and its kin).</summary>
		ConsoleCommand,
	}

	/// <summary>
	/// Give cash or crypto without a transaction: picking money up from the ground, or a console command. It is the
	/// only way a balance goes up outside <see cref="Transactions"/>, so it is named for what it skips: no history
	/// entry, no ledger line. The stats log still hears of it. Host only; zero and negative amounts are ignored, and
	/// the cap holds.
	/// </summary>
	public void GrantUntracked( TransactionCurrency currency, long amount, UntrackedReason reason )
	{
		if ( !CanChange || amount <= 0 )
			return;

		var before = TransactionRules.BalanceOf( Record, currency );
		Record.Settle( currency, amount );
		var after = TransactionRules.BalanceOf( Record, currency );

		var unit = currency == TransactionCurrency.Crypto ? StatsLog.MoneyCrypto : StatsLog.MoneyCash;

		Logged( unit, after - before, after,
			reason == UntrackedReason.GroundPickup ? StatMechanic.GroundPickup : StatMechanic.ConsoleCommand, null );

		Changed();
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

		GrantUntracked( TransactionCurrency.Cash, cash, UntrackedReason.ConsoleCommand );
		AddCredits( credits, StatMechanic.ConsoleCommand );
		GrantUntracked( TransactionCurrency.Crypto, crypto, UntrackedReason.ConsoleCommand );
	}
}
