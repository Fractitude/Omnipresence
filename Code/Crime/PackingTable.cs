namespace Sandbox;

/// <summary>
/// A packing table. Using it opens the packing panel (<see cref="PackingMenu"/>) on the player's own screen, where they
/// pick which accepted order to pack and with which batch of their bag. The press itself changes nothing: the option
/// runs on the pressing player's client (<see cref="InteractionSide.Local"/>) and only the panel's Pack button asks the
/// host (<see cref="PlayerCrime.RequestPack"/>). The label comes from <see cref="PlayerCrime.PackOrders"/>, which the
/// host syncs. Put it with an <see cref="Interactable"/> and a collider.
/// </summary>
public sealed class PackingTable : Component, IInteractionHandler
{
	const string OptionId = "pack";

	/// <summary>How close, in world units, a player must be to a table for the host to pack for them.</summary>
	public const float Reach = 300f;

	/// <summary>
	/// Whether a packing table stands within <see cref="Reach"/> of this position.
	/// </summary>
	public static bool IsNear( Vector3 position ) =>
		Game.ActiveScene?.GetAllComponents<PackingTable>().Any( x => x.WorldPosition.Distance( position ) <= Reach )
		?? false;

	protected override void OnStart()
	{
		var option = InteractionSetup.EnsureOption(
			this, OptionId, "Packing table", "inventory_2", "Pack an order into a parcel." );

		// The press only opens the panel on the player's own screen.
		if ( option is not null )
			option.RunOn = InteractionSide.Local;
	}

	InteractionInfo? IInteractionHandler.Describe( InteractionEvent e )
	{
		if ( e.Option?.Id != OptionId )
			return null;

		var crime = e.Interactor?.Components.Get<PlayerCrime>() ?? PlayerCrime.Local;
		var waiting = crime?.PackableOrders().Count ?? 0;

		if ( waiting == 0 )
			return new( "No accepted orders", "inventory_2", "Accept an order in Hush first.", false );

		var hint = waiting == 1 ? "1 order is waiting to be packed." : $"{waiting} orders are waiting to be packed.";
		return new( "Pack orders", "inventory_2", hint );
	}

	void IInteractionHandler.OnInteract( InteractionEvent e )
	{
		if ( e.Option?.Id == OptionId && !Application.IsHeadless )
			PackingMenu.Open();
	}
}
