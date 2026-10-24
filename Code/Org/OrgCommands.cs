namespace Sandbox;

/// <summary>
/// Console commands for testing organizations. They run on the host and refuse outside the editor; the destructive ones
/// also refuse unless <c>progress_test_data</c> is on, so they never touch the real file. See docs/organizations.md.
/// </summary>
public static class OrgCommands
{
	static bool Allowed() => Application.IsEditor && Networking.IsHost;

	static bool OnTestData() => OrgRegistry.FileName != "orgs.json";

	/// <summary>Register an organization for yourself: <c>org_create "Night Owls"</c>.</summary>
	[ConCmd( "org_create" )]
	public static void Create( string name )
	{
		if ( !Allowed() )
			return;

		var me = Connection.Local;
		var result = OrgRegistry.Create( me.SteamId, me.DisplayName, name, OrgCatalog.Icons[0].Id, OrgCatalog.DefaultColor );
		Log.Info( result.Message );
	}

	/// <summary>
	/// Put a made-up player in your organization to try the split without a second client:
	/// <c>org_member_add 76561198000000001 Mule 30</c> (id, nickname, share).
	/// </summary>
	[ConCmd( "org_member_add" )]
	public static void AddMember( ulong steamId, string nickname = "Test member", int share = 0 )
	{
		if ( !Allowed() || !OnTestData() )
		{
			Log.Info( "Only in the editor, with progress_test_data on" );
			return;
		}

		if ( OrgRegistry.OrgOf( Connection.Local.SteamId ) is not { } org )
		{
			Log.Info( "Create an organization first (org_create)" );
			return;
		}

		var check = OrgRules.CheckInvite(
			org, [], Connection.Local.SteamId, steamId, OrgRegistry.OrgOf( steamId ) is not null, OrgRole.Member, share );

		if ( check is not null )
		{
			Log.Info( check );
			return;
		}

		OrgRegistry.DebugAddMember(
			org, new() { SteamId = steamId, Name = nickname, Role = OrgRole.Member, SharePercent = share, Joined = OrgRegistry.Now } );

		Log.Info( $"{nickname} added to {org.Name}" );
	}

	/// <summary>
	/// Send yourself an invitation from a made-up organization, to try the invitation window without a second client:
	/// <c>org_invite_test "Test Syndicate" Courier 15</c> (organization, role, share, and true to keep the wait after a rejection, which is cleared by default). Only with
	/// <c>progress_test_data</c> on, and you must be in no organization.
	/// </summary>
	[ConCmd( "org_invite_test" )]
	public static void InviteTest(
		string orgName = "Test Syndicate", string role = "Member", int share = 10, bool respectCooldown = false )
	{
		if ( !Allowed() || !OnTestData() )
		{
			Log.Info( "Only in the editor, with progress_test_data on" );
			return;
		}

		if ( !System.Enum.TryParse<OrgRole>( role, true, out var parsed ) || !System.Enum.IsDefined( parsed ) )
		{
			Log.Info( "Roles: Manager, Grower, Presser, Packer, Courier, Member" );
			return;
		}

		var me = Connection.Local;
		var result = OrgRegistry.DebugInvite( me.SteamId, me.DisplayName, orgName, parsed, share, respectCooldown );
		Log.Info( result.Message );

		if ( result.Ok )
			GameManager.Current?.Notify( me, $"{OrgRules.NormalizeName( orgName )} invited you." );
	}

	/// <summary>Log every organization, its members and shares, the waiting invitations and the shares in transit.</summary>
	[ConCmd( "org_dump" )]
	public static void Dump()
	{
		if ( !Allowed() )
			return;

		Log.Info( $"{OrgRegistry.All.Count} organizations in {OrgRegistry.FileName}" );

		foreach ( var org in OrgRegistry.All )
		{
			Log.Info( $"{org.Name} [{org.Id}] icon {org.IconId} {org.IconColor}, founder {org.FounderId}, "
			          + $"{OrgRules.ShareTotal( org.Members )}% shared, {org.Payouts.Count} payouts" );

			foreach ( var member in org.Members )
				Log.Info( $"  {OrgRegistry.NameOf( member )} ({member.SteamId}) {member.Role} {member.SharePercent}%" );

			foreach ( var invite in OrgRegistry.InvitesOf( org.Id ) )
				Log.Info( $"  invited {invite.InviteeName} as {invite.Role} {invite.SharePercent}%" );
		}

		foreach ( var pending in OrgRegistry.Pending )
			Log.Info( $"  waiting: {pending.Amount} {pending.Currency} for {pending.MemberId} from {pending.OrgName}" );
	}

	/// <summary>Delete every organization. Only with <c>progress_test_data on</c>.</summary>
	[ConCmd( "org_clear" )]
	public static void Clear()
	{
		if ( !Allowed() || !OnTestData() )
		{
			Log.Info( "Only in the editor, with progress_test_data on" );
			return;
		}

		OrgRegistry.Clear();
		Log.Info( "Organizations cleared" );
	}
}
