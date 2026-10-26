namespace Sandbox;

/// <summary>
/// Who legally owns an object, on the object itself: a storage box, a car, a door, a computer, a house. The title is
/// kept in <see cref="OwnershipRegistry"/> and mirrored here in a field the host syncs, so every machine can read the
/// owner without the file. A player's request to change hands goes through <see cref="Transfer"/>, which asks
/// <see cref="OwnershipRules"/> first; a sale, a purchase or a repossession is the game's own call and writes
/// <see cref="OwnershipRegistry.Transfer"/> direct.
/// </summary>
/// <remarks>
/// One of these per object, and none on its children: it is the object's title, and everything on it (contents,
/// health, a model) belongs to whoever owns the object. That is why an owner needs no type: whatever the object is,
/// asking it for its components says so.
/// <para>
/// The registry is the truth. <see cref="DefaultOwner"/> is only the title an object is <em>made</em> with, written
/// down once (a house in the scene that belongs to the government, a bought box that belongs to its buyer); from then
/// on the record wins, and a change of hands only ever writes the record.
/// </para>
/// </remarks>
public sealed class OwnershipComponent : Component
{
	/// <summary>
	/// A key of its own, for a thing whose id is not its object id: a car that is saved by a name of its own, a
	/// business, a licence. Empty uses <see cref="OwnershipRegistry.KeyOf"/>, which is scene and object id.
	/// </summary>
	[Property] public string ThingId { get; set; } = "";

	/// <summary>
	/// The owner the object is made with, when the registry has no record of it yet: <c>government</c> for a police
	/// station, <c>public</c> for a park, a player for a bought box. Ignored as soon as there is a record.
	/// </summary>
	[Property] public Ownership DefaultOwner { get; set; } = Ownership.Nobody;

	/// <summary>
	/// Who owns it now, as <see cref="Ownership.Key"/>. Set by the host, from the registry. Empty is nobody.
	/// Not a <see cref="Property"/>: the registry's record is what a title is written to, and a copy saved in a scene
	/// would come back as a stale owner the next time the scene is loaded.
	/// </summary>
	[Sync( SyncFlags.FromHost )] public string OwnerKey { get; set; } = "";

	/// <summary>Who owns it, as every machine sees it. Nobody until the host has said.</summary>
	public Ownership Owner => Ownership.Parse( OwnerKey );

	/// <summary>The key the registry keeps its title under.</summary>
	public string Key => OwnershipRegistry.KeyOf( GameObject );

	/// <summary>
	/// The record: since when, and the last changes of hands. Host only, and null on an object that is not the
	/// registry's (a scene object nobody has written down).
	/// </summary>
	public OwnershipRecord Record => Networking.IsHost ? OwnershipRegistry.Find( Key ) : null;

	// -- Permission ---------------------------------------------------------------------------------------------------

	/// <summary>
	/// Whether this player may use the object: it is theirs, theirs through their organization, or nobody's.
	/// </summary>
	public bool CanUse( ulong steamId ) => Can( OwnershipRight.Use, steamId );

	/// <summary>Whether this player may change the object: its name, what is in it, who else may use it.</summary>
	public bool CanManage( ulong steamId ) => Can( OwnershipRight.Manage, steamId );

	/// <summary>Whether this player may hand the object to somebody else.</summary>
	public bool CanTransfer( ulong steamId ) => Can( OwnershipRight.Transfer, steamId );

	/// <summary>
	/// Whether this player may do this with the object, asked of <see cref="OwnershipRules"/>.
	/// </summary>
	/// <remarks>
	/// The actor is worked out from the player: their organization, and the role they hold in it, if the host knows
	/// it (<see cref="OrgRegistry"/> is the host's). A client knows its own player and nothing of its organization,
	/// so it gets the answer a plain member would: right for everything an organization shares, wrong for the founder
	/// only rights a founder has. A prompt that looks wrong on a client is not a security hole, because what is done
	/// is done on the host.
	/// </remarks>
	public bool Can( OwnershipRight right, ulong steamId )
	{
		var org = Networking.IsHost ? OrgRegistry.OrgOf( steamId ) : null;
		var actor = org is null ? Ownership.Player( steamId ) : Ownership.Organization( org.Id );
		var role = org?.MemberOf( steamId )?.Role ?? OrgRole.Member;

		return OwnershipRules.Allows( Owner, right, actor, role );
	}

	// -- Changing hands -----------------------------------------------------------------------------------------------

	/// <summary>
	/// Hand the object to somebody else, at a player's request. Checked against <see cref="OwnershipRules"/> and
	/// written to the registry, which syncs it here. Host only. Returns whether it changed. To release it, pass
	/// <see cref="Ownership.Nobody"/>.
	/// </summary>
	public bool Transfer( Ownership to, ulong by, string reason = "" ) =>
		Networking.IsHost
		&& OwnershipRules.IsTransferable( to )
		&& CanTransfer( by )
		&& OwnershipRegistry.Transfer( Key, to, by, reason );

	/// <summary>
	/// Claim an object nobody owns, for this player. What a first use does: a box, a plot, a car left in the street.
	/// Host only. Returns whether it was theirs to claim.
	/// </summary>
	public bool Claim( ulong steamId ) =>
		Owner.IsNobody && OwnershipRegistry.Transfer( Key, Ownership.Player( steamId ), steamId, "claimed" );

	/// <summary>
	/// Take the synced copy from the host without touching the registry. Called by
	/// <see cref="OwnershipRegistry"/> when the record changes.
	/// </summary>
	internal void Adopt( Ownership owner ) => OwnerKey = (owner ?? Ownership.Nobody).Key;

	/// <summary>Clear the synced copy: the object has no title any more. Called by the registry's wipe.</summary>
	internal void Forget() => OwnerKey = "";

	protected override void OnStart()
	{
		if ( !Game.IsPlaying || !Networking.IsHost )
			return;

		// A record of this object wins: it is where every change of hands since it was made lives.
		if ( OwnershipRegistry.Find( Key ) is { } record )
		{
			OwnerKey = record.Owner;
			return;
		}

		// Nobody wrote this object down yet: the title it is made with is that record, made now.
		if ( DefaultOwner.IsOwned )
			OwnershipRegistry.Transfer( Key, DefaultOwner, 0, "in the scene" );
	}

	// -- Asking about objects -----------------------------------------------------------------------------------------

	/// <summary>The ownership component on an object or one of its parents, or null.</summary>
	public static OwnershipComponent On( GameObject go ) =>
		go.IsValid() ? go.Components.Get<OwnershipComponent>( FindMode.EverythingInSelfAndParent ) : null;

	/// <summary>Who owns an object, or nobody. The one call most checks need.</summary>
	public static Ownership TitleOf( GameObject go ) => On( go )?.Owner ?? Ownership.Nobody;

	/// <summary>Who owns the object this component is on, or nobody.</summary>
	public static Ownership TitleOf( Component component ) => TitleOf( component?.GameObject );

	/// <summary>Whether this player is the legal owner of the object.</summary>
	public static bool IsOwnedBy( GameObject go, ulong steamId ) => TitleOf( go ).HeldBy( steamId );

	/// <summary>Everything in the scene this owner holds, to look at or act on.</summary>
	public static List<OwnershipComponent> Owned( Ownership owner )
	{
		if ( owner is null || !owner.IsOwned || !Game.ActiveScene.IsValid() )
			return [];

		return Game.ActiveScene.GetAllComponents<OwnershipComponent>().Where( x => x.Owner.Equals( owner ) ).ToList();
	}

	/// <summary>Everything in the scene this player owns.</summary>
	public static List<OwnershipComponent> OwnedBy( ulong steamId ) => Owned( Ownership.Player( steamId ) );

	/// <summary>Everything in the scene this organization owns.</summary>
	public static List<OwnershipComponent> OwnedByOrg( string orgId ) => Owned( Ownership.Organization( orgId ) );

	/// <summary>
	/// Everything in the scene this player owns that carries a component of this type: every car they own, every
	/// computer. The type is what an object is, asked of the object rather than written beside its owner.
	/// </summary>
	public static List<T> OwnedBy<T>( ulong steamId ) where T : Component => OwnedBy( steamId )
		.Select( x => x.Components.Get<T>( FindMode.EverythingInSelfAndParent ) )
		.Where( x => x.IsValid() )
		.ToList();
}
