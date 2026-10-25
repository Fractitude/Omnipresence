namespace Sandbox;

/// <summary>
/// One line of the pickup feed (<see cref="PickupFeed"/>): what the player got and how many. Two pickups with the same
/// <see cref="Key"/> inside the notice's lifetime are one line whose amount adds up.
/// </summary>
public sealed class PickupNotice
{
	public string Key { get; init; }
	public string Title { get; init; }
	public Texture Icon { get; init; }
	public int Amount { get; set; }
	public float ExpiresAt { get; set; }
}

/// <summary>
/// Tells a player what they picked up: ammo, a weapon, any item. The game calls one of <see cref="Ammo"/>,
/// <see cref="Weapon"/> or <see cref="Item"/> on the host after something has gone into the player's hands or bag, and the
/// player's own client shows it in the feed above the ammo counter. A new kind of pickup is a new method here and a case in
/// <see cref="IconOf"/>.
/// <para>
/// Only the owner's client keeps a list; nothing is saved or synced. Call these only for a pickup the player made, not
/// for what they spawn with or what moves between their bag and a box.
/// </para>
/// </summary>
public static class PickupNotices
{
	const string AmmoKind = "ammo";
	const string WeaponKind = "weapon";
	const string ItemKind = "item";

	const float Seconds = 4f;
	const int MaxLines = 6;

	static List<PickupNotice> notices = new();

	/// <summary>
	/// The local player's notices that have not expired, oldest first.
	/// </summary>
	public static IEnumerable<PickupNotice> Active => notices.Where( x => x.ExpiresAt > RealTime.Now );

	/// <summary>
	/// Reserve ammo went into a weapon's owner. Host only.
	/// </summary>
	public static void Ammo( WeaponInventory player, BaseAmmoResource type, int amount )
	{
		if ( type is not null )
			Send( player, AmmoKind, type.ResourcePath, type.Title, amount );
	}

	/// <summary>
	/// A weapon went into a player's hotbar. Host only.
	/// </summary>
	public static void Weapon( WeaponInventory player, BaseSandboxWeapon weapon )
	{
		if ( weapon.IsValid() )
			Send( player, WeaponKind, weapon.GameObject.PrefabInstanceSource ?? "", weapon.DisplayName, 1 );
	}

	/// <summary>
	/// An item, or a stack of them, went into a player's bag. Host only.
	/// </summary>
	public static void Item( GameObject player, string name, int count = 1 )
	{
		if ( player.IsValid() && !string.IsNullOrWhiteSpace( name ) )
			Send( player.Components.Get<WeaponInventory>(), ItemKind, name, name, count );
	}

	static void Send( WeaponInventory player, string kind, string id, string title, int amount )
	{
		if ( !player.IsValid() || amount <= 0 || !(!Networking.IsActive || Networking.IsHost) )
			return;

		player.ReceivePickup( kind, id, title, amount );
	}

	/// <summary>
	/// Runs on the owner's machine: turn what was sent into a line of the feed.
	/// </summary>
	internal static void Show( string kind, string id, string title, int amount )
	{
		var icon = IconOf( kind, id );
		if ( string.IsNullOrWhiteSpace( title ) )
			return;

		var now = RealTime.Now;
		notices.RemoveAll( x => x.ExpiresAt <= now );

		var key = $"{kind}:{id}";
		var existing = notices.FirstOrDefault( x => x.Key == key );

		if ( existing is not null )
		{
			existing.Amount += amount;
			existing.ExpiresAt = now + Seconds;
			return;
		}

		if ( notices.Count >= MaxLines )
			notices.RemoveAt( 0 );

		notices.Add( new PickupNotice { Key = key, Title = title, Icon = icon, Amount = amount, ExpiresAt = now + Seconds } );
	}

	/// <summary>
	/// The picture that goes with a pickup of this kind, or null for none (an item has no picture yet).
	/// </summary>
	static Texture IconOf( string kind, string id ) => kind switch
	{
		AmmoKind => ResourceLibrary.Get<BaseAmmoResource>( id )?.Icon,
		WeaponKind => WeaponIcons.Of( id ),
		_ => null,
	};
}
