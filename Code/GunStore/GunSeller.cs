namespace Sandbox;

/// <summary>What a purchase at the gun store came to. <see cref="Message"/> is what the player reads either way.</summary>
public sealed record GunPurchase( bool Ok, string Message )
{
	public static GunPurchase Fail( string message ) => new( false, message );
}

/// <summary>
/// The gun store's sale, on the host: a weapon or a pack of ammunition costs cash and goes straight into the buyer's
/// <see cref="WeaponInventory"/>. The money is taken first and given back if the goods cannot be handed over, so cash is
/// never spent for nothing.
/// </summary>
public static class GunSeller
{
	/// <summary>The tag the store's computer has, and the app's manifest lists as its <c>ComputerTags</c>.</summary>
	public const string ComputerTag = "store";

	/// <summary>Does this computer carry the tag? The app only works on one that does.</summary>
	public static bool IsGunStore( NaviComputer computer ) =>
		computer?.ComputerTags?.Any( x => string.Equals( x?.Trim(), ComputerTag, System.StringComparison.OrdinalIgnoreCase ) )
		== true;

	/// <summary>Does this player already carry the weapon?</summary>
	public static bool Carries( WeaponInventory inventory, string weapon )
	{
		var path = WeaponInventory.PrefabPath( weapon );

		return inventory.IsValid()
		       && inventory.Weapons.Any( x => string.Equals( x.GameObject.PrefabInstanceSource, path,
			       System.StringComparison.OrdinalIgnoreCase ) );
	}

	/// <summary>The ammo resource of a pack, or null.</summary>
	public static BaseAmmoResource AmmoOf( GunItem item ) =>
		item is { Kind: GunItemKind.Ammo } && ResourceLibrary.TryGet<BaseAmmoResource>( item.AmmoPath, out var type )
			? type
			: null;

	/// <summary>How many rounds of a pack still fit in the player's reserve.</summary>
	public static int RoomFor( WeaponInventory inventory, GunItem item )
	{
		var type = AmmoOf( item );
		return type is null || !inventory.IsValid() ? 0 : System.Math.Max( 0, type.MaxReserve - inventory.GetAmmo( type ) );
	}

	/// <summary>What a pack costs when only <paramref name="rounds"/> of it fit: a part of a pack is a part of the price.</summary>
	public static long PriceFor( GunItem item, int rounds ) =>
		rounds >= item.Amount
			? item.Price
			: System.Math.Max( 1, (long)System.Math.Ceiling( item.Price * (double)rounds / item.Amount ) );

	/// <summary>Buy a weapon or a pack of ammo for <paramref name="buyer"/>. Host only.</summary>
	public static GunPurchase Buy( Connection buyer, GunItem item )
	{
		if ( !Networking.IsHost || buyer is null || item is null )
			return GunPurchase.Fail( "Nothing happens." );

		if ( PlayerWallet.For( buyer ) is not { } wallet )
			return GunPurchase.Fail( "Not enough cash." );

		var inventory = Players.For<WeaponInventory>( buyer );

		if ( !inventory.IsValid() )
			return GunPurchase.Fail( "You have to be alive to buy." );

		return item.Kind == GunItemKind.Weapon
			? BuyWeapon( buyer, wallet, inventory, item )
			: BuyAmmo( buyer, wallet, inventory, item );
	}

	static GunPurchase BuyWeapon( Connection buyer, PlayerWallet wallet, WeaponInventory inventory, GunItem item )
	{
		if ( Carries( inventory, item.Id ) )
			return GunPurchase.Fail( $"You already carry a {item.Name}." );

		if ( !ResourceLibrary.TryGet<PrefabFile>( WeaponInventory.PrefabPath( item.Id ), out _ ) )
			return GunPurchase.Fail( "That isn't available." );

		if ( wallet.Cash < item.Price )
			return GunPurchase.Fail( "Not enough cash." );

		var steamId = (ulong)buyer.SteamId;

		if ( !Transactions.Pay( steamId, Parties.GunStore, TransactionCurrency.Cash, item.Price, TxKind.GunPurchase,
			    $"Gun store: {item.Name}", item.Id ).Ok )
			return GunPurchase.Fail( "Not enough cash." );

		inventory.Pickup( WeaponInventory.PrefabPath( item.Id ) );

		if ( !Carries( inventory, item.Id ) )
		{
			Transactions.Receive( steamId, Parties.GunStore, TransactionCurrency.Cash, item.Price, TxKind.Refund,
				$"Gun store: {item.Name} could not be handed over", item.Id );

			return GunPurchase.Fail( "You can't carry it. You were not charged." );
		}

		StatsLog.Purchase( buyer, "weapon", item.Id, item.Name, item.Price );

		return new GunPurchase( true, $"Bought a {item.Name} for ${item.Price:N0}." );
	}

	static GunPurchase BuyAmmo( Connection buyer, PlayerWallet wallet, WeaponInventory inventory, GunItem item )
	{
		var type = AmmoOf( item );

		if ( type is null )
			return GunPurchase.Fail( "That isn't available." );

		var rounds = System.Math.Min( item.Amount, RoomFor( inventory, item ) );

		if ( rounds <= 0 )
			return GunPurchase.Fail( $"You can't carry any more {type.Title}." );

		var price = PriceFor( item, rounds );

		if ( wallet.Cash < price )
			return GunPurchase.Fail( "Not enough cash." );

		var steamId = (ulong)buyer.SteamId;

		if ( !Transactions.Pay( steamId, Parties.GunStore, TransactionCurrency.Cash, price, TxKind.GunPurchase,
			    $"Gun store: {rounds} {type.Title}", item.Id ).Ok )
			return GunPurchase.Fail( "Not enough cash." );

		var added = inventory.GiveAmmo( type, rounds );

		if ( added <= 0 )
		{
			Transactions.Receive( steamId, Parties.GunStore, TransactionCurrency.Cash, price, TxKind.Refund,
				$"Gun store: {item.Name} could not be handed over", item.Id );

			return GunPurchase.Fail( "You can't carry it. You were not charged." );
		}

		StatsLog.Purchase( buyer, "ammo", item.Id, $"{added} {type.Title}", price );

		return new GunPurchase( true, $"Bought {added} {type.Title} for ${price:N0}." );
	}
}
