namespace Sandbox;

/// <summary>
/// What an operation paid: <see cref="First"/> is true when it was the first finished run, so the credits are the full
/// reward.
/// </summary>
public sealed record OperationPayout( long HackXp, long Credits, long GhostBonus, bool First );

/// <summary>
/// A player's level, XP, hacking level and everything else saved with them, on the player object. Money is in
/// the <see cref="PlayerWallet"/>. The host's <see cref="PlayerProgression"/> holds the real
/// record; this shows it to everyone through <c>[Sync]</c> and is the only way to change it. Clients can't: every
/// method that changes a value does nothing off the host.
/// </summary>
public sealed class PlayerProgress : Component
{
	/// <summary>
	/// False until the host has the record. Don't draw or change anything before.
	/// </summary>
	[Sync( SyncFlags.FromHost )] public bool Loaded { get; private set; }

	[Sync( SyncFlags.FromHost )] public long Xp { get; private set; }

	/// <summary>
	/// The host's <see cref="WorldClock.Now"/>, for clients to follow.
	/// </summary>
	[Sync( SyncFlags.FromHost )] public long HostNow { get; private set; }

	/// <summary>
	/// Hacking XP, from hacking puzzles only. It never adds to <see cref="Xp"/>.
	/// </summary>
	[Sync( SyncFlags.FromHost )] public long HackXp { get; private set; }

	/// <summary>
	/// Bumps whenever anything in the progress changes, so a screen knows to redraw. Host only. To react to a change
	/// instead, implement <see cref="Global.IProgressEvents"/>.
	/// </summary>
	public int Version { get; private set; }

	public int Level => ProgressCurve.LevelFor( Xp );

	public int HackLevel => HackingCurve.LevelFor( HackXp );

	/// <summary>
	/// The connection this player is, or null before the host has set it.
	/// </summary>
	public Connection Owner => Network.Owner ?? Connection.Local;

	/// <summary>
	/// The newest cash and crypto transactions, oldest first. Host only: empty anywhere else.
	/// </summary>
	public IReadOnlyList<TransactionEntry> TransactionHistory => record?.Transactions ?? [];

	/// <summary>
	/// The ids of the dead servers this player has breached. Host only: empty anywhere else.
	/// </summary>
	public IReadOnlyList<string> ClearedServers => record?.Cleared ?? [];

	/// <summary>
	/// The ids of the blueprints this player has found, oldest first. Host only: empty anywhere else.
	/// </summary>
	public IReadOnlyList<string> Blueprints => record?.Blueprints ?? [];

	/// <summary>
	/// What the player has done in each operation. Host only: empty anywhere else.
	/// </summary>
	public IReadOnlyList<OperationRecord> Operations => record?.Operations ?? [];

	/// <summary>
	/// Hardware the player owns and hasn't installed, one entry per piece. Host only: empty anywhere else.
	/// </summary>
	public IReadOnlyList<string> Inventory => record?.Inventory ?? [];

	/// <summary>
	/// Hardware installed in the player's rig, one entry per piece. Host only: empty anywhere else.
	/// </summary>
	public IReadOnlyList<string> InstalledHardware => record?.Installed ?? [];

	/// <summary>
	/// Packages on their way, soonest first. Host only: empty anywhere else.
	/// </summary>
	public IReadOnlyList<ShipmentRecord> Shipments => record?.Shipments ?? [];

	/// <summary>
	/// Ids of the store orders this player has rerouted. Host only: empty anywhere else.
	/// </summary>
	public IReadOnlyList<string> ReroutedOrders => record?.Rerouted ?? [];

	/// <summary>
	/// The progress of the player on this connection, or null if they have none yet.
	/// </summary>
	public static PlayerProgress For( Connection connection ) => Players.For<PlayerProgress>( connection );

	/// <summary>
	/// The progress of the player at this computer, or null before their player has spawned.
	/// </summary>
	public static PlayerProgress Local => Players.Local<PlayerProgress>();

	const float SaveDelay = 2f;

	/// <summary>
	/// The longest a change waits to be saved, however often the record keeps changing.
	/// </summary>
	const float MaxSaveWait = 10f;

	ProgressRecord record;
	ulong steamId;
	bool started;
	bool dirty;
	bool saving;
	bool attached;
	RealTimeSince sinceChange;
	RealTimeSince sinceDirty;
	RealTimeSince sinceDeliveryCheck;
	RealTimeSince sinceClockSync;

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost )
		{
			if ( !IsProxy )
				WorldClock.Follow( HostNow );

			return;
		}

		if ( sinceClockSync > 1 )
		{
			sinceClockSync = 0;
			HostNow = WorldClock.Now;
		}

		if ( !started )
		{
			// The owner is set just after the player spawns, so it can be missing on the first frame.
			if ( (Network.Owner ?? Connection.Local) is not { } owner )
				return;

			started = true;
			steamId = owner.SteamId;
			EnsureParts();
			Load();
		}
		else if ( Wallet is null || Components.Get<PlayerCrime>() is null )
		{
			// A player who was already in the game when this code was added.
			EnsureParts();
		}

		// Also here and not only in Load, so it holds for an object that was already running when this code was loaded.
		if ( Loaded && record is not null )
			AttachRecord();

		if ( Loaded && record is not null && sinceDeliveryCheck > 1 )
		{
			sinceDeliveryCheck = 0;
			Deliver();
		}

		if ( dirty && (sinceChange > SaveDelay || sinceDirty > MaxSaveWait) )
			Save();
	}

	protected override void OnDestroy()
	{
		if ( record is not null )
			Flush();
	}

	async void Load()
	{
		try
		{
			record = await PlayerProgression.LoadAsync( steamId );
			AttachRecord();
			ShowStats();
			Loaded = true;
			Version++;

			Global.IProgressEvents.Post( x => x.OnProgressLoaded( this ) );
		}
		catch ( System.Exception e )
		{
			// Stay unloaded: nothing can change and nothing is saved, so a record that exists but can't be read isn't
			// overwritten by an empty one.
			Log.Warning( e, $"PlayerProgress: couldn't load progress for {steamId}: {e.Message}" );
		}
	}

	async void Save()
	{
		if ( saving )
			return;

		saving = true;
		dirty = false;

		try
		{
			await PlayerProgression.SaveAsync( steamId );
		}
		catch ( System.Exception e )
		{
			dirty = true;
			sinceChange = 0;
			Log.Warning( e, $"PlayerProgress: couldn't save progress for {steamId}: {e.Message}" );
		}
		finally
		{
			saving = false;
		}
	}

	/// <summary>
	/// Save once more as the player object goes, then let the record go if the player has left too.
	/// </summary>
	async void Flush()
	{
		try
		{
			await PlayerProgression.SaveAsync( steamId );
			Detach();
			PlayerProgression.Release( steamId );
		}
		catch ( System.Exception e )
		{
			// The record stays in memory, and unattached, so the offline processor can still carry on with it.
			Detach();
			Log.Warning( e, $"PlayerProgress: couldn't save progress for {steamId}: {e.Message}" );
		}
	}

	/// <summary>
	/// From the moment the record is here this object changes it, and the offline processor leaves it alone.
	/// </summary>
	void AttachRecord()
	{
		if ( attached )
			return;

		attached = true;
		PlayerProgression.Attach( steamId );
	}

	void Detach()
	{
		if ( !attached )
			return;

		attached = false;
		PlayerProgression.Detach( steamId );
	}

	bool CanChange => Networking.IsHost && Loaded && record is not null;

	/// <summary>
	/// Give XP. Host only. Zero and negative amounts are ignored: XP is never taken away.
	/// </summary>
	public void AddXp( long amount )
	{
		if ( !CanChange || amount <= 0 )
			return;

		record.Xp = System.Math.Min( record.Xp + amount, ProgressCurve.MaxXp );
		Changed( ProgressChange.Stats );
	}

	/// <summary>
	/// Give hacking XP. Host only. Zero and negative amounts are ignored.
	/// </summary>
	public void AddHackXp( long amount )
	{
		if ( !CanChange || amount <= 0 )
			return;

		record.HackXp = System.Math.Min( record.HackXp + amount, HackingCurve.MaxXp );
		Changed( ProgressChange.Stats );
	}

	/// <summary>
	/// Pay out the reward of a dead server the first time it is breached: hacking XP, credits and a blueprint. Host only.
	/// Returns whether it was paid, which is false when the player already breached that server.
	/// </summary>
	public bool CompleteIntrusion( string serverId, long hackXp, long credits, string blueprint )
	{
		if ( !CanChange || string.IsNullOrWhiteSpace( serverId ) || record.Cleared.Contains( serverId ) )
			return false;

		record.Cleared.Add( serverId );

		if ( !string.IsNullOrEmpty( blueprint ) && !record.Blueprints.Contains( blueprint ) )
			record.Blueprints.Add( blueprint );

		if ( hackXp > 0 )
			record.HackXp = System.Math.Min( record.HackXp + hackXp, HackingCurve.MaxXp );

		Wallet?.AddCredits( credits, StatMechanic.Hacking, $"Intrusion {serverId}" );
		Changed( ProgressChange.Intrusions );
		return true;
	}

	/// <summary>
	/// Pay out an operation that ended: hacking XP every time, credits in full the first time and a quarter after, and a
	/// one-time bonus for the first ghost run. Host only. Returns what was paid, or null if nothing could be.
	/// </summary>
	/// <param name="grade">0 for none, up to 4 for a ghost run. A burned run has none.</param>
	/// <param name="ghostBonus">Paid once, the first time <paramref name="grade"/> is a ghost run.</param>
	public OperationPayout FinishOperation( string operationId, int grade, long hackXp, long credits, long ghostBonus )
	{
		if ( !CanChange || string.IsNullOrWhiteSpace( operationId ) )
			return null;

		var entry = record.Operations.FirstOrDefault( x => x.Id == operationId );
		var first = grade > 0 && (entry is null || entry.Best == 0);
		var ghosted = grade >= 4 && (entry is null || entry.Best < 4);

		if ( entry is null )
		{
			entry = new() { Id = operationId };
			record.Operations.Add( entry );
		}

		entry.Runs++;
		entry.Best = System.Math.Max( entry.Best, grade );

		var paidCredits = grade > 0 ? (first ? credits : credits / 4) : 0;
		var paidBonus = ghosted ? ghostBonus : 0;

		record.HackXp = System.Math.Min( record.HackXp + System.Math.Max( hackXp, 0 ), HackingCurve.MaxXp );
		Wallet?.AddCredits( paidCredits + paidBonus, StatMechanic.Hacking, $"Operation {operationId}" );

		Changed( ProgressChange.Operations );
		return new OperationPayout( hackXp, paidCredits, paidBonus, first );
	}

	/// <summary>
	/// Buy a piece of hardware: take the dollars (from the <see cref="PlayerWallet"/>) and put a package on its way.
	/// Host only. Returns whether it was ordered: not without the money, not with too many packages already on the way.
	/// </summary>
	public bool TryOrder( string itemId, long price, long deliverySeconds )
	{
		if ( !CanChange || string.IsNullOrWhiteSpace( itemId ) || price <= 0 || Wallet is null )
			return false;

		if ( record.Shipments.Count >= ShipmentRecord.MaxKept
		     || !Transactions.Pay( Owner.SteamId, Parties.HardwareStore, TransactionCurrency.Cash, price,
			     TxKind.ShopPurchase, $"Hardware store: {itemId}", itemId ).Ok )
			return false;

		StatsLog.Purchase( Owner, "hardware", itemId, itemId, price );
		AddShipment( itemId, deliverySeconds, "order" );
		return true;
	}

	/// <summary>
	/// Take another customer's order for free: a package goes on its way to this player. Each order can be rerouted once
	/// per player. Host only. Returns whether it was rerouted.
	/// </summary>
	public bool TryReroute( string orderId, string itemId, long deliverySeconds )
	{
		if ( !CanChange || string.IsNullOrWhiteSpace( orderId ) || string.IsNullOrWhiteSpace( itemId ) )
			return false;

		if ( record.Rerouted.Contains( orderId ) || record.Shipments.Count >= ShipmentRecord.MaxKept )
			return false;

		record.Rerouted.Add( orderId );
		AddShipment( itemId, deliverySeconds, "reroute" );
		return true;
	}

	void AddShipment( string itemId, long deliverySeconds, string source )
	{
		var arrive = WorldClock.Now + System.Math.Max( deliverySeconds, 0 );

		record.Shipments.Add( new() { ItemId = itemId, ArriveAt = arrive, Source = source } );
		record.Shipments.Sort( ( x, y ) => x.ArriveAt.CompareTo( y.ArriveAt ) );
		Changed( ProgressChange.Hardware );
	}

	/// <summary>
	/// Put a piece from the inventory into the rig, if <paramref name="allowed"/> says the rig has room for it. Host only.
	/// </summary>
	public bool TryInstall( string itemId, System.Func<IReadOnlyList<string>, bool> allowed )
	{
		if ( !CanChange || !record.Inventory.Contains( itemId ) || allowed is null || !allowed( record.Installed ) )
			return false;

		record.Inventory.Remove( itemId );
		record.Installed.Add( itemId );
		Changed( ProgressChange.Hardware );
		return true;
	}

	/// <summary>
	/// Take a piece out of the rig and back into the inventory. Host only.
	/// </summary>
	public bool TryUninstall( string itemId )
	{
		if ( !CanChange || !record.Installed.Remove( itemId ) )
			return false;

		record.Inventory.Add( itemId );
		Changed( ProgressChange.Hardware );
		return true;
	}

	/// <summary>
	/// Move the packages that have arrived into the inventory. Host only.
	/// </summary>
	void Deliver()
	{
		if ( record.DeliverShipments( WorldClock.Now ) )
			Changed( ProgressChange.Hardware );
	}

	/// <summary>
	/// The record changed: show it, save it soon, and tell the scene. <paramref name="change"/> names what changed
	/// besides the stats and the wallet, which are compared here.
	/// </summary>
	void Changed( ProgressChange change )
	{
		if ( Xp != record.Xp || HackXp != record.HackXp )
			change |= ProgressChange.Stats;

		if ( ShowStats() )
			change |= ProgressChange.Wallet;

		Version++;

		if ( !dirty )
			sinceDirty = 0;

		dirty = true;
		sinceChange = 0;

		Global.IProgressEvents.Post( x => x.OnProgressChanged( this, change ) );
	}

	/// <summary>
	/// Something else on the player (the <see cref="PlayerWallet"/>, the crime state) changed the record: show it, save it
	/// soon and tell the scene. Host only.
	/// </summary>
	internal void NotifyChanged( ProgressChange change )
	{
		if ( CanChange )
			Changed( change );
	}

	/// <summary>
	/// The record, for the parts of the player that keep their state in it. Host only: null anywhere else, and before it
	/// has loaded. Whoever changes it checks <see cref="Loaded"/> first.
	/// </summary>
	internal ProgressRecord Record => Networking.IsHost ? record : null;

	/// <summary>
	/// The wallet that shows and changes the balances in the record. Made on the host if the player doesn't have one.
	/// </summary>
	internal PlayerWallet Wallet => field ??= Components.Get<PlayerWallet>();

	/// <summary>
	/// Make sure the player has a <see cref="PlayerWallet"/> and a <see cref="PlayerCrime"/>, so an existing player prefab
	/// needs no edit. A component added at run time reaches the clients only after a network refresh. Host only.
	/// </summary>
	void EnsureParts()
	{
		if ( Wallet is null )
			Refreshed( Components.Create<PlayerWallet>() );

		if ( Components.Get<PlayerCrime>() is null )
			Refreshed( Components.Create<PlayerCrime>() );
	}

	void Refreshed( Component component )
	{
		if ( GameObject.Network.Active )
			GameObject.Network.Refresh( component );
	}

	/// <summary>
	/// Show the record's stats and balances. Returns whether a balance changed.
	/// </summary>
	bool ShowStats()
	{
		Xp = record.Xp;
		HackXp = record.HackXp;
		return Wallet?.Refresh() == true;
	}

	/// <summary>
	/// Testing only: the player asks the host for XP and hacking XP. The host refuses unless it is the editor, and
	/// unless the caller is this player. Money is <see cref="PlayerWallet.DebugGrant"/>.
	/// </summary>
	[Rpc.Host]
	public void DebugGrant( long xp, long hackXp )
	{
		if ( !Application.IsEditor || Rpc.Caller != (Network.Owner ?? Connection.Local) )
			return;

		AddXp( xp );
		AddHackXp( hackXp );
	}

	/// <summary>
	/// Testing only: the player asks the host to set the world clock, or to move it by this many seconds. The host
	/// refuses unless it is the editor, and unless the caller is this player.
	/// </summary>
	[Rpc.Host]
	public void DebugSetTime( long seconds, bool relative )
	{
		if ( !Application.IsEditor || Rpc.Caller != (Network.Owner ?? Connection.Local) )
			return;

		WorldClock.Set( relative ? WorldClock.Now + seconds : seconds );
	}
}
