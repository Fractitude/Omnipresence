using Sandbox;

namespace Hl2Combine;

public enum CombineWeaponType
{
	/// <summary>SMG1 most of the time, sometimes an AR2 or a shotgun - like the soldiers in HL2.</summary>
	Random,
	Smg1,
	Ar2,
	Shotgun,

	/// <summary>The Metrocop's (only that package has their stats - see OtherWeaponStats).</summary>
	Pistol,
	Stunstick,
}

/// <summary>How each HL2 weapon behaves in a soldier's hands.</summary>
public sealed class CombineWeaponStats
{
	/// <summary>Body group choice, muzzle bone suffix and dropped model name.</summary>
	public string Name { get; init; }
	public int BodyGroup { get; init; }

	public float Damage { get; init; }
	public int Pellets { get; init; } = 1;
	public float Spread { get; init; }
	public float FireInterval { get; init; }
	public int ClipSize { get; init; }
	public float Range { get; init; }
	public int BurstMin { get; init; }
	public int BurstMax { get; init; }

	/// <summary>Pump after every shot and load shells one by one.</summary>
	public bool PumpAction { get; init; }

	/// <summary>The shooting animation plays once per shot, not looped through a burst (a pistol).</summary>
	public bool SingleShotAnim { get; init; }

	public string ShootAnim { get; init; }
	public string ShootCrouchAnim { get; init; }
	public string CombatIdle { get; init; }
	public string RelaxedIdle { get; init; }

	public string[] Flashes { get; init; }
	public float FlashSize { get; init; }
	public Color TracerColor { get; init; }
	public Color LightColor { get; init; }

	public const int NoWeaponBodyGroup = 3;

	// Built fresh on every call: static values survive hot reload and would keep old numbers
	public static CombineWeaponStats Smg1 => new()
	{
		Name = "smg1", BodyGroup = 1,
		Damage = 6f, Spread = 3f, FireInterval = 0.075f, ClipSize = 45, Range = 3000f, BurstMin = 5, BurstMax = 12,
		ShootAnim = "shoot_smg", ShootCrouchAnim = "shoot_smg_crouch", CombatIdle = "combat_idle", RelaxedIdle = "idle",
		Flashes = new[] { "muzzleflash1", "muzzleflash2", "muzzleflash3", "muzzleflash4" }, FlashSize = 10f,
		TracerColor = new Color( 1f, 0.85f, 0.55f ), LightColor = new Color( 1f, 0.7f, 0.35f ),
	};

	// Built fresh on every call: static values survive hot reload and would keep old numbers
	public static CombineWeaponStats Ar2 => new()
	{
		Name = "ar2", BodyGroup = 0,
		Damage = 9f, Spread = 2.2f, FireInterval = 0.1f, ClipSize = 30, Range = 4000f, BurstMin = 4, BurstMax = 10,
		ShootAnim = "shoot_ar2", ShootCrouchAnim = "shoot_ar2_crouch", CombatIdle = "combat_idle_ar2", RelaxedIdle = "idle_ar2",
		Flashes = new[] { "combinemuzzle1", "combinemuzzle2" }, FlashSize = 14f,
		TracerColor = new Color( 1f, 0.95f, 0.7f ), LightColor = new Color( 1f, 0.85f, 0.55f ),
	};

	// Built fresh on every call: static values survive hot reload and would keep old numbers
	public static CombineWeaponStats Shotgun => new()
	{
		Name = "shotgun", BodyGroup = 2,
		Damage = 8f, Pellets = 8, Spread = 4.5f, FireInterval = 0.9f, ClipSize = 6, Range = 750f, BurstMin = 1, BurstMax = 3, PumpAction = true,
		ShootAnim = "shoot_sg", ShootCrouchAnim = "shoot_sg_crouch", CombatIdle = "combat_idle_sg", RelaxedIdle = "idle",
		Flashes = new[] { "muzzleflash1", "muzzleflash2", "muzzleflash3", "muzzleflash4" }, FlashSize = 16f,
		TracerColor = new Color( 1f, 0.85f, 0.55f ), LightColor = new Color( 1f, 0.7f, 0.35f ),
	};

	public static CombineWeaponStats Get( CombineWeaponType type ) => type switch
	{
		CombineWeaponType.Ar2 => Ar2,
		CombineWeaponType.Shotgun => Shotgun,
		_ => Smg1,
	};

	public static CombineWeaponType Roll()
	{
		var r = Game.Random.Float();
		if ( r < 0.55f ) return CombineWeaponType.Smg1;
		if ( r < 0.85f ) return CombineWeaponType.Ar2;
		return CombineWeaponType.Shotgun;
	}
}
