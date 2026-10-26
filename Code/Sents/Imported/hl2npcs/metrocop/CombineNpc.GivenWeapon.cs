using System;
using Sandbox;
using Sandbox.Npcs;

namespace Hl2Metrocop;

/// <summary>
/// A sandbox or Workshop weapon handed to him (Warbox: its NPC_COMPATIBILITY.md, "Sandbox and Workshop
/// weapons"). Warbox clones it as a child of him, sets this on every machine, and removes it itself.
/// He holds it in his right hand with his own gun put away, moves with his own animations for the
/// nearest kind of gun, and every shot he'd fire - the AI's or a player's - goes through it instead:
/// the weapon makes its own shot, sound and damage. Held by an NPC it never runs dry.
/// </summary>
public sealed partial class CombineNpc
{
	/// <summary>
	/// Tuning where the given weapon sits in his hand: yaw, pitch, roll and offset (forward, left, up) from
	/// the hand bone. The defaults lay it the way his own guns lie (the AR2's barrel runs 88 degrees round
	/// and 20 down from the hand bone), right way up.
	/// </summary>
	[ConVar( "metrocop_given_yaw" )] public static float GivenYaw { get; set; } = 88f;
	[ConVar( "metrocop_given_pitch" )] public static float GivenPitch { get; set; } = 20f;
	[ConVar( "metrocop_given_roll" )] public static float GivenRoll { get; set; } = 180f;
	[ConVar( "metrocop_given_x" )] public static float GivenX { get; set; } = 0f;
	[ConVar( "metrocop_given_y" )] public static float GivenY { get; set; } = 0f;
	[ConVar( "metrocop_given_z" )] public static float GivenZ { get; set; } = 0f;

	private const string RightHandBone = "ValveBiped_Bip01_R_Hand";

	private BaseSandboxWeapon _given;
	private CombineWeaponType _givenPose;
	private GameObject _givenAim;

	/// <summary>The weapon he holds instead of his own, or null for his own.</summary>
	public BaseSandboxWeapon WarboxGivenWeapon
	{
		get => _given;
		set
		{
			if ( _given == value ) return;
			DropGiven();
			_given = value;
			if ( value.IsValid() ) TakeGiven( value );
		}
	}

	public bool HasGivenWeapon => _given.IsValid();

	/// <summary>The kind of gun his animations show: his own, or the nearest to the given weapon.</summary>
	public CombineWeaponType AnimWeapon => HasGivenWeapon ? _givenPose : ActiveWeapon;

	/// <summary>The stats behind his animations (shot loop, idles): his own gun's, or the stand-in's.</summary>
	public CombineWeaponStats AnimGun
	{
		get
		{
			if ( !HasGivenWeapon ) return Gun;
			var stats = CombineWeaponStats.Get( _givenPose );
			OtherWeaponStats( _givenPose, ref stats );
			return stats;
		}
	}

	// Which of his own guns' animations fit a weapon held this way (the Metrocop has a pistol of his own)
	partial void OtherGivenPose( string holdType, ref CombineWeaponType pose );

	private void TakeGiven( BaseSandboxWeapon weapon )
	{
		var hold = (weapon.HoldType ?? "").ToLowerInvariant();
		_givenPose = hold.Contains( "shotgun" ) ? CombineWeaponType.Shotgun
			: hold.Contains( "rifle" ) || hold.Contains( "rpg" ) || hold.Contains( "sniper" ) ? CombineWeaponType.Ar2
			: CombineWeaponType.Smg1;
		OtherGivenPose( hold, ref _givenPose );

		// The sandbox hangs it on the citizen's hold_R bone, which an HL2 skeleton hasn't got: it ends up
		// on the weapon itself, and is put in his hand every frame (UpdateGivenModel)
		weapon.CreateWorldModel( Renderer );
		weapon.SpreadScale = weapon.Npc.SpreadScale;

		if ( IsProxy ) return;

		// Never empty in an NPC's hands: his magazine stays full so he never stops to reload
		Clip = Math.Max( ClipSize, 1 );
		SetReloading( 0f );
		_pilotReloading = false;
		OtherGivenTaken();
		if ( !Piloted ) EndCurrentSchedule();
	}

	partial void OtherGivenTaken();

	/// <summary>A weapon used up close - a sandbox melee weapon (crowbar...) or one an NPC only uses within 200.</summary>
	public bool GivenIsMelee => HasGivenWeapon && (_given is MeleeWeapon || _given.Npc.MaxRange <= 200f);

	/// <summary>How close he has to be to hit with it: a melee weapon's swing reach, else what NPCs use it within.</summary>
	public float GivenReach => _given is MeleeWeapon melee ? MathF.Min( melee.Range, _given.Npc.MaxRange > 0f ? _given.Npc.MaxRange : melee.Range ) : _given.Npc.MaxRange;

	/// <summary>Who he's fighting with it: whoever he sees, else the one his squad knows of.</summary>
	public GameObject GivenTarget()
	{
		var seen = Senses.GetBestTarget();
		if ( seen.IsValid() && !IsDeadTarget( seen ) ) return seen;
		if ( Squad?.KnownEnemy( this ) is { } known && known.SinceSeen < 10f && known.Target.IsValid() && !IsDeadTarget( known.Target ) )
			return known.Target;
		return null;
	}

	/// <summary>
	/// Host, ahead of his own tactics: what the given weapon itself calls for (its Npc ranges). A melee
	/// weapon has him walk up and swing; a weapon with a minimum range (an RPG) has him back off from
	/// someone too close. Anything else he fights with as his own gun.
	/// </summary>
	private ScheduleBase GivenWeaponSchedule()
	{
		if ( !HasGivenWeapon || Piloted || FindDangerousGrenade().IsValid() ) return null;
		var target = GivenTarget();
		if ( !target.IsValid() ) return null;

		if ( GivenIsMelee )
		{
			var close = GetSchedule<CombineGivenCloseSchedule>();
			close.Target = target;
			return close;
		}

		var tooClose = _given.Npc.MinRange - (target.WorldPosition - WorldPosition).WithZ( 0 ).Length;
		if ( tooClose > 0f && SinceMoveFailed > 2f )
		{
			var back = GetSchedule<CombineGivenBackOffSchedule>();
			back.Spot = RetreatPosition( target.WorldPosition, tooClose + 120f );
			back.Target = target;
			return back;
		}

		return null;
	}

	private void DropGiven()
	{
		if ( _given.IsValid() ) _given.NpcAimTarget = null;
		if ( _givenAim.IsValid() ) _givenAim.Destroy();
		_givenAim = null;
		_given = null;

		if ( IsProxy ) return;
		Clip = ClipSize;
		if ( !Piloted && IsAlive ) EndCurrentSchedule();
	}

	/// <summary>Every client, every frame: the given weapon's model in his right hand (or the first-person arms' hand).</summary>
	private void UpdateGivenModel()
	{
		if ( !HasGivenWeapon || !_given.WorldModel.IsValid() ) return;

		var holder = _arms.IsValid() ? _arms : Renderer;
		if ( !holder.IsValid() || !holder.TryGetBoneTransform( RightHandBone, out var hand ) ) return;

		var local = new Transform( new Vector3( GivenX, GivenY, GivenZ ), Rotation.From( GivenPitch, GivenYaw, GivenRoll ) );
		_given.WorldModel.WorldTransform = hand.ToWorld( local );
	}

	/// <summary>
	/// Host: one shot (or swing) from the given weapon. At someone, the weapon aims at their chest
	/// itself. At a bare point (a player's aim, suppressing a spot) it gets a helper: the weapon aims
	/// at a target's chest - 42 above an object without eyes - so the helper is kept 42 under the
	/// point and the shot lands on it. Its own fire rate paces it.
	/// </summary>
	private bool FireGiven( Vector3 point, GameObject target )
	{
		if ( target.IsValid() )
		{
			_given.NpcAimTarget = target;
		}
		else
		{
			if ( !_givenAim.IsValid() )
			{
				_givenAim = new GameObject( true, "combine given weapon aim" );
				_givenAim.Flags |= GameObjectFlags.NotNetworked | GameObjectFlags.NotSaved | GameObjectFlags.Hidden;
			}

			_givenAim.WorldPosition = point - Vector3.Up * 42f;
			_given.NpcAimTarget = _givenAim;
		}

		if ( !_given.FirePrimary() ) return false;

		PlayAction( GivenIsMelee ? "melee" : Crouched ? AnimGun.ShootCrouchAnim : AnimGun.ShootAnim );
		if ( _sinceGunshotStimulus > 1f )
		{
			_sinceGunshotStimulus = 0;
			EmitStimulus( StimulusKind.Gunshot, radius: 2048f, lifetime: 1.5f );
		}
		return true;
	}
}
