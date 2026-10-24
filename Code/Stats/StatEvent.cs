namespace Sandbox;

/// <summary>
/// The kinds of line <see cref="StatsLog"/> writes. Strings in the file, so a line stays readable and a new kind never
/// breaks an old reader.
/// </summary>
public static class StatKind
{
	public const string Join = "join";
	public const string Leave = "leave";
	public const string Kill = "kill";
	public const string Death = "death";
	public const string Money = "money";
	public const string Purchase = "purchase";
	public const string Kick = "kick";
	public const string Ban = "ban";
}

/// <summary>
/// How money moved: the <see cref="StatEvent.Mechanic"/> of a <see cref="StatKind.Money"/> line. A new source of money
/// adds a constant here and passes it to the <see cref="PlayerWallet"/> method it calls.
/// </summary>
public static class StatMechanic
{
	/// <summary>Player to player, through the Payments app.</summary>
	public const string Payment = "payment_app";

	/// <summary>A test command (<c>progress_give_money</c> and its kin).</summary>
	public const string ConsoleCommand = "console_command";

	/// <summary>Money lying in the world that a player picked up. No such item exists yet; see plans/stats.md.</summary>
	public const string GroundPickup = "ground_pickup";

	/// <summary>An organization's payout, shared out by <see cref="OrgPayouts"/>.</summary>
	public const string OrgPayout = "org_payout";

	/// <summary>Dollars and crypto changing into each other.</summary>
	public const string Exchange = "exchange";

	/// <summary>Something bought: hardware, a placeable, a dark web order.</summary>
	public const string Shop = "shop";

	/// <summary>A fee paid, such as registering an organization.</summary>
	public const string Fee = "fee";

	/// <summary>A fee or price given back.</summary>
	public const string Refund = "refund";

	/// <summary>Credits earned from a hacking intrusion or operation.</summary>
	public const string Hacking = "hacking";
}

/// <summary>
/// One line of the stats log (<c>stats/events_{year}-{month}.jsonl</c>). Only the fields of its <see cref="Kind"/> are
/// set; the rest are null and stay in the file as nulls. Times are real-world UTC, not the game clock.
/// </summary>
public sealed class StatEvent
{
	public DateTimeOffset At { get; set; }

	/// <summary>One of <see cref="StatKind"/>.</summary>
	public string Kind { get; set; }

	/// <summary>The player the line is about.</summary>
	public ulong SteamId { get; set; }

	public string Name { get; set; }

	/// <summary>The other side: the giver or receiver of money, the killer, the victim, the admin who kicked.</summary>
	public ulong? OtherId { get; set; }

	public string OtherName { get; set; }

	/// <summary>Money: the signed change. Purchase: the price.</summary>
	public long? Amount { get; set; }

	/// <summary>Money: <c>cash</c>, <c>credits</c> or <c>crypto</c>.</summary>
	public string Currency { get; set; }

	/// <summary>Money: the balance after the change.</summary>
	public long? Balance { get; set; }

	/// <summary>Money: one of <see cref="StatMechanic"/>.</summary>
	public string Mechanic { get; set; }

	/// <summary>Money and purchase: free text about it (the venue, the order, the item).</summary>
	public string Detail { get; set; }

	/// <summary>Kill and death: the weapon's name, when there was one.</summary>
	public string Weapon { get; set; }

	/// <summary>Death: the damage tags, for a death with no weapon (a fall).</summary>
	public string Cause { get; set; }

	/// <summary>Leave: how long this visit lasted.</summary>
	public double? SessionSeconds { get; set; }

	/// <summary>Kick and ban: the reason, when one was given.</summary>
	public string Reason { get; set; }

	/// <summary>Purchase: what kind of thing (<c>placeable</c>, <c>hardware</c>, <c>house</c>, <c>car</c>, <c>weapon</c>).</summary>
	public string ItemKind { get; set; }

	/// <summary>Purchase: the item's id.</summary>
	public string ItemId { get; set; }
}

/// <summary>
/// The totals the stats log keeps per player, <c>stats/players/{steamId}.json</c>, so nobody has to add up the log to
/// answer "how long have they played". Changed only by <see cref="StatsLog"/>.
/// </summary>
public sealed class PlayerStatsSummary
{
	public ulong SteamId { get; set; }

	public string Name { get; set; } = "";

	public DateTimeOffset FirstSeen { get; set; }

	public DateTimeOffset LastSeen { get; set; }

	public int Joins { get; set; }

	public int Leaves { get; set; }

	/// <summary>Total time connected, over every visit. Counted while they are here, so a crash loses seconds, not hours.</summary>
	public double PlaytimeSeconds { get; set; }

	public int Kills { get; set; }

	public int Deaths { get; set; }

	/// <summary>Kills by weapon name (<c>unarmed</c> when there was none).</summary>
	public Dictionary<string, int> KillsByWeapon { get; set; } = new();

	public int Kicks { get; set; }

	public int Bans { get; set; }

	public int Transactions { get; set; }

	public int Purchases { get; set; }

	/// <summary>The last balances written to the log.</summary>
	public long Cash { get; set; }

	public long Credits { get; set; }

	public long Crypto { get; set; }
}
