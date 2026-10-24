using System;

namespace Sandbox;

/// <summary>
/// An electrical spark, made by <see cref="SparkEmitter"/>: a bright scrap that flies out of the emitter, falls under
/// gravity, shrinks, and flickers out. It belongs to nothing, so it is never saved and never sent.
/// </summary>
public sealed class SparkPuff : Component
{
	/// <summary>The billboard the spark is drawn with.</summary>
	public SpriteRenderer Renderer { get; set; }

	/// <summary>How long the spark lives, in seconds.</summary>
	public float Lifetime { get; set; } = 0.35f;

	/// <summary>How big it is when it appears, and when it goes, in world units across.</summary>
	public float StartSize { get; set; } = 2.6f;
	public float EndSize { get; set; } = 0.6f;

	/// <summary>How many times taller than wide it is drawn: a streak, not a dot.</summary>
	public float Stretch { get; set; } = 3.5f;

	/// <summary>How hard gravity pulls it down, in units a second squared.</summary>
	public float Gravity { get; set; } = 220f;

	/// <summary>Where it is going, in units a second, and where it was fired from inside the emitter.</summary>
	public Vector3 Velocity { get; set; }

	/// <summary>Its colour, alpha included.</summary>
	public Color Tint { get; set; } = new( 1f, 0.9f, 0.6f, 1f );

	TimeSince age;

	/// <summary>How far through its life the spark is, from 0 to 1.</summary>
	float Progress => Math.Clamp( (float)age / MathF.Max( Lifetime, 0.01f ), 0f, 1f );

	protected override void OnEnabled() => age = 0;

	protected override void OnUpdate()
	{
		var progress = Progress;

		if ( progress >= 1f )
		{
			GameObject.Destroy();

			return;
		}

		Velocity += Vector3.Down * Gravity * Time.Delta;
		WorldPosition += Velocity * Time.Delta;

		if ( !Renderer.IsValid() )
			return;

		var size = StartSize.LerpTo( EndSize, progress );
		Renderer.Size = new Vector2( size, size * Stretch );

		// A spark is never steady: its brightness jumps from frame to frame, then it goes out.
		var colour = Tint;
		colour.a = Tint.a * (0.45f + System.Random.Shared.NextSingle() * 0.55f) * (1f - progress);

		Renderer.Color = colour;
	}
}
