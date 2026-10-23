namespace Sandbox;

/// <summary>
/// A packing table. Using it packs the next order the player can fill: the accepted order, in time, whose tabs the
/// player carries, earliest deadline first (<see cref="PlayerCrime.PackNext"/>). It takes those tabs and gives a parcel.
/// The label comes from <see cref="PlayerCrime.PackView"/>, which the host syncs, so clients can read it. The press sends
/// nothing but itself; the host picks the order and checks everything. Put it with an <see cref="Interactable"/> (network
/// mode Object) and a collider.
/// </summary>
public sealed class PackingTable : Component, IInteractionHandler
{
	const string OptionId = "pack";

	protected override void OnStart() =>
		InteractionSetup.EnsureOption( this, OptionId, "Packing table", "inventory_2", "Pack an order into a parcel." );

	InteractionInfo? IInteractionHandler.Describe( InteractionEvent e )
	{
		if ( e.Option?.Id != OptionId )
			return null;

		var crime = e.Interactor?.Components.Get<PlayerCrime>() ?? PlayerCrime.Local;

		if ( crime is null )
			return new( "Packing table", "inventory_2", "", false );

		var (status, handle, count, quality, have) = crime.PackOffer();

		return status switch
		{
			"ok" => new( $"Pack order for {handle}", "inventory_2", $"{count} x Static tabs, quality {quality} or better" ),
			"short" => new( $"{handle} needs {count} tabs at quality {quality} or better", "inventory_2",
				$"You have {have}", false ),
			_ => new( "No accepted orders", "inventory_2", "Accept an order in Hush first.", false ),
		};
	}

	void IInteractionHandler.OnInteract( InteractionEvent e )
	{
		if ( e.Option?.Id == OptionId && Networking.IsHost && e.Interactor?.Components.Get<PlayerCrime>() is { } crime )
			crime.Tell( crime.PackNext() );
	}
}
