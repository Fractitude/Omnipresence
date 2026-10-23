namespace Sandbox;

/// <summary>
/// A grow tray. Plant a Static culture, wait for the game clock, harvest raw Static. What is in a tray belongs to the
/// player who planted it (<see cref="PlayerCrime"/>, saved in their <see cref="CrimeRecord"/> by <see cref="TrayId"/>), so
/// two players see their own tray in the same place. The tray has one option whose label follows the player's own state:
/// "Plant", "Growing, 1:30 left", "Harvest". Put it with an <see cref="Interactable"/> (network mode Object) and a collider.
/// </summary>
public sealed class GrowTray : Component, IInteractionHandler
{
	const string OptionId = "tray";

	/// <summary>
	/// The tray's id in the save: its object's id, which is stable in the scene.
	/// </summary>
	public string TrayId => GameObject.Id.ToString();

	protected override void OnStart() =>
		InteractionSetup.EnsureOption( this, OptionId, "Grow tray", "grass", "Grow Static in a tray." );

	InteractionInfo? IInteractionHandler.Describe( InteractionEvent e )
	{
		if ( e.Option?.Id != OptionId )
			return null;

		var crime = (e.Interactor?.Components.Get<PlayerCrime>()) ?? PlayerCrime.Local;
		var inventory = e.Interactor?.Components.Get<PlayerInventory>() ?? PlayerInventory.Local;

		if ( crime is null )
			return new( "Grow tray", "grass", "", false );

		var (planted, readyAt) = crime.TrayState( TrayId );

		if ( !planted )
		{
			var has = inventory?.CountOf( ItemCatalog.StaticCulture ) > 0;

			var hint = has ? "Uses one culture." : "You need a Static culture.";

			return new( "Plant a Static culture", "grass", hint, has );
		}

		var left = readyAt - WorldClock.Now;

		return left > 0
			? new( $"Growing, {InteractionSetup.Countdown( left )} left", "hourglass_top", "", false )
			: new( "Harvest", "agriculture", "Gives raw Static." );
	}

	void IInteractionHandler.OnInteract( InteractionEvent e )
	{
		if ( e.Option?.Id != OptionId || !Networking.IsHost )
			return;

		if ( e.Interactor?.Components.Get<PlayerCrime>() is not { } crime )
			return;

		var planted = crime.TrayState( TrayId ).Planted;
		crime.Tell( planted ? crime.Harvest( TrayId ) : crime.Plant( TrayId ) );
	}
}
