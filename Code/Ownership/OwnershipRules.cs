namespace Sandbox;

/// <summary>
/// What somebody asks to do with a thing, checked against who owns it.
/// </summary>
public enum OwnershipRight
{
	/// <summary>Open it, stand in it, drive it, shoot it: the everyday use of a thing.</summary>
	Use,

	/// <summary>Change it: the name on a door, what is inside a box, who may use it.</summary>
	Manage,

	/// <summary>Hand the title on to somebody else, by sale or by gift.</summary>
	Transfer,
}

/// <summary>
/// What ownership means for permission, as pure functions: no scene, no clock, no file. <see cref="OwnershipRegistry"/>
/// and <see cref="OwnershipComponent"/> ask it, and so does anything else that has a title in hand.
/// </summary>
/// <remarks>
/// The matrix, and why:
/// <list type="bullet">
/// <item><b>Nobody</b> — a free thing: anyone may use it, take it or give it away.</item>
/// <item><b>The public</b> — everyone may use it and nobody may manage or transfer it: a road is not for sale, and
/// nobody but game code may close it.</item>
/// <item><b>The government</b> — the same to a player, so a municipal service works for everyone; game code still
/// transfers it (a licence, a plot sold at auction).</item>
/// <item><b>A player</b> — the owner, and only the owner, for every right. A crew sharing what its members own is the
/// organization's business and lands with locks and keys, not here.</item>
/// <item><b>An organization</b> — every member may use it, and only the founder (the Owner role, which is never handed
/// out) may manage or transfer it. Every member is presumed to be one from the actor they pass
/// (<paramref name="role"/> defaults to <see cref="OrgRole.Member"/>), so a caller that does not know the role is
/// allowed no more than a plain member.</item>
/// </list>
/// </remarks>
public static class OwnershipRules
{
	/// <summary>Whether this player, with no organization to speak of, may do this.</summary>
	public static bool Allows( Ownership title, OwnershipRight right, ulong steamId ) =>
		Allows( title, right, Ownership.Player( steamId ) );

	/// <summary>
	/// Whether <paramref name="actor"/> may do this. An actor is a player or an organization: the caller that resolved
	/// the player's organization passes <see cref="Ownership.Organization"/> and the role they hold in it.
	/// </summary>
	public static bool Allows( Ownership title, OwnershipRight right, Ownership actor, OrgRole role = OrgRole.Member )
	{
		if ( title is null || actor is null )
			return false;

		switch ( title.Kind )
		{
			case OwnerKind.Nobody:
				return true;

			case OwnerKind.Public:
			case OwnerKind.Government:
				return right == OwnershipRight.Use;

			case OwnerKind.Player:
				return title.HeldBy( actor.SteamId ) && actor.Kind == OwnerKind.Player;

			case OwnerKind.Organization:
				return actor.HeldBy( title.OrgId )
					&& (right == OwnershipRight.Use || OrgRules.Can( role, OrgAction.Transfer ));
		}

		return false;
	}

	/// <summary>
	/// Why a title can't be used, or null when it can: a player without an id and an organization without an id are
	/// not owners, and the registry drops them rather than keeping a thing nobody can be said to own.
	/// </summary>
	public static string Check( Ownership title ) => title?.Kind switch
	{
		OwnerKind.Player when title.SteamId == 0 => "A player owner needs a Steam id.",
		OwnerKind.Organization when string.IsNullOrEmpty( title.OrgId ) => "An organization owner needs its id.",
		_ => null,
	};

	/// <summary>
	/// Whether anybody may be handed this title: nobody owns nothing, so it is how a thing is released.
	/// </summary>
	public static bool IsTransferable( Ownership title ) => Check( title ) is null;
}
