using System;
using System.Linq;
using Sandbox;
using Sandbox.Npcs;

namespace Hl2Metrocop;

/// <summary>
/// A player playing as this soldier (Warbox). The AI stands aside and the host runs him from the
/// player's controls: the same gun, magazine and reloads, grenades, AR2 balls and rifle butt.
/// Warbox finds these properties by name (its NPC_COMPATIBILITY.md) and sets them on the host;
/// it runs the camera and hides the body for that player.
/// </summary>
public sealed partial class CombineNpc
{
	private const float PilotWalkSpeed = 100f;
	private const float CrouchSpeed = 95f;
	private const float StandHeight = 72f;
	private const float CrouchHeight = 50f;

	/// <summary>True while a player plays as him.</summary>
	public bool WarboxControlled
	{
		get => _controlled;
		set
		{
			if ( _controlled == value || IsProxy ) return;
			if ( value ) PilotStart();
			else PilotStop();
		}
	}

	public Vector3 WarboxMove { get; set; }
	public Vector3 WarboxAim { get; set; }
	public bool WarboxFire { get; set; }

	/// <summary>Right mouse: the AR2's energy ball; with any other gun, a grenade.</summary>
	public bool WarboxFire2 { get; set; }
	public bool WarboxJump { get; set; }
	public bool WarboxRun { get; set; }
	public bool WarboxReload { get; set; }
	public bool WarboxDuck { get; set; }
	public bool WarboxGrenade { get; set; }

	/// <summary>The rifle butt.</summary>
	public bool WarboxUse { get; set; }

	/// <summary>
	/// The camera: just in front of his face, following the neck through every pose (the aim
	/// stance leans the head well forward of his feet, kneeling drops it). The model has no
	/// "eyes" attachment.
	/// </summary>
	public Transform WarboxEyes
	{
		get
		{
			if ( Renderer.IsValid() && Renderer.TryGetBoneTransform( NeckBone, out var neck ) )
				return new Transform( neck.Position + Vector3.Up * 7f + WorldRotation.Forward * 7f, WorldRotation );

			return new Transform( WorldPosition + Vector3.Up * (Crouched ? 40f : 64f) + WorldRotation.Forward * 8f, WorldRotation );
		}
	}

	private const string NeckBone = "ValveBiped_Bip01_Neck1";

	// ---------------------------------------------------------------- first person, on the player's own screen

	/// <summary>Testing without Warbox: the camera of whoever plays as a soldier goes in his head.</summary>
	[ConVar( "metrocop_pilot_view" )]
	public static bool PilotView { get; set; }

	/// <summary>Tuning the first-person arms: extra pitch (degrees, + raises the gun) and offset from the eyes (forward, left, up).</summary>
	[ConVar( "metrocop_fp_pitch" )] public static float FpPitch { get; set; } = 20f;
	[ConVar( "metrocop_fp_x" )] public static float FpX { get; set; } = 0f;
	[ConVar( "metrocop_fp_y" )] public static float FpY { get; set; } = 0f;
	[ConVar( "metrocop_fp_z" )] public static float FpZ { get; set; } = 0f;

	private SkinnedModelRenderer _arms;
	private CombineWeaponType? _armsWeapon;
	private bool _armsGiven;
	private bool _bodyHidden;

	protected override void OnPreRender()
	{
		base.OnPreRender();

		var camera = Scene.Camera;
		if ( PilotView && Piloted && camera.IsValid() )
		{
			var eyes = WarboxEyes.Position;
			camera.WorldPosition = eyes;
			camera.WorldRotation = Rotation.LookAt( WarboxAim - eyes );
		}

		UpdateFirstPerson( camera );
		UpdateGivenModel();
	}

	/// <summary>
	/// Whoever's camera sits in his head (the player playing as him) sees his arms and gun the way
	/// HL2 shows a weapon: in front of the view, turning up and down with it. His own body only
	/// casts its shadow there - its arms hold the gun at the hip and never look up or down, so
	/// from the eyes that's a floating hand and no gun. The arms are a second copy of him playing
	/// the same frame of the same animation, tilted with the view; nobody else sees it.
	/// </summary>
	private void UpdateFirstPerson( CameraComponent camera )
	{
		if ( !Renderer.IsValid() ) return;

		// Only the player actually playing as him - not anyone whose camera happens to be in his head
		// (walking right into him in the sandbox put the player's own eyes there)
		var mine = PilotActive && (PilotConnection == Guid.Empty || Connection.Local is { } me && me.Id == PilotConnection);
		var inside = IsAlive && mine && camera.IsValid() && camera.WorldPosition.Distance( WarboxEyes.Position ) < 24f;
		if ( _bodyHidden != inside )
		{
			_bodyHidden = inside;
			Renderer.RenderType = inside ? ModelRenderer.ShadowRenderType.ShadowsOnly : ModelRenderer.ShadowRenderType.On;
		}

		if ( !inside )
		{
			RemoveArms();
			return;
		}

		if ( !_arms.IsValid() || _armsWeapon != ActiveWeapon || _armsGiven != HasGivenWeapon ) CreateArms();

		// Shots, reloads, throws: the same frame of the same animation as the body. Otherwise the gun
		// is held up and aimed (the first frame of his shot) - his idles and walks carry it low
		var body = Renderer.Sequence;
		var acting = _action is not null;
		var name = acting ? body.Name : AimPose;
		if ( _arms.Sequence.Name != name )
		{
			_arms.Sequence.Name = name;
			_arms.Sequence.Looping = acting && body.Looping;
		}
		_arms.Sequence.Time = acting ? body.Time : 0f;

		// Where his eyes are in the copy's own pose, put on the camera, tilted with the view
		var view = camera.WorldRotation.Angles();
		var rotation = Rotation.From( view.pitch - FpPitch, view.yaw, 0f );
		var eyesLocal = Vector3.Up * 71f + Vector3.Forward * 7f;
		if ( _arms.TryGetBoneTransform( NeckBone, out var neck ) )
			eyesLocal = _arms.WorldTransform.PointToLocal( neck.Position ) + Vector3.Up * 7f + Vector3.Forward * 7f;

		_arms.WorldRotation = rotation;
		_arms.WorldPosition = camera.WorldPosition - rotation * eyesLocal + rotation * new Vector3( FpX, FpY, FpZ );
	}

	private string AimPose
	{
		get
		{
			// The shotgun's own aim has the stock against his cheek, seen end on from the eyes: the rifle hold shows it
			var name = AnimWeapon == CombineWeaponType.Shotgun ? CombineWeaponStats.Ar2.ShootAnim : AnimGun.ShootAnim;
			OtherAnimName( ref name );
			return name;
		}
	}

	private void CreateArms()
	{
		RemoveArms();

		var go = new GameObject( true, "combine first person" );
		go.Flags |= GameObjectFlags.NotNetworked | GameObjectFlags.NotSaved | GameObjectFlags.Hidden;

		_arms = go.AddComponent<SkinnedModelRenderer>();
		_arms.CopyFrom( Renderer );
		_arms.UseAnimGraph = false;
		_arms.PlaybackRate = 0f;
		_arms.RenderType = ModelRenderer.ShadowRenderType.Off;
		_armsWeapon = ActiveWeapon;
		_armsGiven = HasGivenWeapon;
		if ( HasGivenWeapon ) _arms.SetBodyGroup( "weapon", CombineWeaponStats.NoWeaponBodyGroup );
	}

	private void RemoveArms()
	{
		if ( _arms.IsValid() ) _arms.GameObject.Destroy();
		_arms = null;
	}

	/// <summary>The barrel of the first-person gun on this screen, if this is the player playing as him.</summary>
	private Vector3? FirstPersonMuzzle => _arms.IsValid() && _arms.GetAttachment( "muzzle_" + Gun.Name ) is { } t ? t.Position : null;

	/// <summary>What the player's HUD shows besides health: one "label\tvalue" per line.</summary>
	[Sync] public string WarboxHud { get; set; }

	private bool _controlled;

	/// <summary>Synced to everyone: a player plays as him, and which one (empty when not known).</summary>
	[Sync] public bool PilotActive { get; set; }
	[Sync] public Guid PilotConnection { get; set; }
	private CharacterController _pilotBody;
	private TimeUntil _pilotBusy;
	private TimeUntil _pilotReloadDone;
	private bool _pilotReloading;
	private bool _wasReload, _wasGrenade, _wasUse, _wasFire2, _wasJump, _wasFire2Before;

	public bool Piloted => _controlled;

	private void PilotStart()
	{
		if ( !IsAlive ) return;
		_controlled = true;
		PilotActive = true;

		// Warbox asks from its request RPC, so the caller is the player who'll be playing as him
		PilotConnection = Rpc.Caller?.Id ?? Guid.Empty;

		EndCurrentSchedule();
		Navigation.Stop();
		Navigation.Enabled = false;
		if ( GetComponent<NavMeshAgent>() is { } agent ) agent.Enabled = false;
		Animation.StopLooking();

		_pilotBody = AddComponent<CharacterController>();
		_pilotBody.Flags |= ComponentFlags.NotNetworked;
		_pilotBody.Radius = 14f;
		_pilotBody.Height = Crouched ? CrouchHeight : StandHeight;
		_pilotBody.StepHeight = 18f;
		_pilotBody.Acceleration = 10f;
		_pilotBody.IgnoreLayers.Add( "trigger" );
		_pilotBody.IgnoreLayers.Add( "ragdoll" );

		_wasReload = _wasGrenade = _wasUse = _wasFire2 = _wasJump = false;
		OtherPilotStart();
		UpdatePilotHud();
	}

	// What another NPC built on this code (the Metrocop) does with the player's keys: his own start
	// (the pistol out, the baton on), his own attacks, his own HUD lines
	partial void OtherPilotStart();
	partial void OtherPilotAct( bool fire, bool fire2, bool use, ref bool handled );
	partial void OtherPilotHud( ref string hud );

	private void PilotStop()
	{
		_controlled = false;
		PilotActive = false;
		PilotConnection = Guid.Empty;
		if ( _pilotReloading ) Reloaded();
		_pilotReloading = false;
		SetReloading( 0f );
		WarboxHud = null;

		if ( _pilotBody.IsValid() ) _pilotBody.Destroy();
		_pilotBody = null;

		if ( GetComponent<NavMeshAgent>( includeDisabled: true ) is { } agent )
		{
			agent.Enabled = true;
			agent.Stop();
		}
		Navigation.Enabled = true;
		Navigation.Stop();
		Animation.StopLooking();
		Reconsider();
	}

	/// <summary>Is he walking about right now (the AI or a player) - shots spread more on the move.</summary>
	private bool MovingNow => _pilotBody.IsValid() ? _pilotBody.Velocity.WithZ( 0 ).Length > 20f : Navigation.IsMoving;

	/// <summary>Host, every frame while a player plays as him.</summary>
	private void PilotThink()
	{
		RechargeBalls();
		Alert = true;
		Crouched = WarboxDuck;

		var eye = EyePosition;
		var look = WarboxAim - eye;
		var view = look.LengthSquared > 1f ? look.Normal : WorldRotation.Forward;

		PilotMove( view );
		Animation.LookAt( eye + view * 500f );

		if ( _pilotReloading && _pilotReloadDone )
		{
			_pilotReloading = false;
			Reloaded();
		}

		var reload = WarboxReload && !_wasReload;
		var grenade = WarboxGrenade && !_wasGrenade || WarboxFire2 && !_wasFire2 && (ActiveWeapon != CombineWeaponType.Ar2 || HasGivenWeapon);
		var ball = WarboxFire2 && !_wasFire2 && ActiveWeapon == CombineWeaponType.Ar2 && !HasGivenWeapon;
		var melee = WarboxUse && !_wasUse;
		_wasFire2Before = _wasFire2;
		_wasReload = WarboxReload;
		_wasGrenade = WarboxGrenade;
		_wasFire2 = WarboxFire2;
		_wasUse = WarboxUse;

		var other = false;
		if ( _pilotBusy && !IsReloading ) OtherPilotAct( WarboxFire, WarboxFire2 && !_wasFire2Before, melee, ref other );

		if ( !other && _pilotBusy && !IsReloading )
		{
			if ( reload && Clip < ClipSize ) PilotReload();
			else if ( grenade && GrenadesLeft > 0 ) PilotGrenade();
			else if ( melee ) PilotMelee();
			else if ( ball && Balls > 0 ) PilotBall();
			else if ( WarboxFire )
			{
				// Out of rounds: reload by itself, as a player's gun does
				if ( Clip <= 0 ) PilotReload();
				else TryShootAt( WarboxAim, 1f );
			}
		}

		UpdatePilotHud();
	}

	private void PilotMove( Vector3 view )
	{
		var body = _pilotBody;
		if ( !body.IsValid() ) return;

		// The NPC code (a physgun letting go) can switch the agent back on: it would drag him back to the navmesh
		if ( GetComponent<NavMeshAgent>() is { } agent && agent.Enabled ) agent.Enabled = false;

		WorldRotation = Rotation.FromYaw( view.EulerAngles.yaw );
		body.Height = Crouched ? CrouchHeight : StandHeight;

		// Feet planted while throwing or swinging, as the AI does
		var speed = Crouched ? CrouchSpeed : WarboxRun ? RunSpeed : PilotWalkSpeed;
		var wish = _pilotBusy ? WarboxMove.WithZ( 0 ).ClampLength( 1f ) * speed : Vector3.Zero;
		var gravity = Scene.PhysicsWorld.Gravity;
		var jump = WarboxJump && !_wasJump;
		_wasJump = WarboxJump;

		if ( body.IsOnGround )
		{
			body.Velocity = body.Velocity.WithZ( 0 );
			body.Accelerate( wish );
			body.ApplyFriction( 4f );

			if ( jump && !Crouched && _pilotBusy )
				body.Punch( Vector3.Up * 280f );
		}
		else
		{
			body.Velocity += gravity * Time.Delta * 0.5f;
			body.Accelerate( wish.ClampLength( 50f ) );
			body.ApplyFriction( 0.1f );
		}

		body.Move();

		if ( !body.IsOnGround ) body.Velocity += gravity * Time.Delta * 0.5f;
		else body.Velocity = body.Velocity.WithZ( 0 );

		Animation.SetMove( body.Velocity.WithZ( 0 ), WorldRotation );
		Animation.Grounded = body.IsOnGround;
	}

	private void PilotReload()
	{
		if ( Clip >= ClipSize || _pilotReloading ) return;

		var duration = ReloadDuration( PlayAction( Crouched ? "reload_low" : "reload" ) );
		SetReloading( duration + 0.1f );
		PlayReloadSound();
		_pilotReloading = true;
		_pilotReloadDone = duration * 0.8f;

		// "Cover me!" - his squad hears it from a player just the same
		if ( Squad is not null && Enemy.IsValid() ) Squad.RequestCover( this );
	}

	private void PilotGrenade()
	{
		var duration = PlayAction( "grenade_throw" );
		_pilotBusy = duration;
		Say( CombineVoice.Grenade, force: true );

		// Leaves the hand partway through the throw, at wherever he's looking by then
		Invoke( duration * 0.45f, () =>
		{
			if ( !IsValid || !IsAlive || !Piloted || GrenadesLeft <= 0 ) return;
			ThrowGrenade( GrenadeAim() );
		} );
	}

	/// <summary>Where a thrown grenade should land: what he aims at, no further than an arm throws (HL2's 1100).</summary>
	private Vector3 GrenadeAim()
	{
		var eye = EyePosition;
		if ( WarboxAim.Distance( eye ) <= 1100f ) return WarboxAim;

		var end = eye + (WarboxAim - eye).Normal * 1100f;
		var down = Scene.Trace.Ray( end, end + Vector3.Down * 2000f )
			.WithoutTags( "trigger", "playercontroller", "player", "npc" )
			.Run();
		return down.Hit ? down.EndPosition : end;
	}

	private void PilotMelee()
	{
		var duration = PlayAction( "melee" );
		_pilotBusy = duration;

		Invoke( duration * 0.4f, () =>
		{
			if ( !IsValid || !IsAlive ) return;
			var target = MeleeTarget();
			if ( target.IsValid() ) MeleeHit( target );
			else _sinceMelee = 0;
		} );
	}

	/// <summary>Whoever is closest right in front, in reach of the stock.</summary>
	private GameObject MeleeTarget()
	{
		var candidates = Scene.GetAllComponents<Npc>().Where( n => n != this && n.Health >= 1f ).Select( n => n.GameObject )
			.Concat( Scene.GetAllComponents<PlayerController>().Where( p => (p.GetComponent<PlayerHealth>()?.Health ?? 0f) >= 1f ).Select( p => p.GameObject ) );

		return candidates
			.Where( go => (go.WorldPosition - WorldPosition).WithZ( 0 ).Length < MeleeRange
				&& MathF.Abs( go.WorldPosition.z - WorldPosition.z ) < 50f && IsFacing( go.WorldPosition, 60f ) )
			.OrderBy( go => go.WorldPosition.Distance( WorldPosition ) )
			.FirstOrDefault();
	}

	private void PilotBall()
	{
		_pilotBusy = 0.85f;
		BallChargeEffects();

		Invoke( 0.55f, () =>
		{
			if ( !IsValid || !IsAlive || !Piloted ) return;
			FireBall( WarboxAim, careful: false );
		} );
	}

	private void UpdatePilotHud()
	{
		// A weapon handed to him never runs dry in an NPC's hands
		var hud = $"AMMO\t{(HasGivenWeapon ? "\u221e" : Clip.ToString())}\nGRENADES\t{GrenadesLeft}";
		if ( ActiveWeapon == CombineWeaponType.Ar2 && !HasGivenWeapon ) hud += $"\nALT\t{Balls}";
		OtherPilotHud( ref hud );
		if ( WarboxHud != hud ) WarboxHud = hud;
	}
}
