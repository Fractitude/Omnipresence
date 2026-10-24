namespace Sandbox;

/// <summary>
/// One drug: the three items of its chain (seed, raw, product) and every number that tunes it. The grow tray, the press
/// bench, the market, the Hush shop and the inventory all read this, so a new drug is one more entry in
/// <see cref="ProductCatalog"/> and nothing else. Times are game seconds.
/// </summary>
public sealed record ProductDef
{
	/// <summary>The saved and sent id, like "static". Never rename one: a tray remembers it.</summary>
	public string Id { get; init; } = "";

	/// <summary>
	/// The drug's name, like "Static". It is also the category of its three items (the Hush order filter).
	/// </summary>
	public string Name { get; init; } = "";

	/// <summary>What is planted. Bought in the Hush shop.</summary>
	public ItemDef Seed { get; init; }

	/// <summary>What a harvest gives. Pressed at the press bench.</summary>
	public ItemDef Raw { get; init; }

	/// <summary>What is pressed, packed and sold. Its <see cref="ItemDef.BasePrice"/> is the price of one.</summary>
	public ItemDef Product { get; init; }

	/// <summary>Seconds a planted tray takes to be ready.</summary>
	public long GrowSeconds { get; init; }

	/// <summary>Raw one harvest gives.</summary>
	public int HarvestRaw { get; init; }

	/// <summary>Seeds a harvest gives back, so the loop can go on.</summary>
	public int HarvestSeeds { get; init; } = 1;

	/// <summary>Seeds a new player starts with.</summary>
	public int StartSeeds { get; init; }

	/// <summary>Placeholder crop quality, picked at random when planting.</summary>
	public int CropQualityMin { get; init; } = 40;

	public int CropQualityMax { get; init; } = 85;

	/// <summary>Raw one product takes at the press bench.</summary>
	public int RawPerProduct { get; init; } = 1;

	/// <summary>Most the press moves the quality either way (placeholder for the Tuning minigame).</summary>
	public int PressQualitySpread { get; init; } = 10;

	/// <summary>How often the board shows this drug, relative to the others.</summary>
	public int OrderWeight { get; init; } = 1;

	/// <summary>An order asks for a count from min to max, in steps.</summary>
	public int OrderCountMin { get; init; } = 1;

	public int OrderCountMax { get; init; } = 1;
	public int OrderCountStep { get; init; } = 1;

	/// <summary>The quality range buyers ask for (before the personality adds to it).</summary>
	public int OrderQualityMin { get; init; } = 30;

	public int OrderQualityMax { get; init; } = 70;

	/// <summary>The price range around the product's base price, in percent.</summary>
	public int PriceSpreadMin { get; init; } = 80;

	public int PriceSpreadMax { get; init; } = 130;

	/// <summary>What the Hush shop charges for one seed, in crypto, and the seconds it takes to arrive.</summary>
	public long SeedPrice { get; init; }

	public long SeedDeliverSeconds { get; init; }

	/// <summary>Products one harvest makes when all its raw is pressed.</summary>
	public int ProductsPerHarvest => HarvestRaw / System.Math.Max( RawPerProduct, 1 );
}

/// <summary>
/// The drugs of the game, one <see cref="ProductDef"/> each, and the lookups by id and by item kind. The items of every
/// drug come from here (<see cref="ItemCatalog"/> lists them), the Hush shop sells every seed (<see
/// cref="CrimeCatalog.Shop"/>), and the market makes orders for them by their weights. To add a drug: add an entry to
/// <see cref="BuildProducts"/> and its three item ids to <see cref="ItemCatalog"/>; nothing else needs to change.
/// </summary>
public static class ProductCatalog
{
	/// <summary>The id of Static. A tray saved before there were two drugs holds this.</summary>
	public const string StaticId = "static";

	public const string HazeId = "haze";

	// Cached by HotReloadCache, not held in a static field: see ItemCatalog.
	static ProductDef[] products => HotReloadCache.Get( "ProductCatalog.products", BuildProducts );

	static ProductDef[] BuildProducts() =>
	[
		With( new ProductDef
		{
			Id = StaticId,
			Name = "Static",
			Seed = new( ItemCatalog.StaticCulture, "Static culture",
				"A living culture, ready to plant in a grow tray.", 0, Icon: "biotech", Color: "#8be28b" ),
			Raw = new( ItemCatalog.StaticRaw, "Static (raw)",
				"Freshly harvested. Press it into tabs at a press bench.", 0, Icon: "grain", Color: "#5fcfb4" ),
			Product = new( ItemCatalog.StaticTab, "Static tabs", "Pressed tabs of Static.", 10,
				Icon: "medication", Color: "#7fd1ff" ),
			GrowSeconds = 10,
			HarvestRaw = 12,
			HarvestSeeds = 1,
			StartSeeds = 2,
			CropQualityMin = 40,
			CropQualityMax = 85,
			RawPerProduct = 1,
			PressQualitySpread = 10,
			OrderWeight = 60,
			OrderCountMin = 10,
			OrderCountMax = 30,
			OrderCountStep = 5,
			OrderQualityMin = 30,
			OrderQualityMax = 70,
			PriceSpreadMin = 80,
			PriceSpreadMax = 130,
			SeedPrice = 40,
			SeedDeliverSeconds = 120,
		} ),
		With( new ProductDef
		{
			Id = HazeId,
			Name = "Haze",
			Seed = new( ItemCatalog.HazeSeed, "Haze seedling",
				"A pale, slow seedling. Plant it in a grow tray.", 0, Icon: "spa", Color: "#b9a7f0" ),
			Raw = new( ItemCatalog.HazeRaw, "Haze (raw)",
				"A pale bloom, freshly cut. Press it into vials at a press bench, two blooms to a vial.", 0,
				Icon: "eco", Color: "#d6c9f7" ),
			Product = new( ItemCatalog.HazeVial, "Haze vials", "Small vials of clear Haze syrup.", 30,
				Icon: "opacity", Color: "#c9a0e6" ),
			GrowSeconds = 25,
			HarvestRaw = 8,
			HarvestSeeds = 1,
			StartSeeds = 0,
			CropQualityMin = 40,
			CropQualityMax = 85,
			RawPerProduct = 2,
			PressQualitySpread = 10,
			OrderWeight = 40,
			OrderCountMin = 3,
			OrderCountMax = 10,
			OrderCountStep = 1,
			OrderQualityMin = 30,
			OrderQualityMax = 70,
			PriceSpreadMin = 80,
			PriceSpreadMax = 130,
			SeedPrice = 90,
			SeedDeliverSeconds = 180,
		} ),
	];

	/// <summary>
	/// The entry with its three items put in its category (the drug's name), so the two can never disagree, and given
	/// the stack size.
	/// </summary>
	static ProductDef With( ProductDef product ) => product with
	{
		Seed = product.Seed with { Category = product.Name, MaxStack = StackSize },
		Raw = product.Raw with { Category = product.Name, MaxStack = StackSize },
		Product = product.Product with { Category = product.Name, MaxStack = StackSize },
	};

	/// <summary>
	/// How many of a drug's seed, raw and product one stack holds. Set on every drug's items by <see cref="With"/>, so
	/// a new drug inherits it.
	/// </summary>
	public const int StackSize = 256;

	/// <summary>Every drug, in the order of the table.</summary>
	public static IReadOnlyList<ProductDef> Products => products;

	/// <summary>The drug with this id, or null: an id a client sends is checked here.</summary>
	public static ProductDef Find( string id ) =>
		string.IsNullOrEmpty( id ) ? null : products.FirstOrDefault( x => x.Id == id );

	/// <summary>Static, which an old save and anything without a drug falls back to.</summary>
	public static ProductDef Default => Find( StaticId ) ?? products[0];

	/// <summary>
	/// The drug of a tray from its saved id: an empty or unknown id (a tray saved before there were two drugs) is
	/// Static.
	/// </summary>
	public static ProductDef OfTray( string id ) => Find( id ) ?? Default;

	/// <summary>The drug whose seed is this item kind, or null.</summary>
	public static ProductDef OfSeed( string kind ) => products.FirstOrDefault( x => x.Seed.Id == kind );

	/// <summary>The drug whose raw is this item kind, or null.</summary>
	public static ProductDef OfRaw( string kind ) => products.FirstOrDefault( x => x.Raw.Id == kind );

	/// <summary>The drug whose product is this item kind, or null.</summary>
	public static ProductDef OfProduct( string kind ) => products.FirstOrDefault( x => x.Product.Id == kind );

	/// <summary>The drug whose category is this name, or null.</summary>
	public static ProductDef OfCategory( string name ) =>
		string.IsNullOrEmpty( name ) ? null : products.FirstOrDefault( x => x.Name == name );

	/// <summary>The three items of every drug, for the item catalogue.</summary>
	public static IEnumerable<ItemDef> Items => products.SelectMany( x => new[] { x.Seed, x.Raw, x.Product } );

	/// <summary>A drug for a new order, by the weights.</summary>
	public static ProductDef Pick( System.Random random )
	{
		var all = products;
		var roll = random.Next( System.Math.Max( 1, all.Sum( x => x.OrderWeight ) ) );

		foreach ( var product in all )
		{
			roll -= product.OrderWeight;

			if ( roll < 0 )
				return product;
		}

		return all[^1];
	}
}
