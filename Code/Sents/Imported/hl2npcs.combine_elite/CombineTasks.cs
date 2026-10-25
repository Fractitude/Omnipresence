using System;
using System.Linq;
using Sandbox;
using Sandbox.Npcs;

namespace Hl2CombineElite;

public abstract class CombineTask : TaskBase
{
	protected CombineNpc Combine => Npc as CombineNpc;
}

/// <summary>
/// The sandbox move, but it gives up once he stops getting anywhere - pressed against a corner,
/// or heading for a spot the navmesh can't actually reach. Otherwise he'd walk on the spot forever.
/// </summary>
public sealed class CombineMoveTo : Sandbox.Npcs.Tasks.MoveTo
{
	private const float StuckTime = 2.5f;

	private Vector3 _lastSpot;
	private TimeSince _sinceProgress;

	public CombineMoveTo( Vector3 target, float stopDistance = 10f ) : base( target, stopDistance ) { }

	/// <summary>
	/// The agent can take a few frames to set off - on a big map like flatgrass the path isn't there
	/// the moment it's asked for. Until then "not navigating" isn't a failed move, or he'd re-plan
	/// every frame and never go anywhere.
	/// </summary>
	private const float SetOffTime = 0.6f;

	private TimeSince _sinceStart;
	private bool _waited;

	protected override void OnStart()
	{
		base.OnStart();
		_lastSpot = Npc.WorldPosition;
		_sinceProgress = 0;
		_sinceStart = 0;
		_waited = false;
	}

	protected override TaskStatus OnUpdate()
	{
		var status = base.OnUpdate();
		if ( status == TaskStatus.Failed && _sinceStart < SetOffTime && Npc.Navigation.MoveTarget.HasValue )
		{
			_waited = true;
			return TaskStatus.Running;
		}

		if ( _waited && status == TaskStatus.Running )
		{
			_waited = false;
			if ( CombineSquad.Debug ) Log.Info( $"[move] {Npc.GameObject.Id.ToString()[..4]} set off after {(float)_sinceStart:0.00} s" );
		}

		if ( status == TaskStatus.Failed && Npc is CombineNpc failed )
		{
			failed.SinceMoveFailed = 0;
			if ( CombineSquad.Debug )
				Log.Info( $"[move] {failed.GameObject.Id.ToString()[..4]} failed after {(float)_sinceStart:0.00} s, {(TargetPosition is { } t ? t.Distance( Npc.WorldPosition ) : 0f):0} short" );
		}
		if ( status != TaskStatus.Running ) return status;

		if ( Npc.WorldPosition.Distance( _lastSpot ) > 24f )
		{
			_lastSpot = Npc.WorldPosition;
			_sinceProgress = 0;
			return status;
		}

		if ( _sinceProgress < StuckTime ) return status;

		if ( Npc is CombineNpc c )
		{
			c.SinceMoveStuck = 0;
			c.SinceMoveFailed = 0;
			if ( CombineSquad.Debug && TargetPosition is { } target )
				Log.Info( $"[stuck] {c.GameObject.Id.ToString()[..4]} gave up moving, {target.Distance( c.WorldPosition ):0} short" );
		}

		return TaskStatus.Failed;
	}
}

public sealed class CombineCrouchTask : CombineTask
{
	private readonly bool _crouch;
	private TimeSince _started;
	private bool _changed;

	public CombineCrouchTask( bool crouch ) => _crouch = crouch;

	// Just knelt or just stood up: stay like that a moment rather than bob straight back
	private const float MinHold = 0.8f;

	protected override void OnStart()
	{
		_started = 0;
		_changed = Combine.Crouched != _crouch;
	}

	protected override TaskStatus OnUpdate()
	{
		if ( !_changed ) return TaskStatus.Success;

		if ( Combine.Crouched != _crouch )
		{
			if ( Combine.SincePostureChange < MinHold )
			{
				_started = 0;
				return TaskStatus.Running;
			}

			// Come to a stop before kneeling
			if ( _crouch )
			{
				var speed = Npc.Animation.MoveVelocity.WithZ( 0 ).Length;
				if ( speed > 50f && _started < 0.6f ) return TaskStatus.Running;
			}

			Combine.Crouched = _crouch;
			_started = 0;
		}

		// Standing up is quick, kneeling takes a moment to settle
		return !_crouch || _started > 0.3f ? TaskStatus.Success : TaskStatus.Running;
	}
}

public sealed class CombineFaceTask : CombineTask
{
	private readonly GameObject _target;
	private readonly Vector3 _position;
	private TimeSince _started;

	public CombineFaceTask( GameObject target ) => _target = target;
	public CombineFaceTask( Vector3 position ) => _position = position;

	private Vector3 Point => _target.IsValid() ? _target.WorldPosition : _position;

	protected override void OnStart() => _started = 0;

	protected override TaskStatus OnUpdate()
	{
		Combine.TurnTowards( Point, 220f );
		return Combine.IsFacing( Point, 10f ) || _started > 1.5f ? TaskStatus.Success : TaskStatus.Running;
	}
}

/// <summary>Fire a burst. Fails if a friend steps into the line of fire or the shot is blocked.</summary>
public sealed class CombineShootTask : CombineTask
{
	private readonly GameObject _target;
	private int _shots;
	private TimeUntil _timeout;
	private TimeSince _blocked;
	private TimeSince _sinceShot;
	private bool _isBlocked;

	public CombineShootTask( GameObject target, int shots )
	{
		_target = target;
		_shots = shots;
	}

	protected override void OnStart()
	{
		_timeout = 5f;
		_sinceShot = 0;
		_isBlocked = false;
		Npc.Navigation.Stop();
	}

	protected override TaskStatus OnUpdate()
	{
		var c = Combine;
		if ( CombineNpc.IsDeadTarget( _target ) ) return TaskStatus.Success;
		if ( _timeout ) return TaskStatus.Failed;

		var aim = CombineNpc.AimPoint( _target );
		c.TurnTowards( aim, 240f );
		if ( !c.IsFacing( aim, 12f ) )
		{
			if ( CombineSquad.Debug && _sinceShot > 2f )
			{
				_sinceShot = 0;
				Log.Info( $"[fire] {Npc.GameObject.Id.ToString()[..4]} not facing target" );
			}
			return TaskStatus.Running;
		}
		if ( c.Clip <= 0 ) return TaskStatus.Success;

		var lineOfFire = c.CheckLineOfFire( aim, _target );
		if ( lineOfFire == LineOfFire.Blocked )
		{
			var head = CombineNpc.EyesOf( _target ) - Vector3.Up * 2f;
			if ( c.CheckLineOfFire( head, _target ) == LineOfFire.Clear )
			{
				aim = head;
				lineOfFire = LineOfFire.Clear;
			}
		}

		switch ( lineOfFire )
		{
			case LineOfFire.Friendly:
				if ( CombineSquad.Debug ) Log.Info( $"[fire] {Npc.GameObject.Id.ToString()[..4]} holds fire: friend in the way ({c.LastLineOfFireHit})" );
				return TaskStatus.Failed;

			case LineOfFire.Blocked:
				if ( CombineSquad.Debug ) Log.Info( $"[fire] {Npc.GameObject.Id.ToString()[..4]} blocked by {c.LastLineOfFireHit}" );
				// Kneeling behind something - stand up to shoot over it, give up if that doesn't help
				if ( !_isBlocked ) { _isBlocked = true; _blocked = 0; }
				if ( c.Crouched )
				{
					c.Crouched = false;
					c.StandToShoot();
				}
				if ( _blocked > 0.6f )
				{
					c.ShotBlocked();
					return TaskStatus.Failed;
				}
				return TaskStatus.Running;
		}

		_isBlocked = false;
		if ( c.TryShootAt( aim, 1f ) )
		{
			_shots--;
			_sinceShot = 0;
			CombineOverwatch.For( Npc.Scene )?.ComradeFiring( c, _target );
		}
		else if ( CombineSquad.Debug && _sinceShot > 2f )
		{
			_sinceShot = 0;
			Log.Info( $"[fire] {Npc.GameObject.Id.ToString()[..4]} not firing: clip {c.Clip}, gun {c.Gun.Name}" );
		}

		return _shots <= 0 ? TaskStatus.Success : TaskStatus.Running;
	}
}

/// <summary>Runs a bit of code as a step of a schedule.</summary>
public sealed class CombineCallTask : CombineTask
{
	private readonly Action _action;

	public CombineCallTask( Action action ) => _action = action;

	protected override TaskStatus OnUpdate()
	{
		_action();
		return TaskStatus.Success;
	}
}

/// <summary>Play a one-shot animation (hand signal etc.) standing still.</summary>
public sealed class CombineActionTask : CombineTask
{
	private readonly string _name;
	private float _duration;
	private TimeSince _started;

	public CombineActionTask( string name ) => _name = name;

	protected override void OnStart()
	{
		Npc.Navigation.Stop();
		_duration = Combine.PlayAction( _name );
		_started = 0;
	}

	protected override TaskStatus OnUpdate() => _started > _duration ? TaskStatus.Success : TaskStatus.Running;
}

public sealed class CombineReloadTask : CombineTask
{
	private string _animation;
	private float _duration;
	private TimeSince _started;
	private bool _done;

	protected override void OnStart()
	{
		var c = Combine;
		Npc.Navigation.Stop();
		_done = false;
		_started = 0;
		_animation = c.Crouched ? "reload_low" : "reload";
		_duration = c.ReloadDuration( c.PlayAction( _animation ) );
		c.SetReloading( _duration + 0.1f );
		c.PlayReloadSound();

		// "Cover me!" - a squadmate starts suppressing once he hears it
		if ( c.Squad is not null && c.Enemy.IsValid() )
			c.Squad.RequestCover( c );
	}

	protected override TaskStatus OnUpdate()
	{
		// Keep facing the enemy while the hands are busy - he tracks a player walking round him
		var c = Combine;
		var seen = c.Senses.GetBestTarget();
		if ( seen.IsValid() ) c.TurnTowards( seen.WorldPosition, 150f );
		else if ( c.Enemy.IsValid() ) c.TurnTowards( c.EnemyPosition, 150f );

		if ( !_done && _started > _duration * 0.8f )
		{
			_done = true;
			Combine.Reloaded();
		}

		return _started > _duration ? TaskStatus.Success : TaskStatus.Running;
	}

	// Broken off before the end (an order, a grenade): stop the hands too, or he slides off reloading
	protected override void OnEnd()
	{
		Combine.SetReloading( 0f );
		if ( _started < _duration ) Combine.StopAction( _animation );
	}
}

public sealed class CombineThrowTask : CombineTask
{
	// Fraction of grenThrow where the grenade leaves the hand
	private const float ReleaseAt = 0.45f;

	private readonly Vector3 _target;
	private readonly bool _flat;
	private readonly Action _onThrown;
	private float _duration;
	private TimeSince _started;
	private bool _thrown;

	public CombineThrowTask( Vector3 target, bool flat = false, Action onThrown = null )
	{
		_target = target;
		_flat = flat;
		_onThrown = onThrown;
	}

	protected override void OnStart()
	{
		Npc.Navigation.Stop();
		_thrown = false;
		_started = 0;
		_duration = Combine.PlayAction( "grenade_throw" );
		Combine.Say( CombineVoice.Grenade, force: true );
	}

	protected override TaskStatus OnUpdate()
	{
		if ( !_thrown && _started > _duration * ReleaseAt )
		{
			_thrown = true;
			Combine.ThrowGrenade( _target, _flat );
			_onThrown?.Invoke();
		}

		return _started > _duration ? TaskStatus.Success : TaskStatus.Running;
	}
}

/// <summary>Stand and sweep the area with the gun.</summary>
public sealed class CombineLookAroundTask : CombineTask
{
	private readonly float _duration;
	private TimeSince _started;
	private TimeSince _sinceTurn;
	private float _yaw;
	private float _centre;
	private float _hold;

	public CombineLookAroundTask( float duration ) => _duration = duration;

	protected override void OnStart()
	{
		_started = 0;
		_sinceTurn = 10f;
		_centre = Combine.WorldRotation.Yaw();
	}

	protected override TaskStatus OnUpdate()
	{
		var c = Combine;

		// Sweep either side of where he was looking, pausing on each spot - never wander round in circles
		if ( _sinceTurn > _hold )
		{
			_sinceTurn = 0;
			_hold = Game.Random.Float( 1.2f, 2f );
			_yaw = _centre + Game.Random.Float( -70f, 70f );
		}

		c.TurnTowards( c.WorldPosition + Rotation.FromYaw( _yaw ).Forward * 100f, 90f );
		return _started > _duration ? TaskStatus.Success : TaskStatus.Running;
	}
}

/// <summary>Fire bursts at a spot rather than a person - keeps an enemy behind cover pinned down.</summary>
public sealed class CombineSuppressTask : CombineTask
{
	private readonly EnemyRecord _enemy;
	private readonly float _duration;
	private Vector3 _point;
	private TimeSince _started;
	private TimeUntil _pause;
	private int _burst;
	private Vector3? _aim;

	public CombineSuppressTask( EnemyRecord enemy, float duration )
	{
		_enemy = enemy;
		_duration = duration;
	}

	/// <summary>Chest height at the spot he was last seen.</summary>
	public static Vector3 SpotOf( EnemyRecord enemy ) => enemy.SeenPosition + Vector3.Up * 45f;

	protected override void OnStart()
	{
		_started = 0;
		_burst = Game.Random.Int( 3, 6 );
		_point = SpotOf( _enemy );
		_aim = null;
		Npc.Navigation.Stop();
	}

	protected override TaskStatus OnUpdate()
	{
		var c = Combine;
		if ( _started > _duration || c.Clip <= 0 || CombineNpc.IsDeadTarget( _enemy.Target ) ) return TaskStatus.Success;

		// Heard somewhere else since (round the side, behind us): that spot is old news
		if ( _enemy.HeardElsewhere )
		{
			if ( CombineSquad.Debug ) Log.Info( $"[suppress] {c.GameObject.Id.ToString()[..4]} stops: he was heard elsewhere" );
			return TaskStatus.Success;
		}

		// Seen again somewhere else: the fire follows him there
		var spot = SpotOf( _enemy );
		if ( spot.Distance( _point ) > 60f )
		{
			_point = spot;
			_aim = null;
		}

		// Facing it and it's in plain view, yet nobody's there: he's gone, stop shooting at nothing
		if ( c.IsFacing( _point, 30f ) && !c.SpotHidden( _enemy.SeenPosition ) )
		{
			if ( CombineSquad.Debug ) Log.Info( $"[suppress] {c.GameObject.Id.ToString()[..4]} stops: the spot is in view and empty" );
			return TaskStatus.Success;
		}

		c.TurnTowards( _aim ?? _point, 240f );
		if ( !c.IsFacing( _aim ?? _point, 12f ) || !_pause ) return TaskStatus.Running;

		// Shoot where the enemy is or would have to show himself - never into a wall short of it
		if ( _aim is not { } aim || !c.SuppressionReaches( c.MuzzlePosition, aim ) )
		{
			_aim = c.FindSuppressionAim( _point );
			if ( CombineSquad.Debug )
				Log.Info( $"[suppress] {c.GameObject.Id.ToString()[..4]} {(_aim is { } a ? $"aims {a.Distance( _point ):0} off the spot" : "has only wall to shoot")}" );
			if ( _aim is null )
			{
				// Only wall to shoot at from here: don't keep coming back to it, find another spot
				c.ShotBlocked();
				return TaskStatus.Failed;
			}

			return TaskStatus.Running;
		}

		// Don't hose a friend: hold fire until he's out of the way
		if ( c.CheckLineOfFire( aim, null ) == LineOfFire.Friendly ) return TaskStatus.Running;

		if ( c.TryShootAt( aim, 1.4f ) && --_burst <= 0 )
		{
			_burst = Game.Random.Int( 3, 6 );
			_pause = Game.Random.Float( 0.3f, 0.7f );
		}

		return TaskStatus.Running;
	}
}

/// <summary>This patch is checked: next time pick another one.</summary>
public sealed class CombineSearchDoneTask : CombineTask
{
	protected override TaskStatus OnUpdate()
	{
		Combine.ClearSearchPoint();
		Combine.Squad?.SearchPointDone( Combine );
		return TaskStatus.Success;
	}
}

/// <summary>The charge-up whine, then the ball.</summary>
public sealed class CombineBallTask : CombineTask
{
	private const float ChargeTime = 0.55f;

	private readonly GameObject _target;
	private readonly Vector3 _position;
	private TimeSince _started;
	private bool _fired;

	public CombineBallTask( GameObject target, Vector3 position )
	{
		_target = target;
		_position = position;
	}

	protected override void OnStart()
	{
		_started = 0;
		_fired = false;
		Combine.BallChargeEffects();
	}

	protected override TaskStatus OnUpdate()
	{
		var aim = _target.IsValid() && Combine.CanSee( _target ) ? CombineNpc.AimPoint( _target ) : _position + Vector3.Up * 45f;
		Combine.TurnTowards( aim, 240f );

		if ( !_fired && _started > ChargeTime )
		{
			_fired = true;
			Combine.FireBall( aim );
		}

		return _started > ChargeTime + 0.3f ? TaskStatus.Success : TaskStatus.Running;
	}
}

/// <summary>HL2's rifle-butt bash: an enemy right in his face gets hit instead of shot.</summary>
public sealed class CombineMeleeTask : CombineTask
{
	// Fraction of the swing where the stock connects
	private const float HitAt = 0.4f;

	private readonly GameObject _target;
	private float _duration;
	private TimeSince _started;
	private bool _hit;

	public CombineMeleeTask( GameObject target ) => _target = target;

	protected override void OnStart()
	{
		Npc.Navigation.Stop();
		_hit = false;
		_started = 0;
		_duration = Combine.PlayAction( "melee" );
	}

	protected override TaskStatus OnUpdate()
	{
		if ( _target.IsValid() ) Combine.TurnTowards( _target.WorldPosition, 360f );

		if ( !_hit && _started > _duration * HitAt )
		{
			_hit = true;
			Combine.MeleeHit( _target );
		}

		return _started > _duration ? TaskStatus.Success : TaskStatus.Running;
	}
}

/// <summary>
/// Patrol: keep station in the formation while the leader walks, so the squad moves as one.
/// Ends once the leader has stopped and we're in place, facing the way he faces.
/// </summary>
public sealed class CombineFollowTask : CombineTask
{
	private TimeSince _sinceRepath = 10f;
	private TimeSince _leaderStill;

	protected override void OnStart() => _leaderStill = 0;

	protected override TaskStatus OnUpdate()
	{
		var c = Combine;
		var squad = c.Squad;
		var leader = squad?.Leader;
		if ( leader is null || leader == c ) return TaskStatus.Success;

		var slot = squad.FormationSlot( c );
		var gap = slot.WithZ( 0 ).Distance( c.WorldPosition.WithZ( 0 ) );
		var leaderMoving = leader.Animation.IsValid() && leader.Animation.MoveVelocity.WithZ( 0 ).Length > 15f;
		if ( leaderMoving ) _leaderStill = 0;

		if ( gap > 30f )
		{
			if ( _sinceRepath > 0.25f )
			{
				_sinceRepath = 0;
				// A touch quicker when dropping behind, easing off as the gap closes
				var speed = gap > 400f ? c.RunSpeed : c.WalkSpeed * Math.Clamp( 0.9f + (gap - 40f) / 160f, 0.8f, 1.5f );
				Npc.Navigation.MoveTo( slot, 20f, speed );
			}
			return _leaderStill > 8f ? TaskStatus.Success : TaskStatus.Running;
		}

		if ( leaderMoving ) return TaskStatus.Running;

		Npc.Navigation.Stop();
		var ahead = c.WorldPosition + leader.WorldRotation.Forward * 100f;
		c.TurnTowards( ahead, 120f );
		return _leaderStill > 0.6f && (c.IsFacing( ahead, 15f ) || _leaderStill > 2.5f) ? TaskStatus.Success : TaskStatus.Running;
	}

	protected override void OnEnd() => Npc.Navigation.Stop();
}

/// <summary>Patrol leader: wait (briefly) for the squad to fall in before setting off.</summary>
public sealed class CombineGatherTask : CombineTask
{
	private TimeSince _started;

	protected override void OnStart() => _started = 0;

	protected override TaskStatus OnUpdate()
	{
		var squad = Combine.Squad;
		if ( squad is null || _started > 3f ) return TaskStatus.Success;

		var ready = squad.Alive.All( m => m == Combine || m.WorldPosition.WithZ( 0 ).Distance( squad.FormationSlot( m ).WithZ( 0 ) ) < 90f );
		return ready && _started > 0.3f ? TaskStatus.Success : TaskStatus.Running;
	}
}
