using System;
using System.Linq;
using Sandbox;
using Sandbox.Npcs;
using Sandbox.Npcs.Tasks;

namespace Hl2Combine;

public abstract class CombineSchedule : ScheduleBase
{
	protected CombineNpc Combine => Npc as CombineNpc;
	protected CombineSquad Squad => Combine.Squad;

	protected bool IsSquadLeader => Squad is { } squad && squad.Leader == Combine && squad.Members.Count > 1;

	public override NpcAwareness InterruptedBy => base.InterruptedBy | CombineNpc.GrenadeDanger;
}

/// <summary>Stand (or kneel) and fire a burst, sometimes sidestep afterwards.</summary>
public sealed class CombineEngageSchedule : CombineSchedule
{
	public GameObject Target { get; set; }

	/// <summary>Not holding an attack slot: short bursts with longer pauses.</summary>
	public bool Light { get; set; }

	public override int Priority => SchedulePriority.Combat;
	public override NpcAwareness InterruptedBy => CombineNpc.GrenadeDanger;

	private TimeSince _sinceSeen;

	protected override void OnStart()
	{
		var c = Combine;
		c.SetRelaxed( false );
		_sinceSeen = 0;
		Npc.Animation.SetLookTarget( Target );

		var distance = c.WorldPosition.Distance( Target.WorldPosition );
		var holding = Squad?.Posture == SquadPosture.Hold;

		// Already kneeling: fire from the knee, the shot stands up by itself if cover is in the way.
		// Don't bob up and down between bursts.
		var close = distance < CombineNpc.CloseRange;
		var kneel = !close && (c.Crouched || (!holding && c.SincePostureChange > 1.5f && c.WantsCrouch( distance )));
		AddTask( new CombineCrouchTask( kneel ) );
		var burst = Light ? Math.Max( 1, c.Gun.BurstMin / 2 ) : Game.Random.Int( c.Gun.BurstMin, c.Gun.BurstMax );
		AddTask( new CombineShootTask( Target, burst ) );

		// Holding: duck back down after popping up - only where kneeling actually hides him
		if ( (holding || Light) && c.IsCovered( c.WorldPosition, Target.WorldPosition + Vector3.Up * 60f ) )
			AddTask( new CombineCrouchTask( true ) );
		AddTask( new Wait( Light ? Game.Random.Float( 0.8f, 1.6f ) : Game.Random.Float( 0.2f, 0.7f ) ) );

		if ( !holding && !Light && Game.Random.Float() < 0.1f && !c.IsCovered( c.WorldPosition, Target.WorldPosition + Vector3.Up * 60f ) )
		{
			AddTask( new CombineCrouchTask( false ) );
			AddTask( new CombineMoveTo( c.StrafePosition( Target.WorldPosition ), 15f ) { Speed = c.WalkSpeed * 1.1f, FaceTarget = Target } );
		}
	}

	protected override void OnEnd()
	{
		Npc.Animation.ClearLookTarget();
		Squad?.ReleaseAttack( Combine );
	}

	protected override bool ShouldCancel()
	{
		if ( CombineNpc.IsDeadTarget( Target ) ) return true;
		if ( Combine.CanSee( Target ) ) _sinceSeen = 0;
		return _sinceSeen > 1.2f;
	}
}

/// <summary>Run to a spot the enemy can't see, kneel, reload if needed, wait a moment.</summary>
public sealed class CombineCoverSchedule : CombineSchedule
{
	public Vector3 Threat { get; set; }
	public bool Reload { get; set; }

	public override int Priority => SchedulePriority.Combat;
	public override NpcAwareness InterruptedBy => CombineNpc.GrenadeDanger;

	protected override void OnStart()
	{
		var c = Combine;
		c.SetRelaxed( false );

		if ( c.FindCover( Threat ) is { } cover )
		{
			Squad?.ClaimCover( c, cover );

			if ( cover.Distance( c.WorldPosition ) > 32f )
			{
				c.Say( c.Health < c.StartHealth * 0.5f ? CombineVoice.CoverHurt : CombineVoice.Cover );
				AddTask( new CombineCrouchTask( false ) );
				AddTask( c.CombatMove( cover, 16f ) );
			}
		}
		else if ( Reload || c.Health < c.StartHealth * 0.4f )
		{
			// Nowhere to hide and in trouble: back off a little, once
			var back = c.RetreatPosition( Threat, 250f );
			AddTask( new CombineCrouchTask( false ) );
			AddTask( c.CombatMove( back, 24f ) );
		}

		AddTask( new CombineFaceTask( Threat ) );
		AddTask( new CombineCrouchTask( true ) );
		if ( Reload ) AddTask( new CombineReloadTask() );
		AddTask( new CombineCallTask( () => _waiting = true ) );
		AddTask( new Wait( Game.Random.Float( 0.8f, 2f ) ) );
	}

	private bool _waiting;

	protected override void OnEnd() => _waiting = false;

	// In cover with a gun ready: the moment a turn to shoot frees up and he can see them, go (as in HL2)
	protected override bool ShouldCancel()
	{
		if ( !_waiting ) return false;
		var c = Combine;
		var seen = Npc.Senses.GetBestTarget();
		return seen.IsValid() && !CombineNpc.IsDeadTarget( seen ) && c.Clip > 0 && Squad is { } squad && squad.HasFreeAttack( c );
	}
}

/// <summary>Dig in facing the enemy and wait - for them to show, or for help to arrive.</summary>
public sealed class CombineHoldSchedule : CombineSchedule
{
	public Vector3 Threat { get; set; }

	public override int Priority => SchedulePriority.Alert;

	protected override void OnStart()
	{
		var c = Combine;
		c.SetRelaxed( false );

		if ( c.FindCover( Threat ) is { } cover && cover.Distance( c.WorldPosition ) > 32f )
		{
			Squad?.ClaimCover( c, cover );
			AddTask( new CombineCrouchTask( false ) );
			AddTask( c.CombatMove( cover, 16f ) );
		}

		AddTask( new CombineFaceTask( Threat ) );
		AddTask( new CombineCrouchTask( true ) );
		AddTask( new Wait( Game.Random.Float( 1f, 2f ) ) );
	}
}

/// <summary>
/// Close in on the enemy: shotguns rush straight in, rifles bound from cover to cover in long
/// legs with a short stop at each - never the stop-start shuffle of short hops in the open.
/// </summary>
public sealed class CombineAdvanceSchedule : CombineSchedule
{
	public Vector3 Threat { get; set; }

	public override int Priority => SchedulePriority.Alert;

	protected override void OnStart()
	{
		var c = Combine;
		c.SetRelaxed( false );

		var toThreat = (Threat - c.WorldPosition).WithZ( 0 );
		var distance = toThreat.Length;
		var inRange = distance < c.EngageRange;

		// A shotgun is no use at range: run in until it is, no stopping on the way
		if ( c.Gun.PumpAction )
		{
			var stand = c.EngageRange * 0.6f;
			if ( distance > stand + 40f )
			{
				var spot = Threat - toThreat.Normal * stand;
				spot = Npc.Scene.NavMesh.GetClosestPoint( spot ) ?? spot;
				AddTask( new CombineCrouchTask( false ) );
				AddTask( new CombineMoveTo( spot, 40f ) { Speed = c.RunSpeed } );
				return;
			}
		}

		if ( c.ShouldStayInCover( Threat ) )
		{
			AddTask( new CombineFaceTask( Threat ) );
			if ( inRange ) AddTask( new CombineCrouchTask( true ) );
			AddTask( new Wait( Game.Random.Float( 0.5f, 1f ) ) );
			return;
		}

		if ( c.FindAdvanceCover( Threat ) is { } cover )
		{
			Squad?.ClaimCover( c, cover );
			AddTask( new CombineCrouchTask( false ) );
			AddTask( c.CombatMove( cover, 16f ) );
			AddTask( new CombineFaceTask( Threat ) );
			if ( inRange ) AddTask( new CombineCrouchTask( true ) );
			AddTask( new Wait( Game.Random.Float( 0.3f, 0.7f ) ) );
			return;
		}

		// Nothing to hide behind on the way: one run to fighting distance, not a hop at a time
		var desired = MathF.Min( c.EngageRange * 0.7f, 1800f );
		var step = MathF.Min( 1200f, distance - desired );
		if ( step > 60f )
		{
			var next = c.WorldPosition + toThreat.Normal * step;
			next = Npc.Scene.NavMesh.GetClosestPoint( next ) ?? next;
			AddTask( new CombineCrouchTask( false ) );
			AddTask( new CombineMoveTo( next, 30f ) { Speed = c.RunSpeed } );
			return;
		}

		AddTask( new CombineFaceTask( Threat ) );
		AddTask( new Wait( Game.Random.Float( 0.4f, 0.8f ) ) );
	}
}

/// <summary>Keep the enemy's head down: fire at where they were last seen.</summary>
public sealed class CombineSuppressSchedule : CombineSchedule
{
	public EnemyRecord Enemy { get; set; }

	public override int Priority => SchedulePriority.Combat;
	public override NpcAwareness InterruptedBy => CombineNpc.GrenadeDanger;

	protected override void OnStart()
	{
		var c = Combine;
		c.SetRelaxed( false );
		Npc.Navigation.Stop();
		AddTask( new CombineCrouchTask( false ) );

		// Behind a wall: step out to where the fire gets there first (unless a move just failed - then from here)
		if ( c.SinceMoveFailed > 1f && c.FindSuppressionSpot( CombineSuppressTask.SpotOf( Enemy ) ) is { } spot && spot.Distance( c.WorldPosition ) > 30f )
		{
			if ( CombineSquad.Debug ) Log.Info( $"[suppress] {c.GameObject.Id.ToString()[..4]} steps out {spot.Distance( c.WorldPosition ):0}" );
			AddTask( new CombineMoveTo( spot, 20f ) { Speed = c.RunSpeed } );
		}

		AddTask( new CombineFaceTask( Enemy.SeenPosition ) );
		AddTask( new CombineSuppressTask( Enemy, 3.5f ) );
	}

	// An enemy in view after all (he came round the side, or out of cover): shoot him, not the spot
	protected override bool ShouldCancel()
	{
		if ( !Combine.SeesEnemy ) return false;
		if ( CombineSquad.Debug ) Log.Info( $"[suppress] {Combine.GameObject.Id.ToString()[..4]} stops: an enemy in view" );
		return true;
	}
}

/// <summary>Ordered round the side: get to a spot off to the enemy's flank that still sees them.</summary>
public sealed class CombineFlankSchedule : CombineSchedule
{
	public GameObject Target { get; set; }
	public Vector3 Position { get; set; }

	public override int Priority => SchedulePriority.Combat;
	public override NpcAwareness InterruptedBy => CombineNpc.GrenadeDanger;

	protected override void OnStart()
	{
		var c = Combine;
		c.SetRelaxed( false );

		if ( c.FindFlankPosition( Position, Target, out var side ) is { } point )
		{
			if ( IsSquadLeader )
				AddTask( new CombineActionTask( side > 0f ? "signal_left" : "signal_right" ) );

			AddTask( new CombineCrouchTask( false ) );
			AddTask( new CombineMoveTo( point, 24f ) { Speed = c.RunSpeed } );
		}
		else
		{
			AddTask( new CombineFaceTask( Position ) );
			AddTask( new CombineCrouchTask( true ) );
			AddTask( new Wait( 1f ) );
		}
	}

	protected override void OnEnd()
	{
		Squad?.FlankDone( Combine );
	}
}

public sealed class CombineThrowingSchedule : CombineSchedule
{
	public Vector3 Target { get; set; }
	public bool Flat { get; set; }

	public override int Priority => SchedulePriority.Combat;
	public override NpcAwareness InterruptedBy => CombineNpc.GrenadeDanger;

	protected override void OnStart()
	{
		Combine.SetRelaxed( false );
		Npc.Navigation.Stop();
		AddTask( new CombineCrouchTask( false ) );
		AddTask( new CombineFaceTask( Target ) );
		AddTask( new CombineThrowTask( Target, Flat ) );
	}
}

public sealed class CombineReloadSchedule : CombineSchedule
{
	public override int Priority => SchedulePriority.Ambient;

	protected override void OnStart()
	{
		Npc.Navigation.Stop();
		if ( Combine.Enemy.IsValid() ) AddTask( new CombineCrouchTask( true ) );
		AddTask( new CombineReloadTask() );
	}
}

/// <summary>Each searcher sweeps his own patch around the last known position.</summary>
public sealed class CombineSearchSchedule : CombineSchedule
{
	public override int Priority => SchedulePriority.Alert;

	protected override void OnStart()
	{
		var c = Combine;
		c.SetRelaxed( false );
		c.Crouched = false;

		var point = c.CurrentSearchPoint();
		if ( point.Distance( c.WorldPosition ) > 60f )
			AddTask( new CombineMoveTo( point, 40f ) { Speed = c.WalkSpeed * 1.2f } );
		AddTask( new CombineLookAroundTask( Game.Random.Float( 3f, 5f ) ) );
		AddTask( new CombineSearchDoneTask() );
	}
}

/// <summary>Breach: stack at the door, grenade in, wait for the bang, rush in.</summary>
public sealed class CombineBreachSchedule : CombineSchedule
{
	public override int Priority => SchedulePriority.Alert;

	protected override void OnStart()
	{
		var c = Combine;
		var squad = Squad;
		var plan = squad?.Breach;
		if ( plan is null ) return;

		c.SetRelaxed( false );

		switch ( plan.Step )
		{
			case BreachPlan.Stage.Stacking:
				AddTask( new CombineCrouchTask( false ) );
				AddTask( new CombineMoveTo( plan.StackSpot( c ), 24f ) { Speed = c.RunSpeed } );
				AddTask( new CombineFaceTask( plan.Entrance ) );
				AddTask( new CombineCrouchTask( true ) );
				AddTask( new Wait( 1f ) );
				break;

			case BreachPlan.Stage.Throwing when plan.Grenadier == c:
				AddTask( new CombineCrouchTask( false ) );
				AddTask( new CombineMoveTo( plan.ThrowFrom, 20f ) { Speed = c.WalkSpeed * 1.6f } );
				AddTask( new CombineFaceTask( plan.ThrowAt ) );
				AddTask( new CombineThrowTask( plan.ThrowAt, flat: true, onThrown: squad.OnBreachGrenadeThrown ) );
				AddTask( new CombineMoveTo( plan.StackSpot( c ), 24f ) { Speed = c.RunSpeed } );
				break;

			case BreachPlan.Stage.Rushing:
				AddTask( new CombineCrouchTask( false ) );
				AddTask( new CombineMoveTo( plan.Target, 60f ) { Speed = c.RunSpeed } );
				AddTask( new CombineLookAroundTask( 2f ) );
				break;

			default:
				AddTask( new CombineFaceTask( plan.Entrance ) );
				AddTask( new CombineCrouchTask( true ) );
				AddTask( new Wait( 0.5f ) );
				break;
		}
	}
}

/// <summary>Answering a call for help: the leader runs to the squad in trouble, the rest keep up.</summary>
public sealed class CombineReinforceSchedule : CombineSchedule
{
	public override int Priority => SchedulePriority.Alert;

	protected override void OnStart()
	{
		var c = Combine;
		var squad = Squad;
		if ( squad is null ) return;

		c.SetRelaxed( false );
		c.Crouched = false;

		var leader = squad.Leader;
		if ( leader == c || leader is null )
		{
			AddTask( new CombineMoveTo( squad.HelpPosition, 300f ) { Speed = c.RunSpeed } );
			AddTask( new Wait( 0.3f ) );
			return;
		}

		var spot = leader.WorldPosition + leader.WorldRotation * squad.FormationOffset( c );
		spot = Npc.Scene.NavMesh.GetClosestPoint( spot ) ?? spot;
		AddTask( new CombineMoveTo( spot, 50f ) { Speed = c.RunSpeed } );
		AddTask( new Wait( 0.3f ) );
	}
}

/// <summary>Losing badly: fall back away from the enemy and take cover there.</summary>
public sealed class CombineRetreatSchedule : CombineSchedule
{
	public override int Priority => SchedulePriority.Survival;

	protected override void OnStart()
	{
		var c = Combine;
		c.SetRelaxed( false );
		c.Crouched = false;

		var threat = c.EnemyPosition;
		var spot = c.FindCover( threat, 500f ) ?? c.RetreatPosition( threat, 400f );
		AddTask( new CombineMoveTo( spot, 30f ) { Speed = c.RunSpeed } );
		AddTask( new CombineFaceTask( threat ) );
		AddTask( new CombineCrouchTask( true ) );
		AddTask( new Wait( 2f ) );
	}
}

/// <summary>The leader wanders, the rest follow in formation and chat over the radio.</summary>
public sealed class CombinePatrolSchedule : CombineSchedule
{
	public override int Priority => SchedulePriority.Ambient;

	protected override void OnStart()
	{
		var c = Combine;
		c.SetRelaxed( true );
		c.Crouched = false;

		// Just spawned: give the squad a moment to form before wandering off alone
		if ( c.SinceSpawn < 1.5f )
		{
			AddTask( new Wait( 1f ) );
			return;
		}

		var squad = c.Squad;
		var leader = squad?.Leader;

		if ( leader is not null && leader != c )
		{
			// Walk with the leader, in formation, rather than catching up a stretch at a time
			AddTask( new CombineFollowTask() );
			AddTask( new Wait( Game.Random.Float( 0.2f, 0.5f ) ) );
			return;
		}

		squad?.TryChatter( c );

		// Keep walking to the same spot if the patrol got interrupted on the way - unless it's where he got stuck
		if ( c.PatrolGoal is not { } destination || destination.Distance( c.WorldPosition ) < 60f || c.SincePatrolGoal > 20f || c.SinceMoveStuck < 3f )
		{
			c.PatrolGoal = c.FindPatrolGoal();
			c.SincePatrolGoal = 0;
			if ( c.PatrolGoal is not { } next )
			{
				AddTask( new Wait( Game.Random.Float( 1f, 2f ) ) );
				return;
			}

			destination = next;
		}

		// Turn to face the way first, rather than setting off sideways
		if ( !c.IsFacing( destination, 60f ) ) AddTask( new CombineFaceTask( destination ) );

		// Everyone in their place first, then the squad sets off together
		if ( squad is not null && squad.Alive.Count() > 1 ) AddTask( new CombineGatherTask() );
		AddTask( new CombineMoveTo( destination, 30f ) );
		AddTask( new Wait( Game.Random.Float( 2f, 5f ) ) );
	}
}

/// <summary>"Bouncer, bouncer!" - run from a live grenade.</summary>
public sealed class CombineDodgeSchedule : CombineSchedule
{
	public GameObject Danger { get; set; }

	public override int Priority => SchedulePriority.Survival;
	public override NpcAwareness InterruptedBy => NpcAwareness.None;

	protected override void OnStart()
	{
		var c = Combine;
		c.SetRelaxed( false );
		c.Crouched = false;

		// Only the first to see it shouts; the rest already heard
		if ( !c.Squad?.Warned( Danger ) ?? true )
			c.Say( CombineVoice.Danger, force: true );

		var radius = Danger.GetComponent<TimedExplosive>()?.Radius ?? 256f;
		var needed = radius + 70f - c.WorldPosition.Distance( Danger.WorldPosition );
		AddTask( new CombineMoveTo( c.RetreatPosition( Danger.WorldPosition, MathF.Max( 80f, needed ) ), 20f ) { Speed = c.RunSpeed * 1.15f } );
		AddTask( new CombineFaceTask( Danger.WorldPosition ) );
		AddTask( new CombineCrouchTask( true ) );
	}

	protected override bool ShouldCancel() => !Danger.IsValid();
}

/// <summary>No clean shot from here: step to a spot that has one, gun on the target.</summary>
public sealed class CombineRepositionSchedule : CombineSchedule
{
	public GameObject Target { get; set; }
	public Vector3 Spot { get; set; }

	public override int Priority => SchedulePriority.Combat;
	public override NpcAwareness InterruptedBy => CombineNpc.GrenadeDanger;

	protected override void OnStart()
	{
		var c = Combine;
		c.SetRelaxed( false );
		c.Crouched = false;
		AddTask( new CombineMoveTo( Spot, 12f ) { Speed = c.WalkSpeed * 1.2f, FaceTarget = Target } );
		AddTask( new CombineShootTask( Target, Game.Random.Int( c.Gun.BurstMin, c.Gun.BurstMax ) ) );
	}
}

/// <summary>Charge up and fire an AR2 energy ball - at the enemy, or at the cover he's behind.</summary>
public sealed class CombineBallSchedule : CombineSchedule
{
	public GameObject Target { get; set; }
	public Vector3 Position { get; set; }

	public override int Priority => SchedulePriority.Combat;
	public override NpcAwareness InterruptedBy => CombineNpc.GrenadeDanger;

	protected override void OnStart()
	{
		var c = Combine;
		c.SetRelaxed( false );
		Npc.Navigation.Stop();
		AddTask( new CombineCrouchTask( false ) );
		AddTask( new CombineFaceTask( Position ) );
		AddTask( new CombineBallTask( Target, Position ) );
		AddTask( new Wait( 0.4f ) );
	}
}

/// <summary>Enemy within arm's reach: bash him with the gun.</summary>
public sealed class CombineMeleeSchedule : CombineSchedule
{
	public GameObject Target { get; set; }

	public override int Priority => SchedulePriority.Combat;
	public override NpcAwareness InterruptedBy => CombineNpc.GrenadeDanger;

	protected override void OnStart()
	{
		Combine.SetRelaxed( false );
		Combine.Crouched = false;
		AddTask( new CombineMeleeTask( Target ) );
	}
}

/// <summary>Hit or heard from somewhere he isn't looking: turn round to face it.</summary>
public sealed class CombineReactSchedule : CombineSchedule
{
	public Vector3 Threat { get; set; }

	public override int Priority => SchedulePriority.Combat;
	public override NpcAwareness InterruptedBy => CombineNpc.GrenadeDanger;

	protected override void OnStart()
	{
		Combine.SetRelaxed( false );
		Npc.Navigation.Stop();
		AddTask( new CombineFaceTask( Threat ) );
		AddTask( new Wait( 0.3f ) );
	}
}
