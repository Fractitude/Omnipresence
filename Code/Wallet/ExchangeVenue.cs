namespace Sandbox;

/// <summary>
/// One exchange a player can change money at, and the terms it keeps. The rate itself is one to one everywhere; what
/// differs between exchanges is how much of it they keep, and how long dollars take to arrive when crypto is sold.
/// </summary>
/// <remarks>
/// The terms are plain numbers, so a venue is a constant: <see cref="ExchangeCatalog"/> is the list of them, and
/// <see cref="Exchange"/> is the only code that applies them. A venue never changes at run time.
/// </remarks>
/// <param name="Id">What a save and the panels call it.</param>
/// <param name="Name">What the player sees in the window.</param>
/// <param name="Tagline">One line about what dealing with it is like.</param>
/// <param name="BuyFeePercent">Percent of the dollars kept when dollars are changed into crypto.</param>
/// <param name="SellFeePercent">Percent of the dollars kept when crypto is sold.</param>
/// <param name="CashoutDelaySeconds">How long sold crypto's dollars take to arrive; zero for at once.</param>
/// <param name="MaxPerRequest">The most one request changes, in the currency being spent.</param>
public sealed record ExchangeVenue(
	string Id,
	string Name,
	string Tagline,
	int BuyFeePercent,
	int SellFeePercent,
	long CashoutDelaySeconds,
	long MaxPerRequest )
{
	/// <summary>The crypto this many dollars buys, after the fee.</summary>
	public long CryptoFor( long cash ) => cash * (100 - BuyFeePercent) / 100;

	/// <summary>The dollars this much crypto sells for, after the fee.</summary>
	public long CashFor( long crypto ) => crypto * (100 - SellFeePercent) / 100;

	/// <summary>Whether dollars arrive at once, with no cash-out to wait for.</summary>
	public bool PaysAtOnce => CashoutDelaySeconds <= 0;
}

/// <summary>
/// The exchanges in the game. The Government Exchange is the regulated one: it asks less, and pays at once. The
/// darknet's (the one in Hush) asks more and makes the player wait for the dollars, which is what dealing off the books
/// costs.
/// </summary>
public static class ExchangeCatalog
{
	public const string Government = "government";
	public const string Darknet = "darknet";

	/// <summary>The Government Exchange, the one the Payments app deals with.</summary>
	public static ExchangeVenue GovernmentVenue { get; } = new(
		Government,
		"Government Exchange",
		"Regulated. The best rate you will get in the open.",
		2,
		5,
		0,
		1_000_000 );

	/// <summary>The darknet Exchange, the one Hush deals with.</summary>
	public static ExchangeVenue DarknetVenue { get; } = new(
		Darknet,
		"Darknet Exchange",
		"No questions asked. A bigger cut, and the dollars take a while.",
		4,
		8,
		120,
		1_000_000 );

	/// <summary>Every exchange, cheapest first.</summary>
	public static IReadOnlyList<ExchangeVenue> All { get; } = [GovernmentVenue, DarknetVenue];

	/// <summary>
	/// The venue with this id, or null: an id from outside is checked, never trusted.
	/// </summary>
	public static ExchangeVenue Of( string id ) => All.FirstOrDefault( x => x.Id == id );
}
