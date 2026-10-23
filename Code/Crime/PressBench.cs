namespace Sandbox;

/// <summary>
/// A press bench. Using it opens the pressing panel (<see cref="PressMenu"/>) on the player's own screen, which lists
/// the raw of every drug the player carries, with a Press button for each. The press itself changes nothing: the
/// option runs on the player's own client (<see cref="InteractionSide.Local"/>) and only the panel's Press button asks
/// the host (<see cref="PlayerCrime.RequestPress"/>, with the inventory slot). Pressing turns raw into the drug's
/// product with a placeholder quality (the batch's, nudged at random; the Tuning minigame replaces it later). Put it
/// with an <see cref="Interactable"/> and a collider.
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

	protected override void OnStart()
	{
		var option = InteractionSetup.EnsureOption(
			this, OptionId, "Press bench", "compress", "Press raw into its product." );

		// The press only opens the panel on the player's own screen.
		if ( option is not null )
			option.RunOn = InteractionSide.Local;
	}

	InteractionInfo? IInteractionHandler.Describe( InteractionEvent e )
	{
		if ( e.Option?.Id != OptionId )
			return null;

		var inventory = e.Interactor?.Components.Get<PlayerInventory>() ?? PlayerInventory.Local;
		var raw = ProductCatalog.Products.Sum( x => inventory?.CountOf( x.Raw.Id ) ?? 0 );

		return raw > 0
			? new( "Press", "compress", "Choose what to press." )
			: new( "Press bench", "compress", PressMenu.NothingToPress, false );
	}

	void IInteractionHandler.OnInteract( InteractionEvent e )
	{
		if ( e.Option?.Id == OptionId && !Application.IsHeadless )
			PressMenu.Open();
	}
}
