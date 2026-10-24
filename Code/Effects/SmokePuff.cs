namespace Sandbox;

/// <summary>
/// One puff of smoke, made by <see cref="SmokeEmitter"/>: a billboard that rises, grows, and fades until it destroys
/// itself. It belongs to nothing, so it is never saved and never sent: every machine that sees smoke makes its own.
/// </summary>
public sealed class SmokePuff : Component
{
	/// <summary>The billboard the puff is drawn with.</summary>
	public SpriteRenderer Renderer { get; set; }

	/// <summary>How long the puff lives, in seconds.</summary>
	public float Lifetime { get; set; } = 2f;

	/// <summary>How big it is when it appears, and when it goes, in world units across.</summary>
	public float StartSize { get; set; } = 6f;
	public float EndSize { get; set; } = 30f;

	/// <summary>How fast it climbs, in units a second, and the sideway drift it was given when it was made.</summary>
	public float Rise { get; set; } = 40f;
	public Vector3 Velocity { get; set; }

	/// <summary>Its colour, alpha included: how thick the smoke is.</summary>
	public Color Tint { get; set; } = new( 0.3f, 0.29f, 0.28f, 0.6f );

	TimeSince age;

	/// <summary>How far through its life the puff is, from 0 to 1.</summary>
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

		if ( Renderer.IsValid() )
		{
			var size = StartSize.LerpTo( EndSize, progress );
			Renderer.Size = new Vector2( size, size );

			// In quickly, out slowly: a puff that arrives at full strength pops.
			var colour = Tint;
			colour.a = Tint.a * MathF.Min( progress * 5f, 1f ) * (1f - progress * progress);

			Renderer.Color = colour;
		}

		WorldPosition += (Velocity + Vector3.Up * Rise) * Time.Delta;
	}
}
