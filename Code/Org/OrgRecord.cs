namespace Sandbox;

/// <summary>
/// A job in an organization. Only a label for now (the work each one does is not checked); what a role may do inside the
/// organization is <see cref="OrgRules.Can"/>.
/// </summary>
public enum OrgRole
{
	Owner,
	Manager,
	Grower,
	Presser,
	Packer,
	Courier,
	Member,
}

/// <summary>The currencies a payout can be in. Credits never leave the Wired and are not shared.</summary>
public enum OrgCurrency
{
	Crypto,
	Cash,
}

/// <summary>One person in an organization.</summary>
public sealed class OrgMember
{
	public ulong SteamId { get; set; }

	/// <summary>The nickname last seen, so an offline member still has a name.</summary>
	public string Name { get; set; } = "";

	public OrgRole Role { get; set; } = OrgRole.Member;

	/// <summary>Percent of every payout this member gets (0 to 100). The shares of all members add up to 100 at most.</summary>
	public int SharePercent { get; set; }

	/// <summary>Real unix seconds when they joined.</summary>
	public long Joined { get; set; }
}

/// <summary>An offer to join, waiting for the invited player to accept it.</summary>
public sealed class OrgInvite
{
	public string Id { get; set; } = "";
	public string OrgId { get; set; } = "";
	public ulong InviteeId { get; set; }
	public string InviteeName { get; set; } = "";
	public ulong InviterId { get; set; }
	public string InviterName { get; set; } = "";
	public OrgRole Role { get; set; } = OrgRole.Member;
	public int SharePercent { get; set; }

	/// <summary>Real unix seconds when it was sent.</summary>
	public long Created { get; set; }
}

/// <summary>What one member got from a payout.</summary>
public sealed class OrgPayoutSplit
{
	public ulong MemberId { get; set; }
	public string Name { get; set; } = "";
	public long Amount { get; set; }
}

/// <summary>One payout the organization made: the receiver's earnings and how they were shared.</summary>
public sealed class OrgPayout
{
	public long At { get; set; }
	public string OrderId { get; set; } = "";
	public string Reason { get; set; } = "";
	public OrgCurrency Currency { get; set; }

	/// <summary>The amount that was to be paid to the receiver, before sharing.</summary>
	public long Total { get; set; }

	public ulong ReceiverId { get; set; }
	public string ReceiverName { get; set; } = "";

	/// <summary>What stayed with the receiver (the rounding, the share nobody holds, and any share a member could not hold).</summary>
	public long Kept { get; set; }

	/// <summary>What the other members got.</summary>
	public List<OrgPayoutSplit> Splits { get; set; } = new();
}

/// <summary>A share for a member whose record was not in memory: it is applied when the record is loaded.</summary>
public sealed class OrgPendingCredit
{
	public string Id { get; set; } = "";
	public ulong MemberId { get; set; }
	public OrgCurrency Currency { get; set; }
	public long Amount { get; set; }
	public string OrgName { get; set; } = "";
	public string OrderId { get; set; } = "";
}

/// <summary>An organization.</summary>
public sealed class OrgRecord
{
	public string Id { get; set; } = "";
	public string Name { get; set; } = "";

	/// <summary>The id of one of <see cref="OrgCatalog.Icons"/>.</summary>
	public string IconId { get; set; } = "shield";

	/// <summary>"#rrggbb".</summary>
	public string IconColor { get; set; } = OrgCatalog.DefaultColor;

	public ulong FounderId { get; set; }

	/// <summary>Real unix seconds.</summary>
	public long Created { get; set; }

	public List<OrgMember> Members { get; set; } = new();

	public List<OrgPayout> Payouts { get; set; } = new();

	public OrgMember MemberOf( ulong steamId ) => Members.FirstOrDefault( x => x.SteamId == steamId );
}

/// <summary>Everything the registry keeps in its file.</summary>
public sealed class OrgData
{
	public int Version { get; set; } = 1;
	public List<OrgRecord> Orgs { get; set; } = new();
	public List<OrgInvite> Invites { get; set; } = new();
	public List<OrgPendingCredit> Pending { get; set; } = new();
}
