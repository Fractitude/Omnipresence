using Sandbox;

/// <summary>
/// A mailbox whose flag shows whether it has mail to send. It has two options, added in code to the
/// <see cref="Interactable"/>: "Send all parcels" and "Collect parcels" (what the Hush shop delivered). Any other
/// option the object was set up with is removed, so the flag can't be moved by hand. The flag is for outgoing parcels
/// only. A mailbox belongs to the first player who sends from it (<see cref="OwnerId"/>, remembered in their save by
/// <see cref="BoxId"/>), and nobody else can use it. The flag goes up by itself while a parcel waits for the mailman
/// (<see cref="PostalSystem"/>), which sets <see cref="HasMail"/> on the host and syncs it to everyone else.
/// </summary>
public sealed class MailBox : Component, Component.ExecuteInEditor, IInteractionHandler
{
	const string SendId = "send_parcel";
	const string CollectId = "collect_parcels";

	/// <summary>Whether the mailbox has any mail in it. Raises the flag when true.</summary>
	[Property, Sync] public bool HasMail { get; set; }

	/// <summary>
	/// The Steam id of the player this mailbox belongs to, or 0 while nobody has claimed it. Set by the host.
	/// </summary>
	[Sync( SyncFlags.FromHost )] public ulong OwnerId { get; set; }

	/// <summary>
	/// The id a player's save uses for this mailbox: its object's id, which is stable in the scene.
	/// </summary>
	public string BoxId => GameObject.Id.ToString();

	GameObject _flag;

	protected override void OnStart()
	{
		InteractionSetup.EnsureOption(
			this, SendId, "Send all parcels", "local_shipping", "Puts every parcel you carry in the mailbox." );

		InteractionSetup.EnsureOption(
			this, CollectId, "Collect parcels", "inventory_2", "Takes what you ordered out of the mailbox." );

		// Sending and collecting are the only things to do here, in this order. Run the same way on every machine: the
		// host takes an option as its place in the list. Not in the editor, so the scene is not changed by it.
		if ( Game.IsPlaying && Components.Get<Interactable>( FindMode.EnabledInSelfAndDescendants ) is { } interactable )
		{
			interactable.Options = interactable.Options
				.Where( x => x?.Id == SendId || x?.Id == CollectId )
				.OrderBy( x => x.Id == SendId ? 0 : 1 )
				.ToList();
		}
	}

	InteractionInfo? IInteractionHandler.Describe( InteractionEvent e )
	{
		return e.Option?.Id switch
		{
			SendId => DescribeSend( e ),
			CollectId => DescribeCollect( e ),
			_ => null,
		};
	}

	InteractionInfo DescribeSend( InteractionEvent e )
	{
		var inventory = e.Interactor?.Components.Get<PlayerInventory>() ?? PlayerInventory.Local;
		var owner = e.Interactor?.Network.Owner ?? Connection.Local;
		var me = owner is null ? 0UL : (ulong)owner.SteamId;

		if ( OwnerId != 0 && OwnerId != me )
			return new( "Send all parcels", "local_shipping", "This isn't your mailbox.", false );

		// The count is from the player's own inventory, which is synced, so it is right on a client too.
		var parcels = inventory?.CountOf( ItemCatalog.Parcel ) ?? 0;

		return parcels > 0
			? new( $"Send all parcels ({parcels})", "local_shipping",
				"Puts every parcel you carry in the mailbox. The mailman collects them." )
			: new( "Send all parcels", "local_shipping", "You have no parcel to send.", false );
	}

	InteractionInfo DescribeCollect( InteractionEvent e )
	{
		var owner = e.Interactor?.Network.Owner ?? Connection.Local;
		var me = owner is null ? 0UL : (ulong)owner.SteamId;

		if ( OwnerId != 0 && OwnerId != me )
			return new( "Collect parcels", "inventory_2", "This isn't your mailbox.", false );

		// The count is synced from the host, so it is right on a client too.
		var waiting = (e.Interactor?.Components.Get<PlayerCrime>() ?? PlayerCrime.Local)?.MailWaiting ?? 0;

		return waiting > 0
			? new( $"Collect parcels ({waiting})", "inventory_2", "Takes what you ordered out of the mailbox." )
			: new( "Collect parcels", "inventory_2", "Nothing waiting for you.", false );
	}

	void IInteractionHandler.OnInteract( InteractionEvent e )
	{
		if ( !Networking.IsHost || e.Interactor?.Components.Get<PlayerCrime>() is not { } crime )
			return;

		if ( e.Option?.Id == SendId )
			crime.Tell( crime.SendAllParcels( this ) );
		else if ( e.Option?.Id == CollectId )
			crime.Tell( crime.CollectParcels( this ) );
	}

	protected override void OnUpdate()
	{
		if ( !_flag.IsValid() )
			_flag = GameObject.GetAllObjects( true ).FirstOrDefault( o => o != GameObject && o.Name == "Flag" );

		if ( !_flag.IsValid() )
			return;

		_flag.LocalRotation = Rotation.FromPitch( HasMail ? -90f : 0f );
	}
}
