/// <summary>
/// Something a player drives from a seat, like <see cref="FourWheelVehicle"/>. <see cref="ControlSystem"/> calls
/// <see cref="OnControl"/> on the host every fixed update while someone sits in a <see cref="BaseChair"/> on the same
/// object.
/// </summary>
public interface IPlayerControllable
{
	bool CanControl( PlayerController player ) => true;
	void OnControl();
}
