namespace Sandbox;

/// <summary>
/// A press bench. Press turns the raw Static the player carries into tabs, with a placeholder quality (the average of the
/// raw, nudged at random; the Tuning minigame replaces it later). Put it with an <see cref="Interactable"/> (network mode
/// Object) and a collider.
/// </summary>
public sealed class PressBench : Component, IInteractionHandler
{
	const string OptionId = "press";

	protected override void OnStart() =>
		InteractionSetup.EnsureOption( this, OptionId, "Press bench", "compress", "Press raw Static into tabs." );

	InteractionInfo? IInteractionHandler.Describe( InteractionEvent e )
	{
		if ( e.Option?.Id != OptionId )
			return null;

		var inventory = e.Interactor?.Components.Get<PlayerInventory>() ?? PlayerInventory.Local;
		var raw = inventory?.CountOf( ItemCatalog.StaticRaw ) ?? 0;

		return raw > 0
			? new( $"Press {raw} raw into tabs", "compress", "Turns raw Static into tabs." )
			: new( "Press bench", "compress", "You have no raw Static to press.", false );
	}

	void IInteractionHandler.OnInteract( InteractionEvent e )
	{
		if ( e.Option?.Id == OptionId && Networking.IsHost && e.Interactor?.Components.Get<PlayerCrime>() is { } crime )
			crime.Tell( crime.Press() );
	}
}
