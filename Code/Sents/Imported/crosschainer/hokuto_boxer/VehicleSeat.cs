using Sandbox;
using System;
using System.Linq;

namespace CuratedVehicles;

/// <summary>
/// Native seating with the player's two views: first person from the driver's eyes, and a camera that orbits the car
/// from behind. The switch is the engine's own - <see cref="PlayerController.ToggleCameraModeButton"/> ("View") flips
/// <see cref="PlayerController.ThirdPerson"/> - so it never changes the player's camera preference.
/// </summary>
public sealed class VehicleSeat : BaseChair, ICameraModifier
{
	/// <summary>
	/// How far behind the car the third-person camera sits, inches.
	/// </summary>
	private const float OrbitDistance = 240f;

	private PlayerController cameraDriver;
	private Angles orbit;
	private float cameraDistance = OrbitDistance;
	private bool thirdPerson;
	int ICameraModifier.CameraOrder => 1000;

	protected override void OnStart()
	{
		// The first-person view is the engine's seated camera, which sits at the chair's eye: the car's "Driver eyes".
		if ( !EyePosition.IsValid() )
			EyePosition = GameObject.Children.FirstOrDefault( child => child.Name == "Driver eyes" );
	}

	protected override void OnUpdate()
	{
		var driver = GetOccupant();
		if ( !driver.IsValid() || !driver.Network.IsOwner ) driver = null;

		// A new driver, or the view switched: the camera starts again from behind the car.
		if ( driver != cameraDriver || (driver.IsValid() && driver.ThirdPerson != thirdPerson) )
		{
			cameraDriver = driver;
			thirdPerson = driver.IsValid() && driver.ThirdPerson;
			orbit = new Angles( 12, WorldRotation.Yaw(), 0 );
			cameraDistance = OrbitDistance;
		}

		if ( !cameraDriver.IsValid() || !thirdPerson ) return;

		if ( !Input.Suppressed && !Input.MouseCursorVisible )
		{
			orbit.yaw += Input.AnalogLook.yaw;
			orbit.pitch = Math.Clamp( orbit.pitch + Input.AnalogLook.pitch, -10, 65 );
		}
	}

	void ICameraModifier.ModifyCamera( CameraComponent camera, ref CameraView view )
	{
		var driver = GetOccupant();
		if ( !driver.IsValid() || !driver.Network.IsOwner || Scene.Camera != camera ) return;

		// First person is the engine's seated camera, and there is nothing to add to it: PlayerController puts it at
		// the chair's eye, turns it with the car, and clamps the mouse look to the chair's yaw and pitch range.
		if ( !driver.ThirdPerson ) return;

		var vehicle = GetComponent<CuratedVehicle>();
		var target = WorldPosition + Vector3.Up * 38;
		var rotation = orbit.ToRotation();
		var targetDistance = OrbitDistance + Math.Clamp( vehicle?.Speed ?? 0, 0, 180 ) * 0.35f;
		cameraDistance = cameraDistance.LerpTo( targetDistance, Time.Delta * 3 );
		var desired = target + rotation.Backward * cameraDistance;
		var trace = Scene.Trace.FromTo( target, desired ).Radius( 8 ).IgnoreGameObjectHierarchy( GameObject ).Run();
		view.Position = trace.Hit ? trace.EndPosition + trace.Normal * 4 : desired;
		view.Rotation = Rotation.LookAt( target - view.Position, Vector3.Up );
		camera.RenderExcludeTags.Add( "firstperson" );
	}

	protected override void OnDisabled() => ReleaseDriver();
	protected override void OnDestroy() => ReleaseDriver();

	private void ReleaseDriver()
	{
		if ( !Networking.IsHost ) return;
		var driver = GetOccupant();
		if ( driver.IsValid() ) Eject( driver );
	}
}
