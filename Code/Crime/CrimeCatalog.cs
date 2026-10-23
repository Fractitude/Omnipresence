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
	public const int TabsPerRaw = 1;

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

	/// <summary>
	/// Seconds after the deadline that a Shipped order may wait for its parcel before it counts as lost in the post. A
	/// late parcel is still paid for; this only cleans up an order whose parcel never completes (a lost or damaged
	/// save).
	/// </summary>
	public const long ShippedMaxSeconds = 7200;

	/// <summary>Orders a player can have in progress at once.</summary>
	public const int MaxActiveOrders = 5;

	/// <summary>An order asks for a multiple of this many.</summary>
	public const int OrderCountStep = 5;

	public const int OrderCountMin = 10;
	public const int OrderCountMax = 30;

	public const int OrderQualityMin = 30;
	public const int OrderQualityMax = 70;

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

	// The shop in Hush: supplies bought with crypto, delivered to the mailbox.

	/// <summary>The most one purchase can hold.</summary>
	public const int ShopMaxQuantity = 20;

	/// <summary>Deliveries on their way at once.</summary>
	public const int ShopMaxInTransit = 10;

	/// <summary>The quantities the shop offers as chips.</summary>
	public static readonly int[] ShopQuantities = [1, 5, 10];

	/// <summary>One thing the shop sells. Quality 0 means none.</summary>
	public sealed record ShopEntry( string Id, string Kind, int Quality, long Price, long DeliverSeconds );

	// Cached by HotReloadCache: see ItemCatalog.
	static ShopEntry[] shop => HotReloadCache.Get( "CrimeCatalog.shop", BuildShop );

	static ShopEntry[] BuildShop() =>
	[
		new( "static_culture", ItemCatalog.StaticCulture, 0, 40, 120 ),
	];

	public static IReadOnlyList<ShopEntry> Shop => shop;

	/// <summary>The entry with this id, or null: an action's item id is checked here.</summary>
	public static ShopEntry ShopEntryOf( string id ) => shop.FirstOrDefault( x => x.Id == id );

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

	// Buyer personalities. The rating rule (see Stars): a buyer starts at BaseStars; the gap between the quality
	// delivered and the quality asked moves it (a star per PointsPerStar points, as the personality weighs it); a
	// parcel that arrives after the deadline loses stars by how late it is (see StarsLostForLateness); the result is
	// rounded and kept between the personality's MinStars and MaxStars. The deadline only governs sending: a late
	// parcel is still paid for, and only the rating suffers.

	/// <summary>Stars for a batch exactly as good as the ask, delivered on time.</summary>
	public const double BaseStars = 3.0;

	/// <summary>Quality points that move an average buyer by one star, either way.</summary>
	public const double PointsPerStar = 7.5;

	/// <summary>A late parcel loses one star, and one more for every further this many seconds late.</summary>
	public const long LateStepSeconds = 300;

	/// <summary>The most stars an average buyer takes off for lateness.</summary>
	public const int LateMaxStarsLost = 3;

	/// <summary>An Impatient buyer's bonus applies when the parcel arrives within this part of the window.</summary>
	public const double EarlyFraction = 0.25;

	/// <summary>A buyer capped below five stars still gives five when the raw score reaches this.</summary>
	public const double ExceptionalScore = 5.5;

	/// <summary>
	/// How one personality judges. Weight is how often the board shows it (relative). ExtraAsk and PriceBonus (percent
	/// points of the price spread) make the buyer ask for more quality and pay more for it. Tolerance is the shortfall
	/// in quality points that is not noticed; ShortfallPerStar the points past it that cost a star; ExcessWeight
	/// multiplies the stars earned by quality above the ask; Bias moves every score; NoiseChance is the chance of one
	/// star more or less at random; LateWeight multiplies the stars lost for lateness (rounded down); EarlyBonus is
	/// stars for a parcel that arrives within EarlyFraction of the delivery window.
	/// </summary>
	public sealed record PersonalityRules(
		int Weight, int ExtraAsk, int PriceBonus, double Tolerance, double ShortfallPerStar, double ExcessWeight,
		double Bias, double NoiseChance, double LateWeight, double EarlyBonus, int MinStars, int MaxStars );

	// Cached by HotReloadCache: see ItemCatalog. The order of the table is the order of the board's weights.
	static Dictionary<BuyerPersonality, PersonalityRules> ruleTable =>
		HotReloadCache.Get( "CrimeCatalog.rules", BuildRules );

	static Dictionary<BuyerPersonality, PersonalityRules> BuildRules() => new()
	{
		[BuyerPersonality.Layman] = new( 35, 0, 0, 20, 15, 2.0, 0, 0.2, 1, 0, 1, 5 ),
		[BuyerPersonality.Connoisseur] = new( 20, 10, 15, 0, 8, 0.5, 0, 0, 1, 0, 1, 5 ),
		[BuyerPersonality.Impatient] = new( 15, 0, 0, 0, PointsPerStar, 1.0, 0, 0, 2, 1, 1, 5 ),
		[BuyerPersonality.Easygoing] = new( 15, 0, 0, 10, PointsPerStar, 1.0, 1, 0, 0.5, 0, 3, 5 ),
		[BuyerPersonality.Skeptic] = new( 15, 0, 0, 0, PointsPerStar, 1.0, -1, 0, 1, 0, 1, 4 ),
	};

	public static PersonalityRules RulesOf( BuyerPersonality personality ) =>
		ruleTable.TryGetValue( personality, out var rules ) ? rules : ruleTable[BuyerPersonality.Easygoing];

	static IEnumerable<BuyerPersonality> personalities => ruleTable.Keys;

	/// <summary>A personality for a new order, by the weights.</summary>
	public static BuyerPersonality PickPersonality( System.Random random )
	{
		var roll = random.Next( personalities.Sum( x => RulesOf( x ).Weight ) );

		foreach ( var personality in personalities )
		{
			roll -= RulesOf( personality ).Weight;

			if ( roll < 0 )
				return personality;
		}

		return BuyerPersonality.Easygoing;
	}

	/// <summary>
	/// The stars an average buyer takes off for a parcel that arrived this many seconds after the deadline: none on
	/// time, one when late, one more for every further <see cref="LateStepSeconds"/>, at most <see
	/// cref="LateMaxStarsLost"/>.
	/// </summary>
	public static int StarsLostForLateness( long secondsLate ) =>
		secondsLate <= 0 ? 0 : (int)System.Math.Min( LateMaxStarsLost, 1 + (secondsLate - 1) / LateStepSeconds );

	/// <summary>
	/// The stars a buyer gives a parcel that arrived. <paramref name="secondsLate"/> is how long after the deadline it
	/// arrived (zero or less when on time); <paramref name="timeUsed"/> is the part of the delivery window (accept to
	/// deadline) that had passed on arrival, 0 to 1, used only for an Impatient buyer's bonus for a quick delivery.
	/// </summary>
	public static int Stars(
		BuyerPersonality personality, int asked, int delivered, long secondsLate, double timeUsed,
		System.Random random )
	{
		var rules = RulesOf( personality );
		var delta = delivered - asked;
		var score = BaseStars + rules.Bias;

		if ( delta < 0 )
			score -= System.Math.Max( 0, -delta - rules.Tolerance ) / rules.ShortfallPerStar;
		else
			score += delta * rules.ExcessWeight / PointsPerStar;

		if ( secondsLate > 0 )
			score -= (int)System.Math.Floor( StarsLostForLateness( secondsLate ) * rules.LateWeight );
		else if ( timeUsed <= EarlyFraction )
			score += rules.EarlyBonus;

		var stars = (int)System.Math.Floor( score + 0.5 );

		if ( rules.NoiseChance > 0 && random.NextDouble() < rules.NoiseChance )
			stars += random.Next( 2 ) == 0 ? 1 : -1;

		if ( rules.MaxStars < 5 && score < ExceptionalScore )
			stars = System.Math.Min( stars, rules.MaxStars );

		return System.Math.Clamp( stars, System.Math.Max( rules.MinStars, 1 ), 5 );
	}

	// The buyers' notes: written by hand. A personality only shows in how the note sounds, never as a label.

	static readonly string[] laymanNotes =
	[
		"hi, not sure what to look for tbh. a friend said this works? need some for the weekend",
		"first time buying, hope this is the right thing. thanks!!",
		"got told to ask for roughly this much. please be nice, i dont know much about it",
	];

	static readonly string[] connoisseurNotes =
	[
		"Specification is the quality in the listing. I check every batch. Deliver to the number.",
		"Consistency matters to me. I know what a good batch looks like, and I will notice.",
		"Precise count, stated quality, clean packing. Nothing else is needed.",
	];

	static readonly string[] impatientNotes =
	[
		"Need it ASAP. Ship it today, I am not waiting around.",
		"Fast delivery or do not bother. I have other sellers.",
		"Time matters more to me than anything. Send it the moment it is ready.",
	];

	static readonly string[] easygoingNotes =
	[
		"No rush, no stress. Send what you have when you can. Thanks, friend.",
		"Small order for the weekend. Whatever you send will be fine, I am sure.",
		"Happy to wait. Pay is in escrow. Take care.",
	];

	static readonly string[] skepticNotes =
	[
		"Heard bad things about sellers here. Prove me wrong.",
		"Escrow is funded. Do not make me regret it.",
		"I have been burned before. Show me why you are different.",
	];

	// Reviews: written by hand, by personality and by how many stars (high is 4 and 5, mid is 3, low is 1 and 2).

	static readonly string[] laymanHigh =
	[
		"wow this is amazing!! way better than i expected, thank you!!",
		"so good! no idea how you do it. will buy again!",
	];

	static readonly string[] laymanMid = ["seems fine to me? thanks", "ok i guess, does what it says"];
	static readonly string[] laymanLow = ["hmm not really what i hoped, sorry", "kind of disappointing, not sure why"];

	static readonly string[] connoisseurHigh =
	[
		"Exceeds the specification. Noted for future orders.",
		"Accurate and clean. This is how it is done.",
	];

	static readonly string[] connoisseurMid =
		["To specification. Nothing more, nothing less.", "Acceptable. Meets the stated figure."];

	static readonly string[] connoisseurLow =
		["Below the stated quality. Unacceptable.", "Does not match the listing. I measured."];

	static readonly string[] impatientHigh =
		["Fast and good. Exactly how I like it.", "Quick. That is why I will be back."];
	static readonly string[] impatientMid = ["It arrived. Could have been faster.", "Fine, but I have seen quicker."];

	static readonly string[] impatientLow =
		["Far too slow, or not good enough. Done with this seller.", "I waited for this? No."];

	static readonly string[] easygoingHigh =
		["Lovely, thank you so much! Great seller.", "Really nice. Thanks for everything!"];
	static readonly string[] easygoingMid = ["All good, thanks. No complaints.", "Fine by me. Thanks!"];
	static readonly string[] easygoingLow =
		["Not quite what I hoped, but thanks anyway.", "A bit disappointing, to be honest."];

	static readonly string[] skepticHigh =
		["Okay. I was wrong about this one.", "Surprisingly good. Do not let it go to your head."];

	static readonly string[] skepticMid = ["Adequate. I have seen better.", "Nothing special. It arrived."];
	static readonly string[] skepticLow = ["As I expected. Not worth the price.", "Disappointing. Told you."];

	// Reviews for a parcel that came after the deadline: they say it took too long, whatever the stars.

	static readonly string[] laymanLate =
		["it came really late, took forever. the stuff was ok i guess", "waited sooo long for this, kinda annoying"];

	static readonly string[] connoisseurLate =
		["Delivered after the agreed date. The delay is noted and counted against you.",
			"Arrived late. Timekeeping is part of the trade."];

	static readonly string[] impatientLate =
		["Took far too long to arrive. I told you I had no time.", "Late. Very late. Do not expect a good score."];

	static readonly string[] easygoingLate =
		["It took a while to get here, but thanks anyway.", "A bit slow to arrive, no hard feelings."];

	static readonly string[] skepticLate =
		["Late, like I expected. It took too long to deliver.", "Took far too long to arrive. Typical."];

	/// <summary>What the buyer writes when an order never arrived.</summary>
	static readonly string[] cancelled =
	[
		"It never arrived in time. The order is dead.",
		"Waited and waited. Nothing. Do not trust.",
	];

	public static string PickHandle( System.Random random ) => Handles[random.Next( Handles.Length )];

	public static string PickNote( BuyerPersonality personality, System.Random random )
	{
		var pool = personality switch
		{
			BuyerPersonality.Layman => laymanNotes,
			BuyerPersonality.Connoisseur => connoisseurNotes,
			BuyerPersonality.Impatient => impatientNotes,
			BuyerPersonality.Skeptic => skepticNotes,
			_ => easygoingNotes,
		};

		return pool[random.Next( pool.Length )];
	}

	/// <summary>
	/// The review line for a delivered parcel: by the buyer's personality and the stars. A parcel that came after the
	/// deadline gets a line that says it took too long.
	/// </summary>
	public static string PickReview( BuyerPersonality personality, int stars, bool late, System.Random random )
	{
		var pool = late
			? personality switch
			{
				BuyerPersonality.Layman => laymanLate,
				BuyerPersonality.Connoisseur => connoisseurLate,
				BuyerPersonality.Impatient => impatientLate,
				BuyerPersonality.Skeptic => skepticLate,
				_ => easygoingLate,
			}
			: StarPool( personality, stars );

		return pool[random.Next( pool.Length )];
	}

	static string[] StarPool( BuyerPersonality personality, int stars ) =>
		(personality, stars >= 4 ? 2 : stars == 3 ? 1 : 0) switch
		{
			(BuyerPersonality.Layman, 2) => laymanHigh,
			(BuyerPersonality.Layman, 1) => laymanMid,
			(BuyerPersonality.Layman, _) => laymanLow,
			(BuyerPersonality.Connoisseur, 2) => connoisseurHigh,
			(BuyerPersonality.Connoisseur, 1) => connoisseurMid,
			(BuyerPersonality.Connoisseur, _) => connoisseurLow,
			(BuyerPersonality.Impatient, 2) => impatientHigh,
			(BuyerPersonality.Impatient, 1) => impatientMid,
			(BuyerPersonality.Impatient, _) => impatientLow,
			(BuyerPersonality.Skeptic, 2) => skepticHigh,
			(BuyerPersonality.Skeptic, 1) => skepticMid,
			(BuyerPersonality.Skeptic, _) => skepticLow,
			(_, 2) => easygoingHigh,
			(_, 1) => easygoingMid,
			_ => easygoingLow,
		};

	/// <summary>The review line for an order that never arrived.</summary>
	public static string PickCancelReview( System.Random random ) => cancelled[random.Next( cancelled.Length )];

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
