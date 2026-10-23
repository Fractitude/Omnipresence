namespace Sandbox;

/// <summary>
/// An open order on the board: an NPC buyer wants a batch. Not saved. A player who accepts it gets a copy in their
/// <see cref="CrimeRecord"/> and it leaves the board.
/// </summary>
public sealed class MarketOrder
{
	public string Id { get; init; } = "";
	public string Handle { get; init; } = "";
	public BuyerPersonality Personality { get; init; }
	public string Text { get; init; } = "";
	public string Kind { get; init; } = "";
	public int Count { get; init; }
	/// <summary>The quality the buyer asks for. A guide for the rating, not a requirement.</summary>
	public int MinQuality { get; init; }

	/// <summary>Crypto per item.</summary>
	public long PriceEach { get; init; }

	/// <summary>Game seconds when it leaves the board.</summary>
	public long ExpiresAt { get; init; }

	public long Total => PriceEach * Count;
}

/// <summary>
/// The open orders of the NPC buyers, on the host. It keeps a few on the board, retires old ones and hands one to the
/// first player who takes it. Nothing here is saved: the board starts fresh at every start, and what a player accepted
/// lives in their <see cref="CrimeRecord"/>.
/// </summary>
/// <remarks>
/// Host only. Its state is in instance fields, which a hot reload keeps (see AGENTS.md, "Static state and hot
/// reload"). A <c>new Scene()</c> starts every system, so a system that isn't in the active scene does nothing.
/// </remarks>
public sealed class MarketSystem : GameObjectSystem<MarketSystem>
{
	readonly List<MarketOrder> open = new();
	readonly System.Random random = new();

	long nextRefill;
	bool filled;

	public MarketSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.StartUpdate, 0, Tick, "MarketSystem.Tick" );
	}

	/// <summary>
	/// The orders on the board, soonest to leave first. A copy.
	/// </summary>
	public IReadOnlyList<MarketOrder> Open => open.OrderBy( x => x.ExpiresAt ).ToList();

	/// <summary>
	/// The order with this id, or null if it is gone.
	/// </summary>
	public MarketOrder Find( string id ) => open.FirstOrDefault( x => x.Id == id );

	/// <summary>
	/// Take an order off the board for the player who accepts it. Returns null if it was already taken or has expired.
	/// </summary>
	public MarketOrder TryTake( string id )
	{
		if ( Find( id ) is not { } order || order.ExpiresAt <= WorldClock.Now )
			return null;

		open.Remove( order );
		return order;
	}

	/// <summary>
	/// Put new orders on the board now, even above the usual number. For testing.
	/// </summary>
	public void Fill( int count )
	{
		for ( var i = 0; i < count; i++ )
			open.Add( Generate() );
	}

	/// <summary>
	/// Clear the board. For testing.
	/// </summary>
	public void Clear() => open.Clear();

	void Tick()
	{
		if ( Scene != Game.ActiveScene || !Game.IsPlaying || Scene.IsEditor || !Networking.IsHost )
			return;

		var now = WorldClock.Now;

		if ( !filled )
		{
			filled = true;
			Fill( CrimeCatalog.MinOpenOrders );
			nextRefill = now + CrimeCatalog.RefillSeconds;
		}

		open.RemoveAll( x => x.ExpiresAt <= now );

		if ( open.Count < CrimeCatalog.MaxOpenOrders && now >= nextRefill )
		{
			open.Add( Generate() );
			nextRefill = now + CrimeCatalog.RefillSeconds;
		}
		else if ( open.Count < CrimeCatalog.MinOpenOrders )
		{
			open.Add( Generate() );
		}
	}

	/// <summary>
	/// A new order for a drug picked by the weights in <see cref="ProductCatalog"/>, from a buyer with a personality
	/// picked by the weights in <see cref="CrimeCatalog"/>. The count, the ask and the price come from the drug. Some
	/// buyers ask for more quality and pay a little more for it.
	/// </summary>
	MarketOrder Generate()
	{
		var personality = CrimeCatalog.PickPersonality( random );
		var rules = CrimeCatalog.RulesOf( personality );
		var product = ProductCatalog.Pick( random );
		var kind = product.Product.Id;
		var step = System.Math.Max( product.OrderCountStep, 1 );
		var steps = System.Math.Max( product.OrderCountMax - product.OrderCountMin, 0 ) / step;
		var count = product.OrderCountMin + random.Next( steps + 1 ) * step;

		var quality = random.Next( product.OrderQualityMin, product.OrderQualityMax + 1 );
		var spread = random.Next( product.PriceSpreadMin, product.PriceSpreadMax + 1 );

		quality = System.Math.Min( quality + rules.ExtraAsk, 95 );
		spread += rules.PriceBonus;

		var price = System.Math.Max( 1, product.Product.BasePrice * spread / 100 );

		return new MarketOrder
		{
			Id = System.Guid.NewGuid().ToString( "N" )[..8],
			Handle = CrimeCatalog.PickHandle( random ),
			Personality = personality,
			Text = CrimeCatalog.PickNote( personality, random ),
			Kind = kind,
			Count = count,
			MinQuality = quality,
			PriceEach = price,
			ExpiresAt = WorldClock.Now + CrimeCatalog.OpenSeconds,
		};
	}
}
