namespace Sandbox.Npcs;

public partial class Npc
{
	/// <summary>
	/// A walking NPC is a kinematic body: nothing can push it, so it pushes whatever it walks into, however heavy.
	/// It leaves objects heavier than this alone (a car) and slides along them instead. Lighter ones are shoved
	/// as before.
	/// </summary>
	[Property, Group( "Physics" )]
	public float MaxPushMass { get; set; } = 100f;

	// The NPC body is a box this wide, and a little above the feet so a kerb doesn't count
	private static readonly BBox BodyBounds = new( new Vector3( -14, -14, 12 ), new Vector3( 14, 14, 70 ) );

	/// <summary>
	/// The agent steps itself and teleports the body to it. Take that over, so a step can be stopped before it
	/// pushes a heavy object. See <see cref="MoveBody"/>.
	/// </summary>
	private void TakeOverMovement()
	{
		if ( !IsProxy && _navAgent.IsValid() )
			_navAgent.UpdatePosition = false;
	}

	/// <summary>
	/// Move the body to where the agent wants it, unless that would push something heavy: the part of the step
	/// into it is dropped and the NPC slides along it.
	/// </summary>
	private void MoveBody()
	{
		if ( !_navAgent.IsValid() || !_navAgent.Enabled || isPickedUp || !IsOnGround )
			return;

		var from = WorldPosition;
		var to = _navAgent.AgentPosition;
		var flat = (to - from).WithZ( 0 );

		if ( flat.Length > 0.001f )
		{
			flat = KeepOffHeavy( from, flat );
			flat = KeepOffHeavy( from, flat );
		}

		var step = flat + Vector3.Up * (to.z - from.z);
		WorldPosition = from + step;

		// The agent must not run off ahead of a body that was held back
		if ( (WorldPosition - to).WithZ( 0 ).Length > 0.5f )
			_navAgent.SetAgentPosition( WorldPosition );
	}

	/// <summary>
	/// The step, minus whatever part of it would push a heavy rigidbody.
	/// </summary>
	private Vector3 KeepOffHeavy( Vector3 from, Vector3 step )
	{
		if ( step.Length < 0.001f )
			return step;

		var tr = Scene.Trace.Box( BodyBounds, from, from + step.Normal * (step.Length + 1f) )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();

		if ( !tr.Hit || !tr.GameObject.IsValid() )
			return step;

		var body = tr.GameObject.Components.GetInAncestorsOrSelf<Rigidbody>();
		if ( !body.IsValid() || !body.MotionEnabled || body.Mass <= MaxPushMass )
			return step;

		var normal = tr.Normal.WithZ( 0 ).Normal;
		var into = Vector3.Dot( step, normal );
		if ( into >= 0f )
			return step;

		var slide = step - normal * into;

		// Walking straight at it leaves nothing to slide along: go round, on whichever side of it we are on
		if ( slide.Length < step.Length * 0.35f )
		{
			var along = Vector3.Cross( normal, Vector3.Up ).Normal;
			var side = Vector3.Dot( from - body.WorldPosition, along ) >= 0f ? 1f : -1f;
			slide = along * side * step.Length;
		}

		return slide;
	}
}

