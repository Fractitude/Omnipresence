namespace Sandbox;

/// <summary>
/// Tells the owner what they picked up (<see cref="PickupNotices"/>). Ammo and weapons are reported here; items in the bag
/// are reported where they are picked up (<see cref="PickupItem"/>, <see cref="WorldItem"/>). This is the component on the
/// player the notices travel through, because it is on the player prefab and owned by its player.
/// <para>
/// <see cref="GiveAmmo"/> hides the engine's: call it on a <c>WeaponInventory</c> (not on a <see cref="BaseInventoryComponent"/>)
/// to tell the owner about what they picked up. Ammo granted with the weapon itself goes through the engine's and
/// shows nothing.
/// </para>
/// </summary>
public sealed partial class WeaponInventory
{
	/// <summary>Give reserve ammo and report the amount actually accepted to its owner.</summary>
	public new int GiveAmmo( BaseAmmoResource type, int amount )
	{
		var added = base.GiveAmmo( type, amount );
		PickupNotices.Ammo( this, type, added );
		return added;
	}

	/// <summary>
	/// A weapon lying in the world is walked into or used: report whatever new weapon it put in the hotbar.
	/// </summary>
	public override void PickupWorldItem( BaseInventoryItem item )
	{
		var before = Weapons.ToHashSet();

		base.PickupWorldItem( item );

		foreach ( var weapon in Weapons.Where( x => !before.Contains( x ) ) )
			PickupNotices.Weapon( this, weapon );
	}

	[Rpc.Owner]
	internal void ReceivePickup( string kind, string id, string title, int amount )
	{
		if ( !IsProxy && amount > 0 )
			PickupNotices.Show( kind, id, title, amount );
	}
}
