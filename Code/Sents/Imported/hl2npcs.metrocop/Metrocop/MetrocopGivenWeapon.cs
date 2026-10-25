namespace Hl2Metrocop;

/// <summary>A sandbox or Workshop weapon handed to the cop (CombineNpc.GivenWeapon.cs): his pistol or SMG moves for it.</summary>
public sealed partial class CombineNpc
{
	partial void OtherGivenPose( string holdType, ref CombineWeaponType pose )
	{
		pose = holdType.Contains( "pistol" ) ? CombineWeaponType.Pistol
			: holdType.Contains( "swing" ) || holdType.Contains( "melee" ) || holdType.Contains( "punch" ) || GivenIsMelee ? CombineWeaponType.Stunstick
			: CombineWeaponType.Smg1;
	}

	// It's in his hand from the start, and a baton that was on goes dark
	partial void OtherGivenTaken()
	{
		WeaponDrawn = true;
		_shouldActivateBaton = false;
		BatonOn = false;
	}
}
