namespace Sandbox;

/// <summary>
/// An object the player can pick up into their <see cref="PlayerInventory"/>. It adds a Pick up option to the object's
/// <see cref="Interactable"/>. Using it puts the item in the inventory and removes the object from the world. Needs a
/// collider the player can look at, and a network object, since the pickup runs on the host.
/// </summary>
public class PickupItem : Component, IItem, IInteractionHandler
{
	/// <summary>
	/// The <see cref="InteractionOption.Id"/> of the Pick up option.
	/// </summary>
	public const string PickupId = "pickup";

	[RequireComponent] public Interactable Interactable { get; set; }

	[Property] public string ItemName { get; set; } = "Item";

	[Property, TextArea] public string ItemDescription { get; set; } = "";

	/// <summary>
	/// The icon's model. Empty draws the whole object from its prefab, or its first model if it is not from one.
	/// </summary>
	[Property] public Model IconModel { get; set; }

	string IItem.ItemPrefab => IconModel.IsValid() ? "" : ItemData.NearestPrefab( GameObject );

	Model IItem.ItemModel => IconModel ?? Components.Get<ModelRenderer>( FindMode.EnabledInSelfAndDescendants )?.Model;

	protected override void OnStart()
	{
		if ( !Interactable.IsValid() || Interactable.Options.Any( x => x?.Id == PickupId ) )
			return;

		// The option an Interactable starts with is not wanted beside Pick up.
		Interactable.Options.RemoveAll( x => x is { Id: "", Label: "Use", OnInteract: null } );
		Interactable.Options.Insert( 0, new InteractionOption { Id = PickupId, Label = "Pick up", Icon = "back_hand" } );
	}

	InteractionInfo? IInteractionHandler.Describe( InteractionEvent e ) =>
		e.Option?.Id == PickupId ? new InteractionInfo( $"Pick up {ItemName}", "back_hand", ItemDescription ) : null;

	bool IInteractionHandler.CanInteract( InteractionEvent e ) =>
		e.Option?.Id != PickupId || e.Interactor?.Components.Get<PlayerInventory>() is not { } inventory || inventory.HasRoom;

	void IInteractionHandler.OnInteract( InteractionEvent e )
	{
		if ( e.Option?.Id != PickupId || e.Interactor?.Components.Get<PlayerInventory>() is not { } inventory )
			return;

		if ( !inventory.Add( this ) )
			return;

		PickupNotices.Item( e.Interactor, ItemName );

		// A dropped one is no longer kept in the world file.
		WorldItemRegistry.Forget( GameObject );
		GameObject.Destroy();
	}
}
