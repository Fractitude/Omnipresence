namespace Sandbox;

/// <summary>
/// The flashlight (F), ported from the sandbox game's <c>PlayerFlashlight</c>. That one is a child object of the player
/// prefab; here the lights are made on each machine when the player appears, as children that are never networked, and
/// the only thing shared is <see cref="FlashlightOn"/>. The prefab is left as it is.
/// </summary>
public sealed partial class Player
{
	/// <summary>
	/// Whether the flashlight is on. The owner sets it; everyone's copy of the lights follows.
	/// </summary>
	[Sync] public bool FlashlightOn { get; set; }

	/// <summary>
	/// How far in front of the eyes the soft fill light sits. Pulled back if something is in the way.
	/// </summary>
	const float FlashlightFillDistance = 24f;

	SpotLight flashlight;
	PointLight flashlightFill;

	void CreateFlashlight()
	{
		var beam = new GameObject( true, "Flashlight" ) { NetworkMode = NetworkMode.Never };
		beam.Flags |= GameObjectFlags.NotSaved;
		beam.Parent = GameObject;

		flashlight = beam.Components.Create<SpotLight>();
		flashlight.Attenuation = 1.0931175f;
		flashlight.ConeInner = 19.676113f;
		flashlight.ConeOuter = 42f;
		flashlight.Radius = 1500f;
		flashlight.LightColor = new Color( 12.68f, 11.64494f, 10.37991f );
		flashlight.Cookie = Texture.Load( "textures/lightcookies/lightcookies_01.vtex" );
		flashlight.Shadows = true;
		flashlight.Enabled = false;

		var fill = new GameObject( true, "Flashlight fill" ) { NetworkMode = NetworkMode.Never };
		fill.Flags |= GameObjectFlags.NotSaved;
		fill.Parent = GameObject;

		flashlightFill = fill.Components.Create<PointLight>();
		flashlightFill.Attenuation = 24f;
		flashlightFill.Radius = 128f;
		flashlightFill.LightColor = new Color( 0.37209f, 0.32623f, 0.28037f );
		flashlightFill.Shadows = false;
		flashlightFill.Enabled = false;
	}

	void UpdateFlashlight()
	{
		if ( !flashlight.IsValid() )
			CreateFlashlight();

		if ( IsLocalPlayer && Input.Pressed( "Flashlight" ) && !Input.Suppressed && !Input.MouseCursorVisible )
		{
			// Whatever else wants the key (a car's lights) can ask for it
			var press = new PlayerFlashlightEvent { Player = this };
			Local.IPlayerEvents.PostToGameObject( GameObject, x => x.OnFlashlight( press ) );
			Global.IPlayerEvents.Post( x => x.OnPlayerFlashlight( press ) );

			if ( !press.Cancelled )
			{
				FlashlightOn = !FlashlightOn;
				Sound.Play( FlashlightOn ? "sounds/flashlight/flashlight-on.sound" : "sounds/flashlight/flashlight-off.sound",
					WorldPosition );
			}
		}

		flashlight.Enabled = FlashlightOn;
		flashlightFill.Enabled = FlashlightOn;

		if ( !FlashlightOn )
			return;

		var eye = EyeTransform;
		flashlight.WorldTransform = eye;

		var target = eye.Position + eye.Forward * FlashlightFillDistance;
		var tr = Scene.Trace.FromTo( eye.Position, target ).Radius( 4f ).WithoutTags( "ragdoll", "effect" )
			.IgnoreGameObjectHierarchy( GameObject.Root ).Run();

		flashlightFill.WorldPosition = tr.Hit ? tr.EndPosition : target;
	}
}
