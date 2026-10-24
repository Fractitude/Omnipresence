namespace Sandbox;

/// <summary>
/// The pure rules for cash lying in the world: what a player may drop, how much of a pile a wallet takes, and how
/// dollars are written. No scene, no wallet: <see cref="CashDrops"/> applies them. See <c>docs/currency.md</c>.
/// </summary>
public static class CashRules
{
	/// <summary>The least that can be dropped.</summary>
	public const long MinDrop = 1;

	/// <summary>The most drops a player makes a minute, so nobody floods the scene's limit of world items.</summary>
	public const int DropsPerMinute = 20;

	/// <summary>
	/// Dollars as the game writes them: <c>$1,250</c>.
	/// </summary>
	public static string Format( long amount ) => $"${amount:N0}";

	/// <summary>
	/// The number in typed text: its digits only, 0 for none, and never more than the wallet's cap (so a long run of
	/// digits cannot overflow).
	/// </summary>
	public static long Parse( string text )
	{
		long value = 0;

		foreach ( var c in text ?? "" )
		{
			if ( !char.IsAsciiDigit( c ) )
				continue;

			value = value * 10 + (c - '0');

			if ( value > ProgressRecord.MaxMoney )
				return ProgressRecord.MaxMoney;
		}

		return value;
	}

	/// <summary>
	/// Why <paramref name="amount"/> cannot be dropped from a wallet holding <paramref name="balance"/>, or null when it
	/// can. The text is what the player reads.
	/// </summary>
	public static string CheckDrop( long amount, long balance )
	{
		if ( amount < MinDrop )
			return "Enter an amount of at least $1.";

		if ( balance < 1 )
			return "You have no cash.";

		if ( amount > balance )
			return $"You only have {Format( balance )}.";

		if ( amount > ProgressRecord.MaxMoney )
			return "That is too much to drop.";

		return null;
	}

	/// <summary>
	/// How much of a pile of <paramref name="amount"/> dollars a wallet holding <paramref name="balance"/> takes under
	/// the cap, and how much stays on the ground. They always add up to the pile.
	/// </summary>
	public static (long Taken, long Left) Pickup( long amount, long balance )
	{
		var room = System.Math.Max( ProgressRecord.MaxMoney - System.Math.Max( balance, 0 ), 0 );
		var taken = System.Math.Clamp( amount, 0, room );
		return (taken, System.Math.Max( amount, 0 ) - taken);
	}
}
