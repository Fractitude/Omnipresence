using System;
using Sandbox;
using Sandbox.Npcs;

namespace Hl2Metrocop;

/// <summary>A melee weapon handed to him (CombineNpc.GivenWeapon.cs): run up to the target, face it, swing.</summary>
public sealed class CombineGivenCloseSchedule : CombineSchedule
{
	public GameObject Target { get; set; }
	public override int Priority => SchedulePriority.Combat;

	protected override void OnStart()
	{
		Combine.SetRelaxed( false );
		AddTask( new CombineGivenCloseTask( Target ) );
	}

	protected override bool ShouldCancel() => CombineNpc.IsDeadTarget( Target ) || !Combine.GivenIsMelee;
}

public sealed class CombineGivenCloseTask : CombineTask
{
	private readonly GameObject _target;
	private Vector3 _sent;
	private TimeSince _sinceSent = 10f;
	private TimeSince _sinceSeen;
	private Vector3 _lastSpot;
	private TimeSince _sinceProgress;

	public CombineGivenCloseTask( GameObject target ) => _target = target;

	protected override void OnStart()
	{
		_sinceSeen = 0;
		_lastSpot = Npc.WorldPosition;
		_sinceProgress = 0;
	}

	protected override TaskStatus OnUpdate()
	{
		var c = Combine;
		if ( !_target.IsValid() || CombineNpc.IsDeadTarget( _target ) ) return TaskStatus.Success;
		if ( c.CanSee( _target ) ) _sinceSeen = 0;
		if ( _sinceSeen > 4f ) return TaskStatus.Failed;

		var to = _target.WorldPosition - c.WorldPosition;
		var reach = MathF.Max( 40f, c.GivenReach * 0.75f );

		// Out of reach: after him at a run, re-aimed as he moves
		if ( to.WithZ( 0 ).Length > reach || MathF.Abs( to.z ) > 60f )
		{
			if ( _sinceSent > 0.25f && _target.WorldPosition.Distance( _sent ) > 12f )
			{
				_sent = _target.WorldPosition;
				_sinceSent = 0;
				Npc.Navigation.MoveTo( _sent, reach * 0.6f, c.RunSpeed, _target );
			}

			if ( Npc.WorldPosition.Distance( _lastSpot ) > 24f )
			{
				_lastSpot = Npc.WorldPosition;
				_sinceProgress = 0;
			}
			else if ( _sinceProgress > 2.5f )
			{
				c.SinceMoveStuck = 0;
				c.SinceMoveFailed = 0;
				return TaskStatus.Failed;
			}

			return TaskStatus.Running;
		}

		// In reach: plant the feet, turn onto him and swing (the weapon's own rate paces it)
		Npc.Navigation.Stop();
		_sent = default;
		_lastSpot = Npc.WorldPosition;
		_sinceProgress = 0;
		c.TurnTowards( _target.WorldPosition, 360f );
		if ( c.IsFacing( _target.WorldPosition, 25f ) ) c.TryShootAt( CombineNpc.AimPoint( _target ), 1f, _target );
		return TaskStatus.Running;
	}

	protected override void OnEnd() => Npc.Navigation.Stop();
}

/// <summary>A weapon with a minimum range (an RPG) and someone inside it: back off, facing him.</summary>
public sealed class CombineGivenBackOffSchedule : CombineSchedule
{
	public GameObject Target { get; set; }
	public Vector3 Spot { get; set; }
	public override int Priority => SchedulePriority.Combat;

	protected override void OnStart()
	{
		Combine.SetRelaxed( false );
		AddTask( Combine.CombatMove( Spot, 30f ) );
	}

	protected override bool ShouldCancel() => CombineNpc.IsDeadTarget( Target );
}
