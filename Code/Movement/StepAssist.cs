using Sandbox.Movement;

namespace Sandbox;

/// <summary>
/// Walks the player up steps at walking pace.
/// <para>
/// The engine's <see cref="PlayerController"/> steps up with a forward trace as long as the wish velocity times the
/// <b>frame</b> delta. That is shorter than how far the body actually moves in a physics tick as soon as the frame rate
/// is above the tick rate, so the step is never in front of the trace: the body is simply stopped by it, and there the
/// leftover velocity is far too small to find it again. Sprinting pushes hard enough for that second attempt to land,
/// which is why a step was climbed only while running.
/// </para>
/// <para>
/// Two more things decide whether a kerb was climbable, and both were left to chance before:
/// </para>
/// <list type="bullet">
/// <item>
/// <b>Distance.</b> The body is stopped by its capsule, which is wider than the box the trace sweeps (about three units
/// wider here), so a player leaning on a kerb has the box short of it. Any reach copied from the movement lands back on
/// the road below. The reach is therefore measured from where the trace hit the face, a little past it.
/// </item>
/// <item>
/// <b>Direction.</b> A kerb the player walks <i>along</i> rather than at is never in front of the trace: it slides along
/// the face for tens of units before it catches the corner, which is far past anything to step onto. The face is
/// looked for in a small fan either side of the way the player is walking, and the step itself is taken square into that
/// face, so walking along a kerb while leaning into it climbs it.
/// </item>
/// </list>
/// <para>
/// It also narrows the box while the body overlaps geometry, lifts only as far as there is room (a step under an awning
/// is still a step), requires room to stand where it lands, and only ever lifts onto a standable surface within
/// <see cref="MoveModeWalk.StepUpHeight"/>.
/// </para>
/// </summary>
public sealed class StepAssist : GameObjectSystem<StepAssist>
{
	/// <summary>
	/// How high a step the player walks up, in units. The engine's own step up is switched off (<see
	/// cref="MoveModeWalk.StepUpHeight"/> is 0 on <c>Assets/Player.prefab</c>) so that every step goes through the checks
	/// below, and this is the height it would have stepped. Raise it for taller steps, lower it to be stricter.
	/// </summary>
	[ConVar( "omni.step_height", ConVarFlags.Saved, Help = "How high a step the player can walk up, in units." )]
	public static float StepHeight { get; set; } = 16f;

	/// <summary>The project's physics tick rate, for a reach that does not depend on the frame rate.</summary>
	const float TickRate = 50f;

	/// <summary>How far above the landing the body is placed, so it does not start inside the step.</summary>
	const float Skin = 0.1f;

	/// <summary>How far above <see cref="MoveModeWalk.StepUpHeight"/> to lift, so a step of exactly that height lands.</summary>
	const float Margin = 0.5f;

	/// <summary>Slack added to the contact distance, both for the forward look and for the reach along the step.</summary>
	const float Slack = 0.6f;

	/// <summary>How much further along the step to reach on the second try, for a kerb whose landing is set back.</summary>
	const float ExtraReach = 1.5f;

	/// <summary>How much the player has to be walking into a face for it to count as a step, not a wall passed by.</summary>
	const float Lean = 0.15f;

	/// <summary>How far the probes that look for the face under a landing reach.</summary>
	const float FaceProbeLength = 40f;

	/// <summary>How much further out the face under a landing may sit before the landing counts as an overhang.</summary>
	const float Leeway = 3f;

	/// <summary>How far a step's face may start above the player's feet: a hand-placed prop can float a little.</summary>
	const float GapLeeway = 4f;

	/// <summary>How far apart the sweeps are tried above the feet, to get clear of the ground.</summary>
	const float HeightStep = 4f;

	/// <summary>Narrowing attempts while the body overlaps geometry, as the engine uses.</summary>
	const int ScaleSteps = 3;

	/// <summary>The view smoothing this system keeps alive, so stepping does not snap the camera.</summary>
	StepViewSmoothing viewSmoothing;

	/// <summary>Directions probed around the way the player walks, in degrees: a kerb walked along sits to the side.</summary>
	static readonly float[] Fan = { 0f, 35f, -35f, 70f, -70f };

	public StepAssist( Scene scene ) : base( scene )
	{
		// Before the physics step, like the engine's own step up, so the body is up there when the tick runs.
		Listen( Stage.StartFixedUpdate, 10, Tick, "StepAssist.Tick" );
	}

	void Tick()
	{
		EnsureViewSmoothing();

		// Movement is simulated by the owner. Everyone else's player is a proxy and must not be moved here.
		var controller = Players.LocalController;
		if ( !controller.IsValid() || controller.IsProxy || !controller.Body.IsValid() )
			return;
		if ( !controller.IsOnGround )
			return;

		// Only walking steps: noclip, hovering, swimming, a ladder and a seat have their own movement.
		if ( controller.Mode is not MoveModeWalk walk )
			return;

		if ( StepHeight <= 0f )
			return;

		// Where the player is asking to go, and how fast they are going. A body stopped by a kerb still asks to walk on.
		var wish = controller.WishVelocity.WithZ( 0f );
		var moving = controller.Body.Velocity.WithZ( 0f );
		var heading = wish.Length > 1f ? wish.Normal : moving.Normal;
		if ( heading.IsNearlyZero() )
			return;

		// How far in front of the trace box the body's own collider can hold it off a wall: the capsule is wider than
		// the box, and a player leaning on a kerb stands exactly this far back from it.
		var boxHalfWidth = controller.BodyBox().Maxs.x;
		var bodyHalfWidth = controller.BodyCollider.IsValid()
			? MathF.Max( controller.BodyCollider.Radius, boxHalfWidth )
			: boxHalfWidth;
		var contact = MathF.Max( bodyHalfWidth - boxHalfWidth, 0.5f );

		// Look a tick of walking past that contact distance: far enough to find a face the player leans on.
		var lookahead = contact + Slack + MathF.Max( wish.Length, moving.Length ) / TickRate;
		var from = controller.WorldPosition;

		// The way the player walks first, then a fan either side of it.
		foreach ( var yaw in Fan )
		{
			var probe = Rotation.FromYaw( yaw ) * heading;

			// Swept from the feet, the first thing a step is behind is the ground itself: on a bank it rises into the
			// sweep, and that is a slope to walk up, not the face of a step. So the sweep is tried at a few heights
			// above the feet, where it is clear of the ground, until the face turns up.
			for ( var height = 0f; height <= StepHeight; height += HeightStep )
			{
				var origin = from + Vector3.Up * height;

				// The body stops a fraction of a unit inside geometry, so the box is narrowed until it sweeps clear.
				for ( int attempt = 0; attempt < ScaleSteps; attempt++ )
				{
					var scale = 1f - attempt * 0.1f;
					var forward = controller.TraceBody( origin, origin + probe * lookahead, scale );
					if ( forward.StartedSolid )
						continue;

					// Nothing that way at this height, or the ground rather than a face above it.
					if ( !forward.Hit || IsStandable( forward.Normal, walk.GroundAngle ) )
						break;

					if ( StepOnto( controller, walk, from, probe, heading, contact, scale, forward ) )
						return;

					break;
				}
			}
		}
	}

	/// <summary>
	/// The camera modifier that takes the jolt out of a step lives on a component, and components have to be somewhere:
	/// a hidden object of its own, made once, rather than anything added to the player prefab.
	/// </summary>
	void EnsureViewSmoothing()
	{
		if ( viewSmoothing.IsValid() )
			return;

		var go = new GameObject( true, "Step View Smoothing" );
		go.Flags |= GameObjectFlags.NotSaved;
		go.NetworkMode = NetworkMode.Never;
		viewSmoothing = go.Components.Create<StepViewSmoothing>();
	}

	/// <summary>
	/// One attempt at the step <paramref name="forward"/> found: lift, go across, come down onto something standable.
	/// </summary>
	bool StepOnto( PlayerController controller, MoveModeWalk walk, Vector3 from, Vector3 probe, Vector3 heading,
		float contact, float scale, SceneTraceResult forward )
	{
		if ( !forward.Hit )
			return false;

		// Not against it yet: the physics step brings the body closer, and the next tick tries again from there.
		if ( forward.Distance > contact + Slack )
			return false;

		// A slope is something to walk up, not to step onto.
		if ( IsStandable( forward.Normal, walk.GroundAngle ) )
			return false;

		var face = forward.Normal.WithZ( 0f );
		if ( face.IsNearlyZero() )
			return false;

		// A wall passed by on the way somewhere else is not a step: the player has to be leaning into it.
		var intoFace = -face.Normal;
		if ( Vector3.Dot( heading, intoFace ) < Lean )
			return false;

		var stepUp = StepHeight;
		var lift = controller.TraceBody( from, from + Vector3.Up * ( stepUp + Margin ), scale );
		if ( lift.StartedSolid )
			return false;

		// A step under an awning or a ceiling is still a step: lift only as far as there is room for it.
		var liftHeight = MathF.Min( stepUp + Margin, lift.Distance - 0.02f );
		if ( liftHeight < 2f )
			return false;

		var raised = from + Vector3.Up * liftHeight;

		// A landing is only taken if the body fits on it, so a ledge narrower than the player is not a step.
		bool Land( Vector3 routeDirection, float across )
		{
			var over = controller.TraceBody( raised, raised + routeDirection * across, scale );
			if ( over.StartedSolid )
				return false;

			var down = controller.TraceBody( over.EndPosition, over.EndPosition - Vector3.Up * ( liftHeight + Margin ), scale );
			if ( !down.Hit || !IsStandable( down.Normal, walk.GroundAngle ) )
				return false;

			var rise = (down.EndPosition - from).Dot( Vector3.Up );
			if ( rise <= Skin || rise > stepUp )
				return false;

			// The rise has to be solid down at the player's feet as well: a window sill, a rail or an awning is a ledge
			// hanging over a recess, so its face only starts well above the floor, and stepping onto one is where a
			// step up stops looking like walking. The face is walked down from the landing to find where it begins; a
			// prop placed by hand that floats a little off the ground is still a step, so a few units of gap are fine.
			var aboveLanding = FaceProbe( controller, from + Vector3.Up * MathF.Max( rise - 1f, 0.5f ), intoFace );
			if ( !aboveLanding.Hit )
				return false;

			var faceStartsAt = float.PositiveInfinity;
			for ( var height = 1f; height < rise; height += 2f )
			{
				var hit = FaceProbe( controller, from + Vector3.Up * height, intoFace );
				if ( !hit.Hit || hit.Distance > aboveLanding.Distance + Leeway )
					continue;

				faceStartsAt = height;
				break;
			}

			if ( faceStartsAt > GapLeeway )
				return false;

			var target = down.EndPosition + Vector3.Up * Skin;
			if ( controller.TraceBody( target, target, scale ).StartedSolid )
				return false;

			controller.Body.WorldPosition = target;
			controller.Body.Velocity = controller.Body.Velocity.WithZ( 0f ) * 0.9f;
			return true;
		}

		// Square into the face first, which is the step a player means when they lean on a kerb; then along the way
		// they are walking, for a step that is not square to it. Each tried just past the face, then further along.
		var reach = forward.Distance + Slack;

		return Land( intoFace, reach )
			|| Land( intoFace, reach + ExtraReach )
			|| Land( probe, reach )
			|| Land( probe, reach + ExtraReach );
	}

	/// <summary>Where the face of a step is, looked for square into it from <paramref name="from"/>.</summary>
	static SceneTraceResult FaceProbe( PlayerController controller, Vector3 from, Vector3 intoFace ) =>
		controller.Scene.Trace.Ray( from, from + intoFace * FaceProbeLength )
			.IgnoreGameObjectHierarchy( controller.GameObject )
			.WithCollisionRules( controller.GameObject.Tags )
			.Run();

	/// <summary>Whether the player may stand on a surface with this normal.</summary>
	static bool IsStandable( Vector3 normal, float groundAngle ) =>
		Vector3.GetAngle( Vector3.Up, normal ) <= groundAngle;
}
