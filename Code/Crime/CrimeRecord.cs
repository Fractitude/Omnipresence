namespace Sandbox;

/// <summary>
/// Everything a player has in the crime game, in one section of the <see cref="ProgressRecord"/> so a season wipe can
/// reset it alone without touching XP, hacking level, credits or cosmetics. Plain data, saved with the record.
/// </summary>
public sealed class CrimeRecord
{
	/// <summary>The newest reviews kept.</summary>
	public const int MaxReviews = 20;

	/// <summary>Orders, parcels and cash-outs kept in one list.</summary>
	public const int MaxEntries = 50;

	public const int MaxTrays = 20;
	public const int MaxIdLength = 64;

	/// <summary>
	/// The id of the mailbox object this player claimed, or empty. A box is claimed by the first player who uses it, and
	/// the claim is remembered here so it holds across restarts.
	/// </summary>
	public string MailboxId { get; set; } = "";

	/// <summary>
	/// True once the player has been given their starting stock, so it is given only once.
	/// </summary>
	public bool Started { get; set; }

	/// <summary>
	/// The crime items in the player's inventory (batches with a kind), as they were the last time the inventory changed.
	/// The inventory itself isn't saved, so this is how seeds, raw, products and parcels survive a restart.
	/// </summary>
	public List<ItemData> Items { get; set; } = new();

	/// <summary>
	/// What the player keeps in each storage box, by the box's id. A separate section of the record, so a wipe of the
	/// crime state resets it too. See <see cref="StorageRules"/>.
	/// </summary>
	public List<StorageBoxRecord> Storage { get; set; } = new();

	public List<TrayRecord> Trays { get; set; } = new();

	/// <summary>Orders the player accepted, newest last. See <see cref="OrderState"/>.</summary>
	public List<OrderRecord> Orders { get; set; } = new();

	/// <summary>Parcels sent, newest last.</summary>
	public List<ParcelRecord> Parcels { get; set; } = new();

	/// <summary>Supplies bought in the Hush shop, on their way or waiting in the mailbox. Newest last.</summary>
	public List<DeliveryRecord> Deliveries { get; set; } = new();

	/// <summary>Crypto cashed out into dollars that has not arrived yet.</summary>
	public List<CashoutRecord> Cashouts { get; set; } = new();

	/// <summary>The newest reviews, oldest first.</summary>
	public List<ReviewRecord> Reviews { get; set; } = new();

	/// <summary>Trades that ended well.</summary>
	public int GoodTrades { get; set; }

	/// <summary>Trades that ended badly (an order missed its deadline).</summary>
	public int BadTrades { get; set; }

	/// <summary>Sum of the stars of every review, and how many there are.</summary>
	public int RatingSum { get; set; }

	public int RatingCount { get; set; }

	/// <summary>Counts up so each order and parcel the player makes has its own id.</summary>
	public int NextId { get; set; } = 1;

	/// <summary>
	/// Make a loaded section safe: a file is outside input, and can hold anything.
	/// </summary>
	public void Sanitize()
	{
		MailboxId = Clean( MailboxId );

		Items ??= new();
		Items.RemoveAll( x => x is null || ItemCatalog.Find( x.Kind ) is null || x.Count < 1 );
		Items = Items
			.Select( x => ItemCatalog.Make( x.Kind, x.Count, x.Quality, Clean( x.OrderId ) ) )
			.Select( ( made, i ) =>
			{
				made.Contents = CleanContents( Items[i].Contents );
				return made;
			} )
			.Take( 200 )
			.ToList();

		Storage = StorageRules.Clean( Storage );

		Trays ??= new();
		Trays.RemoveAll( x => x is null || string.IsNullOrWhiteSpace( x.Id ) || x.Id.Length > MaxIdLength );
		Trays = Trays.GroupBy( x => x.Id ).Select( x => x.First() ).Take( MaxTrays ).ToList();

		// A tray saved before there were two drugs has no product: it holds Static. An unknown one does too.
		foreach ( var tray in Trays )
			tray.Kind = ProductCatalog.OfTray( tray.Kind ).Id;

		Orders ??= new();
		Orders.RemoveAll( x => x is null || string.IsNullOrWhiteSpace( x.Id ) || ItemCatalog.Find( x.Kind ) is null );
		Orders = Orders.TakeLast( MaxEntries ).ToList();

		foreach ( var order in Orders )
		{
			order.Handle = Clean( order.Handle );
			order.Text = Clean( order.Text, 200 );
			order.Count = System.Math.Clamp( order.Count, 1, ItemCatalog.MaxCount );
			order.MinQuality = System.Math.Clamp( order.MinQuality, 0, 100 );
			order.PriceEach = System.Math.Clamp( order.PriceEach, 0, 1_000_000 );
			order.PackedQuality = System.Math.Clamp( order.PackedQuality, 0, 100 );

			// An old save has only the old buyer type: an honest buyer is easygoing, a picky one a connoisseur.
			if ( !System.Enum.IsDefined( order.Personality ) || order.Personality == BuyerPersonality.None )
			{
				order.Personality = order.Buyer == BuyerType.Picky
					? BuyerPersonality.Connoisseur
					: BuyerPersonality.Easygoing;
			}
		}

		Parcels ??= new();
		Parcels.RemoveAll( x => x is null || string.IsNullOrWhiteSpace( x.Id ) || ItemCatalog.Find( x.Kind ) is null );

		// Parcels of orders still on their way are the last thing dropped when the list is trimmed.
		var inFlight = Parcels
			.Where( p => Orders.Any( o => o.Id == p.OrderId && o.State == OrderState.Shipped ) )
			.ToList();

		var others = Parcels
			.Except( inFlight )
			.TakeLast( System.Math.Max( 0, MaxEntries - inFlight.Count ) )
			.ToList();

		Parcels = Parcels.Where( p => inFlight.Contains( p ) || others.Contains( p ) ).ToList();

		Deliveries ??= new();
		Deliveries.RemoveAll(
			x => x is null || string.IsNullOrWhiteSpace( x.Id ) || ItemCatalog.Find( x.Kind ) is null );
		Deliveries = Deliveries.TakeLast( MaxEntries ).ToList();

		foreach ( var delivery in Deliveries )
		{
			delivery.Id = Clean( delivery.Id );
			delivery.Count = System.Math.Clamp( delivery.Count, 1, ItemCatalog.MaxCount );
			delivery.Quality = System.Math.Clamp( delivery.Quality, 0, 100 );
			delivery.State = System.Enum.IsDefined( delivery.State ) ? delivery.State : DeliveryState.InMailbox;
		}

		Cashouts ??= new();
		Cashouts.RemoveAll( x => x is null || x.Cash <= 0 );
		Cashouts = Cashouts.TakeLast( MaxEntries ).ToList();

		Reviews ??= new();
		Reviews.RemoveAll( x => x is null );
		Reviews = Reviews.TakeLast( MaxReviews ).ToList();

		foreach ( var review in Reviews )
		{
			review.Handle = Clean( review.Handle );
			review.Text = Clean( review.Text, 200 );
			review.Stars = System.Math.Clamp( review.Stars, 1, 5 );
		}

		GoodTrades = System.Math.Max( GoodTrades, 0 );
		BadTrades = System.Math.Max( BadTrades, 0 );
		RatingCount = System.Math.Max( RatingCount, 0 );
		RatingSum = System.Math.Clamp( RatingSum, 0, RatingCount * 5 );
		NextId = System.Math.Max( NextId, 1 );
	}

	/// <summary>The most entries a parcel's contents keep.</summary>
	public const int MaxContents = 20;

	/// <summary>
	/// A parcel's contents from a file: capped, counts and qualities clamped. An unknown kind is kept as stored (it
	/// shows by its stored name); an empty one is dropped.
	/// </summary>
	internal static List<ItemContent> CleanContents( List<ItemContent> contents ) => (contents ?? [])
		.Where( x => x is not null && !string.IsNullOrWhiteSpace( x.Kind ) )
		.Take( MaxContents )
		.Select( x => new ItemContent( Clean( x.Kind ), System.Math.Clamp( x.Count, 1, ItemCatalog.MaxCount ),
			System.Math.Clamp( x.Quality, 0, 100 ) ) )
		.ToList();

	static string Clean( string text, int max = MaxIdLength ) =>
		string.IsNullOrWhiteSpace( text ) ? "" : text.Length > max ? text[..max] : text;
}

/// <summary>
/// One grow tray of the player's, by the id of the tray object in the world. Absent from the list means empty.
/// </summary>
public sealed class TrayRecord
{
	public string Id { get; set; } = "";

	/// <summary>
	/// The id of the drug planted (<see cref="ProductDef.Id"/>). Empty in a tray saved before there were two drugs:
	/// that is Static (<see cref="ProductCatalog.OfTray"/>, and <see cref="CrimeRecord.Sanitize"/> fills it in).
	/// </summary>
	public string Kind { get; set; } = "";

	/// <summary>False when empty. A planted tray is growing until <see cref="ReadyAt"/>, then ready to harvest.</summary>
	public bool Planted { get; set; }

	/// <summary>Game seconds (<see cref="WorldClock.Now"/>) when the harvest is ready.</summary>
	public long ReadyAt { get; set; }

	/// <summary>The quality of the crop, fixed when it was planted.</summary>
	public int Quality { get; set; }
}

/// <summary>
/// The old hidden buyer type. Only read from old saves, which are moved to a <see cref="BuyerPersonality"/> when
/// loaded.
/// </summary>
public enum BuyerType
{
	Honest,
	Picky,
}

/// <summary>
/// How a buyer judges what they get. Hidden: it is never in the Hush state, the packing panel or anything synced. The
/// numbers are in <see cref="CrimeCatalog"/>.
/// </summary>
public enum BuyerPersonality
{
	/// <summary>Not set. Only an old order that has not been moved yet.</summary>
	None,

	/// <summary>Not an expert: doesn't notice a small shortfall, and is delighted by more than was asked.</summary>
	Layman,

	/// <summary>Knows exactly what they want: no tolerance, little extra for excess.</summary>
	Connoisseur,

	/// <summary>Cares about speed: lateness costs double and early delivery earns a bonus.</summary>
	Impatient,

	/// <summary>Generous: tolerates some shortfall and rarely rates low.</summary>
	Easygoing,

	/// <summary>Stingy: rates lower and hardly ever gives five stars.</summary>
	Skeptic,
}

public enum OrderState
{
	/// <summary>Accepted, escrow held. Waiting to be packed.</summary>
	Accepted,

	/// <summary>Packed into a parcel that has not been sent.</summary>
	Packed,

	/// <summary>The parcel was sent and is on its way.</summary>
	Shipped,

	/// <summary>The parcel arrived. The buyer has reviewed it and escrow releases at <see cref="OrderRecord.ReleaseAt"/>.</summary>
	Delivered,

	/// <summary>Escrow was released: the player was paid.</summary>
	Released,

	/// <summary>Missed its deadline. Costs reputation.</summary>
	Cancelled,
}

/// <summary>
/// An order the player accepted: the terms are copied here so the open list can move on without it.
/// </summary>
public sealed class OrderRecord
{
	public string Id { get; set; } = "";
	public string Handle { get; set; } = "";

	/// <summary>The old buyer type. Read only to move an old save to a <see cref="Personality"/> in Sanitize.</summary>
	public BuyerType Buyer { get; set; }

	/// <summary>How the buyer rates. Host only; never shown to the player.</summary>
	public BuyerPersonality Personality { get; set; }

	/// <summary>The buyer's note when the order was posted.</summary>
	public string Text { get; set; } = "";

	public string Kind { get; set; } = "";
	public int Count { get; set; }

	/// <summary>The quality the buyer asked for: a guide for the rating, not a requirement. Saved as MinQuality.
	/// </summary>
	public int MinQuality { get; set; }

	/// <summary>Crypto per item.</summary>
	public long PriceEach { get; set; }

	/// <summary>Game seconds. The parcel must arrive by then.</summary>
	public long Deadline { get; set; }

	public OrderState State { get; set; }

	/// <summary>The quality of the batch that was packed, set when packing. The review is judged on it.</summary>
	public int PackedQuality { get; set; }

	/// <summary>Game seconds when escrow releases, after delivery.</summary>
	public long ReleaseAt { get; set; }

	/// <summary>Crypto paid when escrow released, after the fee.</summary>
	public long Paid { get; set; }

	public long Total => PriceEach * Count;
}

public enum ParcelState
{
	/// <summary>In the mailbox, waiting for the mailman.</summary>
	Waiting,

	/// <summary>The mailman has it.</summary>
	InTransit,

	Delivered,
}

public enum DeliveryState
{
	/// <summary>Bought, on its way to the mailbox.</summary>
	InTransit,

	/// <summary>Arrived. Waits in the mailbox until the player collects it.</summary>
	InMailbox,
}

/// <summary>
/// Something the player bought in the Hush shop. It travels, then waits in their mailbox; collecting it puts it in the
/// bag.
/// </summary>
public sealed class DeliveryRecord
{
	public string Id { get; set; } = "";
	public string Kind { get; set; } = "";
	public int Count { get; set; }
	public int Quality { get; set; }

	public DeliveryState State { get; set; }

	/// <summary>Game seconds when it reaches the mailbox.</summary>
	public long ArriveAt { get; set; }
}

public sealed class ParcelRecord
{
	public string Id { get; set; } = "";
	public string OrderId { get; set; } = "";
	public string Kind { get; set; } = "";
	public int Count { get; set; }
	public int Quality { get; set; }

	public ParcelState State { get; set; }

	/// <summary>Game seconds when it was sent, when the mailman takes it, and when it arrives.</summary>
	public long SentAt { get; set; }

	public long CollectAt { get; set; }
	public long DeliverAt { get; set; }
}

/// <summary>
/// Crypto sold for dollars. The crypto is gone when the player asks; the dollars arrive at <see cref="ReadyAt"/>.
/// </summary>
public sealed class CashoutRecord
{
	public long Cash { get; set; }
	public long ReadyAt { get; set; }
}

public sealed class ReviewRecord
{
	public long At { get; set; }
	public string Handle { get; set; } = "";
	public int Stars { get; set; }
	public string Text { get; set; } = "";
}
