namespace Sandbox;

/// <summary>
/// A storage box: a grid of slots a player can offload their bag into. Using it opens the storage panel
/// (<see cref="StorageMenu"/>) on the player's own screen; the press itself changes nothing (the option runs on the
/// player's client, <see cref="InteractionSide.Local"/>). Every move is a request to the host, which takes the player
/// from the network (<c>Rpc.Caller</c>), checks they stand within <see cref="Reach"/> of THIS box, checks the slots and
/// the items, and moves one item at a time (<see cref="StorageRules"/>). Only slot indices cross the network.
/// </summary>
/// <remarks>
/// Anyone may open and use any box. A box belongs to the first player who uses it, remembered by
/// <see cref="StorageRegistry"/> across restarts, and that owner's <see cref="CrimeRecord.Storage"/> is where what is
/// in it lives, under <see cref="BoxId"/>: the contents are one set for everyone who opens it, saved with the
/// owner, and reachable while the owner is away. The host sends the contents to the player who asks, and to nobody
/// else. Put it with an <see cref="Interactable"/> and a collider, and make the object a network object.
/// </remarks>
public sealed class StorageBox : ItemContainer, IInteractionHandler
{
	const string OptionId = "open_storage";

	/// <summary>How close, in world units, a player must be to this box for the host to move things for them.</summary>
	public const float ReachDistance = 300f;

	/// <summary>How many slots wide the box is.</summary>
	[Property, Range( 1, StorageRules.MaxColumns )]
	public int Columns { get; set; } = 6;

	/// <summary>How many slots high the box is.</summary>
	[Property, Range( 1, StorageRules.MaxRows )]
	public int Rows { get; set; } = 4;

	/// <summary>
	/// The Steam id of the player this box belongs to, or 0 while nobody has claimed it. Set by the host.
	/// </summary>
	[Sync( SyncFlags.FromHost )]
	public ulong OwnerId { get; set; }

	/// <summary>
	/// The id a player's save uses for this box: a bought box's permanent <see cref="PlacedObject.ObjectId"/>, else its
	/// object's id, which is stable in the scene.
	/// </summary>
	public string BoxId => PlacedObject.IdOf( this );

	public override string Title => "Storage box";

	public override string ActionWord => "storage";

	public override int ColumnCount => System.Math.Clamp( Columns, 1, StorageRules.MaxColumns );

	public override int RowCount => System.Math.Clamp( Rows, 1, StorageRules.MaxRows );

	public override float Reach => ReachDistance;

	protected override string TooFarText => "Go to the storage box.";

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

	/// <summary>
	/// A box belongs to the first player who uses it: this claims an unclaimed box for them and refuses anyone else.
	/// </summary>
	protected override bool Admit( Asker who )
	{
		var steamId = (ulong)who.Caller.SteamId;

		if ( !CanUse( steamId ) || (OwnerId == 0 && !StorageRegistry.Claim( BoxId, steamId )) )
		{
			who.Player.Tell( CrimeResult.Fail( "This isn't your storage." ) );
			return false;
		}

		OwnerId = steamId;
		return true;
	}

	/// <summary>
	/// The contents are in the owner's record, under the box's id.
	/// </summary>
	protected override (CrimeRecord Record, string Id) Contents( Asker who ) => (who.Crime, BoxId);
}
