namespace Sandbox;

/// <summary>
/// Every number the first crime build tunes, and every line of text the game writes for buyers and reviews, in one
/// place. All times are game seconds (<see cref="WorldClock"/>, one real second each). The text is written by hand and
/// picked from small pools: there is no generated text.
/// </summary>
public static class CrimeCatalog
{
	// Growing and pressing (placeholder gameplay: the minigames come later).

	/// <summary>Seconds a planted tray takes to be ready.</summary>
	public const long GrowSeconds = 10;

	/// <summary>Raw Static one tray gives.</summary>
	public const int HarvestRaw = 12;

	/// <summary>Cultures a harvest gives back, so the loop can go on.</summary>
	public const int HarvestCultures = 1;

	/// <summary>Cultures a new player starts with.</summary>
	public const int StartCultures = 2;

	/// <summary>Placeholder crop quality, picked when planting.</summary>
	public const int GrowQualityMin = 40;

	public const int GrowQualityMax = 85;

	/// <summary>Tabs one raw makes at the press bench.</summary>
	public const int TabsPerRaw = 4;

	/// <summary>Most the press moves the quality either way.</summary>
	public const int PressQualitySpread = 10;

	// The mailman and the postal clock.

	/// <summary>The mailman collects at every multiple of this on the game clock.</summary>
	public const long CollectPeriod = 300;

	/// <summary>Seconds from collection to delivery.</summary>
	public const long TransitSeconds = 180;

	/// <summary>Seconds escrow is held after delivery before it releases.</summary>
	public const long EscrowSeconds = 60;

	/// <summary>Parcels waiting or on their way at once.</summary>
	public const int MaxParcels = 10;

	// The market.

	/// <summary>The market refills up to this many open orders.</summary>
	public const int MaxOpenOrders = 5;

	/// <summary>It refills right away to this many; above it, one new order per <see cref="RefillSeconds"/>.</summary>
	public const int MinOpenOrders = 3;

	public const long RefillSeconds = 30;

	/// <summary>Seconds an open order stays on the board.</summary>
	public const long OpenSeconds = 1800;

	/// <summary>Seconds a player has to deliver after accepting.</summary>
	public const long DeliverWindowSeconds = 3600;

	/// <summary>Orders a player can have in progress at once.</summary>
	public const int MaxActiveOrders = 5;

	/// <summary>An order asks for a multiple of this many.</summary>
	public const int OrderCountStep = 5;

	public const int OrderCountMin = 10;
	public const int OrderCountMax = 30;

	public const int OrderQualityMin = 30;
	public const int OrderQualityMax = 70;

	/// <summary>Picky buyers want at least this much more quality.</summary>
	public const int PickyExtraQuality = 10;

	/// <summary>The price range around an item's base price, in percent.</summary>
	public const int PriceSpreadMin = 80;

	public const int PriceSpreadMax = 130;

	// Reputation. The fee is a percent of what escrow pays.

	public const int KnownTrades = 3;
	public const int TrustedTrades = 10;
	public const double TrustedMinRating = 4.0;

	public const int FeeNewcomer = 10;
	public const int FeeKnown = 7;
	public const int FeeTrusted = 4;

	/// <summary>The stars a buyer gives an order that never arrived.</summary>
	public const int CancelStars = 1;

	// The Exchange. The base rate is fixed at one to one.

	/// <summary>Percent kept by the Exchange when crypto is sold for dollars.</summary>
	public const int SellFeePercent = 8;

	/// <summary>Percent kept by the Exchange when dollars are changed into crypto.</summary>
	public const int BuyFeePercent = 4;

	/// <summary>Seconds before cashed-out dollars arrive.</summary>
	public const long CashoutDelaySeconds = 120;

	/// <summary>The most one Exchange request moves.</summary>
	public const long MaxExchange = 1_000_000;

	public static readonly string[] Handles =
	[
		"ghost_lantern", "tin_saint", "velvet_moth", "n0_receipt", "pale_ferry", "salt_and_static",
		"quiet_harbor", "paper_wolf", "night_ledger", "low_orbit", "cold_antler", "mothbox",
	];

	static string[] honestNotes =
	[
		"Need a batch for the weekend. Pay is clean, ship it quiet.",
		"Regular buyer, small order. Send it when you can.",
		"Looking for tabs, nothing fancy. Escrow is funded.",
		"Friends are visiting. I pay on time, always.",
		"First time ordering here. Be gentle.",
	];

	static string[] pickyNotes =
	[
		"Quality matters. Anything below what I ask for gets sent back.",
		"I tested the last seller's batch. Never again. Pure or nothing.",
		"Don't waste my time with cut product.",
		"I read every review. Mine will be honest.",
		"High standards, fair price. Deliver what the listing says.",
	];

	static string[] fiveStar =
	[
		"Exactly as described. Fast and clean.",
		"Better than the listing. Will order again.",
		"Perfect. Seller knows the craft.",
	];

	static string[] fourStar =
	[
		"Good batch, arrived fine. Would buy again.",
		"Solid. Nothing to complain about.",
		"Does the job. Fair deal.",
	];

	static string[] threeStar =
	[
		"Okay. It matches the listing, barely.",
		"Average. I expected a little more.",
	];

	static string[] twoStar =
	[
		"Right on the edge of what I asked. Not impressed.",
		"Barely made the quality. Won't rush back.",
	];

	static string[] oneStar =
	[
		"It never arrived in time. The order is dead.",
		"Waited and waited. Nothing. Do not trust.",
	];

	public static string PickHandle( System.Random random ) => Handles[random.Next( Handles.Length )];

	public static string PickNote( BuyerType type, System.Random random )
	{
		var pool = type == BuyerType.Picky ? pickyNotes : honestNotes;
		return pool[random.Next( pool.Length )];
	}

	public static string PickReview( int stars, System.Random random )
	{
		var pool = stars switch
		{
			>= 5 => fiveStar,
			4 => fourStar,
			3 => threeStar,
			2 => twoStar,
			_ => oneStar,
		};

		return pool[random.Next( pool.Length )];
	}

	/// <summary>The fee percent a player pays on escrow, from their reputation.</summary>
	public static int FeeFor( CrimeRecord crime ) => LevelOf( crime ) switch
	{
		ReputationLevel.Trusted => FeeTrusted,
		ReputationLevel.Known => FeeKnown,
		_ => FeeNewcomer,
	};

	public static ReputationLevel LevelOf( CrimeRecord crime )
	{
		var average = crime.RatingCount == 0 ? 0 : crime.RatingSum / (double)crime.RatingCount;

		if ( crime.GoodTrades >= TrustedTrades && average >= TrustedMinRating )
			return ReputationLevel.Trusted;

		return crime.GoodTrades >= KnownTrades ? ReputationLevel.Known : ReputationLevel.Newcomer;
	}

	/// <summary>The next time the mailman comes, after <paramref name="now"/>.</summary>
	public static long NextCollection( long now ) => (now / CollectPeriod + 1) * CollectPeriod;

	/// <summary>A player's dollars after selling this much crypto at the Exchange.</summary>
	public static long CashFor( long crypto ) => crypto * (100 - SellFeePercent) / 100;

	/// <summary>The crypto after changing this many dollars at the Exchange.</summary>
	public static long CryptoFor( long cash ) => cash * (100 - BuyFeePercent) / 100;
}

public enum ReputationLevel
{
	Newcomer,
	Known,
	Trusted,
}
