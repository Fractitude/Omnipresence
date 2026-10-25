using Sandbox.Movement;

namespace Sandbox;

/// <summary>
/// Picking up light physics objects with use, ported from the sandbox game's <c>PlayerCarry</c>. Use on a prop takes it
/// (the weapon goes away), attack throws it, aim-down-sights lets go, and holding use turns it with the mouse. The
/// holder works out where it should be; whoever simulates the body moves it there through a control joint
/// (<see cref="PhysicsHold"/>), so a carried object is as real to the rest of the world as a thrown one.
/// <para>
/// Not carried: players, NPCs, placed objects (storage boxes, the press bench, ...), anything bolted down or heavier
/// than <see cref="CarryMaxMass"/>, and anything an <see cref="ICarryEvent"/> or a listener refuses.
/// </para>
/// </summary>
public sealed partial class Player
{
	/// <summary>
	/// When off, nobody can pick things up with use.
	/// </summary>
	[ConVar( "omni.carry", ConVarFlags.Replicated | ConVarFlags.Saved, Help = "Players can pick up light props with use." )]
	public static bool AllowCarry { get; set; } = true;

	/// <summary>
	/// The heaviest object players can pick up, in kg.
	/// </summary>
	[ConVar( "omni.carry.maxmass", ConVarFlags.Replicated | ConVarFlags.Saved,
		Help = "The heaviest object, in kg, players can pick up with use." )]
	public static float CarryMaxMass { get; set; } = 80.0f;

	/// <summary>
	/// How fast a prop flies once you throw it.
	/// </summary>
	public float ThrowSpeed { get; set; } = 500.0f;

	/// <summary>
	/// How far in front of the eyes the near side of a carried object is held.
	/// </summary>
	public float HoldDistance { get; set; } = 40.0f;

	/// <summary>
	/// We let go once the object gets this far from where we're holding it, like when it's pushed against a wall.
	/// </summary>
	public float BreakDistance { get; set; } = 96.0f;

	/// <summary>
	/// The object we're carrying.
	/// </summary>
	[Sync] public GameObject Held { get; private set; }

	public Rigidbody HeldBody => Held?.GetComponent<Rigidbody>();

	public bool IsCarrying => Held.IsValid();

	public bool IsRotating { get; private set; }

	/// <summary>
	/// In a seat (a car's, say).
	/// </summary>
	public bool IsSeated => GetComponentInParent<ISitTarget>() is not null;

	// Worked out from the holder's eye on every machine, a synced target would step at the network rate
	Transform holdTarget;

	readonly PhysicsHold hold = new();
	readonly HoldRotation rotation = new();

	[Sync] Rotation grabOffset { get; set; }
	TimeSince timeSincePickup;

	bool rotateBlocked;
	bool attackBlocked;

	/// <summary>
	/// What we had out before picking something up, to take it back out when we let go.
	/// </summary>
	BaseSandboxWeapon weaponBeforeCarry;

	/// <summary>
	/// The object our joint is moving, so we can tell it when we let go.
	/// </summary>
	GameObject simulated;
	bool thrown;

	GameObject refused;
	GameObject CarryClaim => Networking.IsHost && IsProxy ? simulated : Held;

	/// <summary>
	/// Simulating instance only, clamp the target the holder sends to this far from their eyes.
	/// </summary>
	const float MaxReach = 256.0f;

	/// <summary>
	/// Called by <see cref="WeaponInventory"/> on the player's own machine every frame, before the weapon reads input.
	/// </summary>
	internal void ControlCarry()
	{
		if ( IsProxy || !Controller.IsValid() )
			return;

		// A computer or a menu has the keys
		if ( NaviInputCapture.Current is not null || Input.Suppressed || Input.MouseCursorVisible )
		{
			if ( IsCarrying )
				DropHeld();

			IsRotating = false;
			UpdateCarryControls();
			return;
		}

		if ( IsCarrying )
		{
			ControlHeld();
			return;
		}

		IsRotating = false;
		UpdateCarryControls();
		BlockAttack();

		if ( !Input.Pressed( "use" ) || IsSeated )
			return;

		if ( Controller.Hovered.IsValid() || Controller.Pressed.IsValid() )
			return;

		if ( !TryPickup( FindCarryTarget() ) )
			return;

		Input.Clear( "use" );
	}

	/// <summary>
	/// While a carried object is being turned the mouse and the movement keys are for that.
	/// </summary>
	void UpdateCarryControls()
	{
		Controller.UseLookControls = !IsRotating;
		Controller.UseInputControls = !IsRotating;

		if ( IsRotating )
			Controller.WishVelocity = Vector3.Zero;
	}

	void ControlHeld()
	{
		if ( !CanKeepCarrying() )
		{
			DropHeld();
			UpdateCarryControls();
			return;
		}

		// Took a weapon out, our hands are full with that now
		if ( GetComponent<WeaponInventory>()?.ActiveWeapon.IsValid() == true )
		{
			weaponBeforeCarry = null;
			DropHeld();
			UpdateCarryControls();
			return;
		}

		rotation.UpdateSnapping();

		var useDown = Input.Down( "use" );
		if ( !useDown ) rotateBlocked = false;
		Input.Clear( "use" );

		if ( Input.Pressed( "attack1" ) ) ThrowHeld();
		else if ( Input.Pressed( "attack2" ) ) DropHeld();

		attackBlocked = true;
		BlockAttack();

		IsRotating = IsCarrying && useDown && !rotateBlocked;
		UpdateCarryControls();

		if ( !IsRotating )
			return;

		grabOffset = rotation.Rotate( EyeTransform.Rotation, Input.AnalogMove, true );
	}

	void BlockAttack()
	{
		if ( !attackBlocked )
			return;

		if ( !Input.Down( "attack1" ) && !Input.Down( "attack2" ) && !IsCarrying )
		{
			attackBlocked = false;
			return;
		}

		Input.Clear( "attack1" );
		Input.Clear( "attack2" );
	}

	bool CanKeepCarrying()
	{
		var body = HeldBody;
		if ( !body.IsValid() || !body.MotionEnabled || !body.PhysicsBody.IsValid() ) return false;
		if ( IsSeated ) return false;

		if ( Controller.GroundObject?.Root == Held.Root ) return false;

		if ( timeSincePickup > 0.5f && body.PhysicsBody.MassCenter.Distance( holdTarget.Position ) > BreakDistance )
			return false;

		return true;
	}

	/// <summary>
	/// The body we're looking at within reach, if any.
	/// </summary>
	Rigidbody FindCarryTarget()
	{
		var eye = EyeTransform;
		var reach = Controller.ReachLength;

		// Widen the ray a little if it misses, so small things aren't fiddly to grab
		for ( var radius = 0.0f; radius <= 4.0f; radius += 2.0f )
		{
			var tr = Scene.Trace.Ray( eye.Position, eye.Position + eye.Forward * (reach - radius) )
				.Radius( radius )
				.IgnoreGameObjectHierarchy( GameObject.Root )
				.Run();

			if ( !tr.Hit ) continue;
			return tr.Body is not null ? tr.Component as Rigidbody : null;
		}

		return null;
	}

	/// <summary>
	/// Whether we're allowed to pick up <paramref name="body"/>. Asks the object and any listeners through a
	/// <see cref="PlayerCarryEvent"/>.
	/// </summary>
	public bool CanCarry( Rigidbody body )
	{
		if ( !AllowCarry ) return false;
		if ( !body.IsValid() || !body.MotionEnabled || !body.PhysicsBody.IsValid() ) return false;

		var go = body.GameObject;
		if ( go.Root == GameObject.Root ) return false;
		if ( go.Root.Tags.Has( "player" ) || go.Root.Tags.Has( "npc" ) ) return false;

		// Placed objects are bought, delivered and kept between sessions: they are not for moving by hand
		if ( go.Root.Components.Get<PlacedObject>().IsValid() ) return false;

		// Someone else has it, two joints would fight over it
		if ( Scene.GetAllComponents<Player>().Any( x => x != this && x.CarryClaim?.Root == go.Root ) ) return false;

		var bodies = new HashSet<Rigidbody>();
		PhysicsHold.GetConnectedBodies( go, bodies );
		if ( bodies.Any( x => !x.MotionEnabled ) ) return false;

		var e = new PlayerCarryEvent
		{
			Player = this,
			Target = go,
			Body = body,
			Mass = bodies.Sum( x => x.Mass ),
			MaxMass = CarryMaxMass
		};

		go.Root.RunEvent<ICarryEvent>( x => x.OnCarryGrab( e ) );
		Local.IPlayerEvents.PostToGameObject( GameObject, x => x.OnCarry( e ) );
		Global.IPlayerEvents.Post( x => x.OnPlayerCarry( e ) );

		return !e.Cancelled && e.Mass <= e.MaxMass;
	}

	/// <summary>
	/// Picks up <paramref name="body"/> if we're allowed to (owner only).
	/// </summary>
	public bool TryPickup( Rigidbody body )
	{
		if ( IsProxy || IsCarrying ) return false;
		if ( !CanCarry( body ) ) return false;
		if ( !HolsterForCarry() ) return false;

		Held = body.GameObject;

		grabOffset = EyeTransform.Rotation.Inverse * body.WorldRotation;
		rotation.Reset( grabOffset );
		holdTarget = GetHoldTarget();
		timeSincePickup = 0;
		rotateBlocked = true;

		return true;
	}

	/// <summary>
	/// Lets go of what we're carrying where it is (owner only).
	/// </summary>
	public void DropHeld()
	{
		if ( IsProxy || !IsCarrying ) return;

		Held = null;
		IsRotating = false;
		attackBlocked = true;

		UnholsterAfterCarry();
	}

	/// <summary>
	/// Puts away the active weapon, carrying takes both hands. False if it couldn't be put away.
	/// </summary>
	bool HolsterForCarry()
	{
		var inventory = GetComponent<WeaponInventory>();
		if ( !inventory.IsValid() || !inventory.ActiveWeapon.IsValid() ) return true;

		var weapon = inventory.ActiveWeapon;

		// ForceHolster is host only, so a client asks the host through Switch
		if ( !Networking.IsActive || Networking.IsHost )
			inventory.ForceHolster();
		else
			inventory.Switch( null, allowHolster: true );

		// The weapon or a switch event listener refused
		if ( inventory.ActiveWeapon.IsValid() ) return false;

		weaponBeforeCarry = weapon;
		return true;
	}

	/// <summary>
	/// Takes back out the weapon we put away to pick something up, unless something else has come out since.
	/// </summary>
	void UnholsterAfterCarry()
	{
		var weapon = weaponBeforeCarry;
		weaponBeforeCarry = null;

		var inventory = GetComponent<WeaponInventory>();
		if ( !inventory.IsValid() || inventory.ActiveWeapon.IsValid() ) return;
		if ( !weapon.IsValid() || weapon.Owner != Controller || IsSeated ) return;

		inventory.Switch( weapon );
	}

	/// <summary>
	/// Throws what we're carrying where we're looking (owner only).
	/// </summary>
	public void ThrowHeld()
	{
		if ( IsProxy || !IsCarrying ) return;

		ThrowBody( HeldBody, EyeTransform.Forward * ThrowSpeed );
		DropHeld();
	}

	[Rpc.Host]
	void ThrowBody( Rigidbody body, Vector3 velocity )
	{
		// Only what we accepted and are moving for them, not a pickup we refused or haven't got to yet
		if ( !body.IsValid() || body.IsProxy || simulated != body.GameObject ) return;

		thrown = true;
		ReleaseHold();

		body.ApplyImpulse( velocity * body.Mass );
	}

	Transform GetHoldTarget()
	{
		var eye = EyeTransform;
		var body = HeldBody;
		var center = body.PhysicsBody.MassCenter;

		var distance = PhysicsHold.ClampGrabDistance( body, center, eye, 0.0f, HoldDistance );
		return new Transform( eye.Position + eye.Forward * distance, eye.Rotation * grabOffset );
	}

	protected override void OnFixedUpdate()
	{
		var body = HeldBody;

		if ( body.IsValid() && body.PhysicsBody.IsValid() )
			holdTarget = GetHoldTarget();

		// switched straight to another object, the joint still holds the old one
		if ( simulated.IsValid() && simulated != Held )
			ReleaseHold();

		if ( !CanSimulate( body ) )
		{
			ReleaseHold();
			thrown = false;
			refused = null;
			return;
		}

		if ( thrown ) return;

		if ( !hold.IsHolding )
		{
			// The holder asked for this on their own machine, check it again before we move it for them
			if ( refused == Held ) return;

			if ( IsProxy && (!CanCarry( body ) || !IsInReach( body )) )
			{
				refused = Held;
				RefuseCarry( Held );
				return;
			}

			hold.Hold( body, body.PhysicsBody.MassCenter );
			simulated = Held;
			simulated.Root.RunEvent<ICarryEvent>( x => x.OnCarryPickup( this ) );
		}

		hold.Move( IsProxy ? ClampToReach( holdTarget ) : holdTarget );
	}

	/// <summary>
	/// Whether the holder can see <paramref name="body"/> within use reach, with some slack for where we see their eyes.
	/// </summary>
	bool IsInReach( Rigidbody body )
	{
		var eye = EyeTransform.Position;
		var closest = body.FindClosestPoint( eye );
		if ( closest.Distance( eye ) > Controller.ReachLength + 32.0f ) return false;

		var tr = Scene.Trace.Ray( eye, closest )
			.IgnoreGameObjectHierarchy( GameObject.Root )
			.Run();

		return !tr.Hit || tr.GameObject?.Root == body.GameObject.Root;
	}

	[Rpc.Owner]
	void RefuseCarry( GameObject held )
	{
		// They may have let go and picked up something else since
		if ( Held == held ) DropHeld();
	}

	Transform ClampToReach( Transform target )
	{
		var eye = EyeTransform.Position;
		return target.WithPosition( eye + (target.Position - eye).ClampLength( MaxReach ) );
	}

	static bool CanSimulate( Rigidbody body )
	{
		if ( !body.IsValid() ) return false;

		// Only move the body if we own it, and it's dynamic.
		if ( body.IsProxy ) return false;
		if ( !body.MotionEnabled ) return false;
		if ( !body.PhysicsBody.IsValid() ) return false;

		return true;
	}

	void ReleaseHold()
	{
		hold.Release();

		var held = simulated;
		simulated = null;

		if ( !held.IsValid() ) return;

		held.Root.RunEvent<ICarryEvent>( x => x.OnCarryDrop( this, thrown ) );
	}

	protected override void OnDisabled()
	{
		ReleaseHold();

		if ( !IsProxy ) Held = null;

		IsRotating = false;
		thrown = false;
		attackBlocked = false;
		weaponBeforeCarry = null;
	}
}
