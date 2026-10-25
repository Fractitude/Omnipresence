/// <summary>
/// The driver's speedometer (<see cref="VehicleHud"/>), shown on the machine of the player in the seat.
/// </summary>
public sealed partial class FourWheelVehicle : IVehicleGauges
{
	private GameObject _gaugesHud;

	/// <summary>
	/// The player in the driver's seat is this machine's player.
	/// </summary>
	public bool LocalDriver
	{
		get
		{
			if ( !Seat.IsValid() || !Seat.IsOccupied ) return false;

			var occupant = Seat.GetOccupant();
			return occupant.IsValid() && !occupant.IsProxy;
		}
	}

	bool IVehicleGauges.GaugesVisible => LocalDriver;

	// in/s to km/h
	float IVehicleGauges.GaugeSpeed => MathF.Abs( Speed ) * MetersPerInch * 3.6f;

	float IVehicleGauges.GaugeRpm => SoundRpm;

	float IVehicleGauges.GaugeMaxRpm => Engine.MaxRpm;

	// Gear counts from 0 for first. Neutral is standing still with no throttle.
	string IVehicleGauges.GaugeGear => Reversing ? "R"
		: !ThrottleDown && MathF.Abs( Speed ) < 2f ? "N"
		: (Gear + 1).ToString();

	string IVehicleGauges.GaugeMode => null;

	IEnumerable<(string Key, string Label)> IVehicleGauges.GaugePrompts =>
	[
		(Input.GetButtonOrigin( "Run" ), "TURBO"),
		(Input.GetButtonOrigin( "Flashlight" ), "LIGHTS"),
	];

	private void UpdateGauges() => VehicleHud.Update( this, ref _gaugesHud );

	protected override void OnDestroy()
	{
		if ( _gaugesHud.IsValid() )
			_gaugesHud.Destroy();
	}
}
