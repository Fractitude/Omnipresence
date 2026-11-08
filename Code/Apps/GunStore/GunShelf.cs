namespace OmnipresenceApps;

/// <summary>
/// What a gun store computer is showing: the tab, the item picked on each tab, and where the picture is turned. One per
/// computer, kept here and not in the app's window, so it is the same for whoever sits down (or for nobody), and survives
/// the window being closed and opened again. It is the host's: the app runs there and everyone else draws what it publishes.
/// </summary>
public sealed class GunShelf
{
	// Not readonly: filled at run time, and a hot reload would reset it (AGENTS.md, "Static state and hot reload").
	static Dictionary<Guid, GunShelf> shelves = new();

	/// <summary>The shelf of this computer. A computer that can't be found shares one.</summary>
	public static GunShelf For( NaviComputer computer )
	{
		var key = computer.IsValid() ? computer.GameObject.Id : Guid.Empty;

		if ( !shelves.TryGetValue( key, out var shelf ) )
			shelves[key] = shelf = new GunShelf();

		return shelf;
	}

	/// <summary>The tab on show.</summary>
	public GunItemKind Tab = GunItemKind.Weapon;

	/// <summary>The item picked on the weapons tab, and on the ammunition tab.</summary>
	public string WeaponId = GunCatalog.Of( GunItemKind.Weapon ).FirstOrDefault()?.Id ?? "";

	public string AmmoId = GunCatalog.Of( GunItemKind.Ammo ).FirstOrDefault()?.Id ?? "";

	/// <summary>The id of the item on show.</summary>
	public string SelectedId
	{
		get => Tab == GunItemKind.Weapon ? WeaponId : AmmoId;
		set
		{
			if ( Tab == GunItemKind.Weapon )
				WeaponId = value;
			else
				AmmoId = value;
		}
	}

	/// <summary>Degrees the item has turned.</summary>
	public float Yaw = 210f;

	/// <summary>How high the camera looks from, in degrees above the ground.</summary>
	public float Elevation = 18f;

	/// <summary>Distance as a factor of the fitting distance: below 1 is closer.</summary>
	public float Zoom = GunPreviewScene.MinZoom;

	/// <summary>When someone last turned or zoomed it, on the host's clock. The item turns by itself after a while.</summary>
	public RealTimeSince SinceTouched = 1000;
}
