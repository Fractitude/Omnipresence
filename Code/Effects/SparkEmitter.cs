using System;

namespace Sandbox;

/// <summary>
/// Electric sparks: short-lived bright scraps that fly out, fall and crackle out, thrown the way a short circuit throws
/// them. Drop it on anything that should spark — a broken camera, a cut cable, a machine with a bad earth — and set
/// <see cref="Emitting"/> to turn it on and off.
/// </summary>
/// <remarks>
/// The sparks are the engine's, not ours: this component builds a <see cref="ParticleEffect"/> with a
/// <see cref="ParticleSpriteRenderer"/> and a <see cref="ParticleSphereEmitter"/> on its own GameObject, and only maps
/// the handful of properties below onto them. It also plays the crackle, from <c>sounds/effects/sparks.sound</c>, and
/// keeps the point light that makes the sparks read at night. Nothing about it is saved or sent, so every machine makes
/// the sparks it sees.
/// </remarks>
[Title( "Spark Emitter" ), Icon( "bolt" )]
public sealed class SparkEmitter : Component
{
	/// <summary>The side of the spark texture made here, in pixels.</summary>
	const int SparkTextureSize = 32;

	/// <summary>The crackle a clump plays when it isn't given one.</summary>
	const string DefaultCrackle = "sounds/effects/sparks.sound";

	/// <summary>Whether sparks are coming off it right now.</summary>
	[Property] public bool Emitting { get; set; } = true;

	/// <summary>How often a clump of sparks is thrown, in seconds.</summary>
	[Property, Range( 0.02f, 3f )]
	public float Interval { get; set; } = 0.5f;

	/// <summary>How many sparks are in a clump.</summary>
	[Property, Range( 1, 30 )]
	public int Count { get; set; } = 4;

	/// <summary>How long a spark lives, in seconds. Each one differs a little from this.</summary>
	[Property, Range( 0.05f, 2f )]
	public float Lifetime { get; set; } = 0.4f;

	/// <summary>How fast a spark leaves the emitter, in units a second.</summary>
	[Property, Range( 0f, 1000f )]
	public float Speed { get; set; } = 130f;

	/// <summary>How hard they are pulled down, in units a second squared.</summary>
	[Property, Range( 0f, 1000f )]
	public float Gravity { get; set; } = 350f;

	/// <summary>How far from the emitter a spark may start, in units.</summary>
	[Property, Range( 0f, 50f )]
	public float Spread { get; set; } = 2.5f;

	/// <summary>How big a spark is when it appears, and when it goes out, in units across.</summary>
	[Property, Range( 0.5f, 100f )]
	public float StartSize { get; set; } = 1f;

	[Property, Range( 0.2f, 100f )]
	public float EndSize { get; set; } = 0.25f;

	/// <summary>
	/// How many times taller than wide a spark is drawn: a streak rather than a dot, which is what a fast spark looks
	/// like. 1 makes it round.
	/// </summary>
	[Property, Range( 1f, 12f )]
	public float Stretch { get; set; } = 2.5f;

	/// <summary>The sparks' colour.</summary>
	[Property] public Color Tint { get; set; } = new( 1f, 0.88f, 0.6f, 1f );

	/// <summary>
	/// The picture a spark is drawn with. Leave it empty for the bright dot the emitter makes itself; any sprite works.
	/// </summary>
	[Property] public Sprite Sprite { get; set; }

	/// <summary>
	/// What a clump sounds like. Empty uses the crackle that ships with the game — three takes, picked at random, in
	/// `sounds/effects/sparks.sound`.
	/// </summary>
	[Property] public SoundEvent Crackle { get; set; }

	/// <summary>
	/// How loud the crackle is, 0 to 1. It is a small noise at the camera, not a siren: 0.2 is about right, and it is
	/// the volume the sound is played at, so the sound asset's own volume only sets how it sounds next to its neighbours.
	/// </summary>
	[Property, Range( 0f, 1f )]
	public float CrackleVolume { get; set; } = 1f;

	/// <summary>
	/// How far the crackle carries, in units. It is a sound for inside the house: 350 units is the width of it, and past
	/// that it is not heard at all.
	/// </summary>
	[Property, Range( 50f, 2000f )]
	public float HearingRange { get; set; } = 350f;

	/// <summary>
	/// The shortest gap between two crackles, in seconds. The first one plays the moment the sparks start, and then the
	/// noise comes back this often: a shorting camera is mostly silent, with an occasional snap.
	/// </summary>
	[Property, Range( 0f, 15f )]
	public float CrackleGap { get; set; } = 5f;

	/// <summary>
	/// Whether the sparks are drawn additively. Additive is the hotter look on a dark background, but it disappears
	/// against a bright sky; off, the sparks keep their own colour wherever they are.
	/// </summary>
	[Property] public bool Additive { get; set; }

	/// <summary>Whether the sparks light what is around them, with one point light on the emitter.</summary>
	[Property] public bool Light { get; set; } = true;

	/// <summary>How far the light reaches, in units.</summary>
	[Property, Range( 0f, 1000f ), ShowIf( nameof( Light ), true )]
	public float LightRadius { get; set; } = 90f;

	ParticleEffect effect;
	ParticleSpriteRenderer renderer;
	ParticleSphereEmitter emitter;
	Sprite defaultSprite;
	PointLight bulb;

	/// <summary>How long since the last crackle, so one clump in a burst is the one that is heard.</summary>
	RealTimeSince sinceCrackle = float.MaxValue;

	/// <summary>Whether the sparks were flying last frame, so the moment they start can be told apart.</summary>
	bool wasEmitting;

	protected override void OnEnabled()
	{
		Build();
	}

	protected override void OnUpdate()
	{
		if ( !emitter.IsValid() )
			Build();

		if ( !emitter.IsValid() )
			return;

		emitter.Enabled = Emitting;

		if ( !Emitting )
		{
			wasEmitting = false;
			Flicker( 0f );

			return;
		}

		// Fixed and broken again: it cracks at once rather than waiting out the gap from last time.
		if ( !wasEmitting )
		{
			wasEmitting = true;
			sinceCrackle = float.MaxValue;
		}

		CrackleSound();
		Flicker( 0.55f + System.Random.Shared.NextSingle() * 0.45f );
	}

	/// <summary>
	/// Make the particle components the sparks are drawn by, or find the ones already on the object, and set them up.
	/// Safe to call again: it only ever rewrites their numbers.
	/// </summary>
	public void Build()
	{
		if ( !GameObject.IsValid() )
			return;

		var picture = Picture;

		if ( !picture.IsValid() )
			return;

		effect = Find<ParticleEffect>();
		renderer = Find<ParticleSpriteRenderer>();
		emitter = Find<ParticleSphereEmitter>();

		// The engine emits at a rate, not in clumps: Count sparks every Interval is Count / Interval a second, which
		// looks the same once they are flying and costs nothing to hold there.
		var rate = Math.Clamp( Count / MathF.Max( Interval, 0.01f ), 1f, 2000f );

		effect.MaxParticles = Math.Clamp( (int)MathF.Ceiling( rate * Lifetime * 1.4f ) + 16, 8, 1024 );
		effect.Lifetime = new ParticleFloat( Lifetime * 0.6f, Lifetime * 1.4f );
		effect.StartVelocity = new ParticleFloat( Speed * 0.45f, Speed * 1.15f );
		effect.Damping = 1.2f;
		effect.ApplyColor = true;
		effect.ApplyAlpha = true;
		effect.ApplyShape = true;
		effect.Tint = Tint;
		effect.Stretch = Stretch;

		effect.Force = Gravity > 0.01f;
		effect.ForceDirection = Vector3.Down;
		effect.ForceScale = Gravity / 100f;

		var size = new Curve();
		size.AddPoint( 0f, StartSize );
		size.AddPoint( 1f, EndSize );
		effect.Scale = SmokeEmitter.OverLife( size );

		var fade = new Curve();
		fade.AddPoint( 0f, 1f );
		fade.AddPoint( 0.7f, 0.8f );
		fade.AddPoint( 1f, 0f );
		effect.Alpha = SmokeEmitter.OverLife( fade );

		renderer.ParticleEffect = effect;
		renderer.Sprite = picture;
		renderer.Additive = Additive;
		renderer.Lighting = false;
		renderer.Shadows = false;

		// A spark is moving fast enough to smear: the engine draws the streak, so there is nothing to draw by hand.
		renderer.FaceVelocity = Stretch > 1.01f;
		renderer.MotionBlur = false;
		renderer.LeadingTrail = Stretch > 1.01f;

		emitter.Radius = Spread;
		emitter.Loop = true;
		// No duration and looping: the engine reads that as "for ever".
		emitter.Duration = 0f;
		emitter.Rate = rate;
		emitter.Enabled = Emitting;
	}

	/// <summary>The particle component of that kind already on this object, or a new one.</summary>
	T Find<T>() where T : Component, new() =>
		GameObject.Components.Get<T>() ?? GameObject.Components.Create<T>();

	Sprite Picture => Sprite ?? (defaultSprite ??= Sprite.FromTexture( MakeSparkTexture() ));

	/// <summary>
	/// A hot little dot: white in the middle, amber by the time it fades out, so an additive spark keeps its colour
	/// instead of turning into a grey smudge.
	/// </summary>
	static Texture MakeSparkTexture()
	{
		var bitmap = new Bitmap( SparkTextureSize, SparkTextureSize );
		var centre = new Vector2( SparkTextureSize * 0.5f, SparkTextureSize * 0.5f );

		for ( var y = 0; y < SparkTextureSize; y++ )
		{
			for ( var x = 0; x < SparkTextureSize; x++ )
			{
				var offset = (new Vector2( x + 0.5f, y + 0.5f ) - centre) / (SparkTextureSize * 0.5f);
				var distance = offset.Length;

				// A hard edge, all but a couple of pixels of it: a spark is a scrap of light, not a glow, and a soft
				// one blooms into a petal the moment the camera is close to it.
				var alpha = Math.Clamp( (0.92f - distance) / 0.12f, 0f, 1f );
				var heat = Math.Clamp( 1.5f - distance * 1.5f, 0f, 1f );

				bitmap.SetPixel( x, y, new Color(
					1f,
					0.7f + 0.3f * heat,
					0.25f + 0.75f * heat * heat,
					alpha ) );
			}
		}

		return bitmap.ToTexture();
	}

	/// <summary>
	/// Play the crackle, if enough has passed since the last one. The sound is picked, pitched and levelled a little
	/// differently every time, so a sparking camera doesn't sound like a loop.
	/// </summary>
	/// <remarks>
	/// The engine's 3D audio is switched on here rather than in the asset, so an emitter can be given its own range and
	/// nobody has to edit a shared sound file to change it: the crackle is placed in the world
	/// (<see cref="SoundHandle.SpacialBlend"/> 1), geometry blocks it (<see cref="SoundHandle.OcclusionEnabled"/>), the
	/// air soaks up the top of it (<see cref="SoundHandle.AirAbsorption"/>), and <see cref="SoundHandle.ReverbEnabled"/>
	/// lets it trace reflections so it reaches the player round a corner off a wall rather than only in a straight line.
	/// That tracing needs surfaces with acoustic properties (<c>AudioSurface</c>) to bounce off; without them it is simply
	/// occluded.
	/// </remarks>
	void CrackleSound()
	{
		if ( sinceCrackle < CrackleGap )
			return;

		sinceCrackle = 0;

		var volume = CrackleVolume * (0.8f + System.Random.Shared.NextSingle() * 0.2f);

		var handle = Crackle is not null
			? Sound.Play( Crackle, WorldPosition, volume )
			: Sound.Play( DefaultCrackle, WorldPosition, volume );

		if ( handle is null || !handle.IsValid() )
			return;

		handle.Distance = HearingRange;
		handle.SpacialBlend = 1f;
		handle.ReverbEnabled = true;

		// Air absorption off: a crackle is almost all top end, which is the first thing the air takes away, and with it
		// on there was nothing left of the sound by the time it reached the player.
		handle.AirAbsorption = false;

		// Occlusion off: the emitter sits inside the camera's own housing, and a sound traced from in there comes out
		// blocked by the object it belongs to. A crackle is a noise at the surface anyway.
		handle.OcclusionEnabled = false;

		// A crackle is a dry noise: let it be heard off the walls, but don't have it fill the room with reverb.
		handle.Reverb = 0.35f;

		handle.Pitch = 0.92f + System.Random.Shared.NextSingle() * 0.2f;
	}

	/// <summary>A single bulb for all the sparks, brightened as they fly.</summary>
	void Flicker( float brightness )
	{
		if ( !Light || !GameObject.IsValid() )
			return;

		if ( !bulb.IsValid() )
		{
			if ( brightness <= 0f )
				return;

			var go = new GameObject( GameObject, true, "Spark Light" );
			go.Flags |= GameObjectFlags.NotSaved;
			go.LocalPosition = Vector3.Zero;

			bulb = go.Components.Create<PointLight>();
			bulb.Radius = LightRadius;
			bulb.Attenuation = 1f;
			bulb.Shadows = false;
		}

		bulb.LightColor = new Color( Tint.r, Tint.g * 0.85f, Tint.b * 0.6f ) * brightness;
	}
}
