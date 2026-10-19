using System;

namespace OmnipresenceApps;

/// <summary>
/// Everything the Hardware Store does: sells hardware for dollars, tracks the player's packages, and, while the player
/// has broken into the staff panel, cuts prices and lets them reroute other customers' orders to their own home. The
/// break-in is the same word puzzle as the dead servers in Intrusion, played here because this computer has no other app.
/// Only works on the store's computer. Runs in the host's copy only (the app is shared), because the money is the host's.
/// </summary>
/// <remarks>
/// Who buys is always the player in the seat, worked out from the computer. The actions carry only an item or order id:
/// the price, the stock, the blueprint needed and whether the staff panel is open are all checked on the host.
/// </remarks>
public sealed class HardwareStoreViewModel
{
	public const string TabAction = "tab";
	public const string BuyAction = "buy";
	public const string RerouteAction = "reroute";
	public const string LoginAction = "login";
	public const string GuessAction = "guess";
	public const string TrickAction = "trick";

	public const string ShopTab = "shop";
	public const string OrdersTab = "orders";
	public const string StaffTab = "staff";

	readonly Func<Connection> getSeat;
	readonly Func<bool> atStoreTerminal;

	readonly Random random = new();

	StoreScreen screen;
	IntrusionPuzzle puzzle;
	List<string> rewards = [];
	string notice = "";
	Guid lastSeatId;
	string signature;
	int revision;

	public HardwareStoreViewModel( Func<Connection> getSeat, Func<bool> atStoreTerminal )
	{
		this.getSeat = getSeat;
		this.atStoreTerminal = atStoreTerminal;
		UiState = Build();
	}

	public HardwareStoreUiState UiState { get; private set; }

	/// <summary>
	/// Once a frame: start over when the seat changed, and draw again if anything drawn changed.
	/// </summary>
	public void Tick()
	{
		var seat = getSeat();

		if ( (seat?.Id ?? default) != lastSeatId )
		{
			lastSeatId = seat?.Id ?? default;
			GoShop();
		}

		if ( Signature( seat, PlayerProgress.For( seat ) ) != signature )
			UiState = Build();
	}

	public void Act( string action, string[] args )
	{
		if ( UiState.HasAccess && action == LoginAction )
			Login();
		else if ( UiState.HasAccess && args.Length > 0 )
		{
			switch ( action )
			{
				case GuessAction when int.TryParse( args[0], out var word ):
					Guess( word );
					break;

				case TrickAction when int.TryParse( args[0], out var trick ):
					UseTrick( trick );
					break;

				case TabAction:
					ChooseTab( args[0] );
					break;

				case BuyAction:
					Buy( args[0] );
					break;

				case RerouteAction:
					Reroute( args[0] );
					break;
			}
		}

		UiState = Build();
	}

	void GoShop()
	{
		screen = StoreScreen.Shop;
		puzzle = null;
		rewards = [];
		notice = "";
	}

	void ChooseTab( string tab )
	{
		notice = "";
		puzzle = null;

		screen = tab switch
		{
			OrdersTab => StoreScreen.Orders,
			StaffTab => StaffOpen ? StoreScreen.Staff : StoreScreen.Login,
			_ => StoreScreen.Shop,
		};
	}

	bool StaffOpen => getSeat() is { } seat && StoreAccess.IsUnlocked( seat.SteamId );

	/// <summary>
	/// The staff login as a dead-server-like target. Its XP is paid the first time only; the access is every time.
	/// </summary>
	static IntrusionServer StaffPanel =>
		new( "store-admin", "Hardware Store Staff Panel", "13.0.55", "Local", "Medium",
			"The shop's back office.", 6, 10, 4, 2, 50, 0, "" );

	void Login()
	{
		var seat = getSeat();

		if ( screen != StoreScreen.Login || seat is null || StaffOpen || StoreAccess.LockedOutFor( seat.SteamId ) > 0 )
			return;

		// Parts the player installed help here too.
		var hardware = HardwareCatalog.BonusOf( PlayerProgress.For( seat )?.InstalledHardware ?? [] );

		puzzle = new IntrusionPuzzle( StaffPanel, random, hardware.Attempts, hardware.Duds );
		screen = StoreScreen.Terminal;
	}

	void Guess( int word )
	{
		if ( screen != StoreScreen.Terminal || puzzle is null )
			return;

		puzzle.Guess( word );
		Finish();
	}

	void UseTrick( int trick )
	{
		if ( screen != StoreScreen.Terminal || puzzle is null )
			return;

		puzzle.UseTrick( trick );
		Finish();
	}

	/// <summary>
	/// Move on when the puzzle is over: open the staff panel, or lock the player out for a while.
	/// </summary>
	void Finish()
	{
		var seat = getSeat();

		if ( seat is null || puzzle.Status == IntrusionStatus.Running )
			return;

		if ( puzzle.Status == IntrusionStatus.Locked )
		{
			StoreAccess.Lock( seat.SteamId );
			screen = StoreScreen.Locked;
			return;
		}

		StoreAccess.Unlock( seat.SteamId );
		screen = StoreScreen.Breached;
		rewards = [$"Staff access for {StoreAccess.UnlockSeconds / 60} minutes.", "Prices are cut."];

		var server = StaffPanel;

		if ( PlayerProgress.For( seat ) is { Loaded: true } progress
		     && progress.CompleteIntrusion( server.Id, server.Xp, server.Credits, server.Blueprint ) )
			rewards = [.. rewards, $"+{server.Xp:N0} XP"];
	}

	void Buy( string id )
	{
		var seat = getSeat();
		var progress = PlayerProgress.For( seat );

		if ( progress is not { Loaded: true } || HardwareCatalog.Find( id ) is not { Restricted: false } item )
			return;

		var price = StoreAccess.PriceFor( seat.SteamId, item.Price );

		if ( item.Blueprint != "" && !progress.Blueprints.Contains( item.Blueprint ) )
			notice = "You need its blueprint.";
		else if ( progress.Money < price )
			notice = "Not enough money.";
		else if ( progress.TryOrder( item.Id, price, HardwareCatalog.OrderDeliverySeconds ) )
			notice = $"Ordered {item.Name}. It arrives in {Duration( HardwareCatalog.OrderDeliverySeconds )}.";
		else
			notice = "That order can't be placed.";
	}

	void Reroute( string orderId )
	{
		var seat = getSeat();
		var progress = PlayerProgress.For( seat );

		if ( progress is not { Loaded: true } || !StaffOpen || HardwareCatalog.FindOrder( orderId ) is not { } order )
			return;

		var item = HardwareCatalog.Find( order.ItemId );

		if ( item is null )
			return;

		if ( progress.ReroutedOrders.Contains( order.Id ) )
			notice = "You already rerouted that one.";
		else if ( progress.TryReroute( order.Id, order.ItemId, HardwareCatalog.RerouteDeliverySeconds ) )
			notice = $"Rerouted {item.Name} to your home. It arrives in {Duration( HardwareCatalog.RerouteDeliverySeconds )}.";
		else
			notice = "That order can't be rerouted.";
	}

	string Signature( Connection seat, PlayerProgress progress ) =>
		HashCode.Combine(
			seat?.Id,
			progress is { Loaded: true } ? progress.Money : -1,
			progress?.HardwareVersion,
			progress?.IntrusionVersion,
			atStoreTerminal(),
			screen,
			notice,
			HashCode.Combine(
				StoreAccess.LockedOutFor( seat?.SteamId ?? default ),
				// Countdowns redraw once a second, but only while something is on its way or the staff panel is open.
				(progress?.Shipments.Count ?? 0) > 0 || StaffOpen ? DateTimeOffset.UtcNow.ToUnixTimeSeconds() : 0,
				StaffOpen ) ).ToString();

	HardwareStoreUiState Build()
	{
		var seat = getSeat();
		var progress = PlayerProgress.For( seat );

		signature = Signature( seat, progress );

		// The staff panel closed while it was showing.
		if ( screen == StoreScreen.Staff && !StaffOpen )
			screen = StoreScreen.Shop;

		var lockout = seat is null ? 0 : StoreAccess.LockedOutFor( seat.SteamId );

		var state = new HardwareStoreUiState { Revision = ++revision, Screen = screen, Notice = notice };

		if ( seat is null )
			return state with { Message = "Sit down to use the terminal." };

		if ( !atStoreTerminal() )
			return state with { Message = "This terminal isn't connected to the store." };

		if ( progress is not { Loaded: true } )
			return state with { Message = "Loading your account..." };

		var staffOpen = StaffOpen;
		var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

		return state with
		{
			HasAccess = true,
			Balance = $"${progress.Money:N0}",
			StaffOpen = staffOpen,
			StaffRemaining = staffOpen ? Duration( StoreAccess.UnlockedFor( seat.SteamId ) ) : "",
			Shop = HardwareCatalog.Items
				.Where( x => !x.Restricted )
				.Select( x => ToShopRow( x, progress, seat ) )
				.ToList(),
			Shipments = progress.Shipments
				.Select( x => ToShipmentRow( x, now ) )
				.Where( x => x is not null )
				.ToList(),
			Lockout = lockout > 0 ? Duration( lockout ) : "",
			Attempts = puzzle?.Attempts ?? 0,
			MaxAttempts = puzzle?.MaxAttempts ?? 0,
			Dump = puzzle?.BuildLines() ?? [],
			Log = puzzle?.LogLines.ToList() ?? [],
			Rewards = rewards,
			StaffOrders = staffOpen
				? HardwareCatalog.Orders.Select( x => ToStaffRow( x, progress ) ).Where( x => x is not null ).ToList()
				: [],
		};
	}

	static ShopRow ToShopRow( HardwareItem item, PlayerProgress progress, Connection seat )
	{
		var price = StoreAccess.PriceFor( seat.SteamId, item.Price );
		var discounted = price != item.Price;
		var blueprint = item.Blueprint == "" ? null : IntrusionCatalog.FindBlueprint( item.Blueprint );

		var status = item.Blueprint != "" && !progress.Blueprints.Contains( item.Blueprint ) ? ShopStatus.NeedsBlueprint
			: progress.Money >= price ? ShopStatus.Buy
			: ShopStatus.TooExpensive;

		return new ShopRow(
			item.Id,
			item.Name,
			item.Slot.ToString(),
			item.Description,
			item.Effect,
			$"${price:N0}",
			discounted ? $"${item.Price:N0}" : "",
			status,
			blueprint is null ? "" : $"Needs the {blueprint.Name} blueprint, found in the Wired." );
	}

	static ShipmentRow ToShipmentRow( ShipmentRecord shipment, long now )
	{
		if ( HardwareCatalog.Find( shipment.ItemId ) is not { } item )
			return null;

		return new ShipmentRow( item.Name, Duration( shipment.ArriveAt - now ), shipment.Source == "reroute" );
	}

	static StaffOrderRow ToStaffRow( StoreOrder order, PlayerProgress progress )
	{
		if ( HardwareCatalog.Find( order.ItemId ) is not { } item )
			return null;

		return new StaffOrderRow( order.Id, order.Customer, item.Name, item.Effect, progress.ReroutedOrders.Contains( order.Id ) );
	}

	/// <summary>
	/// "1:05" for 65 seconds, and "0:00" for anything that has passed.
	/// </summary>
	static string Duration( long seconds )
	{
		seconds = Math.Max( seconds, 0 );
		return $"{seconds / 60}:{seconds % 60:00}";
	}
}
