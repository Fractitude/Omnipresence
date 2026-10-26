namespace Sandbox;

/// <summary>
/// Who holds legal title to a thing. <see cref="OwnerKind.Nobody"/> is no one: the thing may be claimed or taken.
/// </summary>
public enum OwnerKind
{
	/// <summary>No legal owner: free to be claimed, taken or used by anyone.</summary>
	Nobody,

	/// <summary>One player, by Steam id.</summary>
	Player,

	/// <summary>An organization, by <see cref="OrgRecord.Id"/>: the crew's property rather than one member's.</summary>
	Organization,

	/// <summary>
	/// The public at large: a road, a park, a bench. Everyone may use it, and nobody may sell, fence or claim it.
	/// </summary>
	Public,

	/// <summary>
	/// The state: a licence, a municipal service, a police station. Game code gives it and takes it in the
	/// government's name; a player can no more buy it than they can sell it.
	/// </summary>
	Government,
}

/// <summary>
/// Who legally owns a thing: the player who bought it or is registered as its owner, the organization whose property
/// it is, the public, or the government.
/// </summary>
/// <remarks>
/// This is a plain value, not a component, so anything that can hold a field can hold it: an item in a bag, a
/// business, a bank account, a licence. A thing in the world carries <see cref="OwnershipComponent"/>, which keeps
/// this in a synced field and in <see cref="OwnershipRegistry"/>; a thing that is not a GameObject (a business, a
/// registered name) keeps it in its own record and uses the registry with a key of its own.
/// <para>
/// Title is not access. Title says whose it is; what another player may <em>do</em> with it is
/// <see cref="OwnershipRules.Allows"/> (use, manage, transfer), and the locks and keys of a base are a rule of their
/// own on top. A thing can be legally yours and locked against you by someone who squatted it, or someone else's and
/// open to you.
/// </para>
/// </remarks>
public sealed class Ownership : System.IEquatable<Ownership>
{
	/// <summary>Whose it is.</summary>
	public OwnerKind Kind { get; set; }

	/// <summary>The owner's Steam id, for <see cref="OwnerKind.Player"/>.</summary>
	public ulong SteamId { get; set; }

	/// <summary>The owner's <see cref="OrgRecord.Id"/>, for <see cref="OwnerKind.Organization"/>.</summary>
	public string OrgId { get; set; } = "";

	/// <summary>Nobody owns it. Every read of a value that was never set gives this.</summary>
	public static Ownership Nobody => new() { Kind = OwnerKind.Nobody };

	/// <summary>This player owns it.</summary>
	public static Ownership Player( ulong steamId ) => new() { Kind = OwnerKind.Player, SteamId = steamId };

	/// <summary>This organization owns it.</summary>
	public static Ownership Organization( string orgId ) =>
		new() { Kind = OwnerKind.Organization, OrgId = orgId ?? "" };

	/// <summary>The public owns it: everyone may use it, nobody may sell it.</summary>
	public static Ownership Public => new() { Kind = OwnerKind.Public };

	/// <summary>The government owns it: only game code gives it away.</summary>
	public static Ownership Government => new() { Kind = OwnerKind.Government };

	public bool IsNobody => Kind == OwnerKind.Nobody;

	public bool IsPlayer => Kind == OwnerKind.Player;

	public bool IsOrganization => Kind == OwnerKind.Organization;

	public bool IsPublic => Kind == OwnerKind.Public;

	public bool IsGovernment => Kind == OwnerKind.Government;

	/// <summary>Whether anybody owns it at all.</summary>
	public bool IsOwned => Kind != OwnerKind.Nobody;

	/// <summary>
	/// Whether this player is the owner. Says nothing about an organization or a lease: ask
	/// <see cref="OwnershipRules.Allows"/> for what a player may do with it.
	/// </summary>
	public bool HeldBy( ulong steamId ) => Kind == OwnerKind.Player && SteamId != 0 && SteamId == steamId;

	/// <summary>Whether this organization is the owner.</summary>
	public bool HeldBy( string orgId ) =>
		Kind == OwnerKind.Organization && !string.IsNullOrEmpty( OrgId ) && OrgId == orgId;

	/// <summary>
	/// How this owner is written down: <c>player:76561197960287930</c>, <c>org:a1b2c3d4e5f6</c>, <c>public</c>,
	/// <c>government</c>, and <c>""</c> for nobody. It is what the registry's file keeps and what
	/// <see cref="OwnershipComponent"/> syncs, so a client can read the owner without the registry.
	/// </summary>
	public string Key => Kind switch
	{
		OwnerKind.Player => SteamId == 0 ? "" : $"player:{SteamId}",
		OwnerKind.Organization => string.IsNullOrEmpty( OrgId ) ? "" : $"org:{OrgId}",
		OwnerKind.Public => "public",
		OwnerKind.Government => "government",
		_ => "",
	};

	/// <summary>The owner a <see cref="Key"/> names. Anything that is not a key is nobody.</summary>
	public static Ownership Parse( string key )
	{
		if ( string.IsNullOrWhiteSpace( key ) )
			return Nobody;

		var text = key.Trim();

		if ( text.Equals( "public", System.StringComparison.OrdinalIgnoreCase ) )
			return Public;

		if ( text.Equals( "government", System.StringComparison.OrdinalIgnoreCase ) )
			return Government;

		if ( text.StartsWith( "player:", System.StringComparison.OrdinalIgnoreCase )
		     && ulong.TryParse( text["player:".Length..], out var steamId ) && steamId != 0 )
			return Player( steamId );

		if ( text.StartsWith( "org:", System.StringComparison.OrdinalIgnoreCase ) && text.Length > "org:".Length )
			return Organization( text["org:".Length..] );

		return Nobody;
	}

	/// <summary>
	/// What to call the owner in a menu: the player's name, the organization's, or a word for the rest.
	/// </summary>
	public string DisplayName => Kind switch
	{
		OwnerKind.Player => NameOfPlayer( SteamId ),
		OwnerKind.Organization => OrgRegistry.Find( OrgId )?.Name is { Length: > 0 } name ? name : "An organization",
		OwnerKind.Public => "The public",
		OwnerKind.Government => "The government",
		_ => "Nobody",
	};

	static string NameOfPlayer( ulong steamId )
	{
		if ( Connection.All.FirstOrDefault( x => x.SteamId == steamId )?.DisplayName is { Length: > 0 } live )
			return live;

		return steamId == 0 ? "Nobody" : $"Player {steamId}";
	}

	public bool Equals( Ownership other ) =>
		other is not null
		&& other.Kind == Kind
		&& other.SteamId == SteamId
		&& string.Equals( other.OrgId ?? "", OrgId ?? "", System.StringComparison.Ordinal );

	public override bool Equals( object obj ) => Equals( obj as Ownership );

	public override int GetHashCode() => System.HashCode.Combine( (int)Kind, SteamId, OrgId ?? "" );

	public static bool operator ==( Ownership a, Ownership b ) => a is null ? b is null : a.Equals( b );

	public static bool operator !=( Ownership a, Ownership b ) => !(a == b);

	public override string ToString() => Kind switch
	{
		OwnerKind.Player => $"player {SteamId}",
		OwnerKind.Organization => $"organization {OrgId}",
		OwnerKind.Public => "the public",
		OwnerKind.Government => "the government",
		_ => "nobody",
	};
}
