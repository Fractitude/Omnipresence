namespace Sandbox;

/// <summary>
/// Shakes nearby cameras when a physics prop strikes static world geometry.
/// Listening at scene level also covers map props, duplicates and restored saves.
/// </summary>
public sealed partial class GameManager : ISceneCollisionEvents
{
	const float MinimumImpactSpeed = 100f;
	const float MinimumAmplitude = 0.1f;
	const float MaximumAmplitude = 4f;
	const float ShakeRadius = 512f;
	const float ShakeDuration = 0.32f;
	const float ImpactCooldown = 0.1f;

	readonly record struct Impact( Vector3 Position, float Amplitude );

	// Not readonly: a hot reload would reset these (see "Static state and hot reload" in AGENTS.md).
	Dictionary<Prop, Impact> pendingImpacts = new();
	Dictionary<Prop, float> nextImpactTime = new();
	PropImpactCameraEffect propImpactEffect;

	void IScenePhysicsEvents.PostPhysicsStep() => ProcessImpacts();

	void ISceneCollisionEvents.OnCollisionHit( Collision collision )
	{
		if ( Scene.IsEditor || !Networking.IsHost ) return;
		if ( collision.Self.IsTrigger || collision.Other.IsTrigger ) return;

		// The physics event can report the prop on either side of the contact.
		QueueImpact( collision.Self, collision.Other, collision.Contact );
		QueueImpact( collision.Other, collision.Self, collision.Contact );
	}

	void QueueImpact( CollisionSource source, CollisionSource world, PhysicsContact contact )
	{
		if ( !source.Body.IsValid() || !world.Body.IsValid() ) return;
		if ( source.Body.BodyType != PhysicsBodyType.Dynamic || !source.Body.MotionEnabled ) return;
		if ( world.Body.BodyType != PhysicsBodyType.Static ) return;
		if ( !source.GameObject.IsValid() ) return;

		var prop = source.GameObject.GetComponentInParent<Prop>();
		if ( !prop.IsValid() || prop.IsProxy ) return;

		// Frozen/static props are still props, rather than part of the world.
		if ( world.GameObject.IsValid() && world.GameObject.GetComponentInParent<Prop>().IsValid() ) return;

		var speed = MathF.Abs( contact.NormalSpeed );
		if ( speed < MinimumImpactSpeed ) return;

		// Use the body's current mass so the mass tool and model physics both apply.
		// A 50 kg prop hitting at 200 units/sec produces one unit of shake at the impact.
		var amplitude = (source.Body.Mass * speed / 10000f).Clamp( 0f, MaximumAmplitude );

		if ( amplitude < MinimumAmplitude ) return;
		if ( nextImpactTime.TryGetValue( prop, out var nextTime ) && Time.Now < nextTime ) return;

		// Several shapes/substeps can report the same impact; keep only the strongest.
		if ( pendingImpacts.TryGetValue( prop, out var pending ) && pending.Amplitude >= amplitude ) return;
		pendingImpacts[prop] = new Impact( contact.Point, amplitude );
	}

	void ProcessImpacts()
	{
		foreach ( var prop in nextImpactTime.Keys.Where( x => !x.IsValid() || Time.Now >= nextImpactTime[x] )
			         .ToArray() )
			nextImpactTime.Remove( prop );

		foreach ( var (prop, impact) in pendingImpacts )
		{
			BroadcastImpact( impact.Position, impact.Amplitude );
			if ( prop.IsValid() )
				nextImpactTime[prop] = Time.Now + ImpactCooldown;
		}

		pendingImpacts.Clear();
	}

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Unreliable )]
	void BroadcastImpact( Vector3 position, float amplitude )
	{
		if ( Scene.IsEditor || GamePreferences.Screenshake <= 0f ) return;

		var effects = CameraEffectSystem.Get( Scene );
		if ( effects is null ) return;
		effects.Scale = GamePreferences.Screenshake;
		var camera = Scene.Camera;
		if ( !camera.IsValid() ) return;
		if ( camera.WorldPosition.Distance( position ) >= ShakeRadius ) return;

		// A pile of colliding props should produce one readable jolt, not stacked jitter.
		if ( propImpactEffect is { IsDone: false } active )
		{
			var incomingStrength = amplitude * (1f - camera.WorldPosition.Distance( position ) / ShakeRadius);
			var activeStrength = active.Amplitude * active.ScaleFor( camera );
			if ( active.TimeAlive < 0.12f && incomingStrength <= activeStrength ) return;
			active.Stop();
		}

		var direction = (position - camera.View.Position).Normal;
		propImpactEffect = effects.Add( new PropImpactCameraEffect
		{
			Camera = camera,
			Epicenter = position,
			Radius = ShakeRadius,
			Duration = ShakeDuration,
			Amplitude = amplitude,
			Side = camera.View.Rotation.Right.Dot( direction )
		} );
	}
}
