namespace Sandbox;

/// <summary>
/// Takes the jolt out of the picture when the player steps up or down, without moving the player.
/// <para>
/// A tread has to be cleared in one physics tick: the body cannot move forward onto it while its feet are below it, so
/// <see cref="StepAssist"/> lifts the whole tread height at once, and walking off one drops the body in one tick. The
/// eye is the body, so the view snapped up and down a stair at a time, which is what made a staircase feel like a
/// ladder rather than a ramp. In third person the character model snapped with it, under a camera that was eased.
/// </para>
/// <para>
/// This holds the view back by whatever the body just jumped and lets it catch up over a fraction of a second, and it
/// carries the character model with it, so the picture — camera and character together — reads as a ramp. Only the
/// picture moves: the body, the collider, the eye other clients see and the network position are untouched, so there is
/// nothing to sync. Each client smooths its own view and its own character. A fall is left alone, and so is a jump big
/// enough to be a teleport.
/// </para>
/// <para>
/// The character model cannot be moved directly: it is a networked child whose own transform is written back every
/// tick, so it is put under a holder object of ours, which carries the hold instead.
/// </para>
/// <para>
/// Because the view is what the player aims by, <see cref="BaseSandboxWeapon.AimRay"/> adds the same hold to the shot for
/// the local player, so a shot taken mid-step goes exactly where the crosshair is.
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

	/// <summary>The object the character model is put under, so its hold survives the model's own transform sync.</summary>
	const string HolderName = "Step View Smoothing";

	/// <summary>The one on this client, for whoever needs the view's hold: the aim ray does.</summary>
	static StepViewSmoothing instance;

	float hold;
	float lastZ;
	bool primed;

	/// <summary>The object the character model hangs under while it is eased, so it can be moved as one.</summary>
	GameObject holder;

	/// <summary>How far the view is held behind the body right now, in units. Zero when nothing is being smoothed.</summary>
	public static float Hold => instance.IsValid() ? instance.hold : 0f;

	protected override void OnAwake() => instance = this;

	protected override void OnDestroy()
	{
		RestoreBody();

		if ( instance == this )
			instance = null;
	}

	protected override void OnDisabled() => RestoreBody();

	protected override void OnUpdate()
	{
		var controller = Players.LocalController;
		if ( CatchUp <= 0f || !controller.IsValid() || !controller.Body.IsValid() )
		{
			hold = 0f;
			primed = false;
			RestoreBody();
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
		HoldBody( controller );
	}

	/// <summary>
	/// Carry the character model with the view, so in third person the character and the camera rise together instead of
	/// the character snapping up a step under a camera that is easing.
	/// </summary>
	void HoldBody( PlayerController controller )
	{
		if ( !controller.Renderer.IsValid() )
		{
			RestoreBody();
			return;
		}

		var model = controller.Renderer.GameObject;

		// The model's own transform is written back every tick (it is a networked child), so it goes under a holder of
		// ours and the holder is what moves. Re-made if the model is ever put back on the player, or re-dressed.
		if ( !holder.IsValid() || holder.Parent != controller.GameObject )
		{
			holder = controller.GameObject.Children.FirstOrDefault( x => x.Name == HolderName );
			if ( !holder.IsValid() )
			{
				holder = new GameObject( controller.GameObject, true, HolderName );
				holder.Flags |= GameObjectFlags.NotSaved;
				holder.NetworkMode = NetworkMode.Never;
			}
		}

		if ( model.Parent != holder )
		{
			var local = model.LocalPosition;
			model.Parent = holder;
			model.LocalPosition = local;
		}

		var up = holder.Parent.IsValid() ? holder.Parent.WorldRotation.Inverse * Vector3.Up : Vector3.Up;
		holder.LocalPosition = up * hold;
	}

	/// <summary>Put the character model back where the player has it.</summary>
	void RestoreBody()
	{
		if ( holder.IsValid() )
			holder.LocalPosition = Vector3.Zero;
	}

	/// <summary>The camera pipeline: hold the composed view back by what is left of the last step.</summary>
	void ICameraModifier.ModifyCamera( CameraComponent cam, ref CameraView view )
	{
		if ( hold != 0f )
			view.Position += Vector3.Up * hold;
	}
}
