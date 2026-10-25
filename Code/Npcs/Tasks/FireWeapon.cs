using Sandbox.Npcs.Layers;

namespace Sandbox.Npcs.Tasks;

/// <summary>
/// Fires a burst at a target. The burst length comes from the weapon's NPC usage - a random shot
/// count between its burst min and max. Shots only fire while the target is inside the weapon's
/// engagement band.
/// </summary>
public class FireWeapon : TaskBase
{
	/// <summary>The weapon component to fire.</summary>
	public BaseSandboxWeapon Weapon { get; }

	/// <summary>The GameObject to aim at.</summary>
	public GameObject Target { get; }

	/// <summary>Body rotation speed (degrees/s scale) used while actively aiming. Higher than the default look speed.</summary>
	public float AimTurnSpeed { get; set; } = 8f;

	/// <summary>Seconds (min, max) between shots of a pistol or other semi-automatic - one trigger pull at a time.</summary>
	public Vector2 SemiAutoInterval { get; set; } = new( 0.25f, 0.45f );

	/// <summary>How long a melee fighter waits for its target to come back into reach before it goes after it again.</summary>
	public float MeleeLoseReach { get; set; } = 0.4f;

	private int _shotsLeft;
	private TimeUntil _timeout;
	private TimeUntil _nextShot;
	private TimeSince _timeSinceInReach;

	public FireWeapon( BaseSandboxWeapon weapon, GameObject target )
	{
		Weapon = weapon;
		Target = target;
	}

	protected override void OnStart()
	{
		if ( !Weapon.IsValid() )
			return;

		// A swing is one try: the schedule runs again (and walks to the target again) after every one
		_shotsLeft = IsMelee ? 1 : Game.Random.Int( Weapon.Npc.BurstMin, Weapon.Npc.BurstMax );
		_timeSinceInReach = 0;

		// Shoot at the target, not wherever the animated barrel happens to point.
		Weapon.NpcAimTarget = Target;

		// A burst that can't land its shots (target keeps out of range) gives up rather than stall.
		_timeout = 4f;
		_nextShot = 0f;

		// Let nearby NPCs hear the gunfire and come investigate.
		Npc.EmitStimulus( StimulusKind.Gunshot, radius: 2048f, lifetime: 1.5f );
	}

	/// <summary>
	/// Pistols and anything not set to automatic are fired a shot per trigger pull, not as fast as the fire rate allows.
	/// </summary>
	private bool IsSemiAuto => !Weapon.PrimaryAutomatic || string.Equals( Weapon.HoldType, "pistol", StringComparison.OrdinalIgnoreCase );

	private bool IsMelee => Weapon is MeleeWeapon;

	protected override TaskStatus OnUpdate()
	{
		if ( !Weapon.IsValid() )
			return TaskStatus.Failed;

		// Dry - the burst is over, the schedule reloads next
		if ( ReloadWeapon.IsEmpty( Weapon ) )
			return TaskStatus.Success;

		if ( !Target.IsValid() )
			return TaskStatus.Failed;

		if ( _timeout )
			return TaskStatus.Failed;

		// The target ran out of reach: stop swinging at air, the schedule goes after it again
		if ( IsMelee )
		{
			if ( InRange() )
				_timeSinceInReach = 0;
			else if ( _timeSinceInReach > MeleeLoseReach )
				return TaskStatus.Success;
		}

		RotateBodyTowardTarget();

		// Only fire once we're facing the target, can see it, and it's inside the weapon's engagement
		// band. FirePrimary respects the weapon's fire rate - the burst paces itself.
		var rounds = Weapon.Clip1;
		if ( _nextShot && Npc.Animation.IsFacingTarget() && CanSee() && InRange() && Weapon.FirePrimary() )
		{
			// The engine spends a round from its holder's side, and an NPC has none: take it off the magazine here
			if ( Weapon.UsesPrimaryClip && Weapon.Clip1 >= rounds )
				Weapon.Clip1 = Math.Max( rounds - 1, 0 );

			Npc.Animation.TriggerAttack();
			_shotsLeft--;

			if ( IsSemiAuto )
				_nextShot = Game.Random.Float( SemiAutoInterval.x, SemiAutoInterval.y );
		}

		return _shotsLeft <= 0 ? TaskStatus.Success : TaskStatus.Running;
	}

	protected override void OnEnd()
	{
		if ( Weapon.IsValid() )
			Weapon.NpcAimTarget = null;
	}

	private bool CanSee() => Npc.Senses.VisibleTargets.Contains( Target );

	private bool InRange()
	{
		var distance = Npc.WorldPosition.Distance( Target.WorldPosition );

		// A melee weapon only hits what is in front of it: its own reach, not the engagement band of a gun
		if ( Weapon is MeleeWeapon melee )
			return distance <= melee.Range * 0.75f;

		return distance >= Weapon.Npc.MinRange && distance <= Weapon.Npc.MaxRange;
	}

	private void RotateBodyTowardTarget()
	{
		var toTarget = (Target.WorldPosition - Npc.WorldPosition).WithZ( 0 );
		if ( toTarget.LengthSquared < 1f ) return;

		var targetRot = Rotation.LookAt( toTarget.Normal, Vector3.Up );
		Npc.WorldRotation = Rotation.Lerp( Npc.WorldRotation, targetRot, AimTurnSpeed * Time.Delta );
	}
}
