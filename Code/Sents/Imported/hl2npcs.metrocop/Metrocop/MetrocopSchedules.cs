using System;
using System.Linq;
using Sandbox;
using Sandbox.Npcs;
using TaskStatus = Sandbox.Npcs.TaskStatus;
using Sandbox.Npcs.Tasks;

namespace Hl2Metrocop;

/// <summary>HL2 lists no interrupts for these (draw the pistol, throw the manhack, the baton, a shove, burning).</summary>
public interface ICopUninterruptible { }

/// <summary>
/// Base for the cop's schedules. Each one ends itself on the conditions its HL2 counterpart lists
/// as interrupts (ShouldCancel), and then he chooses again.
/// </summary>
public abstract class CopSchedule : CombineSchedule
{
	public GameObject Target { get; set; }
	public Vector3 Threat { get; set; }

	public override int Priority => SchedulePriority.Combat;
	public override NpcAwareness InterruptedBy => this is ICopUninterruptible ? NpcAwareness.None : CombineNpc.GrenadeDanger;

	protected TimeSince Started { get; private set; }

	/// <summary>Fires at the enemy while it moves (HL2's move-and-shoot, with an attack turn).</summary>
	public virtual bool MoveAndShoot => false;

	/// <summary>Reset what one use of the schedule set - the instance is reused.</summary>
	public virtual void Configure() { }

	protected override void OnStart()
	{
		Started = 0;
		Combine.SetRelaxed( false );
		Build();
	}

	protected abstract void Build();

	protected bool HurtSinceStart => (float)Combine.SinceCopHurt < (float)Started;
	protected bool TargetDead => Target is not null && CombineNpc.IsDeadTarget( Target );
	protected bool SeesTarget => Target.IsValid() && Combine.CanSee( Target );
	protected bool CanShoot => Combine.CopCanRangeAttack( Target );
	protected bool CanMelee => Combine.CopCanMelee( Target );
	protected bool NewEnemyInSight => Combine.CopBestVisible() is { } best && best != Target && Combine.PlayerIsCriminal( best );

	protected Vector3 TargetPosition => Target.IsValid() && SeesTarget ? Target.WorldPosition : Threat;
}

/// <summary>Play an animation and run code at given moments of it (HL2's animation events).</summary>
public sealed class CopAnimTask : CombineTask
{
	private readonly string _name;
	private readonly (float At, Action Do)[] _events;
	private readonly GameObject _face;
	private float _duration;
	private TimeSince _started;
	private int _next;

	public CopAnimTask( string name, GameObject face = null, params (float At, Action Do)[] events )
	{
		_name = name;
		_face = face;
		_events = events;
	}

	protected override void OnStart()
	{
		Npc.Navigation.Stop();
		_duration = Combine.PlayAction( _name );
		_started = 0;
		_next = 0;
	}

	protected override TaskStatus OnUpdate()
	{
		if ( _face.IsValid() ) Combine.TurnTowards( _face.WorldPosition, 150f );

		while ( _next < _events.Length && _started >= _events[_next].At )
			_events[_next++].Do?.Invoke();

		return _started > _duration ? TaskStatus.Success : TaskStatus.Running;
	}

	protected override void OnEnd()
	{
		// Cut short: whatever was still to happen happens now (a manhack never stays stuck in the hand)
		while ( _next < _events.Length ) _events[_next++].Do?.Invoke();
	}
}

/// <summary>Stand facing someone for a while (TASK_WAIT_FACE_ENEMY).</summary>
public sealed class CopWaitFaceTask : CombineTask
{
	private readonly GameObject _target;
	private readonly Vector3 _spot;
	private readonly float _seconds;
	private TimeSince _started;

	public CopWaitFaceTask( GameObject target, Vector3 spot, float seconds )
	{
		_target = target;
		_spot = spot;
		_seconds = seconds;
	}

	protected override void OnStart()
	{
		Npc.Navigation.Stop();
		_started = 0;
	}

	protected override TaskStatus OnUpdate()
	{
		var at = _target.IsValid() && Combine.CanSee( _target ) ? _target.WorldPosition : _spot;

		// Someone circling right round him: he turns as fast as they go round (a walk at arm's length
		// is over 200 degrees a second), or they'd stay behind him for good
		var close = (at - Npc.WorldPosition).WithZ( 0 ).Length < 150f;
		Combine.TurnTowards( at, close ? CombineNpc.CloseTurnSpeed : 150f );
		return _started > _seconds ? TaskStatus.Success : TaskStatus.Running;
	}
}

/// <summary>One burst, the way HL2's shot regulator paces it, then the rest before the next.</summary>
public sealed class CopBurstTask : CombineTask
{
	private readonly GameObject _target;
	private readonly GameObject _aimAt;
	private TimeUntil _next;
	private TimeUntil _timeout;

	public CopBurstTask( GameObject target, GameObject aimAt = null )
	{
		_target = target;
		_aimAt = aimAt;
	}

	protected override void OnStart()
	{
		Npc.Navigation.Stop();
		// HL2's shot regulator lives on the NPC: a burst broken off by a hit carries on where it stopped
		Combine.EnsureBurst( _target );
		_next = 0f;
		_timeout = 6f;
	}

	protected override TaskStatus OnUpdate()
	{
		var c = Combine;
		if ( _timeout ) return TaskStatus.Failed;

		var aimObject = _aimAt.IsValid() ? _aimAt : _target;
		if ( !aimObject.IsValid() || aimObject == _target && CombineNpc.IsDeadTarget( _target ) ) return TaskStatus.Success;

		var aim = _aimAt.IsValid() ? _aimAt.GetBounds().Center : CombineNpc.AimPoint( _target );
		c.TurnTowards( aim, 240f );
		if ( !c.IsFacing( aim, 12f ) || !_next || !c.CanFireNow ) return TaskStatus.Running;
		if ( c.Clip <= 0 ) return TaskStatus.Success;

		var line = c.CheckLineOfFire( aim, _aimAt.IsValid() ? _aimAt : _target );
		if ( line == LineOfFire.Friendly ) return TaskStatus.Failed;
		if ( line == LineOfFire.Blocked && !_aimAt.IsValid() ) return TaskStatus.Failed;

		if ( c.TryShootAt( aim, 1f, _aimAt.IsValid() ? null : _target ) )
		{
			_next = c.BurstInterval;
			if ( c.BurstShotFired() ) return TaskStatus.Success;
		}

		return TaskStatus.Running;
	}
}

/// <summary>Say one of HL2's police lines as a step of a schedule (TASK_SPEAK_SENTENCE).</summary>
public sealed class CopSayTask : CombineTask
{
	private readonly Action _say;
	public CopSayTask( Action say ) => _say = say;

	protected override TaskStatus OnUpdate()
	{
		_say?.Invoke();
		return TaskStatus.Success;
	}
}

// ==================================================================== fighting

/// <summary>SCHED_RANGE_ATTACK1 (pistol) / SCHED_METROPOLICE_SMG_NORMAL_ATTACK: stop, face, one burst.</summary>
public sealed class CopRangeAttackSchedule : CopSchedule
{
	protected override void Build()
	{
		var c = Combine;
		Npc.Navigation.Stop();
		Npc.Animation.SetLookTarget( Target );
		c.Crouched = false;
		AddTask( new CombineFaceTask( Target ) );
		AddTask( new CopBurstTask( Target ) );
	}

	protected override void OnEnd() => Npc.Animation.ClearLookTarget();

	// COND_NEW_ENEMY, ENEMY_DEAD, LIGHT/HEAVY_DAMAGE, ENEMY_OCCLUDED, NO_PRIMARY_AMMO, HEAR_DANGER, WEAPON_BLOCKED_BY_FRIEND, WEAPON_SIGHT_OCCLUDED
	protected override bool ShouldCancel() =>
		TargetDead || NewEnemyInSight || HurtSinceStart && Started > 0.1f || Combine.Clip <= 0
		|| Started > 0.5f && !SeesTarget;
}

/// <summary>
/// SCHED_METROPOLICE_ADVANCE: stand angry facing him a second - "give the guy some time to come
/// out on his own" - then up to three more, then move up to where he can be seen.
/// </summary>
public sealed class CopAdvanceSchedule : CopSchedule
{
	protected override void Build()
	{
		var c = Combine;
		Npc.Navigation.Stop();
		AddTask( new CombineFaceTask( TargetPosition ) );
		AddTask( new CopWaitFaceTask( Target, Threat, 1f ) );
		AddTask( new CopWaitFaceTask( Target, Threat, Game.Random.Float( 0f, 3f ) ) );
		AddTask( new CombineCallTask( () =>
		{
			// TASK_GET_PATH_TO_ENEMY_LOS
			if ( c.CopFindLineOfFire( Target ) is { } spot && spot.Distance( c.WorldPosition ) > 32f )
				_spot = spot;
		} ) );
		AddTask( new CopMoveIfSpot( () => _spot, c ) );
		AddTask( new CombineFaceTask( Target ) );
	}

	private Vector3? _spot;
	public override void Configure() => _spot = null;
	public override bool MoveAndShoot => true;

	// COND_CAN_RANGE_ATTACK1 (once the rest is over), COND_ENEMY_DEAD
	protected override bool ShouldCancel() => TargetDead || Started > 0.3f && CanShoot && Combine.CanFireNow && Combine.RestOver;
}

/// <summary>Move to a spot decided by an earlier step, if there is one (running; facing the enemy on short moves).</summary>
public sealed class CopMoveIfSpot : CombineTask
{
	private readonly Func<Vector3?> _spot;
	private readonly CombineNpc _cop;
	private Vector3? _target;
	private Vector3 _lastSpot;
	private TimeSince _sinceProgress;
	private TimeSince _started;

	public CopMoveIfSpot( Func<Vector3?> spot, CombineNpc cop )
	{
		_spot = spot;
		_cop = cop;
	}

	protected override void OnStart()
	{
		_target = _spot();
		_started = 0;
		_sinceProgress = 0;
		_lastSpot = _cop.WorldPosition;
		if ( _target is not { } target ) return;

		var face = _cop.FaceWhileMoving( target );
		Npc.Navigation.MoveTo( target, 24f, face.IsValid() ? _cop.RunSpeed * 0.7f : _cop.RunSpeed, face );
	}

	protected override TaskStatus OnUpdate()
	{
		if ( _target is not { } target ) return TaskStatus.Success;
		if ( _cop.WorldPosition.WithZ( 0 ).Distance( target.WithZ( 0 ) ) < 32f ) return TaskStatus.Success;

		if ( _cop.WorldPosition.Distance( _lastSpot ) > 24f )
		{
			_lastSpot = _cop.WorldPosition;
			_sinceProgress = 0;
		}

		// Never set off, or stopped getting anywhere: give up like the soldier's move does
		if ( _started > 0.6f && !Npc.Navigation.MoveTarget.HasValue || _sinceProgress > 2.5f )
		{
			_cop.SinceMoveFailed = 0;
			return TaskStatus.Failed;
		}

		return TaskStatus.Running;
	}

	protected override void OnEnd() => Npc.Navigation.Stop();
}

/// <summary>SCHED_METROPOLICE_CHARGE: a pistol cop dashes 150 units at him, stops, faces him.</summary>
public sealed class CopChargeSchedule : CopSchedule
{
	protected override void Build()
	{
		var c = Combine;
		var to = TargetPosition;
		var dir = (to - c.WorldPosition).WithZ( 0 ).Normal;
		var spot = c.Scene.NavMesh.GetClosestPoint( c.WorldPosition + dir * 150f ) ?? c.WorldPosition + dir * 150f;
		AddTask( new CombineMoveTo( spot, 16f ) { Speed = c.RunSpeed } );
		AddTask( new CombineFaceTask( Target ) );
	}

	public override bool MoveAndShoot => true;

	// COND_NEW_ENEMY, ENEMY_DEAD, LOST_ENEMY, CAN_MELEE_ATTACK1, HEAR_DANGER, PLAYER_TOO_CLOSE
	protected override bool ShouldCancel() => TargetDead || NewEnemyInSight;
}

/// <summary>
/// SCHED_METROPOLICE_CHASE_ENEMY: run after him ("moving into position" - a flank if it's the
/// long way round), then face him. Gets there and he's gone: lost him.
/// </summary>
public sealed class CopChaseSchedule : CopSchedule
{
	protected override void Build()
	{
		var c = Combine;
		var to = Target.IsValid() && c.Squad?.Enemies.TryGetValue( Target, out var record ) == true ? record.Position : Threat;

		// No way there at all (he's on a roof, the navmesh doesn't reach): HL2's COND_ENEMY_UNREACHABLE - he's lost.
		// Judged to the ground under him: someone in the air (knocked flying by the baton) is no less reachable
		var ground = c.Scene.NavMesh?.GetClosestPoint( to ) is { } below && below.WithZ( 0 ).Distance( to.WithZ( 0 ) ) < 64f ? below : to;
		var path = c.Scene.NavMesh?.CalculatePath( new Sandbox.Navigation.CalculatePathRequest { Start = c.WorldPosition, Target = ground } );
		var points = path?.Points;
		if ( points is null || points.Count == 0 )
		{
			if ( CombineSquad.Debug ) Log.Info( $"[chase] {Npc.GameObject.Id.ToString()[..4]} no path: from {c.WorldPosition} to {to} (ground {ground}), status {path?.Status}, target at {(Target.IsValid() ? Target.WorldPosition.ToString() : "-")}" );
			AddTask( new CombineFaceTask( to ) );
			AddTask( new CombineCallTask( () => c.CopLostAt( Target ) ) );
			return;
		}

		var length = 0f;
		for ( var i = 1; i < points.Count; i++ ) length += points[i].Position.Distance( points[i - 1].Position );

		AddTask( new CopSayTask( () => MetrocopVoice.MoveIntoPosition( c, Target, length ) ) );
		AddTask( new CopPursueTask( () => TargetPosition, Target ) );
		AddTask( new CombineCallTask( () =>
		{
			if ( !SeesTarget ) c.CopLostAt( Target );
		} ) );
	}

	// COND_NEW_ENEMY, ENEMY_DEAD, CAN_RANGE_ATTACK1, CAN_MELEE_ATTACK1, TOO_CLOSE, TASK_FAILED, LOST_ENEMY, HEAR_DANGER.
	// The baton breaks off for the swing at once, still running
	protected override bool ShouldCancel() => TargetDead || NewEnemyInSight
		|| CanMelee && Combine.SwingReady || Started > 0.3f && CanShoot && Combine.RestOver;
}

/// <summary>
/// HL2's chase (TASK_GET_CHASE_PATH_TO_ENEMY, TASK_RUN_PATH): the path follows the enemy as he moves.
/// Not a run to where he stood when it began, a stop there, and a fresh start from standing - that
/// was the stop-and-go. It also leaves the legs running when it ends, so the swing that cuts it
/// short starts on the move.
/// </summary>
public sealed class CopPursueTask : CombineTask
{
	private const float Arrive = 40f;
	private const float StuckTime = 2.5f;

	private readonly Func<Vector3> _goal;
	private Vector3 _sent;
	private TimeSince _sinceSent;
	private TimeSince _sinceStart;
	private Vector3 _lastSpot;
	private TimeSince _sinceProgress;

	public CopPursueTask( Func<Vector3> goal, GameObject target = null )
	{
		_goal = goal;
		_target = target;
	}

	protected override void OnStart()
	{
		_sinceStart = 0;
		_lastSpot = Npc.WorldPosition;
		_sinceProgress = 0;
		Send( _goal() );
	}

	private void Send( Vector3 goal )
	{
		_sent = goal;
		_sinceSent = 0;

		// The last stretch aimed a little past him, or the agent brakes for its stop and never closes
		// it on someone backing away. Always at his own run (HL2's: the speed of his run animation)
		var aim = goal;
		var toGoal = (goal - Npc.WorldPosition).WithZ( 0 );
		if ( Combine.HasBaton && toGoal.Length < CombineNpc.RunThroughRange && toGoal.Length > 1f )
			aim = Npc.Scene.NavMesh?.GetClosestPoint( goal + toGoal.Normal * 64f ) ?? goal;

		Npc.Navigation.MoveTo( aim, Arrive * 0.5f, Combine.RunSpeed, Close( goal ) ? _target : null );
	}

	private readonly GameObject _target;

	/// <summary>Right up close he keeps his front to the target, not to the way his feet go - so someone
	/// circling him is never behind him when the swing is ready.</summary>
	private bool Close( Vector3 goal ) => _target.IsValid() && (goal - Npc.WorldPosition).WithZ( 0 ).Length < 150f;

	protected override TaskStatus OnUpdate()
	{
		var goal = _goal();
		if ( _sinceSent > 0.2f && goal.Distance( _sent ) > 12f || _sinceSent > 0.5f ) Send( goal );
		if ( Close( goal ) ) Combine.TurnTowards( _target.WorldPosition, CombineNpc.CloseTurnSpeed );

		if ( (goal - Npc.WorldPosition).WithZ( 0 ).Length < Arrive && MathF.Abs( goal.z - Npc.WorldPosition.z ) < 60f )
			return TaskStatus.Success;

		// The path takes a moment to come in (CombineMoveTo's SetOffTime)
		if ( _sinceStart > 0.6f && !Npc.Navigation.IsMoving && Npc.Navigation.GetStatus() == TaskStatus.Failed )
		{
			Combine.SinceMoveFailed = 0;
			return TaskStatus.Failed;
		}

		if ( Npc.WorldPosition.Distance( _lastSpot ) > 24f )
		{
			_lastSpot = Npc.WorldPosition;
			_sinceProgress = 0;
		}
		else if ( _sinceProgress > StuckTime )
		{
			Combine.SinceMoveStuck = 0;
			Combine.SinceMoveFailed = 0;
			return TaskStatus.Failed;
		}

		return TaskStatus.Running;
	}
}

/// <summary>SCHED_METROPOLICE_ESTABLISH_LINE_OF_FIRE: run to a spot that sees him, then face him.</summary>
public sealed class CopEstablishLineOfFireSchedule : CopSchedule
{
	private Vector3 _spot;
	public CopEstablishLineOfFireSchedule At( Vector3 spot ) { _spot = spot; return this; }

	protected override void Build()
	{
		var c = Combine;
		AddTask( new CombineFaceTask( Threat ) );
		AddTask( new CopSayTask( () => MetrocopVoice.MoveIntoPosition( c, Target, c.WorldPosition.Distance( _spot ) ) ) );
		AddTask( c.CombatMove( _spot, 48f ) );
		AddTask( new CombineFaceTask( Target ) );
		AddTask( new CopWaitFaceTask( Target, Threat, 1f ) );
	}

	public override bool MoveAndShoot => true;

	// COND_NEW_ENEMY, ENEMY_DEAD, CAN_RANGE_ATTACK1, CAN_MELEE_ATTACK1, HEAR_DANGER, HEAVY_DAMAGE
	protected override bool ShouldCancel() => TargetDead || NewEnemyInSight || HurtSinceStart
		|| Started > 0.3f && (CanShoot && Combine.RestOver || CanMelee);
}

/// <summary>
/// SCHED_TAKE_COVER_FROM_ENEMY (no breaking off for damage - he's running from it) and
/// SCHED_HIDE_AND_RELOAD (shooting back as he goes, then reload behind it).
/// </summary>
public sealed class CopTakeCoverSchedule : CopSchedule
{
	private Vector3 _cover;
	private bool _reload;

	public CopTakeCoverSchedule At( Vector3 cover ) { _cover = cover; return this; }
	public CopTakeCoverSchedule Reloading() { _reload = true; return this; }
	public override void Configure() => _reload = false;

	protected override void Build()
	{
		var c = Combine;
		c.Squad?.ClaimCover( c, _cover );
		AddTask( c.CombatMove( _cover, 16f ) );
		AddTask( new CombineFaceTask( Threat ) );
		if ( _reload )
		{
			AddTask( new CombineCrouchTask( true ) );
			AddTask( new CombineReloadTask() );
		}
		else AddTask( new CopWaitFaceTask( Target, Threat, Game.Random.Float( 1f, 2f ) ) );
	}

	public override bool MoveAndShoot => _reload;

	// HIDE_AND_RELOAD: COND_CAN_MELEE_ATTACK1, HEAVY_DAMAGE (once in cover the reload itself holds), HEAR_DANGER
	protected override bool ShouldCancel() => _reload && !Combine.IsReloading && HurtSinceStart && Started > 0.5f;
}

/// <summary>SCHED_MELEE_ATTACK1 with the baton (or SCHED_METROPOLICE_SMASH_PROP's swing).</summary>
public sealed class CopMeleeSchedule : CopSchedule
{
	protected override void Build() => AddTask( new CopSwingTask( Target ) );

	protected override bool ShouldCancel() => TargetDead && Started < 0.2f;
}

/// <summary>
/// The baton swing (frame 13 of 35 lands it). He goes straight into it - no stopping to turn on
/// the spot first - and steps in with it, turning onto the target as he goes, so stepping back
/// isn't enough to dodge it. Once it has landed he doesn't stand out the whole follow-through
/// if the enemy is out of reach: he's straight after him.
/// </summary>
public sealed class CopSwingTask : CombineTask
{
	private const float HitAt = 13f / 35f;

	private readonly GameObject _target;
	private float _duration;
	private TimeSince _started;
	private bool _hit;

	public CopSwingTask( GameObject target ) => _target = target;

	protected override void OnStart()
	{
		_duration = Combine.PlayAction( "melee" );
		_started = 0;
		_hit = false;
	}

	protected override TaskStatus OnUpdate()
	{
		var c = Combine;
		var target = _target.IsValid() ? _target.WorldPosition : c.WorldPosition + c.WorldRotation.Forward * 40f;
		var flat = (target - c.WorldPosition).WithZ( 0 );

		if ( !_hit )
		{
			c.TurnTowards( target, 720f );

			// Step in until close enough that the swing lands
			if ( flat.Length > 34f ) Npc.Navigation.MoveTo( target, 30f, c.RunSpeed, _target );
			else Npc.Navigation.Stop();

			if ( _started < _duration * HitAt ) return TaskStatus.Running;

			_hit = true;
			Npc.Navigation.Stop();
			c.BatonHit( _target, stepped: true );
		}

		// Knocked back or stepped away: after him now, not at the end of the follow-through
		if ( _started > _duration * HitAt + 0.25f && flat.Length > 80f ) return TaskStatus.Success;

		return _started > _duration ? TaskStatus.Success : TaskStatus.Running;
	}

	protected override void OnEnd()
	{
		if ( !_hit ) Npc.Navigation.Stop();
	}
}

/// <summary>SCHED_METROPOLICE_SMASH_PROP: walk up to the breakable thing in his way and swat it.</summary>
public sealed class CopSmashPropSchedule : CopSchedule
{
	private GameObject _prop;
	public CopSmashPropSchedule Aim( GameObject prop ) { _prop = prop; return this; }

	protected override void Build()
	{
		var c = Combine;
		if ( !_prop.IsValid() ) return;
		AddTask( new CombineMoveTo( _prop.WorldPosition, 50f ) );
		AddTask( new CombineFaceTask( _prop ) );
		AddTask( new CopAnimTask( "melee", _prop, (13f / 35f, () => c.BatonHit( _prop )) ) );
	}

	protected override bool ShouldCancel() => !_prop.IsValid() || NewEnemyInSight || TargetDead;
}

/// <summary>SCHED_SHOOT_ENEMY_COVER: he's behind something that breaks - shoot that.</summary>
public sealed class CopShootCoverSchedule : CopSchedule
{
	private GameObject _occluder;
	public CopShootCoverSchedule Aim( GameObject occluder ) { _occluder = occluder; return this; }

	protected override void Build()
	{
		Npc.Navigation.Stop();
		if ( !_occluder.IsValid() ) return;
		AddTask( new CombineFaceTask( _occluder ) );
		AddTask( new CopBurstTask( Target, _occluder ) );
	}

	protected override bool ShouldCancel() => !_occluder.IsValid() || TargetDead || HurtSinceStart && Started > 0.1f || CanShoot;
}

/// <summary>SCHED_COMBAT_FACE: stand angry, facing him.</summary>
public sealed class CopCombatFaceSchedule : CopSchedule
{
	private bool _waitingToSwing;

	/// <summary>A baton man squaring up while his next swing comes round: he won't stand there if the enemy backs off.</summary>
	public CopCombatFaceSchedule WaitingToSwing() { _waitingToSwing = true; return this; }
	public override void Configure() => _waitingToSwing = false;

	protected override void Build()
	{
		Npc.Navigation.Stop();
		AddTask( new CombineFaceTask( TargetPosition ) );
		AddTask( new CopWaitFaceTask( Target, Threat, Game.Random.Float( 0.5f, 1.5f ) ) );
	}

	protected override bool ShouldCancel() => TargetDead || CanMelee && Combine.SwingReady
		|| Started > 0.3f && CanShoot && Combine.RestOver
		|| _waitingToSwing && Target.IsValid() && (Target.WorldPosition - Npc.WorldPosition).WithZ( 0 ).Length > 70f;
}

/// <summary>SCHED_METROPOLICE_DRAW_PISTOL: out of the holster (shown at frame 11), face him a moment.</summary>
public sealed class CopDrawPistolSchedule : CopSchedule, ICopUninterruptible
{
	protected override void Build()
	{
		var c = Combine;
		Npc.Navigation.Stop();
		AddTask( new CombineFaceTask( TargetPosition ) );
		AddTask( new CopAnimTask( "draw_pistol", Target, (11f / 30f, () => c.WeaponDrawn = true) ) );
		AddTask( new CopWaitFaceTask( Target, Threat, 0.1f ) );
	}
}

/// <summary>SCHED_METROPOLICE_DEPLOY_MANHACK: "deploying viscerator", off the belt (frame 10), and away (frame 33).</summary>
public sealed class CopDeployManhackSchedule : CopSchedule, ICopUninterruptible
{
	protected override void Build()
	{
		var c = Combine;
		Npc.Navigation.Stop();
		AddTask( new CopSayTask( () => MetrocopVoice.Speak( c, "METROPOLICE_DEPLOY_MANHACK" ) ) );
		AddTask( new CopAnimTask( "deploy_manhack", Target,
			(10f / 30f, c.StartDeployManhack),
			(33f / 30f, c.ReleaseManhack) ) );
	}
}

/// <summary>SCHED_METROPOLICE_ACTIVATE_BATON / _DEACTIVATE_BATON (the switch at frame 7).</summary>
public sealed class CopBatonSchedule : CopSchedule, ICopUninterruptible
{
	private bool _on;
	public CopBatonSchedule Switch( bool on ) { _on = on; return this; }

	protected override void Build()
	{
		var c = Combine;
		Npc.Navigation.Stop();
		if ( _on && Target.IsValid() ) AddTask( new CombineFaceTask( Target ) );
		AddTask( new CopSayTask( () => MetrocopVoice.Speak( c, _on ? "METROPOLICE_ACTIVATE_BATON" : "METROPOLICE_DEACTIVATE_BATON" ) ) );
		var on = _on;
		AddTask( new CopAnimTask( on ? "baton_on" : "baton_off", Target, (7f / 30f, () => c.BatonSwitched( on )) ) );
	}

	protected override bool ShouldCancel() => Combine.BatonOn == _on && Started > 0.5f;
}

/// <summary>SCHED_METROPOLICE_BURNING_STAND: on fire, he burns where he stands and dies.</summary>
public sealed class CopBurningSchedule : CopSchedule, ICopUninterruptible
{
	public override int Priority => SchedulePriority.Survival;

	protected override void Build()
	{
		var c = Combine;
		Npc.Navigation.Stop();
		AddTask( new CopAnimTask( "on_fire", null, (1.5f, () => c.BurnToDeath()) ) );
		AddTask( new Wait( 1f ) );
	}
}

/// <summary>SCHED_FLINCH_PHYSICS: something thrown at him - he cowers.</summary>
public sealed class CopPhysicsFlinchSchedule : CopSchedule
{
	protected override void Build()
	{
		Npc.Navigation.Stop();
		AddTask( new CombineActionTask( "flinch_physics" ) );
	}
}

// ==================================================================== not fighting

/// <summary>Standing about (SCHED_IDLE_STAND / SCHED_ALERT_STAND): looks round now and then; anything happening ends it.</summary>
public sealed class CopIdleSchedule : CombineSchedule
{
	public override int Priority => SchedulePriority.Idle;

	private TimeSince _started;
	private Vector3? _heard;

	protected override void OnStart()
	{
		_started = 0;
		_heard = Combine.CopNoise;
		Combine.SetRelaxed( true );
		Npc.Navigation.Stop();
		if ( Combine.Alert ) AddTask( new CombineLookAroundTask( Game.Random.Float( 3f, 6f ) ) );
		else AddTask( new Wait( Game.Random.Float( 3f, 6f ) ) );
	}

	protected override bool ShouldCancel()
	{
		var c = Combine;
		// No squad yet (the first moment after spawning): nothing to choose from that he can act on
		if ( c.Squad is null ) return c.SinceCopHurt < (float)_started;

		// Only a new noise: one still hanging about from before he started (gunfire fades slowly) is old news
		var noise = c.CopNoise;
		var newNoise = noise is { } n && (_heard is not { } h || h.Distance( n ) > 64f);
		return c.CopBestVisible() is { } seen && c.PlayerIsCriminal( seen ) || c.SinceCopHurt < (float)_started
			|| newNoise || c.PlayerTooClose;
	}
}

/// <summary>SCHED_METROPOLICE_INVESTIGATE_SOUND: go where it can be seen from, wait 5 s, walk back.</summary>
public sealed class CopInvestigateSchedule : CombineSchedule
{
	public Vector3 Spot { get; set; }
	public override int Priority => SchedulePriority.Alert;

	private TimeSince _started;

	protected override void OnStart()
	{
		var c = Combine;
		_started = 0;
		c.SetRelaxed( false );
		var home = c.WorldPosition;
		MetrocopVoice.Speak( c, "METROPOLICE_HEARD_SOMETHING", MetrocopVoice.Medium );

		var look = FindLookSpot( c, Spot ) ?? Spot;
		AddTask( new CombineFaceTask( Spot ) );
		AddTask( new CombineMoveTo( look, 32f ) { Speed = c.RunSpeed } );
		AddTask( new CombineFaceTask( Spot ) );
		AddTask( new CombineLookAroundTask( 5f ) );
		AddTask( new CombineMoveTo( home, 24f ) { Speed = c.WalkSpeed } );
	}

	private static Vector3? FindLookSpot( CombineNpc c, Vector3 spot )
	{
		// The nearest reachable point within gun range that sees the spot
		for ( var radius = 0f; radius <= 768f; radius += 192f )
		{
			for ( var i = 0; i < (radius == 0f ? 1 : 8); i++ )
			{
				var at = spot + Rotation.FromYaw( 45f * i ).Forward * radius;
				if ( c.Scene.NavMesh.GetClosestPoint( at, 96f ) is not { } p ) continue;
				var tr = c.Scene.Trace.Ray( p + Vector3.Up * 60f, spot + Vector3.Up * 32f ).WithoutTags( "player", "npc", "trigger", "ragdoll", "playercontroller" ).Run();
				if ( !tr.Hit && c.CanWalkTo( p ) ) return p;
			}
		}

		return null;
	}

	// COND_NEW_ENEMY, SEE_FEAR, SEE_ENEMY, LIGHT/HEAVY_DAMAGE, HEAR_DANGER
	protected override bool ShouldCancel()
	{
		var c = Combine;
		return c.CopBestVisible() is { } seen && c.PlayerIsCriminal( seen ) || c.SinceCopHurt < (float)_started;
	}
}

/// <summary>SCHED_METROPOLICE_ALERT_FACE_BESTSOUND: "heard something", and turn to it.</summary>
public sealed class CopAlertFaceSchedule : CombineSchedule
{
	public Vector3 Spot { get; set; }
	public override int Priority => SchedulePriority.Alert;

	protected override void OnStart()
	{
		Npc.Navigation.Stop();
		MetrocopVoice.Speak( Combine, "METROPOLICE_HEARD_SOMETHING", MetrocopVoice.Medium );
		AddTask( new CombineFaceTask( Spot ) );
		AddTask( new Wait( Game.Random.Float( 1f, 2f ) ) );
	}

	protected override bool ShouldCancel() => Combine.CopBestVisible() is { } seen && Combine.PlayerIsCriminal( seen );
}

// ==================================================================== the calm cop

/// <summary>SCHED_METROPOLICE_SHOVE: "back up!" and a push - or, warned three times, the baton.</summary>
public sealed class CopShoveSchedule : CopSchedule, ICopUninterruptible
{
	protected override void Build()
	{
		var c = Combine;
		Npc.Navigation.Stop();
		AddTask( new CombineFaceTask( Target ) );
		if ( c.ShouldActivateBaton && !c.BatonOn && c.HasBaton )
			AddTask( new CopAnimTask( "baton_on", Target, (7f / 30f, () => c.BatonSwitched( true )) ) );

		// NPC_TranslateActivity: ACT_PUSH_PLAYER becomes a real swing after the third warning
		if ( c.Warnings >= CombineNpc.MaxWarnings && c.HasBaton )
			AddTask( new CopAnimTask( "melee", Target, (13f / 35f, () => c.BatonHit( Target )) ) );
		else
			AddTask( new CopAnimTask( "push_player", Target, (11f / 30f, () => c.Shove( Target )) ) );
	}
}

/// <summary>SCHED_TARGET_FACE: he's been warned - keep an eye on him.</summary>
public sealed class CopTargetFaceSchedule : CopSchedule
{
	public override int Priority => SchedulePriority.Alert;

	protected override void Build()
	{
		Npc.Navigation.Stop();
		AddTask( new CopWaitFaceTask( Target, Threat, Game.Random.Float( 1f, 2f ) ) );
	}

	protected override bool ShouldCancel() => Combine.PlayerTooClose || Combine.CopBestVisible() is { } seen && Combine.PlayerIsCriminal( seen );
}

/// <summary>SCHED_METROPOLICE_RETURN_TO_PRECHASE: back to where he stood, facing the way he faced.</summary>
public sealed class CopReturnToPreChaseSchedule : CombineSchedule
{
	public Vector3 Spot { get; set; }
	public float Yaw { get; set; }
	public override int Priority => SchedulePriority.Alert;

	protected override void OnStart()
	{
		var c = Combine;
		c.SetRelaxed( true );
		AddTask( new Wait( Game.Random.Float( 0f, 1f ) ) );
		AddTask( new CombineMoveTo( Spot, 12f ) { Speed = c.WalkSpeed } );
		AddTask( new CombineFaceTask( Spot + Rotation.FromYaw( Yaw ).Forward * 100f ) );
		AddTask( new CombineCallTask( c.ClearPreChase ) );
	}

	protected override bool ShouldCancel() => Combine.PlayerTooClose || Combine.CopBestVisible() is { } seen && Combine.PlayerIsCriminal( seen );
}

// ==================================================================== arrest

/// <summary>
/// SCHED_METROPOLICE_WARN_AND_ARREST_ENEMY, for the first to see him: "Freeze!", "he's over here!",
/// wait for the others to draw a bead (5 s at most) - he steps 100 units away: "he's running!" -
/// "take him down!", and the squad opens fire.
/// </summary>
public sealed class CopWarnAndArrestSchedule : CopSchedule
{
	private Vector3 _stood;

	protected override void Build()
	{
		var c = Combine;
		Npc.Navigation.Stop();
		AddTask( new CombineFaceTask( Target ) );
		AddTask( new CopSayTask( () => MetrocopVoice.Speak( c, "METROPOLICE_FREEZE", MetrocopVoice.Medium ) ) );
		AddTask( new CopWaitFaceTask( Target, Threat, 0.5f ) );
		AddTask( new CombineCallTask( () => _stood = Target.IsValid() ? Target.WorldPosition : Threat ) );
		AddTask( new CopWaitFaceTask( Target, Threat, 1f ) );
		AddTask( new CopSayTask( () => MetrocopVoice.Speak( c, "METROPOLICE_OVER_HERE", MetrocopVoice.Medium ) ) );
		AddTask( new CopArrestWaitTask( this, 5f, lead: true ) );
		AddTask( new CopWaitFaceTask( Target, Threat, 2f ) );
		AddTask( new CopSayTask( () => MetrocopVoice.Speak( c, "METROPOLICE_TAKE_HIM_DOWN", MetrocopVoice.High ) ) );
		AddTask( new CopWaitFaceTask( Target, Threat, 1.5f ) );
		AddTask( new CombineCallTask( c.EnemyResistingArrest ) );
	}

	public bool Fled => Target.IsValid() && Target.WorldPosition.Distance( _stood ) > 100f && _stood != default;
	public override void Configure() => _stood = default;

	protected override bool ShouldCancel() => TargetDead || NewEnemyInSight || HurtSinceStart || !Combine.ArrestEnemy;

	protected override void OnEnd()
	{
		if ( Fled )
		{
			MetrocopVoice.Speak( Combine, "METROPOLICE_HES_RUNNING", MetrocopVoice.High );
			Combine.EnemyResistingArrest();
		}
	}
}

/// <summary>SCHED_METROPOLICE_ARREST_ENEMY, for the rest: get a line on him, "in position", hold him there (30 s).</summary>
public sealed class CopArrestSchedule : CopSchedule
{
	protected override void Build()
	{
		var c = Combine;
		if ( !SeesTarget && c.CopFindLineOfFire( Target ) is { } spot ) AddTask( c.CombatMove( spot, 24f ) );
		AddTask( new CombineFaceTask( Target ) );
		AddTask( new CopSayTask( () => MetrocopVoice.Speak( c, "METROPOLICE_ARREST_IN_POS", MetrocopVoice.Medium ) ) );
		AddTask( new CopArrestWaitTask( this, 30f, lead: false ) );
	}

	protected override bool ShouldCancel() => TargetDead || NewEnemyInSight || HurtSinceStart || !Combine.ArrestEnemy;
}

/// <summary>Hold him at gunpoint; the lead cop ends it once everyone is on him, or when he runs.</summary>
public sealed class CopArrestWaitTask : CombineTask
{
	private readonly CopSchedule _schedule;
	private readonly float _seconds;
	private readonly bool _lead;
	private TimeSince _started;

	public CopArrestWaitTask( CopSchedule schedule, float seconds, bool lead )
	{
		_schedule = schedule;
		_seconds = seconds;
		_lead = lead;
	}

	protected override void OnStart()
	{
		Npc.Navigation.Stop();
		_started = 0;
	}

	protected override TaskStatus OnUpdate()
	{
		var target = _schedule.Target;
		if ( !target.IsValid() || _started > _seconds ) return TaskStatus.Success;
		Combine.TurnTowards( target.WorldPosition, 150f );

		if ( _lead )
		{
			if ( _schedule is CopWarnAndArrestSchedule warn && warn.Fled ) return TaskStatus.Failed;
			var squad = Combine.Squad;
			if ( squad is not null && squad.CopArresting >= squad.Alive.Count() ) return TaskStatus.Success;
		}

		return TaskStatus.Running;
	}
}
