using System;

namespace OmnipresenceApps;

/// <summary>
/// Everything the Workbench does: lists the blueprints the player found in the Wired and builds them for dollars. Only
/// works in the real world. Runs in the host's copy only (the app is shared), because the money is the host's.
/// </summary>
/// <remarks>
/// Who builds is always the player in the seat, worked out from the computer. The action carries only the blueprint id,
/// and the host checks the player has it, hasn't built it and can pay the cost from the catalog.
/// </remarks>
public sealed class WorkbenchViewModel
{
	public const string BuildAction = "build";

	readonly Func<Connection> getSeat;

	Guid lastSeatId;
	string notice = "";
	string signature;
	int revision;

	public WorkbenchViewModel( Func<Connection> getSeat )
	{
		this.getSeat = getSeat;
		UiState = Build();
	}

	public WorkbenchUiState UiState { get; private set; }

	/// <summary>
	/// Once a frame: clear the notice when the seat changed, and draw again if anything drawn changed.
	/// </summary>
	public void Tick()
	{
		var seat = getSeat();

		if ( (seat?.Id ?? default) != lastSeatId )
		{
			lastSeatId = seat?.Id ?? default;
			notice = "";
		}

		if ( Signature( seat, PlayerProgress.For( seat ) ) != signature )
			UiState = Build();
	}

	public void Act( string action, string[] args )
	{
		if ( action == BuildAction && args.Length > 0 && UiState.HasAccess )
			BuildItem( args[0] );

		UiState = Build();
	}

	void BuildItem( string id )
	{
		var progress = PlayerProgress.For( getSeat() );

		if ( IntrusionCatalog.FindBlueprint( id ) is not { } blueprint || progress is not { Loaded: true } )
			return;

		if ( progress.Built.Contains( id ) )
			notice = "Already built.";
		else if ( !progress.Blueprints.Contains( id ) )
			notice = "You don't have that blueprint.";
		else if ( progress.Money < blueprint.Cost )
			notice = "Not enough money.";
		else if ( progress.TryBuild( id, blueprint.Cost ) )
			notice = $"Built: {blueprint.Name}.";
		else
			notice = "That can't be built.";
	}

	string Signature( Connection seat, PlayerProgress progress ) =>
		HashCode.Combine(
			seat?.Id,
			progress is { Loaded: true } ? progress.Money : -1,
			progress?.IntrusionVersion,
			progress?.BuildVersion,
			WiredTravel.Current,
			notice ).ToString();

	WorkbenchUiState Build()
	{
		var seat = getSeat();
		var progress = PlayerProgress.For( seat );

		signature = Signature( seat, progress );

		var state = new WorkbenchUiState { Revision = ++revision, Notice = notice };

		if ( seat is null )
			return state with { Message = "Sit down to use the workbench." };

		if ( WiredTravel.Current == WiredPlace.Wired )
			return state with { Message = "Your tools are at home. Disconnect first." };

		if ( progress is not { Loaded: true } )
			return state with { Message = "Loading your account..." };

		return state with
		{
			HasAccess = true,
			Balance = $"${progress.Money:N0}",
			Rows = progress.Blueprints.Select( x => ToRow( x, progress ) ).Where( x => x is not null ).ToList(),
		};
	}

	static BenchRow ToRow( string id, PlayerProgress progress )
	{
		if ( IntrusionCatalog.FindBlueprint( id ) is not { } blueprint )
			return null;

		var status = progress.Built.Contains( id ) ? BenchStatus.Built
			: progress.Money >= blueprint.Cost ? BenchStatus.Buildable
			: BenchStatus.TooExpensive;

		return new BenchRow( id, blueprint.Name, blueprint.Description, blueprint.Effect, $"${blueprint.Cost:N0}", status );
	}
}
