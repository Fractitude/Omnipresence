namespace Sandbox;

/// <summary>
/// One car the dealership sells. <see cref="Prefab"/> is the prefab path the car is made from (an asset path relative to
/// <c>Assets/</c>); <see cref="Ident"/> is the sent's ident for an integrated sent (<c>crosschainer.nishida_240z</c>),
/// whose assets are mounted from the workshop before the car is shown or made (<see cref="SentLocal.Mount"/>), and is
/// empty for a car that is made of the project's own assets.
/// </summary>
public sealed record CarModel( string Id, string Name, string Description, long Price, string Prefab, string Ident = "" );

/// <summary>
/// What the car dealership sells. Data only: a new car is one more line. Prices and descriptions are first guesses.
/// </summary>
public static class CarCatalog
{
	// A table nobody changes at run time, so readonly is fine (a hot reload only picks up edits).
	static readonly CarModel[] cars =
	[
		new( "buggy", "Buggy", "A light open buggy. Rough ground is no problem.", 6_500, "vehicles/buggy/buggy.prefab" ),
		Sent( "hokuto_boxer", "Hokuto Boxer", "A small Hokuto with a boxer engine.", 9_000 ),
		Sent( "nishida_sable_s13", "Nishida Sable S13", "A cheap, light Nishida coupe.", 14_000 ),
		Sent( "nishida_sable_s14", "Nishida Sable S14", "The Sable's later, longer coupe.", 16_500 ),
		Sent( "takeda_hachi_86", "Takeda Hachi 86", "A small, light Takeda coupe.", 17_500 ),
		Sent( "takeda_ascend", "Takeda Ascend", "A Takeda for the daily drive.", 18_000 ),
		Sent( "nishida_stratus_2000", "Nishida Stratus 2000", "A big Nishida saloon.", 12_000 ),
		Sent( "nishida_240z", "Nishida 240Z", "A classic Nishida sports coupe.", 22_000 ),
		Sent( "mizuno_rotary_c", "Mizuno Rotary C", "A Mizuno with a rotary engine.", 28_000 ),
		Sent( "rhenwerk_r30", "Rhenwerk R30", "A Rhenwerk grand tourer.", 31_000 ),
		Sent( "nishida_stratus_s34", "Nishida Stratus S34", "The Stratus at its fastest.", 34_000 ),
	];

	public static IReadOnlyList<CarModel> All => cars;

	public static CarModel Find( string id ) => cars.FirstOrDefault( x => x.Id == id );

	// Not readonly: filled at run time (AGENTS.md, "Static state and hot reload").
	static Dictionary<string, Color?> stockPaints = new();

	/// <summary>
	/// The colour a car comes in: the <see cref="CarPaint.Paint"/> on its prefab, set in the inspector. Null for a car
	/// whose prefab has no <see cref="CarPaint"/>, which cannot be painted.
	/// </summary>
	public static Color? StockPaint( CarModel car )
	{
		if ( car is null )
			return null;

		if ( stockPaints.TryGetValue( car.Id, out var known ) )
			return known;

		if ( !ResourceLibrary.TryGet<PrefabFile>( car.Prefab, out var prefab ) )
			return null;

		Color? paint = SceneUtility.GetPrefabScene( prefab ).GetComponentInChildren<CarPaint>( true )?.Paint;
		stockPaints[car.Id] = paint;
		return paint;
	}

	/// <summary>
	/// An integrated crosschainer sent (<c>Assets/sents/crosschainer/&lt;package&gt;/</c>, docs/sents.md).
	/// </summary>
	static CarModel Sent( string package, string name, string description, long price )
	{
		var ident = $"crosschainer.{package}";
		return new( package, name, description, price, SentLocal.PrefabPath( ident ), ident );
	}
}
