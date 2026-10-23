namespace Sandbox;

/// <summary>
/// A press bench. Press turns the raw Static the player carries into tabs, with a placeholder quality (the average of the
/// raw, nudged at random; the Tuning minigame replaces it later). Put it with an <see cref="Interactable"/> (network mode
/// Object) and a collider.
/// </summary>
public sealed class PressBench : Component, IInteractionHandler
{
	const string OptionId = "press";

	/// <summary>How close, in world units, a player must be to a bench for the host to press for them.</summary>
	public const float Reach = 300f;

	/// <summary>
	/// Whether a press bench stands within <see cref="Reach"/> of this position.
	/// </summary>
	public static bool IsNear( Vector3 position ) =>
		Game.ActiveScene?.GetAllComponents<PressBench>().Any( x => x.WorldPosition.Distance( position ) <= Reach )
		?? false;

	protected override void OnStart() =>
		InteractionSetup.EnsureOption( this, OptionId, "Press bench", "compress", "Press raw Static into tabs." );

	InteractionInfo? IInteractionHandler.Describe( InteractionEvent e )
	{
		if ( e.Option?.Id != OptionId )
			return null;

		var inventory = e.Interactor?.Components.Get<PlayerInventory>() ?? PlayerInventory.Local;
		var raw = ProductCatalog.Products.Sum( x => inventory?.CountOf( x.Raw.Id ) ?? 0 );

		return raw > 0
			? new( "Press", "compress", "Turns raw into product." )
			: new( "Press bench", "compress", "You have nothing to press.", false );
	}

	void IInteractionHandler.OnInteract( InteractionEvent e )
	{
		if ( e.Option?.Id != OptionId || !Networking.IsHost || e.Interactor is null )
			return;

		var inventory = e.Interactor.Components.Get<PlayerInventory>();
		var slot = Enumerable.Range( 0, inventory?.SlotCount ?? 0 )
			.FirstOrDefault( x => ProductCatalog.OfRaw( inventory.At( x )?.Kind ) is not null, -1 );

		if ( e.Interactor.Components.Get<PlayerCrime>() is { } crime )
			crime.Tell( crime.Press( slot ) );
	}
}
