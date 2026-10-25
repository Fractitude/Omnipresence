namespace Sandbox;

public sealed partial class Player
{
	/// <summary>
	/// Puts an outfit (a <c>ClothingContainer</c> as json) on this player's body, on every machine, or the player's own
	/// avatar when <paramref name="json"/> is empty. Only the owner may dress their player. Asked by
	/// <see cref="PlayerCustomizer"/>.
	/// </summary>
	[Rpc.Broadcast]
	public void BroadcastOutfit( string json, string colorsJson, string workshopIdents )
	{
		if ( Network.Owner is { } owner && Rpc.Caller is not null && Rpc.Caller != owner )
			return;

		var dresser = GetComponentInChildren<Dresser>( true );

		if ( !dresser.IsValid() )
			return;

		if ( string.IsNullOrEmpty( json ) )
			_ = PlayerCustomizer.DressDefault( dresser );
		else
			_ = PlayerCustomizer.Dress( dresser, json, OutfitColors.Deserialize( colorsJson ), workshopIdents );
	}
}
