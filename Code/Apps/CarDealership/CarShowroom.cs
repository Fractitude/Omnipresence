namespace OmnipresenceApps;

/// <summary>
/// What a dealership computer is showing: the car picked, the colours picked for each car, and where the picture is turned.
/// One per computer, kept here and not in the app's window, so it is the same for whoever sits down, and survives the window
/// being closed and opened again. It is the host's: the app runs there and everyone else draws what it publishes.
/// </summary>
public sealed class CarShowroom
{
	/// <summary>The colours someone picked for one car. Null is the car's own.</summary>
	public sealed class Picks
	{
		public Color? Paint;
		public Color? Secondary;
		public Color? Glass;
	}

	// Not readonly: filled at run time, and a hot reload would reset it (AGENTS.md, "Static state and hot reload").
	static Dictionary<Guid, CarShowroom> showrooms = new();

	/// <summary>The showroom of this computer. A computer that can't be found shares one.</summary>
	public static CarShowroom For( NaviComputer computer )
	{
		var key = computer.IsValid() ? computer.GameObject.Id : Guid.Empty;

		if ( !showrooms.TryGetValue( key, out var showroom ) )
			showrooms[key] = showroom = new CarShowroom();

		return showroom;
	}

	/// <summary>The car on show.</summary>
	public string SelectedId = CarCatalog.All.FirstOrDefault()?.Id ?? "";

	// Each car keeps its own colours, so going to another car and back finds them as they were left.
	readonly Dictionary<string, Picks> picks = new();

	/// <summary>The picks for a car, made when first asked for.</summary>
	public Picks PicksOf( string id )
	{
		if ( !picks.TryGetValue( id, out var found ) )
			picks[id] = found = new Picks();

		return found;
	}

	/// <summary>Degrees the car has turned.</summary>
	public float Yaw = 210f;

	/// <summary>How high the camera looks from, in degrees above the ground.</summary>
	public float Elevation = 18f;

	/// <summary>Distance as a factor of the fitting distance: below 1 is closer.</summary>
	public float Zoom = 1f;

	/// <summary>When someone last turned or zoomed it, on the host's clock. The car turns by itself after a while.</summary>
	public RealTimeSince SinceTouched = 1000;
}

/// <summary>Where the 3D picture is turned, shared so every screen shows the car the same way.</summary>
public sealed record CarView( float Yaw, float Elevation, float Zoom, bool Spinning )
{
	public static CarView Default { get; } = new( 210f, 18f, 1f, false );
}
