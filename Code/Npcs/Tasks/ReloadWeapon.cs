namespace Sandbox.Npcs.Tasks;

/// <summary>
/// Reloads a weapon whose magazine is empty: plays the reload gesture, waits the weapon's reload time, then fills the
/// magazine. Does nothing for a weapon that still has rounds. An NPC has no inventory to draw reserve ammo from, so it
/// fills the magazine itself - the engine's own reload needs a holder.
/// </summary>
public class ReloadWeapon : TaskBase
{
	/// <summary>The weapon to reload.</summary>
	public BaseSandboxWeapon Weapon { get; }

	private TimeUntil _done;
	private bool _reloading;

	public ReloadWeapon( BaseSandboxWeapon weapon )
	{
		Weapon = weapon;
	}

	/// <summary>
	/// True when the weapon uses a magazine and it has run dry.
	/// </summary>
	public static bool IsEmpty( BaseSandboxWeapon weapon )
	{
		return weapon.IsValid() && weapon.UsesPrimaryClip && weapon.Clip1 <= 0;
	}

	protected override void OnStart()
	{
		if ( !IsEmpty( Weapon ) )
			return;

		// Each reload uses a spare magazine; none left means the schedule picks something else (the melee weapon)
		if ( Npc is CombatNpc.CombatNpc soldier )
		{
			if ( soldier.SpareMagazines <= 0 )
				return;

			soldier.SpareMagazines--;
		}

		_reloading = true;
		_done = Weapon.ReloadTime;
		Npc.Animation.TriggerReload();
	}

	protected override TaskStatus OnUpdate()
	{
		if ( !_reloading )
			return TaskStatus.Success;

		if ( !Weapon.IsValid() )
			return TaskStatus.Failed;

		if ( !_done )
			return TaskStatus.Running;

		Weapon.Clip1 = Weapon.PrimaryClipSize;
		return TaskStatus.Success;
	}
}
