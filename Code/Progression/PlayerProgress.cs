namespace Sandbox;

/// <summary>
/// A player's level, XP, dollars and credits, on the player object. The host's <see cref="PlayerProgression"/> holds the real
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
	/// Dollars, for the real world.
	/// </summary>
	[Sync( SyncFlags.FromHost )] public long Money { get; private set; }

	/// <summary>
	/// Credits, for the Wired.
	/// </summary>
	[Sync( SyncFlags.FromHost )] public long Credits { get; private set; }

	public int Level => ProgressCurve.LevelFor( Xp );

	/// <summary>
	/// The connection this player is, or null before the host has set it.
	/// </summary>
	public Connection Owner => Network.Owner ?? Connection.Local;

	/// <summary>
	/// The newest payments, oldest first. Host only: empty anywhere else.
	/// </summary>
	public IReadOnlyList<PaymentRecord> Payments => record?.Payments ?? [];

	/// <summary>
	/// Bumps whenever a payment is added, so a screen knows to redraw. Host only.
	/// </summary>
	public int PaymentVersion { get; private set; }

	/// <summary>
	/// The ids of the dead servers this player has breached. Host only: empty anywhere else.
	/// </summary>
	public IReadOnlyList<string> ClearedServers => record?.Cleared ?? [];

	/// <summary>
	/// The ids of the blueprints this player has found, oldest first. Host only: empty anywhere else.
	/// </summary>
	public IReadOnlyList<string> Blueprints => record?.Blueprints ?? [];

	/// <summary>
	/// Bumps whenever a server is breached, so a screen knows to redraw. Host only.
	/// </summary>
	public int IntrusionVersion { get; private set; }

	/// <summary>
	/// The ids of the blueprints this player has built at home. Host only: empty anywhere else.
	/// </summary>
	public IReadOnlyList<string> Built => record?.Built ?? [];

	/// <summary>
	/// Bumps whenever something is built, so a screen knows to redraw. Host only.
	/// </summary>
	public int BuildVersion { get; private set; }

	/// <summary>
	/// The progress of the player on this connection, or null if they have none yet.
	/// </summary>
	public static PlayerProgress For( Connection connection ) =>
		connection is null ? null : Game.ActiveScene?.GetAll<PlayerProgress>().FirstOrDefault( x => x.Owner == connection );

	/// <summary>
	/// The progress of the player at this computer, or null before their player has spawned.
	/// </summary>
	public static PlayerProgress Local => Game.ActiveScene?.GetAll<PlayerProgress>().FirstOrDefault( x => !x.IsProxy );

	const float SaveDelay = 2f;

	ProgressRecord record;
	ulong steamId;
	bool started;
	bool dirty;
	bool saving;
	RealTimeSince sinceChange;

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost )
			return;

		if ( !started )
		{
			// The owner is set just after the player spawns, so it can be missing on the first frame.
			if ( (Network.Owner ?? Connection.Local) is not { } owner )
				return;

			started = true;
			steamId = owner.SteamId;
			Load();
		}

		if ( dirty && sinceChange > SaveDelay )
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
			Xp = record.Xp;
			Money = record.Money;
			Credits = record.Credits;
			Loaded = true;
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
			PlayerProgression.Release( steamId );
		}
		catch ( System.Exception e )
		{
			Log.Warning( e, $"PlayerProgress: couldn't save progress for {steamId}: {e.Message}" );
		}
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
		Changed();
	}

	/// <summary>
	/// Give dollars. Host only.
	/// </summary>
	public void AddMoney( long amount )
	{
		if ( !CanChange || amount <= 0 )
			return;

		record.Money = System.Math.Min( record.Money + amount, ProgressRecord.MaxMoney );
		Changed();
	}

	/// <summary>
	/// Take dollars if there are enough. Host only. Returns whether they were taken.
	/// </summary>
	public bool TrySpendMoney( long amount )
	{
		if ( !CanChange || amount <= 0 || record.Money < amount )
			return false;

		record.Money -= amount;
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

		record.Credits = System.Math.Min( record.Credits + amount, ProgressRecord.MaxCredits );
		Changed();
	}

	/// <summary>
	/// Take credits if there are enough. Host only. Returns whether they were taken.
	/// </summary>
	public bool TrySpendCredits( long amount )
	{
		if ( !CanChange || amount <= 0 || record.Credits < amount )
			return false;

		record.Credits -= amount;
		Changed();
		return true;
	}

	/// <summary>
	/// Move money from this player to another, and write it in both histories. Host only. Returns whether it was moved:
	/// not to oneself, not more than there is, not past the most the other can hold.
	/// </summary>
	public bool TryPay( PlayerProgress to, long amount )
	{
		if ( !CanChange || to is null || to == this || !to.CanChange || amount <= 0 )
			return false;

		if ( record.Money < amount || to.record.Money + amount > ProgressRecord.MaxMoney )
			return false;

		var at = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		var from = Owner;
		var target = to.Owner;

		record.Money -= amount;
		to.record.Money += amount;

		AddPayment( new() { At = at, PeerId = target.SteamId.ToString(), PeerName = target.DisplayName, Amount = -amount } );
		to.AddPayment( new() { At = at, PeerId = from.SteamId.ToString(), PeerName = from.DisplayName, Amount = amount } );
		return true;
	}

	/// <summary>
	/// Pay out the reward of a dead server the first time it is breached: XP, credits and a blueprint. Host only.
	/// Returns whether it was paid, which is false when the player already breached that server.
	/// </summary>
	public bool CompleteIntrusion( string serverId, long xp, long credits, string blueprint )
	{
		if ( !CanChange || string.IsNullOrWhiteSpace( serverId ) || record.Cleared.Contains( serverId ) )
			return false;

		record.Cleared.Add( serverId );

		if ( !string.IsNullOrEmpty( blueprint ) && !record.Blueprints.Contains( blueprint ) )
			record.Blueprints.Add( blueprint );

		if ( xp > 0 )
			record.Xp = System.Math.Min( record.Xp + xp, ProgressCurve.MaxXp );

		if ( credits > 0 )
			record.Credits = System.Math.Min( record.Credits + credits, ProgressRecord.MaxCredits );

		IntrusionVersion++;
		Changed();
		return true;
	}

	/// <summary>
	/// Build a blueprint the player has found, for dollars. Host only. Returns whether it was built: not without the
	/// blueprint, not twice, not without the money.
	/// </summary>
	public bool TryBuild( string blueprint, long cost )
	{
		if ( !CanChange || string.IsNullOrWhiteSpace( blueprint ) || cost <= 0 )
			return false;

		if ( !record.Blueprints.Contains( blueprint ) || record.Built.Contains( blueprint ) || record.Money < cost )
			return false;

		record.Money -= cost;
		record.Built.Add( blueprint );
		BuildVersion++;
		Changed();
		return true;
	}

	void AddPayment( PaymentRecord payment )
	{
		record.Payments.Add( payment );

		if ( record.Payments.Count > PaymentRecord.MaxKept )
			record.Payments.RemoveAt( 0 );

		PaymentVersion++;
		Changed();
	}

	void Changed()
	{
		Xp = record.Xp;
		Money = record.Money;
		Credits = record.Credits;
		dirty = true;
		sinceChange = 0;
	}

	/// <summary>
	/// Testing only: the player asks the host for XP, dollars and credits. The host refuses unless it is the editor, and
	/// unless the caller is this player.
	/// </summary>
	[Rpc.Host]
	public void DebugGrant( long xp, long money, long credits )
	{
		if ( !Application.IsEditor || Rpc.Caller != (Network.Owner ?? Connection.Local) )
			return;

		AddXp( xp );
		AddMoney( money );
		AddCredits( credits );
	}
}
