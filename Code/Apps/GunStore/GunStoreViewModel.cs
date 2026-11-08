using System;
using System.Globalization;

namespace OmnipresenceApps;

/// <summary>
/// Everything the Gun Store does: lists the weapons and the ammunition, shows the one picked, and sells it. A bought
/// weapon or pack goes straight into the buyer's inventory (<see cref="GunSeller"/>). Only works on a computer tagged
/// <see cref="GunSeller.ComputerTag"/>. Runs in the host's copy only (the app is shared), because the money is the host's.
/// </summary>
/// <remarks>
/// Who buys is always the player in the seat, worked out from the computer. The actions carry only an item id: the price,
/// the cash and the room are all checked on the host. What is on show (the tab, the item, how the picture is turned) is the
/// computer's, in its <see cref="GunShelf"/>, so it is the same for everyone and for whoever sits down next; only the
/// wallet and what the buyer carries are the seated player's. With nobody seated the screen still shows all of it.
/// </remarks>
public sealed class GunStoreViewModel
{
	/// <summary>Pick an item on the tab on show: one argument, the id.</summary>
	public const string SelectAction = "select";

	/// <summary>Buy the item: one argument, the id on the tab on show.</summary>
	public const string BuyAction = "buy";

	/// <summary>Change tab: one argument, <c>weapons</c> or <c>ammo</c>.</summary>
	public const string TabAction = "tab";

	/// <summary>Turn the picture: three arguments, the yaw, the elevation and the zoom, as numbers.</summary>
	public const string ViewAction = "view";

	public const string WeaponsTab = "weapons";
	public const string AmmoTab = "ammo";

	/// <summary>Seconds without a touch before the item turns by itself, and how fast (degrees a second).</summary>
	public const float IdleBeforeSpin = 3f;

	public const float SpinSpeed = 18f;

	/// <summary>How often the picture's turning is sent while it turns by itself, and while someone turns it, in seconds.</summary>
	const float SpinPublishInterval = 0.25f;

	const float TouchPublishInterval = 0.06f;

	readonly Func<Connection> getSeat;
	readonly Func<NaviComputer> getComputer;

	string notice = "";
	bool noticeIsError;
	Guid lastSeatId;
	string signature;
	int revision;

	RealTimeSince sincePublished = 1000;
	bool viewDirty;
	bool publishedSpinning;

	public GunStoreViewModel( Func<Connection> getSeat, Func<NaviComputer> getComputer )
	{
		this.getSeat = getSeat;
		this.getComputer = getComputer;
		UiState = Build();
	}

	public GunStoreUiState UiState { get; private set; }

	GunShelf Shelf => GunShelf.For( getComputer() );

	/// <summary>How the picture is turned now, every frame. A viewer's copy gets <see cref="GunStoreUiState.View"/>.</summary>
	public CarView LiveView
	{
		get
		{
			var shelf = Shelf;
			return new CarView( shelf.Yaw, shelf.Elevation, shelf.Zoom, shelf.SinceTouched > IdleBeforeSpin );
		}
	}

	/// <summary>
	/// Once a frame: start over when the seat changed, turn the item if nobody has touched it, send how it is turned, and
	/// draw again if anything drawn changed.
	/// </summary>
	public void Tick()
	{
		var seat = getSeat();

		if ( (seat?.Id ?? default) != lastSeatId )
		{
			lastSeatId = seat?.Id ?? default;
			notice = "";
			noticeIsError = false;
		}

		TickView();

		if ( Signature( seat ) != signature )
			UiState = Build();
	}

	/// <summary>The item turns by itself on the host, whoever is watching, once nobody has touched it for a while.</summary>
	void TickView()
	{
		var shelf = Shelf;
		var spinning = shelf.SinceTouched > IdleBeforeSpin;

		if ( spinning )
			shelf.Yaw = (shelf.Yaw + SpinSpeed * Time.Delta) % 360f;

		var due = spinning != publishedSpinning
			? true
			: sincePublished > (spinning ? SpinPublishInterval : TouchPublishInterval) && (spinning || viewDirty);

		if ( !due )
			return;

		sincePublished = 0;
		viewDirty = false;
		publishedSpinning = spinning;

		// Not a new Revision: the panel is not drawn again for this, only the picture is turned.
		UiState = UiState with { View = LiveView };
	}

	public void Act( string action, string[] args )
	{
		if ( UiState.HasAccess && args.Length > 0 )
		{
			var shelf = Shelf;

			switch ( action )
			{
				case SelectAction when GunCatalog.Find( shelf.Tab, args[0] ) is { } item:
					shelf.SelectedId = item.Id;
					notice = "";
					noticeIsError = false;
					break;

				case TabAction when args[0] is WeaponsTab or AmmoTab:
					shelf.Tab = args[0] == AmmoTab ? GunItemKind.Ammo : GunItemKind.Weapon;
					notice = "";
					noticeIsError = false;
					break;

				case ViewAction when args.Length > 2:
					TurnTo( args[0], args[1], args[2] );
					break;

				case BuyAction:
					Buy( args[0] );
					break;
			}
		}

		UiState = Build();
	}

	/// <summary>The picture was turned or zoomed on a screen. Clamped here, as it is a number from a client.</summary>
	void TurnTo( string yaw, string elevation, string zoom )
	{
		if ( !Number( yaw, out var y ) || !Number( elevation, out var e ) || !Number( zoom, out var z ) )
			return;

		var shelf = Shelf;

		shelf.Yaw = ((y % 360f) + 360f) % 360f;
		shelf.Elevation = Math.Clamp( e, CarPreviewScene.MinElevation, CarPreviewScene.MaxElevation );
		shelf.Zoom = Math.Clamp( z, CarPreviewScene.MinZoom, CarPreviewScene.MaxZoom );
		shelf.SinceTouched = 0;
		viewDirty = true;
	}

	static bool Number( string text, out float value ) =>
		float.TryParse( text, NumberStyles.Float, CultureInfo.InvariantCulture, out value ) && float.IsFinite( value );

	void Buy( string id )
	{
		var seat = getSeat();

		if ( seat is null || !GunSeller.IsGunStore( getComputer() ) || GunCatalog.Find( Shelf.Tab, id ) is not { } item )
			return;

		var result = GunSeller.Buy( seat, item );

		notice = result.Message;
		noticeIsError = !result.Ok;
	}

	/// <summary>The seated player's weapons, or null with nobody seated.</summary>
	WeaponInventory InventoryOf( Connection seat ) => seat is null ? null : Players.For<WeaponInventory>( seat );

	/// <summary>What the seated player has of an item: carried, or how full the reserve is. Empty with nobody seated.</summary>
	string StatusOf( GunItem item, WeaponInventory inventory )
	{
		if ( !inventory.IsValid() )
			return "";

		if ( item.Kind == GunItemKind.Weapon )
			return GunSeller.Carries( inventory, item.Id ) ? "Carried" : "";

		var type = GunSeller.AmmoOf( item );
		return type is null ? "" : $"{inventory.GetAmmo( type )} / {type.MaxReserve} in reserve";
	}

	/// <summary>Can the seated player buy it now? Weapons not already carried, ammo with room for some.</summary>
	static bool CanBuy( GunItem item, WeaponInventory inventory ) =>
		inventory.IsValid()
		&& (item.Kind == GunItemKind.Weapon ? !GunSeller.Carries( inventory, item.Id ) : GunSeller.RoomFor( inventory, item ) > 0);

	string Signature( Connection seat )
	{
		var shelf = Shelf;
		var inventory = InventoryOf( seat );
		var items = GunCatalog.Of( shelf.Tab );

		// The seated player's own state of each item shown: what they carry changes without a version to watch.
		var held = string.Join( '|', items.Select( x => StatusOf( x, inventory ) ) );

		return HashCode.Combine(
			seat?.Id,
			PlayerWallet.For( seat )?.Version,
			PlayerProgress.For( seat )?.Version,
			GunSeller.IsGunStore( getComputer() ),
			(shelf.Tab, shelf.SelectedId),
			held,
			notice ).ToString();
	}

	GunStoreUiState Build()
	{
		var seat = getSeat();
		var progress = PlayerProgress.For( seat );
		var shelf = Shelf;

		signature = Signature( seat );

		var state = new GunStoreUiState
		{
			Revision = ++revision, Tab = shelf.Tab, SelectedId = shelf.SelectedId, Notice = notice,
			NoticeIsError = noticeIsError, View = LiveView,
		};

		if ( !GunSeller.IsGunStore( getComputer() ) )
			return state with { Message = "This terminal isn't connected to the gun store." };

		// The screen is the same with nobody seated; only the wallet and what the buyer carries are the seated player's.
		var seated = seat is not null;
		var loaded = progress is { Loaded: true };
		var cash = seated && loaded ? PlayerWallet.For( seat )?.Cash ?? 0 : 0;
		var inventory = seated && loaded ? InventoryOf( seat ) : null;

		return state with
		{
			HasAccess = true,
			Seated = seated && loaded,
			Balance = !seated ? "" : loaded ? $"${cash:N0}" : "Loading your account...",
			Items = GunCatalog.Of( shelf.Tab )
				.Select( x => new GunRow( x.Id, x.Name, x.Description, x.Stats, $"${x.Price:N0}",
					seated && loaded && cash >= x.Price, CanBuy( x, inventory ), StatusOf( x, inventory ) ) )
				.ToList(),
		};
	}
}
