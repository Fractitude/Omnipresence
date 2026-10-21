/// <summary>
/// The driver's hands on the steering wheel. The seat pose leaves them in the lap, so each hand is pinned to the rim
/// with the animgraph's IK and turns with the wheel.
/// </summary>
public sealed partial class FourWheelVehicle
{
	private const string LeftHand = "hand_left";
	private const string RightHand = "hand_right";

	/// <summary>
	/// Where on the rim the hands go, degrees up from 9 and 3 o'clock.
	/// </summary>
	[Property, Group( "Steering Wheel" )] public float HandAngle { get; set; } = 25f;

	/// <summary>
	/// How far from the wheel's centre the hands hold it, in inches.
	/// </summary>
	[Property, Group( "Steering Wheel" )] public float HandRadius { get; set; } = 6.5f;

	/// <summary>
	/// Turns the hands on the rim, degrees.
	/// </summary>
	[Property, Group( "Steering Wheel" )] public Angles HandRotation { get; set; } = new( -90f, 0f, 0f );

	private SkinnedModelRenderer _handsOn;

	/// <summary>
	/// Runs on every machine, so everyone sees the driver's hands on the wheel.
	/// </summary>
	private void UpdateDriverHands()
	{
		var renderer = GetDriverRenderer();

		if ( renderer != _handsOn )
		{
			ReleaseDriverHands();
			_handsOn = renderer;
		}

		if ( !renderer.IsValid() ) return;

		var wheel = GetComponent<SkinnedModelRenderer>()?.GetBoneObject( SteeringWheelBone );
		if ( !wheel.IsValid() ) return;

		// The wheel's plane is square to its column. Grips are measured round it from where the car's up points.
		var axis = (WorldRotation * SteeringWheelAxis).Normal;
		var up = (Vector3.Up - axis * Vector3.Up.Dot( axis )).Normal;
		var side = Vector3.Cross( axis, up ).Normal;

		var turn = Rotation.FromAxis( axis, -_steerPose * SteeringWheelAngle );
		var lift = MathX.DegreeToRadian( HandAngle );
		var centre = wheel.WorldPosition;

		SetHand( renderer, LeftHand, centre, axis, turn * (-side * MathF.Cos( lift ) + up * MathF.Sin( lift )), -1 );
		SetHand( renderer, RightHand, centre, axis, turn * (side * MathF.Cos( lift ) + up * MathF.Sin( lift )), 1 );
	}

	private void SetHand( SkinnedModelRenderer renderer, string name, Vector3 centre, Vector3 axis, Vector3 direction, float sign )
	{
		var position = centre + direction * HandRadius;

		// The fingers wrap round the rim: along the way the rim runs, palm to the wheel's centre.
		var tangent = Vector3.Cross( axis, direction ).Normal * sign;
		renderer.SetIk( name, new Transform( position, Rotation.LookAt( tangent, -direction ) * HandRotation.ToRotation() ) );
	}

	private void ReleaseDriverHands()
	{
		if ( !_handsOn.IsValid() ) return;

		_handsOn.ClearIk( LeftHand );
		_handsOn.ClearIk( RightHand );
		_handsOn = null;
	}

	private SkinnedModelRenderer GetDriverRenderer()
	{
		if ( !Seat.IsValid() || !Seat.IsOccupied ) return null;

		return Seat.GetOccupant()?.GetComponent<PlayerController>()?.Renderer;
	}
}
