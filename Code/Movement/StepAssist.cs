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
/// This repeats the engine's forward / up / across / down trace with a lookahead of a whole tick's walk, so a step the
/// player is standing against is climbed at walking speed, on any frame rate. It only ever lifts the body when a
/// standable surface within <see cref="MoveModeWalk.StepUpHeight"/> is reachable with clearance, so it takes exactly the
/// steps the move mode already allows and changes nothing else.
/// </para>
/// </summary>
public sealed class StepAssist : GameObjectSystem<StepAssist>
{
	/// <summary>The project's physics tick rate, for a lookahead that does not depend on the frame rate.</summary>
	const float TickRate = 50f;

	/// <summary>How far above the landing the body is placed, so it does not start inside the step.</summary>
	const float Skin = 0.1f;

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

		var stepUp = walk.StepUpHeight;
		if ( stepUp <= 0f )
			return;

		// Where the player is asking to go, and how fast they are going. A body stopped by a kerb still asks to walk on.
		var wish = controller.WishVelocity.WithZ( 0f );
		var moving = controller.Body.Velocity.WithZ( 0f );
		var direction = wish.Length > 1f ? wish.Normal : moving.Normal;
		if ( direction.IsNearlyZero() )
			return;

		// One tick of movement, plus the clearance a step edge can hide behind, so the lookahead is never zero.
		var lookahead = MathF.Max( wish.Length, moving.Length ) / TickRate + 0.5f;
		var from = controller.WorldPosition;

		var forward = controller.TraceBody( from, from + direction * lookahead );
		if ( forward.StartedSolid || !forward.Hit )
			return;

		// A slope is something to walk up, not to step onto.
		if ( IsStandable( forward.Normal, walk.GroundAngle ) )
			return;

		var lift = controller.TraceBody( from, from + Vector3.Up * stepUp );
		if ( lift.StartedSolid || lift.Distance < 2f )
			return;

		// Across at the top of the step: the rest of the tick's walk, so there is room to land on.
		var across = MathF.Max( lookahead - forward.Distance, 0.5f );
		var over = controller.TraceBody( lift.EndPosition, lift.EndPosition + direction * across );
		if ( over.StartedSolid )
			return;

		var down = controller.TraceBody( over.EndPosition, over.EndPosition - Vector3.Up * stepUp );
		if ( !down.Hit || !IsStandable( down.Normal, walk.GroundAngle ) )
			return;

		var rise = (down.EndPosition - from).Dot( Vector3.Up );
		if ( rise <= Skin || rise > stepUp )
			return;

		var target = down.EndPosition + Vector3.Up * Skin;
		if ( controller.TraceBody( target, target ).StartedSolid )
			return;

		controller.Body.WorldPosition = target;
		controller.Body.Velocity = moving * 0.9f;
	}

	/// <summary>Whether the player may stand on a surface with this normal.</summary>
	static bool IsStandable( Vector3 normal, float groundAngle ) =>
		Vector3.GetAngle( Vector3.Up, normal ) <= groundAngle;
}
