using System.Threading.Tasks;

namespace Sandbox;

/// <summary>What an organization action did: whether it worked, and a sentence for the player.</summary>
public readonly record struct OrgResult( bool Ok, string Message )
{
	public static OrgResult Success( string message ) => new( true, message );

	public static OrgResult Fail( string message ) => new( false, message );
}

/// <summary>
/// Every organization on the server, kept in <c>orgs.json</c> (<c>orgs_test.json</c> under <c>progress_test_data</c>) so
/// a restart does not forget them: members, roles and shares, invitations, the last payouts, and the shares that wait
/// for a member whose record is not in memory. Every change goes through here, is checked by <see cref="OrgRules"/>
/// first and is written at once. A player is found from their Steam id (<see cref="OrgOf"/>), also while away.
/// </summary>
/// <remarks>
/// Host only. Not <c>readonly</c> (see AGENTS.md, "Static state and hot reload"): loaded the first time it is asked, and
/// the index of Steam ids is rebuilt from the data every time it is loaded. Times are real unix seconds, so an
/// invitation runs out on the wall clock even when the world clock is skipped.
/// </remarks>
public static class OrgRegistry
{
	const string DefaultFile = "orgs.json";

	static OrgData data;
	static Dictionary<ulong, OrgRecord> byMember;
	static string fileName;

	/// <summary>The file in the data folder. A test uses another one so it never touches the real one.</summary>
	public static string FileName => string.IsNullOrEmpty( fileName ) ? DefaultFile : fileName;

	/// <summary>Real unix seconds now.</summary>
	public static long Now => (long)(System.DateTime.UtcNow - System.DateTime.UnixEpoch).TotalSeconds;

	/// <summary>Keep the data in another file from now on, and read it again. For tests. Host only.</summary>
	public static void UseFile( string file )
	{
		if ( !Networking.IsHost )
			return;

		fileName = file;
		data = null;
		byMember = null;
	}

	static OrgData Data => data ??= Load();

	static Dictionary<ulong, OrgRecord> Index
	{
		get
		{
			if ( byMember is null )
			{
				byMember = new();

				foreach ( var org in Data.Orgs )
				{
					foreach ( var member in org.Members )
						byMember[member.SteamId] = org;
				}
			}

			return byMember;
		}
	}

	/// <summary>Goes up on every change, so a panel can tell it must draw again.</summary>
	public static int Version { get; private set; }

	public static IReadOnlyList<OrgRecord> All => Data.Orgs;

	public static OrgRecord Find( string orgId ) => Data.Orgs.FirstOrDefault( x => x.Id == orgId );

	/// <summary>The organization this player is in, or null. Works for a player who is away.</summary>
	public static OrgRecord OrgOf( ulong steamId ) => steamId == 0 ? null : Index.GetValueOrDefault( steamId );

	/// <summary>The nickname to show for a member: the one in the game now, else the last one seen.</summary>
	public static string NameOf( OrgMember member ) =>
		Connection.All.FirstOrDefault( x => x.SteamId == member.SteamId )?.DisplayName is { Length: > 0 } live
			? live
			: member.Name;

	// -- Changes ------------------------------------------------------------------------------------------------------

	/// <summary>Why this organization can't be made, or null. Costs nothing.</summary>
	public static string CheckCreate( ulong founder, string name, string iconId, string color )
	{
		if ( founder == 0 )
			return "No account.";

		if ( OrgOf( founder ) is not null )
			return "You are already in an organization.";

		if ( OrgRules.CheckName( name, Data.Orgs.Select( x => x.Name ) ) is { } nameError )
			return nameError;

		if ( !OrgRules.IsIcon( iconId ) )
			return "Pick an icon.";

		return OrgRules.NormalizeColor( color ) is null ? "The colour is not a hex colour like #b48cff." : null;
	}

	/// <summary>Found an organization. The founder is its Owner with no share (so nothing changes until they share).</summary>
	public static OrgResult Create( ulong founder, string founderName, string name, string iconId, string color )
	{
		if ( CheckCreate( founder, name, iconId, color ) is { } error )
			return OrgResult.Fail( error );

		var now = Now;

		var org = new OrgRecord
		{
			Id = System.Guid.NewGuid().ToString( "N" )[..12],
			Name = OrgRules.NormalizeName( name ),
			IconId = iconId,
			IconColor = OrgRules.NormalizeColor( color ),
			FounderId = founder,
			Created = now,
		};

		org.Members.Add( new() { SteamId = founder, Name = founderName ?? "", Role = OrgRole.Owner, Joined = now } );
		Data.Orgs.Add( org );
		Index[founder] = org;
		Save();
		return OrgResult.Success( $"{org.Name} is registered." );
	}

	public static OrgResult Invite( ulong actor, ulong invitee, string inviteeName, OrgRole role, int share )
	{
		if ( OrgOf( actor ) is not { } org )
			return OrgResult.Fail( "You are not in an organization." );

		PurgeExpired();

		if ( OrgRules.CheckInvite(
			    org, InvitesOf( org.Id ), actor, invitee, OrgOf( invitee ) is not null, role, share, Now ) is { } error )
			return OrgResult.Fail( error );

		var inviter = org.MemberOf( actor );

		Data.Invites.Add( new()
		{
			Id = System.Guid.NewGuid().ToString( "N" )[..10],
			OrgId = org.Id,
			InviteeId = invitee,
			InviteeName = inviteeName ?? "",
			InviterId = actor,
			InviterName = NameOf( inviter ),
			Role = role,
			SharePercent = share,
			Created = Now,
		} );

		Save();
		return OrgResult.Success( $"Invitation sent to {inviteeName}." );
	}

	/// <summary>Take back an invitation. Whoever may invite in that organization can.</summary>
	public static OrgResult CancelInvite( ulong actor, string inviteId )
	{
		var invite = Data.Invites.FirstOrDefault( x => x.Id == inviteId );

		if ( invite is null || OrgOf( actor ) is not { } org || org.Id != invite.OrgId
		     || !OrgRules.Can( org.MemberOf( actor ).Role, OrgAction.Invite ) )
			return OrgResult.Fail( "That invitation can't be cancelled." );

		Data.Invites.Remove( invite );
		Save();
		return OrgResult.Success( "Invitation cancelled." );
	}

	/// <summary>The invitations waiting for this player (the ones that ran out are dropped).</summary>
	public static IReadOnlyList<OrgInvite> InvitesFor( ulong steamId )
	{
		PurgeExpired();
		return Data.Invites.Where( x => x.InviteeId == steamId ).ToList();
	}

	/// <summary>The invitations an organization has waiting.</summary>
	public static IReadOnlyList<OrgInvite> InvitesOf( string orgId ) =>
		Data.Invites.Where( x => x.OrgId == orgId ).ToList();

	/// <summary>Join through an invitation. Everything is checked again: the invitation may be old.</summary>
	public static OrgResult Accept( ulong invitee, string inviteeName, string inviteId )
	{
		PurgeExpired();
		var invite = Data.Invites.FirstOrDefault( x => x.Id == inviteId && x.InviteeId == invitee );

		if ( invite is null )
			return OrgResult.Fail( "That invitation is gone." );

		var org = Find( invite.OrgId );

		if ( OrgRules.CheckAccept( org, invite, OrgOf( invitee ) is not null, Now ) is { } error )
		{
			Data.Invites.Remove( invite );
			Save();
			return OrgResult.Fail( error );
		}

		org.Members.Add( new()
		{
			SteamId = invitee, Name = inviteeName ?? "", Role = invite.Role, SharePercent = invite.SharePercent,
			Joined = Now,
		} );

		Index[invitee] = org;

		// Their other invitations are of no use now.
		Data.Invites.RemoveAll( x => x.InviteeId == invitee );
		Save();
		return OrgResult.Success( $"You joined {org.Name}." );
	}

	public static OrgResult Decline( ulong invitee, string inviteId )
	{
		var invite = Data.Invites.FirstOrDefault( x => x.Id == inviteId && x.InviteeId == invitee );

		if ( invite is null )
			return OrgResult.Fail( "That invitation is gone." );

		Data.Invites.Remove( invite );
		Save();
		return OrgResult.Success( "Invitation declined." );
	}

	/// <summary>Leave. The Owner can't: they disband.</summary>
	public static OrgResult Leave( ulong actor )
	{
		if ( OrgOf( actor ) is not { } org || org.MemberOf( actor ) is not { } member )
			return OrgResult.Fail( "You are not in an organization." );

		if ( member.Role == OrgRole.Owner )
			return OrgResult.Fail( "The Owner can't leave. Disband the organization instead." );

		org.Members.Remove( member );
		Index.Remove( actor );
		Save();
		return OrgResult.Success( $"You left {org.Name}." );
	}

	public static OrgResult Kick( ulong actor, ulong target )
	{
		if ( OrgOf( actor ) is not { } org || org.MemberOf( actor ) is not { } by || OrgOf( target ) != org
		     || org.MemberOf( target ) is not { } member )
			return OrgResult.Fail( "That member can't be removed." );

		if ( !OrgRules.CanKick( by.Role, member.Role, actor == target ) )
			return OrgResult.Fail( "You can't remove that member." );

		org.Members.Remove( member );
		Index.Remove( target );
		Save();
		return OrgResult.Success( $"{NameOf( member )} was removed." );
	}

	public static OrgResult SetRole( ulong actor, ulong target, OrgRole role )
	{
		if ( OrgOf( actor ) is not { } org || org.MemberOf( actor ) is not { } by || OrgOf( target ) != org
		     || org.MemberOf( target ) is not { } member )
			return OrgResult.Fail( "That member can't be changed." );

		if ( !OrgRules.CanChangeRole( by.Role, member.Role, role, actor == target ) )
			return OrgResult.Fail( "You can't give that role." );

		member.Role = role;
		Save();
		return OrgResult.Success( $"{NameOf( member )} is now {role}." );
	}

	public static OrgResult SetShare( ulong actor, ulong target, int share )
	{
		if ( OrgOf( actor ) is not { } org || org.MemberOf( actor ) is not { } by || OrgOf( target ) != org
		     || org.MemberOf( target ) is not { } member )
			return OrgResult.Fail( "That member can't be changed." );

		if ( !OrgRules.CanChangeShare( by.Role, member.Role, actor == target ) )
			return OrgResult.Fail( "You can't change that share." );

		if ( OrgRules.CheckShare( org.Members, target, share ) is { } error )
			return OrgResult.Fail( error );

		member.SharePercent = share;
		Save();
		return OrgResult.Success( $"{NameOf( member )} gets {share}%." );
	}

	/// <summary>Rename it and change its icon and colour. Owner only.</summary>
	public static OrgResult Restyle( ulong actor, string name, string iconId, string color )
	{
		if ( OrgOf( actor ) is not { } org || !OrgRules.Can( org.MemberOf( actor ).Role, OrgAction.Restyle ) )
			return OrgResult.Fail( "Only the Owner can change that." );

		if ( OrgRules.CheckName( name, Data.Orgs.Where( x => x != org ).Select( x => x.Name ) ) is { } nameError )
			return OrgResult.Fail( nameError );

		if ( !OrgRules.IsIcon( iconId ) || OrgRules.NormalizeColor( color ) is not { } normalized )
			return OrgResult.Fail( "Pick an icon and a colour." );

		org.Name = OrgRules.NormalizeName( name );
		org.IconId = iconId;
		org.IconColor = normalized;
		Save();
		return OrgResult.Success( "Saved." );
	}

	/// <summary>Close it for good. Owner only. Shares still waiting for absent members are kept and delivered.</summary>
	public static OrgResult Disband( ulong actor )
	{
		if ( OrgOf( actor ) is not { } org || !OrgRules.Can( org.MemberOf( actor ).Role, OrgAction.Disband ) )
			return OrgResult.Fail( "Only the Owner can disband the organization." );

		foreach ( var member in org.Members )
			Index.Remove( member.SteamId );

		Data.Orgs.Remove( org );
		Data.Invites.RemoveAll( x => x.OrgId == org.Id );
		Save();
		return OrgResult.Success( $"{org.Name} was disbanded." );
	}

	/// <summary>Put a member in without an invitation. For the test command only.</summary>
	internal static void DebugAddMember( OrgRecord org, OrgMember member )
	{
		org.Members.Add( member );
		Index[member.SteamId] = org;
		Save();
	}

	/// <summary>Forget everything. For tests (the dev command refuses outside test data).</summary>
	public static void Clear()
	{
		data = new();
		byMember = null;
		Save();
	}

	// -- Shares waiting for an absent member --------------------------------------------------------------------------

	public static IReadOnlyList<OrgPendingCredit> Pending => Data.Pending;

	/// <summary>Write down a payout that can't reach a member's record yet. <see cref="FlushPendingAsync"/> delivers it.</summary>
	internal static void AddPending( ulong memberId, OrgCurrency currency, long amount, string orgName, string orderId )
	{
		Data.Pending.Add( new()
		{
			Id = System.Guid.NewGuid().ToString( "N" )[..10], MemberId = memberId, Currency = currency, Amount = amount,
			OrgName = orgName, OrderId = orderId,
		} );
	}

	static bool flushing;

	/// <summary>
	/// Put the waiting shares into the records of the members they are for: load the record, add what fits under the
	/// wallet cap, save it, then take the entry off. What does not fit stays and is tried again. Nothing is lost if the
	/// game stops in between (the entry is only removed after the record is saved).
	/// </summary>
	public static async Task FlushPendingAsync()
	{
		if ( flushing || !Networking.IsHost || Data.Pending.Count == 0 )
			return;

		flushing = true;

		try
		{
			foreach ( var credit in Data.Pending.ToList() )
			{
				var before = credit.Amount;
				var record = await PlayerProgression.LoadAsync( credit.MemberId );
				var given = OrgPayouts.CreditRecord(
					credit.MemberId, record, credit.Currency, credit.Amount, $"{credit.OrgName}: waiting share" );

				if ( given > 0 )
					await PlayerProgression.SaveAsync( credit.MemberId );

				PlayerProgression.Release( credit.MemberId );

				credit.Amount = before - given;

				if ( credit.Amount <= 0 )
					Data.Pending.Remove( credit );

				Save();
			}
		}
		catch ( System.Exception e )
		{
			Log.Warning( e, $"OrgRegistry: couldn't deliver waiting shares: {e.Message}" );
		}
		finally
		{
			flushing = false;
		}
	}

	// -- File ---------------------------------------------------------------------------------------------------------

	static void PurgeExpired()
	{
		var now = Now;

		if ( Data.Invites.RemoveAll( x => OrgRules.IsExpired( x, now ) ) > 0 )
			Save();
	}

	static OrgData Load()
	{
		try
		{
			if ( FileSystem.Data is { } files && FileProgressStore.TryReadSafely<OrgData>( files, FileName ) is { } saved )
			{
				saved.Orgs ??= new();
				saved.Invites ??= new();
				saved.Pending ??= new();
				return saved;
			}
		}
		catch ( System.Exception e )
		{
			Log.Warning( e, $"Couldn't read {FileName}: {e.Message}" );
		}

		return new();
	}

	internal static void Save()
	{
		Version++;

		try
		{
			if ( FileSystem.Data is { } files )
				FileProgressStore.WriteSafely( files, "", FileName, Json.Serialize( Data ) );
		}
		catch ( System.Exception e )
		{
			Log.Warning( e, $"Couldn't save {FileName}: {e.Message}" );
		}
	}
}
