using Sandbox;

/// <summary>
/// A mailbox whose flag shows whether it has mail. It is where a player sends a parcel: its only option is "Send parcel",
/// added in code to the <see cref="Interactable"/> (any other option the object was set up with is removed, so the flag
/// can't be moved by hand). A mailbox belongs to the first player who sends from it (<see cref="OwnerId"/>, remembered in
/// their save by <see cref="BoxId"/>), and nobody else can use it. The flag goes up by itself while a parcel waits for the
/// mailman (<see cref="PostalSystem"/>), which sets <see cref="HasMail"/> on the host and syncs it to everyone else.
/// </summary>
public sealed class MailBox : Component, Component.ExecuteInEditor, IInteractionHandler
{
	const string SendId = "send_parcel";

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
		InteractionSetup.EnsureOption( this, SendId, "Send parcel", "local_shipping", "Put a parcel in the mailbox." );

		// Send parcel is the only thing to do here. Run the same way on every machine: the host takes an option as its
		// place in the list. Not in the editor, so the scene is not changed by it.
		if ( Game.IsPlaying && Components.Get<Interactable>( FindMode.EnabledInSelfAndDescendants ) is { } interactable )
			interactable.Options = interactable.Options.Where( x => x?.Id == SendId ).ToList();
	}

	InteractionInfo? IInteractionHandler.Describe( InteractionEvent e )
	{
		return e.Option?.Id == SendId ? DescribeSend( e ) : null;
	}

	InteractionInfo DescribeSend( InteractionEvent e )
	{
		var inventory = e.Interactor?.Components.Get<PlayerInventory>() ?? PlayerInventory.Local;
		var owner = e.Interactor?.Network.Owner ?? Connection.Local;
		var me = owner is null ? 0UL : (ulong)owner.SteamId;

		if ( OwnerId != 0 && OwnerId != me )
			return new( "Send parcel", "local_shipping", "This isn't your mailbox.", false );

		return inventory?.CountOf( ItemCatalog.Parcel ) > 0
			? new( "Send parcel", "local_shipping", "The mailman collects it." )
			: new( "Send parcel", "local_shipping", "You have no parcel to send.", false );
	}

	void IInteractionHandler.OnInteract( InteractionEvent e )
	{
		if ( e.Option?.Id != SendId )
			return;

		if ( Networking.IsHost && e.Interactor?.Components.Get<PlayerCrime>() is { } crime )
			crime.Tell( crime.SendParcel( this ) );
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
