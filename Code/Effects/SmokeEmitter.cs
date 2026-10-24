using System;

namespace Sandbox;

/// <summary>
/// A small smoke plume: puffs that rise, drift, grow and fade. Drop it on anything that should smoke — a broken camera,
/// a chimney, a wreck — and set <see cref="Emitting"/> to turn it on and off. The puffs already in the air finish their
/// life rather than vanishing with it.
/// </summary>
/// <remarks>
/// The smoke is made here, not in the scene: each puff is a billboard that destroys itself when its life runs out, and
/// nothing about them is saved or sent, so every machine makes the smoke it sees. A plume at the default rate costs a
/// dozen billboards at most.
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
	public float Rate { get; set; } = 10f;

	/// <summary>How long a puff lives, in seconds.</summary>
	[Property, Range( 0.2f, 10f )]
	public float Lifetime { get; set; } = 2.4f;

	/// <summary>How fast a puff climbs, in units a second.</summary>
	[Property, Range( 0f, 400f )]
	public float Rise { get; set; } = 26f;

	/// <summary>How far a puff may wander sideways, in units a second.</summary>
	[Property, Range( 0f, 200f )]
	public float Wind { get; set; } = 6f;

	/// <summary>How far from the emitter a puff may start, in units.</summary>
	[Property, Range( 0f, 100f )]
	public float Spread { get; set; } = 2f;

	/// <summary>How big a puff is when it appears, and when it goes, in units across.</summary>
	[Property, Range( 1f, 200f )]
	public float StartSize { get; set; } = 10f;

	[Property, Range( 1f, 400f )]
	public float EndSize { get; set; } = 44f;

	/// <summary>The smoke's colour, alpha included: lower alpha is thinner smoke.</summary>
	[Property] public Color Tint { get; set; } = new( 0.24f, 0.23f, 0.22f, 0.75f );

	/// <summary>
	/// The picture a puff is drawn with. Leave it empty for the engine's smoke sheet; a spark, dust or ash sprite works
	/// just as well.
	/// </summary>
	[Property] public Sprite Sprite { get; set; }

	/// <summary>How much of the next puff has been earned by the rate, so it comes out evenly.</summary>
	float pending;

	Sprite defaultSprite;

	Sprite Picture => Sprite ?? (defaultSprite ??= Sprite.FromTexture( MakePuffTexture() ));

	/// <summary>
	/// The puff a plume uses when it is given no sprite: a soft ragged disc, with the edges torn up so a cloud of them
	/// doesn't read as a string of bubbles. Made here rather than loaded, so it fills its own square and the sizes below
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

				var fill = Math.Clamp( 1f - distance / rim, 0f, 1f );
				var alpha = fill * fill * (0.75f + 0.25f * System.Random.Shared.NextSingle());

				var grey = 0.82f + 0.18f * System.Random.Shared.NextSingle();
				bitmap.SetPixel( x, y, new Color( grey, grey, grey, Math.Clamp( alpha, 0f, 1f ) ) );
			}
		}

		return bitmap.ToTexture();
	}

	protected override void OnUpdate()
	{
		if ( !Emitting || Rate <= 0f )
		{
			pending = 0f;

			return;
		}

		pending += Rate * Time.Delta;

		while ( pending >= 1f )
		{
			pending -= 1f;
			MakePuff();
		}
	}

	void MakePuff()
	{
		if ( !Picture.IsValid() )
			return;

		var go = new GameObject( true, "Smoke Puff" );
		go.Flags |= GameObjectFlags.NotSaved;
		go.NetworkMode = NetworkMode.Never;

		var jitter = new Vector3(
			System.Random.Shared.NextSingle() * 2f - 1f,
			System.Random.Shared.NextSingle() * 2f - 1f,
			System.Random.Shared.NextSingle() * 2f - 1f );

		go.WorldPosition = WorldPosition + jitter * Spread;

		var renderer = go.Components.Create<SpriteRenderer>();
		renderer.Sprite = Picture;
		renderer.Size = new Vector2( StartSize, StartSize );
		renderer.Color = Tint;

		// Smoke is its own light: lit billboards go dark in a corner and cast shadows nothing asked for.
		renderer.Lighting = false;
		renderer.Shadows = false;

		// No cutout: the puff is soft all the way to its edge, and a cutout turns its faint rim into speckles.
		renderer.AlphaCutoff = 0f;

		var angle = System.Random.Shared.NextSingle() * MathF.PI * 2f;

		var puff = go.Components.Create<SmokePuff>();
		puff.Renderer = renderer;
		puff.Lifetime = Lifetime;
		puff.StartSize = StartSize;
		puff.EndSize = EndSize;
		puff.Rise = Rise;
		puff.Tint = Tint;
		var drift = Wind * (0.4f + System.Random.Shared.NextSingle() * 0.6f);
		puff.Velocity = new Vector3( MathF.Cos( angle ), MathF.Sin( angle ), 0f ) * drift;
	}
}
