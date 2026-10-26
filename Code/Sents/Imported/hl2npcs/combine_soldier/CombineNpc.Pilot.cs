using System;
using System.Linq;
using Sandbox;
using Sandbox.Npcs;

namespace Hl2Combine;

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

	/// <summary>The camera: in his head, lower when he kneels.</summary>
	public Transform WarboxEyes => new( WorldPosition + Vector3.Up * (Crouched ? 40f : 64f), WorldRotation );

	/// <summary>What the player's HUD shows besides health: one "label\tvalue" per line.</summary>
	[Sync] public string WarboxHud { get; set; }

	private bool _controlled;
	private CharacterController _pilotBody;
	private TimeUntil _pilotBusy;
	private TimeUntil _pilotReloadDone;
	private bool _pilotReloading;
	private bool _wasReload, _wasGrenade, _wasUse, _wasFire2, _wasJump;

	public bool Piloted => _controlled;

	private void PilotStart()
	{
		if ( !IsAlive ) return;
		_controlled = true;

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
		UpdatePilotHud();
	}

	private void PilotStop()
	{
		_controlled = false;
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
		var grenade = WarboxGrenade && !_wasGrenade || WarboxFire2 && !_wasFire2 && ActiveWeapon != CombineWeaponType.Ar2;
		var ball = WarboxFire2 && !_wasFire2 && ActiveWeapon == CombineWeaponType.Ar2;
		var melee = WarboxUse && !_wasUse;
		_wasReload = WarboxReload;
		_wasGrenade = WarboxGrenade;
		_wasFire2 = WarboxFire2;
		_wasUse = WarboxUse;

		if ( _pilotBusy && !IsReloading )
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
			.Concat( Scene.GetAllComponents<Player>().Where( p => p.Health >= 1f ).Select( p => p.GameObject ) );

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
		var hud = $"AMMO\t{Clip}\nGRENADES\t{GrenadesLeft}";
		if ( ActiveWeapon == CombineWeaponType.Ar2 ) hud += $"\nALT\t{Balls}";
		if ( WarboxHud != hud ) WarboxHud = hud;
	}
}
