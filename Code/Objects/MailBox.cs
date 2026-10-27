using Sandbox;

/// <summary>
/// A mailbox whose flag shows whether there is anything at it. It has two options, added in code to the
/// <see cref="Interactable"/>: "Send all parcels" and "Collect parcels". Any other option the object was set up with is
/// removed, so the flag can't be moved by hand.
/// </summary>
/// <remarks>
/// <b>Anyone may use any box, both ways.</b> Posting a parcel is putting it in the post, so a player posts from whatever
/// box is nearest; and what is waiting in a box is the box's (<see cref="MailboxMail"/>), so whoever opens it takes it,
/// whether or not it was meant for them: that is how another player's delivery is stolen. A box may still belong to
/// somebody: it carries an <see cref="OwnershipComponent"/> whose title is a player or an organization, which is the
/// box's and grants nothing here. What the title does decide is whose box is whose where it matters elsewhere: a home
/// delivery from the Depot lands beside the buyer's (<see cref="IsTheirs"/>). The first player to use a box that
/// belongs to nobody claims it, which is how a box in the scene comes to have an owner at all.
/// <para>
/// The flag goes up while a parcel waits for the mailman (<see cref="PostalSystem"/>, which sets <see cref="HasMail"/>
/// on the host) and while mail sits in the box (<see cref="MailCount"/>).
/// </para>
/// </remarks>
public sealed class MailBox : Component, Component.ExecuteInEditor, IInteractionHandler
{
	const string SendId = "send_parcel";
	const string CollectId = "collect_parcels";

	/// <summary>Whether a parcel of the box's user waits for the mailman. Raises the flag when true.</summary>
	[Property, Sync] public bool HasMail { get; set; }

	/// <summary>
	/// How many letters wait in the box, set by the host, so the label of the Collect option is right on a client too.
	/// </summary>
	[Sync( SyncFlags.FromHost )] public int MailCount { get; set; }

	/// <summary>
	/// The id the box's mail is kept under: its object's id, which is stable in the scene (a bought box's permanent
	/// id).
	/// </summary>
	public string BoxId => PlacedObject.IdOf( this );

	/// <summary>Who the box belongs to, as every machine sees it, or nobody.</summary>
	public Ownership Owner => OwnershipComponent.TitleOf( GameObject );

	/// <summary>
	/// Whether this box is this player's: theirs, or their organization's property. Says nothing about who may use it,
	/// which is anyone. Off the host an organization is not known, so there only a player's own box counts.
	/// </summary>
	public bool IsTheirs( ulong steamId )
	{
		if ( OwnershipComponent.On( GameObject ) is not { } thing )
			return false;

		var title = thing.Owner;

		return title.IsOwned
		       && (title.HeldBy( steamId ) || (title.IsOrganization && thing.CanUse( steamId )));
	}

	GameObject _flag;

	protected override void OnStart()
	{
		InteractionSetup.EnsureOption(
			this, SendId, "Send all parcels", "local_shipping", "Puts every parcel you carry in the mailbox." );

		InteractionSetup.EnsureOption(
			this, CollectId, "Collect parcels", "inventory_2", "Takes whatever mail is waiting in the box." );

		// Sending and collecting are the only things to do here, in this order. Run the same way on every machine: the
		// host takes an option as its place in the list. Not in the editor, so the scene is not changed by it.
		if ( Game.IsPlaying && Components.Get<Interactable>( FindMode.EnabledInSelfAndDescendants ) is { } interactable )
		{
			interactable.Options = interactable.Options
				.Where( x => x?.Id == SendId || x?.Id == CollectId )
				.OrderBy( x => x.Id == SendId ? 0 : 1 )
				.ToList();
		}

		if ( Networking.IsHost && Game.IsPlaying )
			ReadMail();
	}

	/// <summary>
	/// Read the box's mail from <see cref="MailboxMail"/>, and put the count every machine shows right. Host only, and
	/// called by the mail store whenever a letter arrives or is taken.
	/// </summary>
	public void ReadMail()
	{
		if ( Networking.IsHost )
			MailCount = MailboxMail.CountOf( BoxId );
	}

	/// <summary>
	/// The box has been used by this player: it becomes theirs if it belongs to nobody yet. Host only. Use grants
	/// nothing, so this is the only way a box placed in the scene comes to have an owner at all.
	/// </summary>
	public void UseBy( ulong steamId )
	{
		if ( Networking.IsHost && steamId != 0 )
			OwnershipComponent.On( GameObject )?.Claim( steamId );
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
		// The count is from the player's own inventory, which is synced, so it is right on a client too.
		var inventory = e.Interactor?.Components.Get<PlayerInventory>() ?? PlayerInventory.Local;
		var parcels = inventory?.CountOf( ItemCatalog.Parcel ) ?? 0;

		return parcels > 0
			? new( $"Send all parcels ({parcels})", "local_shipping",
				"Puts every parcel you carry in the mailbox. The mailman collects them." )
			: new( "Send all parcels", "local_shipping", "You have no parcel to send.", false );
	}

	InteractionInfo DescribeCollect( InteractionEvent e )
	{
		// What is in the box is synced from the host; what of the player's own has no box to wait in is in their view.
		var mine = (e.Interactor?.Components.Get<PlayerCrime>() ?? PlayerCrime.Local)?.MailWaiting ?? 0;
		var waiting = MailCount + mine;

		if ( waiting <= 0 )
			return new( "Collect parcels", "inventory_2", "There is nothing in this mailbox.", false );

		var text = MailCount > 0
			? "Takes whatever mail is waiting here. It isn't always yours."
			: "Takes what you ordered out of the mailbox.";

		return new( $"Collect parcels ({waiting})", "inventory_2", text );
	}

	void IInteractionHandler.OnInteract( InteractionEvent e )
	{
		if ( !Networking.IsHost || e.Interactor?.Components.Get<PlayerCrime>() is not { } crime )
			return;

		if ( e.Option?.Id == SendId )
			crime.Tell( crime.SendAllParcels( this ) );
		else if ( e.Option?.Id == CollectId )
			crime.Tell( crime.CollectParcels( this ) );

		ReadMail();
	}

	protected override void OnUpdate()
	{
		if ( !_flag.IsValid() )
			_flag = GameObject.GetAllObjects( true ).FirstOrDefault( o => o != GameObject && o.Name == "Flag" );

		if ( !_flag.IsValid() )
			return;

		_flag.LocalRotation = Rotation.FromPitch( HasMail || MailCount > 0 ? -90f : 0f );
	}
}
