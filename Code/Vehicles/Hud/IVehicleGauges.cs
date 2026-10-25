/// <summary>
/// What the speedometer (<see cref="VehicleHud"/>) reads from a vehicle. Any vehicle component can implement it and
/// call <see cref="VehicleHud.Update"/> from its <c>OnUpdate</c>.
/// </summary>
public interface IVehicleGauges
{
	/// <summary>
	/// The player in the driver's seat is this machine's player. The speedometer shows only then.
	/// </summary>
	bool GaugesVisible { get; }

	/// <summary>
	/// Speed in km/h, either way.
	/// </summary>
	float GaugeSpeed { get; }

	float GaugeRpm { get; }

	float GaugeMaxRpm { get; }

	/// <summary>
	/// "R", "N" or the gear number.
	/// </summary>
	string GaugeGear { get; }

	/// <summary>
	/// The text under the dial, such as the driving mode. Null leaves it out.
	/// </summary>
	string GaugeMode { get; }

	/// <summary>
	/// The shortcuts in the row under the dial.
	/// </summary>
	IEnumerable<VehiclePrompt> GaugePrompts { get; }
}

/// <summary>
/// One shortcut in the speedometer's row: the key's label and what it does. A shortcut that can't be used right now
/// (a boost that is recharging) is <see cref="Enabled"/> false and drawn faded.
/// </summary>
public readonly record struct VehiclePrompt( string Key, string Label, bool Enabled = true );
