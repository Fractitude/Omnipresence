namespace Sandbox;

/// <summary>
/// A player's level, XP and money, on the player object. The host's <see cref="PlayerProgression"/> holds the real
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
	[Sync( SyncFlags.FromHost )] public long Money { get; private set; }

	public int Level => ProgressCurve.LevelFor( Xp );

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
	/// Give money. Host only.
	/// </summary>
	public void AddMoney( long amount )
	{
		if ( !CanChange || amount <= 0 )
			return;

		record.Money = System.Math.Min( record.Money + amount, ProgressRecord.MaxMoney );
		Changed();
	}

	/// <summary>
	/// Take money if there is enough. Host only. Returns whether it was taken.
	/// </summary>
	public bool TrySpend( long amount )
	{
		if ( !CanChange || amount <= 0 || record.Money < amount )
			return false;

		record.Money -= amount;
		Changed();
		return true;
	}

	void Changed()
	{
		Xp = record.Xp;
		Money = record.Money;
		dirty = true;
		sinceChange = 0;
	}

	/// <summary>
	/// Testing only: the player asks the host for XP and money. The host refuses unless it is the editor, and unless
	/// the caller is this player.
	/// </summary>
	[Rpc.Host]
	public void DebugGrant( long xp, long money )
	{
		if ( !Application.IsEditor || Rpc.Caller != (Network.Owner ?? Connection.Local) )
			return;

		AddXp( xp );
		AddMoney( money );
	}
}
