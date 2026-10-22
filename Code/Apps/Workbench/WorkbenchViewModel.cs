using System;

namespace OmnipresenceApps;

/// <summary>
/// Everything the Workbench does: installs the hardware the player bought at the Hardware Store (or rerouted) into their
/// rig, and takes it out again. It sells nothing. Only works in the real world. Runs in the host's copy only (the app is
/// shared), because the inventory is the host's.
/// </summary>
/// <remarks>
/// Whose rig it is, is always the player in the seat, worked out from the computer. The actions carry only a piece's id,
/// and the host checks the player has it and the rig has room.
/// </remarks>
public sealed class WorkbenchViewModel
{
	public const string InstallAction = "install";
	public const string RemoveAction = "remove";

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
		if ( UiState.HasAccess && args.Length > 0 )
		{
			switch ( action )
			{
				case InstallAction:
					Install( args[0] );
					break;

				case RemoveAction:
					Remove( args[0] );
					break;
			}
		}

		UiState = Build();
	}

	void Install( string id )
	{
		var progress = PlayerProgress.For( getSeat() );

		if ( progress is not { Loaded: true } || HardwareCatalog.Find( id ) is not { } item )
			return;

		if ( !progress.Inventory.Contains( id ) )
			notice = "You don't have that.";
		else if ( !HardwareCatalog.CanInstall( progress.InstalledHardware, id ) )
			notice = $"No free {item.Slot} slot. Remove something first.";
		else if ( progress.TryInstall( id, installed => HardwareCatalog.CanInstall( installed, id ) ) )
			notice = $"Installed {item.Name}.";
		else
			notice = "That can't be installed.";
	}

	void Remove( string id )
	{
		var progress = PlayerProgress.For( getSeat() );

		if ( progress is not { Loaded: true } || HardwareCatalog.Find( id ) is not { } item )
			return;

		notice = progress.TryUninstall( id ) ? $"Removed {item.Name}." : "That isn't installed.";
	}

	string Signature( Connection seat, PlayerProgress progress ) =>
		HashCode.Combine(
			seat?.Id,
			progress?.Version,
			WiredTravel.PlaceOf( seat ),
			notice ).ToString();

	WorkbenchUiState Build()
	{
		var seat = getSeat();
		var progress = PlayerProgress.For( seat );

		signature = Signature( seat, progress );

		var state = new WorkbenchUiState { Revision = ++revision, Notice = notice };

		if ( seat is null )
			return state with { Message = "Sit down to use the workbench." };

		if ( WiredTravel.PlaceOf( seat ) == WiredPlace.Wired )
			return state with { Message = "Your tools are at home. Disconnect first." };

		if ( progress is not { Loaded: true } )
			return state with { Message = "Loading your account..." };

		var installed = progress.InstalledHardware;
		var bonus = HardwareCatalog.BonusOf( installed );
		var incoming = progress.Shipments.Count;

		return state with
		{
			HasAccess = true,
			Rig = BuildRig( installed ),
			Summary = Summarize( bonus ),
			Inventory = progress.Inventory
				.GroupBy( x => x )
				.Select( x => ToInventoryRow( x.Key, x.Count(), installed ) )
				.Where( x => x is not null )
				.ToList(),
			Incoming = incoming == 0 ? "" : incoming == 1 ? "1 package on its way." : $"{incoming} packages on their way.",
		};
	}

	/// <summary>
	/// One row for each slot the rig has, in a fixed order, with the installed piece in it or nothing.
	/// </summary>
	static List<RigSlotRow> BuildRig( IReadOnlyList<string> installed )
	{
		var rows = new List<RigSlotRow>();

		foreach ( var slot in Enum.GetValues<HardwareSlot>() )
		{
			var pieces = installed.Select( HardwareCatalog.Find ).Where( x => x?.Slot == slot ).ToList();

			for ( var i = 0; i < HardwareCatalog.SlotCount( slot ); i++ )
			{
				rows.Add( i < pieces.Count
					? new RigSlotRow( slot.ToString(), pieces[i].Id, pieces[i].Name, pieces[i].Effect )
					: new RigSlotRow( slot.ToString(), "", "", "" ) );
			}
		}

		return rows;
	}

	static string Summarize( HardwareBonus bonus )
	{
		if ( bonus.Attempts == 0 && bonus.Duds == 0 )
			return "Nothing installed. Terminals play as they come.";

		var parts = new List<string>();

		if ( bonus.Attempts > 0 )
			parts.Add( bonus.Attempts == 1 ? "+1 attempt" : $"+{bonus.Attempts} attempts" );

		if ( bonus.Duds > 0 )
			parts.Add( bonus.Duds == 1 ? "1 wrong word removed" : $"{bonus.Duds} wrong words removed" );

		return $"On every terminal: {string.Join( ", ", parts )}.";
	}

	static InventoryRow ToInventoryRow( string id, int count, IReadOnlyList<string> installed )
	{
		if ( HardwareCatalog.Find( id ) is not { } item )
			return null;

		return new InventoryRow( id, item.Name, item.Slot.ToString(), item.Effect, count, HardwareCatalog.CanInstall( installed, id ) );
	}
}
