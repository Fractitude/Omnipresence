namespace Sandbox;

/// <summary>What the invited player's client is told about one invitation: enough to draw the window, nothing more.</summary>
public sealed class OrgInviteView
{
	public string Id { get; set; } = "";
	public string OrgName { get; set; } = "";
	public string Glyph { get; set; } = "shield";
	public string Color { get; set; } = OrgCatalog.DefaultColor;
	public string Role { get; set; } = "";
	public int Share { get; set; }
	public string From { get; set; } = "";
}

/// <summary>
/// Carries organization invitations to the players they are for, and their answers back. The host watches the registry
/// and tells each connection its own invitations (only its own: the rpc is filtered to that connection, so no client
/// learns of another's); a client keeps the list in <see cref="Mine"/> for <see cref="OrgInviteMenu"/>. An answer is
/// only an invitation id; the host checks it against the caller and applies it through <see cref="OrgRegistry"/>.
/// Made in every scene by the engine.
/// </summary>
public sealed class OrgInviteSystem : GameObjectSystem<OrgInviteSystem>
{
	// Not readonly: a hot reload resets a readonly list of the engine's types.
	static List<OrgInviteView> mine = new();

	/// <summary>The invitations the host sent to this computer's player (client side).</summary>
	public static IReadOnlyList<OrgInviteView> Mine => mine;

	/// <summary>Goes up whenever <see cref="Mine"/> was replaced.</summary>
	public static int Version { get; private set; }

	// Host side: what each connection was last told (ids joined), and when.
	readonly Dictionary<System.Guid, (string Ids, RealTimeSince At)> told = new();
	RealTimeSince sinceCheck;

	public OrgInviteSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.StartUpdate, 2, Tick, "OrgInviteSystem.Tick" );
	}

	void Tick()
	{
		if ( Scene != Game.ActiveScene || !Game.IsPlaying || Scene.IsEditor || !Networking.IsHost || sinceCheck < 0.5f )
			return;

		sinceCheck = 0;

		foreach ( var gone in OrgRegistry.SweepVoid() )
		{
			if ( Connection.All.FirstOrDefault( x => x.SteamId == gone.InviteeId ) is { } who )
				Notify( who, "An organization invitation you had is no longer valid." );
		}

		foreach ( var id in told.Keys.Where( x => Connection.All.All( y => y.Id != x ) ).ToList() )
			told.Remove( id );

		foreach ( var connection in Connection.All )
		{
			// Wait until the player is in the world, so the client has its scene to receive the message in.
			if ( Players.For<PlayerController>( connection ) is null )
				continue;

			var invites = OrgRegistry.InvitesFor( connection.SteamId );
			var ids = string.Join( ',', invites.Select( x => x.Id ) );
			told.TryGetValue( connection.Id, out var last );

			// Resend now and then while some wait, in case a message went before the client could take it.
			if ( ids == (last.Ids ?? "") && (ids == "" || last.At < 20) )
				continue;

			told[connection.Id] = (ids, 0);
			var views = invites.Select( ToView ).ToList();

			using ( Rpc.FilterInclude( connection ) )
				ReceiveRpc( Json.Serialize( views ) );
		}
	}

	static OrgInviteView ToView( OrgInvite invite )
	{
		var org = OrgRegistry.Find( invite.OrgId );

		return new()
		{
			Id = invite.Id,
			OrgName = org?.Name ?? "(gone)",
			Glyph = OrgCatalog.IconOf( org?.IconId )?.Glyph ?? "shield",
			Color = org?.IconColor ?? OrgCatalog.DefaultColor,
			Role = OrgCatalog.RoleLabel( invite.Role ),
			Share = invite.SharePercent,
			From = invite.InviterName,
		};
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ReceiveRpc( string json )
	{
		try
		{
			mine = Json.Deserialize<List<OrgInviteView>>( json ) ?? new();
		}
		catch ( System.Exception e )
		{
			Log.Warning( e, "Couldn't read the invitations" );
			mine = new();
		}

		Version++;
	}

	/// <summary>The invited player pressed Accept or Reject in the window. Only the id comes; the host does the rest.</summary>
	[Rpc.Host]
	internal static void AnswerRpc( string inviteId, bool accept )
	{
		if ( Rpc.Caller is { } caller )
			Answer( caller, inviteId, accept, true );
	}

	/// <summary>
	/// Apply an answer from <paramref name="who"/>, on the host. Accepting joins the organization if everything still
	/// holds (<see cref="OrgRules.CheckAccept"/>; if not, the invitation is removed and the player is told why).
	/// Rejecting removes it and tells whoever sent it. The registry only finds invitations of this player.
	/// </summary>
	internal static OrgResult Answer( Connection who, string inviteId, bool accept, bool tellInvitee )
	{
		if ( !Networking.IsHost || who is null )
			return OrgResult.Fail( "Only the host answers invitations." );

		OrgResult result;

		if ( accept )
		{
			result = OrgRegistry.Accept( who.SteamId, who.DisplayName, inviteId );
		}
		else
		{
			result = OrgRegistry.Decline( who.SteamId, inviteId, out var invite );

			if ( result.Ok && invite is not null
			     && Connection.All.FirstOrDefault( x => x.SteamId == invite.InviterId ) is { } inviter )
			{
				var name = OrgRegistry.Find( invite.OrgId )?.Name ?? "the organization";
				Notify( inviter, $"{who.DisplayName} rejected your invitation to {name}." );
			}
		}

		if ( tellInvitee )
			Notify( who, result.Message );

		return result;
	}

	static void Notify( Connection to, string text ) => GameManager.Current?.Notify( to, text );
}
