using Sandbox;
using System;

namespace CuratedVehicles;

/// <summary>Native seating with a third-person car view; never changes the player's camera preferences.</summary>
public sealed class VehicleSeat : BaseChair, ICameraModifier
{
	private PlayerController cameraDriver;
	private Angles orbit;
	private float cameraDistance;
	int ICameraModifier.CameraOrder => 1000;

	protected override void OnUpdate()
	{
		var driver = GetOccupant();
		if ( !driver.IsValid() || !driver.Network.IsOwner ) driver = null;
		if ( driver != cameraDriver )
		{
			cameraDriver = driver;
			orbit = new Angles( 12, WorldRotation.Yaw(), 0 );
			cameraDistance = 240;
		}
		if ( !cameraDriver.IsValid() ) return;
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
		var vehicle = GetComponent<CuratedVehicle>();
		var target = WorldPosition + Vector3.Up * 38;
		var rotation = orbit.ToRotation();
		var targetDistance = 240 + Math.Clamp( vehicle?.Speed ?? 0, 0, 180 ) * 0.35f;
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
