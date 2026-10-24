using System;

namespace OmnipresenceApps;

/// <summary>
/// Everything Government does: register an organization, run it (invite, roles, shares, remove), answer invitations
/// and read the payouts. Runs in the host's copy only (the app is shared), because organizations are the host's.
/// </summary>
/// <remarks>
/// Who is acting is always the player in the seat, worked out from the computer. An action carries only ids, a role or
/// icon name, a small number or typed text; <see cref="OrgRegistry"/> checks the permission, the shares and every other
/// rule again, so an action can't do more than the seated player's role allows. Nothing here pays anyone: money moves
/// only in <see cref="OrgPayouts"/>.
/// </remarks>
public sealed class GovernmentViewModel
{
	public const string TabAction = "tab";
	public const string NameAction = "name";
	public const string IconAction = "icon";
	public const string ColorAction = "color";
	public const string RegisterAction = "register";
	public const string EditStartAction = "edit-start";
	public const string EditSaveAction = "edit-save";
	public const string EditCancelAction = "edit-cancel";
	public const string PickAction = "pick";
	public const string InviteRoleAction = "invite-role";
	public const string InviteShareAction = "invite-share";
	public const string InviteSendAction = "invite-send";
	public const string InviteCancelAction = "invite-cancel";
	public const string SelectAction = "select";
	public const string MemberRoleAction = "member-role";
	public const string MemberShareAction = "member-share";
	public const string AskAction = "ask";
	public const string YesAction = "yes";
	public const string NoAction = "no";
	public const string AcceptAction = "accept";
	public const string DeclineAction = "decline";

	/// <summary>How much a share button moves a share.</summary>
	public static readonly int[] ShareSteps = [-5, -1, 1, 5];

	readonly Func<Connection> getSeat;
	readonly Func<NaviComputer> getComputer;

	GovernmentTab tab;
	OrgDraft draft = new();
	bool editing;
	string confirm = "";
	string notice = "";
	string selected = "";
	string pickedInvitee = "";
	OrgRole inviteRole = OrgRole.Member;
	int inviteShare;
	Guid lastSeatId;
	string signature;
	int revision;

	public GovernmentViewModel( Func<Connection> getSeat, Func<NaviComputer> getComputer )
	{
		this.getSeat = getSeat;
		this.getComputer = getComputer;
		UiState = Build();
	}

	public GovernmentUiState UiState { get; private set; }

	/// <summary>
	/// Once a frame: start over when the seat changed, and draw again if anything drawn changed.
	/// </summary>
	public void Tick()
	{
		var seat = getSeat();

		if ( (seat?.Id ?? default) != lastSeatId )
		{
			lastSeatId = seat?.Id ?? default;
			tab = GovernmentTab.Organization;
			draft = new();
			editing = false;
			confirm = "";
			notice = "";
			selected = "";
			pickedInvitee = "";
			inviteRole = OrgRole.Member;
			inviteShare = 0;
		}

		if ( Signature( seat ) != signature )
			UiState = Build();
	}

	public void Act( string action, string[] args )
	{
		var seat = getSeat();
		var arg = args.Length > 0 ? args[0] : "";

		if ( seat is not null && GameAppNetwork.IsOnline( getComputer() ) )
			Apply( seat, action, arg );

		// Typing a name changes nothing that is drawn, so it does not draw again (a redraw would move the caret).
		if ( Signature( seat ) != signature )
			UiState = Build();
	}

	void Apply( Connection seat, string action, string arg )
	{
		var me = seat.SteamId;

		switch ( action )
		{
			case TabAction when Enum.TryParse<GovernmentTab>( arg, true, out var next ) && Enum.IsDefined( next ):
				tab = next;
				notice = "";
				confirm = "";
				break;

			// Typed text is kept without drawing again: the box already shows it.
			case NameAction:
				draft = draft with { Name = Clip( arg, 40 ) };
				UiState = UiState with { Draft = UiState.Draft with { Name = draft.Name } };
				return;

			case IconAction when OrgRules.IsIcon( arg ):
				draft = draft with { IconId = arg };
				break;

			case ColorAction:
				var color = OrgRules.NormalizeColor( arg );
				draft = draft with { ColorText = Clip( arg, 9 ), Color = color ?? "" };
				break;

			case RegisterAction:
				Register( seat );
				break;

			case EditStartAction when OrgRegistry.OrgOf( me ) is { } org && OrgRules.Can( RoleOf( org, me ), OrgAction.Restyle ):
				editing = true;
				draft = new() { Name = org.Name, IconId = org.IconId, ColorText = org.IconColor, Color = org.IconColor };
				notice = "";
				break;

			case EditSaveAction when editing:
				var saved = OrgRegistry.Restyle( me, draft.Name, draft.IconId, draft.Color );
				notice = saved.Message;
				editing = !saved.Ok;
				break;

			case EditCancelAction:
				editing = false;
				draft = new();
				break;

			case PickAction:
				pickedInvitee = arg;
				break;

			case InviteRoleAction when Enum.TryParse<OrgRole>( arg, true, out var role ) && Enum.IsDefined( role ):
				inviteRole = role;
				break;

			case InviteShareAction when int.TryParse( arg, out var delta ) && ShareSteps.Contains( delta ):
				inviteShare = Math.Clamp( inviteShare + delta, 0, 100 );
				break;

			case InviteSendAction:
				SendInvite( seat );
				break;

			case InviteCancelAction:
				notice = OrgRegistry.CancelInvite( me, arg ).Message;
				break;

			case SelectAction:
				selected = selected == arg ? "" : arg;
				confirm = "";
				break;

			case MemberRoleAction when ulong.TryParse( selected, out var target )
			                           && Enum.TryParse<OrgRole>( arg, true, out var newRole ) && Enum.IsDefined( newRole ):
				notice = OrgRegistry.SetRole( me, target, newRole ).Message;
				break;

			case MemberShareAction when ulong.TryParse( selected, out var who ) && int.TryParse( arg, out var step )
			                            && ShareSteps.Contains( step ):
				var current = OrgRegistry.OrgOf( who )?.MemberOf( who )?.SharePercent ?? 0;
				notice = OrgRegistry.SetShare( me, who, Math.Clamp( current + step, 0, 100 ) ).Message;
				break;

			case AskAction when arg is "kick" or "disband" or "leave":
				confirm = arg;
				break;

			case NoAction:
				confirm = "";
				break;

			case YesAction:
				Confirmed( me );
				break;

			case AcceptAction:
				var joined = OrgRegistry.Accept( me, seat.DisplayName, arg );
				notice = joined.Message;

				if ( joined.Ok )
					tab = GovernmentTab.Organization;

				break;

			case DeclineAction:
				notice = OrgRegistry.Decline( me, arg ).Message;
				break;
		}
	}

	void Register( Connection seat )
	{
		var me = seat.SteamId;
		var wallet = PlayerWallet.For( seat );

		if ( OrgRegistry.CheckCreate( me, draft.Name, draft.IconId, draft.Color ) is { } error )
		{
			notice = error;
			return;
		}

		var fee = OrgCatalog.RegistrationFee;

		if ( fee > 0 && (wallet is null || !wallet.TrySpendCash( fee )) )
		{
			notice = $"Registering costs ${fee:N0}.";
			return;
		}

		var result = OrgRegistry.Create( me, seat.DisplayName, draft.Name, draft.IconId, draft.Color );

		// The fee is only kept for an organization that was made.
		if ( !result.Ok && fee > 0 )
			wallet?.AddCash( fee );

		notice = result.Message;

		if ( result.Ok )
			draft = new();
	}

	void SendInvite( Connection seat )
	{
		var target = Connection.All.FirstOrDefault( x => x.SteamId.ToString() == pickedInvitee );

		if ( target is null )
		{
			notice = "Pick a player first.";
			return;
		}

		var result = OrgRegistry.Invite( seat.SteamId, target.SteamId, target.DisplayName, inviteRole, inviteShare );
		notice = result.Message;

		if ( !result.Ok )
			return;

		pickedInvitee = "";
		GameManager.Current?.Notify(
			target, $"{seat.DisplayName} invited you to {OrgRegistry.OrgOf( seat.SteamId )?.Name}. Open Government on your computer." );
	}

	void Confirmed( ulong me )
	{
		var kind = confirm;
		confirm = "";

		switch ( kind )
		{
			case "kick" when ulong.TryParse( selected, out var target ):
				notice = OrgRegistry.Kick( me, target ).Message;
				selected = "";
				break;

			case "disband":
				notice = OrgRegistry.Disband( me ).Message;
				selected = "";
				break;

			case "leave":
				notice = OrgRegistry.Leave( me ).Message;
				break;
		}
	}

	static string Clip( string text, int length ) => (text ?? "").Length > length ? text[..length] : text ?? "";

	static OrgRole RoleOf( OrgRecord org, ulong steamId ) => org.MemberOf( steamId )?.Role ?? OrgRole.Member;

	static List<Connection> Candidates( Connection seat, OrgRecord org ) => Connection.All
		.Where( x => x != seat && OrgRegistry.OrgOf( x.SteamId ) is null
		             && (org is null || OrgRegistry.InvitesOf( org.Id ).All( y => y.InviteeId != x.SteamId )) )
		.OrderBy( x => x.DisplayName )
		.ToList();

	string Signature( Connection seat ) =>
		HashCode.Combine(
			seat?.Id,
			GameAppNetwork.IsOnline( getComputer() ),
			OrgRegistry.Version,
			(tab, editing, confirm, notice, selected, pickedInvitee),
			(inviteRole, inviteShare, draft.IconId, draft.Color),
			string.Join( ',', Connection.All.Select( x => $"{x.Id}:{x.DisplayName}" ) ),
			// Invitations run out on the wall clock.
			OrgRegistry.Now / 5,
			PlayerProgress.For( seat )?.Loaded ).ToString();

	GovernmentUiState Build()
	{
		var seat = getSeat();
		signature = Signature( seat );

		var state = new GovernmentUiState { Revision = ++revision, Tab = tab, Notice = notice, Draft = draft };

		if ( !GameAppNetwork.IsOnline( getComputer() ) )
			return state with { OfflineHint = GameAppNetwork.OfflineHint( getComputer() ) };

		if ( seat is null )
			return state with { Message = "Sit down to open Government." };

		if ( PlayerProgress.For( seat ) is not { Loaded: true } )
			return state with { Message = "Loading your account..." };

		var me = seat.SteamId;
		var org = OrgRegistry.OrgOf( me );
		var incoming = OrgRegistry.InvitesFor( me );

		if ( org is null )
			editing = false;

		return state with
		{
			HasAccount = true,
			Editing = editing,
			Confirm = confirm,
			InviteCount = incoming.Count,
			Draft = draft with { Fee = OrgCatalog.RegistrationFee > 0 ? $"${OrgCatalog.RegistrationFee:N0}" : "Free" },
			Org = org is null ? null : BuildOrg( seat, org ),
			Incoming = incoming.Select( ToIncoming ).ToList(),
			Payouts = org is null ? [] : BuildPayouts( org, me ),
		};
	}

	OrgView BuildOrg( Connection seat, OrgRecord org )
	{
		var me = seat.SteamId;
		var mine = org.MemberOf( me );
		var role = mine.Role;

		// A selection that is not in this organization any more is dropped.
		if ( selected != "" && !org.Members.Any( x => x.SteamId.ToString() == selected ) )
			selected = "";

		var rows = org.Members
			.OrderBy( x => x.Role )
			.ThenByDescending( x => x.SharePercent )
			.ThenBy( x => OrgRegistry.NameOf( x ) )
			.Select( x => new MemberRow(
				x.SteamId.ToString(), OrgRegistry.NameOf( x ), x.Role.ToString(), x.SharePercent, x.SteamId == me,
				x.SteamId.ToString() == selected ) )
			.ToList();

		MemberEdit edit = null;

		if ( selected != "" && org.Members.First( x => x.SteamId.ToString() == selected ) is { } target )
		{
			var self = target.SteamId == me;

			edit = new MemberEdit(
				OrgRegistry.NameOf( target ),
				target.Role.ToString(),
				target.SharePercent,
				OrgRules.CanChangeShare( role, target.Role, self ),
				OrgRules.CanKick( role, target.Role, self ),
				RolesFor( x => OrgRules.CanChangeRole( role, target.Role, x, self ), target.Role ) );
		}

		return new OrgView(
			org.Name,
			OrgCatalog.IconOf( org.IconId )?.Glyph ?? "shield",
			org.IconColor,
			role.ToString(),
			mine.SharePercent,
			OrgRules.ShareTotal( org.Members ),
			System.DateTimeOffset.FromUnixTimeSeconds( org.Created ).ToString( "yyyy-MM-dd" ),
			rows,
			OrgRules.Can( role, OrgAction.Invite ),
			OrgRules.Can( role, OrgAction.Restyle ),
			OrgRules.Can( role, OrgAction.Disband ),
			role != OrgRole.Owner,
			edit,
			OrgRules.Can( role, OrgAction.Invite ) ? BuildInviteForm( seat, org ) : null );
	}

	static IReadOnlyList<RoleChip> RolesFor( Func<OrgRole, bool> allowed, OrgRole current ) =>
		Enum.GetValues<OrgRole>()
			.Where( x => x != OrgRole.Owner )
			.Select( x => new RoleChip( x.ToString(), allowed( x ), x == current ) )
			.ToList();

	InviteForm BuildInviteForm( Connection seat, OrgRecord org )
	{
		var role = RoleOf( org, seat.SteamId );
		var candidates = Candidates( seat, org );

		if ( pickedInvitee != "" && candidates.All( x => x.SteamId.ToString() != pickedInvitee ) )
			pickedInvitee = "";

		var now = OrgRegistry.Now;

		return new InviteForm(
			candidates.Select( x => new CandidateRow( x.SteamId.ToString(), x.DisplayName, x.SteamId.ToString() == pickedInvitee ) )
				.ToList(),
			RolesFor( x => OrgRules.CanInviteAs( role, x ), inviteRole ),
			inviteShare,
			pickedInvitee != "",
			OrgRegistry.InvitesOf( org.Id )
				.Select( x => new SentInviteRow(
					x.Id, x.InviteeName, x.Role.ToString(), x.SharePercent, Countdown( OrgCatalog.InviteSeconds - (now - x.Created) ) ) )
				.ToList() );
	}

	IncomingInviteRow ToIncoming( OrgInvite invite )
	{
		var org = OrgRegistry.Find( invite.OrgId );

		return new IncomingInviteRow(
			invite.Id,
			org?.Name ?? "(gone)",
			OrgCatalog.IconOf( org?.IconId )?.Glyph ?? "shield",
			org?.IconColor ?? OrgCatalog.DefaultColor,
			invite.Role.ToString(),
			invite.SharePercent,
			invite.InviterName,
			Countdown( OrgCatalog.InviteSeconds - (OrgRegistry.Now - invite.Created) ) );
	}

	static string Countdown( long seconds ) =>
		seconds >= 60 ? $"{seconds / 60} min" : $"{Math.Max( seconds, 0 )} s";

	static IReadOnlyList<PayoutRow> BuildPayouts( OrgRecord org, ulong me )
	{
		var now = OrgRegistry.Now;

		return org.Payouts
			.AsEnumerable()
			.Reverse()
			.Select( x =>
			{
				var unit = x.Currency == OrgCurrency.Crypto ? "crypto" : "$";
				var yours = x.Splits.Where( y => y.MemberId == me ).Sum( y => y.Amount ) + (x.ReceiverId == me ? x.Kept : 0);
				var order = string.IsNullOrEmpty( x.OrderId ) ? x.Reason : $"{x.Reason} #{x.OrderId}";

				var lines = x.Splits
					.Select( y => new SplitLine( y.Name, $"{y.Amount:N0}", y.MemberId == me ) )
					.Append( new SplitLine( $"{x.ReceiverName} (did the work)", $"{x.Kept:N0}", x.ReceiverId == me ) )
					.ToList();

				return new PayoutRow(
					$"{Countdown( now - x.At )} ago", order, $"{x.Total:N0} {unit}", $"{yours:N0} {unit}", lines );
			} )
			.ToList();
	}
}
