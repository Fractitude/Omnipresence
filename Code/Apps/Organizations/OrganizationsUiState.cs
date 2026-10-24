namespace OmnipresenceApps;

public enum OrganizationsTab
{
	Organization,
	Invitations,
	Payouts,
}

/// <summary>
/// What the Organizations panel draws. Replaced whole by <see cref="OrganizationsViewModel"/> on every change. It holds only
/// what the player in the seat may see: their own organization, their own invitations and its payouts.
/// </summary>
public sealed record OrganizationsUiState
{
	/// <summary>What a viewer draws until the host's state arrives.</summary>
	public static OrganizationsUiState Empty { get; } = new();

	/// <summary>Goes up whenever anything else here changes. The panel's BuildHash is this.</summary>
	public int Revision { get; init; }

	public OrganizationsTab Tab { get; init; }

	/// <summary>False while the app can't work (nobody in the seat, offline, account not loaded).</summary>
	public bool HasAccount { get; init; }

	/// <summary>Shown instead of the app when there is no account: why.</summary>
	public string Message { get; init; } = "";

	public string OfflineHint { get; init; }

	/// <summary>What went wrong, or right, with the last action.</summary>
	public string Notice { get; init; } = "";

	/// <summary>Invitations waiting for this player, for the tab label.</summary>
	public int InviteCount { get; init; }

	/// <summary>The player's organization, or null when they are in none (the register form shows).</summary>
	public OrgView Org { get; init; }

	/// <summary>The register form, and the form that edits the name, icon and colour of the organization.</summary>
	public OrgDraft Draft { get; init; } = new();

	/// <summary>True while the Owner edits the name, icon and colour.</summary>
	public bool Editing { get; init; }

	/// <summary>What is waiting for a second press: "kick", "disband" or "leave", or empty.</summary>
	public string Confirm { get; init; } = "";

	public IReadOnlyList<IncomingInviteRow> Incoming { get; init; } = [];

	public IReadOnlyList<PayoutRow> Payouts { get; init; } = [];
}

/// <summary>The name, icon and colour being typed or chosen.</summary>
public sealed record OrgDraft
{
	public string Name { get; init; } = "";
	public string IconId { get; init; } = OrgCatalog.Icons[0].Id;

	/// <summary>The colour as it is typed, which may not be a colour yet.</summary>
	public string ColorText { get; init; } = OrgCatalog.DefaultColor;

	/// <summary>The typed colour as "#rrggbb", or empty when it is not one.</summary>
	public string Color { get; init; } = OrgCatalog.DefaultColor;

	public string Fee { get; init; } = "";
}

public sealed record MemberRow(
	string Id,
	string Name,
	string Role,
	int Share,
	bool IsYou,
	bool Selected );

public sealed record RoleChip( string Role, bool Allowed, bool Current );

/// <summary>The member being edited, and what the player in the seat may do to them.</summary>
public sealed record MemberEdit(
	string Name,
	string Role,
	int Share,
	bool CanShare,
	bool CanKick,
	IReadOnlyList<RoleChip> Roles );

/// <summary><paramref name="Wait"/> is the time left before this player may be invited again, or empty.</summary>
public sealed record CandidateRow( string Id, string Name, bool Picked, string Wait );

public sealed record SentInviteRow( string Id, string Name, string Role, int Share );

/// <summary>The invitation form of an Owner or a Manager.</summary>
public sealed record InviteForm(
	IReadOnlyList<CandidateRow> Candidates,
	IReadOnlyList<RoleChip> Roles,
	int Share,
	bool CanSend,
	IReadOnlyList<SentInviteRow> Sent );

public sealed record OrgView(
	string Name,
	string Glyph,
	string Color,
	string YourRole,
	int YourShare,
	int SharedTotal,
	string Founded,
	IReadOnlyList<MemberRow> Members,
	bool CanManage,
	bool CanRestyle,
	bool CanDisband,
	bool CanLeave,
	MemberEdit Edit,
	InviteForm Invite );

public sealed record IncomingInviteRow(
	string Id,
	string OrgName,
	string Glyph,
	string Color,
	string Role,
	int Share,
	string From );

public sealed record SplitLine( string Name, string Amount, bool IsYou );

public sealed record PayoutRow(
	string When,
	string Title,
	string Total,
	string Yours,
	IReadOnlyList<SplitLine> Lines );
