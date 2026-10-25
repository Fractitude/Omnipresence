/// <summary>
/// A vehicle's headlights (the low beams) and its light bar (the high beams), which comes on as well as them.
///
/// The driver taps the flashlight key to switch the headlights on and off, and holds it to switch between low and
/// high beam.
/// </summary>
[Title( "Vehicle Lights" ), Category( "Vehicles" ), Icon( "highlight" )]
public sealed class VehicleLights : Component
{
	public enum HeadlightMode
	{
		Off,
		Low,
		High,
	}

	[Property] public FourWheelVehicle Vehicle { get; set; }

	[Property, Group( "Input" )] public string SwitchAction { get; set; } = "VehicleLights";

	/// <summary>
	/// How long the key has to be held to switch beams, seconds. A shorter press switches the lights on or off.
	/// </summary>
	[Property, Group( "Input" )] public float HoldTime { get; set; } = 0.35f;

	[Property] public Color LightColor { get; set; } = new( 1f, 0.94f, 0.84f );

	/// <summary>
	/// How long the bulbs take to come up to full brightness or go out, seconds.
	/// </summary>
	[Property] public float WarmUpTime { get; set; } = 0.12f;

	[Property, Group( "Low Beam" )] public List<SpotLight> LowBeams { get; set; } = [];
	[Property, Group( "Low Beam" )] public Texture LowBeamCookie { get; set; }
	[Property, Group( "Low Beam" )] public float LowBeamBrightness { get; set; } = 14f;
	[Property, Group( "Low Beam" )] public float LowBeamRange { get; set; } = 1800f;
	[Property, Group( "Low Beam" )] public float LowBeamCone { get; set; } = 62f;

	/// <summary>
	/// The light bar. It comes on as well as the low beams.
	/// </summary>
	[Property, Group( "High Beam" )] public List<SpotLight> HighBeams { get; set; } = [];
	[Property, Group( "High Beam" )] public Texture HighBeamCookie { get; set; }
	[Property, Group( "High Beam" )] public float HighBeamBrightness { get; set; } = 28f;
	[Property, Group( "High Beam" )] public float HighBeamRange { get; set; } = 4000f;
	[Property, Group( "High Beam" )] public float HighBeamCone { get; set; } = 44f;

	[Property, Group( "Sound" )] public SoundEvent SwitchOnSound { get; set; }
	[Property, Group( "Sound" )] public SoundEvent SwitchOffSound { get; set; }

	[Sync( SyncFlags.FromHost )] public HeadlightMode Mode { get; set; }

	private bool _wasDown;
	private bool _holdUsed;
	private TimeSince _pressed;
	private float _lowLevel;
	private float _highLevel;

	/// <summary>
	/// Whether the player in the driver's seat is this machine's player.
	/// </summary>
	private bool IsLocalDriver
	{
		get
		{
			var seat = Vehicle?.Seat;
			if ( !seat.IsValid() || !seat.IsOccupied ) return false;

			var occupant = seat.GetOccupant();
			return occupant.IsValid() && !occupant.IsProxy;
		}
	}

	/// <summary>
	/// Tap to switch the lights, hold to switch beams. Read on the driver's own machine, which asks the host.
	/// </summary>
	private void ReadSwitch()
	{
		if ( !IsLocalDriver )
		{
			_wasDown = false;
			return;
		}

		var down = Input.Down( SwitchAction );

		if ( down && !_wasDown )
		{
			_pressed = 0;
			_holdUsed = false;
		}

		if ( down && !_holdUsed && _pressed >= HoldTime )
		{
			_holdUsed = true;
			RequestSwitch( true );
		}

		if ( !down && _wasDown && !_holdUsed )
			RequestSwitch( false );

		_wasDown = down;
	}

	/// <summary>
	/// The driver asks to switch the lights on or off, or the beam between low and high. Runs on the host.
	/// </summary>
	[Rpc.Host]
	private void RequestSwitch( bool beam )
	{
		var seat = Vehicle?.Seat;
		if ( !seat.IsValid() || !seat.IsOccupied ) return;
		if ( seat.GetOccupant()?.Network.Owner != Rpc.Caller ) return;

		if ( beam )
			SetMode( Mode == HeadlightMode.High ? HeadlightMode.Low : HeadlightMode.High );
		else
			SetMode( Mode == HeadlightMode.Off ? HeadlightMode.Low : HeadlightMode.Off );
	}

	private void SetMode( HeadlightMode mode )
	{
		if ( mode == Mode ) return;

		Mode = mode;
		PlaySwitchSound( mode != HeadlightMode.Off );
	}

	[Rpc.Broadcast]
	private void PlaySwitchSound( bool on )
	{
		var sound = on ? SwitchOnSound : SwitchOffSound;
		if ( sound.IsValid() )
			Sound.Play( sound, WorldPosition );
	}

	protected override void OnUpdate()
	{
		ReadSwitch();

		var rate = Time.Delta / MathF.Max( WarmUpTime, 0.001f );
		_lowLevel = _lowLevel.Approach( Mode != HeadlightMode.Off ? 1 : 0, rate );
		_highLevel = _highLevel.Approach( Mode == HeadlightMode.High ? 1 : 0, rate );

		UpdateLights( LowBeams, _lowLevel, LowBeamCookie, LowBeamBrightness, LowBeamRange, LowBeamCone );
		UpdateLights( HighBeams, _highLevel, HighBeamCookie, HighBeamBrightness, HighBeamRange, HighBeamCone );
	}

	private void UpdateLights( List<SpotLight> lights, float level, Texture cookie, float brightness, float range, float cone )
	{
		foreach ( var light in lights )
		{
			if ( !light.IsValid() ) continue;

			light.Enabled = level > 0;
			light.Cookie = cookie;
			light.Radius = range;
			light.ConeOuter = cone;
			light.ConeInner = cone * 0.8f;
			light.LightColor = (LightColor * (brightness * level)).WithAlpha( 1 );
		}
	}
}
