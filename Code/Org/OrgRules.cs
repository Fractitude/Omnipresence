namespace Sandbox;

/// <summary>What a member does in an organization that needs a permission.</summary>
public enum OrgAction
{
	Invite,
	Kick,
	ChangeRole,
	ChangeShare,

	/// <summary>Rename it, or change its icon and colour.</summary>
	Restyle,

	Disband,
}

/// <summary>What <see cref="OrgRules.Split"/> worked out: who is paid what, and what the receiver keeps.</summary>
public sealed record OrgSplit( IReadOnlyList<(ulong MemberId, long Amount)> Credits, long Kept );

/// <summary>
/// The rules of organizations as pure functions: names, colours, shares, who may do what, invitations and how a payout
/// is split. No scene, no player, no clock of its own, so they can be tested alone. <see cref="OrgRegistry"/> applies
/// them to the real data.
/// </summary>
/// <remarks>
/// Permissions: the Owner (the founder) can do everything and can't be kicked or demoted, only disband the
/// organization. A Manager can invite, kick, and change roles and shares, but only of members who are neither Owner nor
/// Manager, never their own, and can't hand out Manager (only the Owner promotes a Manager). Only the Owner renames or
/// restyles it, and disbands it. Everyone but the Owner can leave.
/// </remarks>
public static class OrgRules
{
	/// <summary>Besides letters, digits and the space, a name may hold these.</summary>
	const string NamePunctuation = "-_.'&!";

	// -- Names, colours, icons ----------------------------------------------------------------------------------------

	/// <summary>The name as it is kept: trimmed, and every run of spaces one space.</summary>
	public static string NormalizeName( string name ) =>
		string.Join( ' ', (name ?? "").Split( ' ', System.StringSplitOptions.RemoveEmptyEntries ) );

	/// <summary>What two names are compared by: the normalized name in lower case.</summary>
	public static string NameKey( string name ) => NormalizeName( name ).ToLowerInvariant();

	/// <summary>
	/// Why a name can't be used, or null when it can. <paramref name="otherNames"/> are the names that are taken, which
	/// the caller leaves the organization's own current name out of when it renames.
	/// </summary>
	public static string CheckName( string name, IEnumerable<string> otherNames )
	{
		var text = NormalizeName( name );

		if ( text.Length < OrgCatalog.MinNameLength )
			return $"The name needs at least {OrgCatalog.MinNameLength} characters.";

		if ( text.Length > OrgCatalog.MaxNameLength )
			return $"The name can have at most {OrgCatalog.MaxNameLength} characters.";

		foreach ( var c in text )
		{
			if ( !char.IsLetterOrDigit( c ) && c != ' ' && !NamePunctuation.Contains( c ) )
				return $"The name can only have letters, digits, spaces and {NamePunctuation}";
		}

		var key = NameKey( text );

		if ( otherNames.Any( x => NameKey( x ) == key ) )
			return "That name is taken.";

		return null;
	}

	/// <summary>"#rrggbb" in lower case for "#rrggbb" or "rrggbb", or null.</summary>
	public static string NormalizeColor( string color )
	{
		var text = (color ?? "").Trim();

		if ( text.StartsWith( '#' ) )
			text = text[1..];

		if ( text.Length != 6 || !text.All( char.IsAsciiHexDigit ) )
			return null;

		return "#" + text.ToLowerInvariant();
	}

	public static bool IsIcon( string id ) => OrgCatalog.IconOf( id ) is not null;

	// -- Permissions --------------------------------------------------------------------------------------------------

	/// <summary>Whether a role may do this at all.</summary>
	public static bool Can( OrgRole role, OrgAction action ) => role switch
	{
		OrgRole.Owner => true,
		OrgRole.Manager => action is OrgAction.Invite or OrgAction.Kick or OrgAction.ChangeRole or OrgAction.ChangeShare,
		_ => false,
	};

	/// <summary>The Owner is the founder: nobody holds the role by being given it.</summary>
	public static bool IsHandedOut( OrgRole role ) => role != OrgRole.Owner;

	/// <summary>Whether <paramref name="actor"/> may invite someone as <paramref name="role"/>.</summary>
	public static bool CanInviteAs( OrgRole actor, OrgRole role ) =>
		Can( actor, OrgAction.Invite ) && IsHandedOut( role ) && (actor == OrgRole.Owner || role != OrgRole.Manager);

	/// <summary>Whether <paramref name="actor"/> may remove <paramref name="target"/>. Never oneself or the Owner.</summary>
	public static bool CanKick( OrgRole actor, OrgRole target, bool self ) =>
		!self && Can( actor, OrgAction.Kick ) && target != OrgRole.Owner
		&& (actor == OrgRole.Owner || target != OrgRole.Manager);

	/// <summary>Whether <paramref name="actor"/> may give <paramref name="target"/> the role <paramref name="role"/>.</summary>
	public static bool CanChangeRole( OrgRole actor, OrgRole target, OrgRole role, bool self ) =>
		Can( actor, OrgAction.ChangeRole ) && target != OrgRole.Owner && IsHandedOut( role )
		&& (actor == OrgRole.Owner || (!self && target != OrgRole.Manager && role != OrgRole.Manager));

	/// <summary>Whether <paramref name="actor"/> may change the share of <paramref name="target"/>.</summary>
	public static bool CanChangeShare( OrgRole actor, OrgRole target, bool self ) =>
		Can( actor, OrgAction.ChangeShare )
		&& (actor == OrgRole.Owner || (!self && target is not (OrgRole.Owner or OrgRole.Manager)));

	// -- Shares -------------------------------------------------------------------------------------------------------

	public static int ShareTotal( IEnumerable<OrgMember> members ) => members.Sum( x => x.SharePercent );

	/// <summary>Every share is 0 to 100 and together they are at most 100.</summary>
	public static bool SharesValid( IEnumerable<OrgMember> members ) =>
		members.All( x => x.SharePercent is >= 0 and <= 100 ) && ShareTotal( members ) <= 100;

	/// <summary>Why this member can't have this share (leaving the others as they are), or null.</summary>
	public static string CheckShare( IReadOnlyList<OrgMember> members, ulong memberId, int share )
	{
		if ( share is < 0 or > 100 )
			return "A share is 0 to 100 percent.";

		var others = members.Where( x => x.SteamId != memberId ).Sum( x => x.SharePercent );

		return others + share > 100 ? $"The shares would add up to {others + share}%. At most 100% can be shared." : null;
	}

	// -- Invitations --------------------------------------------------------------------------------------------------

	public static bool IsExpired( OrgInvite invite, long now ) => now - invite.Created >= OrgCatalog.InviteSeconds;

	/// <summary>Why <paramref name="actor"/> can't send this invitation now, or null.</summary>
	public static string CheckInvite(
		OrgRecord org, IReadOnlyList<OrgInvite> waiting, ulong actor, ulong invitee, bool inviteeHasOrg, OrgRole role,
		int share, long now )
	{
		if ( org.MemberOf( actor ) is not { } member || !Can( member.Role, OrgAction.Invite ) )
			return "You can't invite people to this organization.";

		if ( invitee == 0 || invitee == actor )
			return "Pick another player.";

		if ( inviteeHasOrg || org.MemberOf( invitee ) is not null )
			return "That player is already in an organization.";

		if ( !CanInviteAs( member.Role, role ) )
			return $"You can't invite someone as {role}.";

		if ( org.Members.Count >= OrgCatalog.MaxMembers )
			return "The organization is full.";

		var open = waiting.Where( x => !IsExpired( x, now ) ).ToList();

		if ( open.Count >= OrgCatalog.MaxPendingInvites )
			return $"At most {OrgCatalog.MaxPendingInvites} invitations can wait at once.";

		if ( open.Any( x => x.InviteeId == invitee ) )
			return "That player already has an invitation.";

		return CheckShare( org.Members, invitee, share );
	}

	/// <summary>Why this invitation can't be accepted now, or null. Everything is checked again.</summary>
	public static string CheckAccept( OrgRecord org, OrgInvite invite, bool inviteeHasOrg, long now )
	{
		if ( org is null )
			return "That organization no longer exists.";

		if ( IsExpired( invite, now ) )
			return "That invitation ran out.";

		if ( inviteeHasOrg )
			return "You are already in an organization.";

		if ( org.Members.Count >= OrgCatalog.MaxMembers )
			return "The organization is full.";

		if ( !IsHandedOut( invite.Role ) )
			return "That invitation is not valid.";

		return CheckShare( org.Members, invite.InviteeId, invite.SharePercent );
	}

	// -- Payouts ------------------------------------------------------------------------------------------------------

	/// <summary>
	/// Split <paramref name="amount"/>, earned by <paramref name="receiver"/>. Every other member with a share gets
	/// floor(amount x share / 100), but no more than the <paramref name="room"/> they can still hold. The receiver keeps
	/// the rest: the rounding, the share nobody holds, the share a member could not hold and the receiver's own share.
	/// The credits and what is kept always add up to the amount. No members, or no one with a share, keeps everything.
	/// </summary>
	public static OrgSplit Split(
		long amount, ulong receiver, IEnumerable<(ulong MemberId, int Share)> members, System.Func<ulong, long> room )
	{
		var credits = new List<(ulong, long)>();
		var left = System.Math.Max( amount, 0 );

		foreach ( var (id, share) in members )
		{
			if ( id == receiver || share <= 0 || left <= 0 )
				continue;

			var due = amount * System.Math.Min( share, 100 ) / 100;
			var pay = System.Math.Min( System.Math.Min( due, room( id ) ), left );

			if ( pay <= 0 )
				continue;

			credits.Add( (id, pay) );
			left -= pay;
		}

		return new OrgSplit( credits, left );
	}
}
