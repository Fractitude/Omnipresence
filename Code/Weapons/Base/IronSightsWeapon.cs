using Sandbox.Rendering;

/// <summary>
/// A weapon that can aim down sights
/// </summary>
public abstract class IronSightsWeapon : BaseBulletWeapon
{
	/// <summary>
	/// Lowers the amount of recoil / visual noise when aiming
	/// </summary>
	[Property] public float IronSightsFireScale { get; set; } = 0.2f;

	/// <summary>
	/// Multiplier for the player's FOV while aiming in first person. 1 disables zoom.
	/// </summary>
	[Property, Group( "Iron Sights" ), Title( "Aim FOV Scale" ), Range( 0.1f, 1f )]
	public float AimFovScale { get; set; } = 0.8f;

	/// <summary>
	/// How much of the third person camera's distance is left while aiming: 1 leaves it where it is, 0.45 brings it
	/// in to just under a metre behind the shoulder, 0 would sit at the player's eye. The camera is brought in
	/// rather than the lens narrowed, so the picture punches in with the world keeping its perspective.
	/// </summary>
	[Property, Group( "Iron Sights" ), Title( "Third Person Aim Distance" ), Range( 0f, 1f )]
	public float AimThirdPersonDistanceScale { get; set; } = 0.45f;

	/// <summary>
	/// The closest the view is brought to the eye while aiming, in inches - further than the character's own head,
	/// so it never ends up inside it. The camera is never pushed out to reach this, only held back when the boom
	/// behind the player is already shorter than it.
	/// </summary>
	const float MinAimDistance = 24f;

	/// <summary>
	/// Seconds to blend between normal and aimed FOV, and between the two camera distances in third person. 0
	/// switches instantly.
	/// </summary>
	[Property, Group( "Iron Sights" ), Title( "Aim FOV Transition Time" ), Range( 0f, 1f )]
	public float AimFovTransitionTime { get; set; } = 0.15f;

	private float _aimBlend;

	// Synced (owner-authoritative) so the host's authoritative shot trace sees it too - otherwise ADS
	// only narrows the cone on the owner's predicted (non-damaging) pass and the real shot stays wide.
	[Sync] public bool IsAiming { get; set; }

	public override bool CanSecondaryAttack() => false;

	/// <summary>
	/// Draws the crosshair while the weapon is not aiming down sights.
	/// </summary>
	public override void DrawHud( Painter painter, Vector2 crosshair )
	{
		if ( IsAiming ) return;

		base.DrawHud( painter, crosshair );
	}

	protected override void OnControl()
	{
		base.OnControl();

		var wantsAim = Input.Down( "attack2" ) && !IsReloading;

		if ( wantsAim == IsAiming )
			return;

		IsAiming = wantsAim;
		ViewModel?.RunEvent<ViewModel>( x =>
		{
			x.Renderer?.Set( "ironsights", IsAiming ? 1 : 0 );
			x.Renderer?.Set( "ironsights_fire_scale", IsAiming ? IronSightsFireScale : 1f );
		} );
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();
		IsAiming = false;
		_aimBlend = 0f;
	}

	protected override void OnHolstered()
	{
		base.OnHolstered();
		IsAiming = false;
		_aimBlend = 0f;
	}

	/// <summary>
	/// Aiming narrows the lens in first person and brings the camera in in third person. The camera's distance is
	/// the third person punch-in: the picture grows while the world keeps its perspective, the view comes in over
	/// the shoulder towards the sights, and the shot (traced from the camera in third person) starts nearer the
	/// body, so there is less parallax between the crosshair and what it hits.
	/// </summary>
	protected override void ModifyCamera( CameraComponent camera, ref CameraView view )
	{
		_aimBlend = HasOwner
			? AimFovTransitionTime <= 0f
				? (IsAiming ? 1f : 0f)
				: (_aimBlend + (IsAiming ? 1f : -1f) * Time.Delta / AimFovTransitionTime).Clamp( 0f, 1f )
			: 0f;

		if ( HasOwner && !Owner.ThirdPerson )
		{
			view.FieldOfView *= MathX.Lerp( 1f, AimFovScale.Clamp( 0.1f, 1f ), _aimBlend );
		}
		else if ( _aimBlend > 0f )
		{
			// The player's camera is a boom swung from the eye: so much back, so much to the shoulder, a little up.
			// Aiming shortens only the part that is back - the shoulder offset stays, so the view slides past the
			// character instead of into the back of their head, and the aim direction is left alone.
			var eye = Owner.EyeTransform.Position;
			var boom = view.Position - eye;
			var rotation = view.Rotation;
			var back = -boom.Dot( rotation.Forward );

			if ( back > 0.001f )
			{
				// Shorter than the boom is today, but never inside the character, and never pushed out: a boom the
				// geometry behind the player has already shortened stays where it is.
				var pulled = MathF.Max( back * AimThirdPersonDistanceScale.Clamp( 0f, 1f ),
					MathF.Min( back, MinAimDistance ) );
				var aimedBack = MathX.Lerp( back, pulled, _aimBlend );

				view.Position = eye - rotation.Forward * aimedBack
					+ rotation.Right * boom.Dot( rotation.Right )
					+ rotation.Up * boom.Dot( rotation.Up );

				// Shots in third person come from the camera's own transform (BaseCombatWeapon.AimRay), and the
				// player's camera is the only modifier that writes it - so the pull has to reach it too, or the
				// crosshair and the shot stop agreeing on the way in.
				camera.WorldPosition = view.Position;
			}
		}

		base.ModifyCamera( camera, ref view );
	}

	/// <summary>
	/// Aiming down sights narrows the spread cone.
	/// </summary>
	public override Vector2 CurrentSpread => base.CurrentSpread * (IsAiming ? IronSightsFireScale : 1f);
}
