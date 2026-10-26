namespace Sandbox;

/// <summary>
/// A second pair of acceleration and deceleration times for running. The engine's <see cref="PlayerController"/>
/// has only one pair, so this component keeps the controller's own values as the walking ones and swaps in the run
/// values while the player runs. Only the owner simulates movement, so only the owner does anything.
/// </summary>
public sealed class RunAcceleration : Component
{
	/// <summary>Seconds to reach the requested speed from a lower one while running.</summary>
	[Property, Range( 0f, 2f )] public float RunAccelerationTime { get; set; } = 0.2f;

	/// <summary>Seconds to reach the requested speed from a higher one while running or coming down from a run.</summary>
	[Property, Range( 0f, 2f )] public float RunDecelerationTime { get; set; } = 0.2f;

	PlayerController controller;
	float walkAcceleration;
	float walkDeceleration;
	bool swapped;

	protected override void OnEnabled()
	{
		controller = GetComponent<PlayerController>();
	}

	protected override void OnDisabled()
	{
		Restore();
		controller = null;
	}

	protected override void OnUpdate() => Apply();

	protected override void OnFixedUpdate() => Apply();

	void Apply()
	{
		if ( IsProxy || !controller.IsValid() )
			return;

		var runHeld = Input.Down( controller.AltMoveButton ) != controller.RunByDefault;
		var speed = controller.Velocity.WithZ( 0 ).Length;

		// Speeding up uses the run time only once the run is asked for; slowing down keeps it until the player is
		// back at walking pace, so letting go of the keys from a sprint is not a walking stop.
		var runAccel = runHeld && !controller.IsDucking;
		var runDecel = runAccel || speed > controller.WalkSpeed + 1f;

		if ( !swapped )
		{
			walkAcceleration = controller.AccelerationTime;
			walkDeceleration = controller.DeaccelerationTime;
			swapped = true;
		}

		controller.AccelerationTime = runAccel ? RunAccelerationTime : walkAcceleration;
		controller.DeaccelerationTime = runDecel ? RunDecelerationTime : walkDeceleration;
	}

	void Restore()
	{
		if ( !swapped || !controller.IsValid() )
			return;

		controller.AccelerationTime = walkAcceleration;
		controller.DeaccelerationTime = walkDeceleration;
		swapped = false;
	}
}
