using System.Text.Json.Serialization;

/// <summary>
/// A configurable input action for things driven from a seat. This holds no state: it reads the driver's
/// connection while <see cref="IPlayerControllable.OnControl"/> is running.
/// </summary>
public struct ClientInput
{
	[Property]
	public string Action { get; set; }

	[JsonIgnore, Hide]
	public readonly bool IsEnabled => !string.IsNullOrWhiteSpace( Action );

	public readonly bool Down() => IsEnabled && ControlContext.Connection?.Down( Action ) == true;
	public readonly bool Pressed() => IsEnabled && ControlContext.Connection?.Pressed( Action ) == true;
	public readonly bool Released() => IsEnabled && ControlContext.Connection?.Released( Action ) == true;
}
