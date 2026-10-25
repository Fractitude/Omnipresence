namespace Sandbox;

/// <summary>
/// Recent ammo pickups, for the feed above the ammo counter (<see cref="AmmoPickupFeed"/>). Ported from the sandbox
/// game, whose inventory does the same.
/// <para>
/// <see cref="GiveAmmo"/> hides the engine's: call it on a <c>WeaponInventory</c> (not on a <see cref="BaseInventoryComponent"/>)
/// to tell the owner about what they picked up. Ammo granted with the weapon itself goes through the engine's and
/// shows nothing.
/// </para>
/// </summary>
public sealed partial class WeaponInventory
{
	public sealed class AmmoPickupNotice
	{
		public BaseAmmoResource Type { get; init; }
		public int Amount { get; set; }
		public float ExpiresAt { get; set; }
	}

	const float AmmoNoticeSeconds = 1.5f;
	const int MaxAmmoNotices = 4;

	readonly List<AmmoPickupNotice> ammoPickupNotices = new();

	public IReadOnlyList<AmmoPickupNotice> AmmoPickupNotices => ammoPickupNotices;

	/// <summary>Give reserve ammo and report the amount actually accepted to its owner.</summary>
	public new int GiveAmmo( BaseAmmoResource type, int amount )
	{
		var added = base.GiveAmmo( type, amount );
		NotifyAmmoPickup( type, added );
		return added;
	}

	void NotifyAmmoPickup( BaseAmmoResource type, int amount )
	{
		if ( !Networking.IsHost || type is null || amount <= 0 ) return;
		ReceiveAmmoPickup( type.ResourcePath, amount );
	}

	[Rpc.Owner]
	void ReceiveAmmoPickup( string resourcePath, int amount )
	{
		if ( IsProxy || amount <= 0 ) return;

		var type = ResourceLibrary.Get<BaseAmmoResource>( resourcePath );
		if ( type is null ) return;

		var now = RealTime.Now;
		ammoPickupNotices.RemoveAll( notice => notice.ExpiresAt <= now );

		var existing = ammoPickupNotices.FirstOrDefault( notice => notice.Type.ResourcePath == resourcePath );
		if ( existing is not null )
		{
			existing.Amount += amount;
			existing.ExpiresAt = now + AmmoNoticeSeconds;
			return;
		}

		if ( ammoPickupNotices.Count >= MaxAmmoNotices ) ammoPickupNotices.RemoveAt( 0 );
		ammoPickupNotices.Add( new AmmoPickupNotice { Type = type, Amount = amount, ExpiresAt = now + AmmoNoticeSeconds } );
	}
}
