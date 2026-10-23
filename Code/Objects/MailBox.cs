using Sandbox;

/// <summary>
/// A mailbox whose flag shows whether it has mail. Its first option (<see cref="Interactable"/>) puts the flag up or down.
/// Set the Interactable to run on the host, since <see cref="HasMail"/> is set there and synced to everyone else.
/// <para>
/// It is also where a player sends a parcel (the "Send parcel" option, added in code). A mailbox belongs to the first
/// player who sends from it (<see cref="OwnerId"/>, remembered in their save by <see cref="BoxId"/>), and nobody else can
/// use it. The flag goes up by itself while a parcel waits for the mailman (<see cref="PostalSystem"/>).
/// </para>
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

	protected override void OnStart() =>
		InteractionSetup.EnsureOption( this, SendId, "Send parcel", "local_shipping", "Put a parcel in the mailbox." );

	InteractionInfo? IInteractionHandler.Describe( InteractionEvent e )
	{
		if ( e.Option?.Id == SendId )
			return DescribeSend( e );

		return new( HasMail ? "Put flag down" : "Put flag up", "flag" );
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
		{
			HasMail = !HasMail;
			return;
		}

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
