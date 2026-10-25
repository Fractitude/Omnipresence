using System;
using System.Linq;
using Sandbox;

namespace Hl2CombinePrisonGuard;

/// <summary>
/// Picking his gun from Warbox (its NPC_COMPATIBILITY.md, "Weapons"): found by name, set on the host -
/// before he's spawned, right after, in the middle of a fight or while a player plays as him.
/// </summary>
public sealed partial class CombineNpc
{
	private static readonly (string Name, CombineWeaponType Type)[] WeaponNames =
	{
		("SMG1", CombineWeaponType.Smg1),
		("AR2", CombineWeaponType.Ar2),
		("Shotgun", CombineWeaponType.Shotgun),
	};

	/// <summary>
	/// The guns he can carry. Warbox reads this off the prefab before spawning, so it comes from the
	/// properties alone: Random is any of them, a set gun (the Elite's AR2) is just that one.
	/// </summary>
	public string[] WarboxWeapons => Choices.Select( w => w.Name ).ToArray();

	/// <summary>Every gun of his kind, and the one he's set to carry (null: any of them).</summary>
	private (string Name, CombineWeaponType Type)[] AllWeapons( out CombineWeaponType? only )
	{
		var all = WeaponNames;
		only = Weapon == CombineWeaponType.Random ? null : Weapon;
		OtherWeaponChoices( ref all, ref only );
		return all;
	}

	private (string Name, CombineWeaponType Type)[] Choices
	{
		get
		{
			var all = AllWeapons( out var only );
			return only is { } one ? all.Where( w => w.Type == one ).ToArray() : all;
		}
	}

	// Another NPC built on this code (the Metrocop) carries other guns, picked by a property of its own
	partial void OtherWeaponChoices( ref (string Name, CombineWeaponType Type)[] all, ref CombineWeaponType? only );
	partial void OtherWeaponSwitched();

	/// <summary>The gun in his hands; setting it hands him another one at once.</summary>
	public string WarboxWeapon
	{
		get => AllWeapons( out _ ).FirstOrDefault( w => w.Type == ActiveWeapon ).Name;
		set
		{
			if ( IsProxy ) return;
			var match = Choices.FirstOrDefault( w => string.Equals( w.Name, value, StringComparison.OrdinalIgnoreCase ) );
			if ( match.Name is null ) return;

			// Set before he has started: his start takes it instead of rolling one
			if ( !_weaponPicked )
			{
				_chosenWeapon = match.Type;
				return;
			}

			SwitchWeapon( match.Type );
		}
	}

	private CombineWeaponType? _chosenWeapon;
	private bool _weaponPicked;

	partial void ChosenWeapon( ref CombineWeaponType weapon )
	{
		_weaponPicked = true;
		if ( _chosenWeapon is { } chosen ) weapon = chosen;
	}

	/// <summary>A fresh gun: a full magazine of it, the AR2's balls with it, his skin to match (the animation update).</summary>
	private void SwitchWeapon( CombineWeaponType type )
	{
		if ( type == ActiveWeapon ) return;

		ActiveWeapon = type;
		Clip = ClipSize;
		Balls = type == CombineWeaponType.Ar2 ? StartBalls : 0;
		SetReloading( 0f );
		_pilotReloading = false;
		OtherWeaponSwitched();

		// The AI plans again with the new gun; a player just carries on with it
		if ( !Piloted ) EndCurrentSchedule();
	}
}
