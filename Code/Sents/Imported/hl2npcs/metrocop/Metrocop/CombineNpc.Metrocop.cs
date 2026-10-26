using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;
using Sandbox.Npcs;

namespace Hl2Metrocop;

public enum CopWeapon
{
	/// <summary>A pistol most of the time, else a stun baton or an SMG.</summary>
	Random,
	Pistol,
	Smg1,
	Stunstick,
}

/// <summary>
/// The Metrocop (HL2's npc_metropolice): a C# re-telling of how Half-Life 2's Civil Protection
/// choose what to do, on top of the soldier's movement, aiming, cover search, squads and animation.
/// Only the Metrocop package has these files; in it every CombineNpc is a cop.
/// Map-only HL2 behaviours are left out for now, to be done with NPC Wars: rappelling (the model
/// has rappel_loop/rappel_land), manning mounted guns, assault/standoff points, police zones
/// (ai_goal_police: harass, then a baton knockout with a white screen), and the long SMG
/// "stitch" bursts at a target in a vehicle or below him (SF_METROPOLICE_ALWAYS_STITCH).
/// </summary>
public sealed partial class CombineNpc
{
	// ---------------------------------------------------------------- settings

	[Property, Group( "Metrocop" )] public CopWeapon CopWeapon { get; set; } = CopWeapon.Random;

	/// <summary>Manhacks on his belt (HL2's "manhacks" key). -1: one, one cop in three.</summary>
	[Property, Group( "Metrocop" )] public int ManhackCount { get; set; } = -1;

	/// <summary>
	/// Start of HL2 ("gordon_precriminal"): doesn't fight the player, warns and shoves him instead,
	/// and only hits back when a thing is thrown at him. Off: hostile, as later in the game.
	/// </summary>
	[Property, Group( "Metrocop" )] public bool Calm { get; set; }

	/// <summary>SF_METROPOLICE_ALLOWED_TO_RESPOND: a calm cop hit by a thrown thing chases the thrower.</summary>
	[Property, Group( "Metrocop" )] public bool AllowedToRespond { get; set; } = true;

	/// <summary>SF_METROPOLICE_ARREST_ENEMY: "Freeze!" first, everyone draws, and they fire only if he runs or fights.</summary>
	[Property, Group( "Metrocop" )] public bool ArrestEnemy { get; set; }

	/// <summary>SF_METROPOLICE_SIMPLE_VERSION: an easier cop (26 health) with flinches from behind.</summary>
	[Property, Group( "Metrocop" )] public bool SimpleCop { get; set; }

	/// <summary>SF_METROPOLICE_MID_RANGE_ATTACK: sees and shoots out to 3500.</summary>
	[Property, Group( "Metrocop" )] public bool MidRangeAttack { get; set; }

	/// <summary>SF_METROPOLICE_NO_MANHACK_DEPLOY.</summary>
	[Property, Group( "Metrocop" )] public bool NoManhackDeploy { get; set; }

	/// <summary>SF_METROPOLICE_NOCHATTER: no idle chatter.</summary>
	[Property, Group( "Metrocop" )] public bool NoChatter { get; set; }

	[Property, Group( "Metrocop Sounds" )] public SoundEvent PistolFireSound { get; set; }
	[Property, Group( "Metrocop Sounds" )] public SoundEvent BatonSwingSound { get; set; }
	[Property, Group( "Metrocop Sounds" )] public SoundEvent BatonHitSound { get; set; }
	[Property, Group( "Metrocop Sounds" )] public SoundEvent BatonHitWorldSound { get; set; }
	[Property, Group( "Metrocop Sounds" )] public SoundEvent BatonSparkSound { get; set; }
	[Property, Group( "Metrocop Sounds" )] public SoundEvent ShoveSound { get; set; }
	[Property, Group( "Metrocop Sounds" )] public SoundEvent HidingSpeechSound { get; set; }
	[Property, Group( "Metrocop Sounds" )] public SoundEvent WaterSpeechSound { get; set; }

	/// <summary>Spawn new cops calm (1) or hostile (0); -1 leaves each one's own setting.</summary>
	[ConVar( "metrocop_calm", Help = "New Metrocops: 1 calm (warn and shove first, as at the start of HL2), 0 hostile, -1 the prefab's setting" )]
	public static int CalmOverride { get; set; } = -1;

	[ConVar( "metrocop_arrest", Help = "New Metrocops: 1 try to arrest first (\"Freeze!\"), 0 no, -1 the prefab's setting" )]
	public static int ArrestOverride { get; set; } = -1;

	[ConVar( "metrocop_weapon", Help = "New Metrocops get this weapon: pistol, smg, baton (empty: random)" )]
	public static string WeaponOverride { get; set; } = "";

	[ConVar( "metrocop_manhacks", Help = "Manhacks every new Metrocop carries (-1: one cop in three has one)" )]
	public static int ManhacksOverride { get; set; } = -1;

	// ---------------------------------------------------------------- HL2 numbers (npc_metropolice.cpp, skill.cfg)

	public const float CopHealth = 40f;                 // sk_metropolice_health
	public const float SimpleCopHealth = 26f;           // sk_metropolice_simple_health
	public const float MidRange = 3500f;                // METROPOLICE_MID_RANGE_ATTACK_RANGE
	public const int MaxWarnings = 3;                   // METROPOLICE_MAX_WARNINGS
	public const float RecentDamageInterval = 3f;       // RECENT_DAMAGE_INTERVAL
	public const float RecentDamageThreshold = 0.2f;    // RECENT_DAMAGE_THRESHOLD
	public const float CopFreeKnowledge = 6f;           // GetEnemies()->SetFreeKnowledgeDuration( 6.0 )
	public const float BatonDamageNpc = 40f;            // sk_npc_dmg_stunstick ("kill a citizen in one hit")
	public const float SuitScale = 0.25f;               // a player in the HEV suit takes a quarter of a cop's melee
	public const float ShoveDamage = 15f;               // OnAnimEventShove

	// ---------------------------------------------------------------- state

	/// <summary>The pistol starts in its holster; drawing it is an animation of its own.</summary>
	[Sync] public bool WeaponDrawn { get; set; }

	/// <summary>The baton's sparking end is switched on.</summary>
	[Sync] public bool BatonOn { get; set; }

	[Sync] public int Manhacks { get; set; }

	public bool IsCop => true;
	// A weapon handed to him (CombineNpc.GivenWeapon.cs) is a gun in his hand, whatever his own was
	public bool HasBaton => ActiveWeapon == CombineWeaponType.Stunstick && !HasGivenWeapon;
	public bool HasGun => ActiveWeapon is CombineWeaponType.Pistol or CombineWeaponType.Smg1 || HasGivenWeapon;

	private bool _shouldActivateBaton;
	private TimeUntil _batonDebounce;
	private TimeUntil _batonSwing;

	/// <summary>Closer than this, a baton man running at someone aims his run a little past them (CopPursueTask).</summary>
	public const float RunThroughRange = 300f;

	/// <summary>How fast a baton man turns on someone right next to him (degrees a second).</summary>
	public const float CloseTurnSpeed = 450f;

	/// <summary>The time between baton swings (HL2's 1 to 1.75 seconds) is up.</summary>
	public bool SwingReady => _batonSwing;
	private TimeUntil _nextCharge;
	private int _recentDamage;
	private TimeSince _sinceRecentDamage = 100f;
	private TimeSince _sincePhysicsFlinch = 100f;
	private TimeUntil _nextPainSound;
	private bool _painLightSaid, _painHeavySaid;
	private TimeUntil _noFireUntil;
	private TimeUntil _restUntil;
	private int _copLastShootSlot = -1;
	private TimeUntil _yieldShootSlot;

	public GameObject CopEnemy { get; private set; }
	private readonly HashSet<GameObject> _copEluded = new();

	partial void OtherBrainActive( ref bool active ) => active = true;

	// ---------------------------------------------------------------- spawn

	/// <summary>Set the cop up once, on the host, right after the soldier's own start.</summary>
	private void CopStart()
	{
		Calm = CalmOverride switch { 0 => false, 1 => true, _ => Calm };
		ArrestEnemy = ArrestOverride switch { 0 => false, 1 => true, _ => ArrestEnemy };

		var weapon = WeaponOverride?.Trim().ToLowerInvariant() switch
		{
			"pistol" => CopWeapon.Pistol,
			"smg" or "smg1" => CopWeapon.Smg1,
			"baton" or "stunstick" => CopWeapon.Stunstick,
			_ => CopWeapon,
		};

		if ( weapon == CopWeapon.Random )
		{
			var r = Game.Random.Float();
			weapon = r < 0.55f ? CopWeapon.Pistol : r < 0.85f ? CopWeapon.Stunstick : CopWeapon.Smg1;
		}

		var type = weapon switch
		{
			CopWeapon.Smg1 => CombineWeaponType.Smg1,
			CopWeapon.Stunstick => CombineWeaponType.Stunstick,
			_ => CombineWeaponType.Pistol,
		};

		// One picked for him before he started (Warbox's weapon choice) wins over the roll
		ChosenWeapon( ref type );
		ActiveWeapon = type;

		// Anything but a pistol is out from the start (HL2's Spawn: m_fWeaponDrawn unless weapon_pistol)
		WeaponDrawn = ActiveWeapon != CombineWeaponType.Pistol;

		Manhacks = ManhacksOverride >= 0 ? ManhacksOverride
			: ManhackCount >= 0 ? ManhackCount
			: Game.Random.Float() < 1f / 3f ? 1 : 0;

		StartHealth = SimpleCop ? SimpleCopHealth : CopHealth;
		Health = StartHealth;
		Clip = ClipSize;
		GrenadesLeft = 0;
		Balls = 0;

		if ( MidRangeAttack && Senses.IsValid() ) Senses.SightRange = MathF.Max( Senses.SightRange, MidRange );

		_yieldShootSlot = 0f;
		_nextCharge = 0f;
		_batonSwing = 0f;
		MetrocopVoice.Init( this );
	}

	// ---------------------------------------------------------------- weapons

	partial void OtherWeaponStats( CombineWeaponType type, ref CombineWeaponStats stats )
	{
		var range = MidRangeAttack ? MidRange : 0f;
		switch ( type )
		{
			// weapon_pistol in a cop's hands: WEAPON_PROFICIENCY_POOR, 18 rounds, sk_npc_dmg_pistol 3
			case CombineWeaponType.Pistol:
				stats = new CombineWeaponStats
				{
					Name = "pistol", BodyGroup = 0,
					Damage = 3f, Spread = 4.5f, FireInterval = 0.2f, ClipSize = 18, Range = range > 0f ? range : 1500f,
					BurstMin = 2, BurstMax = 8, SingleShotAnim = true,
					ShootAnim = "shoot_pistol", ShootCrouchAnim = "shoot_pistol_crouch", CombatIdle = "combat_idle", RelaxedIdle = "idle",
					Flashes = new[] { "muzzleflash1", "muzzleflash2", "muzzleflash3", "muzzleflash4" }, FlashSize = 7f,
					TracerColor = new Color( 1f, 0.85f, 0.55f ), LightColor = new Color( 1f, 0.7f, 0.35f ),
				};
				break;

			// weapon_smg1: WEAPON_PROFICIENCY_VERY_GOOD, 45 rounds, sk_npc_dmg_smg1 3
			case CombineWeaponType.Smg1:
				stats = CombineWeaponStats.Smg1;
				stats = new CombineWeaponStats
				{
					Name = "smg1", BodyGroup = 1,
					Damage = 3f, Spread = 2f, FireInterval = stats.FireInterval, ClipSize = 45, Range = range > 0f ? range : 1400f,
					BurstMin = 2, BurstMax = 5,
					ShootAnim = "shoot_smg", ShootCrouchAnim = "shoot_smg_crouch", CombatIdle = "combat_idle", RelaxedIdle = "idle",
					Flashes = stats.Flashes, FlashSize = stats.FlashSize, TracerColor = stats.TracerColor, LightColor = stats.LightColor,
				};
				break;

			case CombineWeaponType.Stunstick:
				stats = new CombineWeaponStats
				{
					Name = "stunstick", BodyGroup = 2,
					Damage = 0f, ClipSize = 0, Range = 48f, FireInterval = 1f,
					ShootAnim = "swing", ShootCrouchAnim = "swing", CombatIdle = "combat_idle", RelaxedIdle = "idle",
					Flashes = Array.Empty<string>(),
				};
				break;
		}
	}

	partial void OtherWeaponSound( bool reload, ref SoundEvent sound )
	{
		if ( !reload && ActiveWeapon == CombineWeaponType.Pistol ) sound = PistolFireSound;
		// The NPC pistol reloads with the SMG's sound in HL2 (Weapon_Pistol.NPC_Reload)
		if ( reload && ActiveWeapon == CombineWeaponType.Pistol ) sound = Smg1ReloadSound;
		if ( HasBaton ) sound = null;
	}

	/// <summary>Body group shown for the weapon: a holstered pistol isn't in his hand.</summary>
	private int CopWeaponBodyGroup => HasGivenWeapon || ActiveWeapon == CombineWeaponType.Pistol && !WeaponDrawn ? CombineWeaponStats.NoWeaponBodyGroup : Gun.BodyGroup;

	// ---------------------------------------------------------------- animation names

	private static readonly Dictionary<string, float> CopStrides = new();

	/// <summary>
	/// The soldier code asks for "walk_ne", "reload", "combat_idle"...; the cop's model has a set per
	/// weapon (see make_metrocop_assets.py): u_ empty-handed or baton down, baton_ baton raised,
	/// pistol_ and smg_ with the gun held down (_easy) or up.
	/// </summary>
	partial void OtherAnimName( ref string name )
	{
		var set = !HasGun || !WeaponDrawn || AnimWeapon == CombineWeaponType.Stunstick ? "u" : AnimWeapon == CombineWeaponType.Pistol ? "pistol" : "smg";
		var angry = Alert || BatonOn;

		switch ( name )
		{
			case "idle":
			case "combat_idle":
				if ( set == "u" ) name = HasBaton && BatonOn ? "baton_combat_idle" : "u_idle";
				// HL2's NPC_TranslateActivity: a drawn weapon in combat (or the baton on) idles angry
				else name = $"{set}_{(angry ? "combat_idle" : "idle")}";
				return;
			case "crouch_idle":
				name = set == "u" ? "u_idle" : $"{set}_crouch_idle";
				return;
			case "reload":
				name = set == "smg" ? "reload_smg" : "reload_pistol";
				return;
			case "reload_low":
				name = set == "smg" ? "reload_smg_low" : "reload_pistol_low";
				return;
			case "melee":
				name = "swing";
				return;
			case "stand_to_crouch":
			case "crouch_to_stand":
				name = $"{name}_{(set == "smg" ? "smg" : "pistol")}";
				return;
		}

		// Locomotion: walk_ne / walk_easy_ne / run_ne / crouch_walk_ne / crouch_run
		var m = System.Text.RegularExpressions.Regex.Match( name, @"^(walk_easy|walk|run|crouch_walk|crouch_run)(_[nsew]{1,2})?$" );
		if ( !m.Success ) return;

		var gait = m.Groups[1].Value;
		var dir = m.Groups[2].Value;
		if ( gait == "crouch_run" ) gait = "crouch_walk";

		if ( set == "u" )
		{
			if ( gait.StartsWith( "crouch" ) ) name = "u_crouch_walk" + dir;
			else if ( gait == "run" ) name = "u_run" + dir;
			else name = (HasBaton && BatonOn ? "baton_walk" : "u_walk") + dir;
			return;
		}

		if ( gait.StartsWith( "crouch" ) ) name = $"{set}_crouch_walk{dir}";
		else if ( gait == "run" ) name = $"{set}_{(angry ? "run" : "run_easy")}{dir}";
		else if ( gait == "walk" ) name = $"{set}_{(angry ? "walk" : "walk_easy")}{dir}";
		else name = $"{set}_walk_easy{dir}";
	}

	partial void OtherStrideSpeed( string name, ref float speed )
	{
		var mapped = name;
		OtherAnimName( ref mapped );
		if ( CopStrides.Count == 0 )
		{
			foreach ( var (key, value) in MetrocopStrides.Speeds )
				CopStrides[key] = value;
		}

		if ( CopStrides.TryGetValue( mapped, out var stride ) && stride > 1f ) speed = stride;
	}

	partial void OtherFlinch( in DamageInfo damage, ref string name )
	{
		// HL2's simple cops have a flinch for being shot in the back, and a gut flinch for chest and legs
		if ( !SimpleCop ) return;
		var from = damage.Attacker.IsValid() ? damage.Attacker.WorldPosition : damage.Origin;
		var local = WorldTransform.PointToLocal( from );
		if ( local.x < 0f ) name = "flinch_behind";
		else if ( name is "flinch_chest" ) name = "flinch_gut";
	}

	partial void OtherRagdoll( SkinnedModelRenderer body ) => body.SetBodyGroup( "manhack", Manhacks > 0 ? 1 : 0 );

	/// <summary>Keep the model's body groups in step with the weapon state (every client).</summary>
	private void UpdateCopBodyGroups()
	{
		if ( !Renderer.IsValid() ) return;
		Renderer.SetBodyGroup( "weapon", CopWeaponBodyGroup );
		Renderer.SetBodyGroup( "manhack", Manhacks > 0 && !_manhackInHand ? 1 : 0 );
	}

	// ---------------------------------------------------------------- talking

	// The soldier code says things by meaning (CombineVoice lists); a cop says HL2's police lines instead
	partial void OtherBrainSay( string[] lines, ref bool handled )
	{
		handled = true;
		if ( lines == CombineVoice.CoverMe ) MetrocopVoice.Speak( this, "METROPOLICE_COVER_LOW_AMMO" );
		else if ( lines == CombineVoice.Cover || lines == CombineVoice.CoverHurt ) MetrocopVoice.Speak( this, "METROPOLICE_COVER_HEAVY_DAMAGE" );
		else if ( lines == CombineVoice.Danger ) MetrocopVoice.Speak( this, "METROPOLICE_DANGER", MetrocopVoice.High );
		// HL2's OnScheduleChange with COND_ENEMY_DEAD: the kill, called by what it was
		else if ( lines == CombineVoice.Kill ) MetrocopVoice.AnnounceEnemyKill( this, CopEnemy );
		// The rest (squad orders, contact reports) are the soldiers' radio drill: HL2's cops don't use it
	}

	// ---------------------------------------------------------------- being hurt, dying

	partial void OtherBrainHurt( in DamageInfo damageInfo )
	{
		var info = damageInfo;
		var damage = info.Damage;
		var attacker = info.Attacker;
		SinceCopHurt = 0;

		// SF_METROPOLICE_ARREST_ENEMY: any hit means he's resisting
		if ( ArrestEnemy ) EnemyResistingArrest();

		// Keep track of recent damage from the enemy: if it looks like we're being killed, hide (HL2)
		var root = attacker.IsValid() ? attacker.Root : null;
		if ( root is not null && root == CopEnemy )
		{
			_recentDamage += (int)damage;
			_sinceRecentDamage = 0;
		}

		CopPainSound();

		// After a flinch he can't shoot for half a second ("to ensure old playtested difficulty stays the same")
		_noFireUntil = 0.5f;

		// A calm cop: a thing thrown at him is "pick up that can"; a bullet makes the shooter a criminal
		if ( Calm )
		{
			var thrown = info.Tags.Contains( DamageTags.Impact ) || info.Tags.Contains( DamageTags.Crush );
			var player = root is not null && root.GetComponent<Player>().IsValid() ? root : null;
			if ( player is not null && !PlayerIsCriminal( player ) )
			{
				if ( thrown ) CalmHurtByPlayer( player );
				else ShotByPlayer( player );
			}
			else if ( player is null && thrown ) HitByThrownThing();
		}
	}

	/// <summary>Seconds since anything hurt him.</summary>
	public TimeSince SinceCopHurt { get; private set; } = 100f;

	private void CopPainSound()
	{
		if ( !_nextPainSound || IsOnFire ) return;
		var ratio = Health / MathF.Max( 1f, StartHealth );
		if ( ratio <= 0f ) return;

		var group = "METROPOLICE_PAIN";
		if ( !_painHeavySaid && ratio < 0.25f )
		{
			_painHeavySaid = _painLightSaid = true;
			group = "METROPOLICE_PAIN_HEAVY";
		}
		else if ( !_painLightSaid && ratio > 0.8f )
		{
			_painLightSaid = true;
			group = "METROPOLICE_PAIN_LIGHT";
		}

		MetrocopVoice.Speak( this, group, MetrocopVoice.Always );
		_nextPainSound = 1f;
	}

	partial void OtherBrainDied( in DamageInfo damage )
	{
		// A manhack halfway out of his hand still goes
		if ( _manhackInHand ) ReleaseManhack();

		Squad?.CopMemberDied( this );

		if ( !IsOnFire ) MetrocopVoice.Speak( this, "METROPOLICE_DIE", MetrocopVoice.Always );
	}

	public bool IsOnFire => GameObject.GetComponentInChildren<FireDamage>( true ).IsValid();

	// ---------------------------------------------------------------- the loop

	partial void OtherBrainStart() => CopStart();
	partial void OtherBrainThink() => CopThink();
	partial void OtherAnimationUpdate()
	{
		UpdateCopBodyGroups();
		UpdateBatonGlow();
	}

	/// <summary>Every frame on the host, before the schedule runs.</summary>
	private void CopThink()
	{
		if ( _sinceRecentDamage > RecentDamageInterval ) _recentDamage = 0;

		// The baton is switched off once there's been nothing to be angry at for a while
		if ( HasBaton && !Calm && _shouldActivateBaton && _batonDebounce && !CopEnemy.IsValid() )
			SetBatonState( false );

		MetrocopVoice.Tick( this );
		CalmThink();
		TickManhack();
		TickMoveAndShoot();
	}

	public void SetBatonState( bool on )
	{
		if ( !HasBaton || _shouldActivateBaton == on ) return;
		_shouldActivateBaton = on;
		if ( on ) _batonDebounce = Game.Random.Float( 2.5f, 4f );
	}

	public bool ShouldActivateBaton => _shouldActivateBaton;

	/// <summary>The baton's state flips with sparks and a sound (HL2: SetStunState), seen and heard by all.</summary>
	[Rpc.Broadcast( NetFlags.HostOnly )]
	public void BatonSwitched( bool on )
	{
		if ( !IsProxy ) BatonOn = on;
		if ( BatonSparkSound is not null ) Sound.Play( BatonSparkSound, BatonTip );
		if ( on ) MetrocopEffects.Sparks( Scene, BatonTip );
	}

	public Vector3 BatonTip => Renderer.IsValid() && Renderer.GetAttachment( "muzzle_stunstick" ) is { } t
		? t.Position
		: WorldPosition + Vector3.Up * 50f + WorldRotation.Forward * 20f;

	// ---------------------------------------------------------------- choosing (HL2's SelectSchedule)

	partial void OtherBrainSchedule( ref ScheduleBase schedule ) => schedule = CopGetSchedule();

	/// <summary>
	/// The sandbox also asks while a schedule is running, to see if something should interrupt it.
	/// As in HL2 each running schedule ends itself on its own interrupt conditions; only a live
	/// grenade breaks one off from outside.
	/// </summary>
	private ScheduleBase CopGetSchedule()
	{
		if ( ActiveSchedule is not null )
		{
			var grenade = FindDangerousGrenade();
			if ( grenade.IsValid() && ActiveSchedule is not CombineDodgeSchedule && ActiveSchedule is not ICopUninterruptible )
				return CopDodge( grenade );
			return ActiveSchedule;
		}

		Squad?.CopVacate( this );
		var schedule = CopSelect();
		if ( CombineSquad.Debug )
		{
			var e = CopEnemy;
			var why = e.IsValid() && HasBaton
				? $" (baton: {(e.WorldPosition - WorldPosition).WithZ( 0 ).Length:0} away, facing {Vector3.Dot( (e.WorldPosition - WorldPosition).WithZ( 0 ).Normal, WorldRotation.Forward.WithZ( 0 ).Normal ):0.00}, sees {CanSee( e )}, swing ready {(bool)_batonSwing})"
				: "";
			Log.Info( $"[cop] {GameObject.Id.ToString()[..4]} -> {schedule?.GetType().Name ?? "-"}{why}" );
		}
		return schedule;
	}

	private ScheduleBase CopDodge( GameObject grenade )
	{
		MetrocopVoice.AnnounceDanger( this, grenade );
		var dodge = GetSchedule<CombineDodgeSchedule>();
		dodge.Danger = grenade;
		return dodge;
	}

	private ScheduleBase CopSelect()
	{
		// On fire: "officer down, on fire" and burn where he stands
		if ( IsOnFire )
		{
			MetrocopVoice.Speak( this, "METROPOLICE_ON_FIRE", MetrocopVoice.Always );
			return GetSchedule<CopBurningSchedule>();
		}

		var grenade = FindDangerousGrenade();
		if ( grenade.IsValid() ) return CopDodge( grenade );

		// The calm cop's own business: a thing thrown at him, a player in his face, going back to his post
		if ( CalmSelect() is { } calm ) return calm;

		if ( HasBaton )
		{
			if ( _shouldActivateBaton && !BatonOn ) return CopSchedule<CopBatonSchedule>( null ).Switch( true );
			if ( !_shouldActivateBaton && BatonOn ) return CopSchedule<CopBatonSchedule>( null ).Switch( false );
		}

		// Cower when something is thrown at him (SOUND_PHYSICS_DANGER), at most every four seconds
		if ( _sincePhysicsFlinch > 4f && IncomingPhysicsObject() )
		{
			_sincePhysicsFlinch = 0;
			return GetSchedule<CopPhysicsFlinchSchedule>();
		}

		var record = CopChooseEnemy();
		var enemy = record?.Target;
		var newEnemy = enemy.IsValid() && enemy != CopEnemy;
		if ( CopEnemy.IsValid() && !enemy.IsValid() ) MetrocopVoice.LostEnemy( this, true );
		CopEnemy = enemy;

		var highHealth = Health / StartHealth > 0.75f;

		// Hurt and nearly out: run off backwards, shooting, to reload
		if ( !highHealth && !HasBaton && HasGun && Clip <= 5 && enemy.IsValid() )
		{
			MetrocopVoice.Speak( this, "METROPOLICE_COVER_LOW_AMMO" );
			return CopHideAndReload( record );
		}

		if ( HasGun && Clip <= 0 )
		{
			if ( highHealth || !enemy.IsValid() ) return GetSchedule<CombineReloadSchedule>();
			MetrocopVoice.AnnounceOutOfAmmo( this );
			return CopHideAndReload( record );
		}

		// Chasing a player who threw something at a calm cop
		if ( enemy.IsValid() && _chasePlayerUntil > 0f && !_chasePlayerUntil )
			return CopSchedule<CopChaseSchedule>( record );

		if ( record is not null )
		{
			if ( CopCombat( record, newEnemy ) is { } combat ) return combat;
		}
		else
		{
			if ( CopIdle() is { } idle ) return idle;
		}

		return enemy.IsValid() ? CopSchedule<CopCombatFaceSchedule>( record ) : GetSchedule<CopIdleSchedule>();
	}

	/// <summary>HL2's SelectCombatSchedule.</summary>
	private ScheduleBase CopCombat( EnemyRecord record, bool newEnemy )
	{
		var enemy = record.Target;

		if ( newEnemy )
		{
			MetrocopVoice.AnnounceEnemyType( this, enemy );
			_copEluded.Remove( enemy );
		}

		// ---- SelectScheduleNewEnemy
		// HL2 arrests only with a squad; in the sandbox squads form a moment after spawning, and a
		// lone cop would never arrest at all - so he leads it himself (the slot is his alone)
		if ( ArrestEnemy && CanSee( enemy ) )
		{
			if ( HasGun && !WeaponDrawn ) return CopSchedule<CopDrawPistolSchedule>( record );
			return CopOccupy( enemy, CopSlot.ArrestEnemy )
				? CopSchedule<CopWarnAndArrestSchedule>( record )
				: CopSchedule<CopArrestSchedule>( record );
		}

		if ( newEnemy && CanDeployManhack && CopOccupy( enemy, CopSlot.DeployManhack ) )
			return CopSchedule<CopDeployManhackSchedule>( record );

		if ( HasGun && !WeaponDrawn ) return CopSchedule<CopDrawPistolSchedule>( record );

		if ( HasBaton && !BatonOn && !_shouldActivateBaton )
		{
			SetBatonState( true );
			return CopSchedule<CopBatonSchedule>( record ).Switch( true );
		}

		// ---- taking too much damage: take cover (the baton men just keep coming)
		if ( !HasBaton && _recentDamage / StartHealth > RecentDamageThreshold )
		{
			_recentDamage = 0;
			MetrocopVoice.Speak( this, "METROPOLICE_COVER_HEAVY_DAMAGE", MetrocopVoice.Medium );
			if ( FindCover( record.Position ) is { } cover )
				return CopSchedule<CopTakeCoverSchedule>( record ).At( cover );
		}

		if ( CopCanRangeAttack( enemy ) )
		{
			if ( _restUntil ) return CopSelectRangeAttack( record );
			return CopAdvance( record );
		}

		if ( CopCanMelee( enemy ) )
		{
			if ( _batonSwing )
			{
				_chasePlayerUntil = 0f;
				_batonSwing = Game.Random.Float( 1f, 1.75f );
				return CopSchedule<CopMeleeSchedule>( record );
			}

			// Waiting for the next swing: right up against him he squares up; anywhere further he
			// keeps closing in, so the swing is ready the moment the timer is
			if ( !CanSee( enemy ) ) return CopSchedule<CopCombatFaceSchedule>( record );
			if ( (enemy.WorldPosition - WorldPosition).WithZ( 0 ).Length < 45f )
				return CopSchedule<CopCombatFaceSchedule>( record ).WaitingToSwing();
			return CopSchedule<CopChaseSchedule>( record );
		}

		if ( HasGun && (Clip <= 0 || Clip < ClipSize / 4 + 1) )
		{
			MetrocopVoice.AnnounceOutOfAmmo( this );
			return CopHideAndReload( record );
		}

		// Behind something that breaks: shoot it away (two cops at most)
		if ( HasGun && CanSee( enemy ) && BreakableOccluder( enemy ) is { } occluder
			&& CopOccupy( enemy, CopSlot.AttackOccluder1, CopSlot.AttackOccluder2 ) )
		{
			MetrocopVoice.Speak( this, "METROPOLICE_SHOOT_COVER" );
			return CopSchedule<CopShootCoverSchedule>( record ).Aim( occluder );
		}

		if ( !CanSee( enemy ) )
		{
			// Charge in and break the enemy's cover
			if ( !_copEluded.Contains( enemy ) && !enemy.Tags.Has( "notarget" ) )
				return CopEstablishLineOfFire( record );
		}

		// ---- SelectScheduleNoDirectEnemy
		if ( CanDeployManhack && CopOccupy( enemy, CopSlot.DeployManhack ) )
			return CopSchedule<CopDeployManhackSchedule>( record );

		if ( HasBaton && BlockingProp is { } prop )
		{
			BlockingProp = null;
			return CopSchedule<CopSmashPropSchedule>( record ).Aim( prop );
		}

		if ( _copEluded.Contains( enemy ) ) return null;
		return CopSchedule<CopChaseSchedule>( record );
	}

	private ScheduleBase CopSelectRangeAttack( EnemyRecord record )
	{
		var enemy = record.Target;

		// Two may shoot at once; a cop gives his turn up after 2-6 s so others get theirs
		if ( TryEnterShootSlot( enemy, CopSlot.Attack1 ) || TryEnterShootSlot( enemy, CopSlot.Attack2 ) )
			return CopSchedule<CopRangeAttackSchedule>( record );

		_copLastShootSlot = -1;

		if ( CanDeployManhack && CopOccupy( enemy, CopSlot.DeployManhack ) )
			return CopSchedule<CopDeployManhackSchedule>( record );

		return CopAdvance( record );
	}

	private bool TryEnterShootSlot( GameObject enemy, CopSlot slot )
	{
		if ( (_copLastShootSlot != (int)slot || !_yieldShootSlot) && CopOccupy( enemy, slot ) )
		{
			if ( _copLastShootSlot != (int)slot )
			{
				_yieldShootSlot = Game.Random.Float( 2f, 6f );
				_copLastShootSlot = (int)slot;
			}

			return true;
		}

		return false;
	}

	/// <summary>SCHED_METROPOLICE_ADVANCE - or, for a pistol man now and then, a short charge at him (metropolice_charge).</summary>
	private ScheduleBase CopAdvance( EnemyRecord record )
	{
		if ( _nextCharge && ActiveWeapon == CombineWeaponType.Pistol && record.Target.IsValid()
			&& record.Target.WorldPosition.Distance( WorldPosition ) > 300f && CopOccupy( record.Target, CopSlot.ChargeEnemy ) )
		{
			_nextCharge = Game.Random.Float( 3f, 7f );
			return CopSchedule<CopChargeSchedule>( record );
		}

		return CopSchedule<CopAdvanceSchedule>( record );
	}

	private ScheduleBase CopHideAndReload( EnemyRecord record )
	{
		if ( record?.Target is { } enemy && FindCover( record.Position ) is { } cover )
			return CopSchedule<CopTakeCoverSchedule>( record ).At( cover ).Reloading();
		return GetSchedule<CombineReloadSchedule>();
	}

	private ScheduleBase CopEstablishLineOfFire( EnemyRecord record )
	{
		if ( CopFindLineOfFire( record.Target ) is { } spot )
			return CopSchedule<CopEstablishLineOfFireSchedule>( record ).At( spot );

		// SCHED_FAIL_ESTABLISH_LINE_OF_FIRE: nowhere to see him from - he's lost
		_copEluded.Add( record.Target );
		MetrocopVoice.LostEnemy( this, false );
		return null;
	}

	/// <summary>No enemy in mind: look into a fight he hears (one cop per squad), turn to noises, idle.</summary>
	private Vector3? _checkedNoise;
	private TimeSince _sinceCheckedNoise = 100f;

	private Vector3? _heardFeet;
	private TimeSince _sinceHeardFeet;

	/// <summary>
	/// What the ears in the shared code pick up and the sandbox's disturbances don't: someone running
	/// close by out of sight (HL2's COND_HEAR_PLAYER). Before, the cop dropped it, and someone running
	/// round behind him went unnoticed - unlike the soldiers.
	/// </summary>
	public void CopHeard( Vector3 position )
	{
		_heardFeet = position;
		_sinceHeardFeet = 0;
	}

	/// <summary>Where the latest noise came from: a shot, a blast or a death (the sandbox's), or feet heard.</summary>
	public Vector3? CopNoise => Senses.Disturbance?.Position ?? (_heardFeet is { } feet && _sinceHeardFeet < 2f ? feet : null);

	private ScheduleBase CopIdle()
	{
		if ( WantsReload( 0.6f ) && HasGun ) return GetSchedule<CombineReloadSchedule>();

		// A noise not already looked into (HL2 checks each sound once)
		if ( CopNoise is { } noise && PlayerIsCriminal( null )
			&& !(_checkedNoise is { } done && done.Distance( noise ) < 128f && _sinceCheckedNoise < 15f) )
		{
			if ( Squad is null || Squad.CopOccupy( this, null, CopSlot.InvestigateSound ) )
			{
				_checkedNoise = noise;
				_sinceCheckedNoise = 0;
				var investigate = GetSchedule<CopInvestigateSchedule>();
				investigate.Spot = noise;
				return investigate;
			}

			if ( !IsFacing( noise, 30f ) )
			{
				var face = GetSchedule<CopAlertFaceSchedule>();
				face.Spot = noise;
				return face;
			}
		}

		return null;
	}

	private T CopSchedule<T>( EnemyRecord record ) where T : CopSchedule, new()
	{
		var schedule = GetSchedule<T>();
		schedule.Target = record?.Target;
		schedule.Threat = record?.Position ?? WorldPosition;
		schedule.Configure();
		return schedule;
	}

	public bool CopOccupy( GameObject enemy, CopSlot first, CopSlot last ) =>
		Squad is null || Squad.Alive.Count() < 2 || Squad.CopOccupy( this, enemy, first, last );

	public bool CopOccupy( GameObject enemy, CopSlot slot ) => CopOccupy( enemy, slot, slot );

	// ---------------------------------------------------------------- conditions

	/// <summary>Who to fight: someone in sight, else the freshest one the squad knows of (HL2's free knowledge: 6 s).</summary>
	private EnemyRecord CopChooseEnemy()
	{
		var squad = Squad;
		if ( squad is null ) return null;

		foreach ( var gone in _copEluded.Where( e => !e.IsValid() || CanSee( e ) ).ToList() )
			_copEluded.Remove( gone );

		var visible = CopBestVisible();
		if ( visible.IsValid() && PlayerIsCriminal( visible ) )
		{
			if ( !squad.Enemies.ContainsKey( visible ) ) squad.Sighted( this, visible, visible.WorldPosition );
			if ( squad.Enemies.TryGetValue( visible, out var seen ) ) return seen;
		}

		return squad.Enemies.Values
			.Where( e => e.Target.IsValid() && !IsDeadTarget( e.Target ) && PlayerIsCriminal( e.Target ) && e.SinceSeen < CombineSquad.SearchTime )
			.OrderBy( e => (float)e.SinceSeen )
			.FirstOrDefault();
	}

	public GameObject CopBestVisible()
	{
		var best = Senses.GetBestTarget();
		if ( best.IsValid() && !IsDeadTarget( best ) ) return best;
		return Senses.VisibleTargets
			.Where( t => t.IsValid() && !IsDeadTarget( t ) )
			.OrderBy( t => t.WorldPosition.Distance( WorldPosition ) )
			.FirstOrDefault();
	}

	/// <summary>COND_CAN_RANGE_ATTACK1: gun out, in sight and range, roughly facing him, rounds in it, nobody of ours in the way.</summary>
	public bool CopCanRangeAttack( GameObject enemy )
	{
		if ( !HasGun || !WeaponDrawn || Clip <= 0 || !enemy.IsValid() || !CanSee( enemy ) ) return false;
		var distance = WorldPosition.Distance( enemy.WorldPosition );
		if ( distance > EngageRange ) return false;
		if ( !IsFacing( enemy.WorldPosition, 60f ) && distance > 200f ) return false;
		return CheckLineOfFire( enemy ) == LineOfFire.Clear;
	}

	/// <summary>
	/// The stun baton's reach (weapon_stunstick.cpp): 48 units, judged on where he'll be a moment
	/// from now; not more than 70 up or down; roughly in front.
	/// </summary>
	public bool CopCanMelee( GameObject enemy )
	{
		if ( !HasBaton || !enemy.IsValid() || IsDeadTarget( enemy ) ) return false;
		var velocity = Sandbox.Npcs.Layers.SensesLayer.GetVelocity( enemy );
		var lead = MathF.Max( 0f, 0.1f + Game.Random.Float( -0.3f, 0.2f ) );
		var ahead = enemy.WorldPosition + velocity * lead;
		var delta = ahead - WorldPosition;
		if ( MathF.Abs( delta.z ) > 70f ) return false;

		var forward = WorldRotation.Forward.WithZ( 0 ).Normal;
		var flatNow = (enemy.WorldPosition - WorldPosition).WithZ( 0 );
		var flatAhead = delta.WithZ( 0 );
		// Roughly in front - or anywhere this side of him when right up close: the swing turns him onto
		// the target as it goes, so someone circling him can't stay out of it by never being dead ahead
		var front = flatNow.Length < 90f ? 0f : 0.7f;
		if ( Vector3.Dot( flatNow.Normal, forward ) < front && Vector3.Dot( flatAhead.Normal, forward ) < front ) return false;

		// A player who'll be within 48 in a third of a second gets it
		if ( enemy.GetComponent<Player>().IsValid() && (enemy.WorldPosition + velocity * 0.35f - WorldPosition).WithZ( 0 ).Length <= 48f )
			return true;

		// HL2's 48 between centres, plus a little: s&box bodies can't get as close as HL2's. No further
		// than the swing reaches (32 ahead + its 16 half-width + about the target's own half-width)
		// Running in, he starts it a step sooner: he steps in with the swing (CopSwingTask)
		var reach = Navigation.IsMoving ? 95f : 58f;
		return flatNow.Length <= reach || flatAhead.Length <= reach;
	}

	/// <summary>Is a breakable thing (a prop with health) all that stands between him and the enemy?</summary>
	private GameObject BreakableOccluder( GameObject enemy )
	{
		var tr = Scene.Trace.Ray( MuzzlePosition, AimPoint( enemy ) )
			.IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( "trigger", "ragdoll", "playercontroller" )
			.Run();
		if ( !tr.Hit || !tr.GameObject.IsValid() ) return null;
		var prop = tr.GameObject.Root.GetComponent<Prop>();
		return prop.IsValid() && prop.Health > 0f ? prop.GameObject : null;
	}

	/// <summary>A breakable prop he bumped into on his way (HL2's OnObstructionPreSteer).</summary>
	public GameObject BlockingProp { get; set; }

	/// <summary>Something thrown is coming at him fast (HL2: SOUND_PHYSICS_DANGER from a launched object).</summary>
	private bool IncomingPhysicsObject()
	{
		foreach ( var body in Scene.FindInPhysics( new Sphere( WorldPosition + Vector3.Up * 40f, 350f ) ) )
		{
			if ( !body.IsValid() ) continue;
			var rb = body.GetComponent<Rigidbody>();
			if ( !rb.IsValid() || !rb.MotionEnabled || rb.Velocity.Length < 400f ) continue;
			if ( body.Tags.Has( "player" ) || body.Tags.Has( "npc" ) || body.Tags.Has( "ragdoll" ) ) continue;
			var to = (WorldPosition + Vector3.Up * 40f - body.WorldPosition).Normal;
			if ( Vector3.Dot( rb.Velocity.Normal, to ) > 0.8f ) return true;
		}

		return false;
	}

	/// <summary>HL2's FindLos, as in the soldier's HL2 brain: a step to the side, then rings outwards.</summary>
	public Vector3? CopFindLineOfFire( GameObject enemy )
	{
		if ( !enemy.IsValid() ) return null;
		var eye = EyesOf( enemy );

		bool Sees( Vector3 spot )
		{
			if ( spot.Distance( eye ) > EngageRange ) return false;
			var tr = Scene.Trace.Ray( spot + Vector3.Up * 60f, eye )
				.IgnoreGameObjectHierarchy( GameObject )
				.WithoutTags( "trigger", "ragdoll", "playercontroller", "npc", "player" )
				.Run();
			return !tr.Hit;
		}

		var toEnemy = (enemy.WorldPosition - WorldPosition).WithZ( 0 ).Normal;
		var left = new Vector3( -toEnemy.y, toEnemy.x, 0f );
		foreach ( var step in new[] { 48f, 96f, 144f } )
		{
			foreach ( var side in new[] { left, -left } )
			{
				if ( Scene.NavMesh.GetClosestPoint( WorldPosition + side * step, 64f ) is { } spot && Sees( spot ) && CanWalkTo( spot ) )
					return spot;
			}
		}

		for ( var radius = 192f; radius <= 1536f; radius += 192f )
		{
			var candidates = new List<Vector3>();
			var steps = (int)MathF.Max( 8, radius / 96f );
			for ( var i = 0; i < steps; i++ )
			{
				var direction = Rotation.FromYaw( 360f / steps * i ).Forward;
				if ( Scene.NavMesh.GetClosestPoint( WorldPosition + direction * radius, 96f ) is { } spot && Sees( spot ) )
					candidates.Add( spot );
			}

			foreach ( var spot in candidates.OrderBy( s => s.Distance( enemy.WorldPosition ) ) )
			{
				if ( CanWalkTo( spot ) ) return spot;
			}
		}

		return null;
	}

	// ---------------------------------------------------------------- shooting rhythm (HL2's shot regulator)

	/// <summary>
	/// How many shots this burst, and the pause after it. A pistol cop fires 2-4 to 5-8 shots,
	/// more the closer he is (15 to 150 feet), 0.2-0.5 s apart, then rests 0.6-1.2 to 1.2-2 s.
	/// </summary>
	public (int Shots, float Interval, float Pause) NextBurst( GameObject enemy )
	{
		if ( ActiveWeapon == CombineWeaponType.Pistol )
		{
			var distance = enemy.IsValid() ? WorldPosition.Distance( enemy.WorldPosition ) : 15f * 12f;
			var factor = (Math.Clamp( distance, 15f * 12f, 150f * 12f ) - 15f * 12f) / (135f * 12f);
			var minBurst = (int)(2 + (4 - 2) * (1f - factor));
			var maxBurst = (int)(5 + (8 - 5) * (1f - factor));
			var minRest = 0.6f + (1.2f - 0.6f) * factor;
			var maxRest = 1.2f + (2.0f - 1.2f) * factor;
			return (Game.Random.Int( minBurst, maxBurst ), Game.Random.Float( 0.2f, 0.5f ), Game.Random.Float( minRest, maxRest ));
		}

		// SMG1: its own burst (2-5) at its own rate, a short rest
		return (Game.Random.Int( Gun.BurstMin, Gun.BurstMax ), Gun.FireInterval, Game.Random.Float( 0.3f, 0.6f ));
	}

	public void StartRest( float seconds ) => _restUntil = seconds;

	private int _burstLeft;
	private float _burstPause;
	public float BurstInterval { get; private set; } = 0.2f;

	/// <summary>Start a new burst unless one is still going.</summary>
	public void EnsureBurst( GameObject enemy )
	{
		if ( _burstLeft > 0 ) return;
		(_burstLeft, var interval, _burstPause) = NextBurst( enemy );
		BurstInterval = interval;
	}

	/// <summary>One shot of the burst is gone; true when that was the last, and the rest begins.</summary>
	public bool BurstShotFired()
	{
		if ( --_burstLeft > 0 ) return false;
		StartRest( _burstPause );
		return true;
	}
	public bool CanFireNow => _noFireUntil;
}
