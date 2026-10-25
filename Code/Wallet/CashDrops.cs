namespace Sandbox;

/// <summary>
/// Dollars as a thing in the world. A player drops an amount of cash: the dollars leave the wallet through
/// <see cref="Transactions"/> (to the <see cref="Parties.World"/>, kind <see cref="TxKind.DropCash"/>) and become a
/// <see cref="WorldItem"/> of kind <see cref="ItemCatalog.Cash"/> carrying the amount. Picking it up (see
/// <see cref="Pickup"/>, called by <see cref="WorldItem"/>) puts the dollars back with
/// <see cref="PlayerWallet.GrantUntracked"/> and <see cref="PlayerWallet.UntrackedReason.GroundPickup"/>: that is the
/// one exempt path, because whoever picks the pile up is not necessarily who dropped it. Cash never enters the bag.
/// Only cash can be dropped: crypto and credits are digital. All of it runs on the host. See <c>docs/currency.md</c>.
/// </summary>
public static class CashDrops
{
	// Not readonly: a hot reload would reset a readonly dictionary of plain types.
	static Dictionary<ulong, List<float>> recent = new();

	/// <summary>
	/// The player asks to drop an amount. Only the amount is sent; the host takes the player from the connection.
	/// </summary>
	[Rpc.Host]
	public static void DropRpc( long amount )
	{
		if ( Rpc.Caller is not { } caller )
			return;

		var result = Drop( caller, amount );

		if ( Players.For<PlayerCrime>( caller ) is { } crime )
			crime.Tell( result );
	}

	/// <summary>
	/// Drop <paramref name="amount"/> dollars from a player's wallet in front of them. All or nothing: the amount is
	/// taken only when the pile is made, and given back if it cannot be. Host only.
	/// </summary>
	public static CrimeResult Drop( Connection who, long amount )
	{
		if ( !Networking.IsHost || who is null )
			return CrimeResult.Fail( "Only the host drops cash." );

		if ( Players.For<PlayerWallet>( who ) is not { } wallet || wallet.SteamId == 0 )
			return CrimeResult.Fail( "Your wallet isn't ready." );

		if ( Players.For<Player>( who ) is { Health: < 1 } )
			return CrimeResult.Fail( "You can't drop cash now." );

		if ( PlayerData.For( who ) is { InWired: true } )
			return CrimeResult.Fail( "Cash only exists in the real world." );

		if ( CashRules.CheckDrop( amount, wallet.Cash ) is { } refusal )
			return CrimeResult.Fail( refusal );

		if ( WorldItem.CountInScene( wallet.Scene ) >= WorldItem.MaxInScene )
			return CrimeResult.Fail( "There is too much lying around to drop more." );

		if ( RateLimited( wallet.SteamId ) )
			return CrimeResult.Fail( "You are dropping cash too fast." );

		var paid = Transactions.Pay( wallet.SteamId, Parties.World, TransactionCurrency.Cash, amount, TxKind.DropCash );

		if ( !paid.Ok )
			return CrimeResult.Fail( paid.Message );

		var pile = ItemCatalog.MakeCash( amount );

		if ( WorldItem.SpawnFrom( wallet.GameObject, pile ) is null )
		{
			Refund( wallet, amount );
			return CrimeResult.Fail( "There is no room to drop it here." );
		}

		// The pile and the wallet change together, so the pile is written now rather than a few seconds from now: a
		// crash in between would otherwise lose the dollars. (A pickup does the same.)
		WorldItemRegistry.Flush();
		Record( wallet.SteamId );
		return CrimeResult.Success( $"You dropped {CashRules.Format( amount )}." );
	}

	/// <summary>
	/// A player picks up a pile of cash (<paramref name="pile"/>, kind cash): as many dollars as fit under the wallet's
	/// cap go into the wallet. Returns how many were taken; the caller keeps the rest on the ground. Host only.
	/// </summary>
	public static long Pickup( PlayerWallet wallet, ItemData pile )
	{
		if ( !Networking.IsHost || wallet is null || pile is not { Kind: ItemCatalog.Cash } || wallet.SteamId == 0 )
			return 0;

		var (taken, _) = CashRules.Pickup( pile.Amount, wallet.Cash );

		if ( taken < 1 )
			return 0;

		var before = wallet.Cash;
		wallet.GrantUntracked( TransactionCurrency.Cash, taken, PlayerWallet.UntrackedReason.GroundPickup );
		return System.Math.Min( taken, wallet.Cash - before );
	}

	static void Refund( PlayerWallet wallet, long amount )
	{
		var back = Transactions.Receive( wallet.SteamId, Parties.World, TransactionCurrency.Cash, amount, TxKind.Refund,
			"Cash could not be dropped" );

		// The player had the dollars a moment ago, so the cap cannot refuse. If it somehow does, still never lose them.
		if ( !back.Ok )
		{
			Log.Warning( $"Cash drop refund through Transactions failed ({back.Message}); granting {amount} directly." );
			wallet.GrantUntracked( TransactionCurrency.Cash, amount, PlayerWallet.UntrackedReason.GroundPickup );
		}
	}

	static bool RateLimited( ulong steamId )
	{
		var now = Time.Now;

		if ( !recent.TryGetValue( steamId, out var times ) )
			recent[steamId] = times = new();

		times.RemoveAll( x => now - x > 60f );
		return times.Count >= CashRules.DropsPerMinute;
	}

	static void Record( ulong steamId )
	{
		if ( recent.TryGetValue( steamId, out var times ) )
			times.Add( Time.Now );
	}
}
