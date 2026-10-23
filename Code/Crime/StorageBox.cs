namespace Sandbox;

/// <summary>
/// A storage box: a grid of slots a player can offload their bag into. Using it opens the storage panel
/// (<see cref="StorageMenu"/>) on the player's own screen; the press itself changes nothing (the option runs on the
/// player's client, <see cref="InteractionSide.Local"/>). Every move is a request to the host, which takes the player
/// from the network (<c>Rpc.Caller</c>), checks they stand within <see cref="Reach"/> of THIS box and may use it
/// (<see cref="CanUse"/>), checks the slots and the items, and moves one item at a time (<see cref="StorageRules"/>).
/// Only slot indices cross the network.
/// </summary>
/// <remarks>
/// A box belongs to the first player who uses it, remembered by <see cref="StorageRegistry"/> across restarts. What it
/// holds is in the owner's <see cref="CrimeRecord.Storage"/>, under <see cref="BoxId"/>, not in this component: it is
/// saved with the owner, and works while the owner is away. The host sends the contents to the player who asks, and to
/// nobody else. Put it with an <see cref="Interactable"/> and a collider, and make the object a network object.
/// </remarks>
public sealed class StorageBox : Component, IInteractionHandler
{
	const string OptionId = "open_storage";

	/// <summary>How close, in world units, a player must be to this box for the host to move things for them.</summary>
	public const float Reach = 300f;

	/// <summary>How many slots wide the box is.</summary>
	[Property, Range( 1, StorageRules.MaxColumns )]
	public int Columns { get; set; } = 6;

	/// <summary>How many slots high the box is.</summary>
	[Property, Range( 1, StorageRules.MaxRows )]
	public int Rows { get; set; } = 4;

	/// <summary>The number of slots.</summary>
	public int Capacity =>
		System.Math.Clamp( Columns, 1, StorageRules.MaxColumns ) * System.Math.Clamp( Rows, 1, StorageRules.MaxRows );

	/// <summary>
	/// The Steam id of the player this box belongs to, or 0 while nobody has claimed it. Set by the host.
	/// </summary>
	[Sync( SyncFlags.FromHost )]
	public ulong OwnerId { get; set; }

	/// <summary>
	/// The id a player's save uses for this box: its object's id, which is stable in the scene.
	/// </summary>
	public string BoxId => GameObject.Id.ToString();

	// -- What this machine knows of the contents: only the player who asked is sent them ------------------------------

	ItemData[] view = [];

	/// <summary>
	/// Whether the host has answered since the panel opened.
	/// </summary>
	public bool ViewReady { get; private set; }

	/// <summary>
	/// Bumps whenever the host sends the contents, so the panel redraws.
	/// </summary>
	public int ViewVersion { get; private set; }

	/// <summary>
	/// The item in a slot as the host last told this player, or null. Empty before the host has answered.
	/// </summary>
	public ItemData ViewAt( int slot ) => slot >= 0 && slot < view.Length ? view[slot] : null;

	/// <summary>
	/// How many items the host last said are in the box.
	/// </summary>
	public int ViewCount => view.Count( x => x is not null );

	/// <summary>
	/// Forget the contents (the panel closed), so nothing stays in memory on this machine.
	/// </summary>
	public void ClearView()
	{
		view = [];
		ViewReady = false;
		ViewVersion++;
	}

	protected override void OnStart()
	{
		// A restart forgets who a box belongs to; the host reads it back, so a player who is away keeps theirs.
		if ( Networking.IsHost && Game.IsPlaying && OwnerId == 0 )
			OwnerId = StorageRegistry.OwnerOf( BoxId );

		var option = InteractionSetup.EnsureOption(
			this, OptionId, "Open storage", "inventory", "Keep things here when your bag is full." );

		// The press only opens the panel on the player's own screen.
		if ( option is not null )
			option.RunOn = InteractionSide.Local;
	}

	/// <summary>
	/// Whether this player may use the box: it is theirs, or nobody's yet. The one place that decides who may open it,
	/// so raiding can change it later.
	/// </summary>
	public bool CanUse( ulong steamId ) => OwnerId == 0 || OwnerId == steamId;

	/// <summary>
	/// Whether a position is within <see cref="Reach"/> of this box.
	/// </summary>
	public bool IsNear( Vector3 position ) => WorldPosition.Distance( position ) <= Reach;

	InteractionInfo? IInteractionHandler.Describe( InteractionEvent e )
	{
		if ( e.Option?.Id != OptionId )
			return null;

		var owner = e.Interactor?.Network.Owner ?? Connection.Local;
		var me = owner is null ? 0UL : (ulong)owner.SteamId;

		return CanUse( me )
			? new( "Open storage", "inventory", "Keep things here when your bag is full." )
			: new( "Open storage", "inventory", "This isn't your storage.", false );
	}

	void IInteractionHandler.OnInteract( InteractionEvent e )
	{
		if ( e.Option?.Id == OptionId && !Application.IsHeadless )
			StorageMenu.Open( this );
	}

	// -- Requests from the player's client ----------------------------------------------------------------------------

	/// <summary>
	/// The panel opened or wants the contents again. The first player to ask claims an unclaimed box. The answer goes
	/// to the caller alone.
	/// </summary>
	[Rpc.Host]
	public void RequestRefresh()
	{
		if ( Begin() is { } who )
			Reply( who, CrimeResult.Success() );
	}

	/// <summary>
	/// Move the item in a bag slot to the first free slot of the box.
	/// </summary>
	[Rpc.Host]
	public void RequestStore( int bagSlot )
	{
		if ( Begin() is not { } who )
			return;

		Reply( who, StorageRules.Store( who.Crime, BoxId, Capacity, who.Bag, bagSlot ) );
	}

	/// <summary>
	/// Move the item in a slot of the box to the first free bag slot.
	/// </summary>
	[Rpc.Host]
	public void RequestTake( int boxSlot )
	{
		if ( Begin() is not { } who )
			return;

		Reply( who, StorageRules.Take( who.Crime, BoxId, who.Bag, boxSlot ) );
	}

	/// <summary>
	/// Move the item in a slot of the box to another slot of the box, swapping with what is there.
	/// </summary>
	[Rpc.Host]
	public void RequestMoveInBox( int from, int to )
	{
		if ( Begin() is not { } who )
			return;

		Reply( who, StorageRules.MoveInBox( who.Crime, BoxId, Capacity, from, to ) );
	}

	/// <summary>
	/// Who is asking, once the host has checked they may.
	/// </summary>
	sealed record Asker( Connection Caller, PlayerCrime Player, PlayerProgress Progress, CrimeRecord Crime )
	{
		public PlayerInventory Bag => Player.Components.Get<PlayerInventory>();
	}

	/// <summary>
	/// The player behind the running RPC, or null when they may not do this: their record is not loaded, they are not
	/// within reach of this box, or the box is another player's. Claims an unclaimed box for them. Host only.
	/// </summary>
	Asker Begin()
	{
		var caller = Rpc.Caller ?? Connection.Local;

		if ( !Networking.IsHost || caller is null || PlayerCrime.For( caller ) is not { Ready: true } player
		     || PlayerProgress.For( caller ) is not { Loaded: true } progress
		     || progress.Record?.Crime is not { } crime )
			return null;

		var who = new Asker( caller, player, progress, crime );

		if ( !IsNear( player.WorldPosition ) )
		{
			player.Tell( CrimeResult.Fail( "Go to the storage box." ) );
			return null;
		}

		var steamId = (ulong)caller.SteamId;

		if ( !CanUse( steamId ) || (OwnerId == 0 && !StorageRegistry.Claim( BoxId, steamId )) )
		{
			player.Tell( CrimeResult.Fail( "This isn't your storage." ) );
			SendView( caller, false, "" );
			return null;
		}

		OwnerId = steamId;
		return who;
	}

	/// <summary>
	/// Save what a request did, say how it went, and send the contents back to the caller alone.
	/// </summary>
	void Reply( Asker who, CrimeResult result )
	{
		if ( result.Ok )
			who.Progress.NotifyChanged( ProgressChange.Crime );

		who.Player.Tell( result );
		SendView( who.Caller, true, Json.Serialize( StorageRules.View( who.Crime, BoxId, Capacity ) ) );
	}

	void SendView( Connection caller, bool allowed, string json )
	{
		using ( Rpc.FilterInclude( caller ) )
			ReceiveView( allowed, json );
	}

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	void ReceiveView( bool allowed, string json )
	{
		view = allowed && !string.IsNullOrEmpty( json ) ? Json.Deserialize<ItemData[]>( json ) ?? [] : [];
		ViewReady = allowed;
		ViewVersion++;

		// Told it is not theirs: the panel closes itself.
		if ( !allowed )
			StorageMenu.Refused( this );
	}
}
