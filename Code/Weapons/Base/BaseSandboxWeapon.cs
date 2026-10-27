using Sandbox.Citizen;

/// <summary>
/// The base for every weapon the player carries. Ported from Facepunch's sandbox game
/// (https://github.com/Facepunch/sandbox) with the sandbox-only parts left out: signal
/// wiring, NPC aiming, kill icons and the game's own Player type. Ballistics, ammo,
/// reload and the view model lifecycle come from the engine <see cref="BaseCombatWeapon"/>.
/// </summary>
public partial class BaseSandboxWeapon : BaseCombatWeapon, IPlayerControllable
{
	// DisplayName, DisplayIcon, Value, Slot, ViewModel and WorldModel are all inherited from the
	// engine BaseCombatWeapon / BaseInventoryItem, and so is Owner (the PlayerController holding it).

	/// <summary>
	/// True while a player holds this weapon.
	/// </summary>
	public bool HasOwner => Owner.IsValid();

	/// <summary>
	/// The root GameObject to ignore when tracing from AimRay.
	/// </summary>
	public GameObject AimIgnoreRoot
	{
		get
		{
			if ( HasOwner )
				return Owner.GameObject;

			// Held by an NPC: its own body must not stop the shot or the swing
			var npc = GetComponentInParent<Sandbox.Npcs.Npc>();
			return npc.IsValid() ? npc.GameObject : GameObject;
		}
	}

	/// <summary>
	/// When true, a seated player aims it where the camera looks instead of along the muzzle. Override in weapons
	/// that support player-directed aim.
	/// </summary>
	public virtual bool IsTargetedAim => false;

	/// <summary>
	/// What an NPC holding this weapon is shooting at. While set, shots go from the muzzle to the
	/// target's chest instead of along the barrel, which follows the animation and drifts high.
	/// </summary>
	public GameObject NpcAimTarget { get; set; }

	/// <summary>
	/// Roughly chest height - most of the way from the feet up to the eyes, so it follows crouching.
	/// </summary>
	private static Vector3 GetChestPosition( GameObject target )
	{
		var eyes = Sandbox.Npcs.Layers.AnimationLayer.GetEyePosition( target );
		return Vector3.Lerp( target.WorldPosition, eyes, 0.7f );
	}

	/// <summary>
	/// Controlled from a seat with targeted aim fires where the camera looks, otherwise the engine's muzzle-based
	/// unheld aim applies.
	/// </summary>
	protected override Ray UnheldAimRay
	{
		get
		{
			var seated = ControlContext.Player;
			if ( seated.IsValid() && IsTargetedAim && Scene.Camera.IsValid() )
				return new Ray( Scene.Camera.WorldPosition, Scene.Camera.WorldRotation.Forward );

			if ( NpcAimTarget.IsValid() )
			{
				var muzzle = GetMuzzleTransform().Position;
				return new Ray( muzzle, (GetChestPosition( NpcAimTarget ) - muzzle).Normal );
			}

			return base.UnheldAimRay;
		}
	}

	/// <summary>
	/// Who gets credit for this weapon's damage: the owner if held, the seated player if controlled from a seat,
	/// otherwise the engine's choice.
	/// </summary>
	protected override GameObject Attacker
	{
		get
		{
			if ( HasOwner ) return Owner.GameObject;

			var seatedPlayer = ControlContext.Player;
			if ( seatedPlayer.IsValid() ) return seatedPlayer.GameObject;

			var killSource = GetComponentInParent<IKillSource>( true );
			if ( killSource is Component c ) return c.GameObject;

			return base.Attacker;
		}
	}

	//
	// Seat inputs - a mounted weapon fires from the driver's configured bindings.
	//

	/// <summary>
	/// The input that fires the primary attack when this weapon is controlled via a seat.
	/// </summary>
	[Property, Group( "Input" ), Title( "Primary" )]
	public ClientInput ShootInput { get; set; }

	/// <summary>
	/// The input that fires the secondary attack when this weapon is controlled via a seat.
	/// </summary>
	[Property, Group( "Input" ), Title( "Secondary" )]
	public ClientInput SecondaryInput { get; set; }

	/// <summary>
	/// A mounted weapon stays put: no pickup prompt, no touch pickup.
	/// </summary>
	protected override bool OnCanPickup( BaseInventoryComponent inventory )
	{
		return base.OnCanPickup( inventory ) && !ShootInput.IsEnabled && !SecondaryInput.IsEnabled;
	}

	/// <summary>
	/// Stays put away while the owner sits with their weapons holstered (see <see cref="WeaponInventory.IsSeatHolstered"/>).
	/// </summary>
	protected override bool OnCanSwitchTo()
	{
		if ( Inventory is WeaponInventory { IsSeatHolstered: true } )
			return false;

		return base.OnCanSwitchTo();
	}

	//
	// The triggers, as this weapon sees them.
	//
	// An interface can hold them: the weapon switcher's menu confirms a pick with a click, and that click must
	// not also fire the weapon being put away (it is still the active one on the frame the switch is asked for,
	// and whether the menu or the weapon updates first is not something either of them can know). The menu
	// claims both triggers every frame it is up, so reading them here - never Input directly - leaves the click
	// to the menu, whichever of the two ran first.
	//

	/// <summary>
	/// <c>Input.Down( "attack1" )</c>, unless something above <see cref="InputPriority.Weapon"/> holds it.
	/// </summary>
	protected bool Attack1Down => InputClaims.Down( "attack1", InputPriority.Weapon, this );

	/// <summary>
	/// <c>Input.Pressed( "attack1" )</c>, unless something above <see cref="InputPriority.Weapon"/> holds it.
	/// </summary>
	protected bool Attack1Pressed => InputClaims.Pressed( "attack1", InputPriority.Weapon, this );

	/// <summary>
	/// <c>Input.Down( "attack2" )</c>, unless something above <see cref="InputPriority.Weapon"/> holds it.
	/// </summary>
	protected bool Attack2Down => InputClaims.Down( "attack2", InputPriority.Weapon, this );

	/// <summary>
	/// <c>Input.Pressed( "attack2" )</c>, unless something above <see cref="InputPriority.Weapon"/> holds it.
	/// </summary>
	protected bool Attack2Pressed => InputClaims.Pressed( "attack2", InputPriority.Weapon, this );

	/// <summary>
	/// <c>Input.Released( "attack1" )</c>, unless something above <see cref="InputPriority.Weapon"/> holds it.
	/// </summary>
	protected bool Attack1Released => InputClaims.Released( "attack1", InputPriority.Weapon, this );

	/// <inheritdoc />
	protected override bool WantsPrimaryAttack() => PrimaryAutomatic ? Attack1Down : Attack1Pressed;

	/// <inheritdoc />
	protected override bool WantsSecondaryAttack() => SecondaryAutomatic ? Attack2Down : Attack2Pressed;

	/// <summary>
	/// A seated player can only control this while not holding a weapon of their own.
	/// </summary>
	public virtual bool CanControl( PlayerController player )
	{
		var inventory = player.GetComponent<WeaponInventory>();
		return inventory is null || !inventory.ActiveWeapon.IsValid();
	}

	void IPlayerControllable.OnControl()
	{
		if ( HasOwner ) return;
		OnSeatControl();
	}

	protected virtual void OnSeatControl()
	{
		if ( ShootInput.Down() ) FirePrimary();
		if ( SecondaryInput.Down() ) FireSecondary();
	}

	/// <summary>
	/// If true the game should hide the hud when holding this weapon. Useful for cameras, or scopes.
	/// </summary>
	public virtual bool WantsHideHud => false;

	/// <summary>
	/// Runs on the host when added to an inventory. The engine base seeds the magazines and the
	/// shared reserve pool - only when they've never been filled, so a dropped gun keeps its rounds
	/// through a pickup.
	/// </summary>
	protected override void OnAdded( BaseInventoryComponent inventory )
	{
		// In an inventory - not a world pickup any more.
		IsDropped = false;

		base.OnAdded( inventory );
	}

	// The view the gun is placed at - captured before the camera bone offsets the view, so authored
	// camera animation moves the camera around the gun, not the gun with it.
	CameraView _viewModelView;

	protected override void ModifyCamera( CameraComponent camera, ref CameraView view )
	{
		base.ModifyCamera( camera, ref view );

		_viewModelView = view;

		ViewModel?.GetComponentInChildren<ViewModel>()?.UpdateCameraBone( ref view );
	}

	protected override void PlaceViewModel( CameraComponent camera, in CameraView view )
	{
		var model = ViewModel?.GetComponentInChildren<ViewModel>();
		if ( !model.IsValid() ) return;

		model.Place( _viewModelView );
		model.FollowCameraEffects( camera, view );
	}

	/// <summary>
	/// Drops this weapon into the world as a pickup - the same live object, so it keeps its state
	/// (ammo). Unparents it, drops network ownership and enables the dropped components. Host only.
	/// </summary>
	public void DropIntoWorld( Vector3 position, Vector3 velocity )
	{
		if ( !Networking.IsHost )
			return;

		Network.DropOwnership();

		GameObject.SetParent( null, true );
		WorldScale = GetPrefabWorldScale();
		GameObject.Enabled = true;
		WorldPosition = position;
		Slot = -1;

		IsDropped = true;

		if ( GetComponent<Rigidbody>( true ) is { } body )
		{
			body.Velocity = velocity;
			body.AngularVelocity = Vector3.Random * 8.0f;
		}
	}

	private Vector3 GetPrefabWorldScale()
	{
		if ( !string.IsNullOrEmpty( GameObject.PrefabInstanceSource ) )
		{
			var prefab = GameObject.GetPrefab( GameObject.PrefabInstanceSource );
			if ( prefab.IsValid() ) return prefab.LocalScale;
		}

		return WorldScale;
	}

	/// <summary>
	/// The engine inventory is dropping this - throw the live object out in front of the dropper.
	/// </summary>
	protected override bool OnDrop()
	{
		var player = Owner;

		if ( player.IsValid() )
		{
			var eye = player.EyeTransform;
			var velocity = player.Velocity + eye.Forward * 200f + Vector3.Up * 100f;

			DropIntoWorld( eye.Position + eye.Forward * 48f, velocity );
		}
		else
		{
			DropIntoWorld( WorldPosition, Vector3.Up * 50f );
		}

		return true;
	}

	protected override void OnHolstered()
	{
		base.OnHolstered();

		_ = EnableIfDroppedAsync();
	}

	/// <summary>
	/// The engine disables a holstered weapon on every peer. Dropping the held weapon holsters it first and then enables
	/// it again on the host, but clients only ever see the end state ("enabled", the same as before), so their copy of a
	/// dropped weapon stayed disabled and invisible
	/// </summary>
	private async Task EnableIfDroppedAsync()
	{
		for ( var waited = 0f; waited < 1f; waited += 0.05f )
		{
			await GameTask.DelaySeconds( 0.05f );

			if ( !this.IsValid() )
				return;

			if ( !Inventory.IsValid() )
			{
				GameObject.Enabled = true;
				return;
			}
		}
	}

	/// <summary>
	/// The view model's <see cref="BaseWeaponModel"/>, when it isn't the one <see cref="BaseCombatWeapon.WeaponModel"/>
	/// already drives. In third person the view model stays alive, hidden, and the engine drives only the world model.
	/// </summary>
	BaseWeaponModel HiddenViewModel
	{
		get
		{
			var model = ViewModel?.GetComponentInChildren<BaseWeaponModel>();
			return model.IsValid() && model != WeaponModel ? model : null;
		}
	}

	// Reloads drive the hidden view model too, so after switching back from third person it is in the same state
	// (still reloading, or done) and its reload sounds play.

	protected override void OnReloadStarted()
	{
		base.OnReloadStarted();
		HiddenViewModel?.OnReloadStart();
	}

	protected override void OnReloadFinished()
	{
		base.OnReloadFinished();
		HiddenViewModel?.OnReloadFinish();
	}

	protected override void OnReloadCancelled()
	{
		base.OnReloadCancelled();
		HiddenViewModel?.OnReloadCancel();
	}

	protected override void OnReloadInserted()
	{
		base.OnReloadInserted();
		HiddenViewModel?.OnIncrementalReload();
	}

	/// <summary>
	/// Is this item currently being used? When true, prevents auto-switching away on item pickup etc.
	/// </summary>
	public virtual bool IsInUse()
	{
		return false;
	}

	/// <summary>
	/// The player carrying us just jumped. Forwards to the view model so its anim graph can react.
	/// </summary>
	public virtual void OnJump()
	{
		ViewModel?.RunEvent<ViewModel>( x => x.Renderer.Set( "b_jump", true ) );
	}
}
