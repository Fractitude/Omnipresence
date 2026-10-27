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
/// This repeats the engine's forward / up / across / down trace, and it measures its own reach from the body's own
/// shape. That is the part that made a step a coin flip: the body is stopped by its <b>capsule</b>, which is wider than
/// the box the trace sweeps, so a player leaning on a kerb has the box a few units short of it. A reach copied from the
/// tick's movement misses that gap on some approaches and not others, and the across trace then comes down again on the
/// road below and gives up. Here the gap is worked out from the capsule and the box, the forward trace looks exactly
/// far enough to find a face the player is leaning on, and the box is carried a little past that face before it comes
/// down. Nothing is stepped onto that the player is not already up against.
/// </para>
/// <para>
/// Also: a body pressed into geometry sits a fraction of a unit inside it, so the box is narrowed until it sweeps clear
/// (as the engine does), and the lift goes only as far as there is room, so a step under an awning is still a step. It
/// only ever lifts the body onto a standable surface within <see cref="MoveModeWalk.StepUpHeight"/>, so it takes
/// exactly the steps the move mode already allows.
/// </para>
/// </summary>
public sealed class StepAssist : GameObjectSystem<StepAssist>
{
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

	/// <summary>Narrowing attempts while the body overlaps geometry, as the engine uses.</summary>
	const int ScaleSteps = 3;

	public StepAssist( Scene scene ) : base( scene )
	{
		// Before the physics step, like the engine's own step up, so the body is up there when the tick runs.
		Listen( Stage.StartFixedUpdate, 10, Tick, "StepAssist.Tick" );
	}

	void Tick()
	{
		// Movement is simulated by the owner. Everyone else's player is a proxy and must not be moved here.
		var controller = Players.LocalController;
		if ( !controller.IsValid() || controller.IsProxy || !controller.Body.IsValid() )
			return;
		if ( !controller.IsOnGround )
			return;

		// Only walking steps: noclip, hovering, swimming, a ladder and a seat have their own movement.
		if ( controller.Mode is not MoveModeWalk walk )
			return;

		if ( walk.StepUpHeight <= 0f )
			return;

		// Where the player is asking to go, and how fast they are going. A body stopped by a kerb still asks to walk on.
		var wish = controller.WishVelocity.WithZ( 0f );
		var moving = controller.Body.Velocity.WithZ( 0f );
		var direction = wish.Length > 1f ? wish.Normal : moving.Normal;
		if ( direction.IsNearlyZero() )
			return;

		// How far in front of the trace box the body's own collider can hold it off a wall: the capsule is wider than
		// the box, and a player leaning on a kerb stands exactly this far back from it.
		var boxHalfWidth = controller.BodyBox().Maxs.x;
		var bodyHalfWidth = controller.BodyCollider.IsValid()
			? MathF.Max( controller.BodyCollider.Radius, boxHalfWidth )
			: boxHalfWidth;
		var contact = MathF.Max( bodyHalfWidth - boxHalfWidth, 0.5f );

		// Look a tick of walking past that contact distance: far enough to find a face the player leans on, no further.
		var lookahead = contact + Slack + MathF.Max( wish.Length, moving.Length ) / TickRate;
		var from = controller.WorldPosition;

		// The body stops a fraction of a unit inside geometry, so the box has to be narrowed before it sweeps clear.
		for ( int attempt = 0; attempt < ScaleSteps; attempt++ )
		{
			var scale = 1f - attempt * 0.1f;
			var forward = controller.TraceBody( from, from + direction * lookahead, scale );
			if ( forward.StartedSolid )
				continue;

			if ( StepOnto( controller, walk, from, direction, contact, scale, forward ) )
				return;
		}
	}

	/// <summary>
	/// One attempt at the step <paramref name="forward"/> found: lift, go across, come down onto something standable.
	/// </summary>
	bool StepOnto( PlayerController controller, MoveModeWalk walk, Vector3 from, Vector3 direction, float contact,
		float scale, SceneTraceResult forward )
	{
		if ( !forward.Hit )
			return false;

		// A slope is something to walk up, not to step onto.
		if ( IsStandable( forward.Normal, walk.GroundAngle ) )
			return false;

		// Not against it yet: the physics step brings the body closer, and the next tick tries again from there.
		if ( forward.Distance > contact + Slack )
			return false;

		var stepUp = walk.StepUpHeight;
		var lift = controller.TraceBody( from, from + Vector3.Up * ( stepUp + Margin ), scale );
		if ( lift.StartedSolid )
			return false;

		// A step under an awning or a ceiling is still a step: lift only as far as there is room for it.
		var liftHeight = MathF.Min( stepUp + Margin, lift.Distance - 0.02f );
		if ( liftHeight < 2f )
			return false;

		var raised = from + Vector3.Up * liftHeight;

		// Just past the face, then once further along, for a kerb whose landing is set back from its lip.
		for ( int attempt = 0; attempt < 2; attempt++ )
		{
			var across = forward.Distance + Slack + attempt * ExtraReach;

			var over = controller.TraceBody( raised, raised + direction * across, scale );
			if ( over.StartedSolid )
				continue;

			var down = controller.TraceBody( over.EndPosition, over.EndPosition - Vector3.Up * ( liftHeight + Margin ), scale );
			if ( !down.Hit || !IsStandable( down.Normal, walk.GroundAngle ) )
				continue;

			var rise = (down.EndPosition - from).Dot( Vector3.Up );
			if ( rise <= Skin || rise > stepUp )
				continue;

			var target = down.EndPosition + Vector3.Up * Skin;
			if ( controller.TraceBody( target, target, scale ).StartedSolid )
				continue;

			controller.Body.WorldPosition = target;
			controller.Body.Velocity = controller.Body.Velocity.WithZ( 0f ) * 0.9f;
			return true;
		}

		return false;
	}

	/// <summary>Whether the player may stand on a surface with this normal.</summary>
	static bool IsStandable( Vector3 normal, float groundAngle ) =>
		Vector3.GetAngle( Vector3.Up, normal ) <= groundAngle;
}
