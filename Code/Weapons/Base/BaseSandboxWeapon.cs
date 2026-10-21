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
	public GameObject AimIgnoreRoot => HasOwner ? Owner.GameObject : GameObject;

	/// <summary>
	/// When true, a seated player aims it where the camera looks instead of along the muzzle. Override in weapons
	/// that support player-directed aim.
	/// </summary>
	public virtual bool IsTargetedAim => false;

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
	/// Shows or drops the first-person view model, for switching between first and third person. Owner only.
	/// </summary>
	public void SetViewModelShown( bool shown )
	{
		if ( !shown && ViewModel.IsValid() )
			DestroyViewModel();
		else if ( shown && !ViewModel.IsValid() && IsHeld )
			CreateViewModel();
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
