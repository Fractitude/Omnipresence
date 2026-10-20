using Sandbox.Movement;

namespace Sandbox;

/// <summary>
/// Weightless flight: double tap Jump to start hovering, and again to land. While hovering the player moves in
/// whatever direction they face, including up and down, and stays put when no direction key is held.
/// </summary>
public sealed class MoveModeHover : MoveMode
{
	/// <summary>Seconds between two Jump presses that count as a double tap.</summary>
	[Property, Range( 0.1f, 0.6f )] public float DoubleTapTime { get; set; } = 0.3f;

	/// <summary>Speed while hovering, in units per second.</summary>
	[Property] public float HoverSpeed { get; set; } = 250f;

	/// <summary>Speed while hovering and holding Run.</summary>
	[Property] public float FastHoverSpeed { get; set; } = 450f;

	/// <summary>How quickly velocity follows the wanted velocity. Higher is snappier.</summary>
	[Property] public float Responsiveness { get; set; } = 6f;

	/// <summary>Score while hovering; above walking, swimming and ladders so hovering wins.</summary>
	[Property] public int Priority { get; set; } = 20;

	[Sync] public bool IsHovering { get; private set; }

	public override bool AllowGrounding => false;
	public override bool AllowFalling => true;

	TimeSince sinceJump = 999f;

	protected override void OnUpdate()
	{
		if ( IsProxy || !Controller.IsValid() || !Input.Pressed( "Jump" ) )
			return;

		if ( sinceJump < DoubleTapTime )
		{
			IsHovering = !IsHovering;
			sinceJump = 999f;
		}
		else
		{
			sinceJump = 0f;
		}
	}

	public override int Score( PlayerController controller )
	{
		return IsHovering ? Priority : -100;
	}

	public override void AddVelocity()
	{
		var body = Controller.Body;
		var move = Input.AnalogMove;
		var speed = Input.Down( "Run" ) ? FastHoverSpeed : HoverSpeed;

		// Forward follows the full look direction, so looking up and pressing forward climbs.
		var look = Controller.EyeAngles.ToRotation();
		var wish = look.Forward * move.x + look.Left * move.y;
		wish = wish.ClampLength( 1f ) * speed;

		body.Velocity = Vector3.Lerp( body.Velocity, wish, ( Responsiveness * Time.Delta ).Clamp( 0f, 1f ) );
	}

	public override void UpdateRigidBody( Rigidbody body )
	{
		body.Gravity = false;
		body.LinearDamping = 0f;
		body.AngularDamping = 1f;
	}
}
