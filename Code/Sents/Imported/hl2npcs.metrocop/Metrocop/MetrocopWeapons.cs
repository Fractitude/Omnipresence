namespace Hl2Metrocop;

/// <summary>Warbox's weapon choice (CombineNpc.WarboxWeapons.cs) for the cop: his own three, his own switch.</summary>
public sealed partial class CombineNpc
{
	private static readonly (string Name, CombineWeaponType Type)[] CopWeaponNames =
	{
		("Pistol", CombineWeaponType.Pistol),
		("SMG1", CombineWeaponType.Smg1),
		("Baton", CombineWeaponType.Stunstick),
	};

	// From CopWeapon, not the soldier's Weapon: Random is any of the three, a set one only that
	partial void OtherWeaponChoices( ref (string Name, CombineWeaponType Type)[] all, ref CombineWeaponType? only )
	{
		all = CopWeaponNames;
		only = CopWeapon switch
		{
			CopWeapon.Pistol => CombineWeaponType.Pistol,
			CopWeapon.Smg1 => CombineWeaponType.Smg1,
			CopWeapon.Stunstick => CombineWeaponType.Stunstick,
			_ => null,
		};
	}

	/// <summary>Handed another weapon mid-fight: a gun comes out already drawn; the baton goes off with the old one.</summary>
	partial void OtherWeaponSwitched()
	{
		Balls = 0;
		WeaponDrawn = HasGun;

		if ( !HasBaton )
		{
			_shouldActivateBaton = false;
			BatonOn = false;
			return;
		}

		// A baton in a player's hand is switched on at once, as when he takes control
		if ( Piloted && !BatonOn ) OtherPilotStart();
	}
}
