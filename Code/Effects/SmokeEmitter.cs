using System;

namespace Sandbox;

/// <summary>
/// A small smoke plume: puffs that rise, drift, grow and fade. Drop it on anything that should smoke — a broken camera,
/// a chimney, a wreck — and set <see cref="Emitting"/> to turn it on and off. The puffs already in the air finish their
/// life rather than vanishing with it.
/// </summary>
/// <remarks>
/// The puffs are the engine's, not ours: this component builds a <see cref="ParticleEffect"/> with a
/// <see cref="ParticleSpriteRenderer"/> and a <see cref="ParticleSphereEmitter"/> on its own GameObject, and only maps
/// the handful of properties below onto them. Nothing about the plume is saved or sent, so every machine makes the
/// smoke it sees.
/// </remarks>
[Title( "Smoke Emitter" ), Icon( "cloud" )]
public sealed class SmokeEmitter : Component
{
	/// <summary>
	/// The side of the puff texture made here, in pixels. Small on purpose: it is a soft blob, and the screen almost
	/// never sees it closer than a few feet.
	/// </summary>
	const int PuffTextureSize = 128;

	/// <summary>Whether the plume is making smoke right now.</summary>
	[Property] public bool Emitting { get; set; } = true;

	/// <summary>How many puffs a second.</summary>
	[Property, Range( 0.1f, 40f )]
	public float Rate { get; set; } = 12f;

	/// <summary>How long a puff lives, in seconds. Each one differs a little from this.</summary>
	[Property, Range( 0.2f, 10f )]
	public float Lifetime { get; set; } = 2.4f;

	/// <summary>How hard a puff is pushed up, in units a second squared.</summary>
	[Property, Range( 0f, 400f )]
	public float Rise { get; set; } = 120f;

	/// <summary>How hard a puff is pushed sideways, in units a second squared.</summary>
	[Property, Range( 0f, 200f )]
	public float Wind { get; set; } = 10f;

	/// <summary>How far from the emitter a puff may start, in units.</summary>
	[Property, Range( 0f, 100f )]
	public float Spread { get; set; } = 2f;

	/// <summary>How big a puff is when it appears, and when it goes, in units across.</summary>
	[Property, Range( 1f, 200f )]
	public float StartSize { get; set; } = 12f;

	[Property, Range( 1f, 400f )]
	public float EndSize { get; set; } = 55f;

	/// <summary>How thick the smoke is where it is thickest, 0 to 1: lower is wispier.</summary>
	[Property, Range( 0f, 1f )]
	public float Thickness { get; set; } = 0.85f;

	/// <summary>The smoke's colour. Its alpha multiplies <see cref="Thickness"/>.</summary>
	[Property] public Color Tint { get; set; } = new( 0.24f, 0.23f, 0.22f, 1f );

	/// <summary>
	/// The picture a puff is drawn with. Leave it empty for the soft ragged disc the emitter makes itself; any sprite
	/// works.
	/// </summary>
	[Property] public Sprite Sprite { get; set; }

	ParticleEffect effect;
	ParticleSpriteRenderer renderer;
	ParticleSphereEmitter emitter;
	Sprite defaultSprite;

	protected override void OnEnabled()
	{
		Build();
	}

	protected override void OnUpdate()
	{
		if ( !emitter.IsValid() )
			Build();

		if ( emitter.IsValid() )
			emitter.Enabled = Emitting;
	}

	/// <summary>
	/// Make the particle components this plume is drawn by, or find the ones already on the object, and set them up.
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

		effect.MaxParticles = Math.Clamp( (int)MathF.Ceiling( Rate * Lifetime ) + 8, 8, 512 );
		effect.Lifetime = new ParticleFloat( Lifetime * 0.85f, Lifetime * 1.15f );
		effect.StartVelocity = new ParticleFloat( 2f, 12f );
		effect.Damping = 0.8f;
		effect.ApplyColor = true;
		effect.ApplyAlpha = true;
		effect.ApplyShape = true;
		effect.Tint = Tint;

		// Rise and wind are one force, because the engine has one: up, leaning the way the emitter leans.
		var push = Vector3.Up * Rise + WorldRotation.Right * Wind;
		var leaning = push.Length > 0.01f;

		effect.Force = leaning;
		effect.ForceDirection = leaning ? push.Normal : Vector3.Up;
		effect.ForceScale = push.Length / 100f;

		var size = new Curve();
		size.AddPoint( 0f, StartSize );
		size.AddPoint( 1f, EndSize );
		effect.Scale = OverLife( size );

		// A puff comes in thin, thickens as it leaves the vent, then thins out as it climbs.
		var fade = new Curve();
		fade.AddPoint( 0f, 0f );
		fade.AddPoint( 0.25f, Thickness );
		fade.AddPoint( 1f, 0f );
		effect.Alpha = OverLife( fade );

		renderer.ParticleEffect = effect;
		renderer.Sprite = picture;

		// Smoke is its own light: lit particles go dark in a corner and cast shadows nothing asked for.
		renderer.Lighting = false;
		renderer.Shadows = false;
		renderer.Additive = false;

		emitter.Radius = Spread;
		emitter.Loop = true;
		// No duration and looping: the engine reads that as "for ever", and one loop restarting every second
		// leaves a gap in the plume.
		emitter.Duration = 0f;
		emitter.Rate = Rate;
		emitter.Enabled = Emitting;
	}

	/// <summary>A curve that is read over the particle's life instead of being one number.</summary>
	internal static ParticleFloat OverLife( Curve curve ) =>
		new() { Type = ParticleFloat.ValueType.Curve, Evaluation = ParticleFloat.EvaluationType.Life, CurveA = curve };

	/// <summary>The particle component of that kind already on this object, or a new one.</summary>
	T Find<T>() where T : Component, new() =>
		GameObject.Components.Get<T>() ?? GameObject.Components.Create<T>();

	Sprite Picture => Sprite ?? (defaultSprite ??= Sprite.FromTexture( MakePuffTexture() ));

	/// <summary>
	/// The puff a plume uses when it is given no sprite: a soft ragged disc, with the edges torn up so a cloud of them
	/// doesn't read as a string of bubbles. Made here rather than loaded, so it fills its own square and the sizes above
	/// mean what they say.
	/// </summary>
	static Texture MakePuffTexture()
	{
		var bitmap = new Bitmap( PuffTextureSize, PuffTextureSize );
		var centre = new Vector2( PuffTextureSize * 0.5f, PuffTextureSize * 0.5f );

		for ( var y = 0; y < PuffTextureSize; y++ )
		{
			for ( var x = 0; x < PuffTextureSize; x++ )
			{
				var offset = (new Vector2( x + 0.5f, y + 0.5f ) - centre) / (PuffTextureSize * 0.5f);
				var distance = offset.Length;
				var angle = MathF.Atan2( offset.y, offset.x );

				// A soft round falloff with its rim pushed in and out in slow lumps: a bubble's edge without the bubbles.
				var rim = 0.82f
					+ 0.07f * MathF.Sin( angle * 2f + 0.9f )
					+ 0.05f * MathF.Sin( angle * 3f - 1.4f )
					+ 0.03f * MathF.Sin( angle * 5f + 2.6f );

				// Solid in the middle, soft at the rim: a disc that fades all the way through reads as a smudge.
				var fill = Math.Clamp( 1f - distance / rim, 0f, 1f );
				var alpha = MathF.Pow( fill, 0.6f ) * (0.85f + 0.15f * System.Random.Shared.NextSingle());

				var grey = 0.82f + 0.18f * System.Random.Shared.NextSingle();
				bitmap.SetPixel( x, y, new Color( grey, grey, grey, Math.Clamp( alpha, 0f, 1f ) ) );
			}
		}

		return bitmap.ToTexture();
	}
}
