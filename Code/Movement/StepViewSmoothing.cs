namespace Sandbox;

/// <summary>
/// Takes the jolt out of the picture when the player steps up or down, without moving the player.
/// <para>
/// A tread has to be cleared in one physics tick: the body cannot move forward onto it while its feet are below it, so
/// <see cref="StepAssist"/> lifts the whole tread height at once, and walking off one drops the body in one tick. The
/// eye is the body, so the view snaps up and down a stair at a time, which is what makes a staircase feel like a ladder
/// rather than a ramp.
/// </para>
/// <para>
/// This holds the view back by whatever the body just jumped and lets it catch up over a fraction of a second, so the
/// rise reads as a ramp. Only the picture moves: the body, the collider and the eye the game aims from are untouched.
/// A fall is left alone, and so is a jump big enough to be a teleport.
/// </para>
/// <para>
/// Because the view is what the player aims by, <see cref="BaseSandboxWeapon.AimRay"/> adds the same hold to the shot
/// for the local player, so a shot taken mid-step goes exactly where the crosshair is. The hold is per client and never
/// networked: each client only ever smooths its own view, and a held weapon's shot is traced by its owner.
/// </para>
/// </summary>
public sealed class StepViewSmoothing : Component, ICameraModifier
{
	/// <summary>
	/// Seconds the view takes to catch up after a step. 0 turns the smoothing off and the view snaps as it did before.
	/// </summary>
	[ConVar( "omni.step_smoothing", ConVarFlags.Saved,
		Help = "Seconds the view takes to catch up after stepping up or down. 0 turns it off." )]
	public static float CatchUp { get; set; } = 0.09f;

	/// <summary>How far the body has to move vertically in one frame to count as a step, in units.</summary>
	const float Jump = 1.5f;

	/// <summary>A body that moved this far in a frame was teleported, not stepped: nothing to smooth.</summary>
	const float Teleport = 64f;

	/// <summary>The most the view may be held away from the body, so nothing odd can pile up.</summary>
	const float MaxHold = 32f;

	/// <summary>The one on this client, for whoever needs the view's hold: the aim ray does.</summary>
	static StepViewSmoothing instance;

	float hold;
	float lastZ;
	bool primed;

	/// <summary>How far the view is held behind the body right now, in units. Zero when nothing is being smoothed.</summary>
	public static float Hold => instance.IsValid() ? instance.hold : 0f;

	protected override void OnAwake() => instance = this;

	protected override void OnDestroy()
	{
		if ( instance == this )
			instance = null;
	}

	protected override void OnUpdate()
	{
		var controller = Players.LocalController;
		if ( CatchUp <= 0f || !controller.IsValid() || !controller.Body.IsValid() )
		{
			hold = 0f;
			primed = false;
			return;
		}

		// The body jumps a whole tread in one tick going up and one tick going down; hold the view by that much.
		var z = controller.WorldPosition.z;
		if ( primed && controller.IsOnGround )
		{
			var moved = z - lastZ;
			if ( MathF.Abs( moved ) > Teleport )
				hold = 0f;
			else if ( MathF.Abs( moved ) > Jump )
				hold = (hold - moved).Clamp( -MaxHold, MaxHold );
		}

		lastZ = z;
		primed = true;

		hold = MathF.Abs( hold ) < 0.05f ? 0f : hold * MathF.Exp( -Time.Delta / CatchUp );
	}

	/// <summary>The camera pipeline: hold the composed view back by what is left of the last step.</summary>
	void ICameraModifier.ModifyCamera( CameraComponent cam, ref CameraView view )
	{
		if ( hold != 0f )
			view.Position += Vector3.Up * hold;
	}
}
