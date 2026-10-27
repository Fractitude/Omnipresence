namespace Sandbox;

/// <summary>
/// The flashlight (F), ported from the sandbox game's <c>PlayerFlashlight</c>. That one is a child object of the player
/// prefab; here the lights are made on each machine when the player appears, as children that are never networked, and
/// the only thing shared is <see cref="FlashlightOn"/>. The prefab is left as it is.
/// </summary>
/// <remarks>
/// Neither light is anchored right at the eyes. <see cref="Player.EyeTransform"/> sits inside the head, so a beam
/// that started there put the owner's own face and chest in its near field: lit up harshly, and throwing their own
/// nose-and-shoulder shadow across whatever they were looking at, visible to anyone watching them in third person
/// (the engine has no way to exclude one object from one light's illumination or shadows, so this can't be fixed by
/// flagging the body). Both lights sit <see cref="FlashlightReach"/> units in front of the eyes instead, pulled back
/// to whatever's actually in the way: a spot light only emits forward of its own position, so with the origin out
/// past the whole body, the owner's body is behind it and never enters its cone - it neither lights nor shadows
/// itself, on any client, while still being lit normally by anyone else's flashlight. Computed from the eyes the
/// same way on every machine, so everyone agrees on where it sits.
/// </remarks>
public sealed partial class Player
{
	/// <summary>
	/// Whether the flashlight is on. The owner sets it; everyone's copy of the lights follows.
	/// </summary>
	[Sync] public bool FlashlightOn { get; set; }

	/// <summary>
	/// How far in front of the eyes the lights are anchored - enough to clear the owner's own body, so the beam
	/// never catches or shadows them. Pulled back if something is in the way.
	/// </summary>
	const float FlashlightReach = 24f;

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
		flashlight.ConeOuter = 24f;
		flashlight.Radius = 1200f;
		flashlight.LightColor = new Color( 3f, 2.65f, 1.37f );
		flashlight.Cookie = Texture.Load( "textures/lightcookies/lightcookies_01.vtex" );
		flashlight.Shadows = true;
		flashlight.Enabled = false;

		var fill = new GameObject( true, "Flashlight fill" ) { NetworkMode = NetworkMode.Never };
		fill.Flags |= GameObjectFlags.NotSaved;
		fill.Parent = GameObject;

		flashlightFill = fill.Components.Create<PointLight>();
		flashlightFill.Attenuation = 18f;
		flashlightFill.Radius = 128f;
		flashlightFill.LightColor = new Color( 0.37f, 0.32f, 0.28f );
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
		var target = eye.Position + eye.Forward * FlashlightReach;
		var tr = Scene.Trace.FromTo( eye.Position, target ).Radius( 4f ).WithoutTags( "ragdoll", "effect" )
			.IgnoreGameObjectHierarchy( GameObject.Root ).Run();

		// Clamp short of whatever the trace hit (a wall right in front of the player), but never so short the
		// origin falls back behind the owner's own body - that would put them back in their own light.
		var reach = tr.Hit ? MathF.Max( tr.Distance - 2f, FlashlightReach * 0.25f ) : FlashlightReach;
		var origin = eye.Position + eye.Forward * reach;

		flashlight.WorldTransform = new Transform( origin, eye.Rotation );
		flashlightFill.WorldPosition = origin;
	}
}
