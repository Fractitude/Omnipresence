using Sandbox.Npcs.Tasks;

namespace Sandbox.Npcs.CombatNpc;

/// <summary>
/// Out of ammo: walk to an ammo box or a loose weapon and take it.
/// </summary>
public class CombatPickupSchedule : ScheduleBase
{
	/// <summary>
	/// The ammo pickup or dropped weapon to go and get.
	/// </summary>
	public GameObject Pickup { get; set; }

	/// <summary>
	/// Speed the NPC moves at.
	/// </summary>
	public float Speed { get; set; } = 180f;

	// Set when the walk failed, so the NPC leaves this pickup alone for a while instead of trying again at once
	private bool _unreachable;

	public override int Priority => SchedulePriority.Combat;

	// Whoever shoots at it, it finishes the errand: the ammo is what it needs most
	public override NpcAwareness InterruptedBy => NpcAwareness.None;

	protected override void OnStart()
	{
		_unreachable = false;

		if ( !Pickup.IsValid() )
			return;

		AddTask( new Reach( this, Pickup.WorldPosition ) { Speed = Speed } );
		AddTask( new Collect( Pickup ) );
	}

	protected override void OnEnd()
	{
		if ( _unreachable && Pickup.IsValid() && Npc is CombatNpc soldier )
			soldier.IgnorePickup( Pickup );
	}

	protected override bool ShouldCancel() => !Pickup.IsValid();

	/// <summary>
	/// Walks to the pickup, and remembers if it could not.
	/// </summary>
	private sealed class Reach : MoveTo
	{
		private readonly CombatPickupSchedule _schedule;

		public Reach( CombatPickupSchedule schedule, Vector3 position ) : base( position, 30f )
		{
			_schedule = schedule;
		}

		protected override TaskStatus OnUpdate()
		{
			var status = base.OnUpdate();
			if ( status == TaskStatus.Failed )
				_schedule._unreachable = true;

			return status;
		}
	}

	/// <summary>
	/// Takes what is there.
	/// </summary>
	private sealed class Collect : TaskBase
	{
		private readonly GameObject _pickup;
		private bool _taken;

		public Collect( GameObject pickup )
		{
			_pickup = pickup;
		}

		protected override void OnStart()
		{
			_taken = Npc is CombatNpc soldier && soldier.Collect( _pickup );

			if ( !_taken && Npc is CombatNpc other )
				other.IgnorePickup( _pickup );
		}

		protected override TaskStatus OnUpdate() => _taken ? TaskStatus.Success : TaskStatus.Failed;
	}
}
