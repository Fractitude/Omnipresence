using System;

namespace Sandbox;

/// <summary>
/// Electric sparks: short-lived bright scraps that fly out, fall, and crackle out, thrown in clumps the way a short
/// circuit throws them rather than one at a time. Drop it on anything that should spark — a broken camera, a cut cable,
/// a machine with a bad earth — and set <see cref="Emitting"/> to turn it on and off.
/// </summary>
/// <remarks>
/// Like the smoke, the sparks are made here rather than in the scene: each is a billboard that destroys itself, marked
/// not saved and never networked, so every machine makes the sparks it sees. The optional light is a single point light
/// on the emitter, flickering with the clumps, which is what makes the sparks read at night.
/// </remarks>
[Title( "Spark Emitter" ), Icon( "bolt" )]
public sealed class SparkEmitter : Component
{
	/// <summary>The side of the spark texture made here, in pixels.</summary>
	const int SparkTextureSize = 32;

	/// <summary>Whether sparks are coming off it right now.</summary>
	[Property] public bool Emitting { get; set; } = true;

	/// <summary>How often a clump of sparks is thrown, in seconds.</summary>
	[Property, Range( 0.02f, 3f )]
	public float Interval { get; set; } = 0.07f;

	/// <summary>How many sparks are in a clump.</summary>
	[Property, Range( 1, 30 )]
	public int Count { get; set; } = 14;

	/// <summary>How long a spark lives, in seconds. Each one differs a little from this.</summary>
	[Property, Range( 0.05f, 2f )]
	public float Lifetime { get; set; } = 0.18f;

	/// <summary>How fast a spark leaves the emitter, in units a second.</summary>
	[Property, Range( 0f, 1000f )]
	public float Speed { get; set; } = 260f;

	/// <summary>How hard they are pulled down, in units a second squared.</summary>
	[Property, Range( 0f, 1000f )]
	public float Gravity { get; set; } = 600f;

	/// <summary>
	/// How much of every spark's direction is up, from 0 (anywhere) to 1 (straight up): a little makes them arc out of
	/// the emitter like a short, none makes them spray every way.
	/// </summary>
	[Property, Range( 0f, 1f )]
	public float UpBias { get; set; } = 0.05f;

	/// <summary>How far from the emitter a spark may start, in units.</summary>
	[Property, Range( 0f, 50f )]
	public float Spread { get; set; } = 2.5f;

	/// <summary>How big a spark is when it appears, and when it goes out, in units across.</summary>
	[Property, Range( 0.5f, 100f )]
	public float StartSize { get; set; } = 1.8f;

	[Property, Range( 0.2f, 100f )]
	public float EndSize { get; set; } = 0.4f;

	/// <summary>
	/// How many times taller than wide a spark is drawn: a streak rather than a dot, which is what a fast spark looks
	/// like. 1 makes it round.
	/// </summary>
	[Property, Range( 1f, 12f )]
	public float Stretch { get; set; } = 2f;

	/// <summary>The sparks' colour. Drawn additively, so a whiter colour reads as a hotter spark.</summary>
	[Property] public Color Tint { get; set; } = new( 1f, 0.88f, 0.6f, 1f );

	/// <summary>
	/// The picture a spark is drawn with. Leave it empty for the bright dot the emitter makes itself; any sprite works.
	/// </summary>
	[Property] public Sprite Sprite { get; set; }

	/// <summary>
	/// Whether the sparks are drawn additively. Additive is the hotter look on a dark background, but it disappears
	/// against a bright sky; off, the sparks keep their own colour wherever they are.
	/// </summary>
	[Property] public bool Additive { get; set; }

	/// <summary>
	/// Whether each clump also flashes where it was thrown from: one large spark that lives a few frames. It is what
	/// reads as the arc itself, with the sparks as what it throws off.
	/// </summary>
	[Property] public bool Flash { get; set; } = true;

	/// <summary>Whether the sparks light what is around them, with one point light on the emitter.</summary>
	[Property] public bool Light { get; set; } = true;

	/// <summary>How far the light reaches, in units.</summary>
	[Property, Range( 0f, 1000f ), ShowIf( nameof( Light ), true )]
	public float LightRadius { get; set; } = 90f;

	/// <summary>How much of the next clump has been earned, so the clumps come out evenly.</summary>
	float pending;

	Sprite defaultSprite;

	PointLight bulb;

	protected override void OnEnabled() => pending = Interval;

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

	protected override void OnUpdate()
	{
		if ( !Emitting )
		{
			pending = 0f;
			Flicker( 0f );

			return;
		}

		pending += Time.Delta;

		while ( pending >= Interval )
		{
			pending -= Interval;
			Clump();
		}

		Flicker( 0.55f + System.Random.Shared.NextSingle() * 0.45f );
	}

	void Clump()
	{
		if ( !Picture.IsValid() )
			return;

		if ( Flash )
			MakeSpark( FlashSize, FlashLifetime );

		for ( var i = 0; i < Count; i++ )
			MakeSpark( StartSize, Lifetime * (0.6f + System.Random.Shared.NextSingle() * 0.8f) );

		Flicker( 1.6f );
	}

	/// <summary>How big the flash is, and how long it lasts.</summary>
	const float FlashSize = 9f, FlashLifetime = 0.05f;

	void MakeSpark( float size, float lifetime )
	{
		var go = new GameObject( true, "Spark" );
		go.Flags |= GameObjectFlags.NotSaved;
		go.NetworkMode = NetworkMode.Never;

		go.WorldPosition = WorldPosition + new Vector3(
			System.Random.Shared.NextSingle() * 2f - 1f,
			System.Random.Shared.NextSingle() * 2f - 1f,
			System.Random.Shared.NextSingle() * 2f - 1f ) * Spread;

		var renderer = go.Components.Create<SpriteRenderer>();
		renderer.Sprite = Picture;
		renderer.Size = new Vector2( size, size * Stretch );
		renderer.Color = Tint;
		renderer.Additive = Additive;
		renderer.Lighting = false;
		renderer.Shadows = false;
		renderer.AlphaCutoff = 0f;

		var direction = new Vector3(
			System.Random.Shared.NextSingle() * 2f - 1f,
			System.Random.Shared.NextSingle() * 2f - 1f,
			System.Random.Shared.NextSingle() * 2f - 1f ).Normal + Vector3.Up * UpBias;

		var spark = go.Components.Create<SparkPuff>();
		spark.Renderer = renderer;
		spark.Lifetime = lifetime;
		spark.StartSize = size;
		spark.EndSize = size * (EndSize / MathF.Max( StartSize, 0.01f ));
		spark.Stretch = Stretch;
		spark.Gravity = Gravity;
		spark.Tint = Tint;
		spark.Velocity = direction.Normal * Speed * (0.55f + System.Random.Shared.NextSingle() * 0.75f);
	}

	/// <summary>A single bulb for all the sparks, brightened as a clump goes off.</summary>
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
